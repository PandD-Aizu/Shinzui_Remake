namespace Shinzui.Application.DTOs.Enemy
{
    /// <summary>
    /// 統括AIが個別Enemyへ渡す命令の種類
    /// </summary>
    public enum EnemyCommandType
    {
        Idle,               // 待機
        Wander,             // 探索
        ChasePlayer,        // プレイヤー追跡
        InvestigatePosition, // 位置調査
        Retreat             // プレイヤーから離れて緊張を下げる
    }
}
