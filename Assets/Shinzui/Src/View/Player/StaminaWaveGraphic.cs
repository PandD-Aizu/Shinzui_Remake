using UnityEngine;
using UnityEngine.UI;

namespace Shinzui.View
{
    /// <summary>スタミナ残量を細い発光波形として描画するuGUIグラフィック</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class StaminaWaveGraphic : MaskableGraphic
    {
        private float _ratio, _trail, _time, _amplitude, _activity, _recovery, _flash, _warning;
        private Color _tint = Color.white;
        private Slider.Direction _direction;

        /// <summary>波形の描画状態を更新</summary>
        /// <param name="ratio">実残量の割合</param>
        /// <param name="trail">消費残像の割合</param>
        /// <param name="time">演出の経過秒数</param>
        /// <param name="amplitude">波の振幅</param>
        /// <param name="activity">消費演出の強さ</param>
        /// <param name="recovery">回復演出の強さ</param>
        /// <param name="flash">満タン時の発光量</param>
        /// <param name="warning">残量警告のしきい値</param>
        /// <param name="tint">既存Fillとアイテム効果の色</param>
        /// <param name="direction">Sliderの充填方向</param>
        public void SetState(float ratio, float trail, float time, float amplitude, float activity,
            float recovery, float flash, float warning, Color tint, Slider.Direction direction)
        {
            _ratio = Mathf.Clamp01(ratio);
            _trail = Mathf.Clamp(trail, _ratio, 1f);
            _time = time;
            _amplitude = amplitude;
            _activity = activity;
            _recovery = recovery;
            _flash = flash;
            _warning = warning;
            _tint = tint;
            _direction = direction;
            SetVerticesDirty();
        }

        /// <summary>下地、消費残像、発光する波形を順に描画</summary>
        /// <param name="mesh">uGUIの頂点バッファ</param>
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            Rect rect = rectTransform.rect;
            if (rect.width <= 0f || rect.height <= 0f) return;

            // 空になっても全長と残量警告を読み取れる下地を残す
            float low = _warning > 0f ? 1f - Mathf.Clamp01(_ratio / _warning) : 0f;
            float pulse = 0.5f + 0.5f * Mathf.Sin(_time * 9f);
            Color baseColor = Color.Lerp(new Color(0.48f, 0.43f, 0.26f, 0.45f),
                new Color(1f, 0.28f, 0.08f, 0.55f + pulse * 0.25f), low);
            DrawStrip(mesh, 0f, 1f, 1f, baseColor, false);
            DrawStrip(mesh, _ratio, _trail, 3f, new Color(1f, 0.48f, 0.12f, 0.55f), false);

            // 柔らかい外光と明るい芯を同じ波形に重ねる
            Color gold = new Color(1f, 0.84f, 0.28f, 1f) * _tint;
            gold = Color.Lerp(gold, new Color(1f, 0.28f, 0.08f, _tint.a), low * 0.75f);
            gold = Color.Lerp(gold, new Color(1f, 0.98f, 0.72f, _tint.a), _flash * 0.65f);
            Color glow = gold;
            glow.a *= 0.12f + _activity * 0.06f + low * pulse * 0.12f;
            DrawStrip(mesh, 0f, _ratio, 9f, glow, true);
            glow.a *= 2f;
            DrawStrip(mesh, 0f, _ratio, 5f, glow, true);
            DrawStrip(mesh, 0f, _ratio, 2f + low * pulse, gold, true);

            // 回復中は左から先端へ光を走らせ、残量の先端を常に読みやすくする
            if (_ratio > 0f)
            {
                float sweep = Mathf.Repeat(_time * 0.65f, 1.25f);
                Color light = new Color(1f, 1f, 0.85f, _tint.a);
                if (_recovery > 0.01f)
                {
                    light.a *= _recovery * 0.75f;
                    DrawStrip(mesh, Mathf.Clamp(sweep - 0.08f, 0f, _ratio),
                        Mathf.Min(sweep, _ratio), 3f, light, true);
                }

                light.a = _tint.a * (0.75f + _flash * 0.25f);
                float length = IsVertical() ? rect.height : rect.width;
                DrawStrip(mesh, Mathf.Max(0f, _ratio - 3f / length), _ratio, 4f, light, true);
            }
        }

        /// <summary>指定した残量範囲へ連続した帯状メッシュを追加</summary>
        /// <param name="mesh">描画先</param>
        /// <param name="start">帯の開始割合</param>
        /// <param name="end">帯の終了割合</param>
        /// <param name="thickness">帯の太さ</param>
        /// <param name="tint">頂点色</param>
        /// <param name="wave">波形変位を適用するか</param>
        private void DrawStrip(VertexHelper mesh, float start, float end, float thickness, Color tint, bool wave)
        {
            if (end <= start) return;

            Rect rect = rectTransform.rect;
            bool vertical = IsVertical();
            float length = vertical ? rect.height : rect.width;
            int segments = Mathf.Clamp(Mathf.CeilToInt((end - start) * length / 5f), 1, 180);
            int first = mesh.currentVertCount;
            Vector2 normal = vertical ? Vector2.right : Vector2.up;

            // 頂点を共有して毎フレームの配列生成を避ける
            for (int i = 0; i <= segments; i++)
            {
                float t = Mathf.Lerp(start, end, (float)i / segments);
                float position = (_direction == Slider.Direction.RightToLeft ||
                    _direction == Slider.Direction.TopToBottom) ? 1f - t : t;
                Vector2 center = vertical
                    ? new Vector2(rect.center.x, Mathf.Lerp(rect.yMin, rect.yMax, position))
                    : new Vector2(Mathf.Lerp(rect.xMin, rect.xMax, position), rect.center.y);
                if (wave) center += normal * WaveOffset(t);
                mesh.AddVert(center - normal * (thickness * 0.5f), tint, Vector2.zero);
                mesh.AddVert(center + normal * (thickness * 0.5f), tint, Vector2.zero);
                if (i == 0) continue;

                int index = first + i * 2;
                mesh.AddTriangle(index - 2, index - 1, index);
                mesh.AddTriangle(index, index - 1, index + 1);
            }
        }

        /// <summary>両端を固定した緩やかな進行波を算出</summary>
        /// <param name="position">ゲージ全長に対する位置</param>
        /// <returns>中心線からの変位</returns>
        private float WaveOffset(float position)
        {
            float envelope = Mathf.Sin(Mathf.Clamp01(position / Mathf.Max(_ratio, 0.001f)) * Mathf.PI);
            float wave = Mathf.Sin(position * 24f - _time * 5f)
                + 0.35f * Mathf.Sin(position * 47f + _time * 7f);
            return wave * envelope * _amplitude * (0.3f + _activity * 0.7f + _recovery * 0.2f);
        }

        /// <summary>縦方向のSliderかを判定</summary>
        /// <returns>縦方向ならtrue</returns>
        private bool IsVertical()
        {
            return _direction == Slider.Direction.BottomToTop || _direction == Slider.Direction.TopToBottom;
        }
    }
}
