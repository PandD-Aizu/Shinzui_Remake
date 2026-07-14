using System;
using Shinzui.Application.UseCases.Enemy;
using Shinzui.View;
using UnityEngine;
using R3;
using VContainer.Unity;

namespace Shinzui.Presentation
{
    /// <summary>
    /// 敵に見つかった瞬間に画面エフェクトを一瞬強く適用し、
    /// 見つかり続けている間は弱めに適用し、見失ったら減衰させて消すPresenter。
    /// </summary>
    public class HorrorDetectionPresenter : IInitializable, ITickable, IDisposable
    {
        private readonly EnemyDirectorUseCase _enemyDirectorUseCase;
        private readonly HorrorDetectionView _view;
        private readonly CompositeDisposable _disposables = new();

        // エフェクトの強度設定値
        private const float StrongIntensity = 0.8f;      // 発見された瞬間（一瞬だけ）の最大強度
        private const float WeakIntensity = 0.02f;       // 発見中（見つかり続けている間）の維持強度
        private const float FadeToWeakSpeed = 2.0f;      // 強いノイズから弱いノイズへ減衰する速度（1秒あたり減衰量）
        private const float FadeToZeroSpeed = 0.001f;      // 見失った後にゼロへ減衰する速度（1秒あたり減衰量）

        private float _currentIntensity = 0f;
        private bool _isPlayerFound = false;

        public HorrorDetectionPresenter(
            EnemyDirectorUseCase enemyDirectorUseCase,
            HorrorDetectionView view)
        {
            _enemyDirectorUseCase = enemyDirectorUseCase;
            _view = view;
        }

        public void Initialize()
        {
            // 初期化時に一度エフェクトをクリア
            _view.UpdateEffect(0f);

            // 敵全体の発見ステータスを監視
            _enemyDirectorUseCase.IsPlayerFound
                .Subscribe(found =>
                {
                    if (found && !_isPlayerFound)
                    {
                        // 敵に発見された瞬間！一瞬だけエフェクトの強度を最大にする
                        _currentIntensity = StrongIntensity;
                    }
                    _isPlayerFound = found;
                })
                .AddTo(_disposables);
        }

        public void Tick()
        {
            float deltaTime = Time.deltaTime;

            if (_isPlayerFound)
            {
                // 見つかっている間：
                // 瞬間的な強ノイズ(StrongIntensity)から、徐々に維持用ノイズ(WeakIntensity)へと近づける
                if (_currentIntensity > WeakIntensity)
                {
                    _currentIntensity = Mathf.MoveTowards(_currentIntensity, WeakIntensity, deltaTime * FadeToWeakSpeed);
                }
                else
                {
                    // プレイヤーの視界外などから見つかった場合などで現在の強度が低い場合は、弱ノイズへと上昇/補間させる
                    _currentIntensity = Mathf.MoveTowards(_currentIntensity, WeakIntensity, deltaTime * FadeToWeakSpeed);
                }
            }
            else
            {
                // 見失った場合：
                // エフェクトをゼロに向けてフェードアウトさせる
                _currentIntensity = Mathf.MoveTowards(_currentIntensity, 0f, deltaTime * FadeToZeroSpeed);
            }

            // Viewへ適用
            _view.UpdateEffect(_currentIntensity);
        }

        public void Dispose()
        {
            _disposables.Dispose();
        }
    }
}
