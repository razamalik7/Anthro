using System.Collections.Generic;
using Anthro.Sim.Activities;

namespace Anthro.Sim.People
{
    /// <summary>
    /// What a person expects, learned from their own results, from what they witness, and from
    /// others. Keys are plain topics ("Hunt", "danger:mammoth"), so any new kind of belief uses the
    /// same learning and spreads through the same social copying.
    /// </summary>
    public sealed class Expectations
    {
        readonly Dictionary<string, double> _values = new Dictionary<string, double>();
        readonly List<string> _topics = new List<string>();

        /// <summary>Every topic this person holds a belief about.</summary>
        public IReadOnlyList<string> Topics => _topics;

        void Set(string topic, double value)
        {
            if (!_values.ContainsKey(topic)) _topics.Add(topic);
            _values[topic] = value;
        }

        static readonly Dictionary<string, string> _danger = new Dictionary<string, string>();

        public static string Danger(string speciesName)
        {
            lock (_danger)
            {
                if (!_danger.TryGetValue(speciesName, out var t)) _danger[speciesName] = t = "danger:" + speciesName;
                return t;
            }
        }

        /// <summary>
        /// A shared value of the band ("valor", "caution", "sharing"). Held by each person, shaped by
        /// what happens, and spread by conversation, so a band's culture is the sum of its people.
        /// </summary>
        static readonly Dictionary<string, string> _norms = new Dictionary<string, string>();

        public static string Norm(string name)
        {
            lock (_norms)
            {
                if (!_norms.TryGetValue(name, out var t)) _norms[name] = t = "norm:" + name;
                return t;
            }
        }

        public double Get(string topic, double fallback) => _values.TryGetValue(topic, out var v) ? v : fallback;
        static readonly string[] _verbNames = System.Array.ConvertAll((Verb[])System.Enum.GetValues(typeof(Verb)), v => v.ToString());

        public static string Topic(Verb verb) => _verbNames[(int)verb];

        public double Get(Verb verb, double fallback) => Get(_verbNames[(int)verb], fallback);

        public bool Has(string topic) => _values.ContainsKey(topic);

        /// <summary>
        /// Move toward what actually happened. Open people adjust faster; cautious people weigh
        /// disappointments double.
        /// </summary>
        public void Learn(Person p, string topic, double outcome, double fallback, double strength = 1)
        {
            double current = Get(topic, fallback);
            double error = outcome - current;
            double rate = (0.1 + 0.3 * p.M(MindTrait.Openness)) * strength;
            if (error < 0) rate *= 1 + (1 - p.M(MindTrait.Boldness));
            Set(topic, current + System.Math.Min(1, rate) * error);
        }

        /// <summary>
        /// What an activity yields per hour is remembered as a long-run average of how it has gone
        /// (roughly the last 10-30 tries; shorter for the open-minded), good and bad weighed alike.
        /// Risky work like hunting fails most days and pays big now and then; a memory that dwelt on
        /// the failures would make it look worthless. Caution about risk belongs in the choice
        /// (boldness), not in a distorted memory.
        /// </summary>
        public void Learn(Person p, Verb verb, double outcome, double fallback)
        {
            string topic = _verbNames[(int)verb];
            double current = Get(topic, fallback);
            double rate = 0.03 + 0.07 * p.M(MindTrait.Openness);
            Set(topic, current + rate * (outcome - current));
        }

        /// <summary>
        /// Danger is learned asymmetrically: a disaster raises fear fast (more for the cautious),
        /// while uneventful encounters only slowly calm it.
        /// </summary>
        public void LearnDanger(Person p, string speciesName, double observed, double prior, double weight)
        {
            string topic = Danger(speciesName);
            double current = Get(topic, prior);
            double rate = observed > current ? 0.5 * (1.5 - p.M(MindTrait.Boldness)) : 0.05 * (0.5 + p.M(MindTrait.Openness));
            Set(topic, current + System.Math.Min(1, rate * weight) * (observed - current));
        }

        /// <summary>Copy part of someone else's expectation (social learning).</summary>
        public void Absorb(string topic, double theirs, double weight, double fallback)
        {
            double current = Get(topic, fallback);
            Set(topic, current + weight * (theirs - current));
        }

        public IEnumerable<KeyValuePair<string, double>> All => _values;

        readonly List<KeyValuePair<string, double>> _snapshot = new List<KeyValuePair<string, double>>();

        /// <summary>A copy safe to iterate while someone else's expectations change (reused to avoid allocation).</summary>
        public List<KeyValuePair<string, double>> Snapshot()
        {
            _snapshot.Clear();
            _snapshot.AddRange(_values);
            return _snapshot;
        }
    }
}
