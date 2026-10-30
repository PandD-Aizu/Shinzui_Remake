using Shinzui.Application.DTOs.Enemy;
using Shinzui.Application.Interfaces.Enemy;
using Shinzui.Domain.Entities;

namespace Shinzui.Application.UseCases.Player
{
    /// <summary>隠れ状態、入る瞬間の目撃猶予、スタミナを使う息止めとノイズを管理</summary>
    public sealed class PlayerHidingUseCase
    {
        // 手でドアを開けて入り、内側から閉め切るまでの演出時間
        private const float EntryDuration = 2f;
        private readonly IHidingSpotRegistry _registry;
        private readonly INoiseEventSink _noise;
        private readonly PlayerEntity _player;
        private float _closing;
        private bool _holding;
        public int CurrentSpotId { get; private set; }
        public bool IsHidden => CurrentSpotId != 0;
        public bool IsEntering => IsHidden && _closing > 0f;
        public bool IsHoldingBreath => IsHidden && _player.IsHoldingBreath;
        /// <summary>地形一覧、音の発行先、スタミナを持つプレイヤーを受け取る</summary>
        /// <param name="registry">ロッカー一覧</param>
        /// <param name="noise">音の発行先</param>
        /// <param name="player">息止めで消費するスタミナの持ち主</param>
        public PlayerHidingUseCase(IHidingSpotRegistry registry, INoiseEventSink noise, PlayerEntity player)
        { _registry = registry; _noise = noise; _player = player; }
        /// <summary>ドアを閉めるまでの目撃猶予を付けて隠れる</summary>
        /// <param name="id">入るロッカー</param>
        /// <param name="wasRunning">走って入ったか</param>
        /// <returns>入れたか</returns>
        public bool Enter(int id, bool wasRunning)
        {
            if (IsHidden || id == 0 || !_registry.TryGet(id, out var spot)) return false;
            CurrentSpotId = id;
            _closing = EntryDuration;
            if (wasRunning) _noise.Emit(new NoiseEvent(NoiseKind.Door, spot.Position, 8f));
            return true;
        }
        /// <summary>退出音を出して隠れ状態を解除</summary>
        public void Exit()
        {
            if (_registry.TryGet(CurrentSpotId, out var spot)) _noise.Emit(new NoiseEvent(NoiseKind.Door, spot.Position, 4f));
            CurrentSpotId = 0;
            _closing = 0f;
            _holding = _player.SetHoldingBreath(false);
        }
        /// <summary>息止めの要求とドアの目撃猶予を進め、スタミナ切れの息継ぎを音にする</summary>
        /// <param name="hold">息止め入力</param>
        /// <param name="dt">経過秒数</param>
        public void Tick(bool hold, float dt)
        {
            if (dt <= 0f) return;
            _closing = System.Math.Max(0f, _closing - dt);

            // 息止め中にスタミナが尽きた瞬間だけ、こらえきれず息を吐く
            bool holding = _player.SetHoldingBreath(IsHidden && hold);
            if (_holding && !holding && _player.IsExhausted.Value && _registry.TryGet(CurrentSpotId, out var spot))
                _noise.Emit(new NoiseEvent(NoiseKind.Breath, spot.Position, 6f));
            _holding = holding;
        }
    }
}
