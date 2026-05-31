using System.IO;
using Shinzui.Application.Interfaces;

namespace Shinzui.Infrastructure.Services
{
    public class JsonCreationService : IJsonFileCreationService
    {
        /// <inheritdoc />
        public void CreateTextToJsonFile(string filePath, string jsonText)
        {
            if (CheckJsonText(jsonText) == false)
            {
                UnityEngine.Debug.LogError($"[JsonCreator] Invalid Json Text: {jsonText}]");
                return;
            }
            
            File.WriteAllText(filePath, jsonText);
        }

        /// <summary>
        /// 文字列がJson形式かチェックする
        /// </summary>
        /// <param name="text">チェックする文字列</param>
        /// <returns>Json形式: true</returns>
        private bool CheckJsonText(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                UnityEngine.Debug.LogError($"[JsonCreator] Invalid Json Text: {text}]");
                return false;
            }

            var trimmed = text.Trim();
            return (trimmed.StartsWith("{") && trimmed.EndsWith("}")) ||
                   (trimmed.StartsWith("[") && trimmed.EndsWith("]"));
        }
    }
}