#ifndef SHINZUI_HDRP_OBJECT_COMMON
#define SHINZUI_HDRP_OBJECT_COMMON
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
#include "Packages/com.unity.render-pipelines.high-definition/Runtime/RenderPipeline/ShaderPass/ShaderPass.cs.hlsl"
#define SHADERPASS SHADERPASS_FORWARD
#include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderVariables.hlsl"

/// <summary>クリップ座標から画面UVの同次座標を取得する</summary>
/// <param name="positionCS">クリップ座標</param>
/// <returns>画面の同次座標</returns>
float4 ComputeScreenPos(float4 positionCS)
{
    float4 screen = positionCS * 0.5;
    screen.xy = float2(screen.x, screen.y * _ProjectionParams.x) + screen.w;
    screen.zw = positionCS.zw;
    return screen;
}
#endif
