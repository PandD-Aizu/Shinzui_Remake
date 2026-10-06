using UnityEngine;
using UnityEngine.Rendering;

namespace Shinzui.View.Rendering
{
    /// <summary>
    /// ULTRA専用の環境マテリアルと局所反射の設定を保持する
    /// </summary>
    [CreateAssetMenu(menuName = "Shinzui/Rendering/Ultra Environment Profile")]
    public sealed class UltraEnvironmentProfile : ScriptableObject
    {
        public RenderPipelineAsset pipelineAsset;
        public Material sourceMaterial;
        public Material ultraMaterial;
        [Min(0f)] public float ceilingLightIntensityMultiplier = 32f;

        [Min(128)] public int reflectionResolution = 512;
        public Vector3 reflectionBoxSize = new Vector3(18f, 10f, 24f);
        [Min(0.1f)] public float reflectionRefreshSeconds = 1f;
        [Min(0f)] public float reflectionIntensity = 1f;
    }
}
