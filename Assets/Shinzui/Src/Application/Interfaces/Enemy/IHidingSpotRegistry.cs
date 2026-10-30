using System;
using UnityEngine;
using Shinzui.Application.DTOs.Enemy;

namespace Shinzui.Application.Interfaces.Enemy
{
    /// <summary>占有状態を公開しない隠れ場所の一覧</summary>
    public interface IHidingSpotRegistry
    {
        /// <summary>隠れ場所の地形情報を登録</summary>
        /// <param name="spot">登録する地形</param>
        void Register(HidingSpotDto spot);

        /// <summary>破棄された隠れ場所を削除</summary>
        /// <param name="id">識別子</param>
        void Remove(int id);

        /// <summary>一覧を初期化</summary>
        void Clear();

        /// <summary>識別子から地形情報を取得</summary>
        /// <param name="id">識別子</param>
        /// <param name="spot">取得した地形</param>
        /// <returns>登録済みか</returns>
        bool TryGet(int id, out HidingSpotDto spot);

        /// <summary>周辺の地形情報をバッファへ格納</summary>
        /// <param name="center">探索の中心</param>
        /// <param name="radius">探索半径</param>
        /// <param name="buffer">格納先</param>
        /// <returns>格納した件数</returns>
        int GetWithin(Vector3 center, float radius, Span<HidingSpotDto> buffer);
    }
}
