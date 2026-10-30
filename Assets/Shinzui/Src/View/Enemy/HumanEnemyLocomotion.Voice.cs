using System.IO;
using UnityEngine;

namespace Shinzui.View
{
    public sealed partial class HumanEnemyLocomotion
    {
        [SerializeField, Min(1f)] private float screamIntervalMin = 8f;
        [SerializeField, Min(1f)] private float screamIntervalMax = 12f;
        [SerializeField, Range(0f, 1f)] private float screamVolume = .65f;
        private float _screamTimer = -1f;
        private FMOD.Sound _screamSound;
        private FMOD.Channel _screamChannel;
        private FMOD.Studio.Bus _screamBus;
        private bool _screamBusLocked;

        /// <summary>通常行動中に間隔を空けて叫び、再生位置を敵へ追従</summary>
        /// <param name="deltaTime">フレーム経過時間</param>
        /// <param name="active">通常行動中で音声を再生できるか</param>
        public void TickVoice(float deltaTime, bool active)
        {
            if (!active || !isActiveAndEnabled)
            {
                ReleaseVoice();
                return;
            }

            // 初回と各再生の間隔を分散し、複数の敵が同時に叫ぶのを防ぐ
            if (_screamTimer < 0f) ResetVoiceTimer();
            _screamTimer -= deltaTime;
            if (_screamTimer <= 0f)
            {
                bool playing = false;
                if (_screamChannel.hasHandle()) _screamChannel.isPlaying(out playing);
                if (!playing) PlayVoice();
                ResetVoiceTimer();
            }

            // 3D音声の発生位置を頭付近へ合わせる
            if (_screamChannel.hasHandle())
            {
                var position = FMODUnity.RuntimeUtils.ToFMODVector(_head != null ? _head.position : transform.position);
                var velocity = new FMOD.VECTOR();
                _screamChannel.set3DAttributes(ref position, ref velocity);
            }
        }

        /// <summary>次の叫びまでの時間を設定</summary>
        private void ResetVoiceTimer()
        {
            _screamTimer = Random.Range(Mathf.Max(1f, screamIntervalMin), Mathf.Max(screamIntervalMin, screamIntervalMax));
        }

        /// <summary>既存の叫び声をSEバス経由の3D音声として再生</summary>
        private void PlayVoice()
        {
            var system = FMODUnity.RuntimeManager.CoreSystem;
            if (!_screamSound.hasHandle())
            {
                var result = system.createSound(Path.Combine(UnityEngine.Application.streamingAssetsPath,
                    "EventEnemy", "MonstrousScream.mp3"), FMOD.MODE._3D | FMOD.MODE.CREATESAMPLE, out _screamSound);
                if (result != FMOD.RESULT.OK) { Debug.LogError("Human enemy voice: " + result, this); return; }
            }

            // SE音量設定に従い、距離に応じて叫び声を減衰
            FMOD.ChannelGroup group = default;
            var studio = FMODUnity.RuntimeManager.StudioSystem;
            if (!_screamBusLocked && studio.getBus("bus:/SE", out _screamBus) == FMOD.RESULT.OK
                && _screamBus.lockChannelGroup() == FMOD.RESULT.OK)
            {
                _screamBusLocked = true;
                studio.flushCommands();
            }
            if (_screamBusLocked) _screamBus.getChannelGroup(out group);
            system.playSound(_screamSound, group, true, out _screamChannel);
            var position = FMODUnity.RuntimeUtils.ToFMODVector(_head != null ? _head.position : transform.position);
            var velocity = new FMOD.VECTOR();
            _screamChannel.set3DAttributes(ref position, ref velocity);
            _screamChannel.set3DMinMaxDistance(2f, 30f);
            _screamChannel.setVolume(screamVolume);
            _screamChannel.setPaused(false);
        }

        /// <summary>舞台裏への移動や捕獲演出で叫び声とバス参照を解放</summary>
        private void ReleaseVoice()
        {
            if (_screamChannel.hasHandle()) { _screamChannel.stop(); _screamChannel.clearHandle(); }
            if (_screamSound.hasHandle()) { _screamSound.release(); _screamSound.clearHandle(); }
            if (_screamBusLocked) { _screamBus.unlockChannelGroup(); _screamBusLocked = false; }
            _screamTimer = -1f;
        }

        /// <summary>無効化や破棄で音声を停止</summary>
        private void OnDisable() => ReleaseVoice();
    }
}
