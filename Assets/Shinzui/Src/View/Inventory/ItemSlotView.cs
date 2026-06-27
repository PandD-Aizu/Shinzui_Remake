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

        private UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationHandle<Sprite> _iconLoadHandle;

        public void SetItem(string iconAddress, int quantity)
        {
            if (iconImage != null)
            {
                if (_iconLoadHandle.IsValid())
                {
                    UnityEngine.AddressableAssets.Addressables.Release(_iconLoadHandle);
                }

                if (!string.IsNullOrEmpty(iconAddress))
                {
                    _iconLoadHandle = UnityEngine.AddressableAssets.Addressables.LoadAssetAsync<Sprite>(iconAddress);
                    _iconLoadHandle.Completed += handle =>
                    {
                        if (handle.Status == UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationStatus.Succeeded)
                        {
                            iconImage.sprite = handle.Result;
                            iconImage.gameObject.SetActive(true);
                        }
                        else
                        {
                            iconImage.gameObject.SetActive(false);
                        }
                    };
                }
                else
                {
                    iconImage.gameObject.SetActive(false);
                    iconImage.sprite = null;
                }
            }
            
            if (quantityText != null)
            {
                quantityText.gameObject.SetActive(quantity > 1);
                quantityText.text = quantity.ToString();
            }
        }

        public void ClearSlot()
        {
            if (_iconLoadHandle.IsValid())
            {
                UnityEngine.AddressableAssets.Addressables.Release(_iconLoadHandle);
            }

            if (iconImage != null)
            {
                iconImage.gameObject.SetActive(false);
                iconImage.sprite = null;
            }
            if (quantityText != null)
            {
                quantityText.gameObject.SetActive(false);
            }
            
            _isSelected = false;
            _isEquipped = false;
            UpdateFrameColor();
        }

        private bool _isSelected;
        private bool _isEquipped;

        public void SetSelection(bool isSelected)
        {
            _isSelected = isSelected;
            UpdateFrameColor();
        }

        public void SetEquipped(bool isEquipped)
        {
            _isEquipped = isEquipped;
            UpdateFrameColor();
        }

        private void UpdateFrameColor()
        {
            if (frameImage != null)
            {
                if (_isSelected)
                {
                    frameImage.color = Color.red;
                }
                else if (_isEquipped)
                {
                    frameImage.color = Color.green; // 装備中は緑
                }
                else
                {
                    frameImage.color = Color.white;
                }
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
            if (_iconLoadHandle.IsValid())
            {
                UnityEngine.AddressableAssets.Addressables.Release(_iconLoadHandle);
            }
            _onClick.OnCompleted();
            _onDragDrop.OnCompleted();
        }
    }
}
