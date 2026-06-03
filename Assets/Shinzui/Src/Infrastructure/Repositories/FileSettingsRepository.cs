using System;
using System.IO;
using Shinzui.Application.Interfaces;
using Shinzui.Domain.Settings;
using UnityEngine;

namespace Shinzui.Infrastructure.Repositories
{
    /// <summary>
    /// 設定データをPCローカルの永続化パスへ読み書きするリポジトリ実装
    /// Nintendo Switch版など別プラットフォームでの実装切り替えを考慮しとく
    /// </summary>
    public class FileSettingsRepository : ISettingsRepository
    {
        private readonly string _saveFilePath;

        public FileSettingsRepository()
        {
            // Unityの永続データパス直下に settings.json として保存
            _saveFilePath = Path.Combine(UnityEngine.Application.persistentDataPath, "settings.json");
        }

        /// <summary>
        /// 設定データをJSONファイルから読み込み
        /// ファイルが存在しない、または読み込みに失敗した場合はデフォルト設定オブジェクトを生成して返す
        /// </summary>
        public GameSettings Load()
        {
#if UNITY_SWITCH && !UNITY_EDITOR
            // Nintendo Switch実機ビルド時は、任天堂のセーブデータAPI経由で読み込むためのダミー分岐
            // 実際はSwitch用のマウント処理・セーブ用バッファ読込ロジックをここに実装
            Debug.Log("[Settings] Nintendo Switch save data read requested.");
            return LoadForSwitch();
#else
            try
            {
                if (File.Exists(_saveFilePath))
                {
                    string json = File.ReadAllText(_saveFilePath);
                    var settings = JsonUtility.FromJson<GameSettings>(json);
                    if (settings != null)
                    {
                        Debug.Log($"[Settings] Loaded settings from: {_saveFilePath}");
                        return settings;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Settings] Failed to load settings from {_saveFilePath}: {ex.Message}");
            }

            // ロード失敗時、または初回起動時はデフォルト設定を返す
            Debug.Log("[Settings] No saved settings found. Initializing with default values.");
            var defaultSettings = new GameSettings();
            defaultSettings.ResetToDefault();
            return defaultSettings;
#endif
        }

        /// <summary>
        /// 設定データをJSONファイルへシリアライズして書き込み
        /// </summary>
        public void Save(GameSettings settings)
        {
#if UNITY_SWITCH && !UNITY_EDITOR
            // Nintendo Switch実機ビルド時のセーブ処理
            Debug.Log("[Settings] Nintendo Switch save data write requested.");
            SaveForSwitch(settings);
#else
            try
            {
                string json = JsonUtility.ToJson(settings, true);
                string directory = Path.GetDirectoryName(_saveFilePath);
                if (directory != null && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                File.WriteAllText(_saveFilePath, json);
                Debug.Log($"[Settings] Saved settings to: {_saveFilePath}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Settings] Failed to save settings to {_saveFilePath}: {ex.Message}");
            }
#endif
        }

#if UNITY_SWITCH && !UNITY_EDITOR
        private GameSettings LoadForSwitch()
        {
            // Switchの実機セーブデータAPI（nn::fs）を呼び出すコードを想定
            // ここではフォールバックとしてデフォルトデータを返す
            var settings = new GameSettings();
            settings.ResetToDefault();
            return settings;
        }

        private void SaveForSwitch(GameSettings settings)
        {
            // nn::fs::OpenFile などを経由してSwitchのユーザーセーブデータ領域に保存する処理
        }
#endif
    }
}
