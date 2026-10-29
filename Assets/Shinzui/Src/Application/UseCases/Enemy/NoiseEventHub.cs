using System.Collections.Generic;
using Shinzui.Application.DTOs.Enemy;
using Shinzui.Application.Interfaces.Enemy;

namespace Shinzui.Application.UseCases.Enemy
{
    /// <summary>フレーム内の音を全敵へ一括配信</summary>
    public sealed class NoiseEventHub : INoiseEventSink
    {
        private List<NoiseEvent> _pending = new(16);
        private List<NoiseEvent> _current = new(16);

        /// <summary>次の知覚更新に音を追加</summary>
        /// <param name="noise">音の刺激</param>
        public void Emit(NoiseEvent noise) => _pending.Add(noise);

        /// <summary>蓄積した音を取り出し、次のフレーム用バッファを空にする</summary>
        /// <returns>次回Drainまで有効な音の一覧</returns>
        public IReadOnlyList<NoiseEvent> Drain()
        {
            (_pending, _current) = (_current, _pending);
            _pending.Clear();
            return _current;
        }
    }
}
