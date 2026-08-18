using System;

namespace Shinzui.Domain.ValueObjects.ItemSpawn
{
    /// <summary>
    /// 高速かつ決定論的なXorShift128アルゴリズムに基づく擬似乱数生成器。
    /// UnityEngine.Randomのグローバル状態を一切変更せず、シード値から完全に同一の乱数列を再現する。
    /// </summary>
    public sealed class ItemSpawnPRNG : IRandomNumberGenerator
    {
        private uint _x;
        private uint _y;
        private uint _z;
        private uint _w;

        public int Seed { get; }

        public ItemSpawnPRNG(int seed)
        {
            Seed = seed;
            Initialize((uint)seed);
        }

        private void Initialize(uint seed)
        {
            // 0シード対策および全ステートの分散初期化 (SplitMix32風初期化)
            uint state = seed == 0 ? 0x853c49e6u : seed;
            _x = NextSplitMix(ref state);
            _y = NextSplitMix(ref state);
            _z = NextSplitMix(ref state);
            _w = NextSplitMix(ref state);

            // 全ビットが0にならないよう保護
            if (_x == 0 && _y == 0 && _z == 0 && _w == 0)
            {
                _w = 1;
            }
        }

        private static uint NextSplitMix(ref uint state)
        {
            state += 0x9e3779b9u;
            uint z = state;
            z = (z ^ (z >> 16)) * 0x85ebca6bu;
            z = (z ^ (z >> 13)) * 0xc2b2ae35u;
            return z ^ (z >> 16);
        }

        public uint NextUInt()
        {
            uint t = _x ^ (_x << 11);
            _x = _y;
            _y = _z;
            _z = _w;
            _w = _w ^ (_w >> 19) ^ (t ^ (t >> 8));
            return _w;
        }

        public int NextInt(int minInclusive, int maxExclusive)
        {
            if (minInclusive >= maxExclusive)
            {
                return minInclusive;
            }

            long range = (long)maxExclusive - minInclusive;
            uint raw = NextUInt();
            return (int)(minInclusive + (raw % range));
        }

        public float NextFloat()
        {
            // [0.0f, 1.0f)
            return (NextUInt() & 0x00FFFFFF) / 16777216.0f;
        }

        public float NextFloat(float minInclusive, float maxInclusive)
        {
            if (minInclusive >= maxInclusive)
            {
                return minInclusive;
            }
            return minInclusive + (NextFloat() * (maxInclusive - minInclusive));
        }

        public double NextDouble()
        {
            // [0.0, 1.0) with 53-bit resolution
            ulong high = (ulong)NextUInt() << 21;
            ulong low = (ulong)NextUInt() >> 11;
            return (high | low) * (1.0 / 9007199254740992.0);
        }
    }
}
