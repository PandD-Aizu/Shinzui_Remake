using UnityEngine;
using UnityEngine.UI;

namespace Shinzui.View.Flashlight
{
    /// <summary>
    /// ストロボチャージ用の円形ゲージUI制御クラス
    /// </summary>
    public class StrobeGaugeView : MonoBehaviour
    {
        [Header("UI Components")]
        [Tooltip("Fill Method が Radial 360, Origin が Bottom(下), Clockwise が True に設定された Image")]
        [SerializeField] private Image gaugeImage;
        
        [Tooltip("ゲージ全体を表示・非表示にするための親オブジェクト")]
        [SerializeField] private GameObject rootObject;

        private void Start()
        {
            if (rootObject == null)
            {
                rootObject = gameObject;
            }
            
            // 初期状態は非表示
            SetGaugeActive(false);
        }

        /// <summary>
        /// ゲージの表示状態を切り替える
        /// </summary>
        public void SetGaugeActive(bool active)
        {
            if (rootObject != null && rootObject.activeSelf != active)
            {
                rootObject.SetActive(active);
            }
        }

        /// <summary>
        /// ゲージの進捗度（0.0 〜 1.0）を設定する
        /// </summary>
        public void SetProgress(float progress)
        {
            if (gaugeImage != null)
            {
                gaugeImage.fillAmount = progress;
            }
        }
    }
}
