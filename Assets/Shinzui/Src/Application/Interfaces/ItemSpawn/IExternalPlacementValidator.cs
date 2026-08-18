using Shinzui.Domain.ValueObjects.ItemSpawn;

namespace Shinzui.Application.Interfaces.ItemSpawn
{
    /// <summary>
    /// ドメイン層外（Unity PhysicsのOverlap等やカスタム禁止領域）での配置妥当性検証を行うインターフェース。
    /// </summary>
    public interface IExternalPlacementValidator
    {
        bool ValidateLocation(
            SpawnVector3 position,
            SpawnVector3 normal,
            ItemSpawnTarget target,
            out string failureReason);
    }
}
