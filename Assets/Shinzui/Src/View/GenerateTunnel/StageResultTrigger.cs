using System;
using UnityEngine;

namespace Shinzui.View.GenerateTunnel
{
    /// <summary>生成ゴールの接触をシーン内の結果Presenterへ渡す</summary>
    [DisallowMultipleComponent]
    public sealed class StageResultTrigger : MonoBehaviour
    {
        public event Func<bool> Reached;

        /// <summary>結果表示の開始を依頼する</summary>
        /// <returns>接続先が開始を受け付けた場合はtrue</returns>
        public bool TryComplete()
        {
            // 配線が完了するまでゴールの再接触を許可する
            return Reached?.Invoke() == true;
        }
    }
}
