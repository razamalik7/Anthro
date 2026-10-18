using System.Linq;

namespace Anthro.Sim.People
{
    /// <summary>
    /// People copy each other's expectations, weighted toward the charismatic and the successful.
    /// Sociable people copy more. Copies are partial, which is how traditions drift.
    /// </summary>
    public static class SocialLearning
    {
        /// <summary>A conversation touches on a few things, not everything each person believes.</summary>
        const int TopicsPerTalk = 6;

        public static void Exchange(Person a, Person b, Core.Rng rng, Core.Rng? godRng = null)
        {
            CopyFrom(a, b, rng, godRng);
            CopyFrom(b, a, rng, godRng);
        }

        static void CopyFrom(Person listener, Person speaker, Core.Rng rng, Core.Rng? godRng)
        {
            double weight = 0.04 * (0.3 + speaker.M(MindTrait.Charisma)) * (0.5 + listener.M(MindTrait.Sociability))
                            * (1 + System.Math.Max(0, Simulation.Following(listener, speaker))) * (0.5 + listener.M(MindTrait.Openness));

            // 1. Secular social learning: uses sim.Rng on SecularTopics, preserving exact sequence from god-off
            var secular = speaker.Expect.SecularTopics;
            if (secular.Count > 0)
            {
                int n = System.Math.Min(TopicsPerTalk, secular.Count);
                double secularWeight = weight * ((double)secular.Count / n);
                if (secularWeight > 0.3) secularWeight = 0.3;
                for (int i = 0; i < n; i++)
                {
                    var key = secular[rng.Range(0, secular.Count)];
                    double v = speaker.Expect.Get(key, 0);
                    listener.Expect.Absorb(key, v, secularWeight, v);
                }
            }

            // 2. God topics: shared in conversation without diluting secular learning or altering sim.Rng sequence
            if (!Simulation.Off("god") && godRng != null)
            {
                var godTopics = speaker.Expect.GodTopics;
                if (godTopics.Count > 0)
                {
                    // D3 (owner decision 2026-10-13): Devout people talk about the god more often and more persuasively;
                    // listeners absorb testimony filtered by their own credulity, without weak echoes erasing firsthand experience.
                    double real = speaker.Expect.Get("god:real", 0.2);
                    if (real > 0.35)
                    {
                        double talkChance = System.Math.Min(0.85, (real - 0.25) * 1.1);
                        if (godRng.Chance(talkChance))
                        {
                            var key = godTopics[godRng.Range(0, godTopics.Count)];
                            double v = speaker.Expect.Get(key, 0);
                            double listenerCred = listener.M(MindTrait.Credulity);
                            double receptivity = System.Math.Pow(System.Math.Clamp((listenerCred - 0.15) / 0.75, 0.04, 1.4), 1.5);
                            double godWeight = weight * (0.3 + 1.2 * real) * receptivity;
                            if (godWeight > 0.4) godWeight = 0.4;

                            if (key == "god:real")
                            {
                                double listenerReal = listener.Expect.Get("god:real", 0.2);
                                double targetReal = v * System.Math.Clamp(1.25 * (listenerCred - 0.12), 0.05, 1.0);
                                if (targetReal > listenerReal)
                                    listener.Expect.Absorb(key, targetReal, godWeight * 0.14, listenerReal);
                            }
                            else if (System.Math.Abs(v) > 0.04)
                            {
                                double listenerVal = listener.Expect.Get(key, 0.0);
                                double targetVal = v * (0.25 + 0.70 * listenerCred);
                                listener.Expect.Absorb(key, targetVal, godWeight * 0.14, listenerVal);
                            }
                        }
                    }
                }
            }
        }
    }
}
