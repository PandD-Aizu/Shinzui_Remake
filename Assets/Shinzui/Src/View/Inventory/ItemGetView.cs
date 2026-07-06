using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

namespace Shinzui.View.Inventory
{
    public class ItemGetView : MonoBehaviour
    {
        [SerializeField, Tooltip("表示上限")] private int maxItem = 5;
        [SerializeField, Tooltip("UI１つの表示時間")] private float displayTime = 3.0f;
        [SerializeField, Tooltip("表示するUIの親")] private Transform parentTransform;
        [SerializeField, Tooltip("UIのプレハブ")] private ItemGetUnitView prefab;

        private ObjectPool<ItemGetUnitView> _uiPool;
        private readonly List<ItemGetUnitView> _activeUIList = new();

        private void Start()
        {
            if (prefab == null)
            {
                Debug.LogError($"[{nameof(ItemGetView)}] UI Prefab is not assigned in the inspector! ItemGetUI display will be disabled.", this);
                return;
            }

            if (parentTransform == null)
            {
                parentTransform = transform;
            }

            _uiPool = new ObjectPool<ItemGetUnitView>(
                createFunc: OnCreateUI,
                actionOnGet: OnGetUI,
                actionOnRelease: OnReleaseUI,
                actionOnDestroy: OnDestroyUI,
                collectionCheck: true,
                defaultCapacity: maxItem / 2,
                maxSize: maxItem
            );
        }

        public void Show(string itemName, string iconAddress)
        {
            if (prefab == null || _uiPool == null)
            {
                Debug.LogWarning($"[{nameof(ItemGetView)}] Cannot show item get UI because Prefab is null or Pool initialization failed.");
                return;
            }

            // 表示上限に達した場合は、最も古いUIをプールへ強制返却
            if (_activeUIList.Count >= maxItem)
            {
                ItemGetUnitView oldestUI = _activeUIList[0];
                _uiPool.Release(oldestUI);
            }

            // プールからUIを取得
            ItemGetUnitView uiInstance = _uiPool.Get();
            if (uiInstance == null) return;

            // 最下部に配置
            uiInstance.transform.SetAsLastSibling();

            // 初期化
            uiInstance.Initialize(itemName, iconAddress, displayTime, OnUnitAnimationComplete);
        }

        private void OnUnitAnimationComplete(ItemGetUnitView unit)
        {
            // アニメーション完了時に安全にプールへ返却
            if (unit != null && unit.gameObject.activeSelf && _activeUIList.Contains(unit))
            {
                _uiPool.Release(unit);
            }
        }

        #region プールのコールバック

        private ItemGetUnitView OnCreateUI()
        {
            return Instantiate(prefab, parentTransform);
        }

        private void OnGetUI(ItemGetUnitView ui)
        {
            ui.gameObject.SetActive(true);
            _activeUIList.Add(ui);
        }

        private void OnReleaseUI(ItemGetUnitView ui)
        {
            ui.gameObject.SetActive(false);
            _activeUIList.Remove(ui);
        }

        private void OnDestroyUI(ItemGetUnitView ui)
        {
            if (ui != null)
            {
                Destroy(ui.gameObject);
            }
        }

        #endregion
    }
}
