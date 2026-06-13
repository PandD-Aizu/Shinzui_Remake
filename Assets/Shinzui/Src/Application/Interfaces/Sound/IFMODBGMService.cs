using FMODUnity;

namespace Shinzui.Application.Interfaces
{
    public interface IFMODBGMService
    {
        /// <summary>
        /// BGMを再生する
        /// </summary>
        /// <param name="eventReference">再生したいFMODイベントリファレンス</param>
        /// <param name="key">Dictionary にて定義する key（defaultはGuidの文字列）</param>
        public void PlayBGM(EventReference eventReference, string key = null);

        /// <summary>
        /// BGMを停止する
        /// </summary>
        /// <param name="key">停止したいBGMのkey</param>
        /// <param name="allowFadeOut">true: フェードアウトを許可する</param>
        public void StopBGM(string key, bool allowFadeOut = true);

        /// <summary>
        /// すべてのBGMを停止する
        /// </summary>
        /// <param name="allowFadeOut">true: フェードアウトを許可する</param>
        public void StopAllBGM(bool allowFadeOut = true);

        /// <summary>
        /// 指定したBGMを一時停止する
        /// </summary>
        /// <param name="key">一時停止したいBGMのkey</param>
        public void PauseBGM(string key);

        /// <summary>
        /// 指定したBGMを再開する
        /// </summary>
        /// <param name="key">再開したいBGMのkey</param>
        public void ResumeBGM(string key);

        /// <summary>
        /// 指定したBGMを別のBGMに切り替える
        /// </summary>
        /// <param name="oldKey">停止したいBGMのkey</param>
        /// <param name="newEventReference">再生したいFMODイベントリファレンス</param>
        /// <param name="allowFadeOut">true: フェードアウトを許可する</param>
        public void SwitchBGM(string oldKey, EventReference newEventReference, bool allowFadeOut = true);

        /// <summary>
        /// 指定したBGMの指定したパラメータを変更する
        /// </summary>
        /// <param name="key">パラメータを変更したいBGMのkey</param>
        /// <param name="parameterName">変更したいパラメータ名</param>
        /// <param name="value">変更値</param>
        public void SetBGMParameter(string key, string parameterName, float value);

        /// <summary>
        /// 指定したBGMが再生中かをチェック
        /// </summary>
        /// <param name="key">チェックするBGMのkey</param>
        /// <returns>再生中: true</returns>
        public bool IsBGMPlaying(string key);
    }
}