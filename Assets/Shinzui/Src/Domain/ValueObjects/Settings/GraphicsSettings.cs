namespace Shinzui.Domain.Settings
{
    /// <summary>
    /// 保存データの品質番号をUnityの品質インデックスから独立させる描画プリセット
    /// </summary>
    public enum GraphicsQualityPreset
    {
        Low = 0,
        Medium = 1,
        High = 2,
        Ultra = 3,
        Custom = 4
    }

    /// <summary>
    /// グラフィックスおよび描画品質に関する設定データを保持するドメインモデル
    /// </summary>
    [System.Serializable]
    public class GraphicsSettings
    {
        // --- 基本・画面設定 ---

        /// <summary>描画品質プリセット</summary>
        public GraphicsQualityPreset QualityPreset { get; set; } = GraphicsQualityPreset.Ultra;
        
        /// <summary>【PC専用】FPSやGPU情報等の表示有無</summary>
        public bool ShowPerformanceMetrics { get; set; } = false;

        /// <summary>セーフエリア／表示領域の設定調整 (0.8 ~ 1.0)</summary>
        public float DisplayAreaRatio { get; set; } = 1.0f;

        /// <summary>ゲーム画面の明るさ調整 (0.0 ~ 1.0)</summary>
        public float BrightnessValue { get; set; } = 0.5f;

        /// <summary>HDR表示の有効化</summary>
        public bool EnableHDR { get; set; } = false;

        /// <summary>カラースペース／色空間設定 (0: Gamma, 1: Linear)</summary>
        public int ColorSpaceType { get; set; } = 1;

        /// <summary>【PC専用】画面モード (0: フルスクリーン, 1: ウィンドウ, 2: 仮想フルスクリーン)</summary>
        public int ScreenMode { get; set; } = 0;

        /// <summary>【PC専用】解像度文字列 (例: "1920x1080")</summary>
        public string Resolution { get; set; } = "1920x1080";

        /// <summary>【PC専用】ディスプレイ周波数／リフレッシュレート (Hz)</summary>
        public int RefreshRate { get; set; } = 60;

        /// <summary>【PC専用】フレームレート制限値 (30, 60, 120, 144, 0: 無制限)</summary>
        public int FrameRateLimit { get; set; } = 60;

        /// <summary>【PC専用】垂直同期 (VSync) の有無</summary>
        public bool EnableVSync { get; set; } = true;

        // --- アップスケーリング・レンダリング ---

        /// <summary>FidelityFX Super Resolution (FSR) 1.0 モード (0: OFF, 1: Performance, 2: Balanced, 3: Quality, 4: UltraQuality)</summary>
        public int FsrMode { get; set; } = 0;

        /// <summary>レンダリング方式 (0: Forward, 1: Deferred)</summary>
        public int RenderingPath { get; set; } = 1;

        /// <summary>イメージクオリティ／解像度スケール倍率 (0.5 ~ 2.0)</summary>
        public float ImageQualityScale { get; set; } = 1.0f;

        /// <summary>FidelityFX Contrast Adaptive Sharpening (CAS) の有無</summary>
        public bool EnableFidelityFxCas { get; set; } = false;

        /// <summary>アンチエイリアス手法 (0: OFF, 1: FXAA, 2: SMAA, 3: TAA)</summary>
        public int AntiAliasingType { get; set; } = 3;

        /// <summary>可変レートシェーディング (VRS) の有無</summary>
        public bool EnableVrs { get; set; } = false;

        // --- アセット・描画品質 ---

        /// <summary>テクスチャ解像度品質 (0: 低, 1: 中, 2: 高, 3: ウルトラ)</summary>
        public int TextureQuality { get; set; } = 3;

        /// <summary>テクスチャフィルタリング（異方性フィルタリング）品質 (0: 2x, 1: 4x, 2: 8x, 3: 16x)</summary>
        public int TextureFilteringQuality { get; set; } = 3;

        /// <summary>3DメッシュのLOD（詳細度）品質 (0: 低, 1: 中, 2: 高)</summary>
        public int MeshQuality { get; set; } = 2;

        // --- ライティング・レイトレーシング ---

        /// <summary>レイトレーシングの有効化</summary>
        public bool EnableRayTracing { get; set; } = true;

        /// <summary>Motion blur preference, independent of automatic camera focus</summary>
        public bool EnableMotionBlur { get; set; } = false;

        /// <summary>Portal texture scale, applied independently of the main camera</summary>
        public float PortalResolutionScale { get; set; } = 0.75f;

        /// <summary>グローバルイルミネーション (GI) & 反射の品質 (0: OFF, 1: 低, 2: 中, 3: 高)</summary>
        public int GiAndReflectionQuality { get; set; } = 2;

        /// <summary>反射量／反射強度係数 (0.0 ~ 1.0)</summary>
        public float ReflectionIntensity { get; set; } = 1.0f;

        /// <summary>アンビエントオクルージョン (AO) の有効化</summary>
        public bool EnableAo { get; set; } = true;

        /// <summary>スクリーンスペースリフレクション (SSR) の有効化</summary>
        public bool EnableSsr { get; set; } = true;

        /// <summary>ボリュームライト（霧・光のシャフト）品質 (0: OFF, 1: 低, 2: 中, 3: 高)</summary>
        public int VolumeLightQuality { get; set; } = 2;

        /// <summary>サブサーフェイススキャッタリング（肌の透け感表現）の有効化</summary>
        public bool EnableSss { get; set; } = true;

        // --- 影設定 ---

        /// <summary>メインシャドウの品質 (0: OFF, 1: 低, 2: 中, 3: 高)</summary>
        public int ShadowQuality { get; set; } = 3;

        /// <summary>コンタクトシャドウ（接地部の微細な影）の有効化</summary>
        public bool EnableContactShadow { get; set; } = true;

        /// <summary>影のキャッシュ（静的オブジェクトのシャドウマップ保持による高速化）の有効化</summary>
        public bool EnableShadowCache { get; set; } = true;

        // --- ポストエフェクト ---

        /// <summary>ブルーム（光あふれ効果）の有効化</summary>
        public bool EnableBloom { get; set; } = true;

        /// <summary>レンズフレアの有効化</summary>
        public bool EnableLensFlare { get; set; } = true;

        /// <summary>フィルム粒子（ホラー演出用ノイズ）の有効化</summary>
        public bool EnableFilmGrain { get; set; } = true;

        /// <summary>被写界深度（ピンボケ効果）の有効化</summary>
        public bool EnableDepthOfField { get; set; } = true;

        /// <summary>レンズゆがみ（魚眼・色収差等のゆがみエフェクト）の有効化</summary>
        public bool EnableLensDistortion { get; set; } = true;

        /// <summary>
        /// 画面と音量の設定を維持しながら描画品質プリセットを切り替える
        /// </summary>
        /// <param name="preset">適用する描画品質</param>
        public void SetQualityPreset(GraphicsQualityPreset preset)
        {
            if (preset == GraphicsQualityPreset.Custom) { QualityPreset = preset; return; }
            QualityPreset = preset;

            // 品質切り替え時に高品質プリセットの個別設定を次の品質へ持ち越さない
            int quality = (int)preset;
            TextureQuality = quality;
            TextureFilteringQuality = quality;
            MeshQuality = System.Math.Min(quality, 2);
            ShadowQuality = System.Math.Min(quality + 1, 3);
            // Ultra keeps rough-surface RT coverage with half-resolution sampling and a medium fog budget
            GiAndReflectionQuality = preset == GraphicsQualityPreset.Ultra ? 2 : System.Math.Min(quality + 1, 3);
            VolumeLightQuality = preset == GraphicsQualityPreset.Ultra ? 2 : System.Math.Min(quality + 1, 3);
            AntiAliasingType = quality == 0 ? 1 : quality == 1 ? 2 : 3;
            FsrMode = 0;
            ImageQualityScale = 1f;
            EnableRayTracing = preset == GraphicsQualityPreset.Ultra;
            EnableAo = quality > 0;
            EnableSsr = quality > 0;
            EnableContactShadow = quality >= 2;
            EnableBloom = true;
            EnableLensFlare = quality >= 2;
            EnableFilmGrain = true;
            EnableDepthOfField = quality >= 2;
            EnableMotionBlur = false;
            EnableLensDistortion = true;
            PortalResolutionScale = quality == 0 ? 0.5f : quality == 1 ? 0.625f : 0.75f;
        }

        /// <summary>Copy this scalar-only value object for independent editing</summary>
        /// <returns>An independent graphics draft</returns>
        public GraphicsSettings Clone() => (GraphicsSettings)MemberwiseClone();

        /// <summary>Compare display modes that require confirmation before persistence</summary>
        /// <param name="other">Previously confirmed settings</param>
        /// <returns>Whether resolution, refresh, fullscreen or HDR output changed</returns>
        public bool DisplayDiffersFrom(GraphicsSettings other) => Resolution != other.Resolution
            || ScreenMode != other.ScreenMode || RefreshRate != other.RefreshRate || EnableHDR != other.EnableHDR;

        /// <summary>Clamp persisted values before they reach rendering APIs</summary>
        public void Validate()
        {
            if (!System.Enum.IsDefined(typeof(GraphicsQualityPreset), QualityPreset)) QualityPreset = GraphicsQualityPreset.High;
            // Preserve older raster or full-resolution Ultra preferences while honestly reporting the tuned preset
            if (QualityPreset == GraphicsQualityPreset.Ultra &&
                (!EnableRayTracing || GiAndReflectionQuality != 2 || VolumeLightQuality != 2))
                QualityPreset = GraphicsQualityPreset.Custom;
            ScreenMode = System.Math.Clamp(ScreenMode, 0, 2);
            RefreshRate = System.Math.Clamp(RefreshRate, 24, 1000);
            FrameRateLimit = FrameRateLimit <= 0 ? 0 : System.Math.Clamp(FrameRateLimit, 30, 360);
            TextureQuality = System.Math.Clamp(TextureQuality, 0, 3);
            TextureFilteringQuality = System.Math.Clamp(TextureFilteringQuality, 0, 3);
            MeshQuality = System.Math.Clamp(MeshQuality, 0, 2);
            ShadowQuality = System.Math.Clamp(ShadowQuality, 0, 3);
            GiAndReflectionQuality = System.Math.Clamp(GiAndReflectionQuality, 0, 3);
            VolumeLightQuality = System.Math.Clamp(VolumeLightQuality, 0, 3);
            AntiAliasingType = System.Math.Clamp(AntiAliasingType, 0, 3);
            ImageQualityScale = float.IsNaN(ImageQualityScale) ? 1f : System.Math.Clamp(ImageQualityScale, 0.5f, 1f);
            PortalResolutionScale = float.IsNaN(PortalResolutionScale) ? 0.75f : System.Math.Clamp(PortalResolutionScale, 0.25f, 1f);
            BrightnessValue = float.IsNaN(BrightnessValue) ? 0.5f : System.Math.Clamp(BrightnessValue, 0f, 1f);
            ReflectionIntensity = float.IsNaN(ReflectionIntensity) ? 1f : System.Math.Clamp(ReflectionIntensity, 0f, 1f);
        }
    }
}
