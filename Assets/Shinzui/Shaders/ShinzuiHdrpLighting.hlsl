#ifndef SHINZUI_HDRP_LIGHTING_INCLUDED
#define SHINZUI_HDRP_LIGHTING_INCLUDED
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
#include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderVariables.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/SpaceTransforms.hlsl"
#include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderVariablesFunctions.hlsl"
#define SHADOW_LOW
#define DIRECTIONAL_SHADOW_LOW
#define AREA_SHADOW_MEDIUM
#define LIGHTLOOP_DISABLE_TILE_AND_CLUSTER
#include "Packages/com.unity.render-pipelines.high-definition/Runtime/Lighting/Lighting.hlsl"

// Small forward-only horror surfaces use HDRP's culled punctual light and shadow buffers.
struct ShinzuiLight { float3 color; float3 direction; float distanceAttenuation; float shadowAttenuation; uint layerMask; };
ShinzuiLight ShinzuiPunctual(uint index, float3 positionRWS, float3 normalWS, float2 pixel)
{
    LightData data = _LightDatas[index];
    ShinzuiLight result = (ShinzuiLight)0;
    float3 delta = data.positionRWS - positionRWS;
    float distanceSquared = max(dot(delta, delta), .001);
    float distance = sqrt(distanceSquared);
    result.direction = delta / distance;
    float attenuation = rcp(distance) * DistanceWindowing(distanceSquared, data.rangeAttenuationScale, data.rangeAttenuationBias);
    attenuation *= AngleAttenuation(dot(-result.direction, data.forward), data.angleScale, data.angleOffset);
    result.distanceAttenuation = attenuation * attenuation;
    result.color = data.color * data.lightDimmer;
    result.layerMask = data.lightLayers;
    result.shadowAttenuation = 1;
    if (data.shadowIndex >= 0 && result.distanceAttenuation > 0)
        result.shadowAttenuation = lerp(1, GetPunctualShadowAttenuation(InitShadowContext(), pixel, positionRWS, normalWS,
            data.shadowIndex, result.direction, distance, data.lightType == GPULIGHTTYPE_POINT, true), data.shadowDimmer);
    return result;
}
ShinzuiLight ShinzuiDirectional(uint index, float3 positionRWS, float3 normalWS, float2 pixel)
{
    DirectionalLightData data = _DirectionalLightDatas[index];
    ShinzuiLight result = (ShinzuiLight)0;
    result.direction = -data.forward;
    result.color = data.color * data.lightDimmer;
    result.distanceAttenuation = 1;
    result.shadowAttenuation = 1;
    result.layerMask = data.lightLayers;
    if (data.shadowIndex >= 0)
    {
        HDShadowContext context = InitShadowContext();
        result.shadowAttenuation = lerp(1, GetDirectionalShadowAttenuation(context, pixel, positionRWS, normalWS,
            data.shadowIndex, result.direction), data.shadowDimmer);
    }
    return result;
}
#endif
