using System.IO;
using Cysharp.Threading.Tasks;
using Shinzui.Application.Interfaces;
using Shinzui.Infrastructure.DTOs;
using UnityEngine;

namespace Shinzui.Infrastructure.Repositories
{
    public class FMODSettingsRepository : IFMODSettingsRepository
    {
        private readonly IJsonUtilityService _jsonUtilityService;
        private readonly IJsonFileCreationService _jsonCreator;
        
        private const string SettingsFileName = "FMODSettings.json";
        private string filePath = Path.Combine(UnityEngine.Application.persistentDataPath, SettingsFileName);
        
        public float MasterVolume { get; set; } = 1.0f;
        public float BgmVolume { get; set; } = 1.0f;
        public float SeVolume { get; set; } = 1.0f;

        public FMODSettingsRepository(
            IJsonUtilityService jsonUtilityService,
            IJsonFileCreationService jsonCreator)
        {
            _jsonUtilityService = jsonUtilityService;
            _jsonCreator = jsonCreator;
            
            LoadSettingsAsync().Forget();
        }

        /// <inheritdoc/>
        public void SaveSettings()
        {
            var settingsData = new FMODSettingsData
            {
                MasterVolume = MasterVolume,
                BgmVolume = BgmVolume,
                SeVolume = SeVolume
            };

            string jsonText = _jsonUtilityService.ConvertAnyObjectToJsonAsync(settingsData);
            _jsonCreator.CreateTextToJsonFile(filePath, jsonText);
        }

        /// <inheritdoc/>
        public UniTaskVoid LoadSettingsAsync()
        {
            if (!File.Exists(filePath))
            {
                Debug.LogWarning($"[FMODSettingsRepository] Settings file not found: {filePath}");
                return default;
            }

            try
            {
                string jsonText = File.ReadAllText(filePath);
                var settingsData = _jsonUtilityService.ConvertRawJsonToAnyObject<FMODSettingsData>(jsonText);
                if (settingsData == null)
                {
                    Debug.LogError($"[FMODSettingsRepository] Failed to parse settings JSON: {filePath}");
                    return default;
                }
                
                MasterVolume = settingsData.MasterVolume;
                BgmVolume = settingsData.BgmVolume;
                SeVolume = settingsData.SeVolume;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[FMODSettingsRepository] Failed to load settings from file: {filePath}");
                Debug.LogError(e);
            }

            return default;
        }
    }
}