using UnityEngine;

namespace Shinzui.View.GenerateTunnel
{
    /// <summary>
    /// Scene-owned settings component for tunnel generation and generated model presentation.
    /// This view component only stores inspector values; orchestration is handled by the bootstrapper.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-110)]
    public sealed class TunnelGenerator : MonoBehaviour
    {
        [Header("Generation")]
        [Min(5)] [SerializeField] private int tunnelCount = 8;
        [SerializeField] private int seed = 2777;
        [Min(1)] [SerializeField] private int placementAttemptsPerTunnel = 80;

        [Header("Small Rooms")]
        [Min(0)] [SerializeField] private int smallRoomCount = 1;
        [Min(1.0f)] [SerializeField] private float smallRoomWidth = 8.0f;
        [Min(1.0f)] [SerializeField] private float smallRoomLength = 6.0f;

        [Header("Layout Size")]
        [Min(4.0f)] [SerializeField] private float tunnelLength = 153.12695f;
        [Tooltip("Distance from the center connection point to the front/back connection points on each tunnel side.")]
        [Min(0.0f)] [SerializeField] private float connectionPointSpacing = 51.042316f;
        [Min(3.0f)] [SerializeField] private float tunnelWidth = 14.800003f;
        [Min(2.0f)] [SerializeField] private float tunnelHeight = 5.0f;
        [Min(1.0f)] [SerializeField] private float corridorLength = 8.0f;
        [Min(1.0f)] [SerializeField] private float corridorWidth = 3.0f;
        [Min(0.0f)] [SerializeField] private float placementMargin = 2.0f;

        [Header("Overlap Prevention")]
        [Min(0.1f)] [SerializeField] private float tunnelOverlapSizeMultiplier = 1.0f;
        [Min(0.1f)] [SerializeField] private float corridorOverlapSizeMultiplier = 1.0f;
        [Min(0.1f)] [SerializeField] private float smallRoomOverlapSizeMultiplier = 1.0f;

        [Header("Scene References")]
        [SerializeField] private TunnelMapView mapView;

        [Header("Models")]
        [SerializeField] private GameObject basicTunnelModel;
        [SerializeField] private GameObject corridorModel;
        [SerializeField] private GameObject smallRoomModel;
        [SerializeField] private GameObject warpCorridorModel;

        [Header("Model Scale")]
        [SerializeField] private Vector3 basicTunnelModelScale = Vector3.one;
        [SerializeField] private Vector3 corridorModelScale = Vector3.one;
        [SerializeField] private Vector3 smallRoomModelScale = Vector3.one;
        [SerializeField] private Vector3 warpCorridorModelScale = Vector3.one;

        [Header("Model Rotation")]
        [SerializeField] private Vector3 basicTunnelModelEuler = new(0.0f, 90.0f, 0.0f);
        [SerializeField] private Vector3 corridorModelEuler = Vector3.zero;
        [SerializeField] private Vector3 smallRoomModelEuler = Vector3.zero;
        [SerializeField] private Vector3 warpCorridorModelEuler = Vector3.zero;

        [Header("Model Dimensions")]
        [Tooltip("Use the unscaled model bounds for layout size. Model Scale only changes the generated visuals.")]
        [SerializeField] private bool useModelBoundsForLayout = true;
        [SerializeField] private bool configureBasicTunnelExitParts = true;

        public int TunnelCount => tunnelCount;
        public int Seed => seed;
        public int PlacementAttemptsPerTunnel => placementAttemptsPerTunnel;
        public int SmallRoomCount => smallRoomCount;
        public float SmallRoomWidth => smallRoomWidth;
        public float SmallRoomLength => smallRoomLength;
        public float TunnelLength => tunnelLength;
        public float ConnectionPointSpacing => connectionPointSpacing * Mathf.Abs(BasicTunnelModelScale.x);
        public float TunnelWidth => tunnelWidth;
        public float TunnelHeight => tunnelHeight;
        public float CorridorLength => corridorLength;
        public float CorridorWidth => corridorWidth;
        public float PlacementMargin => placementMargin;
        public float TunnelOverlapSizeMultiplier => tunnelOverlapSizeMultiplier;
        public float CorridorOverlapSizeMultiplier => corridorOverlapSizeMultiplier;
        public float SmallRoomOverlapSizeMultiplier => smallRoomOverlapSizeMultiplier;
        public TunnelMapView MapView => mapView;
        public GameObject BasicTunnelModel => basicTunnelModel;
        public GameObject CorridorModel => corridorModel;
        public GameObject SmallRoomModel => smallRoomModel;
        public GameObject WarpCorridorModel => warpCorridorModel;
        public Vector3 BasicTunnelModelScale => SanitizeScale(basicTunnelModelScale);
        public Vector3 CorridorModelScale => SanitizeScale(corridorModelScale);
        public Vector3 SmallRoomModelScale => SanitizeScale(smallRoomModelScale);
        public Vector3 WarpCorridorModelScale => SanitizeScale(warpCorridorModelScale);
        public Quaternion BasicTunnelModelRotation => Quaternion.Euler(basicTunnelModelEuler);
        public Quaternion CorridorModelRotation => Quaternion.Euler(corridorModelEuler);
        public Quaternion SmallRoomModelRotation => Quaternion.Euler(smallRoomModelEuler);
        public Quaternion WarpCorridorModelRotation => Quaternion.Euler(warpCorridorModelEuler);
        public bool UseModelBoundsForLayout => useModelBoundsForLayout;
        public bool ConfigureBasicTunnelExitParts => configureBasicTunnelExitParts;

        private static Vector3 SanitizeScale(Vector3 value)
        {
            return new Vector3(
                Mathf.Approximately(value.x, 0.0f) ? 1.0f : value.x,
                Mathf.Approximately(value.y, 0.0f) ? 1.0f : value.y,
                Mathf.Approximately(value.z, 0.0f) ? 1.0f : value.z);
        }
    }
}
