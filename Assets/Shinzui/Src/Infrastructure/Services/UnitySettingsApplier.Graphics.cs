using System;
using System.Collections.Generic;
using System.Linq;
using Shinzui.Domain.Settings;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using GraphicsSettings = Shinzui.Domain.Settings.GraphicsSettings;

namespace Shinzui.Infrastructure.Services
{
    public partial class UnitySettingsApplier
    {
        private static GraphicsSettings _graphics;
        private static Volume _graphicsVolume;
        private static bool _graphicsDirty;
        private static bool _rayTracingActive;
        private static float _nextLightRefresh;

        /// <summary>現在のGPUとHDRPでレイトレーシングが使用可能か取得する</summary>
        /// <returns>実行中のHDRPで使用可能なら真</returns>
        public bool IsRayTracingSupported() => DetectRayTracing();

        /// <summary>レイトレーシングの実行環境を判定する</summary>
        /// <returns>GPUと現在のパイプラインが対応していれば真</returns>
        private static bool DetectRayTracing()
        {
            // コンソールではPC向けのレイトレーシング設定を適用しない
#if (UNITY_SWITCH || UNITY_SWITCH2 || UNITY_PS4 || UNITY_PS5 || UNITY_XBOXONE || UNITY_GAMECORE) && !UNITY_EDITOR
            return false;
#else
            // アセットの機能フラグと実行中のパイプラインの対応状態を確認する
            return SystemInfo.supportsRayTracing
                && UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline is HDRenderPipelineAsset asset
                && asset.currentPlatformRenderPipelineSettings.supportRayTracing
                && RenderPipelineManager.currentPipeline is HDRenderPipeline pipeline
                && pipeline.rayTracingSupported;
#endif
        }

        /// <summary>重複を除いた昇順の画面解像度を取得する</summary>
        /// <returns>幅x高さ形式の解像度一覧</returns>
        public IReadOnlyList<string> GetAvailableResolutions()
        {
            // 周波数違いの重複を除き現在の画面サイズも候補に含める
            return Screen.resolutions.Select(value => (value.width, value.height))
                .Append((Screen.width, Screen.height))
                .Where(value => value.Item1 > 0 && value.Item2 > 0)
                .Distinct().OrderBy(value => value.Item1).ThenBy(value => value.Item2)
                .Select(value => $"{value.Item1}x{value.Item2}").ToArray();
        }

        /// <summary>プリセットに対応するUnity品質レベルへ切り替える</summary>
        /// <param name="preset">指定されたプリセット</param>
        private static void ApplyQualityLevel(GraphicsPreset preset)
        {
            // カスタムでは現在の品質アセットを保つ
            string name = preset switch
            {
                GraphicsPreset.Low => "Low",
                GraphicsPreset.Medium => "Midium",
                GraphicsPreset.High or GraphicsPreset.Ultra => "High",
                _ => null
            };
            if (name == null) return;

            // アセットの再生成を避けるため変更時だけ切り替える
            int index = Array.IndexOf(QualitySettings.names, name);
            if (index >= 0 && index != QualitySettings.GetQualityLevel())
                QualitySettings.SetQualityLevel(index, true);
        }

        /// <summary>前回の実行で生成した描画設定を破棄する</summary>
        private static void ResetGraphicsOverrides()
        {
            // ランタイム専用のプロファイルとオブジェクトを破棄する
            if (_graphicsVolume != null)
            {
                var profile = _graphicsVolume.sharedProfile;
                if (profile != null)
                {
                    foreach (var component in profile.components)
                        UnityEngine.Object.Destroy(component);
                    UnityEngine.Object.Destroy(profile);
                }
                UnityEngine.Object.Destroy(_graphicsVolume.gameObject);
            }

            // 静的な状態を初期化する
            _graphicsVolume = null;
            _graphics = null;
            _graphicsDirty = false;
            _rayTracingActive = false;
            _nextLightRefresh = 0f;
        }

        /// <summary>描画設定用の永続Volumeを用意する</summary>
        private static void EnsureGraphicsVolume()
        {
            // シーン遷移後も同じVolumeを使用する
            if (_graphicsVolume != null) return;
            var owner = new GameObject("Runtime Graphics Options");
            owner.hideFlags = HideFlags.DontSave;
            UnityEngine.Object.DontDestroyOnLoad(owner);
            _graphicsVolume = owner.AddComponent<Volume>();
            _graphicsVolume.isGlobal = true;
            _graphicsVolume.priority = float.MaxValue;
            _graphicsVolume.weight = 1f;
            _graphicsVolume.sharedProfile = ScriptableObject.CreateInstance<VolumeProfile>();
            _graphicsVolume.sharedProfile.hideFlags = HideFlags.DontSave;
            _graphicsVolume.sharedProfile.name = "Runtime Graphics Options Profile";
            _graphicsDirty = true;
        }

        /// <summary>実行時プロファイルの指定コンポーネントを取得する</summary>
        /// <typeparam name="T">Volumeコンポーネントの型</typeparam>
        /// <returns>実行時専用のコンポーネント</returns>
        private static T GraphicsOverride<T>() where T : VolumeComponent
        {
            // 必要になったコンポーネントだけを生成する
            var profile = _graphicsVolume.sharedProfile;
            if (profile.TryGet<T>(out var component)) return component;
            // Add内部のCreateInstanceで生成しプロファイルの変更通知も更新する
            return profile.Add<T>();
        }

        /// <summary>設定変更とパイプラインの初期化完了を描画へ反映する</summary>
        private static void RefreshGraphicsOverrides()
        {
            // 編集モードや初回設定前はカメラやシーンに介入しない
            if (!UnityEngine.Application.isPlaying || _graphics == null) return;
            EnsureGraphicsVolume();
            bool rayTracing = _graphics.EnableRayTracing && DetectRayTracing();
            bool changed = _graphicsDirty || rayTracing != _rayTracingActive;

            // 起動直後や品質アセット切り替え後の対応状態を再反映する
            if (changed)
            {
                _rayTracingActive = rayTracing;
                ApplyVolumeGraphics();
                _graphicsDirty = false;
            }

            // 動的生成されたライトも定期的に設定対象へ取り込む
            if (changed || Time.unscaledTime >= _nextLightRefresh)
            {
                ApplyLightGraphics();
                _nextLightRefresh = Time.unscaledTime + 1f;
            }
        }

        /// <summary>GIとポストエフェクトを実行時Volumeへ反映する</summary>
        private static void ApplyVolumeGraphics()
        {
            // GIと反射の品質およびレイトレーシング方式を指定する
            var g = _graphics;
            int tier = Mathf.Clamp(g.GiAndReflectionQuality - 1, 0, 2);
            var tracing = _rayTracingActive ? RayCastingMode.RayTracing : RayCastingMode.RayMarching;
            var gi = GraphicsOverride<GlobalIllumination>();
            gi.enable.Override(g.GiAndReflectionQuality > 0);
            gi.tracing.Override(tracing);
            gi.quality.Override(tier);
            gi.mode.Override(RayTracingMode.Quality);
            gi.sampleCount.Override(1 << tier);
            gi.fullResolutionSS.Override(tier > 0);
            var reflection = GraphicsOverride<ScreenSpaceReflection>();
            reflection.enabled.Override(g.EnableSsr && g.GiAndReflectionQuality > 0);
            reflection.tracing.Override(tracing);
            reflection.quality.Override(tier);
            reflection.mode.Override(RayTracingMode.Quality);
            reflection.sampleCount.Override(1 << tier);

            // AOと接地影を有効化し無効時はシーン側の値も抑止する
            var ao = GraphicsOverride<ScreenSpaceAmbientOcclusion>();
            ao.intensity.Override(g.EnableAo ? 1f : 0f);
            ao.rayTracing.Override(_rayTracingActive && g.EnableAo);
            ao.quality.Override(tier);
            GraphicsOverride<ContactShadows>().enable.Override(g.EnableContactShadow && g.ShadowQuality > 0);
            var shadows = GraphicsOverride<HDShadowSettings>();
            shadows.maxShadowDistance.Override(g.ShadowQuality switch { 0 => 0f, 1 => 40f, 2 => 80f, _ => 150f });
            shadows.cascadeShadowSplitCount.Override(g.ShadowQuality >= 3 ? 4 : 2);

            // シーンの霧の色や密度を保ち描画品質だけを変更する
            var fog = GraphicsOverride<Fog>();
            fog.enabled.value = true;
            fog.enabled.overrideState = g.VolumeLightQuality > 0;
            fog.enableVolumetricFog.Override(g.VolumeLightQuality > 0);
            fog.quality.Override(Mathf.Clamp(g.VolumeLightQuality - 1, 0, 2));
            fog.denoisingMode.Override(g.VolumeLightQuality >= 2 ? FogDenoisingMode.Both : FogDenoisingMode.Gaussian);

            // オフ時はゼロを上書きしオン時はシーンの演出値を使用する
            var bloom = GraphicsOverride<Bloom>();
            bloom.intensity.value = 0f;
            bloom.intensity.overrideState = !g.EnableBloom;
            var flare = GraphicsOverride<ScreenSpaceLensFlare>();
            flare.intensity.value = 0f;
            flare.intensity.overrideState = !g.EnableLensFlare;
            var grain = GraphicsOverride<FilmGrain>();
            grain.intensity.value = 0f;
            grain.intensity.overrideState = !g.EnableFilmGrain;
            var depth = GraphicsOverride<DepthOfField>();
            depth.focusMode.value = DepthOfFieldMode.Off;
            depth.focusMode.overrideState = !g.EnableDepthOfField;
            var distortion = GraphicsOverride<LensDistortion>();
            distortion.intensity.value = 0f;
            distortion.intensity.overrideState = !g.EnableLensDistortion;
            var aberration = GraphicsOverride<ChromaticAberration>();
            aberration.intensity.value = 0f;
            aberration.intensity.overrideState = !g.EnableLensDistortion;

            // 中央値を基準に画面の明るさを露出補正へ変換する
            GraphicsOverride<ColorAdjustments>().postExposure.Override((Mathf.Clamp01(g.BrightnessValue) - 0.5f) * 4f);
        }

        /// <summary>ライトの影解像度とレイトレース影を設定する</summary>
        private static void ApplyLightGraphics()
        {
            // シーン内のライトへHDRP固有の影設定を適用する
            int quality = Mathf.Clamp(_graphics.ShadowQuality, 0, 3);
            foreach (var light in UnityEngine.Object.FindObjectsByType<HDAdditionalLightData>(FindObjectsSortMode.None))
            {
                light.SetShadowResolution(quality switch { 0 => 256, 1 => 512, 2 => 1024, _ => 2048 });
                light.SetShadowResolutionOverride(true);
                light.useContactShadow.useOverride = true;
                light.useContactShadow.@override = _graphics.EnableContactShadow && quality > 0;
                light.useScreenSpaceShadows = _rayTracingActive && quality > 0;
                light.useRayTracedShadows = _rayTracingActive && quality > 0;
                light.numRayTracingSamples = quality >= 3 ? 4 : 1;
                light.filterTracedShadow = true;
            }
        }

        /// <summary>カメラの描画機能とアンチエイリアスを設定する</summary>
        /// <param name="data">対象カメラのHDRP設定</param>
        private static void ApplyCameraGraphics(HDAdditionalCameraData data)
        {
            // 通常のゲームカメラへ設定用Volumeと個別描画設定を適用する
            if (_graphics == null) return;
            var g = _graphics;
            data.volumeLayerMask |= 1 << _graphicsVolume.gameObject.layer;
            data.customRenderingSettings = true;
            data.renderingPathCustomFrameSettingsOverrideMask.mask[(uint)FrameSettingsField.MSAAMode] = true;
            data.renderingPathCustomFrameSettings.msaaMode = MSAAMode.None;
            data.antialiasing = g.AntiAliasingType switch
            {
                1 => HDAdditionalCameraData.AntialiasingMode.FastApproximateAntialiasing,
                2 => HDAdditionalCameraData.AntialiasingMode.SubpixelMorphologicalAntiAliasing,
                3 => HDAdditionalCameraData.AntialiasingMode.TemporalAntialiasing,
                _ => HDAdditionalCameraData.AntialiasingMode.None
            };

            // 対象項目のマスクだけを有効にし他のカメラ設定を保つ
            SetFrameFeature(data, FrameSettingsField.Postprocess, true);
            SetFrameFeature(data, FrameSettingsField.Bloom, g.EnableBloom || g.EnableLensFlare);
            SetFrameFeature(data, FrameSettingsField.FilmGrain, g.EnableFilmGrain);
            SetFrameFeature(data, FrameSettingsField.DepthOfField, g.EnableDepthOfField);
            SetFrameFeature(data, FrameSettingsField.LensDistortion, g.EnableLensDistortion);
            SetFrameFeature(data, FrameSettingsField.ChromaticAberration, g.EnableLensDistortion);
            SetFrameFeature(data, FrameSettingsField.SSAO, g.EnableAo);
            SetFrameFeature(data, FrameSettingsField.SSR, g.EnableSsr && g.GiAndReflectionQuality > 0);
            SetFrameFeature(data, FrameSettingsField.SSGI, g.GiAndReflectionQuality > 0);
            SetFrameFeature(data, FrameSettingsField.ContactShadows, g.EnableContactShadow && g.ShadowQuality > 0);
            SetFrameFeature(data, FrameSettingsField.SubsurfaceScattering, g.EnableSss);
            // 霧の大気散乱はシーン既定を保ちボリューム有効時だけ保証する
            if (g.VolumeLightQuality > 0) SetFrameFeature(data, FrameSettingsField.AtmosphericScattering, true);

            SetFrameFeature(data, FrameSettingsField.Volumetrics, g.VolumeLightQuality > 0);
            SetFrameFeature(data, FrameSettingsField.ReprojectionForVolumetrics, g.VolumeLightQuality >= 2);
            SetFrameFeature(data, FrameSettingsField.RayTracing, _rayTracingActive);
            SetFrameFeature(data, FrameSettingsField.ShadowMaps, g.ShadowQuality > 0);
            SetFrameFeature(data, FrameSettingsField.ScreenSpaceShadows, _rayTracingActive && g.ShadowQuality > 0);
            SetFrameFeature(data, FrameSettingsField.LensFlareScreenSpace, g.EnableLensFlare);
            SetFrameFeature(data, FrameSettingsField.LensFlareDataDriven, g.EnableLensFlare);
        }

        /// <summary>指定したフレーム設定だけを上書きする</summary>
        /// <param name="data">対象カメラ</param>
        /// <param name="field">上書きする機能</param>
        /// <param name="enabled">有効状態</param>
        private static void SetFrameFeature(HDAdditionalCameraData data, FrameSettingsField field, bool enabled)
        {
            // 上書きマスクと設定値を同時に更新する
            data.renderingPathCustomFrameSettingsOverrideMask.mask[(uint)field] = true;
            data.renderingPathCustomFrameSettings.SetEnabled(field, enabled);
        }
    }
}
