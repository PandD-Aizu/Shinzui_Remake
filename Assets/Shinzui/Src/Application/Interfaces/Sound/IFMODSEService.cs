using FMODUnity;

namespace Shinzui.Application.Interfaces
{
    public interface IFMODSEService
    {
        /// <summary>
        /// 指定したSEを再生する
        /// </summary>
        /// <param name="eventReference">再生したいFMODイベントリファレンス</param>
        /// <param name="key">Dictionary にて定義する key（defaultはGuidの文字列）</param>
        public void PlaySE(EventReference eventReference, string key = null);

        /// <summary>
        /// 指定したSEを一度だけ再生する
        /// </summary>
        /// <param name="eventReference">再生したいFMODイベントリファレンス</param>
        public void PlayOneShot(EventReference eventReference);

        /// <summary>
        /// 指定したSEを停止する
        /// </summary>
        /// <param name="key">停止したいSEのkey</param>
        /// <param name="allowFadeOut">true: フェードアウトを許可する</param>
        public void StopSE(string key, bool allowFadeOut = true);

        /// <summary>
        /// すべてのSEを停止する
        /// </summary>
        /// <param name="allowFadeOut">true: フェードアウトを許可する</param>
        public void StopAllSE(bool allowFadeOut = true);

        /// <summary>
        /// 指定したSEを一時停止する
        /// </summary>
        /// <param name="key">一時停止したいSEのkey</param>
        public void PauseSE(string key);

        /// <summary>
        /// 指定したSEを再開する
        /// </summary>
        /// <param name="key">再開したいSEのkey</param>
        public void ResumeSE(string key);

        /// <summary>
        /// 指定したSEの指定したパラメータを変更する
        /// </summary>
        /// <param name="key">パラメータを変更したいSEのkey</param>
        /// <param name="parameterName">変更したいパラメータ名</param>
        /// <param name="value">変更値</param>
        public void SetSEParameter(string key, string parameterName, float value);

        /// <summary>
        /// 指定したSEの音量を変更する
        /// </summary>
        /// <param name="key">音量を変更したいSEのkey</param>
        /// <param name="volume">変更値（0 ~ 1）</param>
        public void SetSEVolume(string key, float volume);

        /// <summary>
        /// 指定したSEが再生中かをチェック
        /// </summary>
        /// <param name="key">チェックするSEのkey</param>
        /// <returns>再生中: true</returns>
        public bool IsSEPlaying(string key);
    }
}