using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace Shinzui.Infrastructure.Lighting
{
    /// <summary>
    /// HDRPライトを物理単位で初期化する
    /// </summary>
    public static class HdrpLightUtility
    {
        /// <summary>
        /// 追加ライトデータを用意し物理単位をネイティブ強度へ変換する
        /// </summary>
        /// <param name="light">対象ライト</param>
        /// <param name="intensity">指定単位の強度</param>
        /// <param name="unit">強度の物理単位</param>
        /// <returns>HDRPの追加ライトデータ</returns>
        public static HDAdditionalLightData SetIntensity(UnityEngine.Light light, float intensity, LightUnit unit)
        {
            // ライト形状の設定後にHDRPデータを追加する
            if (!light.TryGetComponent(out HDAdditionalLightData data))
                data = light.gameObject.AddComponent<HDAdditionalLightData>();

            // Unity 6ではLightが単位を保持しintensityはネイティブ単位を使用する
            light.lightUnit = unit;
            light.intensity = LightUnitUtils.ConvertIntensity(light, intensity, unit,
                LightUnitUtils.GetNativeLightUnit(light.type));
            return data;
        }
    }
}
