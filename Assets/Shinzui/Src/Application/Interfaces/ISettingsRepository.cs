using Shinzui.Domain.Settings;

namespace Shinzui.Application.Interfaces
{
    /// <summary>
    /// 設定データの読み書きを行うリポジトリインターフェース
    /// プラットフォームに応じて実装が切り替わる
    /// </summary>
    public interface ISettingsRepository
    {
        /// <summary>
        /// 保存された設定を読み込む
        /// データが存在しない場合はデフォルト値を返す
        /// </summary>
        GameSettings Load();

        /// <summary>
        /// 設定データを保存する
        /// </summary>
        void Save(GameSettings settings);
    }
}
