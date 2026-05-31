using Cysharp.Threading.Tasks;

namespace Shinzui.Application.Interfaces
{
    public interface IFMODSettingsRepository
    {
        public float MasterVolume { get; set; }
        public float BgmVolume { get; set; }
        public float SeVolume { get; set; }
        
        /// <summary>
        /// 現在の設定値をjsonファイルにセーブ
        /// </summary>
        public void SaveSettings();
        
        /// <summary>
        /// jsonファイルから設定値をロードしてプロパティに反映
        /// </summary>
        public UniTaskVoid LoadSettingsAsync();
    }
}