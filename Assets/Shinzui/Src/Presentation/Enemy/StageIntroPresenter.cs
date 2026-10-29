using System;
using Shinzui.Application.Interfaces;
using Shinzui.Application.UseCases;
using Shinzui.Application.UseCases.Inventory;
using Shinzui.View;
using VContainer.Unity;

namespace Shinzui.Presentation
{
    /// <summary>導入演出と既存の入力・敵制御を接続</summary>
    public sealed class StageIntroPresenter : IStartable, ITickable, IDisposable
    {
        private readonly StageIntroView _view;
        private readonly PlayerView _player;
        private readonly IInputService _input;
        private readonly EnemyPresenter _enemies;
        private readonly InventoryUseCase _inventory;
        private readonly PlayerDeathUseCase _death;
        private readonly HorrorDetectionView _horror;

        /// <summary>導入演出で使用する既存サービスを受け取る</summary>
        /// <param name="view">演出ビュー</param>
        /// <param name="player">プレイヤービュー</param>
        /// <param name="input">通常入力サービス</param>
        /// <param name="enemies">敵統括Presenter</param>
        /// <param name="inventory">インベントリ状態</param>
        /// <param name="death">通常のゲームオーバー処理</param>
        /// <param name="horror">追跡時と共通の砂嵐</param>
        public StageIntroPresenter(StageIntroView view, PlayerView player, IInputService input,
            EnemyPresenter enemies, InventoryUseCase inventory, PlayerDeathUseCase death, HorrorDetectionView horror)
        {
            _view = view;
            _player = player;
            _input = input;
            _enemies = enemies;
            _inventory = inventory;
            _death = death;
            _horror = horror;
        }

        /// <summary>通常AIを止め、ステージ準備完了を待つ導入を開始</summary>
        public void Start()
        {
            _view.ControlChanged += SetControl;
            _view.Finished += Finish;
            _view.Failed += Fail;
            _view.Begin(_player);
            if (_view.Phase != StageIntroView.IntroPhase.Complete) _enemies.SetCinematicPaused(true);
        }

        /// <summary>転倒・起立に合わせて通常入力を切り替える</summary>
        /// <param name="blocked">通常入力を停止するか</param>
        private void SetControl(bool blocked)
        {
            if (blocked && _inventory.IsOpen.CurrentValue) _inventory.ToggleOpen();
            _input.SetBlocked(blocked || _death.IsDead.CurrentValue);
            if (blocked) _view.HideCursor();
        }

        /// <summary>登場した敵を含む通常AIを再開</summary>
        private void Finish()
        {
            _horror.SetEventIntensity(0f);
            _enemies.SetCinematicPaused(false);
        }

        /// <summary>演出中の弱い砂嵐を通常の追跡ノイズへ重ねる</summary>
        public void Tick()
        {
            _horror.SetEventIntensity(_view.EventNoiseIntensity);
        }

        /// <summary>起立前に敵の顔が画面中央へ到達したら通常のゲームオーバーへ進む</summary>
        private void Fail()
        {
            _horror.SetEventIntensity(0f);
            _death.TryKillPlayer(allowDeathPrevention: false);
        }

        /// <summary>演出中断時も操作・カメラ・敵の占有を解除</summary>
        public void Dispose()
        {
            if (_view != null) _view.Cancel();
            _view.ControlChanged -= SetControl;
            _view.Finished -= Finish;
            _view.Failed -= Fail;
            _horror.SetEventIntensity(0f);
            _enemies.SetCinematicPaused(false);
        }
    }
}
