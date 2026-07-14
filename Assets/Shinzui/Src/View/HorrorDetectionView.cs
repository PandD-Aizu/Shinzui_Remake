using UnityEngine;

namespace Shinzui.View
{
    /// <summary>
    /// 敵に発見された時の画面ノイズ・歪みエフェクトをシェーダー変数経由で制御するView
    /// </summary>
    public class HorrorDetectionView : MonoBehaviour
    {
        [Header("Shader Properties")]
        [SerializeField, Range(0f, 0.1f)] private float maxDistortionIntensity = 0.03f;
        [SerializeField, Range(0f, 0.05f)] private float maxChromaticAberrationIntensity = 0.015f;
        [SerializeField, Range(0f, 1f)] private float maxScanlineIntensity = 0.15f;

        /// <summary>
        /// 現在のノイズ強度を元に、シェーダーのグローバルパラメータを更新する
        /// </summary>
        /// <param name="intensity">ノイズの現在の強度 (0.0 ~ 1.0)</param>
        public void UpdateEffect(float intensity)
        {
            // shader側の _HorrorNoiseIntensity を変更する。
            // これにより、shader内部で歪み量や色収差、走査線もこの強度に応じて乗算される。
            Shader.SetGlobalFloat("_HorrorNoiseIntensity", intensity);
            Shader.SetGlobalFloat("_HorrorDistortionIntensity", maxDistortionIntensity);
            Shader.SetGlobalFloat("_HorrorChromaticAberrationIntensity", maxChromaticAberrationIntensity);
            Shader.SetGlobalFloat("_HorrorScanlineIntensity", maxScanlineIntensity);
        }

        private void OnDestroy()
        {
            // ゲーム終了時やオブジェクト破棄時にグローバル変数をリセットし、エフェクトを無効化する
            Shader.SetGlobalFloat("_HorrorNoiseIntensity", 0f);
        }
    }
}
