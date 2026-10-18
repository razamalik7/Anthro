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
        public void InterpretStrikingEvent(double x, double y, bool isGood, string source, Person? focalPerson = null, double maxDistanceMeters = 5000)
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
                    if (source != "BigKill")
                        witness.LastGodSignDay = Clock.Day;
                    double real = Simulation.GodReal(witness);
                    if (real <= 0.05 && focalPerson == null) continue; // Unbelievers do not attribute general events to divine will

                    double cred = witness.M(MindTrait.Credulity);
                    double receptivity = Math.Pow(Math.Clamp((cred - 0.12) / 0.75, 0.05, 1.5), 1.6) * (0.85 + 0.30 * witness.I(Interest.Spirits));
                    bool isPersonalMiracle = focalPerson != null && (focalPerson == witness || witness.Children.Contains(focalPerson) || witness.Spouses.Contains(focalPerson));

                    // Learning from relief (T10 A2)
                    if (!Simulation.Off("relief"))
                    {
                        if (!isGood)
                        {
                            if (!string.IsNullOrEmpty(witness.AppeasementAct))
                            {
                                // Another blow came despite an appeasement attempt: the attempt failed
                                double failWeight = 0.20 * (0.4 + cred * 0.8) * Math.Max(0.2, real);
                                if (witness.AppeasementAct == "offer")
                                {
                                    witness.Expect.Absorb(Simulation.GodOfferTopic, -1.0, failWeight, witness.Expect.Get(Simulation.GodOfferTopic, 0.0));
                                    string actTopic = Simulation.GodActTopic(Act.Offer);
                                    witness.Expect.Absorb(actTopic, -1.0, failWeight, witness.Expect.Get(actTopic, 0.0));
                                }
                                else if (witness.AppeasementAct == "pray")
                                {
                                    witness.Expect.Absorb(Simulation.GodAnswersTopic, 0.0, failWeight, Simulation.GodAnswers(witness));
                                }
                                else
                                {
                                    string topic = $"god:{witness.AppeasementAct.ToLowerInvariant()}";
                                    witness.Expect.Absorb(topic, -1.0, failWeight, witness.Expect.Get(topic, 0.0));
                                }
                                witness.AppeasementAct = null;
                                witness.AppeasementDread = 0.0;
                            }

                            if (real > 0.05)
                            {
                                witness.Dread = Math.Min(1.0, witness.Dread + 0.6);
                                witness.DreadPeak = Math.Max(witness.DreadPeak, witness.Dread);
                            }

                            // D1 (owner decision 2026-10-13): A blow followed thanks -> weaken god:thank (continuous recency over the season)
                            long daysSinceThanks = Clock.Day - witness.LastThanksDay;
                            if (daysSinceThanks >= 0 && daysSinceThanks <= SimClock.DaysPerSeason)
                            {
                                double recency = 1.0 - (daysSinceThanks / (double)SimClock.DaysPerSeason) * 0.75;
                                double weight = 0.18 * recency * receptivity * Math.Max(0.2, real);
                                witness.Expect.Absorb(Simulation.GodThankTopic, -1.0, weight, Simulation.GodThank(witness));
                            }
                        }
                        else
                        {
                            witness.Dread = Math.Max(0.0, witness.Dread - 0.4);

                            // D1 (owner decision 2026-10-13): Good fortune followed thanks -> strengthen god:thank (continuous surprise)
                            double foodComfort = Math.Min(1.0, (witness.Home?.DaysOfFood ?? 0.0) / 3.5);
                            double satComfort = Math.Max(0.0, (witness.Satiety - 0.4) / 0.6);
                            double comfort = 0.6 * foodComfort + 0.4 * satComfort;
                            double surprise = (source == "BigKill")
                                ? Math.Clamp(1.0 - comfort * 0.90, 0.04, 0.85)
                                : Math.Clamp(1.0 - comfort * 0.70, 0.15, 1.0);

                            long daysSinceThanks = Clock.Day - witness.LastThanksDay;
                            if (daysSinceThanks >= 0 && daysSinceThanks <= SimClock.DaysPerSeason)
                            {
                                double recency = 1.0 - (daysSinceThanks / (double)SimClock.DaysPerSeason) * 0.70;
                                double weight = 0.25 * recency * surprise * receptivity;
                                witness.Expect.Absorb(Simulation.GodThankTopic, 1.0, weight, Simulation.GodThank(witness));
                            }
                            else if (source != "BigKill")
                            {
                                // Witnessing an unprompted divine blessing inspires gratitude
                                witness.Expect.Absorb(Simulation.GodThankTopic, 0.65, 0.16 * surprise * receptivity, Simulation.GodThank(witness));
                            }
                        }
                    }

                    if (focalPerson != null)
                    {
                        // C3: Targeted direct acts (Lightning and Heal)
                        if (source == "Lightning")
                        {
                            var allDeeds = witness.Deeds.Concat(witness.OwnDeeds);
                            Remembered? wrongDeed = null;
                            double bestWrongSalience = 0.0;
                            foreach (var rem in allDeeds)
                            {
                                var d = rem.Deed;
                                if (d.Actor != focalPerson) continue;
                                long ageDays = Clock.Day - d.Day;
                                if (ageDays < 0 || ageDays > SimClock.DaysPerSeason) continue;
                                if (d.Act == Act.Attack || d.Act == Act.Take || d.Act == Act.Refuse)
                                {
                                    double salience = (1.0 - (ageDays / (double)SimClock.DaysPerSeason) * 0.75) * (rem.Firsthand ? 1.0 : 0.6) * (1.0 + Math.Min(2.0, Math.Abs(d.Amount)));
                                    if (salience > bestWrongSalience)
                                    {
                                        bestWrongSalience = salience;
                                        wrongDeed = rem;
                                    }
                                }
                            }

                            if (wrongDeed.HasValue)
                            {
                                // Lightning on a wrongdoer: "the god punishes <their act>"
                                var d = wrongDeed.Value.Deed;
                                string topic = Simulation.GodActTopic(d.Act);
                                double weight = 0.25 * receptivity * Math.Max(0.2, real);
                                witness.Expect.Absorb(topic, -1.0, weight, witness.Expect.Get(topic, 0.0));
                                witness.Expect.Absorb(Simulation.GodRealTopic, 1.0, 0.18 * receptivity, Simulation.GodReal(witness));
                                witness.Expect.Absorb(Simulation.GodKindTopic, -0.2, 0.08 * receptivity, Simulation.GodKind(witness));
                            }
                            else
                            {
                                // Lightning on someone seen as innocent (no remembered wrongs):
                                // PLANNING: innocent deaths cost believers; god:kind drops sharply
                                witness.Expect.Absorb(Simulation.GodRealTopic, 0.0, 0.15 * (0.4 + cred * 0.8), Simulation.GodReal(witness));
                                witness.Expect.Absorb(Simulation.GodKindTopic, -1.0, 0.25 * (0.4 + cred * 0.8), Simulation.GodKind(witness));
                            }
                        }
                        else if (source == "Heal")
                        {
                            var allDeeds = witness.Deeds.Concat(witness.OwnDeeds);
                            bool knownKiller = allDeeds.Any(rem => rem.Deed.Actor == focalPerson && rem.Deed.Act == Act.Attack && Clock.Day - rem.Deed.Day <= SimClock.DaysPerSeason);
                            if (knownKiller)
                            {
                                // Healing a known killer: people may conclude the god favours killers
                                string topic = Simulation.GodActTopic(Act.Attack);
                                double weight = 0.25 * receptivity * Math.Max(0.2, real);
                                witness.Expect.Absorb(topic, 1.0, weight, witness.Expect.Get(topic, 0.0));
                                witness.Expect.Absorb(Simulation.GodRealTopic, 1.0, 0.16 * receptivity, Simulation.GodReal(witness));
                                witness.Expect.Absorb(Simulation.GodKindTopic, -0.5, 0.12 * receptivity, Simulation.GodKind(witness));
                            }
                            else
                            {
                                // Healing an innocent/ordinary person: miraculous benevolence
                                witness.Expect.Absorb(Simulation.GodRealTopic, 1.0, 0.20 * receptivity, Simulation.GodReal(witness));
                                witness.Expect.Absorb(Simulation.GodKindTopic, 1.0, 0.15 * receptivity, Simulation.GodKind(witness));
                                if (isPersonalMiracle)
                                {
                                    witness.Expect.Absorb(Simulation.GodAnswersTopic, 1.0, 0.25 * receptivity, Simulation.GodAnswers(witness));
                                }
                            }
                        }
                        else if (source == "BlessedBirth")
                        {
                            witness.Expect.Absorb(Simulation.GodRealTopic, 1.0, 0.20 * receptivity, Simulation.GodReal(witness));
                            witness.Expect.Absorb(Simulation.GodKindTopic, 1.0, 0.15 * receptivity, Simulation.GodKind(witness));
                        }
                    }
                    else
                    {
                        if (band.LastWitnessedDeed != null && band.LastWitnessedDeed.Day == Clock.Day)
                        {
                            var td = band.LastWitnessedDeed;
                            if (!witness.Deeds.Any(rem => rem.Deed.Actor == td.Actor && rem.Deed.Act == td.Act && rem.Deed.Day == td.Day) &&
                                !witness.OwnDeeds.Any(rem => rem.Deed.Actor == td.Actor && rem.Deed.Act == td.Act && rem.Deed.Day == td.Day))
                            {
                                witness.Deeds.Add(new Remembered(td, false));
                            }
                        }

                        Remembered? mostSalient = null;
                        double bestSalience = 0.0;

                        var allDeeds = witness.Deeds.Concat(witness.OwnDeeds);
                        foreach (var rem in allDeeds)
                        {
                            var d = rem.Deed;
                            long ageDays = Clock.Day - d.Day;
                            if (ageDays < 0 || ageDays > SimClock.DaysPerSeason) continue;

                            // Salience: recency (steep drop so immediate deed strongly dominates)
                            double recency = ageDays == 0 ? 3.0 : (ageDays <= 2 ? 1.6 : Math.Pow(Math.Max(0.0, 1.0 - (ageDays / (double)SimClock.DaysPerSeason)), 2.5));
                            // Firsthand observation is much more vivid than gossip
                            double vivid = rem.Firsthand ? 1.0 : 0.25;
                            // Personal involvement (own deed or deed against self) strongly dominates conscience
                            double involvement = (d.Actor == witness || d.Target == witness)
                                ? 4.2
                                : ((d.Hearth != null && d.Hearth == witness.Home) ? 1.4 : 1.0);
                            // Transgressive or sacred acts stand out strongly over routine sharing
                            double actWeight = (d.Act == Act.Attack || d.Act == Act.Take || d.Act == Act.Offer || d.Act == Act.Refuse)
                                ? 3.5
                                : (d.Act == Act.Give ? 0.45 : 1.2);
                            // Individual focus variation so bystanders don't all fixate on the exact same background deed
                            double focus = 0.80 + 0.40 * ((((witness.Id * 31 + d.Actor.Id * 17 + (int)d.Act * 7 + (int)d.Day) & 31)) / 31.0);

                            double salience = recency * vivid * involvement * actWeight * focus * (1.0 + Math.Min(1.5, Math.Abs(d.Amount) * 0.5));
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
                            double involvementFactor = (deed.Actor == witness || deed.Target == witness)
                                ? 2.5
                                : (mostSalient.Value.Firsthand ? 0.65 : 0.20);
                            double weight = 0.22 * receptivity * involvementFactor * Math.Max(0.25, real);
                            double current = witness.Expect.Get(topic, 0.0);

                            witness.Expect.Absorb(topic, target, weight, current);

                            // B1 (owner decision 2026-10-12): Sharing teaches generosity (god:give), not offering.
                            // Only dedicated offerings (Act.Offer, including grave goods) teach/reinforce god:offer.
                            if (deed.Act == Act.Offer)
                            {
                                double curOffer = witness.Expect.Get(Simulation.GodOfferTopic, 0.0);
                                witness.Expect.Absorb(Simulation.GodOfferTopic, target, weight, curOffer);
                            }
                        }

                        // Striking events feed belief in the unseen power and update perceived disposition (GOD-LAYER §1)
                        double eventStrength = (source == "Omen") ? 0.14 : 0.20;
                        witness.Expect.Absorb(Simulation.GodRealTopic, 1.0, eventStrength * receptivity, Simulation.GodReal(witness));
                        witness.Expect.Absorb(Simulation.GodKindTopic, isGood ? 1.0 : -1.0, 0.08 * receptivity * real, Simulation.GodKind(witness));

                        // Events while a plea is open count (brief B1): sign (belief up) or refusal (belief/kind down)
                        if (HasOpenPrayer(witness))
                        {
                            if (isGood)
                            {
                                witness.Expect.Absorb(Simulation.GodAnswersTopic, 1.0, 0.15 * receptivity, Simulation.GodAnswers(witness));
                                witness.Expect.Absorb(Simulation.GodRealTopic, 1.0, 0.12 * receptivity, Simulation.GodReal(witness));
                            }
                            else
                            {
                                witness.Expect.Absorb(Simulation.GodAnswersTopic, 0.0, 0.08, Simulation.GodAnswers(witness));
                                witness.Expect.Absorb(Simulation.GodKindTopic, -1.0, 0.06, Simulation.GodKind(witness));
                            }
                        }
                    }
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
