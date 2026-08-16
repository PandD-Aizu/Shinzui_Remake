using System;
using System.Collections.Generic;

namespace Shinzui.Domain.ValueObjects.ResourceNeed
{
    /// <summary>
    /// サンプリングされたキーフレーム点群による区分線形補間カーブ評価器
    /// UnityのAnimationCurveなどを純粋POCOとしてDomain層で扱えるようにする
    /// </summary>
    public class SampledPiecewiseCurveEvaluator : ICurveEvaluator
    {
        public readonly struct KeyPoint
        {
            public float Time { get; }
            public float Value { get; }

            public KeyPoint(float time, float value)
            {
                Time = time;
                Value = value;
            }
        }

        private readonly KeyPoint[] _points;

        public SampledPiecewiseCurveEvaluator(IEnumerable<KeyPoint> points)
        {
            var list = new List<KeyPoint>(points ?? Array.Empty<KeyPoint>());
            if (list.Count == 0)
            {
                _points = new[] { new KeyPoint(0f, 2f), new KeyPoint(0.5f, 1f), new KeyPoint(1f, 0.5f) };
            }
            else
            {
                list.Sort((a, b) => a.Time.CompareTo(b.Time));
                _points = list.ToArray();
            }
        }

        public SampledPiecewiseCurveEvaluator(float[] sampledValues)
        {
            if (sampledValues == null || sampledValues.Length == 0)
            {
                _points = new[] { new KeyPoint(0f, 2f), new KeyPoint(0.5f, 1f), new KeyPoint(1f, 0.5f) };
                return;
            }

            if (sampledValues.Length == 1)
            {
                _points = new[] { new KeyPoint(0f, sampledValues[0]), new KeyPoint(1f, sampledValues[0]) };
                return;
            }

            _points = new KeyPoint[sampledValues.Length];
            for (int i = 0; i < sampledValues.Length; i++)
            {
                float t = (float)i / (sampledValues.Length - 1);
                _points[i] = new KeyPoint(t, sampledValues[i]);
            }
        }

        /// <summary>
        /// 標準的な穏当な不足度カーブ: ratio 0.0 => 2.0 (最大不足), 0.5 => 1.0 (標準), 1.0 => 0.5 (充足・低不足)
        /// </summary>
        public static SampledPiecewiseCurveEvaluator DefaultInverse()
        {
            return new SampledPiecewiseCurveEvaluator(new[]
            {
                new KeyPoint(0.0f, 2.0f),
                new KeyPoint(0.5f, 1.0f),
                new KeyPoint(1.0f, 0.5f)
            });
        }

        public static SampledPiecewiseCurveEvaluator LinearInverse(float startValue = 2.0f, float endValue = 0.5f)
        {
            return new SampledPiecewiseCurveEvaluator(new[]
            {
                new KeyPoint(0.0f, startValue),
                new KeyPoint(1.0f, endValue)
            });
        }

        public float Evaluate(float t)
        {
            if (_points == null || _points.Length == 0) return 1.0f;
            if (_points.Length == 1) return _points[0].Value;

            if (t <= _points[0].Time) return _points[0].Value;
            if (t >= _points[^1].Time) return _points[^1].Value;

            for (int i = 0; i < _points.Length - 1; i++)
            {
                if (t >= _points[i].Time && t <= _points[i + 1].Time)
                {
                    float segmentDuration = _points[i + 1].Time - _points[i].Time;
                    if (segmentDuration <= 0.00001f) return _points[i].Value;

                    float factor = (t - _points[i].Time) / segmentDuration;
                    return _points[i].Value + factor * (_points[i + 1].Value - _points[i].Value);
                }
            }

            return _points[^1].Value;
        }
    }
}
