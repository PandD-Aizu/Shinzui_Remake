using UnityEngine;

namespace Shinzui.View.Inventory
{
    public class InventoryView : MonoBehaviour
    {
        [SerializeField] private GameObject inventoryPanel;
        [SerializeField] private ItemSlotView slotPrefab;    // スロットのプレハブ
        [SerializeField] private Transform slotParent;        // スロットを配置する親オブジェクト（Gridなど）

        private ItemSlotView[] _slotViews;

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
            // すでに生成されている場合は一旦破棄する（再初期化対応）
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

        public void Show()
        {
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
        }
    }
}
