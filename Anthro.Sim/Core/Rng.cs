using System;
using System.Collections.Generic;

namespace Anthro.Sim.Core
{
    /// <summary>
    /// PCG32 random numbers. Identical on every platform, so a seed always replays the same history.
    /// </summary>
    public sealed class Rng
    {
        ulong _state;
        readonly ulong _inc;

        public Rng(ulong seed, ulong stream = 0xda3e39cb94b95bdbUL)
        {
            _inc = (stream << 1) | 1UL;
            NextUInt();
            _state += seed;
            NextUInt();
        }

        public uint NextUInt()
        {
            ulong old = _state;
            _state = old * 6364136223846793005UL + _inc;
            uint xorshifted = (uint)(((old >> 18) ^ old) >> 27);
            int rot = (int)(old >> 59);
            return (xorshifted >> rot) | (xorshifted << ((-rot) & 31));
        }

        public double NextDouble() => NextUInt() / 4294967296.0;

        public int Range(int minInclusive, int maxExclusive) =>
            minInclusive + (int)(NextDouble() * (maxExclusive - minInclusive));

        public double Range(double min, double max) => min + NextDouble() * (max - min);

        public bool Chance(double p) => NextDouble() < p;

        public double Normal(double mean = 0, double sd = 1)
        {
            double u1 = 1.0 - NextDouble();
            double u2 = NextDouble();
            return mean + sd * Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2 * Math.PI * u2);
        }

        /// <summary>Count of independent random arrivals with average <paramref name="lambda"/>.</summary>
        public int Poisson(double lambda)
        {
            if (lambda <= 0) return 0;
            if (lambda > 30) return Math.Max(0, (int)Math.Round(Normal(lambda, Math.Sqrt(lambda))));
            double limit = Math.Exp(-lambda), product = NextDouble();
            int count = 0;
            while (product > limit) { count++; product *= NextDouble(); }
            return count;
        }

        public T Pick<T>(IReadOnlyList<T> list) => list[Range(0, list.Count)];

        /// <summary>A child generator, so subsystems don't shift each other's sequences.</summary>
        public Rng Fork(ulong salt) => new Rng(((ulong)NextUInt() << 32 | NextUInt()) ^ salt, salt * 2 + 1);
    }
}
