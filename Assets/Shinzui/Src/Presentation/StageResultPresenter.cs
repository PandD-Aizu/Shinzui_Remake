using System;
using Shinzui.Application.Interfaces;
using Shinzui.Application.Title;
using Shinzui.Application.UseCases;
using Shinzui.Domain.ValueObjects.StageResults;
using Shinzui.View.GenerateTunnel;
using Shinzui.View.UI;
using UnityEngine;
using VContainer.Unity;

namespace Shinzui.Presentation
{
    /// <summary>ゴールと結果集計と結果画面とSaveStageへの遷移を接続する</summary>
    public sealed class StageResultPresenter : IInitializable, IDisposable
    {
        private readonly StageResultUseCase _tracker;
        private readonly StageResultRules _rules;
        private readonly StageResultView _view;
        private readonly StageResultTrigger _trigger;
        private readonly IInputService _input;
        private readonly EnemyPresenter _enemies;
        private readonly PlayerDeathUseCase _death;
        private readonly ITitleNavigation _navigation;
        private readonly IStageResultAudio _audio;
        private bool _completed;
        private bool _loading;
        private bool _disposed;

        /// <summary>シーンの結果画面に必要なサービスを受け取る</summary>
        /// <param name="tracker">統計集計</param>
        /// <param name="rules">評価条件</param>
        /// <param name="view">結果画面</param>
        /// <param name="trigger">プレイヤーのゴール通知</param>
        /// <param name="input">ゲーム入力</param>
        /// <param name="enemies">敵AIの停止先</param>
        /// <param name="death">死亡状態</param>
        /// <param name="navigation">既存のAddressables遷移</param>
        /// <param name="audio">効果音の制御</param>
        public StageResultPresenter(StageResultUseCase tracker, StageResultRules rules, StageResultView view,
            StageResultTrigger trigger, IInputService input, EnemyPresenter enemies, PlayerDeathUseCase death,
            ITitleNavigation navigation, IStageResultAudio audio)
        {
            // ステージと同じスコープの依存を保持する
            _tracker = tracker;
            _rules = rules;
            _view = view;
            _trigger = trigger;
            _input = input;
            _enemies = enemies;
            _death = death;
            _navigation = navigation;
            _audio = audio;
        }

        /// <summary>ステージ開始時に統計と画面イベントを接続する</summary>
        public void Initialize()
        {
            // Time.timeAsDoubleはポーズ中に進まないため停止時間を除外できる
            _tracker.Start(Time.timeAsDouble);
            _trigger.Reached += Complete;
            _view.ContinueRequested += LoadSaveStage;
            _view.MedalRevealed += _audio.PlayMedalTick;
            _view.RankRevealed += _audio.PlayRankStamp;
        }

        /// <summary>生存中の初回ゴールだけを受け付けて結果を確定する</summary>
        /// <returns>結果画面を開始した場合はtrue</returns>
        private bool Complete()
        {
            // 死亡とクリアの競合や複数ゴールの二重演出を防ぐ
            if (_completed || _death.IsDead.CurrentValue) return false;
            _completed = true;
            var result = _tracker.Complete(Time.timeAsDouble, _rules);

            // キャプチャ前に入力と敵の接触判定を停止する
            _view.Show(result);
            _input.SetBlocked(true);
            _enemies.SetCinematicPaused(true);
            _audio.Duck();
            return true;
        }

        /// <summary>黒画面のままAddressablesのSaveStageを読み込む</summary>
        private async void LoadSaveStage()
        {
            // 二重ロードを防ぎ、次のシーンへ停止状態を持ち越さない
            if (_loading || _disposed) return;
            _loading = true;
            _view.PrepareSceneLoad();
            _audio.Restore();
            try
            {
                await _navigation.LoadGameAsync("SaveStage");
            }
            catch (Exception exception)
            {
                // カタログやバンドルの失敗時は結果画面から再試行できるようにする
                Debug.LogException(exception);
                if (_disposed) return;
                _loading = false;
                _audio.Duck();
                _view.ShowLoadFailure();
            }
        }

        /// <summary>購読と一時的な入力制限をシーン終了時に解放する</summary>
        public void Dispose()
        {
            // ロード失敗の継続処理から破棄済みビューを参照させない
            _disposed = true;
            _trigger.Reached -= Complete;
            _view.ContinueRequested -= LoadSaveStage;
            _view.MedalRevealed -= _audio.PlayMedalTick;
            _view.RankRevealed -= _audio.PlayRankStamp;
            _audio.Restore();
            if (_completed) _input.SetBlocked(false);
        }
    }
}
