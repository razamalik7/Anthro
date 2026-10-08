using System;
using System.Linq;
using Anthro.Sim.Core;
using Anthro.Sim.People;

namespace Anthro.Sim
{
    /// <summary>
    /// Gatherings: in a time of plenty a camp feasts and dances together, and people who barely knew
    /// each other come away a little closer. This is what lets a bigger camp hold together: the strain
    /// of a big camp is living among near-strangers (see Crowding), and gatherings make fewer of them
    /// strangers. Ritual makes gatherings far more binding (the shared words and acts of lang.ritual);
    /// rites for the dead and body paint (a shared look that marks who belongs) add to it. Feasting costs
    /// food, so only a camp with plenty to spare gathers, which is why storage and gatherings go together.
    /// </summary>
    public sealed partial class Simulation
    {
        /// <summary>The wish to come together, growing since the last gathering (faster where ritual matters).</summary>
        double _gatherUrge;

        /// <summary>
        /// Checked daily. People want to come together more the longer it's been (about a season, sooner
        /// with ritual); they do once there's food enough to feast. So gatherings follow plenty, not dates.
        /// </summary>
        void Gathering()
        {
            int n = Band.Members.Count;
            if (n < 6 || Off("gatherings")) return;
            _gatherUrge += (1.0 + (Band.AnyoneKnows("lang.ritual") ? 0.5 : 0)) / SimClock.DaysPerSeason;
            if (_gatherUrge < 1) return;
            double daysOfFood = Band.FoodTotal / (n * 0.9);
            if (daysOfFood < 6) return;
            _gatherUrge = 0;
            // How much a gathering binds people: a dance by the fire does a little; ritual a great deal more.
            double bond = 0.01 + (Band.AnyoneKnows("lang.ritual") ? 0.03 : 0) + (Band.AnyoneKnows("burial.rites") ? 0.01 : 0)
                          + (Band.AnyoneKnows("ochre.body") ? 0.01 : 0) + (Band.AnyoneKnows("lang.story") ? 0.005 : 0)
                          + (Band.AnyoneKnows("ochre.paint") ? 0.005 : 0) + (Band.AnyoneKnows("ochre.figure") ? 0.005 : 0);
            var there = Band.Members.Where(m => !(m.Current is Activities.Visit) && m.AgeYears(Clock) >= 10).ToList();
            if (there.Count < 6) return;
            // The feast: each hearth puts in about a day's food for its people.
            foreach (var h in Band.Households)
            {
                double give = Math.Min(h.Food.Total * 0.3, h.Members.Count * 0.9);
                double eaten = h.Food.Take(give, out _);
                ThisYear.Eaten += eaten;
                foreach (var m in h.Members) m.Stomach = Math.Min(1.5, m.Stomach + eaten / Math.Max(1, h.Members.Count) / Math.Max(0.1, DailyNeed(m)));
            }
            for (int i = 0; i < there.Count; i++)
                for (int j = i + 1; j < there.Count; j++)
                {
                    var a = there[i]; var b = there[j];
                    // Mostly it's the near-strangers who come closer; old friends were close already.
                    if (a.GetAffinity(b) < 0.3) a.AddAffinity(b, bond);
                    if (b.GetAffinity(a) < 0.3) b.AddAffinity(a, bond);
                }
            foreach (var m in there) m.Discontent *= 0.9;
            Chronicle.Write(Clock, Importance.Detail, Band.AnyoneKnows("lang.ritual")
                ? $"The camp came together for a feast and the old rites; {there.Count} took part."
                : $"The camp feasted and danced by the fire; {there.Count} took part.");
        }
    }
}
