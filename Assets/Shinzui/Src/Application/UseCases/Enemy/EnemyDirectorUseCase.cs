using System;
using Shinzui.Application.Interfaces;
using Shinzui.Application.DTOs.Enemy;
using Shinzui.Domain.ValueObjects.Enemy;
using Shinzui.Domain.ValueObjects.FMOD;
using UnityEngine;

namespace Shinzui.Application.UseCases.Enemy
{
    /// <summary>
    /// ゲーム全体を監視して個別Enemyへ命令を割り当てる統括AI
    /// </summary>
    public class EnemyDirectorUseCase
    {
        private readonly IFMODSEService _seService;
        private EnemyCommand[] _commands = Array.Empty<EnemyCommand>();
        private bool _wasPlayerFound;

        public EnemyDirectorUseCase(IFMODSEService seService)
        {
            _seService = seService;
        }

        /// <summary>
        /// 現在のゲーム状態からEnemyごとの命令を決定する
        /// </summary>
        /// <param name="worldState">統括AIが参照するゲーム全体の状態</param>
        /// <returns>EnemyのIdに対応する命令配列</returns>
        public EnemyCommand[] DecideCommands(EnemyWorldState worldState)
        {
            ReadOnlySpan<EnemyReport> reports = worldState.EnemyReports.Span;
            EnsureCommandCapacity(reports.Length);

            for (int i = 0; i < reports.Length; i++)
            {
                _commands[i] = EnemyCommand.Wander;
            }

            int chaseEnemyIndex = FindNearestEnemyInChaseRange(worldState.PlayerPosition, reports);
            bool isPlayerFound = chaseEnemyIndex >= 0;
            if (isPlayerFound && !_wasPlayerFound)
            {
                _seService.PlayOneShot(FMODEventPath.SE_ON_ENEMY_FOUND.Reference);
            }

            _wasPlayerFound = isPlayerFound;

            if (chaseEnemyIndex >= 0)
            {
                int enemyId = reports[chaseEnemyIndex].Id;
                if (enemyId >= 0 && enemyId < _commands.Length)
                {
                    _commands[enemyId] = EnemyCommand.ChasePlayer(worldState.PlayerPosition);
                }
            }

            return _commands;
        }

        /// <summary>
        /// 命令配列の容量をEnemy数に合わせる
        /// </summary>
        /// <param name="enemyCount">現在管理しているEnemy数</param>
        private void EnsureCommandCapacity(int enemyCount)
        {
            if (_commands.Length == enemyCount)
            {
                return;
            }

            _commands = new EnemyCommand[enemyCount];
        }

        /// <summary>
        /// 追跡可能範囲内で最もプレイヤーに近いEnemyを探す
        /// </summary>
        /// <param name="playerPosition">プレイヤー位置</param>
        /// <param name="reports">Enemy状態報告</param>
        /// <returns>対象Enemyの配列インデックス</returns>
        private static int FindNearestEnemyInChaseRange(Vector3 playerPosition, ReadOnlySpan<EnemyReport> reports)
        {
            int nearestIndex = -1;
            float nearestSqrDistance = float.MaxValue;

            for (int i = 0; i < reports.Length; i++)
            {
                EnemyReport report = reports[i];
                if (!report.CanReceiveCommand || report.ChaseDistance <= 0f)
                {
                    continue;
                }

                float sqrDistance = (report.Position - playerPosition).sqrMagnitude;
                float chaseSqrDistance = report.ChaseDistance * report.ChaseDistance;
                if (sqrDistance > chaseSqrDistance || sqrDistance >= nearestSqrDistance)
                {
                    continue;
                }

                nearestSqrDistance = sqrDistance;
                nearestIndex = i;
            }

            return nearestIndex;
        }
    }
}
