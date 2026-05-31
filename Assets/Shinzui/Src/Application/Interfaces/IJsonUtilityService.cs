using Cysharp.Threading.Tasks;

namespace Shinzui.Application.Interfaces
{
    public interface IJsonUtilityService
    {
        /// <summary>
        /// Jsonファイルを読み込んで任意のオブジェクトに変換する
        /// </summary>
        /// <param name="addressableJsonKey">Addressableのキー</param>
        /// <typeparam name="T">変換するオブジェクトの型</typeparam>
        /// <returns>変換したオブジェクト</returns>
        public UniTask<T> ConvertJsonToAnyObjectAsync<T>(string addressableJsonKey);

        /// <summary>
        /// 任意のオブジェクトをJson形式の文字列に変換する
        /// </summary>
        /// <param name="obj">任意のオブジェクト</param>
        /// <param name="prettyPrint">インデントを整えるかどうか</param>
        /// <typeparam name="T">任意の型</typeparam>
        /// <returns>Json形式の文字列</returns>
        public string ConvertAnyObjectToJsonAsync<T>(T obj, bool prettyPrint = false);

        /// <summary>
        /// 生のJson文字列を任意のオブジェクトに変換する
        /// </summary>
        /// <param name="jsonText">Json形式の文字列</param>
        /// <typeparam name="T">変換するオブジェクトの型</typeparam>
        /// <returns>変換したオブジェクト</returns>
        public T ConvertRawJsonToAnyObject<T>(string jsonText);
    }
}