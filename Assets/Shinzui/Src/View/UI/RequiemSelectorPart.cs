using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Shinzui.View.UI
{
    /// <summary>
    /// 設定行の中の矢印や区切りへのクリックを行へ伝える
    /// </summary>
    public sealed class RequiemSelectorPart : MonoBehaviour, IPointerClickHandler
    {
        private RequiemOptionSelector _owner;
        private int _direction;
        private int _index;

        /// <summary>
        /// 押したときの動作を設定する
        /// </summary>
        /// <param name="graphic">押下を受け取る表示</param>
        /// <param name="owner">対象の設定行</param>
        /// <param name="direction">矢印の向き、区切りでは0</param>
        /// <param name="index">区切りの選択位置</param>
        public static void Attach(Graphic graphic, RequiemOptionSelector owner, int direction, int index = -1)
        {
            if (graphic == null) return;
            graphic.raycastTarget = true;
            var part = graphic.gameObject.AddComponent<RequiemSelectorPart>();
            part._owner = owner;
            part._direction = direction;
            part._index = index;
        }

        /// <summary>
        /// 矢印なら一つずらし、区切りならその選択肢を選ぶ
        /// </summary>
        /// <param name="eventData">ポインター情報</param>
        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;
            _owner.Select();
            if (_direction != 0) _owner.Step(_direction, false);
            else _owner.Choose(_index);
        }
    }
}
