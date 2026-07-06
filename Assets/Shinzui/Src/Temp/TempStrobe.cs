using UnityEngine;
using UnityEngine.InputSystem;

namespace Shinzui.Temp
{
    /// <summary>
    /// 懐中電灯のストロボギミック実装
    /// 左クリック長押しで、ためて、離すと一気に光る
    /// 
    /// </summary>
    public class TempStrobe : MonoBehaviour
    {
        [SerializeField] private float maxStrobeIntensity;
        [SerializeField] private float minStrobeIntensity;
        [SerializeField] private float strobeInterval;
        [SerializeField] private float intensityChangeSpeed;

        private float currentIntensity = 0.0f;
        private float currentPushTime = 0.0f; // 左クリックを押し続けている時間

        public void Update()
        {
            if (Mouse.current.leftButton.isPressed)
            {
                currentPushTime += Time.deltaTime;
            }
            else if (currentPushTime > 0)
            {
                StartStrobe();
            }
        }

        private void StartStrobe()
        {
            // ここで一気に、懐中電灯の光を強くして、徐々に弱くするようにする
            // lerpは仮実装
            currentIntensity = Mathf.Lerp(minStrobeIntensity, maxStrobeIntensity, intensityChangeSpeed * Time.deltaTime);
        }
    }
}