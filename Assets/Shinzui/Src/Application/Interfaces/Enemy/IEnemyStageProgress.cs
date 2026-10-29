namespace Shinzui.Application.Interfaces.Enemy
{
    /// <summary>マップ内の上下位置ではなくランの進行階層</summary>
    public interface IEnemyStageProgress
    {
        int FloorNumber { get; }
    }
}
