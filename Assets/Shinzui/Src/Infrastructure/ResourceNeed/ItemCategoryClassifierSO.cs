using System;
using System.Collections.Generic;
using Shinzui.Application.Interfaces.ResourceNeed;
using Shinzui.Domain.ValueObjects.ResourceNeed;
using Shinzui.Infrastructure.Repositories;
using UnityEngine;

namespace Shinzui.Infrastructure.ResourceNeed
{
    [Serializable]
    public class ItemCategoryMapping
    {
        [SerializeField] private string itemId;
        [SerializeField] private ResourceCategory category;
        /// <summary>
        /// 1個のアイテムが何単位分の資源量として換算されるかを表す係数（例: 上級回復薬1個で回復資源2個分、大型バッテリー1個で電池2個分等）
        /// </summary>
        [SerializeField, Min(1)] private int unitCount = 1;

        public string ItemId => itemId;
        public ResourceCategory Category => category;
        public int UnitCount => unitCount;

        public ItemCategoryMapping() { }

        public ItemCategoryMapping(string itemId, ResourceCategory category, int unitCount = 1)
        {
            this.itemId = itemId;
            this.category = category;
            this.unitCount = Math.Max(1, unitCount);
        }
    }

    /// <summary>
    /// アイテムIDごとのResourceCategory分類 ScriptableObject。
    /// Inspectorでの個別オーバーライド設定を保持し、未設定項目はCanonicalCatalogRegistry（AddressableItemCatalog互換）の正規既定値にフォールバックします。
    /// </summary>
    [CreateAssetMenu(fileName = "ItemCategoryClassifier", menuName = "Shinzui/ResourceNeed/ItemCategoryClassifier")]
    public class ItemCategoryClassifierSO : ScriptableObject, IItemCategoryClassifier
    {
        [SerializeField]
        private List<ItemCategoryMapping> mappings = new();

        private Dictionary<string, ItemCategoryMapping> _lookup;

        private void OnEnable()
        {
            BuildLookup();
        }

        private void OnValidate()
        {
            BuildLookup();
        }

        public void BuildLookup()
        {
            _lookup = new Dictionary<string, ItemCategoryMapping>(StringComparer.OrdinalIgnoreCase);
            if (mappings != null)
            {
                foreach (var mapping in mappings)
                {
                    if (mapping != null && !string.IsNullOrEmpty(mapping.ItemId))
                    {
                        _lookup[mapping.ItemId] = mapping;
                    }
                }
            }
        }

        /// <summary>
        /// マッピングリストを差し替え、即座にルックアップを再構築する
        /// </summary>
        public void ReplaceMappings(IEnumerable<ItemCategoryMapping> newMappings)
        {
            mappings = newMappings != null ? new List<ItemCategoryMapping>(newMappings) : new List<ItemCategoryMapping>();
            BuildLookup();
        }

        /// <summary>
        /// 指定したマッピングで初期化されたインスタンスを生成する
        /// </summary>
        public static ItemCategoryClassifierSO Create(IEnumerable<ItemCategoryMapping> initialMappings)
        {
            var so = CreateInstance<ItemCategoryClassifierSO>();
            so.ReplaceMappings(initialMappings);
            return so;
        }

        public ResourceCategory? ClassifyItem(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return null;
            if (_lookup == null) BuildLookup();

            // 1. Inspectorでの個別オーバーライドを優先
            if (_lookup.TryGetValue(itemId, out var mapping))
            {
                return mapping.Category;
            }

            // 2. カタログの正規既定分類にフォールバック
            if (CanonicalCatalogRegistry.TryGetDefaultCategory(itemId, out var defaultCategory))
            {
                return defaultCategory;
            }

            // 3. カタログに存在しない未知/没アイテムはnull（集計対象外）
            return null;
        }

        /// <summary>
        /// 1個あたりの換算単位数を取得する（未登録時はカタログ既定値または1単位）
        /// </summary>
        public int GetItemUnitCount(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return 1;
            if (_lookup == null) BuildLookup();

            if (_lookup.TryGetValue(itemId, out var mapping))
            {
                return Math.Max(1, mapping.UnitCount);
            }

            if (CanonicalCatalogRegistry.TryGetDefaultUnitCount(itemId, out var defaultUnitCount))
            {
                return Math.Max(1, defaultUnitCount);
            }

            return 1;
        }

        public static ItemCategoryClassifierSO CreateDefault()
        {
            // mappingsが空の場合でもCanonicalCatalogRegistryの既定分類が適用される
            return Create(Array.Empty<ItemCategoryMapping>());
        }
    }
}
