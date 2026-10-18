using System;
using System.Collections.Generic;
using System.Linq;
using Anthro.Sim.People;

namespace Anthro.Sim
{
    /// <summary>
    /// Belief state for the god layer (Phase 1, T1). Belief lives in individual people as learned topics
    /// (Expectations), spreads by conversation, is inherited mother -> child and across kin camps,
    /// and slowly decays toward its baseline seed when unfed by signs.
    /// </summary>
    public sealed partial class Simulation
    {
        public const string GodRealTopic = "god:real";
        public const string GodAnswersTopic = "god:answers";
        public const string GodKindTopic = "god:kind";
        public const string GodOfferTopic = "god:offer";
        public const string GodThankTopic = "god:thank";

        public static string GodActTopic(string act) => $"god:{act.ToLowerInvariant()}";
        public static string GodActTopic(Act act) => $"god:{act.ToString().ToLowerInvariant()}";

        public static double GodReal(Person p) => p.Expect.Get(GodRealTopic, 0.2);
        public static double GodAnswers(Person p) => p.Expect.Get(GodAnswersTopic, 0.1);
        public static double GodKind(Person p) => p.Expect.Get(GodKindTopic, 0.0);
        public static double GodOffer(Person p) => GodAct(p, Act.Offer);
        public static double GodThank(Person p) => p.Expect.Get(GodThankTopic, 0.0);
        public static double GodAct(Person p, string act) => p.Expect.Get(GodActTopic(act), 0.0);
        public static double GodAct(Person p, Act act) => p.Expect.Get(GodActTopic(act), 0.0);
        public static bool IsBeliever(Person p) => GodReal(p) > 0.5;

        /// <summary>
        /// Seed beliefs for a people (vague spirits, low faith).
        /// </summary>
        void SeedBelief(List<Person> group)
        {
            if (Off("god") || Off("belief")) return;
            var rng = Region.GodRng;
            double baseReal = Math.Clamp(rng.Normal(0.20, 0.06), 0.14, 0.30);
            double baseAnswers = Inheritance.Clamp01(rng.Normal(0.1, 0.05));
            double baseKind = Math.Max(-1, Math.Min(1, rng.Normal(0.0, 0.1)));

            foreach (var p in group)
            {
                p.Expect.Absorb(GodRealTopic, Math.Clamp(baseReal + rng.Normal(0, 0.05), 0.08, 0.35), 1, 0);
                p.Expect.Absorb(GodAnswersTopic, Math.Clamp(baseAnswers + rng.Normal(0, 0.02), 0.02, 0.18), 1, 0);
                p.Expect.Absorb(GodKindTopic, Math.Max(-1, Math.Min(1, baseKind + rng.Normal(0, 0.05))), 1, 0);
            }

            // B4 (owner decision 2026-10-12): Seed god:offer per people with small variation
            // (some start with a weak offering habit, most with none), the way customs are seeded in Customs.SeedCustoms.
            // From GodRng.
            double baseOffer = rng.Range(0.0, 1.0) < 0.25 ? rng.Range(0.05, 0.15) : 0.0;
            if (baseOffer > 0.0)
            {
                Region.OfferingsFromSeedCount++;
                foreach (var p in group)
                {
                    double indOffer = Math.Max(0.0, baseOffer + rng.Normal(0, 0.02));
                    p.Expect.Absorb(GodOfferTopic, indOffer, 1, 0);
                }
            }
            // D1 (owner decision 2026-10-13): Seed god:thank per people with cultural variation
            double baseThank = rng.Range(0.12, 0.22);
            foreach (var p in group)
            {
                double indThank = Math.Clamp(baseThank + rng.Normal(0, 0.04), 0.02, 0.28);
                p.Expect.Absorb(GodThankTopic, indThank, 1, 0);
            }
        }

        static readonly string[] _moralGodActTopics = { "god:take", "god:give", "god:attack", "god:refuse", "god:ask", "god:confront" };

        /// <summary>
        /// Faith fades without signs: god:real relaxes slowly toward a temperament-grounded baseline (~0.05 to 0.28),
        /// faster for the less credulous (D3 / Owner Target 5).
        /// Offerings and thanksgivings that go unanswered erode slowly and continuously, like prayers.
        /// Tiny daily adjustment in the daily person update.
        /// </summary>
        public void FadeBelief(Person p)
        {
            if (Off("god") || Off("belief")) return;
            double cred = p.M(MindTrait.Credulity);
            double restReal = Math.Clamp(0.06 + 0.32 * cred, 0.06, 0.34);
            double fadeRate = 0.00035 * Math.Pow(Math.Clamp((1.15 - cred) / 0.65, 0.35, 2.0), 1.4);
            p.Expect.Absorb(GodRealTopic, restReal, fadeRate, restReal);

            // Moral act attributions relax slowly unless refreshed by new deeds + signs, preserving personal variation
            foreach (var topic in _moralGodActTopics)
            {
                double val = p.Expect.Get(topic, 0.0);
                if (Math.Abs(val) > 0.005)
                    p.Expect.Absorb(topic, 0.0, 0.0006, val);
            }

            // Offerings that go unanswered erode daily, like prayers (T10 A3: no timers)
            double offerBelief = GodOffer(p);
            if (offerBelief > 0)
            {
                bool unansweredOwn = p.OwnDeeds.Any(d => d.Deed.Act == Act.Offer && d.Deed.Day > p.LastGodSignDay);
                bool unansweredSeen = !unansweredOwn && p.Deeds.Any(d => d.Deed.Act == Act.Offer && d.Deed.Day > p.LastGodSignDay);
                double decay = unansweredOwn ? 0.004 : (unansweredSeen ? 0.002 : 0.0008);
                p.Expect.Absorb(GodOfferTopic, 0.0, decay, offerBelief);
            }

            // D1: Everyday devotion urge grows when fed and well; unanswered thanks erode daily without signs
            if (!Off("pray") && p.Satiety >= 0.55 && p.Health >= 0.6)
            {
                double real = GodReal(p);
                double thank = Math.Max(0.0, GodThank(p));
                // Urge grows from faith and learned gratitude, slightly faster after recent signs, slower when over-comfortable
                double signFreshness = (p.LastGodSignDay >= 0) ? Math.Exp(-(Clock.Day - p.LastGodSignDay) / 90.0) : 0.15;
                p.ThanksUrge = Math.Min(2.0, p.ThanksUrge + real * thank * (0.35 + 0.65 * signFreshness));
            }
            double thankBelief = GodThank(p);
            if (thankBelief > 0.01)
            {
                bool unansweredThanks = p.LastThanksDay > p.LastGodSignDay;
                double thankDecay = unansweredThanks ? 0.0020 * (1.2 - 0.5 * cred) : 0.0005 * (1.2 - 0.5 * cred);
                p.Expect.Absorb(GodThankTopic, 0.01, thankDecay, thankBelief);
            }

            // Learning from relief (T10 A2): dread fades continuously; easing dread credits appeasement
            if (!Off("relief") && p.Dread > 0)
            {
                double prevDread = p.Dread;
                double dreadDecay = (p.Health >= 0.7 && p.Satiety >= 0.5) ? 0.05 : 0.02;
                p.Dread = Math.Max(0.0, p.Dread - dreadDecay);
                double drop = prevDread - p.Dread;

                if (drop > 0 && !string.IsNullOrEmpty(p.AppeasementAct))
                {
                    double real = GodReal(p);
                    double reliefWeight = 0.20 * (0.4 + cred * 0.8) * Math.Max(0.2, real) * drop;

                    if (p.AppeasementAct == "offer")
                    {
                        p.Expect.Absorb(GodOfferTopic, 1.0, reliefWeight, p.Expect.Get(GodOfferTopic, 0.0));
                        string actTopic = GodActTopic(Act.Offer);
                        p.Expect.Absorb(actTopic, 1.0, reliefWeight, p.Expect.Get(actTopic, 0.0));
                    }
                    else if (p.AppeasementAct == "pray")
                    {
                        p.Expect.Absorb(GodAnswersTopic, 1.0, reliefWeight, GodAnswers(p));
                    }
                    else
                    {
                        string topic = $"god:{p.AppeasementAct.ToLowerInvariant()}";
                        p.Expect.Absorb(topic, 1.0, reliefWeight, p.Expect.Get(topic, 0.0));
                    }
                }

                if (p.Dread <= 0.01)
                {
                    p.Dread = 0.0;
                    p.DreadPeak = 0.0;
                    p.AppeasementAct = null;
                    p.AppeasementDread = 0.0;
                }
            }
        }
    }
}
