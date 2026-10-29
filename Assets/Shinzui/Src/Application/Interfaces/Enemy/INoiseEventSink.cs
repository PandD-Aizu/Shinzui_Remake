using Shinzui.Application.DTOs.Enemy;

namespace Shinzui.Application.Interfaces.Enemy
{
    /// <summary>音源から敵の聴覚へ刺激を渡す窓口</summary>
    public interface INoiseEventSink
    {
        /// <summary>ノイズを発行</summary>
        /// <param name="noise">音の刺激</param>
        void Emit(NoiseEvent noise);
    }
}
