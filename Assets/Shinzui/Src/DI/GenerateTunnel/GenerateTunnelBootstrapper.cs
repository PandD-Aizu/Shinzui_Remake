using System;
using System.Collections.Generic;
using Shinzui.Application.DTOs.Tunnel;
using Shinzui.Application.Interfaces.Tunnel;
using Shinzui.Application.UseCases.Tunnel;
using Shinzui.Domain.DomainServices.Tunnel;
using Shinzui.Presentation.GenerateTunnel;
using Shinzui.Presentation.ItemSpawn;
using Shinzui.View.GenerateTunnel;
using Shinzui.View.ItemSpawn;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Shinzui.DI.GenerateTunnel
{
    /// <summary>
    /// 対象シーン（GenerateTunnelTest / LatestStageGenerateTemp / StageTemp_DoorMoveCopy）開始時に
    /// Domain・Application・Presentation・View の全層を結線し、
    /// 厳密に1回だけトンネルマップ生成を実行するComposition Root / Scene Bootstrapper。
    /// マップ生成成功後、MapRoot配下のWeightedSpawnSurfaceを収集し、
    /// ItemSpawnContainerViewおよびItemSpawnManagerによるアイテムスポーンを厳密に1回起動する。
    /// 重複起動（RuntimeInitializeOnLoadMethod / sceneLoaded / Awake）を防止し、
    /// シーン再ロード時には新しい開始として再度1回だけ実行する。
    /// </summary>
    public static class GenerateTunnelBootstrapper
    {
        private static readonly HashSet<string> TargetSceneNames = new(StringComparer.OrdinalIgnoreCase)
        {
            "GenerateTunnelTest",
            "LatestStageGenerateTemp",
            "StageTemp_DoorMoveCopy"
        };

        private static readonly HashSet<ulong> HandledSceneHandles = new();
        private static readonly object ExecutionLock = new();
        private static bool _isExecuting;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            lock (ExecutionLock)
            {
                HandledSceneHandles.Clear();
                _isExecuting = false;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RegisterSceneCallbacks()
        {
            lock (ExecutionLock)
            {
                HandledSceneHandles.Clear();
                _isExecuting = false;
            }

            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            SceneManager.sceneUnloaded -= OnSceneUnloaded;
            SceneManager.sceneUnloaded += OnSceneUnloaded;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void BootstrapActiveScene()
        {
            TryBootstrapScene(SceneManager.GetActiveScene());
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            TryBootstrapScene(scene);
        }

        private static void OnSceneUnloaded(Scene scene)
        {
            lock (ExecutionLock)
            {
                HandledSceneHandles.Remove(scene.handle.GetRawData());
            }
        }

        /// <summary>
        /// 指定シーンに対してトンネル生成の起動を試みる。
        /// 対象シーンまたはTunnelGenerator/GenerateTunnelTestBootstrapが存在し、未実行の場合にのみ1回だけ実行する。
        /// </summary>
        private static bool TryBootstrapScene(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded)
            {
                return false;
            }

            bool isTargetScene = TargetSceneNames.Contains(scene.name);
            TunnelGenerator tunnelGenerator = FindTunnelGeneratorInScene(scene);
            GenerateTunnelTestBootstrap settingsView = FindBootstrapViewInScene(scene);

            if (!isTargetScene && tunnelGenerator == null && settingsView == null)
            {
                return false;
            }

            ulong rawHandle = scene.handle.GetRawData();

            lock (ExecutionLock)
            {
                if (_isExecuting || HandledSceneHandles.Contains(rawHandle))
                {
                    return false;
                }

                _isExecuting = true;
                HandledSceneHandles.Add(rawHandle);
            }

            try
            {
                ExecuteSceneGeneration(scene, tunnelGenerator, settingsView);
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[GenerateTunnelBootstrapper] Exception during tunnel map generation in scene '{scene.name}': {ex.Message}\n{ex.StackTrace}");
                return false;
            }
            finally
            {
                lock (ExecutionLock)
                {
                    _isExecuting = false;
                }
            }
        }

        private static void ExecuteSceneGeneration(Scene scene, TunnelGenerator tunnelGenerator, GenerateTunnelTestBootstrap settingsView)
        {
            // 1. 生成リクエストDTOの構築（Viewから設定値を読み取り、無ければ既定値）
            TunnelGenerationRequestDto request = CreateRequestDto(tunnelGenerator, settingsView);

            // 2. TunnelMapView の取得または生成（MapRoot配下に配置）
            TunnelMapView mapView = ResolveTunnelMapView(scene, tunnelGenerator);
            if (mapView == null)
            {
                Debug.LogError($"[GenerateTunnelBootstrapper] Failed to resolve or create TunnelMapView for scene '{scene.name}'.");
                return;
            }

#if STEAMAUDIO_ENABLED
            Shinzui.DI.TunnelAcoustics.TunnelAudioBinding.Attach(mapView);
#endif

            mapView.Configure(tunnelGenerator);

            // 3. 各層のインスタンスを生成・結線 (Composition Root)
            ITunnelLayoutGenerator layoutGenerator = new TunnelLayoutGenerator(); // Domain
            IGenerateTunnelUseCase useCase = new GenerateTunnelUseCase(layoutGenerator); // Application
            GenerateTunnelPresenter presenter = new GenerateTunnelPresenter(useCase, mapView); // Presentation

            // 4. マップ生成とNavMeshベイクを実行
            bool success = presenter.ExecuteGeneration(request);
            if (success)
            {
                Debug.Log($"[GenerateTunnelBootstrapper] Successfully generated tunnel map for scene '{scene.name}' (handle: {scene.handle}).");

                // 5. マップ生成成功後にアイテムスポーンを連携実行
                BootstrapItemSpawn(scene, mapView);
            }
            else
            {
                Debug.LogWarning($"[GenerateTunnelBootstrapper] Tunnel map generation was skipped or already completed for scene '{scene.name}'.");
            }
        }

        /// <summary>
        /// マップ生成完了後にMapRoot配下のWeightedSpawnSurfaceを収集し、ItemSpawnManagerを厳密に1回起動する。
        /// </summary>
        private static void BootstrapItemSpawn(Scene scene, TunnelMapView mapView)
        {
            try
            {
                // 1. MapRoot GameObjectの取得
                GameObject mapRoot = mapView != null ? mapView.MapRootObject : null;
                if (mapRoot == null)
                {
                    foreach (GameObject rootObj in scene.GetRootGameObjects())
                    {
                        if (string.Equals(rootObj.name, "MapRoot", StringComparison.Ordinal))
                        {
                            mapRoot = rootObj;
                            break;
                        }
                    }
                }

                if (mapRoot == null)
                {
                    Debug.LogWarning($"[GenerateTunnelBootstrapper] MapRoot was not found for scene '{scene.name}'. Item spawning skipped.");
                    return;
                }

                // 2. MapRoot配下の有効なWeightedSpawnSurfaceを収集
                WeightedSpawnSurface[] surfaces = mapRoot.GetComponentsInChildren<WeightedSpawnSurface>(true);
                var validSurfaces = new List<WeightedSpawnSurface>();
                if (surfaces != null)
                {
                    for (int i = 0; i < surfaces.Length; i++)
                    {
                        if (surfaces[i] != null && surfaces[i].gameObject.activeInHierarchy && surfaces[i].IsDataValid)
                        {
                            validSurfaces.Add(surfaces[i]);
                        }
                    }
                }

                if (validSurfaces.Count == 0)
                {
                    Debug.LogWarning($"[GenerateTunnelBootstrapper] No valid WeightedSpawnSurface found under MapRoot in scene '{scene.name}'. Item spawning skipped.");
                    return;
                }

                // 3. シーン内のItemSpawnContainerViewおよびItemSpawnManagerを探索
                ItemSpawnContainerView containerView = null;
                ItemSpawnManager spawnManager = null;

                foreach (GameObject rootObj in scene.GetRootGameObjects())
                {
                    if (containerView == null)
                    {
                        containerView = rootObj.GetComponentInChildren<ItemSpawnContainerView>(true);
                    }
                    if (spawnManager == null)
                    {
                        spawnManager = rootObj.GetComponentInChildren<ItemSpawnManager>(true);
                    }
                    if (containerView != null && spawnManager != null) break;
                }

                if (containerView == null)
                {
                    containerView = UnityEngine.Object.FindFirstObjectByType<ItemSpawnContainerView>();
                }
                if (spawnManager == null)
                {
                    spawnManager = UnityEngine.Object.FindFirstObjectByType<ItemSpawnManager>();
                }

                if (containerView == null)
                {
                    Debug.LogWarning($"[GenerateTunnelBootstrapper] ItemSpawnContainerView was not found in scene '{scene.name}'. Item spawning skipped.");
                    return;
                }

                if (spawnManager == null)
                {
                    Debug.LogWarning($"[GenerateTunnelBootstrapper] ItemSpawnManager was not found in scene '{scene.name}'. Item spawning skipped.");
                    return;
                }

                // 4. 収集したサーフェスをItemSpawnContainerViewへ設定
                containerView.SetSurfaces(validSurfaces);

                // 5. ItemSpawnManager.RequestGenerateAfterMapReady() を呼び出してマップ完了後の自動生成を要求
                spawnManager.RequestGenerateAfterMapReady();
                Debug.Log($"[GenerateTunnelBootstrapper] Successfully requested item spawn after map ready for scene '{scene.name}' with {validSurfaces.Count} surfaces.");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[GenerateTunnelBootstrapper] Exception during item spawn bootstrapping in scene '{scene.name}': {ex.Message}\n{ex.StackTrace}");
            }
        }

        private static TunnelGenerationRequestDto CreateRequestDto(TunnelGenerator tunnelGenerator, GenerateTunnelTestBootstrap settingsView)
        {
            if (tunnelGenerator != null)
            {
                return new TunnelGenerationRequestDto
                {
                    TunnelCount = tunnelGenerator.TunnelCount,
                    Seed = tunnelGenerator.Seed,
                    PlacementAttemptsPerTunnel = tunnelGenerator.PlacementAttemptsPerTunnel,
                    SmallRoomCount = tunnelGenerator.SmallRoomCount,
                    SmallRoomWidth = tunnelGenerator.SmallRoomWidth,
                    SmallRoomLength = tunnelGenerator.SmallRoomLength,
                    TunnelLength = tunnelGenerator.TunnelLength,
                    ConnectionPointSpacing = tunnelGenerator.ConnectionPointSpacing,
                    TunnelWidth = tunnelGenerator.TunnelWidth,
                    TunnelHeight = tunnelGenerator.TunnelHeight,
                    CorridorLength = tunnelGenerator.CorridorLength,
                    CorridorWidth = tunnelGenerator.CorridorWidth,
                    PlacementMargin = tunnelGenerator.PlacementMargin,
                    TunnelOverlapSizeMultiplier = tunnelGenerator.TunnelOverlapSizeMultiplier,
                    CorridorOverlapSizeMultiplier = tunnelGenerator.CorridorOverlapSizeMultiplier,
                    SmallRoomOverlapSizeMultiplier = tunnelGenerator.SmallRoomOverlapSizeMultiplier
                };
            }

            if (settingsView != null)
            {
                return new TunnelGenerationRequestDto
                {
                    TunnelCount = settingsView.TunnelCount,
                    Seed = settingsView.Seed,
                    PlacementAttemptsPerTunnel = settingsView.PlacementAttemptsPerTunnel,
                    SmallRoomCount = settingsView.SmallRoomCount,
                    SmallRoomWidth = settingsView.SmallRoomWidth,
                    SmallRoomLength = settingsView.SmallRoomLength,
                    TunnelLength = settingsView.TunnelLength,
                    ConnectionPointSpacing = settingsView.ConnectionPointSpacing,
                    TunnelWidth = settingsView.TunnelWidth,
                    TunnelHeight = settingsView.TunnelHeight,
                    CorridorLength = settingsView.CorridorLength,
                    CorridorWidth = settingsView.CorridorWidth,
                    PlacementMargin = settingsView.PlacementMargin,
                    TunnelOverlapSizeMultiplier = settingsView.TunnelOverlapSizeMultiplier,
                    CorridorOverlapSizeMultiplier = settingsView.CorridorOverlapSizeMultiplier,
                    SmallRoomOverlapSizeMultiplier = settingsView.SmallRoomOverlapSizeMultiplier
                };
            }

            return new TunnelGenerationRequestDto();
        }

        private static TunnelMapView ResolveTunnelMapView(Scene scene, TunnelGenerator tunnelGenerator)
        {
            if (tunnelGenerator != null && tunnelGenerator.MapView != null)
            {
                return tunnelGenerator.MapView;
            }

            // シーン内の既存 TunnelMapView を検索
            foreach (GameObject rootObj in scene.GetRootGameObjects())
            {
                TunnelMapView existingView = rootObj.GetComponentInChildren<TunnelMapView>(true);
                if (existingView != null)
                {
                    return existingView;
                }
            }

            // 既存または新規の MapRoot を取得
            GameObject mapRoot = null;
            foreach (GameObject rootObj in scene.GetRootGameObjects())
            {
                if (string.Equals(rootObj.name, "MapRoot", StringComparison.Ordinal))
                {
                    mapRoot = rootObj;
                    break;
                }
            }

            if (mapRoot == null)
            {
                mapRoot = new GameObject("MapRoot");
                SceneManager.MoveGameObjectToScene(mapRoot, scene);
            }

            mapRoot.SetActive(true);

            // MapRoot に TunnelMapView コンポーネントを追加して返却
            TunnelMapView mapView = mapRoot.GetComponent<TunnelMapView>();
            if (mapView == null)
            {
                mapView = mapRoot.AddComponent<TunnelMapView>();
            }

            return mapView;
        }

        private static TunnelGenerator FindTunnelGeneratorInScene(Scene scene)
        {
            foreach (GameObject rootObj in scene.GetRootGameObjects())
            {
                TunnelGenerator generator = rootObj.GetComponentInChildren<TunnelGenerator>(true);
                if (generator != null)
                {
                    return generator;
                }
            }
            return null;
        }

        private static GenerateTunnelTestBootstrap FindBootstrapViewInScene(Scene scene)
        {
            foreach (GameObject rootObj in scene.GetRootGameObjects())
            {
                GenerateTunnelTestBootstrap bootstrap = rootObj.GetComponentInChildren<GenerateTunnelTestBootstrap>(true);
                if (bootstrap != null)
                {
                    return bootstrap;
                }
            }
            return null;
        }
    }
}
