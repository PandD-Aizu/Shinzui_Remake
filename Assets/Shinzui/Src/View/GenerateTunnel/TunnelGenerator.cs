using UnityEngine;

namespace Shinzui.View.GenerateTunnel
{
    /// <summary>
    /// Scene-owned settings component for tunnel generation and generated model presentation.
    /// This view only exposes inspector values; a scene LifetimeScope owns generation.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-110)]
    public sealed class TunnelGenerator : MonoBehaviour
    {
        [Header("Gimmicks")]
        [Min(0)] [SerializeField] private int spiderWebCount = 3;
        [Min(0)] [SerializeField] private int fireAlarmCount = 3;
        [Min(0)] [SerializeField] private float fireAlarmMinSpacing = 40.0f;
        [Min(0)] [SerializeField] private float fireAlarmEndMargin = 12.0f;
        [Min(0)] [SerializeField] private float fireAlarmMouthMargin = 4.0f;
        [SerializeField] private GameObject spiderWebTemplate;
        [SerializeField] private GameObject fireAlarmPrefab;
        [Min(0)] [SerializeField] private int hidingLockerCount = 4;
        [Min(0)] [SerializeField] private float hidingLockerMinSpacing = 25f;
        [SerializeField] private GameObject hidingLockerPrefab;
        [Min(0)] [SerializeField] private int carCount = 4;
        [Min(0)] [SerializeField] private float carMinSpacing = 15f;
        public int CarCount => carCount;
        public float CarMinSpacing => carMinSpacing;
        public int HidingLockerCount => hidingLockerCount;
        public float HidingLockerMinSpacing => hidingLockerMinSpacing;
        public GameObject HidingLockerPrefab => hidingLockerPrefab != null ? hidingLockerPrefab : Resources.Load<GameObject>("HidingLocker");
        [Min(0)] [SerializeField] private float fireAlarmHeight = 1.4f;

        [Header("Generation")]
        [Min(5)] [SerializeField] private int tunnelCount = 8;
        [SerializeField] private int seed = 2777;
        [Min(1)] [SerializeField] private int placementAttemptsPerTunnel = 80;

        [Header("Upper Floor")]
        [SerializeField] private bool generateUpperFloor;
        [Min(8.0f)] [SerializeField] private float upperFloorHeight = 12.0f;

        [Header("Lower Floor")]
        [SerializeField] private bool generateLowerFloor;
        [Min(8.0f)] [SerializeField] private float lowerFloorDepth = 12.0f;

        [Header("Floor Connections")]
        [Min(0)] [SerializeField] private int dropOpeningCount = 2;
        [Min(2.0f)] [SerializeField] private float dropOpeningSize = 3.0f;

        [Header("Small Rooms")]
        [Min(0)] [SerializeField] private int smallRoomCount = 1;
        [Min(1.0f)] [SerializeField] private float smallRoomWidth = 8.0f;
        [Min(1.0f)] [SerializeField] private float smallRoomLength = 6.0f;

        [Header("Layout Size")]
        [Min(4.0f)] [SerializeField] private float tunnelLength = 153.12695f;
        [Min(4.0f)] [SerializeField] private float minTunnelLength = 153.12695f;
        [Min(4.0f)] [SerializeField] private float maxTunnelLength = 153.12695f;
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

        [Header("Ceiling Lights")]
        [Tooltip("側道のLED照明の間隔（m）")]
        [Min(1.5f)] [SerializeField] private float ceilingLightSpacing = 12.0f;
        [Tooltip("大型トンネル中央の蛍光灯の間隔（m） ループ内では周期に合わせて補正")]
        [Min(1.5f)] [SerializeField] private float mainTunnelLightSpacing = 5.0f;

        [Header("Tunnel Look")]
        [SerializeField, Range(0f, 1f)] private float deadFixtureFraction = 0f;
        [SerializeField, Range(0f, 1f)] private float flickerFixtureFraction = 0f;
        [SerializeField, Range(0f, 0.5f)] private float decalDensity = 0.08f;
        [SerializeField, Range(0, 256)] private int maxTunnelDecals = 96;
        [SerializeField, Range(5f, 80f)] private float decalFadeDistance = 35f;
        [SerializeField, Range(0, 32)] private int maxGroundFogPatches = 16;
        public int MaxGroundFogPatches => maxGroundFogPatches;
        public float DeadFixtureFraction => deadFixtureFraction;
        public float FlickerFixtureFraction => flickerFixtureFraction;
        public float DecalDensity => decalDensity;
        public int MaxTunnelDecals => maxTunnelDecals;
        public float DecalFadeDistance => decalFadeDistance;

        [Header("Model Scale")]
        [Tooltip("Xは長さの基準倍率、Yは断面の倍率。縦横比を維持するためZはYと同じ倍率になる")]
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
        [Tooltip("Measure layout dimensions and side-mouth spacing from model meshes, including Model Scale.")]
        [SerializeField] private bool useModelBoundsForLayout = true;
        [SerializeField] private bool configureBasicTunnelExitParts = true;

        [Header("Small Room Racks")]
        [Min(0)] [SerializeField] private int smallRoomRackCount = 2;
        [SerializeField] private GameObject smallRoomRackPrefab;

        public int SmallRoomRackCount => smallRoomRackCount;
        public GameObject SmallRoomRackPrefab => smallRoomRackPrefab != null
            ? smallRoomRackPrefab : Resources.Load<GameObject>("RustedWireRack");

        public int SpiderWebCount => spiderWebCount;
        public int FireAlarmCount => fireAlarmCount;
        public float FireAlarmMinSpacing => fireAlarmMinSpacing;
        public float FireAlarmEndMargin => fireAlarmEndMargin;
        public float FireAlarmMouthMargin => fireAlarmMouthMargin;
        public GameObject SpiderWebTemplate => spiderWebTemplate;
        public GameObject FireAlarmPrefab => fireAlarmPrefab;
        public float FireAlarmHeight => fireAlarmHeight;

        public int TunnelCount => tunnelCount;
        public int Seed => seed;
        public int PlacementAttemptsPerTunnel => placementAttemptsPerTunnel;
        public bool GenerateUpperFloor => generateUpperFloor;
        public float UpperFloorHeight => upperFloorHeight;
        public bool GenerateLowerFloor => generateLowerFloor;
        public float LowerFloorDepth => lowerFloorDepth;
        public int DropOpeningCount => dropOpeningCount;
        public float DropOpeningSize => dropOpeningSize;
        public int SmallRoomCount => smallRoomCount;
        public float SmallRoomWidth => smallRoomWidth;
        public float SmallRoomLength => smallRoomLength;
        public float TunnelLength => tunnelLength;
        public float MinTunnelLength => Mathf.Min(minTunnelLength, maxTunnelLength);
        public float MaxTunnelLength => Mathf.Max(minTunnelLength, maxTunnelLength);
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
        public float CeilingLightSpacing => Mathf.Max(1.5f, ceilingLightSpacing);
        /// <summary>SaveStageと同じ中央配置の基準間隔を取得する</summary>
        public float MainTunnelLightSpacing => Mathf.Max(1.5f, mainTunnelLightSpacing);
        public Vector3 BasicTunnelModelScale
        {
            get
            {
                Vector3 scale = SanitizeScale(basicTunnelModelScale);
                return new Vector3(scale.x, scale.y, scale.y);
            }
        }
        public Vector3 CorridorModelScale => SanitizeScale(corridorModelScale);
        public Vector3 SmallRoomModelScale => SanitizeScale(smallRoomModelScale);
        public Vector3 WarpCorridorModelScale => SanitizeScale(warpCorridorModelScale);
        public Quaternion BasicTunnelModelRotation => Quaternion.Euler(basicTunnelModelEuler);
        public Quaternion CorridorModelRotation => Quaternion.Euler(corridorModelEuler);
        public Quaternion SmallRoomModelRotation => Quaternion.Euler(smallRoomModelEuler);
        public Quaternion WarpCorridorModelRotation => Quaternion.Euler(warpCorridorModelEuler);
        public bool UseModelBoundsForLayout => useModelBoundsForLayout;
        public bool ConfigureBasicTunnelExitParts => configureBasicTunnelExitParts;

        /// <summary>
        /// Inspectorでも断面の縦横倍率を揃える
        /// </summary>
        private void OnValidate()
        {
            basicTunnelModelScale.z = basicTunnelModelScale.y;
        }

        private static Vector3 SanitizeScale(Vector3 value)
        {
            return new Vector3(
                Mathf.Approximately(value.x, 0.0f) ? 1.0f : value.x,
                Mathf.Approximately(value.y, 0.0f) ? 1.0f : value.y,
                Mathf.Approximately(value.z, 0.0f) ? 1.0f : value.z);
        }
    }
}
