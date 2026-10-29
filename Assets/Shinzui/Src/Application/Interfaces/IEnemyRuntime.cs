using UnityEngine;
using Shinzui.Application.DTOs.Enemy;

namespace Shinzui.Application.Interfaces
{
    /// <summary>Engine operations supplied by Infrastructure; no View or Presenter dependency.</summary>
    public interface IEnemyRuntime
    {
        bool IsBackstage { get; }
        bool IsAvailable { get; }
        bool IsReady { get; }
        Vector3 Position { get; }
        void Tick(float deltaTime);
        /// <summary>視界内の頭、胴、足を採取</summary>
        /// <param name="playerPosition">対象の位置</param>
        /// <param name="playerHeight">対象の高さ</param>
        /// <param name="playerVelocity">対象の速度</param>
        /// <returns>可視時のみ座標を含むサンプル</returns>
        SightSample SampleSight(Vector3 playerPosition, float playerHeight, Vector3 playerVelocity);
        /// <summary>音源への遮蔽を確認</summary>
        /// <param name="position">音源位置</param>
        /// <returns>遮蔽がない場合はtrue</returns>
        bool HasLineTo(Vector3 position);
        bool CanSee(Vector3 playerPosition, float targetHeight);
        bool HasClearContact(Vector3 playerPosition, float targetHeight);
        bool MoveTo(Vector3 targetPosition);
        void Wander(float radius);
        void Stop();
        /// <summary>停止したまま目標の方向へ向く</summary>
        /// <param name="position">注視する位置</param>
        void FaceTowards(Vector3 position);
        void SetSpeed(float multiplier, bool stopped);
        void WarpZ(float z);
        /// <summary>NavMeshAgentの舞台裏状態を切り替える</summary>
        /// <param name="backstage">退避するか</param>
        void SetBackstage(bool backstage);
        /// <summary>指定したNavMesh上の点へ移動し、失敗時は非表示を維持</summary>
        /// <param name="position">検証済みの位置</param>
        /// <returns>配置に成功したか</returns>
        bool WarpTo(Vector3 position);
        /// <summary>現在地からの到達可能な経路長を取得</summary>
        /// <param name="position">到達先</param>
        /// <returns>経路がない場合はfloat.MaxValue</returns>
        float PathDistanceTo(Vector3 position);
    }
}
