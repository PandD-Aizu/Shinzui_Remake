using System;
using System.Collections.Generic;
using Shinzui.Application.Interfaces;
using Shinzui.Application.Interfaces.Enemy;
using Shinzui.Application.DTOs.Enemy;
using Shinzui.Application.UseCases;
using Shinzui.Application.UseCases.Enemy;
using Shinzui.Application.UseCases.Flashlight;
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
        private readonly FlashlightUseCase _flashlight;
        private readonly NoiseEventHub _noiseHub;
        private readonly EnemyBrainSettings _brainSettings;
        private readonly IEnemyEncounterFeedback _feedback;
        private readonly Func<Vector3, bool> _visibility;
        private readonly Func<int, Vector3, bool> _canReachExit;
        private readonly PlayerView _playerView;
        private readonly PlayerDeathView _deathView;
        private Vector3 _previousPlayerPosition;
        private float _footstepTimer;
        private bool _hasPreviousPosition;
        private bool _cinematicPaused;
        private readonly List<EnemyController> _controllers = new();
        private readonly Dictionary<EnemyView, EnemyController> _controllersByView = new();
        private EnemyReport[] _enemyReports = Array.Empty<EnemyReport>();
        private readonly PlayerTacticTracker _tactics;
        private readonly IEnemySearchPointProvider _points;
        private IEnemyStageProgress _stageProgress;
        private readonly IHidingSpotRegistry _hidingSpots;
        private readonly Shinzui.Application.UseCases.Player.PlayerHidingUseCase _hiding;
        private readonly PlayerHidingPresenter _hidingPresenter;
        private readonly StageResultUseCase _stageResult;

        /// <summary>敵の知覚と統括AIをプレイヤー状態へ接続</summary>
        /// <param name="playerTracker">プレイヤーの位置</param>
        /// <param name="playerDeathUseCase">死亡判定</param>
        /// <param name="enemyDirectorUseCase">統括AI</param>
        /// <param name="enemyMoveUseCaseFactory">移動状態の生成</param>
        /// <param name="enemyViews">敵の表示</param>
        /// <param name="runtimes">物理と経路探索</param>
        /// <param name="settings">統括設定</param>
        /// <param name="playerMove">姿勢と移動状態</param>
        /// <param name="playerView">プレイヤー表示</param>
        /// <param name="deathView">捕獲演出</param>
        /// <param name="flashlight">光の状態</param>
        /// <param name="noiseHub">フレーム内の音</param>
        /// <param name="brainSettings">知覚設定</param>
        /// <param name="feedback">敵の出現音通知</param>
        /// <param name="tactics">全敵で共有する学習</param>
        /// <param name="points">捜索点の取得</param>
        /// <param name="hidingSpots">隠れ場所の登録先</param>
        /// <param name="hiding">プレイヤーの隠れ状態</param>
        /// <param name="hidingPresenter">隠れ場所の開閉処理</param>
        /// <param name="stageResult">追跡遷移の集計先</param>
        public EnemyPresenter(
            IPlayerTracker playerTracker,
            PlayerDeathUseCase playerDeathUseCase,
            EnemyDirectorUseCase enemyDirectorUseCase,
            Func<EnemyMoveUseCase> enemyMoveUseCaseFactory,
            EnemyView[] enemyViews,
            IEnemyRuntime[] runtimes,
            EnemyDirectorSettings settings,
            PlayerMoveUseCase playerMove,
            PlayerView playerView,
            PlayerDeathView deathView, FlashlightUseCase flashlight, NoiseEventHub noiseHub, EnemyBrainSettings brainSettings,
            IEnemyEncounterFeedback feedback, PlayerTacticTracker tactics, IEnemySearchPointProvider points,
            IHidingSpotRegistry hidingSpots, Shinzui.Application.UseCases.Player.PlayerHidingUseCase hiding, PlayerHidingPresenter hidingPresenter,
            StageResultUseCase stageResult = null)
        {
            _playerTracker = playerTracker;
            _playerDeathUseCase = playerDeathUseCase;
            _enemyDirectorUseCase = enemyDirectorUseCase;
            _enemyMoveUseCaseFactory = enemyMoveUseCaseFactory;
            _enemyViews = enemyViews ?? Array.Empty<EnemyView>();
            _runtimes = runtimes ?? Array.Empty<IEnemyRuntime>();
            _settings = settings;
            _playerMove = playerMove;
            _playerView = playerView;
            _deathView = deathView;
            _flashlight = flashlight;
            _noiseHub = noiseHub;
            _brainSettings = brainSettings;
            _feedback = feedback;
            _tactics = tactics;
            _points = points;
            _hidingSpots = hidingSpots; _hiding = hiding; _hidingPresenter = hidingPresenter;
            _visibility = IsPositionVisible;
            _canReachExit = CanReachExit;
            _stageResult = stageResult;
        }

        /// <summary>各敵に独立した知覚状態を作成</summary>
        public void Initialize()
        {
            _controllers.Clear();
            _controllersByView.Clear();

            // 仮の進行処理にもインターフェイスだけで接続し、手置きシーンでは一階扱い
            foreach (var component in UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
                if (component.gameObject.scene == _playerView.gameObject.scene && component is IEnemyStageProgress progress)
                { _stageProgress = progress; break; }

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
                    _settings,
                    _brainSettings,
                    _feedback,
                    CapturePlayer, _tactics, _points, _hidingSpots, _hiding, _hidingPresenter,
                    id => OpenHidingSpot(id, enemyView));
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

        /// <summary>知覚を更新して移動と接触判定を駆動</summary>
        public void Tick()
        {
            // 導入演出中もマップ生成を待ち、通常AIと接触判定より先に初期配置を確定
            foreach (EnemyView enemy in _enemyViews)
                if (enemy != null && enemy.isActiveAndEnabled
                    && !enemy.TryCompleteInitialPlacement(_playerTracker.PlayerPosition)) return;

            if (_cinematicPaused || _playerDeathUseCase.IsDead.CurrentValue)
            {
                _noiseHub.Drain();
                return;
            }

            float deltaTime = Time.deltaTime;
            if (deltaTime <= 0f) return;
            DirectorDirective[] directives = DecideDirectives(deltaTime);

            foreach (EnemyController controller in _controllers)
            {
                DirectorDirective directive = controller.Id >= 0 && controller.Id < directives.Length
                    ? directives[controller.Id]
                    : default;
                controller.Tick(deltaTime, _playerTracker, _playerMove.CurrentHeight.CurrentValue, directive);

                // 各敵のChase開始を集計し、継続フレームの重複はUseCase側で除く
                _stageResult?.ObserveIntent(controller.Id, controller.ResultIntent);

                _enemyReports[controller.Id] = controller.CurrentReport();
                if (_playerDeathUseCase.IsDead.CurrentValue) break;
            }

            // 同じフレームに確定した発見を演出へ反映する
            _enemyDirectorUseCase.ObserveReports(_enemyReports);
            if (!_playerDeathUseCase.IsDead.CurrentValue)
                _tactics.Tick(deltaTime, _playerMove.IsCrouching, _playerTracker.PlayerPosition, _enemyReports,
                    _stageProgress?.FloorNumber ?? 1);
        }

        /// <summary>死亡回避を判定し、人型の捕獲だけ専用演出を予約</summary>
        /// <param name="enemy">捕獲した敵</param>
        /// <returns>死亡が確定した場合はtrue</returns>
        private bool CapturePlayer(EnemyView enemy)
        {
            bool human = enemy.GetComponent<HumanEnemyLocomotion>() != null;
            if (human) _deathView.PrepareHumanCapture(enemy, _playerView);
            bool killed = _playerDeathUseCase.TryKillPlayer(playDeathSound: !human);
            if (!killed) _deathView.PrepareHumanCapture(null, null);
            else
            {
                SetCinematicPaused(true);

                // 捕獲した敵を残し、周囲の敵を演出開始時に消す
                foreach (var other in _enemyViews)
                    if (other != null && other != enemy) other.FadeOut();
            }
            return killed;
        }

        /// <summary>ドアを開けて実際に中にいた場合だけ既存の死亡判定を行う</summary>
        /// <param name="id">ロッカー</param>
        /// <param name="enemy">開けた敵</param>
        /// <returns>プレイヤーを引きずり出したか</returns>
        private bool OpenHidingSpot(int id, EnemyView enemy)
        {
            if (!_hidingPresenter.OpenByEnemy(id)) return false;
            CapturePlayer(enemy);
            return true;
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
        /// 導入中の通常AIと接触死亡判定をまとめて停止または再開
        /// </summary>
        /// <param name="paused">演出側が敵を操作する場合はtrue</param>
        public void SetCinematicPaused(bool paused)
        {
            _cinematicPaused = paused;
            _hasPreviousPosition = false;
            _noiseHub.Drain();

            // 演出側が動かす敵を含め、前の経路と速度設定を解除
            foreach (var runtime in _runtimes)
            {
                if (runtime == null) continue;
                runtime.Stop();
                runtime.SetSpeed(paused ? 0f : 1f, paused);
            }
        }

        /// <summary>
        /// 知覚を採取し、前回のBrain報告から統括AIのヒントを決定
        /// </summary>
        /// <param name="deltaTime">フレーム経過時間</param>
        /// <returns>EnemyのIdに対応する指示配列</returns>
        private DirectorDirective[] DecideDirectives(float deltaTime)
        {
            if (_enemyDirectorUseCase == null || _playerTracker == null)
            {
                return Array.Empty<DirectorDirective>();
            }

            EnsureReportCapacity();

            // ワープを速度と足音に含めず、同じノイズ一覧を全敵へ渡す
            Vector3 position = _playerTracker.PlayerPosition;
            Vector3 movement = position - _previousPlayerPosition;
            Vector3 velocity = _hasPreviousPosition && movement.sqrMagnitude < 4f ? movement / deltaTime : Vector3.zero;
            float footstepRadius = GetFootstepNoiseRadius(deltaTime);
            if (footstepRadius > 0f) _noiseHub.Emit(new NoiseEvent(NoiseKind.Footstep, position, footstepRadius));
            IReadOnlyList<NoiseEvent> noises = _noiseHub.Drain();

            for (int i = 0; i < _controllers.Count; i++)
            {
                bool illuminatesEnemy = _playerView.MainCamera != null && Vector3.Angle(_playerView.CameraForward,
                    _controllers[i].StrobeTargetPosition - _playerView.CameraPosition) <= 30f;
                var signature = new PlayerSignature(_playerMove.IsCrouching, _playerMove.IsRunning,
                    velocity.sqrMagnitude > .0001f, _flashlight.IsOn.CurrentValue, illuminatesEnemy);
                _enemyReports[i] = _controllers[i].CreateReport(deltaTime, _playerTracker.PlayerPosition,
                    _playerMove.CurrentHeight.CurrentValue, _playerView.MainCamera, _playerView.transform, velocity, signature, noises);
            }

            var tunnelBounds = _playerTracker.CurrentTunnelBounds;
            EnemyWorldState worldState = new EnemyWorldState(
                _playerTracker.PlayerPosition,
                _enemyReports,
                tunnelBounds.HasValue,
                tunnelBounds.HasValue ? tunnelBounds.Value.start : Vector3.zero,
                tunnelBounds.HasValue ? tunnelBounds.Value.end : Vector3.zero, isVisibleToPlayer: _visibility, canReachExit: _canReachExit);

            return _enemyDirectorUseCase.DecideDirectives(worldState, deltaTime);
        }

        /// <summary>対象の敵の経路を確認</summary>
        /// <param name="id">敵の識別子</param>
        /// <param name="position">退出候補</param>
        /// <returns>到達可能か</returns>
        private bool CanReachExit(int id, Vector3 position)
            => id >= 0 && id < _controllers.Count && _controllers[id].CanReachExit(position);

        /// <summary>どの敵の大きさでも見えてしまう候補を除外</summary>
        /// <param name="position">出入り候補</param>
        /// <returns>カメラ不明または候補が可視ならtrue</returns>
        private bool IsPositionVisible(Vector3 position)
        {
            if (_controllers.Count == 0) return true;
            foreach (var controller in _controllers)
                if (controller.IsVisibleAt(position)) return true;
            return false;
        }

        /// <summary>移動距離と足音間隔からノイズ半径を決定</summary>
        /// <param name="deltaTime">経過秒数</param>
        /// <returns>足音が発生しない場合は0</returns>
        private float GetFootstepNoiseRadius(float deltaTime)
        {
            if (_hiding.IsHidden) { _hasPreviousPosition = false; return 0f; }
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
                if (controller.CurrentReport().IsBackstage) continue;
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
