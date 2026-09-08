using Shinzui.Application.Interfaces.Inventory;

/// <summary>
/// ゲームデータのセーブに含めたい情報を保持するクラス
/// </summary>
public class GameSaveData
{
    public InventorySaveData InventorySaveData;
    public SpecialItemSaveData SpecialItemSaveData;
}
