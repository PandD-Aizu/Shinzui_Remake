using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace Shinzui.Infrastructure.Lighting
{
    [RequireComponent(typeof(UnityEngine.Light), typeof(HDAdditionalLightData))]
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

        /// <summary>
        /// 保存済みの物理強度をHDRPライトへ反映する
        /// </summary>
        public void UpdateIntensityFromPhysical()
        {
            EnsureLight();
            if (m_Light == null) return;
            m_Unit = GetValidUnitForCurrentLight(m_Unit);
            SanitizePhysicalIntensity();
            // HDRPの単位表示とネイティブ強度を同期する
            m_Light.lightUnit = GetHdrpUnit();
            m_Light.intensity = GetActualIntensity();
            MarkLightStateKnown(m_Light.intensity);
        }

        private float GetActualIntensity()
        {
            return GetUnitIntensity() * Mathf.Max(0.0f, m_IntensityMultiplier);
        }

        /// <summary>
        /// 保存済みの物理強度をHDRPのネイティブ単位へ変換する
        /// </summary>
        /// <returns>カンデラ、ニトまたはルクスで表した強度</returns>
        private float GetUnitIntensity()
        {
            if (m_Light == null) return 0f;

            // 面積やスポット形状を含むUnity共通の単位変換を使用する
            return LightUnitUtils.ConvertIntensity(m_Light, m_PhysicalIntensity, GetHdrpUnit(),
                LightUnitUtils.GetNativeLightUnit(m_Light.type));
        }

        /// <summary>
        /// HDRPのネイティブ強度から保存用の物理強度を復元する
        /// </summary>
        /// <param name="actualIntensity">ネイティブ単位の強度</param>
        private void SetPhysicalFromActual(float actualIntensity)
        {
            EnsureLight();
            if (m_Light == null) return;

            // 倍率を除去してから選択単位へ戻す
            float unitIntensity = actualIntensity / Mathf.Max(0.0001f, m_IntensityMultiplier);
            m_PhysicalIntensity = LightUnitUtils.ConvertIntensity(m_Light, unitIntensity,
                LightUnitUtils.GetNativeLightUnit(m_Light.type), GetHdrpUnit());
            SanitizePhysicalIntensity();
        }

        /// <summary>
        /// 既存のシリアライズ済み列挙値をUnityの物理単位へ対応させる
        /// </summary>
        /// <returns>Unityのライト単位</returns>
        private UnityEngine.Rendering.LightUnit GetHdrpUnit()
        {
            return m_Unit switch
            {
                LightUnit.Lumen => UnityEngine.Rendering.LightUnit.Lumen,
                LightUnit.Lux => UnityEngine.Rendering.LightUnit.Lux,
                LightUnit.Nits => UnityEngine.Rendering.LightUnit.Nits,
                LightUnit.EV100 => UnityEngine.Rendering.LightUnit.Ev100,
                _ => UnityEngine.Rendering.LightUnit.Candela
            };
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

    }
}
