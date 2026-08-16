using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;
using Shinzui.Application.DTOs.ResourceNeed;
using Shinzui.Application.Interfaces.ResourceNeed;
using Shinzui.Application.UseCases.ResourceNeed;
using Shinzui.Domain.DomainServices.ResourceNeed;
using Shinzui.Domain.Entities.Flashlight;
using Shinzui.Domain.Entities.Inventory;
using Shinzui.Domain.ValueObjects.Inventory;
using Shinzui.Domain.ValueObjects.ResourceNeed;
using Shinzui.Infrastructure.Repositories;
using Shinzui.Infrastructure.ResourceNeed;

namespace Shinzui.Tests.ResourceNeed
{
    [TestFixture]
    public class PlayerResourceNeedEvaluationTests
    {
        private PlayerResourceNeedEvaluator _evaluator;
        private PlayerNeedWeightEvaluationConfig _defaultConfig;

        [SetUp]
        public void SetUp()
        {
            _evaluator = new PlayerResourceNeedEvaluator();
            _defaultConfig = PlayerNeedWeightEvaluationConfig.CreateDefault();
        }

        #region Canonical Items & Classifier Tests

        [Test]
        public void Classifier_AllNineCanonicalItems_AreClassifiedToCorrectCategories()
        {
            var classifier = ItemCategoryClassifierSO.CreateDefault();

            // 1. 回復 (Healing)
            Assert.That(classifier.ClassifyItem("potion_red"), Is.EqualTo(ResourceCategory.Healing));

            // 2. 光源リソース (LightResource)
            Assert.That(classifier.ClassifyItem("FlashlightBattery"), Is.EqualTo(ResourceCategory.LightResource));

            // 3. ユーティリティ (Utility)
            Assert.That(classifier.ClassifyItem("stone"), Is.EqualTo(ResourceCategory.Utility));
            Assert.That(classifier.ClassifyItem("potion_stamina"), Is.EqualTo(ResourceCategory.Utility));
            Assert.That(classifier.ClassifyItem("potion_botamochi"), Is.EqualTo(ResourceCategory.Utility));

            // 4. 希少・特殊 (Rare)
            Assert.That(classifier.ClassifyItem("key_old"), Is.EqualTo(ResourceCategory.Rare));
            Assert.That(classifier.ClassifyItem("special_death_charm"), Is.EqualTo(ResourceCategory.Rare));
            Assert.That(classifier.ClassifyItem("special_speed_boots"), Is.EqualTo(ResourceCategory.Rare));
            Assert.That(classifier.ClassifyItem("special_stamina_core"), Is.EqualTo(ResourceCategory.Rare));
        }

        [Test]
        public void Classifier_UnmappedOrFictitiousItems_ReturnNull()
        {
            var classifier = ItemCategoryClassifierSO.CreateDefault();

            // 没アイテム・架空アイテム・未登録アイテムはnull（集計対象外）となること
            Assert.That(classifier.ClassifyItem("weapon_handgun"), Is.Null);
            Assert.That(classifier.ClassifyItem("ammo_9mm"), Is.Null);
            Assert.That(classifier.ClassifyItem("heavy_machine"), Is.Null);
            Assert.That(classifier.ClassifyItem("unknown_junk"), Is.Null);
        }

        [Test]
        public void Classifier_WhenEmptyAssetMappings_StillClassifiesAllCanonicalItems()
        {
            // ItemCategoryClassifier.asset (mappings: []) と同様に空リストで生成
            var classifier = ItemCategoryClassifierSO.Create(Array.Empty<ItemCategoryMapping>());

            foreach (var entry in CanonicalCatalogRegistry.Entries)
            {
                var category = classifier.ClassifyItem(entry.Definition.Id);
                Assert.That(category, Is.EqualTo(entry.DefaultCategory), $"Item '{entry.Definition.Id}' should resolve to default category '{entry.DefaultCategory}'");
            }
        }

        [Test]
        public void Classifier_InspectorMappingOverride_TakesPrecedenceOverCatalogDefault()
        {
            // stone の既定分類 Utility を Healing に上書きするマッピング
            var overrides = new[]
            {
                new ItemCategoryMapping("stone", ResourceCategory.Healing, 3)
            };

            var classifier = ItemCategoryClassifierSO.Create(overrides);

            // stone はオーバーライドされて Healing & unitCount 3
            Assert.That(classifier.ClassifyItem("stone"), Is.EqualTo(ResourceCategory.Healing));
            Assert.That(classifier.GetItemUnitCount("stone"), Is.EqualTo(3));

            // 他のアイテムはカタログ既定値が維持される
            Assert.That(classifier.ClassifyItem("potion_red"), Is.EqualTo(ResourceCategory.Healing));
            Assert.That(classifier.ClassifyItem("FlashlightBattery"), Is.EqualTo(ResourceCategory.LightResource));
        }

        [Test]
        public async Task AddressableItemCatalog_ContainsAllCanonicalEntries()
        {
            var catalog = new AddressableItemCatalog();

            foreach (var entry in CanonicalCatalogRegistry.Entries)
            {
                // IDによる解決
                var itemById = await catalog.GetItemAsync(entry.Definition.Id);
                Assert.That(itemById, Is.Not.Null, $"Item with Id '{entry.Definition.Id}' must exist in catalog");
                Assert.That(itemById.Id, Is.EqualTo(entry.Definition.Id));

                // 辞書キーによる解決
                if (!string.IsNullOrEmpty(entry.AlternateLookupKey))
                {
                    var itemByKey = await catalog.GetItemAsync(entry.AlternateLookupKey);
                    Assert.That(itemByKey, Is.Not.Null, $"Item with lookup key '{entry.AlternateLookupKey}' must exist in catalog");
                    Assert.That(itemByKey.Id, Is.EqualTo(entry.Definition.Id));
                }
            }
        }

        #endregion

        #region Healing Evaluation Tests

        [Test]
        public void Evaluate_Healing_LowHpWithoutHealItems_ProducesHighNeedWeight()
        {
            var snapshot = new PlayerResourceSnapshot(
                hp: new PlayerHpState(10f, 100f), // HP ratio = 0.1
                healing: new HealingResourceState(0) // 0 healing items
            );

            var result = _evaluator.Evaluate(snapshot, _defaultConfig);

            // HealingRatio = 0.6 * 0.1 + 0.4 * 0.0 = 0.06
            Assert.That(result.Ratios.HealingRatio, Is.EqualTo(0.06f).Within(0.001f));
            // 3点区分線形カーブ (0, 2.0)-(0.5, 1.0): 2.0 - (0.06 / 0.5) * 1.0 = 1.88
            Assert.That(result.GetNeedWeight(ResourceCategory.Healing), Is.GreaterThan(1.8f));
            Assert.That(result.GetNeedWeight(ResourceCategory.Healing), Is.LessThanOrEqualTo(2.0f));
        }

        [Test]
        public void Evaluate_Healing_LowHpWithAbundantHealItems_SuppressesNeedWeight()
        {
            var snapshotWithoutItems = new PlayerResourceSnapshot(
                hp: new PlayerHpState(10f, 100f),
                healing: new HealingResourceState(0)
            );

            var snapshotWithItems = new PlayerResourceSnapshot(
                hp: new PlayerHpState(10f, 100f),
                healing: new HealingResourceState(3) // target is 3 -> item ratio = 1.0
            );

            var resultWithout = _evaluator.Evaluate(snapshotWithoutItems, _defaultConfig);
            var resultWith = _evaluator.Evaluate(snapshotWithItems, _defaultConfig);

            // HealingRatio with items = 0.6 * 0.1 + 0.4 * 1.0 = 0.46
            Assert.That(resultWith.Ratios.HealingRatio, Is.EqualTo(0.46f).Within(0.001f));
            // 3点区分線形カーブ (0, 2.0)-(0.5, 1.0): 2.0 + (0.46 / 0.5) * (1.0 - 2.0) = 1.08
            Assert.That(resultWith.GetNeedWeight(ResourceCategory.Healing), Is.LessThan(resultWithout.GetNeedWeight(ResourceCategory.Healing)));
            Assert.That(resultWith.GetNeedWeight(ResourceCategory.Healing), Is.EqualTo(1.08f).Within(0.01f));
        }

        [Test]
        public void Evaluate_Healing_FullHpWithoutHealItems_HasLowerNeedThanLowHp()
        {
            var lowHpSnapshot = new PlayerResourceSnapshot(
                hp: new PlayerHpState(10f, 100f),
                healing: new HealingResourceState(0)
            );

            var fullHpSnapshot = new PlayerResourceSnapshot(
                hp: new PlayerHpState(100f, 100f),
                healing: new HealingResourceState(0)
            );

            var lowResult = _evaluator.Evaluate(lowHpSnapshot, _defaultConfig);
            var fullResult = _evaluator.Evaluate(fullHpSnapshot, _defaultConfig);

            // Full HP, 0 items -> ratio = 0.6 * 1.0 + 0.4 * 0.0 = 0.6
            Assert.That(fullResult.Ratios.HealingRatio, Is.EqualTo(0.6f).Within(0.001f));
            // 3点区分線形カーブ (0.5, 1.0)-(1.0, 0.5): 1.0 + ((0.6 - 0.5) / 0.5) * (0.5 - 1.0) = 0.90
            Assert.That(fullResult.GetNeedWeight(ResourceCategory.Healing), Is.LessThan(lowResult.GetNeedWeight(ResourceCategory.Healing)));
            Assert.That(fullResult.GetNeedWeight(ResourceCategory.Healing), Is.EqualTo(0.90f).Within(0.01f));
        }

        [Test]
        public void Evaluate_Healing_FullHpWithAbundantHealItems_ProducesLowestNeedWeight()
        {
            var snapshot = new PlayerResourceSnapshot(
                hp: new PlayerHpState(100f, 100f),
                healing: new HealingResourceState(3)
            );

            var result = _evaluator.Evaluate(snapshot, _defaultConfig);

            Assert.That(result.Ratios.HealingRatio, Is.EqualTo(1.0f).Within(0.001f));
            Assert.That(result.GetNeedWeight(ResourceCategory.Healing), Is.EqualTo(0.5f).Within(0.001f));
        }

        [Test]
        public void Evaluate_Healing_WithoutHpSystem_SafelyEvaluatesBasedOnItems()
        {
            var noHpNoItems = new PlayerResourceSnapshot(
                hp: PlayerHpState.Unavailable,
                healing: new HealingResourceState(0)
            );

            var noHpWithItems = new PlayerResourceSnapshot(
                hp: PlayerHpState.Unavailable,
                healing: new HealingResourceState(3)
            );

            var resultNoItems = _evaluator.Evaluate(noHpNoItems, _defaultConfig);
            var resultWithItems = _evaluator.Evaluate(noHpWithItems, _defaultConfig);

            Assert.That(resultNoItems.Ratios.HealingRatio, Is.EqualTo(0.0f).Within(0.001f));
            Assert.That(resultWithItems.Ratios.HealingRatio, Is.EqualTo(1.0f).Within(0.001f));
            Assert.That(resultNoItems.GetNeedWeight(ResourceCategory.Healing), Is.EqualTo(2.0f).Within(0.001f));
            Assert.That(resultWithItems.GetNeedWeight(ResourceCategory.Healing), Is.EqualTo(0.5f).Within(0.001f));
        }

        #endregion

        #region Ammo Evaluation Tests

        [Test]
        public void Evaluate_Ammo_WhenNoWeaponsOwned_DoesNotInflateAmmoNeedWeight()
        {
            var snapshot = new PlayerResourceSnapshot(
                ammo: new AmmoResourceState(Array.Empty<WeaponAmmoInfo>(), unassignedAmmoCount: 0)
            );

            var result = _evaluator.Evaluate(snapshot, _defaultConfig);

            // 武器を未所持の場合はデフォルト充足率 1.0 (不足なし -> 最低Need Weight 0.5)
            Assert.That(result.Ratios.AmmoRatio, Is.EqualTo(1.0f).Within(0.001f));
            Assert.That(result.GetNeedWeight(ResourceCategory.Ammo), Is.EqualTo(0.5f).Within(0.001f));
        }

        [Test]
        public void Evaluate_Ammo_WhenWeaponOwnedWithLowLoadedAndReserve_ProducesHighNeedWeight()
        {
            var weaponInfo = new WeaponAmmoInfo("handgun", loadedAmmo: 1, maxLoadedAmmo: 10, reserveAmmo: 0, targetReserveAmmo: 30);
            var snapshot = new PlayerResourceSnapshot(
                ammo: new AmmoResourceState(new[] { weaponInfo })
            );

            var result = _evaluator.Evaluate(snapshot, _defaultConfig);

            // LoadedRatio = 0.1, ReserveRatio = 0.0 -> AmmoRatio = 0.5 * 0.1 + 0.5 * 0.0 = 0.05
            Assert.That(result.Ratios.AmmoRatio, Is.EqualTo(0.05f).Within(0.001f));
            Assert.That(result.GetNeedWeight(ResourceCategory.Ammo), Is.GreaterThan(1.8f));
            Assert.That(snapshot.Ammo.TotalLoadedAmmo, Is.EqualTo(1));
            Assert.That(snapshot.Ammo.TotalReserveAmmo, Is.EqualTo(0));
        }

        [Test]
        public void Evaluate_Ammo_WhenWeaponOwnedWithFullAmmo_ProducesLowNeedWeight()
        {
            var weaponInfo = new WeaponAmmoInfo("handgun", loadedAmmo: 10, maxLoadedAmmo: 10, reserveAmmo: 30, targetReserveAmmo: 30);
            var snapshot = new PlayerResourceSnapshot(
                ammo: new AmmoResourceState(new[] { weaponInfo })
            );

            var result = _evaluator.Evaluate(snapshot, _defaultConfig);

            Assert.That(result.Ratios.AmmoRatio, Is.EqualTo(1.0f).Within(0.001f));
            Assert.That(result.GetNeedWeight(ResourceCategory.Ammo), Is.EqualTo(0.5f).Within(0.001f));
            Assert.That(snapshot.Ammo.TotalLoadedAmmo, Is.EqualTo(10));
            Assert.That(snapshot.Ammo.TotalReserveAmmo, Is.EqualTo(30));
        }

        #endregion

        #region Light Resource Evaluation Tests

        [Test]
        public void Evaluate_Light_WhenStrobeEmptyAndNoBatteries_ProducesHighNeedWeight()
        {
            var snapshot = new PlayerResourceSnapshot(
                light: new LightResourceState(hasFlashlight: true, strobeCharge: 0f, batteryCount: 0)
            );

            var result = _evaluator.Evaluate(snapshot, _defaultConfig);

            Assert.That(result.Ratios.LightRatio, Is.EqualTo(0.0f).Within(0.001f));
            Assert.That(result.GetNeedWeight(ResourceCategory.LightResource), Is.EqualTo(2.0f).Within(0.001f));
        }

        [Test]
        public void Evaluate_Light_WhenStrobeChargedAndAbundantBatteries_ProducesLowNeedWeight()
        {
            var snapshot = new PlayerResourceSnapshot(
                light: new LightResourceState(hasFlashlight: true, strobeCharge: 1.0f, batteryCount: 4) // target is 4
            );

            var result = _evaluator.Evaluate(snapshot, _defaultConfig);

            Assert.That(result.Ratios.LightRatio, Is.EqualTo(1.0f).Within(0.001f));
            Assert.That(result.GetNeedWeight(ResourceCategory.LightResource), Is.EqualTo(0.5f).Within(0.001f));
        }

        [Test]
        public void Evaluate_Light_WhenStrobeEmptyButHasBatteries_SuppressesNeed()
        {
            var emptyResult = _evaluator.Evaluate(new PlayerResourceSnapshot(
                light: new LightResourceState(hasFlashlight: true, strobeCharge: 0f, batteryCount: 0)
            ), _defaultConfig);

            var batteriesResult = _evaluator.Evaluate(new PlayerResourceSnapshot(
                light: new LightResourceState(hasFlashlight: true, strobeCharge: 0f, batteryCount: 4)
            ), _defaultConfig);

            // LightRatio with 4 batteries = 0.5 * 0.0 + 0.5 * 1.0 = 0.5
            Assert.That(batteriesResult.Ratios.LightRatio, Is.EqualTo(0.5f).Within(0.001f));
            Assert.That(batteriesResult.GetNeedWeight(ResourceCategory.LightResource), Is.EqualTo(1.0f).Within(0.001f));
            Assert.That(batteriesResult.GetNeedWeight(ResourceCategory.LightResource), Is.LessThan(emptyResult.GetNeedWeight(ResourceCategory.LightResource)));
        }

        #endregion

        #region Curve & Clamp Tests

        [Test]
        public void Evaluate_CurveAndClamp_RestrictsWeightWithinConfiguredMinMax()
        {
            var customCategoryConfigs = new Dictionary<ResourceCategory, NeedWeightCategoryConfig>
            {
                {
                    ResourceCategory.Healing,
                    new NeedWeightCategoryConfig(
                        ResourceCategory.Healing,
                        SampledPiecewiseCurveEvaluator.LinearInverse(10.0f, 0.0f),
                        minWeight: 0.8f,
                        maxWeight: 1.6f
                    )
                }
            };

            var customConfig = new PlayerNeedWeightEvaluationConfig(
                categoryConfigs: customCategoryConfigs
            );

            // Ratio 0 -> Curve gives 10.0f -> clamped to max 1.6f
            var result0 = _evaluator.Evaluate(new PlayerResourceSnapshot(
                hp: new PlayerHpState(0f, 100f),
                healing: new HealingResourceState(0)
            ), customConfig);

            Assert.That(result0.GetNeedWeight(ResourceCategory.Healing), Is.EqualTo(1.6f).Within(0.001f));

            // Ratio 1.0 -> Curve gives 0.0f -> clamped to min 0.8f
            var result1 = _evaluator.Evaluate(new PlayerResourceSnapshot(
                hp: new PlayerHpState(100f, 100f),
                healing: new HealingResourceState(3)
            ), customConfig);

            Assert.That(result1.GetNeedWeight(ResourceCategory.Healing), Is.EqualTo(0.8f).Within(0.001f));
        }

        #endregion

        #region Snapshot Immutability & Defensive Copying Tests

        [Test]
        public void Snapshot_DefensiveCopy_ExternalListMutationDoesNotAffectSnapshot()
        {
            var weaponList = new List<WeaponAmmoInfo>
            {
                new("handgun", 5, 10, 15, 30)
            };
            var weaponIds = new List<string> { "handgun" };

            var snapshot = new PlayerResourceSnapshot(
                ammo: new AmmoResourceState(weaponList, unassignedAmmoCount: 5),
                weapons: new WeaponResourceState(1, weaponIds)
            );

            // 外部リストを変更する
            weaponList.Clear();
            weaponList.Add(new WeaponAmmoInfo("rifle", 30, 30, 90, 90));
            weaponIds.Add("shotgun");

            // スナップショットが防御的コピーによって保護されていること
            Assert.That(snapshot.Ammo.WeaponAmmoList.Count, Is.EqualTo(1));
            Assert.That(snapshot.Ammo.WeaponAmmoList[0].WeaponId, Is.EqualTo("handgun"));
            Assert.That(snapshot.Weapons.OwnedWeaponIds.Count, Is.EqualTo(1));
            Assert.That(snapshot.Weapons.OwnedWeaponIds[0], Is.EqualTo("handgun"));
        }

        [Test]
        public void PlayerResourceSnapshot_IsImmutableAndConsistentAcrossEvaluations()
        {
            var snapshot = new PlayerResourceSnapshot(
                hp: new PlayerHpState(50f, 100f),
                healing: new HealingResourceState(2),
                light: new LightResourceState(true, 0.5f, 2)
            );

            var result1 = _evaluator.Evaluate(snapshot, _defaultConfig);
            var result2 = _evaluator.Evaluate(snapshot, _defaultConfig);

            Assert.That(result1.GetNeedWeight(ResourceCategory.Healing), Is.EqualTo(result2.GetNeedWeight(ResourceCategory.Healing)));
            Assert.That(result1.GetNeedWeight(ResourceCategory.LightResource), Is.EqualTo(result2.GetNeedWeight(ResourceCategory.LightResource)));
            Assert.That(result1.Ratios.HealingRatio, Is.EqualTo(result2.Ratios.HealingRatio));
            Assert.That(result1.GetDebugSummary(), Is.Not.Null.And.Not.Empty);
        }

        #endregion

        #region Provider & UseCase Integration Tests

        [Test]
        public void PlayerResourceSnapshotProvider_AggregatesFromInventoryAndFlashlightCorrectly()
        {
            var inventory = new InventoryEntity(5);
            var flashlight = new FlashlightEntity();
            var classifier = ItemCategoryClassifierSO.CreateDefault();

            // FlashlightBattery アイテムを追加
            var batteryDef = new ItemDefinition("FlashlightBattery", "乾電池", "", "", ItemType.Consumable, 99);
            inventory.TryAddItem(batteryDef, 3, out _);

            // 赤ポーションを追加
            var potionDef = new ItemDefinition("potion_red", "赤ポーション", "", "", ItemType.Consumable, 99);
            inventory.TryAddItem(potionDef, 2, out _);

            // ストロボチャージを設定
            flashlight.SetStrobeCharge(0.75f);

            var provider = new PlayerResourceSnapshotProvider(inventory, flashlight, classifier);
            var snapshot = provider.CaptureSnapshot();

            Assert.That(snapshot.Light.BatteryCount, Is.EqualTo(3));
            Assert.That(snapshot.Light.StrobeCharge, Is.EqualTo(0.75f).Within(0.001f));
            Assert.That(snapshot.Healing.HealingItemCount, Is.EqualTo(2));
        }

        [Test]
        public void PlayerResourceSnapshotProvider_MultipliesUnitCountWhenConfigured()
        {
            var inventory = new InventoryEntity(5);
            
            // unitCount = 2 のマッピングを持つテスト用分類器（公式Create APIを使用）
            var customClassifier = ItemCategoryClassifierSO.Create(new[]
            {
                new ItemCategoryMapping("potion_big_heal", ResourceCategory.Healing, unitCount: 2)
            });

            var itemDef = new ItemDefinition("potion_big_heal", "特大回復薬", "", "", ItemType.Consumable, 99);
            inventory.TryAddItem(itemDef, 3, out _);

            var provider = new PlayerResourceSnapshotProvider(inventory, null, customClassifier);
            var snapshot = provider.CaptureSnapshot();

            // 3個 * 2単位 = 6単位の回復資源として集計されること
            Assert.That(snapshot.Healing.HealingItemCount, Is.EqualTo(6));
        }

        [Test]
        public void PlayerResourceSnapshotProvider_IgnoresUnmappedUnknownItems()
        {
            var inventory = new InventoryEntity(5);
            var classifier = ItemCategoryClassifierSO.CreateDefault();

            // カタログにない未知・架空アイテム（例: 銃器、弾薬、重機等）
            var unknownDef = new ItemDefinition("heavy_machine", "重機", "", "", ItemType.Consumable, 99);
            inventory.TryAddItem(unknownDef, 5, out _);

            var provider = new PlayerResourceSnapshotProvider(inventory, null, classifier);
            var snapshot = provider.CaptureSnapshot();

            // 未知アイテムは集計対象外となり、各リソースカウントを歪めないこと
            Assert.That(snapshot.Utility.UtilityItemCount, Is.EqualTo(0));
            Assert.That(snapshot.Healing.HealingItemCount, Is.EqualTo(0));
            Assert.That(snapshot.Rare.RareItemCount, Is.EqualTo(0));
            Assert.That(snapshot.Weapons.OwnedWeaponCount, Is.EqualTo(0));
            Assert.That(snapshot.Ammo.UnassignedAmmoCount, Is.EqualTo(0));
        }

        [Test]
        public void PlayerResourceNeedUseCase_ProvidesCategoryWeightsAndDebugDto()
        {
            var inventory = new InventoryEntity(5);
            var flashlight = new FlashlightEntity();
            var classifier = ItemCategoryClassifierSO.CreateDefault();
            var provider = new PlayerResourceSnapshotProvider(inventory, flashlight, classifier);

            var useCase = new PlayerResourceNeedUseCase(provider, _evaluator, _defaultConfig);

            float healingWeight = useCase.GetNeedWeight(ResourceCategory.Healing);
            float lightWeight = useCase.GetNeedWeight(ResourceCategory.LightResource);
            var ratios = useCase.GetRatios();
            var dto = useCase.GetNeedDto();

            Assert.That(healingWeight, Is.GreaterThan(0f));
            Assert.That(lightWeight, Is.GreaterThan(0f));
            Assert.That(ratios.HealingRatio, Is.EqualTo(dto.HealingRatio));
            Assert.That(dto.LightNeedWeight, Is.EqualTo(lightWeight));
            Assert.That(dto.TotalLoadedAmmo, Is.EqualTo(0));
            Assert.That(dto.TotalReserveAmmo, Is.EqualTo(0));
        }

        #endregion
    }
}
