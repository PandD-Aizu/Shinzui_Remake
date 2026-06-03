using System;
using System.IO;
using Shinzui.Application.Settings;
using Shinzui.Domain.Settings;
using UnityEngine;

namespace Shinzui.Infrastructure.Settings
{
    /// <summary>
    /// 設定データをPCローカルの永続化パスへ読み書きするリポジトリ実装。
    /// Nintendo Switch版など別プラットフォームでの実装切り替えを考慮しています。
    /// </summary>
    public class FileSettingsRepository : ISettingsRepository
    {
        private readonly string _saveFilePath;

        public FileSettingsRepository()
        {
            // Unityの永続データパス直下に settings.json として保存
            _saveFilePath = Path.Combine(Application.persistentDataPath, "settings.json");
        }

        /// <summary>
        /// 設定データをJSONファイルから読み込みます。
        /// ファイルが存在しない、または読み込みに失敗した場合はデフォルト設定オブジェクトを生成して返します。
        /// </summary>
        public GameSettings Load()
        {
#if UNITY_SWITCH && !UNITY_EDITOR
            // Nintendo Switch実機ビルド時は、任天堂のセーブデータAPI経由で読み込むためのダミー分岐
            // 実際はSwitch用のマウント処理・セーブ用バッファ読込ロジックをここに実装します
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
        /// 設定データをJSONファイルへシリアライズして書き込む
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
                string json = JsonUtility.ToJson(settings, true); // 読みやすさのためにインデント整形
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
            // ここではフォールバックとしてデフォルトデータを返します
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
