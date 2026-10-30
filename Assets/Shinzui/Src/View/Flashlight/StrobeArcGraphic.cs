using UnityEngine;
using UnityEngine.UI;

namespace Shinzui.View.Flashlight
{
    /// <summary>ストロボの電池残量を発光する円弧として描画するuGUIグラフィック</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class StrobeArcGraphic : MaskableGraphic
    {
        [Header("形状")]
        [SerializeField, Min(0f)] private float innerRadius = 79f;
        [SerializeField, Min(0f)] private float outerRadius = 101f;

        [Tooltip("ゲージ0の位置の角度（ローカル座標、度）")]
        [SerializeField] private float startAngle = 0f;

        [Tooltip("ゲージ最大の位置の角度（ローカル座標、度）")]
        [SerializeField] private float endAngle = 180f;

        [Header("演出")]
        [SerializeField, Min(0f)] private float waveAmplitude = 2.2f;
        [SerializeField, Range(0f, 1f)] private float warningThreshold = 0.2f;
        [SerializeField] private Color costColorA = new(1f, 0.35f, 0.1f, 1f);
        [SerializeField] private Color costColorB = new(1f, 0.85f, 0.6f, 1f);

        private float _remaining, _energy, _trail, _time, _activity, _flash;

        /// <summary>円弧の描画状態を更新</summary>
        /// <param name="remaining">発光後に残る量</param>
        /// <param name="energy">現在の電池残量</param>
        /// <param name="trail">消費残像の量</param>
        /// <param name="time">演出の経過秒数</param>
        /// <param name="activity">チャージ演出の強さ</param>
        /// <param name="flash">電池交換時の発光量</param>
        public void SetState(float remaining, float energy, float trail, float time, float activity, float flash)
        {
            _remaining = Mathf.Clamp01(remaining);
            _energy = Mathf.Clamp(energy, _remaining, 1f);
            _trail = Mathf.Clamp(trail, _energy, 1f);
            _time = time;
            _activity = activity;
            _flash = flash;
            SetVerticesDirty();
        }

        /// <summary>下地、消費予定と残像、発光する残量を順に描画</summary>
        /// <param name="mesh">uGUIの頂点バッファ</param>
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            float mid = (innerRadius + outerRadius) * 0.5f;

            // 空になっても全長と残量警告を読み取れる下地を残す
            float low = warningThreshold > 0f ? 1f - Mathf.Clamp01(_energy / warningThreshold) : 0f;
            float pulse = 0.5f + 0.5f * Mathf.Sin(_time * 9f);
            Color track = Color.Lerp(new Color(0.48f, 0.43f, 0.26f, 0.3f),
                new Color(1f, 0.28f, 0.08f, 0.35f + pulse * 0.25f), low);
            DrawBand(mesh, 0f, 1f, innerRadius, outerRadius, track, track, false);

            // 消費予定は点滅させ、発光後は残像として遅れて縮める
            if (_trail > _remaining)
            {
                bool previewing = _energy > _remaining;
                Color cost = previewing
                    ? Color.Lerp(costColorA, costColorB, 0.5f + 0.5f * Mathf.Sin(_time * 12f))
                    : new Color(1f, 0.48f, 0.12f, 0.6f);
                DrawBand(mesh, _remaining, _trail, innerRadius, outerRadius, cost, cost, false);
            }

            if (_remaining <= 0f) return;

            // 残量の少なさと電池交換に応じて金色を変える
            Color gold = new Color(1f, 0.84f, 0.28f, 1f);
            gold = Color.Lerp(gold, new Color(1f, 0.28f, 0.08f, 1f), low * 0.75f);
            gold = Color.Lerp(gold, new Color(1f, 0.98f, 0.72f, 1f), _flash * 0.65f);
            Color deep = new Color(gold.r * 0.72f, gold.g * 0.6f, gold.b * 0.4f, 1f);

            // 帯の外側へ柔らかく漏れる外光
            Color glow = gold;
            glow.a = 0.18f + _activity * 0.22f + low * pulse * 0.15f + _flash * 0.3f;
            Color clear = glow;
            clear.a = 0f;
            DrawBand(mesh, 0f, _remaining, innerRadius - 7f, mid, clear, glow, false);
            DrawBand(mesh, 0f, _remaining, mid, outerRadius + 7f, glow, clear, false);

            // 内側を濃く外側を明るくした本体と、揺れる発光の芯
            DrawBand(mesh, 0f, _remaining, innerRadius, outerRadius, deep, gold, false);
            Color core = new Color(1f, 0.97f, 0.75f, 0.85f);
            DrawBand(mesh, 0f, _remaining, mid - 1.2f - low * pulse, mid + 1.2f + low * pulse, core, core, true);

            // 始端から先端へ光を走らせる
            float sweep = Mathf.Repeat(_time * 0.55f, 1.4f) - 0.2f;
            Color light = new Color(1f, 1f, 0.9f, 0.28f + _activity * 0.2f);
            DrawBand(mesh, Mathf.Clamp(sweep - 0.07f, 0f, _remaining), Mathf.Clamp(sweep, 0f, _remaining),
                innerRadius, outerRadius, light, light, false);

            // 残量の先端を常に読みやすくする
            float tip = 3f / ArcLength(mid);
            Color tipColor = new Color(1f, 1f, 0.85f, 0.8f + _flash * 0.2f);
            DrawBand(mesh, Mathf.Max(0f, _remaining - tip), _remaining, innerRadius - 2f, outerRadius + 2f,
                tipColor, tipColor, false);
        }

        /// <summary>指定したゲージ範囲へ円弧状の帯メッシュを追加</summary>
        /// <param name="mesh">描画先</param>
        /// <param name="start">帯の開始割合</param>
        /// <param name="end">帯の終了割合</param>
        /// <param name="radius0">内側の半径</param>
        /// <param name="radius1">外側の半径</param>
        /// <param name="color0">内側の頂点色</param>
        /// <param name="color1">外側の頂点色</param>
        /// <param name="wave">波形変位を適用するか</param>
        private void DrawBand(VertexHelper mesh, float start, float end, float radius0, float radius1,
            Color color0, Color color1, bool wave)
        {
            if (end <= start) return;

            int segments = Mathf.Clamp(Mathf.CeilToInt((end - start) * ArcLength(radius1) / 4f), 1, 180);
            int first = mesh.currentVertCount;

            // 頂点を共有して毎フレームの配列生成を避ける
            for (int i = 0; i <= segments; i++)
            {
                float t = Mathf.Lerp(start, end, (float)i / segments);
                float angle = Mathf.Lerp(startAngle, endAngle, t) * Mathf.Deg2Rad;
                Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                float offset = wave ? WaveOffset(t) : 0f;
                mesh.AddVert(direction * (radius0 + offset), color0, Vector2.zero);
                mesh.AddVert(direction * (radius1 + offset), color1, Vector2.zero);
                if (i == 0) continue;

                int index = first + i * 2;
                mesh.AddTriangle(index - 2, index - 1, index);
                mesh.AddTriangle(index, index - 1, index + 1);
            }
        }

        /// <summary>両端を固定した緩やかな進行波を算出</summary>
        /// <param name="position">ゲージ全長に対する位置</param>
        /// <returns>中心線からの半径方向の変位</returns>
        private float WaveOffset(float position)
        {
            float envelope = Mathf.Sin(Mathf.Clamp01(position / Mathf.Max(_remaining, 0.001f)) * Mathf.PI);
            float wave = Mathf.Sin(position * 24f - _time * 5f)
                + 0.35f * Mathf.Sin(position * 47f + _time * 7f);
            return wave * envelope * waveAmplitude * (0.35f + _activity * 0.65f);
        }

        /// <summary>指定半径でのゲージ全長を求める</summary>
        /// <param name="radius">半径</param>
        /// <returns>円弧の長さ</returns>
        private float ArcLength(float radius)
        {
            return Mathf.Max(1f, radius * Mathf.Abs(endAngle - startAngle) * Mathf.Deg2Rad);
        }
    }
}
