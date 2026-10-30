using System;
using System.Linq;
using System.Reflection;
using Shinzui.Infrastructure.Rendering;
using Shinzui.Infrastructure.Rendering.GameOverDissolve;
using Shinzui.Infrastructure.Rendering.InverseScanLine;
using Shinzui.Infrastructure.Rendering.Scanline;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using HdrpExposure = UnityEngine.Rendering.HighDefinition.Exposure;
using LegacyExposure = Shinzui.Infrastructure.Rendering.Exposure.ExposureVolume;
using LegacyExposureMode = Shinzui.Infrastructure.Rendering.Exposure.ExposureMode;
using LegacyAdaptationMode = Shinzui.Infrastructure.Rendering.Exposure.EyeAdaptationMode;
using Object = UnityEngine.Object;

namespace Shinzui.Editor.HdrpMigration
{
    /// <summary>
    /// HDRPの品質別パイプラインと既定Volumeを再実行可能な手順で準備する
    /// </summary>
    public static class HdrpPipelineSetup
    {
        private const string Folder = "Assets/Shinzui/GraphicSettings";
        private static readonly Type[] Effects =
        {
            typeof(PixelationVolume), typeof(ScanlineVolume),
            typeof(InverseScanlineVolume), typeof(GameOverDissolveVolume)
        };
        private static readonly string[] ShaderNames =
        {
            "Custom/Pixelation", "Custom/Scanline", "Custom/InverseScanline", "Custom/GameOverDissolve"
        };

        /// <summary>
        /// 品質別HDRPアセットを作成しグラフィックス設定へ割り当てる
        /// </summary>
        [MenuItem("Shinzui/HDRP Migration/1 Setup Pipeline")]
        public static void SetupPipeline()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode before HDRP setup");

            // 既存のURPアセットを上書きせずHDRP専用アセットを再利用する
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Shinzui", "GraphicSettings");
            VolumeProfile profile = CreateProfile(Folder + "/HDRP_DefaultVolumeProfile.asset", 2);
            HDRenderPipelineAsset high = CreatePipeline("High", 2);
            HDRenderPipelineAsset medium = CreatePipeline("Midium", 1);
            HDRenderPipelineAsset low = CreatePipeline("Low", 0);
            GraphicsSettings.defaultRenderPipeline = high;

            /* 各品質を選択してHDRPを割り当て処理後に元の品質へ戻す */
            int originalQuality = QualitySettings.GetQualityLevel();
            try
            {
                for (int i = 0; i < QualitySettings.names.Length; i++)
                {
                    /* PCとMobileを含む残りの品質名はHighへ対応させる */
                    string quality = QualitySettings.names[i];
                    QualitySettings.SetQualityLevel(i, false);
                    QualitySettings.renderPipeline = quality == "Low" ? low : quality == "Midium" ? medium : high;
                }
            }
            finally
            {
                QualitySettings.SetQualityLevel(originalQuality, false);
            }

            PlayerSettings.colorSpace = ColorSpace.Linear;

            // HDRP内部の標準初期化を使いシェーダー等のリソースも設定する
            Object global = EnsureGlobalSettings();
            ConfigureGlobalSettings(global, profile);
            IncludeShaders();
            AssetDatabase.SaveAssets();
            Debug.Log("HDRP setup complete: High / Midium / Low, RGBA16F, ray tracing disabled");
        }

        /// <summary>
        /// 品質別のパイプラインを作成または更新する
        /// </summary>
        /// <param name="quality">既存の品質名</param>
        /// <param name="tier">LowからHighまでの番号</param>
        /// <returns>設定済みHDRPアセット</returns>
        private static HDRenderPipelineAsset CreatePipeline(string quality, int tier)
        {
            string path = $"{Folder}/Quality{quality}_HDRP.asset";
            var asset = AssetDatabase.LoadAssetAtPath<HDRenderPipelineAsset>(path);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<HDRenderPipelineAsset>();
                AssetDatabase.CreateAsset(asset, path);
            }

            // 暗所の階調を全品質で維持し重い画面空間効果を段階的に減らす
            RenderPipelineSettings settings = asset.currentPlatformRenderPipelineSettings;
            settings.colorBufferFormat = RenderPipelineSettings.ColorBufferFormat.R16G16B16A16;
            settings.customBufferFormat = RenderPipelineSettings.CustomBufferFormat.R16G16B16A16;
            settings.supportedLitShaderMode = RenderPipelineSettings.SupportedLitShaderMode.DeferredOnly;
            settings.supportRayTracing = false;
            settings.supportSSGI = tier == 2;
            settings.supportSSR = tier > 0;
            settings.supportSSRTransparent = false;
            settings.supportSSAO = true;
            settings.supportVolumetrics = true;
            settings.supportVolumetricClouds = false;
            settings.supportMotionVectors = true;
            settings.supportCustomPass = true;
            settings.lightSettings.useContactShadow = new BoolScalableSetting(
                new[] { tier > 0, tier > 0, tier > 0 }, ScalableSettingSchemaId.With3Levels);

            // アトラス容量と各ライトの既定解像度を品質に合わせる
            var shadows = settings.hdShadowInitParams;
            int maximum = tier == 2 ? 4096 : tier == 1 ? 2048 : 1024;
            shadows.punctualLightShadowAtlas.shadowAtlasResolution = tier == 2 ? 8192 : tier == 1 ? 4096 : 2048;
            shadows.areaLightShadowAtlas.shadowAtlasResolution = tier == 2 ? 4096 : 2048;
            shadows.maxDirectionalShadowMapResolution = maximum;
            shadows.maxPunctualShadowMapResolution = maximum;
            shadows.maxAreaShadowMapResolution = maximum;
            shadows.shadowResolutionDirectional = new IntScalableSetting(
                new[] { maximum / 4, maximum / 2, maximum, maximum }, ScalableSettingSchemaId.With4Levels);
            shadows.shadowResolutionPunctual = new IntScalableSetting(
                new[] { maximum / 8, maximum / 4, maximum / 2, maximum }, ScalableSettingSchemaId.With4Levels);
            shadows.shadowResolutionArea = new IntScalableSetting(
                new[] { maximum / 8, maximum / 4, maximum / 2, maximum }, ScalableSettingSchemaId.With4Levels);
            shadows.punctualShadowFilteringQuality = (HDShadowFilteringQuality)tier;
            shadows.directionalShadowFilteringQuality = (HDShadowFilteringQuality)tier;
            shadows.supportContactShadows = tier > 0;
            shadows.supportScreenSpaceShadows = tier > 0;
            settings.hdShadowInitParams = shadows;
            asset.currentPlatformRenderPipelineSettings = settings;
            asset.volumeProfile = CreateProfile($"{Folder}/Quality{quality}_HDRP_VolumeProfile.asset", tier);
            EditorUtility.SetDirty(asset);
            return asset;
        }

        /// <summary>
        /// 中立な既定Volumeを初回だけ作成し再実行時は調整値を維持する
        /// </summary>
        /// <param name="path">保存先</param>
        /// <param name="tier">効果の品質番号</param>
        /// <returns>既定Volumeプロファイル</returns>
        private static VolumeProfile CreateProfile(string path, int tier)
        {
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if (profile != null) return profile;

            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, path);

            // 暗所で過度に持ち上がらないヒストグラム露出と自然な順応を設定する
            var exposure = profile.Add<HdrpExposure>(true);
            HdrpGameplayTuning.ConfigureExposure(exposure);

            // 光源による散乱を主体にし環境色による白い霧を抑える
            var fog = profile.Add<Fog>(true);
            fog.enabled.Override(true);
            fog.enableVolumetricFog.Override(true);
            fog.colorMode.Override(FogColorMode.ConstantColor);
            fog.color.Override(Color.black);
            fog.albedo.Override(new Color(0.9f, 0.9f, 0.9f));
            fog.meanFreePath.Override(80f);
            fog.baseHeight.Override(-10f);
            fog.maximumHeight.Override(20f);
            fog.depthExtent.Override(64f);
            fog.anisotropy.Override(0.25f);
            fog.globalLightProbeDimmer.Override(0f);
            fog.quality.Override(tier);

            // 画面空間の間接光と反射を使用しレイトレーシングを要求しない
            var gi = profile.Add<GlobalIllumination>(true);
            gi.enable.Override(tier == 2);
            gi.tracing.Override(RayCastingMode.RayMarching);
            gi.quality.Override(tier);
            var reflections = profile.Add<ScreenSpaceReflection>(true);
            reflections.enabled.Override(tier > 0);
            reflections.enabledTransparent.Override(false);
            reflections.tracing.Override(RayCastingMode.RayMarching);
            reflections.quality.Override(tier);
            var contact = profile.Add<ContactShadows>(true);
            contact.enable.Override(tier > 0);
            contact.length.Override(0.15f);
            contact.maxDistance.Override(tier == 2 ? 40f : 20f);
            contact.quality.Override(tier);
            profile.Add<Tonemapping>(true).mode.Override(TonemappingMode.ACES);
            var bloom = profile.Add<Bloom>(true);
            bloom.intensity.Override(0.15f);
            bloom.threshold.Override(1f);
            bloom.quality.Override(tier);

            // 既定画面は中立に保ち既存シーンのVolumeから演出値を上書きする
            profile.Add<PixelationVolume>(true).intensity.Override(0f);
            var scanline = profile.Add<ScanlineVolume>(true);
            scanline.strength.Override(0f);
            scanline.curvature.Override(0f);
            scanline.fineLines.Override(0f);
            scanline.vignette.Override(0f);
            scanline.flicker.Override(0f);
            profile.Add<InverseScanlineVolume>(true).glitchProbability.Override(0f);
            profile.Add<GameOverDissolveVolume>(true).progress.Override(0f);

            // Volumeコンポーネントをサブアセットとして永続化する
            foreach (VolumeComponent component in profile.components)
            {
                component.name = component.GetType().Name;
                AssetDatabase.AddObjectToAsset(component, profile);
                EditorUtility.SetDirty(component);
            }
            EditorUtility.SetDirty(profile);
            return profile;
        }

        /// <summary>
        /// HDRP標準のGlobal Settings初期化を呼び出す
        /// </summary>
        /// <returns>登録済みGlobal Settingsアセット</returns>
        private static Object EnsureGlobalSettings()
        {
            // HDRP 17.5では型とEnsureがinternalのためこの箇所だけリフレクションを使う
            Type type = typeof(HDRenderPipelineAsset).Assembly.GetType(
                "UnityEngine.Rendering.HighDefinition.HDRenderPipelineGlobalSettings", true);
            MethodInfo ensure = type.GetMethod("Ensure", BindingFlags.Static | BindingFlags.NonPublic);
            if (ensure == null) throw new MissingMethodException(type.FullName, "Ensure");
            var settings = (Object)ensure.Invoke(null, new object[] { true });
            if (settings == null) throw new InvalidOperationException("HDRP Global Settings initialization failed");
            return settings;
        }

        /// <summary>
        /// Global Settings内の設定コンテナを型名で取得する
        /// </summary>
        /// <param name="settings">Global Settingsのシリアライズ表現</param>
        /// <param name="typeName">設定コンテナの型名</param>
        /// <returns>対象コンテナのプロパティ</returns>
        private static SerializedProperty FindSettings(SerializedObject settings, string typeName)
        {
            // HDRP 17.5の管理参照コンテナを辿り旧移行用フィールドを避ける
            SerializedProperty iterator = settings.GetIterator();
            while (iterator.Next(true))
            {
                if (iterator.propertyType == SerializedPropertyType.ManagedReference
                    && iterator.managedReferenceValue?.GetType().Name == typeName) return iterator.Copy();
            }
            throw new InvalidOperationException($"HDRP settings container missing: {typeName}");
        }

        /// <summary>
        /// 既定Volumeとカスタムポスト処理の順序を登録する
        /// </summary>
        /// <param name="global">Global Settingsアセット</param>
        /// <param name="profile">全品質の基準プロファイル</param>
        private static void ConfigureGlobalSettings(Object global, VolumeProfile profile)
        {
            var settings = new SerializedObject(global);
            var defaults = (HDRPDefaultVolumeProfileSettings)FindSettings(settings,
                "HDRPDefaultVolumeProfileSettings").managedReferenceValue;
            defaults.volumeProfile = profile;
            settings.Update();
            SerializedProperty orders = FindSettings(settings, "CustomPostProcessOrdersSettings");
            string[] lists =
            {
                "m_BeforeTransparentCustomPostProcesses", "m_BeforeTAACustomPostProcesses",
                "m_BeforePostProcessCustomPostProcesses", "m_AfterPostProcessBlursCustomPostProcesses",
                "m_AfterPostProcessCustomPostProcesses"
            };

            // 他の効果を残し対象の四種類だけを所定のリストと順序へ移動する
            foreach (string list in lists)
            {
                SerializedProperty types = orders.FindPropertyRelative(list)
                    .FindPropertyRelative("m_CustomPostProcessTypesAsString");
                for (int i = types.arraySize - 1; i >= 0; i--)
                {
                    string type = types.GetArrayElementAtIndex(i).stringValue;
                    if (Effects.Any(effect => type.Split(',')[0] == effect.FullName)) types.DeleteArrayElementAtIndex(i);
                }
                if (list != "m_AfterPostProcessCustomPostProcesses") continue;
                foreach (Type effect in Effects)
                {
                    int index = types.arraySize++;
                    types.GetArrayElementAtIndex(index).stringValue = effect.AssemblyQualifiedName;
                }
            }

            // カメラの既定フレーム設定も必要な機能を有効にする
            SerializedProperty camera = FindSettings(settings, "RenderingPathFrameSettings").FindPropertyRelative("m_Camera");
            var frame = (FrameSettings)camera.boxedValue;
            foreach (FrameSettingsField field in new[]
            {
                FrameSettingsField.Postprocess, FrameSettingsField.CustomPostProcess, FrameSettingsField.ExposureControl,
                FrameSettingsField.SSGI, FrameSettingsField.SSR, FrameSettingsField.ContactShadows,
                FrameSettingsField.AtmosphericScattering, FrameSettingsField.Volumetrics
            }) frame.SetEnabled(field, true);
            camera.boxedValue = frame;
            settings.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(global);
        }

        /// <summary>
        /// 実行時に名前で取得するシェーダーをビルドから除外させない
        /// </summary>
        private static void IncludeShaders()
        {
            var graphics = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset")[0]);
            SerializedProperty shaders = graphics.FindProperty("m_AlwaysIncludedShaders");
            foreach (string name in ShaderNames)
            {
                Shader shader = Shader.Find(name);
                if (shader == null) throw new InvalidOperationException($"Custom post-process shader missing: {name}");
                bool found = false;
                for (int i = 0; i < shaders.arraySize; i++) found |= shaders.GetArrayElementAtIndex(i).objectReferenceValue == shader;
                if (found) continue;
                int index = shaders.arraySize++;
                shaders.GetArrayElementAtIndex(index).objectReferenceValue = shader;
            }
            graphics.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// 旧露出Volumeの値をHDRP Exposureへ移し旧サブアセットを削除する
        /// </summary>
        [MenuItem("Shinzui/HDRP Migration/2 Migrate Exposure Profiles")]
        public static void MigrateExposureProfiles()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode before exposure migration");
            int count = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:VolumeProfile", new[] { "Assets/Shinzui" }))
            {
                var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(AssetDatabase.GUIDToAssetPath(guid));
                LegacyExposure old = profile.components.OfType<LegacyExposure>().FirstOrDefault();
                if (old == null) continue;
                HdrpExposure exposure = profile.components.OfType<HdrpExposure>().FirstOrDefault();
                if (exposure == null)
                {
                    exposure = profile.Add<HdrpExposure>();
                    exposure.name = "Exposure";
                    exposure.active = old.active;
                    exposure.mode.value = old.mode.value == LegacyExposureMode.Fixed ? ExposureMode.Fixed
                        : old.mode.value == LegacyExposureMode.Automatic ? ExposureMode.Automatic : ExposureMode.AutomaticHistogram;
                    exposure.mode.overrideState = old.mode.overrideState;
                    CopyParameter(old.filtering, exposure.histogramPercentages);
                    CopyParameter(old.minLuminance, exposure.limitMin);
                    CopyParameter(old.maxLuminance, exposure.limitMax);
                    CopyParameter(old.exposureCompensation, exposure.compensation);
                    exposure.adaptationMode.value = old.eyeAdaptation.value == LegacyAdaptationMode.Instant
                        ? AdaptationMode.Fixed : AdaptationMode.Progressive;
                    exposure.adaptationMode.overrideState = old.eyeAdaptation.overrideState;

                    // 旧SpeedUpは露出倍率の増加を表すため明所から暗所への順応へ対応する
                    CopyParameter(old.speedUp, exposure.adaptationSpeedLightToDark);
                    CopyParameter(old.speedDown, exposure.adaptationSpeedDarkToLight);
                    if (old.mode.value == LegacyExposureMode.Fixed)
                    {
                        // 旧固定値はEVでなく直接倍率だったため対数の補正値へ変換する
                        exposure.fixedExposure.value = ColorUtils.ConvertExposureToEV100(
                            Mathf.Max(old.exposureCompensation.value, 0.0001f));
                        exposure.fixedExposure.overrideState = old.exposureCompensation.overrideState;
                        exposure.compensation.value = 0f;
                    }
                    AssetDatabase.AddObjectToAsset(exposure, profile);
                    EditorUtility.SetDirty(exposure);
                }

                // 旧ポータル補正は描画側で未使用だったため主カメラの補正には加算しない
                if (old.portalExposureCompensation.overrideState && !Mathf.Approximately(old.portalExposureCompensation.value, 1f))
                    Debug.Log($"{profile.name}: unused legacy portalExposureCompensation={old.portalExposureCompensation.value}", profile);
                profile.components.Remove(old);
                Object.DestroyImmediate(old, true);
                EditorUtility.SetDirty(profile);
                count++;
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"HDRP Exposure migration complete: {count} profiles");
        }

        /// <summary>
        /// パラメータの値と上書き状態を保持して転記する
        /// </summary>
        /// <typeparam name="T">パラメータの値型</typeparam>
        /// <param name="source">旧パラメータ</param>
        /// <param name="target">新パラメータ</param>
        private static void CopyParameter<T>(VolumeParameter<T> source, VolumeParameter<T> target)
        {
            target.value = source.value;
            target.overrideState = source.overrideState;
        }
    }
}
