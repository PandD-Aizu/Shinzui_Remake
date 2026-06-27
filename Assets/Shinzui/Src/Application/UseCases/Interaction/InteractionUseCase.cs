using System;
using System.Threading.Tasks;
using R3;
using Shinzui.Application.Interfaces.Inventory;
using Shinzui.Application.UseCases.Inventory;

namespace Shinzui.Application.UseCases.Interaction
{
    /// <summary>
    /// プレイヤーのインタラクト実行およびメッセージテロップ表示、アイテム取得を仲介するユースケース
    /// </summary>
    public class InteractionUseCase
    {
        private readonly InventoryUseCase _inventoryUseCase;
        private readonly IItemCatalog _itemCatalog;
        private readonly Subject<string> _onShowMessage = new();

        /// <summary>
        /// 画面下部にメッセージテロップを表示するイベント
        /// </summary>
        public Observable<string> OnShowMessage => _onShowMessage;

        public InteractionUseCase(InventoryUseCase inventoryUseCase, IItemCatalog itemCatalog)
        {
            _inventoryUseCase = inventoryUseCase;
            _itemCatalog = itemCatalog;
        }

        /// <summary>
        /// 指定されたインタラクトIDとメッセージ内容に応じてインタラクト処理を実行
        /// </summary>
        /// <param name="interactableId">インタラクト対象のID</param>
        /// <param name="message">インスペクター等で指定された表示メッセージ</param>
        public async Task InteractAsync(string interactableId, string message)
        {
            if (string.IsNullOrEmpty(interactableId)) return;

            // メッセージ表示処理
            // インスペクターで設定されたカスタムメッセージがあれば優先表示
            if (!string.IsNullOrEmpty(message))
            {
                _onShowMessage.OnNext(message);
            }

            // 2. アイテム取得の処理
            if (interactableId.StartsWith("ItemTest_"))
            {
                string itemId = interactableId.Substring(9); // "ItemTest_"の文字数分スキップしてItemIdを取得
                
                // カタログからアイテム情報を非同期で取得
                var item = await _itemCatalog.GetItemAsync(itemId);
                
                // インベントリへアイテムを追加
                bool success = await _inventoryUseCase.AddItemAsync(itemId, 1);
                
                if (success)
                {
                    // カスタムメッセージが指定されていない場合は、デフォルトの取得メッセージを表示
                    if (string.IsNullOrEmpty(message))
                    {
                        string displayName = item != null ? item.Name : itemId;
                        _onShowMessage.OnNext($"Obtained {displayName}.");
                    }
                }
            }
            else
            {
                // 将来的な他のオブジェクトに対するインタラクトロジック拡張用のプレースホルダー
            }
        }
    }
}
