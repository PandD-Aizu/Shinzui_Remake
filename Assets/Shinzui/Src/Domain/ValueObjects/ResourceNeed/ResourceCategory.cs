namespace Shinzui.Domain.ValueObjects.ResourceNeed
{
    public enum ResourceCategory
    {
        Healing,        // 回復・HP関連
        Ammo,           // 弾薬（所持武器に応じた弾薬）
        LightResource,  // ライト・バッテリー・光源リソース
        Weapon,         // 武器本体・装備
        Utility,        // ユーティリティ・探索補助（スタミナ剤、石、消耗品等）
        Rare            // 希少・特殊アイテム（お守り、鍵等）
    }
}
