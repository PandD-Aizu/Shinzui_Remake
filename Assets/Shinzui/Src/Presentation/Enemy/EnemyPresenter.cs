using System;
using System.Collections.Generic;
using Shinzui.Application.Interfaces;
using Shinzui.Application.DTOs.Enemy;
using Shinzui.Application.UseCases;
using Shinzui.Application.UseCases.Enemy;
using Shinzui.View;
using UnityEngine;
using VContainer.Unity;

namespace Shinzui.Presentation
{
    /// <summary>
    /// 敵全体をVContainerのEntryPointとして駆動するPresenter。
    /// 1体ごとの行動制御は EnemyController に委譲する。
    /// </summary>
    public class EnemyPresenter : IInitializable, ITickable, IDisposable
    {
        private readonly IPlayerTracker _playerTracker;
        private readonly PlayerDeathUseCase _playerDeathUseCase;
        private readonly EnemyDirectorUseCase _enemyDirectorUseCase;
        private readonly Func<EnemyMoveUseCase> _enemyMoveUseCaseFactory;
        private readonly EnemyView[] _enemyViews;
        private readonly IEnemyRuntime[] _runtimes;
        private readonly EnemyDirectorSettings _settings;
        private readonly PlayerMoveUseCase _playerMove;
        private Vector3 _previousPlayerPosition;
        private float _footstepTimer;
        private bool _hasPreviousPosition;
        private readonly List<EnemyController> _controllers = new();
        private readonly Dictionary<EnemyView, EnemyController> _controllersByView = new();
        private EnemyReport[] _enemyReports = Array.Empty<EnemyReport>();

        public EnemyPresenter(
            IPlayerTracker playerTracker,
            PlayerDeathUseCase playerDeathUseCase,
            EnemyDirectorUseCase enemyDirectorUseCase,
            Func<EnemyMoveUseCase> enemyMoveUseCaseFactory,
            EnemyView[] enemyViews,
            IEnemyRuntime[] runtimes,
            EnemyDirectorSettings settings,
            PlayerMoveUseCase playerMove)
        {
            _playerTracker = playerTracker;
            _playerDeathUseCase = playerDeathUseCase;
            _enemyDirectorUseCase = enemyDirectorUseCase;
            _enemyMoveUseCaseFactory = enemyMoveUseCaseFactory;
            _enemyViews = enemyViews ?? Array.Empty<EnemyView>();
            _runtimes = runtimes ?? Array.Empty<IEnemyRuntime>();
            _settings = settings;
            _playerMove = playerMove;
        }

        public void Initialize()
        {
            _controllers.Clear();
            _controllersByView.Clear();

            for (int i = 0; i < _enemyViews.Length; i++)
            {
                EnemyView enemyView = _enemyViews[i];
                if (enemyView == null || i >= _runtimes.Length || _runtimes[i] == null || _controllersByView.ContainsKey(enemyView))
                {
                    continue;
                }

                var controller = new EnemyController(
                    _controllers.Count,
                    enemyView,
                    _runtimes[i],
                    _enemyMoveUseCaseFactory(),
                    _playerDeathUseCase,
                    _settings);
                controller.Initialize();

                _controllers.Add(controller);
                _controllersByView.Add(enemyView, controller);
            }

            if (_controllers.Count == 0)
            {
                Debug.LogWarning("EnemyPresenter: EnemyView was not assigned and not found in the scene hierarchy.");
            }

            _enemyReports = new EnemyReport[_controllers.Count];
        }

        public void Tick()
        {
            float deltaTime = Time.deltaTime;
            if (deltaTime <= 0f) return;
            EnemyCommand[] commands = DecideCommands(deltaTime);

            foreach (EnemyController controller in _controllers)
            {
                EnemyCommand command = controller.Id >= 0 && controller.Id < commands.Length
                    ? commands[controller.Id]
                    : EnemyCommand.Wander;
                controller.Tick(deltaTime, _playerTracker, _playerMove.CurrentHeight.CurrentValue, command);
            }
        }

        public void Dispose()
        {
            foreach (EnemyController controller in _controllers)
            {
                controller.Dispose();
            }

            _controllers.Clear();
            _controllersByView.Clear();
            _enemyReports = Array.Empty<EnemyReport>();
        }

        /// <summary>
        /// 統括AIに現在の世界状態を渡してEnemyごとの命令を決定する
        /// </summary>
        /// <returns>EnemyのIdに対応する命令配列</returns>
        private EnemyCommand[] DecideCommands(float deltaTime)
        {
            if (_enemyDirectorUseCase == null || _playerTracker == null)
            {
                return Array.Empty<EnemyCommand>();
            }

            EnsureReportCapacity();

            for (int i = 0; i < _controllers.Count; i++)
            {
                _enemyReports[i] = _controllers[i].CreateReport(deltaTime, _playerTracker.PlayerPosition,
                    _playerMove.CurrentHeight.CurrentValue);
            }

            var tunnelBounds = _playerTracker.CurrentTunnelBounds;
            EnemyWorldState worldState = new EnemyWorldState(
                _playerTracker.PlayerPosition,
                _enemyReports,
                tunnelBounds.HasValue,
                tunnelBounds.HasValue ? tunnelBounds.Value.start : Vector3.zero,
                tunnelBounds.HasValue ? tunnelBounds.Value.end : Vector3.zero,
                GetFootstepNoiseRadius(deltaTime));

            return _enemyDirectorUseCase.DecideCommands(worldState, deltaTime);
        }

        private float GetFootstepNoiseRadius(float deltaTime)
        {
            Vector3 position = _playerTracker.PlayerPosition;
            Vector3 movement = position - _previousPlayerPosition;
            movement.y = 0f;
            bool moved = _hasPreviousPosition && movement.sqrMagnitude > .000001f && movement.sqrMagnitude < 4f;
            _previousPlayerPosition = position;
            _hasPreviousPosition = true;
            _footstepTimer -= deltaTime;
            if (!moved || _playerMove.IsCrouching || _footstepTimer > 0f) return 0f;
            _footstepTimer = Mathf.Max(.1f, _settings.footstepInterval);
            return Mathf.Max(0f, _playerMove.IsRunning ? _settings.runningNoiseRadius : _settings.walkingNoiseRadius);
        }

        /// <summary>
        /// Enemy状態報告配列の容量を現在のController数に合わせる
        /// </summary>
        private void EnsureReportCapacity()
        {
            if (_enemyReports.Length == _controllers.Count)
            {
                return;
            }

            _enemyReports = new EnemyReport[_controllers.Count];
        }

        /// <summary>
        /// RaycastHit からストロボのターゲットを決定する。
        /// </summary>
        /// <param name="hit">Raycastの結果</param>
        /// <param name="maxDistance">最大検索距離</param>
        /// <returns>ストロボのターゲット</returns>
        public StrobeTarget FindStrobeTarget(RaycastHit hit, float maxDistance)
        {
            if (TryFindController(hit, out EnemyController controller))
            {
                return new StrobeTarget(controller);
            }

            return FindNearestStrobeTarget(hit.point, maxDistance);
        }

        /// <summary>
        /// RaycastHit から EnemyController を検索する。
        /// </summary>
        /// <param name="hit">Raycastの結果</param>
        /// <param name="controller">見つかったEnemyController</param>
        /// <returns>見つかったかどうか</returns>
        private bool TryFindController(RaycastHit hit, out EnemyController controller)
        {
            controller = null;
            if (hit.collider == null)
            {
                return false;
            }

            if (TryFindController(hit.collider.GetComponentInParent<EnemyView>(), out controller))
            {
                return true;
            }

            if (TryFindController(hit.collider.GetComponentInChildren<EnemyView>(), out controller))
            {
                return true;
            }

            return TryFindController(hit.collider.transform.root.GetComponentInChildren<EnemyView>(), out controller);
        }

        /// <summary>
        /// EnemyView から EnemyController を検索する
        /// </summary>
        /// <param name="enemyView">検索するEnemyView</param>
        /// <param name="controller">見つかったEnemyController</param>
        /// <returns>見つかったかどうか</returns>
        private bool TryFindController(EnemyView enemyView, out EnemyController controller)
        {
            controller = null;
            return enemyView != null && _controllersByView.TryGetValue(enemyView, out controller);
        }

        /// <summary>
        /// 指定された位置から最も近いストロボターゲットを検索する
        /// </summary>
        /// <param name="position">検索する位置</param>
        /// <param name="maxDistance">最大検索距離</param>
        /// <returns>ストロボのターゲット</returns>
        private StrobeTarget FindNearestStrobeTarget(Vector3 position, float maxDistance)
        {
            EnemyController nearestController = null;
            float nearestSqrDistance = maxDistance * maxDistance;

            foreach (EnemyController controller in _controllers)
            {
                float sqrDistance = (controller.StrobeTargetPosition - position).sqrMagnitude;
                if (sqrDistance > nearestSqrDistance)
                {
                    continue;
                }

                nearestSqrDistance = sqrDistance;
                nearestController = controller;
            }

            return nearestController != null ? new StrobeTarget(nearestController) : StrobeTarget.Empty;
        }
    }
}
