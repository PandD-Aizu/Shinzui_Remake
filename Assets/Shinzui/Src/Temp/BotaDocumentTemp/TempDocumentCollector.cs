using System;
using System.Collections.Generic;

namespace Shinzui.Temp
{
    // 収集したドキュメントをセッション中だけ保持する（セーブ非対応）
    public static class TempDocumentCollector
    {
        public readonly struct DocumentRecord
        {
            public readonly string Id;
            public readonly string Title;
            public readonly string Content;

            public DocumentRecord(string id, string title, string content)
            {
                Id = id;
                Title = title;
                Content = content;
            }
        }

        private static readonly Dictionary<string, DocumentRecord> _collected = new();

        public static event Action<DocumentRecord> OnDocumentCollected;

        public static IReadOnlyCollection<DocumentRecord> CollectedDocuments => _collected.Values;

        public static int CollectedCount => _collected.Count;

        /// <summary>
        /// 指定IDのドキュメントが収集済みかどうかを返す
        /// </summary>
        public static bool IsCollected(string documentId)
        {
            return !string.IsNullOrEmpty(documentId) && _collected.ContainsKey(documentId);
        }

        /// <summary>
        /// ドキュメントを収集済みとして記録する。既に収集済みならfalseを返す
        /// </summary>
        public static bool TryCollect(string documentId, string title, string content)
        {
            if (string.IsNullOrEmpty(documentId) || _collected.ContainsKey(documentId))
            {
                return false;
            }

            var record = new DocumentRecord(documentId, title, content);
            _collected[documentId] = record;
            OnDocumentCollected?.Invoke(record);
            return true;
        }

        /// <summary>
        /// 収集状態をリセットする
        /// </summary>
        public static void ResetAll()
        {
            _collected.Clear();
        }
    }
}
