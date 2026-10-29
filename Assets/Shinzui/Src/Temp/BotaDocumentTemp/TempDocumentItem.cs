using UnityEngine;
using Shinzui.View.Interaction;

namespace Shinzui.Temp
{
    /// <summary>
    /// マップに置くドキュメントアイテム
    /// </summary>
    public class TempDocumentItem : InteractableComponent
    {
        public override bool RequiresHold => true;
        [Header("Document Config")]
        [SerializeField] private string documentId = "document_001"; // 重複収集防止用ID
        [SerializeField] private string documentTitle = "### 初期メッセージ(要変更😊) ###";

        public string DocumentId => documentId;

        /// <summary>
        /// インタラクト時にドキュメントを収集済みとして記録する
        /// </summary>
        public override void ExecuteInteractEffect()
        {
            base.ExecuteInteractEffect();

            bool isNew = TempDocumentCollector.TryCollect(documentId, documentTitle, InteractMessage);
            if (!isNew)
            {
                Debug.Log($"[TempDocumentItem] Document '{documentId}' was already collected.");
            }
        }
    }
}
