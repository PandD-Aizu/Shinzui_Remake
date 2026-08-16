namespace Shinzui.Domain.DomainServices.ItemSpawn
{
    /// <summary>
    /// アイテム配置前の妥当性検証インターフェース（拡張可能）。
    /// 距離制限、禁止領域、障害物判定などをプラグイン可能にする。
    /// </summary>
    public interface ISpawnPositionValidator
    {
        bool ValidatePosition(in SpawnValidationContext context, out string failReason);
    }
}
