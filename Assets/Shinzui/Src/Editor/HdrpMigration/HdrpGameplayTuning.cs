using System;
using System.Linq;
using Shinzui.View;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace Shinzui.Editor.HdrpMigration
{
    /// <summary>暗所用の露出と物体ブラーを既存アセットへ設定する</summary>
    public static class HdrpGameplayTuning
    {
        private const string Folder = "Assets/Shinzui/GraphicSettings/";
        private static int dynamicViews, cameras;

        /// <summary>ゲームプレイの露出を統一し恐怖演出の描画経路を再設定する</summary>
        [MenuItem("Shinzui/HDRP Migration/8 Tune Gameplay Rendering")]
        public static void Tune()
        {
            HdrpVerificationAssets.RequireSavedScenes();
            int profiles = 0, skipped = 0;
            dynamicViews = cameras = 0;
            string[] names = { "HDRP_DefaultVolumeProfile", "QualityHigh_HDRP_VolumeProfile",
                "QualityMidium_HDRP_VolumeProfile", "QualityLow_HDRP_VolumeProfile",
                "QualityHigh_VolumeProfile", "QualityMidium_VolumeProfile", "QualityLow_VolumeProfile", "DynamicOverrideProfile" };
            foreach (string name in names)
            {
                string path = Folder + name + ".asset";
                var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
                if (profile == null || !AssetDatabase.IsOpenForEdit(path))
                {
                    Debug.LogWarning($"HDRP gameplay profile skipped (missing/read-only): {path}");
                    skipped++;
                    continue;
                }
                Exposure exposure = Ensure<Exposure>(profile);
                string before = $"mode={exposure.mode.value}, EV={exposure.limitMin.value}..{exposure.limitMax.value}, compensation={exposure.compensation.value}";
                ConfigureExposure(exposure);
                MotionBlur blur = Ensure<MotionBlur>(profile);
                blur.intensity.Override(Mathf.Min(blur.intensity.value, 0.1f));
                blur.cameraMotionBlur.Override(false);
                blur.quality.levelAndOverride = (0, true);
                blur.quality.overrideState = true;
                blur.sampleCount = 8;
                /* sampleCountの公開setterは上書きフラグを変更しないため明示する */
                var serialized = new SerializedObject(blur);
                serialized.FindProperty("m_SampleCount.m_OverrideState").boolValue = true;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(exposure);
                EditorUtility.SetDirty(blur);
                EditorUtility.SetDirty(profile);
                profiles++;
                Debug.Log($"HDRP gameplay profile: {path}, before {before} -> histogram EV=0..6, compensation=-0.5, percentiles=50..95, adaptation bright=3/dark=0.5; blur={blur.intensity.value}, camera blur=off, samples=8");
                ReportGrade(profile, path);
            }

            /* 材質とResources参照を維持しCustomPassを一つにする */
            HdrpSceneMigration.SetupHorrorDetection();
            EnableCustomPass();
            HdrpVerificationAssets.VisitAssets(TuneRoot);
            AssetDatabase.SaveAssets();
            Debug.Log($"HDRP gameplay tuning: profiles={profiles}, dynamic views={dynamicViews}, cameras={cameras}, HorrorDetection prefabs=1, skipped={skipped}; grading preserved");
        }

        /// <summary>暗い画面を中間灰色まで持ち上げない露出範囲を設定する</summary>
        /// <param name="exposure">設定対象</param>
        internal static void ConfigureExposure(Exposure exposure)
        {
            exposure.active = true;
            exposure.mode.Override(ExposureMode.AutomaticHistogram);
            exposure.limitMin.Override(0f);
            exposure.limitMax.Override(6f);
            exposure.compensation.Override(-0.5f);
            exposure.histogramPercentages.Override(new Vector2(50f, 95f));
            exposure.histogramUseCurveRemapping.Override(false);
            exposure.adaptationMode.Override(AdaptationMode.Progressive);
            exposure.adaptationSpeedDarkToLight.Override(3f);
            exposure.adaptationSpeedLightToDark.Override(0.5f);
        }

        /// <summary>必要なVolumeコンポーネントをサブアセットとして用意する</summary>
        /// <typeparam name="T">コンポーネント型</typeparam>
        /// <param name="profile">保存先</param>
        /// <returns>既存または追加したコンポーネント</returns>
        private static T Ensure<T>(VolumeProfile profile) where T : VolumeComponent
        {
            if (profile.TryGet(out T component)) return component;
            component = profile.Add<T>();
            component.name = typeof(T).Name;
            AssetDatabase.AddObjectToAsset(component, profile);
            return component;
        }

        /// <summary>保存済みの動的ブラー上限とカメラの演出設定を調整する</summary>
        /// <param name="root">階層のルート</param>
        /// <param name="path">アセットパス</param>
        /// <param name="persistentRoot">永続プレハブのルート</param>
        /// <returns>変更した場合はtrue</returns>
        private static bool TuneRoot(GameObject root, string path, GameObject persistentRoot)
        {
            bool changed = false;
            foreach (DynamicPostProcessView view in root.GetComponentsInChildren<DynamicPostProcessView>(true))
            {
                var serialized = new SerializedObject(view);
                var minimum = serialized.FindProperty("minBlurIntensity");
                var maximum = serialized.FindProperty("maxBlurIntensity");
                maximum.floatValue = Mathf.Clamp(maximum.floatValue, 0f, 0.15f);
                minimum.floatValue = Mathf.Min(0.05f, maximum.floatValue);
                if (!serialized.ApplyModifiedPropertiesWithoutUndo()) continue;
                if (PrefabUtility.IsPartOfPrefabInstance(view)) PrefabUtility.RecordPrefabInstancePropertyModifications(view);
                dynamicViews++;
                changed = true;
                Debug.Log($"HDRP dynamic blur: {path} / {HdrpVerificationAssets.Hierarchy(view.transform)}, min={minimum.floatValue}, max={maximum.floatValue}");
            }
            foreach (Camera camera in root.GetComponentsInChildren<Camera>(true))
            {
                if (!camera.TryGetComponent(out HDAdditionalCameraData data) || camera.targetTexture != null) continue;
                bool dirty = false;
                if (camera.CompareTag("MainCamera") && (data.volumeLayerMask.value & 1) == 0)
                {
                    data.volumeLayerMask = data.volumeLayerMask.value | 1;
                    dirty = true;
                }
                if (data.customRenderingSettings)
                {
                    foreach (FrameSettingsField field in new[] { FrameSettingsField.CustomPass, FrameSettingsField.CustomPostProcess, FrameSettingsField.Postprocess })
                    {
                        if (data.renderingPathCustomFrameSettings.IsEnabled(field) && data.renderingPathCustomFrameSettingsOverrideMask.mask[(uint)field]) continue;
                        data.renderingPathCustomFrameSettings.SetEnabled(field, true);
                        data.renderingPathCustomFrameSettingsOverrideMask.mask[(uint)field] = true;
                        dirty = true;
                    }
                }
                if (!dirty) continue;
                EditorUtility.SetDirty(data);
                if (PrefabUtility.IsPartOfPrefabInstance(data)) PrefabUtility.RecordPrefabInstancePropertyModifications(data);
                changed = true;
                cameras++;
                Debug.Log($"HDRP camera effects enabled: {path} / {HdrpVerificationAssets.Hierarchy(camera.transform)}");
            }
            return changed;
        }

        /// <summary>グローバルフレーム設定と品質アセットでCustomPassを有効にする</summary>
        private static void EnableCustomPass()
        {
            int pipelines = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:HDRenderPipelineAsset", new[] { "Assets/Shinzui/GraphicSettings" }))
            {
                var asset = AssetDatabase.LoadAssetAtPath<HDRenderPipelineAsset>(AssetDatabase.GUIDToAssetPath(guid));
                var settings = asset.currentPlatformRenderPipelineSettings;
                settings.supportCustomPass = true;
                asset.currentPlatformRenderPipelineSettings = settings;
                EditorUtility.SetDirty(asset);
                pipelines++;
            }
            /* HDRP 17.5の内部コンテナは既存の移行処理と同じSerializedObject経由で設定する */
            var global = GraphicsSettings.GetSettingsForRenderPipeline<HDRenderPipeline>();
            if (global == null) throw new InvalidOperationException("HDRP Global Settings missing");
            var serialized = new SerializedObject(global);
            var iterator = serialized.GetIterator();
            while (iterator.Next(true))
            {
                if (iterator.propertyType != SerializedPropertyType.ManagedReference ||
                    iterator.managedReferenceValue?.GetType().Name != "RenderingPathFrameSettings") continue;
                var camera = iterator.FindPropertyRelative("m_Camera");
                var frame = (FrameSettings)camera.boxedValue;
                frame.SetEnabled(FrameSettingsField.CustomPass, true);
                frame.SetEnabled(FrameSettingsField.CustomPostProcess, true);
                frame.SetEnabled(FrameSettingsField.Postprocess, true);
                camera.boxedValue = frame;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(global);
                Debug.Log($"HDRP effect frame settings: pipelines={pipelines}, global camera settings=1");
                return;
            }
            throw new InvalidOperationException("HDRP RenderingPathFrameSettings.m_Camera missing");
        }

        /// <summary>色相を変更せず既存の色調補正をログへ記録する</summary>
        /// <param name="profile">確認対象</param>
        /// <param name="path">アセットパス</param>
        private static void ReportGrade(VolumeProfile profile, string path)
        {
            string grade = string.Join(", ", profile.components.Where(c => c != null && c.active &&
                c is WhiteBalance or SplitToning or ChannelMixer or LiftGammaGain or ColorCurves or ColorAdjustments or Tonemapping)
                .Select(c => c.GetType().Name + " overrides=" + c.parameters.Count(p => p.overrideState)));
            Debug.Log($"HDRP grading retained: {path}, {grade}; VolumeStack blends parameters once, profiles are not sequential grading passes");
        }
    }
}
