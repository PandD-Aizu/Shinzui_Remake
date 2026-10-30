using System;
using System.Collections.Generic;
using UnityEngine;
using Shinzui.Application.DTOs.Enemy;
using Shinzui.Application.Interfaces.Enemy;

namespace Shinzui.Application.UseCases.Enemy
{
    /// <summary>生成と破棄に追従するラン内のロッカー地形一覧</summary>
    public sealed class HidingSpotRegistry : IHidingSpotRegistry
    {
        private readonly Dictionary<int, HidingSpotDto> _spots = new();
        /// <inheritdoc />
        public void Register(HidingSpotDto spot) => _spots[spot.Id] = spot;
        /// <inheritdoc />
        public void Remove(int id) => _spots.Remove(id);
        /// <inheritdoc />
        public void Clear() => _spots.Clear();
        /// <inheritdoc />
        public bool TryGet(int id, out HidingSpotDto spot) => _spots.TryGetValue(id, out spot);
        /// <inheritdoc />
        public int GetWithin(Vector3 center, float radius, Span<HidingSpotDto> buffer)
        {
            int count = 0;
            foreach (var spot in _spots.Values)
            {
                if (count == buffer.Length) break;
                if ((spot.Position - center).sqrMagnitude <= radius * radius) buffer[count++] = spot;
            }
            return count;
        }
    }
}
