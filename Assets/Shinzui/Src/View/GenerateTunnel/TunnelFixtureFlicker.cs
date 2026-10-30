using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace Shinzui.View.GenerateTunnel
{
    /// <summary>故障した器具の光源と発光面を同じシードで変化させる</summary>
    public sealed class TunnelFixtureFlicker : MonoBehaviour
    {
        private Light[] lights;
        private HDAdditionalLightData[] data;
        private float[] volumetric;
        private readonly List<(Renderer renderer, int slot, Color color)> emission = new();
        private MaterialPropertyBlock block;
        private float phase;
        private float started;

        /// <summary>Unityオブジェクトをメインスレッドの初期化時に用意する</summary>
        private void Awake()
        {
            block ??= new MaterialPropertyBlock();
        }

        /// <summary>故障状態と発光面の元の色を記録する</summary>
        /// <param name="seed">この器具のシード</param>
        /// <param name="dead">完全に消灯するか</param>
        public void Initialize(int seed, bool dead)
        {
            /* 非アクティブな器具ではAwakeより先に呼ばれるためここでも初期化する */
            block ??= new MaterialPropertyBlock();
            emission.Clear();
            lights = GetComponentsInChildren<Light>(true);
            data = GetComponentsInChildren<HDAdditionalLightData>(true);
            volumetric = new float[data.Length];
            for (int i = 0; i < data.Length; i++) volumetric[i] = data[i].volumetricDimmer;
            phase = (seed & 65535) * 0.001f;
            started = Time.time;
            foreach (Renderer renderer in GetComponentsInChildren<Renderer>(true))
            {
                Material[] materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                    if (materials[i] != null && materials[i].HasProperty("_EmissiveColor"))
                        emission.Add((renderer, i, materials[i].GetColor("_EmissiveColor")));
            }
            if (!dead) return;
            foreach (Light light in lights) light.enabled = false;
            Apply(0f);
            enabled = false;
        }

        /// <summary>短い断続的な減光を生成する</summary>
        private void Update()
        {
            if (data == null) return;
            float t = Time.time - started + phase;
            float window = Mathf.Repeat(t, 7.3f);
            float gain = window < 0.45f && Mathf.Sin(t * 71f) > 0f ? 0.08f : 1f;
            Apply(gain);
        }

        /// <summary>物理強度を変更せず光と発光面の倍率を揃える</summary>
        /// <param name="gain">明るさの倍率</param>
        private void Apply(float gain)
        {
            for (int i = 0; i < data.Length; i++)
            {
                if (data[i] == null) continue;
                data[i].lightDimmer = gain;
                data[i].volumetricDimmer = volumetric[i] * gain;
            }
            foreach (var entry in emission)
            {
                if (entry.renderer == null) continue;
                entry.renderer.GetPropertyBlock(block, entry.slot);
                block.SetColor("_EmissiveColor", entry.color * gain);
                entry.renderer.SetPropertyBlock(block, entry.slot);
            }
        }
    }
}
