using UnityEngine;

namespace Shinzui.Application.DTOs.Enemy
{
    /// <summary>統括AIが渡せるヒントと舞台への出入り</summary>
    public enum DirectorDirectiveType { None, HintArea, GoBackstage, EmergeAt }

    /// <summary>追跡命令を含まない統括AIの指示</summary>
    public readonly struct DirectorDirective
    {
        public DirectorDirectiveType Type { get; }
        public Vector3 Position { get; }
        public Vector3 SearchHint { get; }
        public float Radius { get; }

        /// <summary>指示と対象領域を保持</summary>
        /// <param name="type">指示の種類</param>
        /// <param name="position">ぼかした中心または出入り地点</param>
        /// <param name="radius">対象領域の半径</param>
        /// <param name="searchHint">出現後のぼかした捜索中心</param>
        public DirectorDirective(DirectorDirectiveType type, Vector3 position = default, float radius = 0f, Vector3 searchHint = default)
        {
            SearchHint = searchHint;
            Type = type;
            Position = position;
            Radius = radius;
        }
    }
}
