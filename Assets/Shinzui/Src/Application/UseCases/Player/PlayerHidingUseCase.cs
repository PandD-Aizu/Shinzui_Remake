using Shinzui.Application.DTOs.Enemy;
using Shinzui.Application.Interfaces.Enemy;
using Shinzui.Domain.ValueObjects.Player;

namespace Shinzui.Application.UseCases.Player
{
    /// <summary>隠れ状態、入る瞬間の目撃猶予、呼吸とノイズを管理</summary>
    public sealed class PlayerHidingUseCase
    {
        private readonly IHidingSpotRegistry _registry;
        private readonly INoiseEventSink _noise;
        private float _closing;
        public BreathGauge Breath { get; } = new();
        public int CurrentSpotId { get; private set; }
        public bool IsHidden => CurrentSpotId != 0;
        public bool IsEntering => IsHidden && _closing > 0f;
        public bool IsHoldingBreath => IsHidden && Breath.IsHolding;
        /// <summary>地形一覧と音の発行先を受け取る</summary>
        /// <param name="registry">ロッカー一覧</param>
        /// <param name="noise">音の発行先</param>
        public PlayerHidingUseCase(IHidingSpotRegistry registry, INoiseEventSink noise)
        { _registry = registry; _noise = noise; }
        /// <summary>ドアを閉めるまでの目撃猶予を付けて隠れる</summary>
        /// <param name="id">入るロッカー</param>
        /// <param name="wasRunning">走って入ったか</param>
        /// <returns>入れたか</returns>
        public bool Enter(int id, bool wasRunning)
        {
            if (IsHidden || id == 0 || !_registry.TryGet(id, out var spot)) return false;
            CurrentSpotId = id;
            _closing = .6f;
            if (wasRunning) _noise.Emit(new NoiseEvent(NoiseKind.Door, spot.Position, 8f));
            return true;
        }
        /// <summary>退出音を出して隠れ状態を解除</summary>
        public void Exit()
        {
            if (_registry.TryGet(CurrentSpotId, out var spot)) _noise.Emit(new NoiseEvent(NoiseKind.Door, spot.Position, 4f));
            CurrentSpotId = 0;
            _closing = 0f;
        }
        /// <summary>呼吸とドアの目撃猶予を進める</summary>
        /// <param name="hold">息止め入力</param>
        /// <param name="dt">経過秒数</param>
        public void Tick(bool hold, float dt)
        {
            if (dt <= 0f) return;
            _closing = System.Math.Max(0f, _closing - dt);
            if (Breath.Tick(IsHidden && hold, dt) && _registry.TryGet(CurrentSpotId, out var spot))
                _noise.Emit(new NoiseEvent(NoiseKind.Breath, spot.Position, 6f));
        }
    }
}
