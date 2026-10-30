using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace Shinzui.Infrastructure.Lighting
{
    /// <summary>生成器とアセット構築で同じ物理照明値を使用する</summary>
    public static class TunnelLookLighting
    {
        /// <summary>既知の間接光用補助ライトを識別する</summary>
        /// <param name="light">確認対象</param>
        /// <returns>削除対象の補助ライトならtrue</returns>
        public static bool IsCeilingBounce(UnityEngine.Light light)
        {
            return light != null && light.name.StartsWith("Ceiling Bounce", System.StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>床の実測距離と目標照度から器具の配光と物理強度を設定する</summary>
        /// <param name="light">設定対象</param>
        /// <param name="surfaces">同じシーンまたは生成区間の衝突面</param>
        /// <param name="meshes">衝突面がない床を測る描画面</param>
        /// <param name="fluorescent">蛍光灯として校正するか</param>
        /// <param name="spacing">隣接器具との基準間隔</param>
        /// <returns>測定方法と校正値のログ</returns>
        public static string ConfigureFixture(UnityEngine.Light light, Collider[] surfaces = null,
            Renderer[] meshes = null, bool fluorescent = false, float spacing = 12f)
        {
            if (IsCeilingBounce(light)) return "Ceiling Bounce excluded from fixture calibration";
            Vector3 point;
            Vector3 normal;
            string measurement;
            if (TryMeasureFloor(light, surfaces, out point, out normal)) measurement = "floor raycast";
            else if (TryMeasureFloorBounds(light, meshes, out point))
            {
                normal = Vector3.up;
                measurement = "named floor mesh bounds";
            }
            else
            {
                /* 単体プレハブには床がないため設置後に再測定する基準高を明示する */
                float referenceHeight = fluorescent ? 9f : 6f;
                point = light.transform.position + Vector3.down * referenceHeight;
                normal = Vector3.up;
                measurement = $"UNMEASURED reference height {referenceHeight}m; recalibrate after placement";
            }
            float distance = Vector3.Distance(light.transform.position, point);
            float height = light.transform.position.y - point.y;
            spacing = Mathf.Max(2f, spacing);
            light.type = LightType.Spot;
            light.enableSpotReflector = true;
            /* 床への到達を確保し低い天井でも器具間の中間地点を範囲内に含める */
            light.range = Mathf.Max(distance * 1.8f, 1.5f * Mathf.Sqrt(distance * distance + spacing * spacing * 0.25f));
            light.spotAngle = Mathf.Clamp(2f * Mathf.Atan(spacing * 0.8f / distance) * Mathf.Rad2Deg, 40f, 150f);
            light.innerSpotAngle = Mathf.Min(light.spotAngle - 10f, 2f * Mathf.Atan(spacing * 0.4f / distance) * Mathf.Rad2Deg);
            light.transform.rotation = Quaternion.LookRotation((point - light.transform.position).normalized, Vector3.forward);
            light.color = Color.white;
            light.useColorTemperature = true;
            light.colorTemperature = fluorescent ? 4600f : 4500f;
            light.shadows = LightShadows.Soft;
            light.shadowStrength = 1f;
            light.shadowBias = 0.025f;
            light.shadowNormalBias = 0.12f;

            /* HDRPの逆二乗減衰と距離窓と床面の余弦を使って軸上照度を合わせる */
            /* 蛍光灯は暗所設定と移行直後の対数的な中間へ寄せ側道も同じ約2.27段持ち上げる */
            float targetLux = fluorescent ? 7f : 5.31f;
            float cosine = Mathf.Max(0.2f, Vector3.Dot(normal, (light.transform.position - point).normalized));
            float window = DistanceWindow(distance, light.range);
            float candela = targetLux * distance * distance / (window * cosine);
            var data = HdrpLightUtility.SetIntensity(light, candela, LightUnit.Candela);
            data.lightDimmer = 1f;
            data.applyRangeAttenuation = true;
            data.affectDiffuse = true;
            data.useContactShadow.useOverride = true;
            data.useContactShadow.@override = true;
            data.affectsVolumetric = true;
            data.volumetricDimmer = 0.35f;
            data.SetShadowResolution(512);
            /* 旧クッキーによる未知の減衰を避け器具の配光をスポット角で定義する */
            light.cookie = null;
            float lumens = LightUnitUtils.ConvertIntensity(light, light.intensity, LightUnit.Candela, LightUnit.Lumen);
            float midpoint = EvaluateFloorLux(candela, distance, spacing * 0.5f, light.range, light.spotAngle, light.innerSpotAngle);
            return $"{(fluorescent ? "fluorescent" : "LED")}, {measurement}, height={height:F2}m, range={light.range:F2}m, outer/inner={light.spotAngle:F1}/{light.innerSpotAngle:F1}deg, intensity={candela:F0}cd (~{lumens:F0}lm), floor target={targetLux:F2}lux, ideal two-light midpoint={midpoint * 2f:F1}lux (no shadows/cookie/fog)";
        }

        /// <summary>HDRPと同じ距離範囲の滑らかな減衰窓を求める</summary>
        /// <param name="distance">光源からの距離</param>
        /// <param name="range">光の有効範囲</param>
        /// <returns>逆二乗減衰以外の倍率</returns>
        private static float DistanceWindow(float distance, float range)
        {
            float window = Mathf.Clamp01(1f - Mathf.Pow(distance / range, 4f));
            return window * window;
        }

        /// <summary>水平床に置いた確認点の直接照度を見積もる</summary>
        /// <param name="candela">軸上光度</param>
        /// <param name="height">取付高</param>
        /// <param name="offset">光源直下からの水平距離</param>
        /// <param name="range">光の有効範囲</param>
        /// <param name="outer">外側の全角</param>
        /// <param name="inner">内側の全角</param>
        /// <returns>遮蔽のない床での照度</returns>
        private static float EvaluateFloorLux(float candela, float height, float offset, float range, float outer, float inner)
        {
            float distance = Mathf.Sqrt(height * height + offset * offset);
            float cosine = height / distance;
            float outerCos = Mathf.Cos(outer * 0.5f * Mathf.Deg2Rad);
            float innerCos = Mathf.Cos(inner * 0.5f * Mathf.Deg2Rad);
            float angular = Mathf.Clamp01((cosine - outerCos) / Mathf.Max(0.001f, innerCos - outerCos));
            return candela / (distance * distance) * DistanceWindow(distance, range) * angular * angular * cosine;
        }

        /// <summary>器具の真下にある上向きの構造面を測る</summary>
        /// <param name="light">測定するライト</param>
        /// <param name="surfaces">同じシーンの衝突面</param>
        /// <param name="point">床の位置</param>
        /// <param name="normal">床の法線</param>
        /// <returns>床を測れた場合はtrue</returns>
        private static bool TryMeasureFloor(UnityEngine.Light light, Collider[] surfaces, out Vector3 point, out Vector3 normal)
        {
            point = default;
            normal = Vector3.up;
            float nearest = float.PositiveInfinity;
            int bestPriority = int.MaxValue;
            if (surfaces == null) return false;
            foreach (Collider surface in surfaces)
            {
                if (surface == null || !surface.enabled || !surface.gameObject.activeInHierarchy || surface.isTrigger || surface.attachedRigidbody != null) continue;
                string name = surface.name.ToLowerInvariant();
                if (name.Contains("ceiling") || name.Contains("header") || name.Contains("light") || name.Contains("fixture")) continue;
                int priority = name.Contains("floor") || name.Contains("road") || name.Contains("walkway") ? 0 : 1;
                Vector3 start = light.transform.position;
                float remaining = 80f;
                /* 複合メッシュの天井を通過して床まで探索する */
                for (int layer = 0; layer < 4 && remaining > 0f; layer++)
                {
                    if (!surface.Raycast(new Ray(start, Vector3.down), out RaycastHit hit, remaining)) break;
                    float height = light.transform.position.y - hit.point.y;
                    if (hit.normal.y > 0.6f && height >= 1f && (priority < bestPriority || priority == bestPriority && height < nearest))
                    {
                        bestPriority = priority;
                        nearest = height;
                        point = hit.point;
                        normal = hit.normal;
                    }
                    float step = hit.distance + 0.02f;
                    start += Vector3.down * step;
                    remaining -= step;
                }
            }
            return !float.IsPositiveInfinity(nearest);
        }

        /// <summary>衝突面がない場合は既知の床メッシュの上面で取付高を補う</summary>
        /// <param name="light">測定するライト</param>
        /// <param name="meshes">同じシーンの描画面</param>
        /// <param name="point">床上面の位置</param>
        /// <returns>対応する床がある場合はtrue</returns>
        private static bool TryMeasureFloorBounds(UnityEngine.Light light, Renderer[] meshes, out Vector3 point)
        {
            point = default;
            if (meshes == null) return false;
            float nearest = float.PositiveInfinity;
            Vector3 position = light.transform.position;
            foreach (Renderer mesh in meshes)
            {
                if (mesh == null || !mesh.enabled || !mesh.gameObject.activeInHierarchy) continue;
                string name = mesh.name.ToLowerInvariant();
                if (!name.Contains("floor") && !name.Contains("road") && !name.Contains("walkway")) continue;
                Bounds bounds = mesh.bounds;
                if (position.x < bounds.min.x || position.x > bounds.max.x || position.z < bounds.min.z || position.z > bounds.max.z) continue;
                float height = position.y - bounds.max.y;
                if (height < 1f || height >= nearest) continue;
                nearest = height;
                point = new Vector3(position.x, bounds.max.y, position.z);
            }
            return !float.IsPositiveInfinity(nearest);
        }

        /// <summary>中心光と広い周辺光を持つ線形配光を評価する</summary>
        /// <param name="radius">クッキーの中心から外周までを1とする距離</param>
        /// <returns>中心光度に対する倍率</returns>
        public static float FlashlightCookie(float radius)
        {
            float core = 0.75f * Mathf.Exp(-Mathf.Pow(radius / 0.32f, 2.4f));
            float spill = 0.25f * Mathf.Exp(-radius * radius * 0.8f);
            float edge = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.78f, 1f, radius));
            return Mathf.Clamp01((core + spill) * edge);
        }

        /// <summary>HDRPの角度減衰とクッキーを立体角で積分して中心光度を求める</summary>
        /// <param name="withCookie">共通の配光クッキーを使うか</param>
        /// <returns>3000lmを出射する中心光度</returns>
        public static float FlashlightCandela(bool withCookie)
        {
            const int samples = 256;
            float outer = 35f * Mathf.Deg2Rad;
            float innerCos = Mathf.Cos(30f * Mathf.Deg2Rad);
            float outerCos = Mathf.Cos(outer);
            float solidAngle = 0f;
            for (int i = 0; i < samples; i++)
            {
                float angle = (i + 0.5f) * outer / samples;
                float attenuation = Mathf.Clamp01((Mathf.Cos(angle) - outerCos) / (innerCos - outerCos));
                float cookie = withCookie ? FlashlightCookie(Mathf.Tan(angle) / Mathf.Tan(outer)) : 1f;
                solidAngle += cookie * attenuation * attenuation * 2f * Mathf.PI * Mathf.Sin(angle) * outer / samples;
            }
            return 3000f / solidAngle;
        }

        /// <summary>懐中電灯を主光源として配光と強度を設定する</summary>
        /// <param name="light">設定対象</param>
        /// <param name="cookie">中心光と周辺光を持つクッキー</param>
        public static void ConfigureFlashlight(UnityEngine.Light light, Texture cookie)
        {
            light.type = LightType.Spot;
            light.range = 35f;
            light.spotAngle = 70f;
            light.innerSpotAngle = 60f;
            light.enableSpotReflector = true;
            light.color = Color.white;
            light.useColorTemperature = true;
            light.colorTemperature = 5000f;
            light.cookie = cookie;
            light.shadows = LightShadows.Soft;
            light.shadowStrength = 1f;
            /* クッキーと角度減衰を含む配光の積分で実際の出射光束を3000lmへ揃える */
            float candela = FlashlightCandela(cookie != null);
            var data = HdrpLightUtility.SetIntensity(light, candela, LightUnit.Candela);
            data.lightDimmer = 1f;
            data.affectDiffuse = true;
            data.SetShadowNearPlane(0.35f);
            light.shadowNearPlane = 0.35f;
            int playerLayer = LayerMask.NameToLayer("Player");
            if (playerLayer >= 0) light.cullingMask &= ~(1 << playerLayer);
            data.useContactShadow.useOverride = true;
            data.useContactShadow.@override = true;
            data.affectsVolumetric = true;
            data.volumetricDimmer = 0.65f;
            data.SetShadowResolution(1024);
        }
    }
}
