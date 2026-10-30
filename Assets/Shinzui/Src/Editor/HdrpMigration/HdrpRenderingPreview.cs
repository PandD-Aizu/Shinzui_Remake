using System;
using System.Linq;
using Shinzui.Infrastructure.Rendering;
using Shinzui.Infrastructure.Rendering.Scanline;
using Shinzui.Infrastructure.Rendering.InverseScanLine;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using Object = UnityEngine.Object;

namespace Shinzui.Editor.HdrpMigration
{
    /// <summary>保存アセットを変更せず実際のカメラで演出を検証する</summary>
    [InitializeOnLoad]
    public static class HdrpRenderingPreview
    {
        private static GameObject previewObject;
        private static VolumeProfile previewProfile;
        private static bool reportPending;
        private static int startFrame;

        /// <summary>再コンパイルと再生終了時に一時演出を解除する</summary>
        static HdrpRenderingPreview()
        {
            AssemblyReloadEvents.beforeAssemblyReload += Stop;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            RenderPipelineManager.endCameraRendering += ReportCamera;
        }

        /// <summary>Presenterに上書きされない恐怖ノイズのプレビューを切り替える</summary>
        [MenuItem("Shinzui/HDRP Migration/9 Toggle Horror Detection Preview")]
        public static void ToggleHorror()
        {
            RequirePlayMode();
            bool enable = HdrpHorrorDetectionPass.PreviewIntensity <= 0;
            Stop();
            HdrpHorrorDetectionPass.PreviewIntensity = enable ? 0.8f : 0f;
            startFrame = Time.frameCount;
            reportPending = true;
            Debug.Log($"HDRP HorrorDetection preview: active={(enable ? 1 : 0)}, intensity={HdrpHorrorDetectionPass.PreviewIntensity}, saved assets=0; select this menu again or exit Play Mode to stop");
            if (!enable) return;
            var volumes = Object.FindObjectsByType<CustomPassVolume>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Where(v => v.customPasses.Any(p => p is HdrpHorrorDetectionPass)).ToArray();
            foreach (CustomPassVolume volume in volumes)
            {
                foreach (HdrpHorrorDetectionPass pass in volume.customPasses.OfType<HdrpHorrorDetectionPass>())
                    Debug.Log($"HDRP HorrorDetection runtime: {HdrpVerificationAssets.Hierarchy(volume.transform)}, active={volume.isActiveAndEnabled}, global={volume.isGlobal}, layer={volume.gameObject.layer}, injection={volume.injectionPoint}, pass enabled={pass.enabled}, material={AssetDatabase.GetAssetPath(pass.material)}, shader={pass.material?.shader?.name}, supported={pass.material?.shader?.isSupported}");
            }
            if (volumes.Length != 1) Debug.LogWarning($"HDRP HorrorDetection expected one runtime volume, found={volumes.Length}; source=Assets/Shinzui/Resources/HdrpHorrorDetection.prefab");
        }

        /// <summary>全パラメータを上書きした走査線の既定演出を表示する</summary>
        [MenuItem("Shinzui/HDRP Migration/10 Preview Post Effects/Scanline Defaults")]
        public static void PreviewScanline() => Preview<ScanlineVolume>();

        /// <summary>逆走査線の既定演出を表示する</summary>
        [MenuItem("Shinzui/HDRP Migration/10 Preview Post Effects/Inverse Scanline Defaults")]
        public static void PreviewInverse() => Preview<InverseScanlineVolume>();

        /// <summary>既定値ゼロの代わりに識別できる強度でピクセル化を表示する</summary>
        [MenuItem("Shinzui/HDRP Migration/10 Preview Post Effects/Pixelation 0.25")]
        public static void PreviewPixelation()
        {
            Preview<PixelationVolume>();
            previewProfile.TryGet(out PixelationVolume pixel);
            pixel.intensity.Override(0.25f);
        }

        /// <summary>一時Volumeで演出のパラメータを他のプロファイルから分離する</summary>
        /// <typeparam name="T">検証する演出</typeparam>
        private static void Preview<T>() where T : VolumeComponent
        {
            RequirePlayMode();
            Stop();
            Camera camera = Camera.main;
            if (camera == null || !camera.TryGetComponent(out HDAdditionalCameraData data))
                throw new InvalidOperationException("HDRP MainCamera missing");
            int layer = Enumerable.Range(0, 32).FirstOrDefault(i => (data.volumeLayerMask.value & (1 << i)) != 0);
            if (data.volumeLayerMask.value == 0) throw new InvalidOperationException("MainCamera volume layer mask is empty");
            previewObject = new GameObject("HDRP Post Effect Preview") { hideFlags = HideFlags.DontSave, layer = layer };
            Object.DontDestroyOnLoad(previewObject);
            previewProfile = ScriptableObject.CreateInstance<VolumeProfile>();
            previewProfile.hideFlags = HideFlags.DontSave;
            /* 全項目を上書きし通常プロファイルのゼロ値を継承しない */
            var component = previewProfile.Add<T>(true);
            component.hideFlags = HideFlags.DontSave;
            Volume volume = previewObject.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 10000;
            volume.sharedProfile = previewProfile;
            startFrame = Time.frameCount;
            reportPending = true;
            Debug.Log($"HDRP post effect preview: {typeof(T).Name}, volumes=1, saved assets=0; use Stop Previews or exit Play Mode to stop");
        }

        /// <summary>カメラの合成後パラメータと実行済みパスを一回報告する</summary>
        /// <param name="context">描画コンテキスト</param>
        /// <param name="camera">描画済みカメラ</param>
        private static void ReportCamera(ScriptableRenderContext context, Camera camera)
        {
            if (!reportPending || !EditorApplication.isPlaying || camera != Camera.main || Time.frameCount < startFrame + 3) return;
            reportPending = false;
            HDCamera hd = HDCamera.GetOrCreate(camera);
            Exposure exposure = hd.volumeStack.GetComponent<Exposure>();
            MotionBlur blur = hd.volumeStack.GetComponent<MotionBlur>();
            ScanlineVolume scanline = hd.volumeStack.GetComponent<ScanlineVolume>();
            Debug.Log($"HDRP camera audit: {camera.name}, CustomPass={hd.frameSettings.IsEnabled(FrameSettingsField.CustomPass)}, CustomPostProcess={hd.frameSettings.IsEnabled(FrameSettingsField.CustomPostProcess)}, Postprocess={hd.frameSettings.IsEnabled(FrameSettingsField.Postprocess)}, exposure={exposure.mode.value} EV={exposure.limitMin.value}..{exposure.limitMax.value} compensation={exposure.compensation.value}, blur={blur.intensity.value} cameraBlur={blur.cameraMotionBlur.value}; Scanline strength={scanline.strength.value}, fineLines={scanline.fineLines.value}, curvature={scanline.curvature.value}, vignette={scanline.vignette.value}");
            if (HdrpHorrorDetectionPass.PreviewIntensity <= 0) return;
            bool executed = HdrpHorrorDetectionPass.LastExecutionFrame >= startFrame;
            bool drawn = HdrpHorrorDetectionPass.LastDrawFrame >= startFrame;
            string status = $"HDRP HorrorDetection draw audit: executed={(executed ? 1 : 0)}, drawn={(drawn ? 1 : 0)}, camera={HdrpHorrorDetectionPass.LastCameraName}, intensity={HdrpHorrorDetectionPass.LastIntensity}, global gameplay intensity={Shader.GetGlobalFloat("_HorrorNoiseIntensity")}";
            if (drawn) Debug.Log(status);
            else Debug.LogWarning(status + "; inspect runtime volume, material, camera volume mask and frame settings; do not infer success from preview activation alone");
        }

        /// <summary>プレビュー終了時に一時オブジェクトとサブアセットを破棄する</summary>
        [MenuItem("Shinzui/HDRP Migration/10 Preview Post Effects/Stop Previews")]
        public static void Stop()
        {
            bool active = previewObject != null || HdrpHorrorDetectionPass.PreviewIntensity > 0;
            HdrpHorrorDetectionPass.PreviewIntensity = 0;
            reportPending = false;
            if (previewObject != null) Object.DestroyImmediate(previewObject);
            if (previewProfile != null)
            {
                foreach (VolumeComponent component in previewProfile.components) Object.DestroyImmediate(component);
                Object.DestroyImmediate(previewProfile);
            }
            previewObject = null;
            previewProfile = null;
            Debug.Log($"HDRP previews stopped: previously active={(active ? 1 : 0)}, active=0, saved assets=0");
        }

        /// <summary>再生状態の変化でプレビューを解除する</summary>
        /// <param name="state">再生状態</param>
        private static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredPlayMode) Stop();
        }

        /// <summary>実描画を検証できる再生状態を要求する</summary>
        private static void RequirePlayMode()
        {
            if (!EditorApplication.isPlaying) throw new InvalidOperationException("Enter Play Mode before rendering preview");
        }
    }
}
