using System;
using System.Collections.Generic;
using Shinzui.Application.Interfaces.ResourceNeed;
using Shinzui.Domain.Entities.Flashlight;
using Shinzui.Domain.Entities.Inventory;
using Shinzui.Domain.ValueObjects.Inventory;
using Shinzui.Domain.ValueObjects.ResourceNeed;

namespace Shinzui.Infrastructure.ResourceNeed
{
    /// <summary>
    /// 実在するインベントリおよび懐中電灯エンティティからリソースを集計し、イミュータブルなスナップショットを生成するプロバイダー
    /// </summary>
    public class PlayerResourceSnapshotProvider : IPlayerResourceSnapshotProvider
    {
        private readonly InventoryEntity _inventory;
        private readonly FlashlightEntity _flashlight;
        private readonly IItemCategoryClassifier _classifier;

        public PlayerResourceSnapshotProvider(
            InventoryEntity inventory,
            FlashlightEntity flashlight = null,
            IItemCategoryClassifier classifier = null)
        {
            _inventory = inventory;
            _flashlight = flashlight;
            _classifier = classifier;
        }

        public PlayerResourceSnapshot CaptureSnapshot()
        {
            int healCount = 0;
            int batteryCount = 0;
            int weaponCount = 0;
            int utilityCount = 0;
            int rareCount = 0;
            int ammoCount = 0;

            if (_inventory != null)
            {
                for (int i = 0; i < _inventory.Capacity; i++)
                {
                    var stack = _inventory.GetSlot(i).CurrentValue;
                    if (stack != null && stack.Item != null && stack.Quantity > 0)
                    {
                        string id = stack.Item.Id;
                        int rawQty = stack.Quantity;
                        
                        // 分類器による判定（カタログ未登録の未知・没アイテムはnullとなり集計除外）
                        ResourceCategory? category = _classifier?.ClassifyItem(id);

                        if (category.HasValue)
                        {
                            int unitCount = _classifier != null ? _classifier.GetItemUnitCount(id) : 1;
                            int effectiveQty = rawQty * Math.Max(1, unitCount);

                            switch (category.Value)
                            {
                                case ResourceCategory.Healing:
                                    healCount += effectiveQty;
                                    break;
                                case ResourceCategory.LightResource:
                                    batteryCount += effectiveQty;
                                    break;
                                case ResourceCategory.Ammo:
                                    ammoCount += effectiveQty;
                                    break;
                                case ResourceCategory.Weapon:
                                    weaponCount += effectiveQty;
                                    break;
                                case ResourceCategory.Utility:
                                    utilityCount += effectiveQty;
                                    break;
                                case ResourceCategory.Rare:
                                    rareCount += effectiveQty;
                                    break;
                            }
                        }
                    }
                }
            }

            // 懐中電灯のストロボチャージ状況
            float strobeCharge = 0f;
            bool hasFlashlight = _flashlight != null;
            if (_flashlight != null)
            {
                strobeCharge = _flashlight.StrobeCharge.CurrentValue;
            }

            // 各サブステートの構築（HP・銃器等は現状未実装のため安全な未定義/デフォルト値）
            var hpState = PlayerHpState.Unavailable; // 将来HP実装時に拡張可能
            var healState = new HealingResourceState(healCount);
            var lightState = new LightResourceState(hasFlashlight, strobeCharge, batteryCount);
            var ammoState = new AmmoResourceState(Array.Empty<WeaponAmmoInfo>(), ammoCount);
            var weaponState = new WeaponResourceState(weaponCount);
            var utilityState = new UtilityResourceState(utilityCount);
            var rareState = new RareResourceState(rareCount);

            return new PlayerResourceSnapshot(
                hpState,
                healState,
                lightState,
                ammoState,
                weaponState,
                utilityState,
                rareState,
                DateTime.UtcNow
            );
        }
    }
}
