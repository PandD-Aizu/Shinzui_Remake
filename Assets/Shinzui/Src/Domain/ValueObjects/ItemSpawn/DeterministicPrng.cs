using System;

namespace Shinzui.Domain.ValueObjects.ItemSpawn
{
    /// <summary>
    /// 高速かつ決定論的なPRNG（擬似乱数生成器）実装。
    /// UnityEngine.Randomのグローバルステートに一切干渉せず、シード値によって完全な再現性を保証する。
    /// </summary>
    public sealed class DeterministicPrng : ISpawnPrng, IRandomNumberGenerator
    {
        private ulong _s0;
        private ulong _s1;
        public int Seed { get; private set; }

        public DeterministicPrng(int seed)
        {
            SetSeed(seed);
        }

        public void SetSeed(int seed)
        {
            Seed = seed;
            // SplitMix64で初期シードを拡散
            ulong s = (ulong)(seed == 0 ? 0x123456789ABCDEF0 : (long)seed);
            _s0 = SplitMix64(ref s);
            _s1 = SplitMix64(ref s);
            if (_s0 == 0 && _s1 == 0)
            {
                _s0 = 0x853c49e6548df9d5UL;
                _s1 = 0xda3e2b9737a915fcUL;
            }
        }

        private static ulong SplitMix64(ref ulong x)
        {
            ulong z = (x += 0x9E3779B97F4A7C15UL);
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }

        private ulong NextUInt64()
        {
            // Xoroshiro128+
            ulong s0 = _s0;
            ulong s1 = _s1;
            ulong result = s0 + s1;

            s1 ^= s0;
            _s0 = ((s0 << 24) | (s0 >> 40)) ^ s1 ^ (s1 << 16);
            _s1 = (s1 << 37) | (s1 >> 27);

            return result;
        }

        public uint NextUInt()
        {
            return (uint)(NextUInt64() & 0xFFFFFFFF);
        }

        public int NextInt()
        {
            return (int)(NextUInt64() & 0x7FFFFFFF);
        }

        public int NextInt(int minInclusive, int maxExclusive)
        {
            if (minInclusive >= maxExclusive)
            {
                return minInclusive;
            }
            long range = (long)maxExclusive - minInclusive;
            ulong rand = NextUInt64() & 0x7FFFFFFF;
            long offset = (long)(rand % (ulong)range);
            return (int)(minInclusive + offset);
        }

        public float NextFloat()
        {
            // [0.0f, 1.0f)
            return (float)((NextUInt64() >> 40) * (1.0 / (1 << 24)));
        }

        public float NextFloat(float minInclusive, float maxInclusive)
        {
            if (minInclusive >= maxInclusive)
            {
                return minInclusive;
            }
            return minInclusive + (maxInclusive - minInclusive) * NextFloat();
        }

        public double NextDouble()
        {
            // [0.0, 1.0)
            return (NextUInt64() >> 11) * (1.0 / (1UL << 53));
        }
    }
}
