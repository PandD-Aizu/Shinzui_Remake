using System;

namespace Shinzui.Domain.ValueObjects.Enemy
{
    /// <summary>ロッカー以外の解放可能な行動</summary>
    [Flags]
    public enum EnemyBehaviorFlags
    {
        None = 0,
        SlowStalk = 1,
        DecoyWariness = 2,
        StrobeResistance = 4,
        SearchSideRooms = 8,
        Backtrack = 16,
        PredictiveChase = 32,
        AmbushFromAbove = 64
    }
}
