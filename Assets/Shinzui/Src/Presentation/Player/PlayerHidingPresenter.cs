using System;
using Shinzui.Application.DTOs.Enemy;
using Shinzui.Application.Interfaces;
using Shinzui.Application.Interfaces.Enemy;
using Shinzui.Application.UseCases;
using Shinzui.Application.UseCases.Player;
using Shinzui.Application.UseCases.Flashlight;
using Shinzui.View;
using UnityEngine;
using VContainer.Unity;

namespace Shinzui.Presentation
{
    /// <summary>ロッカーの登録、操作、スタミナを使う息止めとカメラを接続</summary>
    public sealed class PlayerHidingPresenter : IInitializable, ITickable, ILateTickable, IDisposable
    {
        private const double TutorialIntervalSeconds = 3600d;
        private static double _nextLockerTutorialTime;
        private static double _nextItemTutorialTime;
        private static double _nextBreathTutorialTime;

        /// <summary>ゲーム起動時に案内の待機時間を初期化する</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetTutorialCooldowns()
        {
            // シーン遷移では維持し、ドメインリロード無効時も起動時にはリセットする
            _nextLockerTutorialTime = 0d;
            _nextItemTutorialTime = 0d;
            _nextBreathTutorialTime = 0d;
        }

        private readonly PlayerHidingUseCase _hiding;
        private readonly IHidingSpotRegistry _spots;
        private readonly PlayerView _player;
        private readonly PlayerMoveUseCase _move;
        private readonly IInputService _input;
        private readonly FlashlightUseCase _light;
        private readonly PlayerDeathUseCase _death;
        private HidingLockerView _current;
        private Shinzui.View.Objective.ObjectiveView _tutorial;
        private bool _nearLocker;
        private bool _nearItem;
        private Shinzui.View.Interaction.PlayerInteractionView _interaction;
        private float _tutorialRemaining;
        private bool _breathTutorialPending;
        private Vector3 _entryPosition;
        private int _entryFrame;
        public Vector3 EntryPosition => _entryPosition;
        /// <summary>既存のプレイヤー処理へ接続</summary>
        /// <param name="hiding">隠れ状態</param>
        /// <param name="spots">地形一覧</param>
        /// <param name="player">表示</param>
        /// <param name="move">移動状態</param>
        /// <param name="input">入力</param>
        /// <param name="light">懐中電灯</param>
        /// <param name="death">死亡状態</param>
        public PlayerHidingPresenter(PlayerHidingUseCase hiding, IHidingSpotRegistry spots, PlayerView player,
            PlayerMoveUseCase move, IInputService input, FlashlightUseCase light, PlayerDeathUseCase death)
        { _hiding = hiding; _spots = spots; _player = player; _move = move; _input = input; _light = light; _death = death; }
        /// <summary>手置きと動的生成のロッカーを同じ一覧へ登録</summary>
        public void Initialize()
        {
            foreach (var locker in HidingLockerView.Active) Register(locker);
            HidingLockerView.Registered += Register;
            HidingLockerView.Removed += Remove;
            HidingLockerView.InteractionRequested += Interact;

            // 目標HUDと同じCanvasに操作案内を作成する
            var objective = UnityEngine.Object.FindFirstObjectByType<Shinzui.View.Objective.ObjectiveView>();
            if (objective != null) _tutorial = objective.CreateTutorialView();
            _interaction = _player.GetComponentInChildren<Shinzui.View.Interaction.PlayerInteractionView>();
            if (_interaction == null)
                _interaction = UnityEngine.Object.FindFirstObjectByType<Shinzui.View.Interaction.PlayerInteractionView>();
        }
        /// <summary>現在のシーンのロッカーだけ登録</summary>
        /// <param name="locker">対象</param>
        private void Register(HidingLockerView locker)
        {
            if (locker.gameObject.scene != _player.gameObject.scene || locker.StandPoint == null) return;
            _spots.Register(new HidingSpotDto(locker.Id, locker.transform.position, locker.StandPoint.position, locker.transform.forward));
        }
        /// <summary>生成マップの破棄時に隠れ状態も解除</summary>
        /// <param name="locker">対象</param>
        private void Remove(HidingLockerView locker)
        { if (_current == locker) Exit(false); _spots.Remove(locker.Id); }
        /// <summary>入口の視認位置を残して内部へ移動</summary>
        /// <param name="locker">対象</param>
        private void Interact(HidingLockerView locker)
        {
            if (_death.IsDead.CurrentValue || _player.IsCinematicControlled || _hiding.IsHidden || locker.CameraAnchor == null
                || Vector3.Distance(_player.transform.position, locker.ExitPoint.position) > 3f) return;
            if (!_hiding.Enter(locker.Id, _move.IsRunning)) return;
            _current = locker; _entryFrame = Time.frameCount; _entryPosition = _player.transform.position;
            locker.Occupied = true;
            _input.SetHiding(true); _light.ExtinguishForHiding();

            // 入る案内を消し、1時間以上空いていれば入室後に息止めを案内する
            HideTutorial();
            _breathTutorialPending = Time.realtimeSinceStartupAsDouble >= _nextBreathTutorialTime;

            // 扉は入室演出の手の動きで開閉する
            _player.BeginHiding(locker);
        }
        /// <summary>息止め、退出、死亡時の後始末を処理</summary>
        public void Tick()
        {
            UpdateTutorial();
            if (_death.IsDead.CurrentValue) { if (_hiding.IsHidden) Exit(false); return; }
            if (_hiding.IsHidden && Time.frameCount != _entryFrame && !_player.IsEnteringHiding && _input.HidingExitPressed) Exit(true);

            // 入室演出が終わってから、スタミナを消費して息を止められる
            _hiding.Tick(_input.HoldBreathHeld && !_player.IsEnteringHiding, Time.deltaTime);
        }

        /// <summary>接近時の操作案内を種類ごとに1時間以上空けて10秒間表示する</summary>
        private void UpdateTutorial()
        {
            if (_tutorial == null) return;

            // 入室演出が終わってから、息止めの操作を案内する
            if (_hiding.IsHidden && !_death.IsDead.CurrentValue && !_player.IsCinematicControlled)
            {
                if (_breathTutorialPending && !_player.IsEnteringHiding && Time.timeScale > 0f)
                {
                    _tutorial.Show("[右クリック] 長押しで息を止める\n（スタミナを消費）");
                    _tutorialRemaining = 10f;
                    _breathTutorialPending = false;
                    _nextBreathTutorialTime = Time.realtimeSinceStartupAsDouble + TutorialIntervalSeconds;
                }
                TickTutorialRemaining();
                return;
            }

            // 死亡、演出中は案内を消し、ポーズ中は残り時間を維持する
            if (_death.IsDead.CurrentValue || _player.IsCinematicControlled || _hiding.IsHidden)
            {
                if (_tutorialRemaining > 0f) _tutorial.Hide();
                _tutorialRemaining = 0f;
                return;
            }
            if (Time.timeScale == 0f) return;

            // 時間倍率に依存しない起動後の実時間で表示間隔を判定する
            double now = Time.realtimeSinceStartupAsDouble;

            // 入口から3m以内への接近を検出し、4m離れるまで再表示しない
            bool nearby = false;
            float radius = _nearLocker ? 4f : 3f;
            foreach (var locker in HidingLockerView.Active)
            {
                if (locker.gameObject.scene != _player.gameObject.scene || locker.ExitPoint == null
                    || locker.CameraAnchor == null || locker.Occupied) continue;
                if ((locker.ExitPoint.position - _player.transform.position).sqrMagnitude > radius * radius) continue;
                nearby = true;
                break;
            }

            if (nearby && !_nearLocker && now >= _nextLockerTutorialTime)
            {
                _tutorial.Show("[E] でロッカーに入る");
                _tutorialRemaining = 10f;
                _nextLockerTutorialTime = now + TutorialIntervalSeconds;
            }
            _nearLocker = nearby;

            // アイテム案内は表示中の案内が終わるまで待ち、離れるまで再表示しない
            bool nearItem = _interaction != null
                && _interaction.HasNearbyPickup(_player.transform.position, _nearItem ? 4f : 3f);
            if (!nearItem) _nearItem = false;
            if (nearItem && !_nearItem && _tutorialRemaining <= 0f && !_interaction.IsPickupActive
                && now >= _nextItemTutorialTime)
            {
                _tutorial.Show("[Ctrl] でしゃがんで、\n[E] でアイテムを拾う");
                _tutorialRemaining = 10f;
                _nearItem = true;
                _nextItemTutorialTime = now + TutorialIntervalSeconds;
            }

            TickTutorialRemaining();
        }

        /// <summary>滞在中も表示時間を延長せず、時間経過でフェードアウトする</summary>
        private void TickTutorialRemaining()
        {
            if (_tutorialRemaining <= 0f) return;
            _tutorialRemaining -= Time.deltaTime;
            if (_tutorialRemaining <= 0f) _tutorial.Hide();
        }

        /// <summary>表示中の案内を消し、未表示の息止め案内も取り消す</summary>
        private void HideTutorial()
        {
            _breathTutorialPending = false;
            if (_tutorial == null || _tutorialRemaining <= 0f) return;
            _tutorial.Hide();
            _tutorialRemaining = 0f;
        }
        /// <summary>カメラ更新後にも内部視点を維持</summary>
        public void LateTick() { if (_hiding.IsHidden) _player.UpdateHidingCamera(_hiding.IsHoldingBreath); }
        /// <summary>安全な出口へプレイヤーを戻す</summary>
        /// <param name="animate">通常のドア開閉を行うか</param>
        private void Exit(bool animate)
        {
            var locker = _current;
            _hiding.Exit(); _input.SetHiding(false);
            HideTutorial();
            if (locker != null)
            {
                locker.Occupied = false;
                _player.EndHiding(locker.ExitPoint.position, locker.transform.forward);
                if (animate) locker.UseDoor();
            }
            else _player.EndHiding(_entryPosition, _player.transform.forward);
            _current = null;
        }
        /// <summary>敵が開けた対象だけを公開し、死亡回避にも備えて外へ戻す</summary>
        /// <param name="id">開けるロッカー</param>
        /// <returns>中にプレイヤーがいたか</returns>
        public bool OpenByEnemy(int id)
        {
            foreach (var locker in HidingLockerView.Active)
                if (locker.Id == id)
                {
                    locker.OpenByEnemy();
                    if (_hiding.CurrentSpotId != id) return false;
                    Exit(false);
                    return true;
                }
            return false;
        }
        /// <summary>購読と生成UIを解放</summary>
        public void Dispose()
        {
            HidingLockerView.Registered -= Register; HidingLockerView.Removed -= Remove;
            HidingLockerView.InteractionRequested -= Interact;
            if (_hiding.IsHidden) Exit(false);
            _spots.Clear();
            if (_tutorial != null) UnityEngine.Object.Destroy(_tutorial.gameObject);
        }
    }
}
