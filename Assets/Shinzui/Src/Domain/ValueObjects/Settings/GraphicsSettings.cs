namespace Shinzui.Domain.Settings
{
    /// <summary>
    /// グラフィックスおよび描画品質に関する設定データを保持するドメインモデル
    /// </summary>
    [System.Serializable]
    public class GraphicsSettings
    {
        // --- 基本・画面設定 ---
        
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
        public int TextureQuality { get; set; } = 2;

        /// <summary>テクスチャフィルタリング（異方性フィルタリング）品質 (0: 2x, 1: 4x, 2: 8x, 3: 16x)</summary>
        public int TextureFilteringQuality { get; set; } = 2;

        /// <summary>3DメッシュのLOD（詳細度）品質 (0: 低, 1: 中, 2: 高)</summary>
        public int MeshQuality { get; set; } = 2;

        // --- ライティング・レイトレーシング ---

        /// <summary>レイトレーシングの有効化</summary>
        public bool EnableRayTracing { get; set; } = false;

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
        public int ShadowQuality { get; set; } = 2;

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
    }
}
