namespace Shinzui.Application.Interfaces
{
    public interface IJsonFileCreationService
    {
        /// <summary>
        /// 文字列からJsonファイルを作成する
        /// </summary>
        /// <param name="filePath">保存先のファイルパス</param>
        /// <param name="jsonText">Json形式の文字列</param>
        public void CreateTextToJsonFile(string filePath, string jsonText);
    }
}