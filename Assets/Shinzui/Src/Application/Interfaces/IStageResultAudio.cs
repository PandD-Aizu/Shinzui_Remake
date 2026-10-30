namespace Shinzui.Application.Interfaces
{
    /// <summary>結果画面の減音と演出音を提供する</summary>
    public interface IStageResultAudio
    {
        /// <summary>ゲーム中の効果音を減音する</summary>
        void Duck();

        /// <summary>減音前の音量へ戻す</summary>
        void Restore();

        /// <summary>獲得メダルの演出音を再生する</summary>
        void PlayMedalTick();

        /// <summary>ランク確定の演出音を再生する</summary>
        void PlayRankStamp();
    }
}
