using System.Collections.Generic;
using Anthro.Sim.Core;

namespace Anthro.Sim.People
{
    /// <summary>Short names from a small syllable set, so a band's names sound like they belong together.</summary>
    public sealed class Names
    {
        static readonly string[] Starts = { "A", "Ka", "To", "Ri", "Su", "Me", "Na", "Ul", "Ve", "Ro", "Is", "Ta", "Ke", "Lo", "Mi", "Da", "E", "Ha" };
        static readonly string[] Middles = { "ra", "ni", "ka", "lo", "su", "me", "ta", "ri", "va", "no", "shi", "do" };
        static readonly string[] Ends = { "", "", "n", "k", "sh", "m", "l", "r" };

        readonly HashSet<string> _used = new HashSet<string>();

        public string Next(Rng rng)
        {
            for (int i = 0; i < 50; i++)
            {
                string name = rng.Pick(Starts) + (rng.Chance(0.6) ? rng.Pick(Middles) : "") + rng.Pick(Ends);
                if (name.Length >= 2 && _used.Add(name)) return name;
            }
            string fallback = rng.Pick(Starts) + rng.Pick(Middles) + rng.Pick(Middles);
            _used.Add(fallback);
            return fallback;
        }
    }
}
