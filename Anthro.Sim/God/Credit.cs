using System;
using System.Collections.Generic;
using System.Linq;
using Anthro.Sim.Core;
using Anthro.Sim.People;
using Anthro.Sim.World;

namespace Anthro.Sim
{
    /// <summary>
    /// Credit and blame by misinterpretation (Phase 1, T5 / GOD-LAYER §5).
    /// When striking events occur (omens, answered/failed prayers, bounties, harsh weather),
    /// witnesses associate the event with the most salient deed in their recent memory
    /// (Person.Deeds, last season) and learn what the god approves or punishes (god:<act>).
    /// Believers incorporate Felt(god:<act>) * god:real into their decision making.
    /// </summary>
    public sealed partial class Region
    {
        /// <summary>
        /// Process a striking divine or natural event: witnesses tie the event to a salient deed.
        /// </summary>
        public void InterpretStrikingEvent(double x, double y, bool isGood, string source, double maxDistanceMeters = 5000)
        {
            if (Simulation.Off("god") || Simulation.Off("credit")) return;

            double maxDist = WorldMap.FromMeters(maxDistanceMeters);
            bool regionWide = (x == 0 && y == 0);

            var candidateBands = regionWide
                ? Bands
                : Bands.Where(b => WorldMap.Distance(b.Band.CampX, b.Band.CampY, x, y) <= maxDist);

            foreach (var band in candidateBands)
            {
                foreach (var witness in band.Band.Members.Where(m => band.Grown(m) && m.Alive))
                {
                    double real = Simulation.GodReal(witness);
                    if (real <= 0.05) continue; // Unbelievers do not attribute the event to divine will

                    Remembered? mostSalient = null;
                    double bestSalience = 0.0;

                    foreach (var rem in witness.Deeds)
                    {
                        var d = rem.Deed;
                        long ageDays = Clock.Day - d.Day;
                        if (ageDays < 0 || ageDays > SimClock.DaysPerSeason) continue;

                        // Salience: recency (1.0 down to 0.25 over one season)
                        double recency = 1.0 - (ageDays / (double)SimClock.DaysPerSeason) * 0.75;
                        // Firsthand observation is more vivid
                        double vivid = rem.Firsthand ? 1.0 : 0.6;
                        // Personal involvement increases salience
                        double involvement = (d.Actor == witness || d.Target == witness || (d.Hearth != null && d.Hearth == witness.Home)) ? 1.5 : 1.0;
                        // Dramatic/anti-social acts stand out
                        double actWeight = (d.Act == Act.Attack || d.Act == Act.Take) ? 1.4 : 1.0;

                        double salience = recency * vivid * involvement * actWeight * (1.0 + Math.Min(2.0, Math.Abs(d.Amount)));
                        if (salience > bestSalience)
                        {
                            bestSalience = salience;
                            mostSalient = rem;
                        }
                    }

                    if (mostSalient.HasValue && bestSalience > 0.2)
                    {
                        var deed = mostSalient.Value.Deed;
                        string topic = Simulation.GodActTopic(deed.Act);
                        double target = isGood ? 1.0 : -1.0;
                        double credulity = witness.M(MindTrait.Credulity);
                        double weight = 0.15 * (0.4 + credulity * 0.8) * real;
                        double current = witness.Expect.Get(topic, 0.0);

                        witness.Expect.Absorb(topic, target, weight, current);
                    }

                    // Striking events feed belief in the unseen power and update perceived disposition (GOD-LAYER §1)
                    double cred = witness.M(MindTrait.Credulity);
                    witness.Expect.Absorb(Simulation.GodRealTopic, 1.0, 0.08 * (0.4 + cred * 0.8), Simulation.GodReal(witness));
                    witness.Expect.Absorb(Simulation.GodKindTopic, isGood ? 1.0 : -1.0, 0.06 * (0.4 + cred * 0.8) * real, Simulation.GodKind(witness));
                }
            }
        }
    }

    public sealed partial class Simulation
    {
        /// <summary>
        /// Value adjustment for an act due to belief about the god's favor or wrath: Felt(god:<act>) * god:real.
        /// </summary>
        public static double GodActValue(Person p, Act act)
        {
            if (Off("god") || Off("credit")) return 0.0;
            return Felt(GodAct(p, act)) * GodReal(p);
        }
    }
}
