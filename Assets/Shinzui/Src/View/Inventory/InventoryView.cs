using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Shinzui.View.Inventory
{
    public class InventoryView : MonoBehaviour
    {
        [Header("Required Components")]
        [SerializeField] private GameObject inventoryPanel;
        [SerializeField] private ItemSlotView slotPrefab;    // スロットのプレハブ
        [SerializeField] private Transform slotParent;       // スロットを配置する親オブジェクト

        [Header("Detailed Item Info Components")]
        [SerializeField] private Image largeIconImage;                   // 左上：大アイコン画像
        [SerializeField] private TMP_Text selectedItemNameText;          // 下部：アイテム名テキスト
        [SerializeField] private TMP_Text selectedItemDescriptionText;   // 下部：詳細説明テキスト

        [Header("Use Context Menu Components")]
        [SerializeField] private GameObject useContextMenu;             // コンテキストメニュー全体の親（非表示トグル用）
        [SerializeField] private RectTransform contextMenuPanel;        // 実際に表示されるメニューパネル（位置合わせ用）
        [SerializeField] private Button useButton;                      // 「使用する」ボタン
        [SerializeField] private Button contextMenuBackgroundButton;    // 背景の閉じるボタン

        private ItemSlotView[] _slotViews;
        private UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationHandle<Sprite> _largeIconLoadHandle;

        private Button _equipButton;
        public Button EquipButton
        {
            get
            {
                if (_equipButton == null && useButton != null)
                {
                    CreateEquipButton();
                }
                return _equipButton;
            }
        }

        private void CreateEquipButton()
        {
            _equipButton = Instantiate(useButton, useButton.transform.parent);
            _equipButton.name = "EquipButton";
            
            var tmpText = _equipButton.GetComponentInChildren<TMPro.TMP_Text>();
            if (tmpText != null)
            {
                tmpText.text = "Equip";
            }
            else
            {
                var normalText = _equipButton.GetComponentInChildren<Text>();
                if (normalText != null)
                {
                    normalText.text = "Equip";
                }
            }
        }

        public void SetContextMenuButtons(bool showUse, bool showEquip)
        {
            if (useButton != null) useButton.gameObject.SetActive(showUse);
            if (_equipButton != null) _equipButton.gameObject.SetActive(showEquip);
        }

        public int SlotCount => _slotViews?.Length ?? 0;

        public ItemSlotView GetSlotView(int index)
        {
            if (_slotViews == null || index < 0 || index >= _slotViews.Length)
            {
                return null;
            }
            return _slotViews[index];
        }

        /// <summary>
        /// インベントリ容量に合わせてスロットオブジェクトを動的生成する
        /// </summary>
        public void InitializeSlots(int capacity)
        {
            // すでに生成されている場合は一旦破棄する
            if (_slotViews != null)
            {
                foreach (var slot in _slotViews)
                {
                    if (slot != null)
                    {
                        Destroy(slot.gameObject);
                    }
                }
            }

            _slotViews = new ItemSlotView[capacity];

            if (slotPrefab == null || slotParent == null)
            {
                Debug.LogError("[InventoryView] slotPrefab または slotParent がアタッチされていません。");
                return;
            }

            for (int i = 0; i < capacity; i++)
            {
                var slotInstance = Instantiate(slotPrefab, slotParent, false);
                slotInstance.Setup(i);
                _slotViews[i] = slotInstance;
            }
        }

        /// <summary>
        /// 選択されたアイテムの詳細情報を更新
        /// </summary>
        public void UpdateDetailedInfo(string itemName, string description, string iconAddress)
        {
            if (selectedItemNameText != null)
            {
                selectedItemNameText.text = itemName;
            }

            if (selectedItemDescriptionText != null)
            {
                selectedItemDescriptionText.text = description;
            }

            if (largeIconImage != null)
            {
                if (_largeIconLoadHandle.IsValid())
                {
                    UnityEngine.AddressableAssets.Addressables.Release(_largeIconLoadHandle);
                }

                if (!string.IsNullOrEmpty(iconAddress))
                {
                    _largeIconLoadHandle = UnityEngine.AddressableAssets.Addressables.LoadAssetAsync<Sprite>(iconAddress);
                    _largeIconLoadHandle.Completed += handle =>
                    {
                        if (handle.Status == UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationStatus.Succeeded)
                        {
                            largeIconImage.sprite = handle.Result;
                            largeIconImage.gameObject.SetActive(true);
                        }
                        else
                        {
                            largeIconImage.gameObject.SetActive(false);
                        }
                    };
                }
                else
                {
                    largeIconImage.gameObject.SetActive(false);
                    largeIconImage.sprite = null;
                }
            }
        }

        /// <summary>
        /// 詳細情報表示エリアをクリア
        /// </summary>
        public void ClearDetailedInfo()
        {
            if (_largeIconLoadHandle.IsValid())
            {
                UnityEngine.AddressableAssets.Addressables.Release(_largeIconLoadHandle);
            }

            if (selectedItemNameText != null)
            {
                selectedItemNameText.text = string.Empty;
            }

            if (selectedItemDescriptionText != null)
            {
                selectedItemDescriptionText.text = string.Empty;
            }

            if (largeIconImage != null)
            {
                largeIconImage.gameObject.SetActive(false);
                largeIconImage.sprite = null;
            }
        }

        public Button UseButton => useButton;
        public Button ContextMenuBackgroundButton => contextMenuBackgroundButton;

        /// <summary>
        /// 指定されたスロットの位置を基準に、右下にコンテキストメニューを表示
        /// </summary>
        public void ShowContextMenu(Vector3 slotPosition)
        {
            if (useContextMenu == null || contextMenuPanel == null) return;

            useContextMenu.SetActive(true);

            // スロットの位置から少し右下にオフセット（Xに+30, Yに-60）を加える
            contextMenuPanel.position = slotPosition + new Vector3(30f, -60f, 0f);
        }

        /// <summary>
        /// コンテキストメニューを非表示に
        /// </summary>
        public void HideContextMenu()
        {
            if (useContextMenu != null)
            {
                useContextMenu.SetActive(false);
            }
        }

        public void Show()
        {
            gameObject.SetActive(true);
            if (inventoryPanel != null)
            {
                inventoryPanel.SetActive(true);
            }
        }

        public void Hide()
        {
            if (inventoryPanel != null)
            {
                inventoryPanel.SetActive(false);
            }
            gameObject.SetActive(false);
            ClearDetailedInfo(); // 非表示時に詳細情報もクリア
            HideContextMenu();   // 非表示時にコンテキストメニューも非表示
        }

        private void OnDestroy()
        {
            if (_largeIconLoadHandle.IsValid())
            {
                UnityEngine.AddressableAssets.Addressables.Release(_largeIconLoadHandle);
            }
        }
    }
}
