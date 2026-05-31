using System;
using FMODUnity;
using Shinzui.Application.Interfaces;
using UnityEngine;

namespace Shinzui.Infrastructure.Services
{
    public class FMODVCAService : IFMODVCAService, IDisposable
    {
        private readonly IFMODSettingsRepository _settingsRepository;
        
        private FMOD.Studio.VCA _masterVCA = RuntimeManager.GetVCA("vca:/Master");
        private FMOD.Studio.VCA _bgmVCA = RuntimeManager.GetVCA("vca:/BGM");
        private FMOD.Studio.VCA _seVCA = RuntimeManager.GetVCA("vca:/SE");

        public FMODVCAService(IFMODSettingsRepository settingsRepository)
        {
            _settingsRepository = settingsRepository;
            _settingsRepository.LoadSettingsAsync();
        }
        
        /// <inheritdoc/>
        public void SetMasterVolume(float volume)
        {
            _masterVCA.setVolume(Mathf.Clamp01(volume));
            _settingsRepository.MasterVolume = volume;
            _settingsRepository.SaveSettings();
        }
        
        /// <inheritdoc/>
        public void SetBGMVolume(float volume)
        {
            _bgmVCA.setVolume(Mathf.Clamp01(volume));
            _settingsRepository.BgmVolume = volume;
            _settingsRepository.SaveSettings();
        }
        
        /// <inheritdoc/>
        public void SetSEVolume(float volume)
        {
            _seVCA.setVolume(Mathf.Clamp01(volume));
            _settingsRepository.SeVolume = volume;
            _settingsRepository.SaveSettings();
        }
        
        public float GetMasterVolume() => _settingsRepository.MasterVolume;
        public float GetBGMVolume() => _settingsRepository.BgmVolume;
        public float GetSEVolume() => _settingsRepository.SeVolume;

        public void Dispose()
        {
            _masterVCA.clearHandle();
            _bgmVCA.clearHandle();
            _seVCA.clearHandle();
        }
    }
}