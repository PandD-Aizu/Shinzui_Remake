using System;
using System.Collections.Generic;
using Shinzui.Application.DTOs.Tunnel;
using Shinzui.Application.Interfaces.Enemy;
using UnityEngine;
using UnityEngine.AI;

namespace Shinzui.Infrastructure.Enemy
{
    /// <summary>生成マップの出入口とNavMeshから接続された候補を取得</summary>
    public sealed class NavMeshSearchPointProvider : IEnemySearchPointProvider
    {
        private readonly Func<TunnelMapDto> _mapSource;
        private readonly Func<Matrix4x4> _mapTransform;
        private readonly List<Vector3> _mapPoints = new();
        private readonly NavMeshPath _path = new();
        private TunnelMapDto _map;
        private Matrix4x4 _matrix;
        private int _sequence;
        private int _mapOffset;
        private readonly int _agentTypeId;

        /// <summary>遅延生成や再生成に追従する参照を保持</summary>
        /// <param name="mapSource">現在のマップを取得</param>
        /// <param name="mapTransform">マップのワールド変換</param>
        /// <param name="agentTypeId">使用するNavMeshのAgent種別</param>
        public NavMeshSearchPointProvider(Func<TunnelMapDto> mapSource = null,
            Func<Matrix4x4> mapTransform = null, int agentTypeId = 0)
        { _mapSource = mapSource; _mapTransform = mapTransform; _agentTypeId = agentTypeId; }

        private NavMeshQueryFilter Filter => new() { agentTypeID = _agentTypeId, areaMask = NavMesh.AllAreas };

        /// <summary>必要な時だけ生成マップの小部屋と開口部をワールド座標に変換</summary>
        private void RefreshMap()
        {
            TunnelMapDto map = _mapSource?.Invoke();
            Matrix4x4 matrix = _mapTransform?.Invoke() ?? Matrix4x4.identity;
            if (ReferenceEquals(map, _map) && matrix == _matrix) return;
            _map = map;
            _matrix = matrix;
            _mapPoints.Clear();
            _mapOffset = 0;
            if (map == null) return;

            if (map.SmallRooms != null)
                foreach (var room in map.SmallRooms)
                {
                    Vector3 center = new(room.CenterX, room.CenterY, room.CenterZ);
                    AddMapPoint(center);
                    AddMapPoint(center + new Vector3(room.DirX, room.DirY, room.DirZ) * room.RoomLength * .35f);
                }
            if (map.Tunnels != null)
                foreach (var tunnel in map.Tunnels)
                {
                    Vector3 center = new(tunnel.PositionX, tunnel.PositionY, tunnel.PositionZ);
                    if (tunnel.FloorIndex > 0) AddMapPoint(center);
                    if (tunnel.EntranceMarkers == null) continue;
                    Quaternion rotation = Quaternion.LookRotation(new Vector3(tunnel.DirX, 0f, tunnel.DirZ));
                    foreach (var marker in tunnel.EntranceMarkers)
                        if (marker.IsOpen) AddMapPoint(center + rotation * new Vector3(marker.LocalPosX, marker.LocalPosY, marker.LocalPosZ));
                }
            if (map.DropOpenings != null)
                foreach (var opening in map.DropOpenings)
                {
                    // 穴そのものではなく縁の歩行可能な床を候補にする
                    float offset = opening.Size * .5f + 1f;
                    AddMapPoint(new Vector3(opening.CenterX + offset, opening.LowerFloorY, opening.CenterZ));
                    AddMapPoint(new Vector3(opening.CenterX + offset, opening.UpperFloorY, opening.CenterZ));
                    AddMapPoint(new Vector3(opening.CenterX - offset, opening.UpperFloorY, opening.CenterZ));
                }
        }

        /// <summary>マップのローカル候補を登録</summary>
        /// <param name="point">ローカル位置</param>
        private void AddMapPoint(Vector3 point) => _mapPoints.Add(_matrix.MultiplyPoint3x4(point));

        /// <summary>マップ候補を優先し、不足分をNavMeshサンプリングで補う</summary>
        /// <param name="center">捜索中心</param>
        /// <param name="radius">捜索半径</param>
        /// <param name="includeSideRooms">マップ候補を優先するか</param>
        /// <param name="buffer">結果の格納先</param>
        /// <returns>候補数</returns>
        public int GetSearchPoints(Vector3 center, float radius, bool includeSideRooms, Span<Vector3> buffer)
        {
            RefreshMap();
            if (buffer.Length == 0 || !NavMesh.SamplePosition(center, out NavMeshHit start, 2f, Filter)) return 0;
            int count = 0;
            int mapLimit = Math.Max(1, buffer.Length / 2);
            if (includeSideRooms)
                for (int i = 0; i < _mapPoints.Count && count < mapLimit; i++)
                {
                    Vector3 candidate = _mapPoints[(i + _mapOffset) % _mapPoints.Count];
                    AddConnected(start.position, candidate, radius, buffer, ref count);
                }
            if (_mapPoints.Count > 0) _mapOffset = (_mapOffset + mapLimit) % _mapPoints.Count;

            // 独立した位相を使い、マップ生成やゲーム全体の乱数列を消費しない
            float phase = ++_sequence * .731f;
            for (int i = 0; i < 96 && count < buffer.Length; i++)
            {
                float angle = phase + i * 2.399963f;
                float distance = radius * (.2f + .8f * ((i % 13) / 12f));
                AddConnected(start.position, center + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * distance,
                    radius, buffer, ref count);
            }
            return count;
        }

        /// <summary>歩いて到達できる退出候補を取得</summary>
        /// <param name="from">現在位置</param>
        /// <param name="buffer">格納先</param>
        /// <returns>候補数</returns>
        public int GetExitPoints(Vector3 from, Span<Vector3> buffer)
            => GetSearchPoints(from, 30f, true, buffer);

        /// <summary>完全経路と範囲を満たす重複しない候補だけ格納</summary>
        /// <param name="from">経路の始点</param>
        /// <param name="candidate">候補位置</param>
        /// <param name="radius">最大距離</param>
        /// <param name="buffer">格納先</param>
        /// <param name="count">現在の格納数</param>
        private void AddConnected(Vector3 from, Vector3 candidate, float radius, Span<Vector3> buffer, ref int count)
        {
            if ((candidate - from).sqrMagnitude > radius * radius
                || !NavMesh.SamplePosition(candidate, out NavMeshHit hit, 2f, Filter)
                || (hit.position - from).sqrMagnitude > radius * radius) return;
            for (int i = 0; i < count; i++)
                if ((buffer[i] - hit.position).sqrMagnitude < 1f) return;
            if (!NavMesh.CalculatePath(from, hit.position, Filter, _path)
                || _path.status != NavMeshPathStatus.PathComplete) return;
            buffer[count++] = hit.position;
        }

        /// <summary>不可視、距離、同一歩行領域の条件を全て満たす再出現地点を探す</summary>
        /// <param name="playerPosition">プレイヤー位置</param>
        /// <param name="minDistance">最小距離</param>
        /// <param name="maxDistance">最大距離</param>
        /// <param name="includeUpperFloor">上階を許可するか</param>
        /// <param name="isVisibleToPlayer">視認判定</param>
        /// <param name="point">結果位置</param>
        /// <returns>見つかったか</returns>
        public bool TryFindEmergePoint(Vector3 playerPosition, float minDistance, float maxDistance,
            bool includeUpperFloor, Func<Vector3, bool> isVisibleToPlayer, out Vector3 point)
        {
            point = default;
            if (isVisibleToPlayer == null) return false;
            Span<Vector3> candidates = stackalloc Vector3[48];
            int count = GetSearchPoints(playerPosition, maxDistance, true, candidates);
            for (int i = 0; i < count; i++)
            {
                Vector3 candidate = candidates[i];
                float distance = Vector3.Distance(playerPosition, candidate);
                if (distance < minDistance || distance > maxDistance
                    || (!includeUpperFloor && candidate.y > playerPosition.y + 2.5f)
                    || isVisibleToPlayer(candidate)) continue;
                point = candidate;
                return true;
            }
            return false;
        }
    }
}
