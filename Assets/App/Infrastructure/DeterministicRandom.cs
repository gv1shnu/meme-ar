namespace MemeAR.Infrastructure
{
    /// <summary>
    /// Small, allocation-free, seedable PRNG (xorshift128). We deliberately do not use
    /// UnityEngine.Random for ranking/jitter because that global state is not reproducible
    /// across frames or tests. A seedable stream lets ranking and comedic jitter be
    /// verified deterministically while still feeling varied at runtime.
    /// </summary>
    public sealed class DeterministicRandom
    {
        private uint _x;
        private uint _y;
        private uint _z;
        private uint _w;

        public DeterministicRandom(uint seed = 0x9E3779B9u)
        {
            Reseed(seed);
        }

        public void Reseed(uint seed)
        {
            if (seed == 0u)
            {
                seed = 0x9E3779B9u;
            }

            _x = seed;
            _y = seed ^ 0x6C078965u;
            _z = seed * 1812433253u + 1u;
            _w = ~seed + 0x165667B1u;
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

        /// <summary>Uniform float in [0, 1).</summary>
        public float NextFloat()
        {
            // 24 bits of mantissa precision.
            return (NextUInt() >> 8) * (1.0f / 16777216.0f);
        }

        /// <summary>Uniform float in [min, max).</summary>
        public float Range(float min, float max)
        {
            if (max <= min)
            {
                return min;
            }

            return min + (max - min) * NextFloat();
        }

        /// <summary>Uniform int in [minInclusive, maxExclusive).</summary>
        public int Range(int minInclusive, int maxExclusive)
        {
            if (maxExclusive <= minInclusive)
            {
                return minInclusive;
            }

            uint span = (uint)(maxExclusive - minInclusive);
            return minInclusive + (int)(NextUInt() % span);
        }
    }
}
