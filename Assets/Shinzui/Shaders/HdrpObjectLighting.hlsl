#ifndef SHINZUI_HDRP_OBJECT_LIGHTING
#define SHINZUI_HDRP_OBJECT_LIGHTING
#include "HdrpObjectCommon.hlsl"
#define SHADOW_DATA_NOT_GUARANTEED_SCALAR
#define LIGHTLOOP_DISABLE_TILE_AND_CLUSTER
#include "Packages/com.unity.render-pipelines.high-definition/Runtime/Lighting/Lighting.hlsl"
#include "Packages/com.unity.render-pipelines.high-definition/Runtime/Lighting/LightLoop/CookieSampling.hlsl"
#include "Packages/com.unity.render-pipelines.high-definition/Runtime/Lighting/PunctualLightCommon.hlsl"
#include "Packages/com.unity.render-pipelines.high-definition/Runtime/Sky/SkyUtils.hlsl"
#include "Packages/com.unity.render-pipelines.high-definition/Runtime/Lighting/AtmosphericScattering/AtmosphericScattering.hlsl"

struct ShinzuiLight { float3 direction; float3 color; };

/// <summary>HDRPの光源と影を画素位置で評価する</summary>
/// <param name="index">方向光に続く点光源の番号</param>
/// <param name="positionWS">カメラ相対座標</param>
/// <param name="normalWS">表面法線</param>
/// <param name="positionSS">画面画素座標</param>
/// <returns>減衰と影を反映した光源</returns>
ShinzuiLight ShinzuiGetLight(uint index, float3 positionWS, float3 normalWS, float2 positionSS)
{
    ShinzuiLight result = (ShinzuiLight)0;
    HDShadowContext shadows = InitShadowContext();
    uint layers = GetMeshRenderingLayerMask();
    if (index < _DirectionalLightCount)
    {
        DirectionalLightData light = _DirectionalLightDatas[index];
        result.direction = -light.forward;
        if ((light.lightLayers & layers) == 0) return result;
        float shadow = light.shadowIndex < 0 ? 1 : GetDirectionalShadowAttenuation(shadows,
            positionSS, positionWS, normalWS, light.shadowIndex, result.direction);
        result.color = light.color * light.lightDimmer * lerp(1, shadow, light.shadowDimmer);
        if (light.cookieMode != COOKIEMODE_NONE)
        {
            float3 delta = positionWS - light.positionRWS;
            float2 uv = float2(dot(delta, light.right), dot(delta, light.up)) * 0.5 + 0.5;
            result.color *= SampleCookie2D(uv, light.cookieScaleOffset);
        }
        return result;
    }
    LightData punctual = _LightDatas[index - _DirectionalLightCount];
    if ((punctual.lightLayers & layers) == 0) return result;
    float4 distances;
    GetPunctualLightVectors(positionWS, punctual, result.direction, distances);
    float attenuation = min(distances.z, 1.0 / PUNCTUAL_LIGHT_THRESHOLD) * DistanceWindowing(distances.y,
        punctual.rangeAttenuationScale, punctual.rangeAttenuationBias);
    attenuation *= AngleAttenuation(distances.w / max(distances.x, 0.0001), punctual.angleScale, punctual.angleOffset);
    float shadow = punctual.shadowIndex < 0 ? 1 : GetPunctualShadowAttenuation(shadows, positionSS,
        positionWS, normalWS, punctual.shadowIndex, result.direction, distances.x,
        punctual.lightType == GPULIGHTTYPE_POINT, punctual.lightType != GPULIGHTTYPE_PROJECTOR_BOX);
    result.color = punctual.color * punctual.lightDimmer * attenuation * attenuation * lerp(1, shadow, punctual.shadowDimmer);
    /* HDRPのクッキーアトラスで懐中電灯の光束形状を維持する */
    if (punctual.cookieMode != COOKIEMODE_NONE)
    {
        float3 delta = positionWS - punctual.positionRWS;
        if (punctual.lightType == GPULIGHTTYPE_POINT)
            result.color *= SamplePointCookie(mul(float3x3(punctual.right, punctual.up, punctual.forward), delta), punctual.cookieScaleOffset);
        else
        {
            float3 local = float3(dot(delta, punctual.right), dot(delta, punctual.up), dot(delta, punctual.forward));
            float2 projected = local.xy / (punctual.lightType == GPULIGHTTYPE_PROJECTOR_BOX ? 1 : max(local.z, 0.0001));
            float inside = Max3(abs(projected.x), abs(projected.y), abs(local.z - punctual.range * 0.5) - punctual.range * 0.5 + 1) <= punctual.boxLightSafeExtent ? 1 : 0;
            result.color *= SampleCookie2D(projected * 0.5 + 0.5, punctual.cookieScaleOffset) * inside;
        }
    }
    return result;
}

/// <summary>露出済みの色へHDRPの体積霧を一度だけ合成する</summary>
/// <param name="color">露出済みの色</param>
/// <param name="positionCS">画面座標</param>
/// <param name="positionWS">カメラ相対座標</param>
/// <returns>霧を反映した色</returns>
float3 ShinzuiFog(float3 color, float4 positionCS, float3 positionWS)
{
    PositionInputs position = GetPositionInput(positionCS.xy, _ScreenSize.zw, positionCS.z,
        LinearEyeDepth(positionWS, UNITY_MATRIX_V), positionWS);
    float3 fogColor, fogOpacity;
    EvaluateAtmosphericScattering(position, GetWorldSpaceNormalizeViewDir(positionWS), fogColor, fogOpacity);
    return color * (1 - fogOpacity) + fogColor;
}
#endif
