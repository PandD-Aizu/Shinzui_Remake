using UnityEngine;

namespace Shinzui.View.Interaction
{
    /// <summary>
    /// 背景が透ける細い円形マーカーを描画
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class InteractionRingGraphic : UnityEngine.UI.MaskableGraphic
    {
        [SerializeField] private Color shadowColor = new Color(0f, 0f, 0f, 0.4f);
        [SerializeField] private Vector2 shadowOffset = new Vector2(0.75f, -1f);
        [SerializeField, Min(0f)] private float shadowSoftness = 2f;

        /// <summary>
        /// 円周に沿って一定の太さのリングを生成
        /// </summary>
        /// <param name="vh">描画メッシュの構築先</param>
        protected override void OnPopulateMesh(UnityEngine.UI.VertexHelper vh)
        {
            vh.Clear();
            Rect rect = rectTransform.rect;
            float outer = Mathf.Min(rect.width, rect.height) * 0.5f;
            float inner = Mathf.Max(0f, outer - 1.5f);

            // 円の背後に中心から両端へ透過する柔らかい影を配置
            Vector2 shadowCenter = rect.center + shadowOffset;
            Color shadow = shadowColor;
            shadow.a *= color.a;
            Color transparent = new Color(shadow.r, shadow.g, shadow.b, 0f);
            AddRing(vh, shadowCenter, Mathf.Max(0f, inner - shadowSoftness), inner, transparent, shadow);
            AddRing(vh, shadowCenter, inner, outer, shadow, shadow);
            AddRing(vh, shadowCenter, outer, outer + shadowSoftness, shadow, transparent);

            // 影より手前に白い円を描画
            AddRing(vh, rect.center, inner, outer, color, color);
        }

        /// <summary>
        /// 内周と外周の色を補間したリングを追加
        /// </summary>
        /// <param name="vh">描画メッシュの構築先</param>
        /// <param name="center">円の中心</param>
        /// <param name="inner">内周の半径</param>
        /// <param name="outer">外周の半径</param>
        /// <param name="innerColor">内周の色</param>
        /// <param name="outerColor">外周の色</param>
        private static void AddRing(UnityEngine.UI.VertexHelper vh, Vector2 center,
            float inner, float outer, Color innerColor, Color outerColor)
        {
            const int segments = 64;
            int start = vh.currentVertCount;

            // 内周と外周をつなぎ、中心部分を透明に保つ
            for (int i = 0; i < segments; i++)
            {
                float angle = i * Mathf.PI * 2f / segments;
                Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                vh.AddVert(center + direction * outer, outerColor, Vector2.zero);
                vh.AddVert(center + direction * inner, innerColor, Vector2.zero);
                int current = start + i * 2;
                int next = start + (i + 1) % segments * 2;
                vh.AddTriangle(current, next, current + 1);
                vh.AddTriangle(next, next + 1, current + 1);
            }
        }
    }
}
