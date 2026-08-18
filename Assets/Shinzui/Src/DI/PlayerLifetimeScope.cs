using Shinzui.Application.Interfaces;
using Shinzui.Application.Interfaces.Inventory;
using Shinzui.Application.UseCases;
using Shinzui.Application.UseCases.Enemy;
using Shinzui.Application.UseCases.Inventory;
using Shinzui.Application.UseCases.Flashlight; // 【追加】
using Shinzui.Domain.Entities;
using Shinzui.Domain.Entities.Inventory;
using Shinzui.Domain.Entities.Flashlight; // 【追加】
using Shinzui.Domain.ValueObjects.Player;
using Shinzui.Infrastructure.Repositories;
using Shinzui.Infrastructure.Services;
using Shinzui.Presentation;
using Shinzui.Presentation.Inventory;
using Shinzui.Presentation.Flashlight;
using Shinzui.View;
using Shinzui.View.Inventory;
using Shinzui.View.Flashlight;
using Shinzui.Application.UseCases.Interaction;
using Shinzui.Presentation.Interaction;
using Shinzui.View.Interaction;
using Shinzui.Application.Interfaces.ResourceNeed;
using Shinzui.Application.UseCases.ResourceNeed;
using Shinzui.Domain.DomainServices.ResourceNeed;
using Shinzui.Domain.ValueObjects.ResourceNeed;
using Shinzui.Infrastructure.ResourceNeed;
using UnityEngine;
using UnityEngine.UI;
using VContainer;
using VContainer.Unity;

namespace Shinzui.DI
{
    public class PlayerLifetimeScope : LifetimeScope
    {
        [Header("Views")]
        [SerializeField] private PlayerView playerView;
        [SerializeField] private InventoryView inventoryView;
        [SerializeField] private FlashlightView flashlightView;
        [SerializeField] private StrobeGaugeView strobeGaugeView;
        [SerializeField] private PlayerInteractionView interactionView;
        [SerializeField] private InteractionMessageView interactionMessageView;
        [SerializeField] private SpecialItemHudView specialItemHudView;
        [SerializeField] private PlayerQuickItemSlotView playerQuickItemSlotView;
        [SerializeField] private PlayerDeathView playerDeathView;
        [SerializeField] private EnemyView[] enemyViews;
        [SerializeField] private HorrorDetectionView horrorDetectionView;

        [Header("Resource Need")]
        [SerializeField] private PlayerNeedWeightSettingsSO needWeightSettings;
        [SerializeField] private ItemCategoryClassifierSO itemCategoryClassifier;

        protected override void Configure(IContainerBuilder builder)
        {
            var player = CreatePlayer();

            // シングルトンインスタンスとして登録
            builder.RegisterInstance(player);

            // InputServiceの登録
            builder.Register<UnityInputService>(Lifetime.Singleton).As<IInputService>();

            // UseCaseの登録
            builder.Register<PlayerMoveUseCase>(Lifetime.Scoped);
            builder.Register<PlayerThrowUseCase>(Lifetime.Scoped);
            builder.Register<EnemyDirectorUseCase>(Lifetime.Scoped);
            builder.Register<EnemyMoveUseCase>(Lifetime.Transient);
            builder.RegisterFactory<EnemyMoveUseCase>(resolver => () => resolver.Resolve<EnemyMoveUseCase>(), Lifetime.Scoped);

            // Viewの登録
            var viewInstance = playerView;
            if (viewInstance == null)
            {
                viewInstance = FindFirstObjectByType<PlayerView>();
            }

            if (viewInstance != null)
            {
                builder.RegisterComponent(viewInstance);

                // PlayerThrowViewの動的アタッチとDI登録
                var throwViewInstance = viewInstance.GetComponent<PlayerThrowView>();
                if (throwViewInstance == null)
                {
                    throwViewInstance = viewInstance.gameObject.AddComponent<PlayerThrowView>();
                }
                builder.RegisterComponent(throwViewInstance);
            }
            else
            {
                Debug.LogWarning("PlayerView instance was not assigned and not found in the scene hierarchy.");
            }

            // PresenterをVContainerのEntryPointとして登録
            builder.RegisterEntryPoint<PlayerMovePresenter>().AsSelf().As<IPlayerTracker>().As<ITickable>().As<IInitializable>();
            builder.RegisterEntryPoint<PlayerThrowPresenter>();
            builder.RegisterEntryPoint<TunnelLoopPresenter>();

            // ==========================================
            // インベントリシステムのDI登録
            // ==========================================
            
            // Domain & UseCase
            builder.Register<InventoryEntity>(Lifetime.Singleton).WithParameter(10); // 初期スロット数: 10
            builder.Register<InventoryUseCase>(Lifetime.Singleton);
            builder.Register<SpecialItemEntity>(Lifetime.Singleton);
            builder.Register<SpecialItemUseCase>(Lifetime.Singleton);
            builder.Register<PlayerDeathUseCase>(Lifetime.Singleton);

            // Infrastructure (Catalog & Repository & Sound)
            builder.Register<IItemCatalog, AddressableItemCatalog>(Lifetime.Singleton);
            builder.Register<IInventoryRepository, PlayerPrefsInventoryRepository>(Lifetime.Singleton);
            builder.Register<ISpecialItemRepository, PlayerPrefsInventoryRepository>(Lifetime.Singleton);
            builder.Register<IFMODSEService, FMODSEService>(Lifetime.Singleton);

            // View (シーン上のUIコンポーネント)
            var invViewInstance = inventoryView;
            if (invViewInstance == null)
            {
                invViewInstance = FindFirstObjectByType<InventoryView>();
            }

            if (invViewInstance != null)
            {
                builder.RegisterComponent(invViewInstance);
            }
            else
            {
                Debug.LogWarning("InventoryView instance was not assigned and not found in the scene hierarchy.");
            }

            // Presentation
            builder.RegisterEntryPoint<InventoryPresenter>();
            RegisterSpecialItemHud(builder);
            RegisterPlayerQuickItemSlot(builder);
            RegisterPlayerDeath(builder);
            RegisterEnemies(builder);
            
            // Domain & UseCase
            builder.Register<FlashlightEntity>(Lifetime.Singleton).WithParameter(false); // 初期状態: OFF
            builder.Register<FlashlightUseCase>(Lifetime.Singleton);

            // View (シーン上のLightオブジェクト)
            var lightViewInstance = flashlightView;
            if (lightViewInstance == null)
            {
                lightViewInstance = FindFirstObjectByType<FlashlightView>();
            }

            if (lightViewInstance != null)
            {
                builder.RegisterComponent(lightViewInstance);
            }
            else
            {
                Debug.LogWarning("FlashlightView instance was not assigned and not found in the scene hierarchy.");
            }

            // View (ストロボゲージUI)
            var gaugeViewInstance = strobeGaugeView;
            if (gaugeViewInstance == null)
            {
                gaugeViewInstance = FindFirstObjectByType<StrobeGaugeView>();
            }

            if (gaugeViewInstance != null)
            {
                builder.RegisterComponent(gaugeViewInstance);
            }
            else
            {
                Debug.LogWarning("StrobeGaugeView instance was not assigned and not found in the scene hierarchy.");
            }

            // Presentation (EntryPoint)
            builder.RegisterEntryPoint<FlashlightPresenter>();

            // ==========================================
            // インタラクトシステムのDI登録
            // ==========================================
            builder.Register<InteractionUseCase>(Lifetime.Singleton);

            var interactViewInstance = interactionView;
            if (interactViewInstance == null)
            {
                interactViewInstance = FindFirstObjectByType<PlayerInteractionView>();
            }
            if (interactViewInstance != null)
            {
                builder.RegisterComponent(interactViewInstance);
            }
            else
            {
                Debug.LogWarning("PlayerInteractionView instance was not assigned and not found in the scene hierarchy.");
            }

            var messageViewInstance = interactionMessageView;
            if (messageViewInstance == null)
            {
                messageViewInstance = FindFirstObjectByType<InteractionMessageView>();
            }
            if (messageViewInstance != null)
            {
                builder.RegisterComponent(messageViewInstance);
            }
            else
            {
                Debug.LogWarning("InteractionMessageView instance was not assigned and not found in the scene hierarchy.");
            }

            builder.RegisterEntryPoint<InteractionPresenter>();

            // ==========================================
            // ホラー演出（敵発見時の画面ノイズ）のDI登録
            // ==========================================
            var horrorViewInstance = horrorDetectionView;
            if (horrorViewInstance == null)
            {
                horrorViewInstance = FindFirstObjectByType<HorrorDetectionView>();
            }

            if (horrorViewInstance == null)
            {
                var horrorViewObject = new GameObject("HorrorDetectionView");
                horrorViewInstance = horrorViewObject.AddComponent<HorrorDetectionView>();
            }

            builder.RegisterComponent(horrorViewInstance);
            builder.RegisterEntryPoint<HorrorDetectionPresenter>();

            // ==========================================
            // プレイヤー資源不足度（Need Weight）評価システムのDI登録
            // ==========================================
            var classifierInstance = itemCategoryClassifier != null
                ? itemCategoryClassifier
                : ItemCategoryClassifierSO.CreateDefault();
            builder.RegisterInstance(classifierInstance).As<IItemCategoryClassifier>();

            var needSettingsInstance = needWeightSettings != null
                ? needWeightSettings
                : PlayerNeedWeightSettingsSO.CreateDefault();
            builder.RegisterInstance(needSettingsInstance);
            builder.RegisterInstance(needSettingsInstance.ToDomainConfig());

            builder.Register<PlayerResourceNeedEvaluator>(Lifetime.Singleton);
            builder.Register<PlayerResourceSnapshotProvider>(Lifetime.Singleton).As<IPlayerResourceSnapshotProvider>();
            builder.Register<PlayerResourceNeedUseCase>(Lifetime.Singleton).As<IPlayerResourceNeedUseCase>().AsSelf();
        }

        private void RegisterSpecialItemHud(IContainerBuilder builder)
        {
            var specialItemHudViewInstance = specialItemHudView;
            if (specialItemHudViewInstance == null)
            {
                specialItemHudViewInstance = FindFirstObjectByType<SpecialItemHudView>();
            }

            if (specialItemHudViewInstance == null)
            {
                specialItemHudViewInstance = CreateSpecialItemHudViewFromScenePath();
            }

            if (specialItemHudViewInstance != null)
            {
                builder.RegisterComponent(specialItemHudViewInstance);
                builder.RegisterEntryPoint<SpecialItemHudPresenter>();
            }
            else
            {
                Debug.LogWarning("SpecialItemHudView was not assigned and HUDCanvas/SpecialItem/ItemSprite was not found.");
            }
        }

        private static SpecialItemHudView CreateSpecialItemHudViewFromScenePath()
        {
            var hudCanvas = GameObject.Find("HUDCanvas");
            var specialItemRoot = hudCanvas != null ? hudCanvas.transform.Find("SpecialItem") : null;
            var itemSprite = specialItemRoot != null ? specialItemRoot.Find("ItemSprite") : null;
            var itemImage = itemSprite != null ? itemSprite.GetComponent<Image>() : null;

            if (specialItemRoot == null || itemImage == null)
            {
                return null;
            }

            var view = specialItemRoot.GetComponent<SpecialItemHudView>();
            if (view == null)
            {
                view = specialItemRoot.gameObject.AddComponent<SpecialItemHudView>();
            }

            view.Configure(itemImage);
            return view;
        }

        private void RegisterPlayerQuickItemSlot(IContainerBuilder builder)
        {
            var quickItemSlotViewInstance = playerQuickItemSlotView;
            if (quickItemSlotViewInstance == null)
            {
                quickItemSlotViewInstance = FindFirstObjectByType<PlayerQuickItemSlotView>();
            }

            if (quickItemSlotViewInstance == null)
            {
                quickItemSlotViewInstance = CreatePlayerQuickItemSlotViewFromScenePath();
            }

            if (quickItemSlotViewInstance != null)
            {
                builder.RegisterComponent(quickItemSlotViewInstance);
                builder.RegisterEntryPoint<PlayerQuickItemSlotPresenter>();
            }
            else
            {
                Debug.LogWarning("PlayerQuickItemSlotView was not assigned and HUDCanvas could not be found or created.");
            }
        }

        private static PlayerQuickItemSlotView CreatePlayerQuickItemSlotViewFromScenePath()
        {
            var hudCanvas = GameObject.Find("HUDCanvas");
            if (hudCanvas == null)
            {
                return null;
            }

            var quickItemRoot = hudCanvas.transform.Find("QuickItemSlot") ?? hudCanvas.transform.Find("PlayerQuickItemSlot");
            if (quickItemRoot == null)
            {
                var quickItemObject = new GameObject("QuickItemSlot", typeof(RectTransform));
                quickItemObject.transform.SetParent(hudCanvas.transform, false);
                quickItemRoot = quickItemObject.transform;
            }

            var rootRect = quickItemRoot.GetComponent<RectTransform>();
            if (rootRect == null)
            {
                rootRect = quickItemRoot.gameObject.AddComponent<RectTransform>();
            }
            rootRect.anchorMin = new Vector2(1f, 0f);
            rootRect.anchorMax = new Vector2(1f, 0f);
            rootRect.pivot = new Vector2(1f, 0f);
            rootRect.anchoredPosition = new Vector2(-32f, 32f);
            rootRect.sizeDelta = new Vector2(160f, 78f);

            var view = quickItemRoot.GetComponent<PlayerQuickItemSlotView>();
            if (view == null)
            {
                view = quickItemRoot.gameObject.AddComponent<PlayerQuickItemSlotView>();
            }

            Image normalSlot = EnsureSlot(quickItemRoot, "NormalItemSlot", new Vector2(-50f, 0f), new Vector2(72f, 72f), new Color(0.08f, 0.08f, 0.08f, 0.72f));
            Image normalIcon = EnsureIcon(normalSlot.transform, "ItemIcon", new Vector2(52f, 52f));
            Image specialSlot = EnsureSlot(quickItemRoot, "SpecialItemSlot", new Vector2(-126f, 6f), new Vector2(48f, 48f), new Color(0.12f, 0.09f, 0.04f, 0.72f));
            Image specialIcon = EnsureIcon(specialSlot.transform, "ItemIcon", new Vector2(34f, 34f));

            view.Configure(normalSlot, normalIcon, specialSlot, specialIcon);
            return view;
        }

        private static Image EnsureSlot(Transform parent, string name, Vector2 anchoredPosition, Vector2 sizeDelta, Color color)
        {
            var child = parent.Find(name);
            if (child == null)
            {
                var childObject = new GameObject(name, typeof(RectTransform), typeof(Image));
                childObject.transform.SetParent(parent, false);
                child = childObject.transform;
            }

            var rect = child.GetComponent<RectTransform>();
            if (rect == null)
            {
                rect = child.gameObject.AddComponent<RectTransform>();
            }
            rect.anchorMin = new Vector2(1f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = sizeDelta;

            var image = child.GetComponent<Image>();
            if (image == null)
            {
                image = child.gameObject.AddComponent<Image>();
            }
            image.color = color;
            return image;
        }

        private static Image EnsureIcon(Transform parent, string name, Vector2 sizeDelta)
        {
            var child = parent.Find(name);
            if (child == null)
            {
                var childObject = new GameObject(name, typeof(RectTransform), typeof(Image));
                childObject.transform.SetParent(parent, false);
                child = childObject.transform;
            }

            var rect = child.GetComponent<RectTransform>();
            if (rect == null)
            {
                rect = child.gameObject.AddComponent<RectTransform>();
            }
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = sizeDelta;

            var image = child.GetComponent<Image>();
            if (image == null)
            {
                image = child.gameObject.AddComponent<Image>();
            }
            image.preserveAspect = true;
            image.raycastTarget = false;
            return image;
        }

        private void RegisterEnemies(IContainerBuilder builder)
        {
            EnemyView[] enemyViewInstances = enemyViews;
            if (enemyViewInstances == null || enemyViewInstances.Length == 0)
            {
                enemyViewInstances = FindObjectsByType<EnemyView>(FindObjectsSortMode.None);
            }

            if (enemyViewInstances == null || enemyViewInstances.Length == 0)
            {
                Debug.LogWarning("EnemyView instances were not assigned and not found in the scene hierarchy.");
                enemyViewInstances = new EnemyView[0];
            }

            builder.RegisterInstance(enemyViewInstances);
            builder.RegisterEntryPoint<EnemyPresenter>().AsSelf();
        }

        private void RegisterPlayerDeath(IContainerBuilder builder)
        {
            var deathViewInstance = playerDeathView;
            if (deathViewInstance == null)
            {
                deathViewInstance = FindFirstObjectByType<PlayerDeathView>();
            }

            if (deathViewInstance == null)
            {
                var deathViewObject = new GameObject("PlayerDeathView");
                deathViewInstance = deathViewObject.AddComponent<PlayerDeathView>();
            }

            builder.RegisterComponent(deathViewInstance);
            builder.RegisterEntryPoint<PlayerDeathPresenter>();
        }

        private static PlayerEntity CreatePlayer()
        {
            return new PlayerEntity(
                new PlayerSpeedStatus(),
                new PlayerCrouchStatus(),
                new PlayerStamina());
        }
    }
}
