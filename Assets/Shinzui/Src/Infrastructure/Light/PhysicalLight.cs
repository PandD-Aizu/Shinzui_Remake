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

        private UnityEngine.Light m_Light;

        private void Awake()
        {
            m_Light = GetComponent<UnityEngine.Light>();
            UpdateIntensityFromPhysical();
        }

        private void OnValidate()
        {
            if (m_Light == null) m_Light = GetComponent<UnityEngine.Light>();
            UpdateIntensityFromPhysical();
        }

        private void Update()
        {
            if (m_Light == null) m_Light = GetComponent<UnityEngine.Light>();
            
            #if UNITY_EDITOR
            if (!UnityEngine.Application.isPlaying)
            {
                SyncFromLight();
            }
            #endif
        }

        public LightUnit Unit
        {
            get => m_Unit;
            set
            {
                if (m_Unit != value)
                {
                    float actualIntensity = GetActualIntensity();
                    m_Unit = value;
                    SetPhysicalFromActual(actualIntensity);
                }
            }
        }

        public float PhysicalIntensity
        {
            get => m_PhysicalIntensity;
            set
            {
                m_PhysicalIntensity = value;
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
        public float IntensityMultiplier { get => m_IntensityMultiplier; set => m_IntensityMultiplier = value; }
        public bool DisplayEmissiveMesh { get => m_DisplayEmissiveMesh; set => m_DisplayEmissiveMesh = value; }
        public bool IncludeForRayTracing { get => m_IncludeForRayTracing; set => m_IncludeForRayTracing = value; }

        public void SyncFromLight()
        {
            if (m_Light == null) return;
            float actual = m_Light.intensity;
            
            if (Mathf.Abs(actual - GetActualIntensity()) > 0.001f)
            {
                SetPhysicalFromActual(actual);
            }
        }

        public void UpdateIntensityFromPhysical()
        {
            if (m_Light == null) return;
            m_Light.intensity = GetActualIntensity();
        }

        private float GetActualIntensity()
        {
            if (m_Light == null) return 0f;

            switch (m_Light.type)
            {
                case LightType.Directional:
                    if (m_Unit == LightUnit.EV100)
                    {
                        return 2.5f * Mathf.Pow(2.0f, m_PhysicalIntensity);
                    }
                    else
                    {
                        return m_PhysicalIntensity;
                    }

                case LightType.Point:
                    if (m_Unit == LightUnit.Lumen)
                    {
                        return m_PhysicalIntensity / (4.0f * Mathf.PI);
                    }
                    else
                    {
                        return m_PhysicalIntensity;
                    }

                case LightType.Spot:
                    if (m_Unit == LightUnit.Lumen)
                    {
                        float angleRad = m_Light.spotAngle * Mathf.Deg2Rad;
                        float omega = 2.0f * Mathf.PI * (1.0f - Mathf.Cos(angleRad * 0.5f));
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
                        return 2.5f * Mathf.Pow(2.0f, m_PhysicalIntensity);
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
            if (m_Light == null) return;

            switch (m_Light.type)
            {
                case LightType.Directional:
                    if (m_Unit == LightUnit.EV100)
                    {
                        m_PhysicalIntensity = Mathf.Log(Mathf.Max(actualIntensity, 0.0001f) / 2.5f, 2.0f);
                    }
                    else
                    {
                        m_PhysicalIntensity = actualIntensity;
                    }
                    break;

                case LightType.Point:
                    if (m_Unit == LightUnit.Lumen)
                    {
                        m_PhysicalIntensity = actualIntensity * 4.0f * Mathf.PI;
                    }
                    else
                    {
                        m_PhysicalIntensity = actualIntensity;
                    }
                    break;

                case LightType.Spot:
                    if (m_Unit == LightUnit.Lumen)
                    {
                        float angleRad = m_Light.spotAngle * Mathf.Deg2Rad;
                        float omega = 2.0f * Mathf.PI * (1.0f - Mathf.Cos(angleRad * 0.5f));
                        m_PhysicalIntensity = actualIntensity * omega;
                    }
                    else
                    {
                        m_PhysicalIntensity = actualIntensity;
                    }
                    break;

                case LightType.Rectangle:
                case LightType.Disc:
                    if (m_Unit == LightUnit.EV100)
                    {
                        m_PhysicalIntensity = Mathf.Log(Mathf.Max(actualIntensity, 0.0001f) / 2.5f, 2.0f);
                    }
                    else
                    {
                        m_PhysicalIntensity = actualIntensity;
                    }
                    break;

                default:
                    m_PhysicalIntensity = actualIntensity;
                    break;
            }
        }
    }
}
