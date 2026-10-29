using System;
using Shinzui.Application.DTOs.Enemy;
using Shinzui.Application.Interfaces.Enemy;
using Shinzui.View;
using Shinzui.View.Interaction;
using UnityEngine;
using VContainer.Unity;

namespace Shinzui.Presentation
{
    /// <summary>動的生成される音源のイベントをシーン内の聴覚へ接続</summary>
    public sealed class EnemyNoisePresenter : IInitializable, IDisposable
    {
        private readonly INoiseEventSink _sink;
        private readonly PlayerView _player;

        /// <summary>音の転送先と対象シーンを設定</summary>
        /// <param name="sink">ノイズ受付</param>
        /// <param name="player">対象プレイヤー</param>
        public EnemyNoisePresenter(INoiseEventSink sink, PlayerView player)
        {
            _sink = sink;
            _player = player;
        }

        /// <summary>生成タイミングに依存しない音源通知を購読</summary>
        public void Initialize()
        {
            StoneCollisionHandler.Impacted += OnImpact;
            FireAlarm.Rang += OnAlarm;
        }

        /// <summary>石の着弾を聴覚へ転送</summary>
        /// <param name="source">音源</param>
        /// <param name="position">着弾位置</param>
        /// <param name="direction">飛来方向</param>
        private void OnImpact(StoneCollisionHandler source, Vector3 position, Vector3 direction)
        {
            if (source.gameObject.scene != _player.gameObject.scene) return;
            _sink.Emit(new NoiseEvent(NoiseKind.Impact, position, 10f, direction));
        }

        /// <summary>警報音を聴覚へ転送</summary>
        /// <param name="source">音源</param>
        /// <param name="position">鳴動位置</param>
        private void OnAlarm(FireAlarm source, Vector3 position)
        {
            if (source.gameObject.scene != _player.gameObject.scene) return;
            _sink.Emit(new NoiseEvent(NoiseKind.Alarm, position, 30f));
        }

        /// <summary>シーン終了時に静的イベントの購読を解除</summary>
        public void Dispose()
        {
            StoneCollisionHandler.Impacted -= OnImpact;
            FireAlarm.Rang -= OnAlarm;
        }
    }
}
