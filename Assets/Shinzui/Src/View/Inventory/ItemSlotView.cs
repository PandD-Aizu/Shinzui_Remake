using System;
using R3;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace Shinzui.View.Inventory
{
    public class ItemSlotView : MonoBehaviour, IPointerClickHandler, IDragHandler, IBeginDragHandler, IEndDragHandler
    {
        [Header("Components")]
        [SerializeField] private Image frameImage;     // 通常時のフレーム（外枠）Image
        [SerializeField] private Image iconImage;      // アイコン画像Image
        [SerializeField] private TextMeshProUGUI quantityText;

        public int SlotIndex { get; private set; }

        // ユーザー入力を Presenter 向けに Observable として公開
        private readonly Subject<int> _onClick = new();
        private readonly Subject<(int fromIndex, int toIndex)> _onDragDrop = new();

        public Observable<int> OnClick => _onClick;
        public Observable<(int fromIndex, int toIndex)> OnDragDrop => _onDragDrop;

        public void Setup(int index)
        {
            SlotIndex = index;
            
            // frameImageが未設定の場合は、自信のImageコンポーネントの取得を試みる
            if (frameImage == null)
            {
                frameImage = GetComponent<Image>();
            }

            ClearSlot();
        }

        public void SetItem(string iconAddress, int quantity)
        {
            if (iconImage != null)
            {
                iconImage.gameObject.SetActive(true);
                // 実際の実装ではここで Addressables を利用して画像を非同期でロードします。
            }
            
            if (quantityText != null)
            {
                quantityText.gameObject.SetActive(quantity > 1);
                quantityText.text = quantity.ToString();
            }
        }

        public void ClearSlot()
        {
            if (iconImage != null)
            {
                iconImage.gameObject.SetActive(false);
            }
            if (quantityText != null)
            {
                quantityText.gameObject.SetActive(false);
            }
            
            // 選択状態も解除（白に戻す）
            SetSelection(false);
        }

        /// <summary>
        /// 選択状態に応じてフレームの色を切り替える（選択時：赤、通常時：白）
        /// </summary>
        public void SetSelection(bool isSelected)
        {
            if (frameImage != null)
            {
                frameImage.color = isSelected ? Color.red : Color.white;
            }
        }

        // PointerClickイベント
        public void OnPointerClick(PointerEventData eventData)
        {
            _onClick.OnNext(SlotIndex);
        }

        public void OnBeginDrag(PointerEventData eventData) { }
        public void OnDrag(PointerEventData eventData) { }
        
        public void OnEndDrag(PointerEventData eventData)
        {
            var raycastResults = new System.Collections.Generic.List<RaycastResult>();
            EventSystem.current.RaycastAll(eventData, raycastResults);
            
            foreach (var result in raycastResults)
            {
                var targetSlot = result.gameObject.GetComponent<ItemSlotView>();
                if (targetSlot != null && targetSlot.SlotIndex != this.SlotIndex)
                {
                    _onDragDrop.OnNext((this.SlotIndex, targetSlot.SlotIndex));
                    break;
                }
            }
        }

        private void OnDestroy()
        {
            _onClick.OnCompleted();
            _onDragDrop.OnCompleted();
        }
    }
}
