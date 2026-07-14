using UnityEngine;

namespace Shinzui.Infrastructure.Rendering.GameOverDissolve
{
    public static class GameOverDissolveRuntimeState
    {
        public static float Progress { get; private set; }
        public static float EdgeWidth { get; private set; } = 0.11f;
        public static float NoiseStrength { get; private set; } = 0.24f;
        public static float CellIntensity { get; private set; } = 1.0f;
        public static Color CoverColor { get; private set; } = new(0.0f, 0.0f, 0.01f, 1.0f);
        public static Color MembraneColor { get; private set; } = new(0.12f, 0.55f, 0.72f, 1.0f);
        public static Color HotEdgeColor { get; private set; } = new(1.05f, 1.22f, 1.25f, 1.0f);

        public static bool IsActive => Progress > 0.001f;

        public static void Set(
            float progress,
            float edgeWidth,
            float noiseStrength,
            float cellIntensity,
            Color coverColor,
            Color membraneColor,
            Color hotEdgeColor)
        {
            Progress = Mathf.Clamp01(progress);
            EdgeWidth = Mathf.Clamp(edgeWidth, 0.01f, 0.35f);
            NoiseStrength = Mathf.Clamp(noiseStrength, 0.0f, 0.65f);
            CellIntensity = Mathf.Clamp(cellIntensity, 0.0f, 2.0f);
            CoverColor = coverColor;
            MembraneColor = membraneColor;
            HotEdgeColor = hotEdgeColor;
        }

        public static void Reset()
        {
            Progress = 0f;
        }
    }
}
