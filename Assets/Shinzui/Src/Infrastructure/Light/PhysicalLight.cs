using UnityEngine;

namespace Shinzui.Infrastructure.Lighting
{
    [RequireComponent(typeof(UnityEngine.Light))]
    [DisallowMultipleComponent]
    [ExecuteAlways]
    [AddComponentMenu("Rendering/Physical Light (HDRP Mode)")]
    public class PhysicalLight : MonoBehaviour
    {
        public enum LightUnit
        {
            Lumen,
            Candela,
            Lux,
            EV100,
            Nits
        }

        [SerializeField] private LightUnit m_Unit = LightUnit.Candela;
        [SerializeField] private float m_PhysicalIntensity = 1.0f;

        // IES Profile関連
        [SerializeField] private Texture2D m_IesProfile;
        [SerializeField] private float m_IesCutoffAngle = 100.0f;

        // More Options関連
        [SerializeField] private bool m_MoreOptions = false;
        [SerializeField] private bool m_AffectDiffuse = true;
        [SerializeField] private bool m_AffectSpecular = true;
        [SerializeField] private bool m_RangeAttenuation = true;
        [SerializeField] private float m_FadeDistance = 10.0f;
        [SerializeField] private float m_IntensityMultiplier = 1.0f;
        [SerializeField] private bool m_DisplayEmissiveMesh = false;
        [SerializeField] private bool m_IncludeForRayTracing = true;
        [SerializeField, HideInInspector] private bool m_Initialized;
        [SerializeField, HideInInspector] private LightType m_LastLightType;
        [SerializeField, HideInInspector] private float m_LastSpotAngle;

        private UnityEngine.Light m_Light;

        private void Awake()
        {
            EnsureLight();
            InitializeFromLightIfNeeded();
        }

        private void OnEnable()
        {
            EnsureLight();
            InitializeFromLightIfNeeded();
        }

        private void OnValidate()
        {
            EnsureLight();
            m_Unit = GetValidUnitForCurrentLight(m_Unit);
            SanitizePhysicalIntensity();
            if (m_Initialized)
            {
                UpdateIntensityFromPhysical();
            }
            else
            {
                InitializeFromLightIfNeeded();
            }
        }

        #if UNITY_EDITOR
        private void Update()
        {
            if (UnityEngine.Application.isPlaying) return;

            EnsureLight();
            if (m_Light == null) return;

            InitializeFromLightIfNeeded();

            bool lightShapeChanged = m_Light.type != m_LastLightType ||
                                     (m_Light.type == LightType.Spot && !Mathf.Approximately(m_Light.spotAngle, m_LastSpotAngle));

            if (lightShapeChanged)
            {
                m_Unit = GetValidUnitForCurrentLight(m_Unit);
                UpdateIntensityFromPhysical();
            }
        }
        #endif

        public LightUnit Unit
        {
            get => m_Unit;
            set
            {
                LightUnit validUnit = GetValidUnitForCurrentLight(value);
                if (m_Unit != validUnit)
                {
                    EnsureLight();
                    float actualIntensity = m_Light != null ? m_Light.intensity : GetActualIntensity();
                    m_Unit = validUnit;
                    SetPhysicalFromActual(actualIntensity);
                    MarkLightStateKnown(actualIntensity);
                }
            }
        }

        public float PhysicalIntensity
        {
            get => m_PhysicalIntensity;
            set
            {
                m_PhysicalIntensity = value;
                SanitizePhysicalIntensity();
                UpdateIntensityFromPhysical();
            }
        }

        // IES Properties
        public Texture2D IesProfile { get => m_IesProfile; set => m_IesProfile = value; }
        public float IesCutoffAngle { get => m_IesCutoffAngle; set => m_IesCutoffAngle = value; }

        // More Options Properties
        public bool MoreOptions { get => m_MoreOptions; set => m_MoreOptions = value; }
        public bool AffectDiffuse { get => m_AffectDiffuse; set => m_AffectDiffuse = value; }
        public bool AffectSpecular { get => m_AffectSpecular; set => m_AffectSpecular = value; }
        public bool RangeAttenuation { get => m_RangeAttenuation; set => m_RangeAttenuation = value; }
        public float FadeDistance { get => m_FadeDistance; set => m_FadeDistance = value; }
        public float IntensityMultiplier
        {
            get => m_IntensityMultiplier;
            set
            {
                m_IntensityMultiplier = Mathf.Max(0.0f, value);
                UpdateIntensityFromPhysical();
            }
        }
        public bool DisplayEmissiveMesh { get => m_DisplayEmissiveMesh; set => m_DisplayEmissiveMesh = value; }
        public bool IncludeForRayTracing { get => m_IncludeForRayTracing; set => m_IncludeForRayTracing = value; }

        public void EnsureValidUnitForCurrentLight()
        {
            m_Unit = GetValidUnitForCurrentLight(m_Unit);
        }

        public void SyncFromLight()
        {
            EnsureLight();
            if (m_Light == null) return;
            float actual = m_Light.intensity;

            if (Mathf.Abs(actual - GetActualIntensity()) > 0.001f)
            {
                SetPhysicalFromActual(actual);
            }

            MarkLightStateKnown(actual);
        }

        public void UpdateIntensityFromPhysical()
        {
            EnsureLight();
            if (m_Light == null) return;
            m_Unit = GetValidUnitForCurrentLight(m_Unit);
            SanitizePhysicalIntensity();
            m_Light.intensity = GetActualIntensity();
            MarkLightStateKnown(m_Light.intensity);
        }

        private float GetActualIntensity()
        {
            return GetUnitIntensity() * Mathf.Max(0.0f, m_IntensityMultiplier);
        }

        private float GetUnitIntensity()
        {
            if (m_Light == null) return 0f;

            switch (m_Light.type)
            {
                case LightType.Directional:
                    if (m_Unit == LightUnit.EV100)
                    {
                        return EV100ToLightIntensity(m_PhysicalIntensity);
                    }
                    else
                    {
                        return m_PhysicalIntensity;
                    }

                case LightType.Point:
                    if (m_Unit == LightUnit.EV100)
                    {
                        return EV100ToLightIntensity(m_PhysicalIntensity);
                    }
                    if (m_Unit == LightUnit.Lumen)
                    {
                        return m_PhysicalIntensity / (4.0f * Mathf.PI);
                    }
                    else
                    {
                        return m_PhysicalIntensity;
                    }

                case LightType.Spot:
                    if (m_Unit == LightUnit.EV100)
                    {
                        return EV100ToLightIntensity(m_PhysicalIntensity);
                    }
                    if (m_Unit == LightUnit.Lumen)
                    {
                        float omega = GetSpotSolidAngle();
                        return (omega > 0.0001f) ? (m_PhysicalIntensity / omega) : 0f;
                    }
                    else
                    {
                        return m_PhysicalIntensity;
                    }
                
                case LightType.Rectangle:
                case LightType.Disc:
                    // Areaライト（Lumen, Nits, EV100）
                    if (m_Unit == LightUnit.EV100)
                    {
                        return EV100ToLightIntensity(m_PhysicalIntensity);
                    }
                    else
                    {
                        return m_PhysicalIntensity;
                    }
            }

            return m_PhysicalIntensity;
        }

        private void SetPhysicalFromActual(float actualIntensity)
        {
            EnsureLight();
            if (m_Light == null) return;

            float unitIntensity = actualIntensity / Mathf.Max(0.0001f, m_IntensityMultiplier);

            switch (m_Light.type)
            {
                case LightType.Directional:
                    if (m_Unit == LightUnit.EV100)
                    {
                        m_PhysicalIntensity = LightIntensityToEV100(unitIntensity);
                    }
                    else
                    {
                        m_PhysicalIntensity = unitIntensity;
                    }
                    break;

                case LightType.Point:
                    if (m_Unit == LightUnit.EV100)
                    {
                        m_PhysicalIntensity = LightIntensityToEV100(unitIntensity);
                    }
                    else if (m_Unit == LightUnit.Lumen)
                    {
                        m_PhysicalIntensity = unitIntensity * 4.0f * Mathf.PI;
                    }
                    else
                    {
                        m_PhysicalIntensity = unitIntensity;
                    }
                    break;

                case LightType.Spot:
                    if (m_Unit == LightUnit.EV100)
                    {
                        m_PhysicalIntensity = LightIntensityToEV100(unitIntensity);
                    }
                    else if (m_Unit == LightUnit.Lumen)
                    {
                        m_PhysicalIntensity = unitIntensity * GetSpotSolidAngle();
                    }
                    else
                    {
                        m_PhysicalIntensity = unitIntensity;
                    }
                    break;

                case LightType.Rectangle:
                case LightType.Disc:
                    if (m_Unit == LightUnit.EV100)
                    {
                        m_PhysicalIntensity = LightIntensityToEV100(unitIntensity);
                    }
                    else
                    {
                        m_PhysicalIntensity = unitIntensity;
                    }
                    break;

                default:
                    m_PhysicalIntensity = unitIntensity;
                    break;
            }

            SanitizePhysicalIntensity();
        }

        private void EnsureLight()
        {
            if (m_Light == null) m_Light = GetComponent<UnityEngine.Light>();
        }

        private void InitializeFromLightIfNeeded()
        {
            if (m_Initialized || m_Light == null) return;

            m_Unit = GetValidUnitForCurrentLight(m_Unit);
            SetPhysicalFromActual(m_Light.intensity);
            MarkLightStateKnown(m_Light.intensity);
            m_Initialized = true;
        }

        private void MarkLightStateKnown(float intensity)
        {
            if (m_Light == null) return;

            m_LastLightType = m_Light.type;
            m_LastSpotAngle = m_Light.spotAngle;
            m_Initialized = true;
        }

        private LightUnit GetValidUnitForCurrentLight(LightUnit unit)
        {
            if (m_Light == null) return unit;

            switch (m_Light.type)
            {
                case LightType.Directional:
                    return LightUnit.Lux;

                case LightType.Rectangle:
                case LightType.Disc:
                    return unit == LightUnit.Nits || unit == LightUnit.EV100 ? unit : LightUnit.Lumen;

                default:
                    return unit == LightUnit.Nits ? LightUnit.Candela : unit;
            }
        }

        private void SanitizePhysicalIntensity()
        {
            m_IntensityMultiplier = Mathf.Max(0.0f, m_IntensityMultiplier);
            if (m_Unit != LightUnit.EV100)
            {
                m_PhysicalIntensity = Mathf.Max(0.0f, m_PhysicalIntensity);
            }
        }

        private float GetSpotSolidAngle()
        {
            float angleRad = Mathf.Clamp(m_Light.spotAngle, 0.0f, 179.0f) * Mathf.Deg2Rad;
            return 2.0f * Mathf.PI * (1.0f - Mathf.Cos(angleRad * 0.5f));
        }

        private static float EV100ToLightIntensity(float ev100)
        {
            return 2.5f * Mathf.Pow(2.0f, ev100);
        }

        private static float LightIntensityToEV100(float intensity)
        {
            return Mathf.Log(Mathf.Max(intensity, 0.0001f) / 2.5f, 2.0f);
        }
    }
}
