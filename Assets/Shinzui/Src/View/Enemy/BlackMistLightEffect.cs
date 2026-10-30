using System.Collections.Generic;
using UnityEngine;

namespace Shinzui.View
{
    /// <summary>黒い霧の周囲を消灯し、懐中電灯へ距離に応じた干渉を与える</summary>
    [DisallowMultipleComponent]
    public sealed class BlackMistLightEffect : MonoBehaviour
    {
        [SerializeField, Min(0.1f)] private float blackoutRadius = 8f;
        [SerializeField, Min(0.1f)] private float flashlightRadius = 12f;

        private static readonly List<BlackMistLightEffect> Sources = new();
        private static readonly Dictionary<Light, (bool enabled, float intensity, float started)> SuppressedLights = new();
        private static readonly Dictionary<Renderer, (MaterialPropertyBlock original, MaterialPropertyBlock working, Color color, float started)> SuppressedEmission = new();
        private static readonly List<Light> ReleasedLights = new();
        private static readonly List<Renderer> ReleasedRenderers = new();
        private static Light[] _lights = System.Array.Empty<Light>();
        private static Renderer[] _fixtures = System.Array.Empty<Renderer>();
        private static float _nextScan;
        private EnemyView _view;

        /// <summary>干渉源を登録し、新しいシーンの照明を再収集する</summary>
        private void OnEnable()
        {
            _view = GetComponent<EnemyView>();
            Sources.Add(this);
            _nextScan = 0f;
        }

        /// <summary>干渉源を解除し、最後の敵が消えた際に照明を復元する</summary>
        private void OnDisable()
        {
            Sources.Remove(this);
            RestoreOutsideLights();
            if (Sources.Count != 0) return;

            _lights = System.Array.Empty<Light>();
            _fixtures = System.Array.Empty<Renderer>();
        }

        /// <summary>最も近い有効な霧の干渉強度を求める</summary>
        /// <param name="position">判定するワールド座標</param>
        /// <param name="flashlight">懐中電灯用の範囲で判定するか</param>
        /// <returns>範囲外で0、敵の中心で1となる強度</returns>
        public static float GetInterference(Vector3 position, bool flashlight = true)
        {
            float strength = 0f;
            foreach (var source in Sources)
            {
                if (source == null || !source.isActiveAndEnabled ||
                    (source._view != null && (!source._view.isActiveAndEnabled || source._view.IsBackstage))) continue;

                float radius = flashlight ? source.flashlightRadius : source.blackoutRadius;
                strength = Mathf.Max(strength, 1f - Vector3.Distance(position, source.transform.position) / Mathf.Max(.1f, radius));
            }
            return strength;
        }

        /// <summary>複数の敵の範囲をまとめて照明と器具の発光へ反映する</summary>
        private void LateUpdate()
        {
            if (Sources[0] != this) return;

            // 動的生成された照明も定期的に収集する
            if (Time.time >= _nextScan)
            {
                _nextScan = Time.time + .5f;
                _lights = FindObjectsByType<Light>(FindObjectsSortMode.None);
                var fixtures = new List<Renderer>();
                foreach (var renderer in FindObjectsByType<Renderer>(FindObjectsSortMode.None))
                {
                    if (renderer.GetComponentInParent<Flashlight.FlashlightView>() != null) continue;
                    if (renderer.GetComponentInParent<EnemyView>() != null) continue;
                    foreach (var material in renderer.sharedMaterials)
                    {
                        if (material == null || !material.IsKeywordEnabled("_EMISSION")) continue;
                        fixtures.Add(renderer);
                        break;
                    }
                }
                _fixtures = fixtures.ToArray();
            }

            RestoreOutsideLights();

            // 懐中電灯は専用の点滅処理に任せ、照明の元の有効状態を保持する
            foreach (var light in _lights)
            {
                if (light == null || light.type == LightType.Directional ||
                    Flashlight.FlashlightView.IsFlashlight(light) ||
                    GetInterference(light.transform.position, false) <= 0f) continue;
                if (!SuppressedLights.TryGetValue(light, out var state))
                {
                    state = (light.enabled, light.intensity, Time.time);
                    SuppressedLights.Add(light, state);
                }

                // 短い点滅の後に光量を落とし、演出終了時に消灯する
                float gain = GetBlackoutGain(Time.time - state.started);
                light.intensity = state.intensity * gain;
                light.enabled = state.enabled && gain > 0f;
            }

            // 蛍光灯の発光材質も暗くし、共有マテリアルは変更しない
            foreach (var renderer in _fixtures)
            {
                if (renderer == null || GetInterference(renderer.transform.position, false) <= 0f) continue;
                if (!SuppressedEmission.TryGetValue(renderer, out var state))
                {
                    var original = new MaterialPropertyBlock();
                    renderer.GetPropertyBlock(original);
                    var working = new MaterialPropertyBlock();
                    renderer.GetPropertyBlock(working);
                    Color color = Color.black;
                    foreach (var material in renderer.sharedMaterials)
                    {
                        if (material == null || !material.IsKeywordEnabled("_EMISSION") ||
                            !material.HasProperty("_EmissionColor")) continue;
                        color = material.GetColor("_EmissionColor");
                        break;
                    }
                    if (original.HasColor("_EmissionColor")) color = original.GetColor("_EmissionColor");
                    state = (original, working, color, Time.time);
                    SuppressedEmission.Add(renderer, state);
                }

                state.working.SetColor("_EmissionColor", state.color * GetBlackoutGain(Time.time - state.started));
                renderer.SetPropertyBlock(state.working);
            }
        }

        /// <summary>消灯開始からの経過時間に応じた点滅と減衰を求める</summary>
        /// <param name="elapsed">消灯演出の経過秒数</param>
        /// <returns>元の光量に乗算する0から1の係数</returns>
        private static float GetBlackoutGain(float elapsed)
        {
            // 0.6秒間で3回点滅させ、その後1.2秒かけて滑らかに消灯する
            const float flickerDuration = .6f;
            const float fadeDuration = 1.2f;
            if (elapsed < flickerDuration)
                return Mathf.Repeat(elapsed * 5f, 1f) < .5f ? 1f : .15f;

            return 1f - Mathf.SmoothStep(0f, 1f, (elapsed - flickerDuration) / fadeDuration);
        }

        /// <summary>すべての敵の範囲から離れた照明を元の状態へ戻す</summary>
        private static void RestoreOutsideLights()
        {
            ReleasedLights.Clear();
            foreach (var entry in SuppressedLights)
            {
                if (entry.Key != null && GetInterference(entry.Key.transform.position, false) > 0f) continue;
                if (entry.Key != null)
                {
                    entry.Key.intensity = entry.Value.intensity;
                    entry.Key.enabled = entry.Value.enabled;
                }
                ReleasedLights.Add(entry.Key);
            }
            foreach (var light in ReleasedLights) SuppressedLights.Remove(light);

            ReleasedRenderers.Clear();
            foreach (var entry in SuppressedEmission)
            {
                if (entry.Key != null && GetInterference(entry.Key.transform.position, false) > 0f) continue;
                if (entry.Key != null) entry.Key.SetPropertyBlock(entry.Value.original);
                ReleasedRenderers.Add(entry.Key);
            }
            foreach (var renderer in ReleasedRenderers) SuppressedEmission.Remove(renderer);
        }
    }
}
