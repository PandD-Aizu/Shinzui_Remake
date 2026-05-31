namespace Shinzui.Application.Interfaces
{
    public interface IFMODVCAService
    {
        /// <summary>
        /// マスターボリュームを設定する
        /// </summary>
        /// <param name="volume">設定する音量(0 ~ 1)</param>
        public void SetMasterVolume(float volume);
        
        /// <summary>
        /// BGMボリュームを設定する
        /// </summary>
        /// <param name="volume">設定する音量(0 ~ 1)</param>
        public void SetBGMVolume(float volume);
        
        /// <summary>
        /// SEボリュームを設定する
        /// </summary>
        /// <param name="volume">設定する音量(0 ~ 1)</param>
        public void SetSEVolume(float volume);
        
        /// <summary>
        /// 現在のマスターボリュームの設定値を取得する
        /// </summary>
        /// <returns>現在のマスターボリュームの設定値</returns>
        public float GetMasterVolume();
        
        /// <summary>
        /// 現在のBGMボリュームの設定値を取得する
        /// </summary>
        /// <returns>現在のBGMボリュームの設定値</returns>
        public float GetBGMVolume();
        
        /// <summary>
        /// 現在のSEボリュームの設定値を取得する
        /// </summary>
        /// <returns>現在のSEボリュームの設定値</returns>
        public float GetSEVolume();
    }
}