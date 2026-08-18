using System;
using UnityEngine;

namespace Shinzui.View.GenerateTunnel
{
    public enum GeneratedCorridorKind
    {
        Normal,
        Warp
    }

    /// <summary>
    /// 生成後の通路種別と、ワープ通路の対応相手を保持する識別用コンポーネント。
    /// 純粋なView層のコンポーネントであり、他層への依存を持たない。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GeneratedCorridorInfo : MonoBehaviour
    {
        [SerializeField] private GeneratedCorridorKind kind;
        [SerializeField] private int warpPairId = -1;
        [SerializeField] private GeneratedCorridorInfo pairedCorridor;

        public GeneratedCorridorKind Kind => kind;
        public int WarpPairId => warpPairId;
        public GeneratedCorridorInfo PairedCorridor => pairedCorridor;

        /// <summary>
        /// 通路の種別とワープペアIDを初期化する。
        /// </summary>
        public void Initialize(GeneratedCorridorKind corridorKind, int pairId = -1)
        {
            kind = corridorKind;
            warpPairId = pairId;
        }

        /// <summary>
        /// このワープ通路と対になる通路情報を登録する。
        /// </summary>
        public void SetPair(GeneratedCorridorInfo pair)
        {
            pairedCorridor = pair;
        }
    }
}
