using System;
using System.Linq;
using Shinzui.Editor.HdrpMigration;
using Shinzui.Infrastructure.Lighting;
using Shinzui.View.Flashlight;
using Shinzui.View.GenerateTunnel;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace Shinzui.Editor.LookDev
{
    /// <summary>照明とカメラのルックを既存アセットへ反映する</summary>
    public static class TunnelLookEnvironment
    {
        private static int fixtures, flashlights, generators, skipped, bounceRemoved;
        private static Texture cookie;
        private static readonly FrameSettingsField[] Features = { FrameSettingsField.Decals,
            FrameSettingsField.AtmosphericScattering, FrameSettingsField.Volumetrics, FrameSettingsField.SSGI,
            FrameSettingsField.SSR, FrameSettingsField.SSAO, FrameSettingsField.ContactShadows };

        /// <summary>器具の配光と懐中電灯と保存済み配置間隔を調整する</summary>
        [MenuItem("Shinzui/LookDev/3 Tune Tunnel Lighting")]
        public static void TuneLighting()
        {
            HdrpVerificationAssets.RequireSavedScenes();
            fixtures = flashlights = generators = skipped = bounceRemoved = 0;
            FlashlightBeamAssets.RebuildCookie();
            cookie = AssetDatabase.LoadAssetAtPath<Texture>("Assets/Shinzui/Art/Flashlight/FlashlightBeam.asset");
            HdrpVerificationAssets.VisitAssets(TuneRoot);
            AssetDatabase.SaveAssets();
            Debug.Log($"Tunnel lighting summary: fixtures={fixtures}, flashlights={flashlights}, generators={generators}, cookies=1, skipped={skipped}, bounce lights removed={bounceRemoved}; LED target=1.1lux, fluorescent target=1.45lux, measured range/beam; main tunnels=SaveStage fluorescent/crown centreline/5m nominal/dead 0%/flicker 0%; side passages=LED/12m; flashlight=3000 emitted lm after cookie/5000K/70deg/35m");
        }

        /// <summary>既知の役割に限定してライトと生成設定を変更する</summary>
        /// <param name="root">処理階層</param>
        /// <param name="path">保存パス</param>
        /// <param name="persistentRoot">元プレハブ</param>
        /// <returns>変更した場合はtrue</returns>
        private static bool TuneRoot(GameObject root, string path, GameObject persistentRoot)
        {
            bool changed = false;
            /* 取り外すのは補助光源のコンポーネントだけとしプレハブのルートを維持する */
            foreach (Light light in root.GetComponentsInChildren<Light>(true))
            {
                if (!TunnelLookLighting.IsCeilingBounce(light)) continue;
                string hierarchy = HdrpVerificationAssets.Hierarchy(light.transform);
                if (light.TryGetComponent(out HDAdditionalLightData data)) UnityEngine.Object.DestroyImmediate(data);
                UnityEngine.Object.DestroyImmediate(light);
                bounceRemoved++;
                changed = true;
                Debug.Log($"Tunnel fake GI light removed: {path} / {hierarchy}");
            }
            /* プレハブ編集中に別の開いているシーンの床を拾わない */
            GameObject[] geometryRoots = persistentRoot != null ? new[] { root } : root.scene.GetRootGameObjects();
            Collider[] surfaces = geometryRoots.SelectMany(r => r.GetComponentsInChildren<Collider>(true)).ToArray();
            Renderer[] meshes = geometryRoots.SelectMany(r => r.GetComponentsInChildren<Renderer>(true)).ToArray();
            Physics.SyncTransforms();
            foreach (TunnelGenerator generator in root.GetComponentsInChildren<TunnelGenerator>(true))
            {
                var serialized = new SerializedObject(generator);
                serialized.FindProperty("ceilingLightSpacing").floatValue = 12f;
                serialized.FindProperty("mainTunnelLightSpacing").floatValue = 5f;
                serialized.FindProperty("deadFixtureFraction").floatValue = 0f;
                serialized.FindProperty("flickerFixtureFraction").floatValue = 0f;
                changed |= serialized.ApplyModifiedPropertiesWithoutUndo();
                Record(generator);
                generators++;
                Debug.Log($"Tunnel main spacing=5m (SaveStage): {path} / {HdrpVerificationAssets.Hierarchy(generator.transform)}; fluorescent/crown centreline, side LED spacing=12m, dead=0%, flicker=0%, main loop spacing fitted to an integer fixture count");
            }
            foreach (FlashlightView view in root.GetComponentsInChildren<FlashlightView>(true))
            {
                var serialized = new SerializedObject(view);
                Light light = serialized.FindProperty("flashlightLight").objectReferenceValue as Light;
                if (light == null)
                {
                    skipped++;
                    Debug.LogWarning($"Tunnel flashlight skipped (missing reference): {path} / {HdrpVerificationAssets.Hierarchy(view.transform)}");
                    continue;
                }
                TunnelLookLighting.ConfigureFlashlight(light, cookie);
                serialized.FindProperty("beamShadows").intValue = (int)LightShadows.Soft;
                serialized.FindProperty("useRealisticBeam").boolValue = true;
                serialized.FindProperty("beamDownwardAngle").floatValue = 8f;
                serialized.FindProperty("beamFocusDistance").floatValue = 8f;
                var obstruction = serialized.FindProperty("beamObstructionMask");
                int playerLayer = LayerMask.NameToLayer("Player");
                if (playerLayer >= 0) obstruction.intValue &= ~(1 << playerLayer);
                serialized.FindProperty("scattering").floatValue = 0.65f;
                serialized.FindProperty("enableVolumetric").boolValue = true;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                foreach (Renderer renderer in view.GetSelfShadowRenderers())
                {
                    renderer.shadowCastingMode = ShadowCastingMode.Off;
                    Record(renderer);
                }
                /* 旧補助光は点灯系に接続せず独立した照明としても無効のまま保持する */
                foreach (Light legacy in view.GetComponentsInChildren<Light>(true))
                {
                    if (legacy == light || legacy.name != "Area Light") continue;
                    legacy.enabled = false;
                    Record(legacy);
                    Debug.Log($"Legacy flashlight fill disabled: {path} / {HdrpVerificationAssets.Hierarchy(legacy.transform)}");
                }
                Record(view);
                Record(light);
                Record(light.GetComponent<HDAdditionalLightData>());
                changed = true;
                flashlights++;
                Debug.Log($"Tunnel flashlight tuned: {path} / {HdrpVerificationAssets.Hierarchy(light.transform)}, peak={light.intensity:F0}cd, emitted flux=3000lm after cookie/angular falloff, outer/inner=70/60deg, range=35m, aim=8deg down, shadow near=0.35m");
            }
            foreach (Light light in root.GetComponentsInChildren<Light>(true))
            {
                string role = (path + "/" + HdrpVerificationAssets.Hierarchy(light.transform)).ToLowerInvariant();
                if (!role.Contains("tunnelledlight") && !role.Contains("tunnelfluorescentlight")
                    && !role.Contains("ceiling lights") && !role.Contains("fluorescent illumination")) continue;
                bool fluorescent = role.Contains("fluorescent");
                string calibration = TunnelLookLighting.ConfigureFixture(light, surfaces, meshes, fluorescent);
                Record(light.transform);
                Record(light);
                Record(light.GetComponent<HDAdditionalLightData>());
                fixtures++;
                changed = true;
                Debug.Log($"Tunnel fixture tuned: {path} / {HdrpVerificationAssets.Hierarchy(light.transform)}, {calibration}");
            }
            foreach (HDAdditionalCameraData data in root.GetComponentsInChildren<HDAdditionalCameraData>(true))
            {
                if (!data.customRenderingSettings) continue;
                foreach (FrameSettingsField field in Features)
                {
                    data.renderingPathCustomFrameSettings.SetEnabled(field, true);
                    data.renderingPathCustomFrameSettingsOverrideMask.mask[(uint)field] = true;
                }
                Record(data);
                changed = true;
            }
            foreach (Volume volume in root.GetComponentsInChildren<Volume>(true))
                Debug.Log($"Tunnel active profile reference: {path} / {HdrpVerificationAssets.Hierarchy(volume.transform)} -> {AssetDatabase.GetAssetPath(volume.sharedProfile)}, weight={volume.weight}, priority={volume.priority}");
            return changed;
        }

        /// <summary>既存プロファイルを暗所の中立なルックへ調整する</summary>
        [MenuItem("Shinzui/LookDev/4 Tune Look Profiles")]
        public static void TuneProfiles()
        {
            TunnelLookMaterials.RequireEditMode();
            int profiles = 0, missing = 0;
            string[] names = { "HDRP_DefaultVolumeProfile", "QualityHigh_HDRP_VolumeProfile", "QualityMidium_HDRP_VolumeProfile",
                "QualityLow_HDRP_VolumeProfile", "DynamicOverrideProfile", "QualityHigh_VolumeProfile", "QualityMidium_VolumeProfile", "QualityLow_VolumeProfile" };
            foreach (string name in names)
            {
                string path = "Assets/Shinzui/GraphicSettings/" + name + ".asset";
                var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
                if (profile == null)
                {
                    missing++;
                    Debug.LogWarning($"Tunnel look profile missing: {path}");
                    continue;
                }
                int tier = name.Contains("Low") ? 0 : name.Contains("Midium") ? 1 : 2;
                var exposure = Ensure<Exposure>(profile);
                exposure.mode.Override(ExposureMode.AutomaticHistogram);
                exposure.limitMin.Override(3f);
                exposure.limitMax.Override(4f);
                exposure.compensation.Override(0f);
                exposure.histogramPercentages.Override(new Vector2(40f, 85f));
                exposure.histogramUseCurveRemapping.Override(false);
                exposure.adaptationMode.Override(AdaptationMode.Progressive);
                exposure.adaptationSpeedDarkToLight.Override(3f);
                exposure.adaptationSpeedLightToDark.Override(1f);
                Ensure<Tonemapping>(profile).mode.Override(TonemappingMode.ACES);
                /* ポータル面には仮想空間の速度を持たせずカメラ回転による二重のぶれを避ける */
                Ensure<MotionBlur>(profile).cameraMotionBlur.Override(false);
                var balance = Ensure<WhiteBalance>(profile);
                balance.temperature.Override(-6f);
                balance.tint.Override(0f);
                var color = Ensure<ColorAdjustments>(profile);
                color.postExposure.Override(0f);
                color.colorFilter.Override(Color.white);
                color.hueShift.Override(0f);
                color.contrast.Override(8f);
                color.saturation.Override(-12f);
                /* 既存の色相演出を重ねずホワイトバランスを色温度の基準にする */
                var split = Ensure<SplitToning>(profile);
                split.shadows.Override(Color.gray);
                split.highlights.Override(Color.gray);
                split.balance.Override(0f);
                var lift = Ensure<LiftGammaGain>(profile);
                lift.lift.Override(new Vector4(1f, 1f, 1f, 0f));
                lift.gamma.Override(new Vector4(1f, 1f, 1f, 0f));
                lift.gain.Override(new Vector4(1f, 1f, 1f, 0f));
                var mixer = Ensure<ChannelMixer>(profile);
                mixer.redOutRedIn.Override(100f); mixer.redOutGreenIn.Override(0f); mixer.redOutBlueIn.Override(0f);
                mixer.greenOutRedIn.Override(0f); mixer.greenOutGreenIn.Override(100f); mixer.greenOutBlueIn.Override(0f);
                mixer.blueOutRedIn.Override(0f); mixer.blueOutGreenIn.Override(0f); mixer.blueOutBlueIn.Override(100f);
                if (profile.TryGet(out ColorCurves curves)) curves.active = false;
                var vignette = Ensure<Vignette>(profile);
                vignette.mode.Override(VignetteMode.Procedural);
                vignette.intensity.Override(0.18f);
                vignette.smoothness.Override(0.5f);
                vignette.color.Override(Color.black);
                var grain = Ensure<FilmGrain>(profile);
                grain.type.Override(FilmGrainLookup.Thin1);
                grain.intensity.Override(0.025f);
                grain.response.Override(0.8f);
                var bloom = Ensure<Bloom>(profile);
                bloom.intensity.Override(0.08f);
                bloom.threshold.Override(1.5f);
                var fog = Ensure<Fog>(profile);
                fog.enabled.Override(true);
                fog.enableVolumetricFog.Override(true);
                fog.colorMode.Override(FogColorMode.ConstantColor);
                fog.color.Override(Color.black);
                fog.albedo.Override(new Color(0.78f, 0.81f, 0.84f));
                fog.meanFreePath.Override(120f);
                fog.baseHeight.Override(-20f);
                fog.maximumHeight.Override(30f);
                fog.depthExtent.Override(48f);
                fog.anisotropy.Override(0.35f);
                fog.globalLightProbeDimmer.Override(0f);
                /* カメラの共通プロファイルでは品質別の有効状態を上書きしない */
                if (name != "DynamicOverrideProfile")
                {
                    fog.quality.Override(tier);
                    bloom.quality.Override(tier);
                    var gi = Ensure<GlobalIllumination>(profile);
                    gi.enable.Override(tier == 2);
                    gi.tracing.Override(RayCastingMode.RayMarching);
                    gi.quality.Override(tier);
                    var ssr = Ensure<ScreenSpaceReflection>(profile);
                    ssr.enabled.Override(tier > 0);
                    ssr.tracing.Override(RayCastingMode.RayMarching);
                    ssr.quality.Override(tier);
                    var ao = Ensure<ScreenSpaceAmbientOcclusion>(profile);
                    ao.intensity.Override(tier == 2 ? 1.2f : 0.7f);
                    ao.radius.Override(0.5f);
                    ao.directLightingStrength.Override(0.15f);
                    ao.quality.Override(tier);
                    var contact = Ensure<ContactShadows>(profile);
                    contact.enable.Override(tier > 0);
                    contact.length.Override(0.12f);
                    contact.maxDistance.Override(tier == 2 ? 25f : 15f);
                    contact.quality.Override(tier);
                }
                foreach (VolumeComponent component in profile.components) if (component != null) EditorUtility.SetDirty(component);
                EditorUtility.SetDirty(profile);
                profiles++;
                Debug.Log($"Tunnel look profile: {path}, EV=3..4, compensation=0, histogram=40..85, temperature=-6, contrast=8, saturation=-12, fog MFP=120m, tier={tier}");
            }
            /* 8%反射の路面が1.45luxを受ける基準をトーンマッピング前の値で報告する */
            float floorLuminance = 1.45f * 0.08f / Mathf.PI;
            float exposedFloor = floorLuminance * ColorUtils.ConvertEV100ToExposure(3f);
            Debug.Log($"Tunnel exposure reference: 1.45lux, 8% diffuse floor -> {floorLuminance:F3}cd/m2, at EV3 -> linear {exposedFloor:F3}, {Mathf.Log(exposedFloor / 0.18f, 2f):F2} stops relative to 18% grey before ACES; histogram/actual albedo change the result");
            ConfigurePipelines();
            AssetDatabase.SaveAssets();
            Debug.Log($"Tunnel look profile summary: updated={profiles}, missing={missing}; dynamic profile preserves quality feature overrides, ground moisture generated at puddles");
        }

        /// <summary>デカールと品質別の画面空間効果をパイプラインで有効にする</summary>
        private static void ConfigurePipelines()
        {
            int count = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:HDRenderPipelineAsset", new[] { "Assets/Shinzui/GraphicSettings" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<HDRenderPipelineAsset>(path);
                int tier = path.Contains("Low") ? 0 : path.Contains("Midium") ? 1 : 2;
                var settings = asset.currentPlatformRenderPipelineSettings;
                settings.supportDecals = true;
                settings.decalSettings.perChannelMask = true;
                settings.decalSettings.drawDistance = tier == 2 ? 40 : tier == 1 ? 25 : 18;
                settings.supportSSGI = tier == 2;
                settings.supportSSR = tier > 0;
                settings.supportSSAO = true;
                settings.supportVolumetrics = true;
                settings.lightSettings.useContactShadow = new BoolScalableSetting(new[] { false, true, true }, ScalableSettingSchemaId.With3Levels);
                settings.hdShadowInitParams.supportContactShadows = tier > 0;
                asset.currentPlatformRenderPipelineSettings = settings;
                EditorUtility.SetDirty(asset);
                count++;
                Debug.Log($"Tunnel pipeline: {path}, decals=on, SSGI={(tier == 2)}, SSR={(tier > 0)}, contact={(tier > 0)}");
            }
            SetDefaultFrameFeatures(Features);
            Debug.Log($"Tunnel rendering features: pipelines={count}, global frame settings=1");
        }

        /// <summary>露出や照明値を変更せずデカールの描画経路を有効にする</summary>
        internal static void EnsureDecalRendering()
        {
            int count = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:HDRenderPipelineAsset", new[] { "Assets/Shinzui/GraphicSettings" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<HDRenderPipelineAsset>(path);
                var settings = asset.currentPlatformRenderPipelineSettings;
                settings.supportDecals = true;
                settings.decalSettings.perChannelMask = true;
                asset.currentPlatformRenderPipelineSettings = settings;
                EditorUtility.SetDirty(asset);
                count++;
                Debug.Log($"Tunnel decal pipeline verified: {path}, support=on, per-channel mask=on, distance={settings.decalSettings.drawDistance}");
            }
            SetDefaultFrameFeatures(new[] { FrameSettingsField.Decals });
            Debug.Log($"Tunnel decal rendering summary: pipelines={count}, global camera decal frame flag=on; camera overrides verified by menu 1");
        }

        /// <summary>既定カメラのフレーム設定で指定機能を有効にする</summary>
        /// <param name="features">有効にする機能</param>
        private static void SetDefaultFrameFeatures(FrameSettingsField[] features)
        {
            var global = GraphicsSettings.GetSettingsForRenderPipeline<HDRenderPipeline>();
            if (global == null) throw new InvalidOperationException("HDRP Global Settings missing");
            var serialized = new SerializedObject(global);
            var iterator = serialized.GetIterator();
            while (iterator.Next(true))
            {
                if (iterator.propertyType != SerializedPropertyType.ManagedReference || iterator.managedReferenceValue?.GetType().Name != "RenderingPathFrameSettings") continue;
                var camera = iterator.FindPropertyRelative("m_Camera");
                var frame = (FrameSettings)camera.boxedValue;
                foreach (FrameSettingsField field in features) frame.SetEnabled(field, true);
                camera.boxedValue = frame;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(global);
                return;
            }
            throw new InvalidOperationException("HDRP RenderingPathFrameSettings container missing");
        }

        /// <summary>Volumeコンポーネントを必要に応じて永続化する</summary>
        /// <typeparam name="T">追加する型</typeparam>
        /// <param name="profile">対象プロファイル</param>
        /// <returns>既存または追加したコンポーネント</returns>
        private static T Ensure<T>(VolumeProfile profile) where T : VolumeComponent
        {
            if (!profile.TryGet(out T component))
            {
                component = profile.Add<T>();
                component.name = typeof(T).Name;
                AssetDatabase.AddObjectToAsset(component, profile);
            }
            component.active = true;
            return component;
        }

        /// <summary>変更を永続化しプレハブインスタンスの上書きを記録する</summary>
        /// <param name="target">変更対象</param>
        private static void Record(UnityEngine.Object target)
        {
            EditorUtility.SetDirty(target);
            if (PrefabUtility.IsPartOfPrefabInstance(target)) PrefabUtility.RecordPrefabInstancePropertyModifications(target);
        }
    }
}
