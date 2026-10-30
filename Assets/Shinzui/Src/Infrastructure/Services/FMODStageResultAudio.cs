using FMODUnity;
using Shinzui.Application.Interfaces;

namespace Shinzui.Infrastructure.Services
{
    /// <summary>既存のSEバスとVCAを一時的に減音する</summary>
    public sealed class FMODStageResultAudio : IStageResultAudio
    {
        private FMOD.Studio.Bus _bus;
        private FMOD.Studio.VCA _vca;
        private float _volume;
        private bool _ducked;
        private bool _usesVca;

        /// <summary>効果音の現在値を保存して減音する</summary>
        public void Duck()
        {
            // 生成トンネル音も含むVCAを優先し、なければ既存のSEバスを使う
            if (_ducked || !RuntimeManager.IsInitialized) return;
            _usesVca = RuntimeManager.StudioSystem.getVCA("vca:/SE", out _vca) == FMOD.RESULT.OK
                && _vca.isValid() && _vca.getVolume(out _volume) == FMOD.RESULT.OK;
            if (_usesVca)
                _ducked = _vca.setVolume(_volume * 0.15f) == FMOD.RESULT.OK;
            else if (RuntimeManager.StudioSystem.getBus("bus:/SE", out _bus) == FMOD.RESULT.OK
                && _bus.isValid() && _bus.getVolume(out _volume) == FMOD.RESULT.OK)
                _ducked = _bus.setVolume(_volume * 0.15f) == FMOD.RESULT.OK;
        }

        /// <summary>画面終了時に元の効果音量へ戻す</summary>
        public void Restore()
        {
            // 再試行や破棄から複数回呼ばれても音量を変えない
            if (!_ducked) return;
            if (_usesVca && _vca.isValid()) _vca.setVolume(_volume);
            else if (_bus.isValid()) _bus.setVolume(_volume);
            _ducked = false;
        }

        /// <summary>メダル獲得音の割り当て口を提供する</summary>
        public void PlayMedalTick()
        {
            // 対応するFMODイベントが未登録のため無音とする
        }

        /// <summary>ランク確定音の割り当て口を提供する</summary>
        public void PlayRankStamp()
        {
            // 対応するFMODイベントが未登録のため無音とする
        }
    }
}
