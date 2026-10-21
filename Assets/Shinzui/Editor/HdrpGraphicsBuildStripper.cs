using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

/// <summary>Exclude the dormant URP-only GI feature from HDRP Windows builds</summary>
public sealed class HdrpGraphicsBuildStripper : IPreprocessShaders
{
    public int callbackOrder => 0;

    /// <summary>HDRP uses its native SSGI and RTGI; the legacy URP package remains available for old authoring scenes</summary>
    /// <param name="shader">Shader considered by the build</param>
    /// <param name="snippet">Pass being compiled</param>
    /// <param name="data">Retained variants</param>
    public void OnProcessShader(Shader shader, ShaderSnippetData snippet, IList<ShaderCompilerData> data)
    {
        if (GraphicsSettings.defaultRenderPipeline is HDRenderPipelineAsset &&
            AssetDatabase.GetAssetPath(shader).StartsWith("Packages/com.jiaozi158.unityssgiurp/")) data.Clear();
    }
}
