using UnityEngine;

namespace Shinzui.View.Interaction
{
    /// <summary>中央のボタンと時計回りに満ちる長押しゲージを描画</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class HoldProgressGraphic : UnityEngine.UI.MaskableGraphic
    {
        private float _progress;
        private bool _completed;

        /// <summary>長押し進捗と成立時の明るさを反映</summary>
        /// <param name="progress">0から1までの進捗</param>
        /// <param name="completed">操作が成立したか</param>
        public void SetProgress(float progress, bool completed)
        {
            progress = Mathf.Clamp01(progress);
            if (Mathf.Approximately(_progress, progress) && _completed == completed) return;
            _progress = progress;
            _completed = completed;
            SetVerticesDirty();
        }

        /// <summary>背景の陰影、中央ボタン、外周の進捗を生成</summary>
        /// <param name="vh">描画メッシュの構築先</param>
        protected override void OnPopulateMesh(UnityEngine.UI.VertexHelper vh)
        {
            vh.Clear();
            Vector2 center = rectTransform.rect.center;
            float radius = Mathf.Min(rectTransform.rect.width, rectTransform.rect.height) * .5f;

            // 中央も外周と同じ進捗で時計回りに白く満たす
            Color empty = new Color(.42f, .45f, .47f, 1f);
            Color filled = new Color(.85f, .88f, .89f, 1f);
            AddBand(vh, center, 0f, radius * .48f, empty, empty, 1f);
            AddBand(vh, center, 0f, radius * .48f, filled, filled, _progress);

            // 中央のボタンから暗い縁へ陰影を付ける
            AddBand(vh, center, radius * .48f, radius * .70f, new Color(.55f, .58f, .60f, .95f), new Color(.24f, .26f, .27f, .92f), 1f);
            AddBand(vh, center, radius * .70f, radius * .90f, new Color(.24f, .26f, .27f, .92f), new Color(.04f, .05f, .05f, .9f), 1f);

            // 未達成部分を灰色で残し、12時の位置から白い外周を伸ばす
            AddBand(vh, center, radius * .90f, radius, new Color(.23f, .25f, .26f, .95f), new Color(.23f, .25f, .26f, .95f), 1f);
            Color foreground = _completed ? Color.white : new Color(.95f, .97f, .97f, 1f);
            AddBand(vh, center, radius * .90f, radius, foreground, foreground, _progress);
        }

        /// <summary>任意の割合の円弧を内周と外周で構成</summary>
        /// <param name="vh">頂点の出力先</param>
        /// <param name="center">円の中心</param>
        /// <param name="inner">内周半径</param>
        /// <param name="outer">外周半径</param>
        /// <param name="innerColor">内周色</param>
        /// <param name="outerColor">外周色</param>
        /// <param name="fraction">時計回りの描画割合</param>
        private static void AddBand(UnityEngine.UI.VertexHelper vh, Vector2 center, float inner, float outer,
            Color innerColor, Color outerColor, float fraction)
        {
            if (fraction <= 0f) return;
            int segments = Mathf.Max(1, Mathf.CeilToInt(96f * fraction));
            int start = vh.currentVertCount;
            for (int i = 0; i <= segments; i++)
            {
                float angle = i / (float)segments * fraction * Mathf.PI * 2f;
                Vector2 direction = new Vector2(Mathf.Sin(angle), Mathf.Cos(angle));
                vh.AddVert(center + direction * outer, outerColor, Vector2.zero);
                vh.AddVert(center + direction * inner, innerColor, Vector2.zero);
                if (i == segments) continue;
                int vertex = start + i * 2;
                vh.AddTriangle(vertex, vertex + 1, vertex + 2);
                vh.AddTriangle(vertex + 2, vertex + 1, vertex + 3);
            }
        }
    }
}
