using System;
using Shinzui.View;
using UnityEngine;
using VContainer.Unity;

namespace Shinzui.Presentation
{
    /// <summary>
    /// 毎フレームプレイヤーのコライダーとゲートコライダーの交差を判定し、
    /// 平面跨ぎ（符号付き距離の反転）を検出した瞬間にシームレスに逆側のゲートへワープさせるPresenter。
    /// </summary>
    public class TunnelLoopPresenter : IInitializable, ITickable, IDisposable
    {
        private readonly PlayerView _playerView;
        
        // 連続ワープ（チャタリング）防止のためのクールダウン時間（秒）
        private const float WarpCooldown = 0.15f;
        private float _lastWarpTime;
        private Vector3 _prevCameraPosition;

        public TunnelLoopPresenter(PlayerView playerView)
        {
            _playerView = playerView;
            Debug.Log("[TunnelLoopPresenter] Constructor called");
        }

        public void Initialize()
        {
            Debug.Log("[TunnelLoopPresenter] Initialize called");
            if (_playerView != null)
            {
                _prevCameraPosition = _playerView.CameraPosition;
            }
        }

        public void Tick()
        {
            var playerCollider = _playerView.PlayerCollider;
            if (playerCollider == null)
            {
                return;
            }

            // 物理トランスフォームの同期を強制し、コライダーの bounds を最新位置に更新する
            Physics.SyncTransforms();

            // アクティブなすべてのトンネルゲートを巡回して判定
            for (int i = 0; i < TunnelGateView.ActiveGates.Count; i++)
            {
                var gate = TunnelGateView.ActiveGates[i];
                if (gate == null || gate.Collider == null || gate.TargetGate == null)
                {
                    continue;
                }

                // 1. 当たり判定（コライダーのバウンズ）に入っているか判定
                if (playerCollider.bounds.Intersects(gate.Collider.bounds))
                {
                    EvaluateAndWarp(gate);
                    break; // 同一フレームでのワープ処理は1回のみにするためループを抜ける
                }
            }

            // 次のフレームのために位置を保存
            _prevCameraPosition = _playerView.CameraPosition;
        }

        private void EvaluateAndWarp(TunnelGateView gate)
        {
            if (Time.time - _lastWarpTime < WarpCooldown)
            {
                return;
            }

            var targetGate = gate.TargetGate;
            Vector3 gateCenter = gate.transform.position;
            if (gate.Collider is BoxCollider box)
            {
                gateCenter = gate.transform.TransformPoint(box.center);
            }

            Vector3 gateForward = gate.transform.forward;

            // プレイヤーの現在カメラ位置と前フレームのカメラ位置
            Vector3 currPos = _playerView.CameraPosition;
            Vector3 prevPos = _prevCameraPosition;

            // ゲート平面に対する前フレームと現フレームの符号付き距離（投影距離）
            float dPrev = Vector3.Dot(prevPos - gateCenter, gateForward);
            float dCurr = Vector3.Dot(currPos - gateCenter, gateForward);

            // 跨ぎ（符号の反転）を検出
            bool crossed = (dPrev < 0.0f && dCurr >= 0.0f) || (dPrev > 0.0f && dCurr <= 0.0f);

            if (crossed)
            {
                // 基本のワープ移動量（ゲート間の位置の差分）
                Vector3 offset = targetGate.transform.position - gate.transform.position;

                // プレイヤーのワープ実行（ルートオブジェクトが移動し、子であるカメラも移動する）
                _playerView.Warp(offset);

                // 過去位置も同じオフセットで同期（チャタリング防止の肝）
                _prevCameraPosition += offset;

                _lastWarpTime = Time.time;
            }
        }

        public void Dispose()
        {
            // リソース解放処理
        }
    }
}
