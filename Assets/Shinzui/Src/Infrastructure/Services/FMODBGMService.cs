using System;
using System.Collections.Generic;
using FMOD.Studio;
using FMODUnity;
using Shinzui.Application.Interfaces;

namespace Shinzui.Infrastructure.Services
{
    public class FMODBGMService : IFMODBGMService, IDisposable
    {
        private readonly Dictionary<string, EventInstance> _bgmInstances = new Dictionary<string, EventInstance>();

        /// <inheritdoc/>
        public void PlayBGM(EventReference eventReference, string key = null)
        {
            if (eventReference.IsNull)
            {
                UnityEngine.Debug.LogError("[FMODBGMService] Invalid EventReference.");
                return;
            }

            key ??= eventReference.Guid.ToString();

            if (_bgmInstances.ContainsKey(key))
            {
                UnityEngine.Debug.LogWarning($"[FMODBGMService] Already playing BGM with key: {key}");
                return;
            }

            try
            {
                var instance = RuntimeManager.CreateInstance(eventReference);
                instance.start();
                _bgmInstances[key] = instance;
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError($"[FMODBGMService] Failed to play BGM: {eventReference}");
                UnityEngine.Debug.LogError(e);
            }
        }

        /// <inheritdoc/>
        public void StopBGM(string key, bool allowFadeOut = true)
        {
            if (string.IsNullOrEmpty(key) || !_bgmInstances.TryGetValue(key, out var instance))
            {
                UnityEngine.Debug.LogWarning($"[FMODBGMService] BGM with key not found: {key}");
                return;
            }

            var stopMode = allowFadeOut ? FMOD.Studio.STOP_MODE.ALLOWFADEOUT : FMOD.Studio.STOP_MODE.IMMEDIATE;
            instance.stop(stopMode);
            instance.release();
            instance.clearHandle();
            _bgmInstances.Remove(key);
        }

        /// <inheritdoc/>
        public void StopAllBGM(bool allowFadeOut = true)
        {
            var keys = new List<string>(_bgmInstances.Keys);
            foreach (var k in keys)
            {
                StopBGM(k, allowFadeOut);
            }
        }

        /// <inheritdoc/>
        public void PauseBGM(string key)
        {
            if (string.IsNullOrEmpty(key) || !_bgmInstances.TryGetValue(key, out var instance))
            {
                UnityEngine.Debug.LogWarning($"[FMODBGMService] BGM with key not found: {key}");
                return;
            }

            instance.setPaused(true);
        }

        /// <inheritdoc/>
        public void ResumeBGM(string key)
        {
            if (string.IsNullOrEmpty(key) || !_bgmInstances.TryGetValue(key, out var instance))
            {
                UnityEngine.Debug.LogWarning($"[FMODBGMService] BGM with key not found: {key}");
                return;
            }

            instance.setPaused(false);
        }

        /// <inheritdoc/>
        public void SwitchBGM(string oldKey, EventReference newEventReference, bool allowFadeOut = true)
        {
            StopBGM(oldKey, allowFadeOut);
            PlayBGM(newEventReference);
        }

        /// <inheritdoc/>
        public void SetBGMParameter(string key, string parameterName, float value)
        {
            if (string.IsNullOrEmpty(key))
            {
                UnityEngine.Debug.LogError($"[FMODBGMService] Invalid key: {key}");
                return;
            }

            if (string.IsNullOrEmpty(parameterName))
            {
                UnityEngine.Debug.LogError($"[FMODBGMService] Invalid parameter name: {parameterName}");
                return;
            }

            if (_bgmInstances.TryGetValue(key, out var instance))
            {
                instance.setParameterByName(parameterName, value);
            }
            else
            {
                UnityEngine.Debug.LogError($"[FMODBGMService] BGM with key not found: {key}");
            }
        }

        /// <inheritdoc/>
        public bool IsBGMPlaying(string key)
        {
            if (string.IsNullOrEmpty(key) || !_bgmInstances.TryGetValue(key, out var instance))
            {
                return false;
            }

            if (!instance.isValid())
            {
                return false;
            }

            instance.getPlaybackState(out PLAYBACK_STATE playbackState);
            return playbackState != PLAYBACK_STATE.STOPPED;
        }

        public void Dispose()
        {
            StopAllBGM(false);
        }
    }
}