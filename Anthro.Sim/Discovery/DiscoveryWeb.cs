using System.Collections.Generic;
using System.Linq;

namespace Anthro.Sim.Discovery
{
    /// <summary>One condition row on a stage. With <see cref="Any"/>, one ref is enough; otherwise all are needed.</summary>
    public sealed class Need
    {
        public string Type = "";
        public string Text = "";
        public List<string> Refs = new List<string>();
        public bool Any;

        /// <summary>"Helps" rows make a stage likelier but aren't required.</summary>
        public bool IsRequired => Type != "Helps";
    }

    /// <summary>
    /// The machine-readable side of a stage's conditions: where it can be stumbled on, what must be
    /// at hand, what pushes people toward it, and who notices. Knowledge prerequisites are the refs
    /// in <see cref="Stage.Needs"/>.
    /// </summary>
    public sealed class SimRules
    {
        public List<string> During = new List<string>();
        public List<string> Materials = new List<string>();
        public List<string> Pressures = new List<string>();
        public string? Interest, Skill, Trait, Way;
        public double Rate = 1;
        /// <summary>The interest, skill and trait as enum indexes (-1 if none), worked out once.</summary>
        public int InterestIndex = -1, SkillIndex = -1, TraitIndex = -1;
        internal bool Parsed;
    }

    public sealed class Stage
    {
        public SimRules? Sim;
        public string Id = "";
        public string Name = "";
        public string Short = "";
        public bool Known;
        public string? Variant;
        public string Does = "";
        public string? Accident;
        public List<Need> Needs = new List<Need>();
        public SkillTile Tile = null!;
    }

    public sealed class SkillTile
    {
        public string Id = "";
        public string Name = "";
        public string Kind = "";
        public string Summary = "";
        public List<Stage> Stages = new List<Stage>();
    }

    /// <summary>
    /// The discovery web, loaded from docs/discovery-web.json. The sim has no JSON dependency;
    /// the runner (or Unity) parses the file and hands the tiles in.
    /// </summary>
    public sealed class DiscoveryWeb
    {
        public readonly IReadOnlyList<SkillTile> Tiles;
        public readonly IReadOnlyDictionary<string, Stage> Stages;

        public DiscoveryWeb(IEnumerable<SkillTile> tiles)
        {
            Tiles = tiles.ToList();
            var index = new Dictionary<string, Stage>();
            foreach (var t in Tiles)
                foreach (var s in t.Stages)
                {
                    s.Tile = t;
                    index[s.Id] = s;
                }
            Stages = index;
        }

        public IEnumerable<Stage> KnownAtStart => Stages.Values.Where(s => s.Known);

        Dictionary<string, List<Stage>>? _byActivity;

        /// <summary>Stages that can be stumbled on while doing an activity (by Verb name).</summary>
        public IReadOnlyList<Stage> DiscoverableDuring(string verb)
        {
            if (_byActivity == null)
            {
                _byActivity = new Dictionary<string, List<Stage>>();
                foreach (var s in Stages.Values.Where(s => !s.Known && s.Sim != null))
                {
                    var r = s.Sim!;
                    if (!r.Parsed)
                    {
                        r.InterestIndex = r.Interest != null && System.Enum.TryParse<Anthro.Sim.People.Interest>(r.Interest, out var i) ? (int)i : -1;
                        r.SkillIndex = r.Skill != null && System.Enum.TryParse<Anthro.Sim.People.Skill>(r.Skill, out var k) ? (int)k : -1;
                        r.TraitIndex = r.Trait != null && System.Enum.TryParse<Anthro.Sim.People.MindTrait>(r.Trait, out var t) ? (int)t : -1;
                        r.Parsed = true;
                    }
                    foreach (var v in r.During)
                    {
                        if (!_byActivity.TryGetValue(v, out var list)) _byActivity[v] = list = new List<Stage>();
                        list.Add(s);
                    }
                }
            }
            return _byActivity.TryGetValue(verb, out var found) ? found : (IReadOnlyList<Stage>)System.Array.Empty<Stage>();
        }

        /// <summary>Whether someone knows everything a stage builds on.</summary>
        public static bool CanLearn(Stage s, HashSet<string> knows)
        {
            foreach (var n in s.Needs)
            {
                if (!n.IsRequired || n.Refs.Count == 0) continue;
                if (n.Any ? !n.Refs.Any(knows.Contains) : !n.Refs.All(knows.Contains)) return false;
            }
            return true;
        }

        /// <summary>Problems in the data: refs to stages that don't exist.</summary>
        public IEnumerable<string> Validate() =>
            from s in Stages.Values
            from n in s.Needs
            from r in n.Refs
            where !Stages.ContainsKey(r)
            select $"{s.Id} refers to unknown stage {r}";
    }
}
