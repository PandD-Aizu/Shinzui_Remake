using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace Shinzui.Infrastructure.Rendering
{
    /// <summary>
    /// HDRPカメラの追加設定とフレーム設定を管理する
    /// </summary>
    public static class HdrpCameraUtility
    {
        /// <summary>
        /// カメラのHDRP設定を取得し未設定の場合は基本設定を引き継いで追加する
        /// </summary>
        /// <param name="camera">対象カメラ</param>
        /// <returns>HDRPカメラ設定</returns>
        public static HDAdditionalCameraData GetOrAdd(Camera camera)
        {
            if (camera.TryGetComponent(out HDAdditionalCameraData data)) return data;

            // 追加時にHDRP独自の背景設定へ転記する
            data = camera.gameObject.AddComponent<HDAdditionalCameraData>();
            data.clearColorMode = camera.clearFlags == CameraClearFlags.Skybox
                ? HDAdditionalCameraData.ClearColorMode.Sky
                : camera.clearFlags == CameraClearFlags.SolidColor
                    ? HDAdditionalCameraData.ClearColorMode.Color
                    : HDAdditionalCameraData.ClearColorMode.None;
            data.backgroundColorHDR = camera.backgroundColor;
            data.clearDepth = camera.clearFlags != CameraClearFlags.Nothing;
            return data;
        }

        /// <summary>
        /// 指定したフレーム設定だけを上書きする
        /// </summary>
        /// <param name="data">対象カメラ設定</param>
        /// <param name="field">上書き項目</param>
        /// <param name="enabled">有効状態</param>
        public static void SetFrameSetting(HDAdditionalCameraData data, FrameSettingsField field, bool enabled)
        {
            // 他の設定の既定値継承を維持する
            data.customRenderingSettings = true;
            data.renderingPathCustomFrameSettings.SetEnabled(field, enabled);
            data.renderingPathCustomFrameSettingsOverrideMask.mask[(uint)field] = true;
        }

        /// <summary>
        /// ポストプロセスの有効状態を設定する
        /// </summary>
        /// <param name="camera">対象カメラ</param>
        /// <param name="enabled">有効状態</param>
        public static void SetPostProcessing(Camera camera, bool enabled)
        {
            SetFrameSetting(GetOrAdd(camera), FrameSettingsField.Postprocess, enabled);
        }

        /// <summary>
        /// メインカメラの設定をポータルへ複製しシーンリニアで出力する
        /// </summary>
        /// <param name="source">複製元カメラ</param>
        /// <param name="target">ポータルカメラ</param>
        /// <param name="volumeAnchor">Volume判定位置</param>
        /// <returns>ポータルのHDRPカメラ設定</returns>
        public static HDAdditionalCameraData CopyForPortal(Camera source, Camera target, Transform volumeAnchor)
        {
            // 背景色、AA、Volumeマスク、独自フレーム設定をHDRPのAPIで複製する
            HDAdditionalCameraData data = GetOrAdd(target);
            GetOrAdd(source).CopyTo(data);
            data.volumeAnchorOverride = volumeAnchor;
            data.xrRendering = false;
            data.allowDynamicResolution = false;

            // 再帰合成中は露出と画面演出を重ねずメインカメラで一度だけ適用する
            SetFrameSetting(data, FrameSettingsField.Postprocess, false);
            SetFrameSetting(data, FrameSettingsField.CustomPostProcess, false);
            SetFrameSetting(data, FrameSettingsField.ExposureControl, false);
            // 同じカメラを複数の再帰段で使うため霧の履歴を混ぜない
            SetFrameSetting(data, FrameSettingsField.ReprojectionForVolumetrics, false);
            return data;
        }
    }
}
