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

        public static void Exchange(Person a, Person b, Core.Rng rng)
        {
            CopyFrom(a, b, rng);
            CopyFrom(b, a, rng);
        }

        static void CopyFrom(Person listener, Person speaker, Core.Rng rng)
        {
            double weight = 0.04 * (0.3 + speaker.M(MindTrait.Charisma)) * (0.5 + listener.M(MindTrait.Sociability))
                            * (1 + System.Math.Max(0, Simulation.Following(listener, speaker))) * (0.5 + listener.M(MindTrait.Openness));
            var topics = speaker.Expect.Topics;
            if (topics.Count == 0) return;
            // Each topic that comes up is copied more strongly than when everything was copied at once,
            // so beliefs spread at about the same pace overall.
            int n = System.Math.Min(TopicsPerTalk, topics.Count);
            weight *= (double)topics.Count / n;
            if (weight > 0.3) weight = 0.3;
            for (int i = 0; i < n; i++)
            {
                var key = topics[rng.Range(0, topics.Count)];
                double v = speaker.Expect.Get(key, 0);
                listener.Expect.Absorb(key, v, weight, v);
            }
        }
    }
}
