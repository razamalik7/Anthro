using System.Diagnostics;
using Anthro.Runner;
using Anthro.Sim;
using Anthro.Sim.Core;
using Anthro.Sim.Ecology;

// Usage: dotnet run --project Anthro.Runner -- [--seed N] [--years N] [--detail] [--quiet]
Console.OutputEncoding = System.Text.Encoding.UTF8;
ulong seed = 1;
int years = 100;
bool detail = false, quiet = false, families = false, kids = false, allBands = false;
int kinCamps = 0;
string[] grant = Array.Empty<string>();
for (int i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--seed": seed = ulong.Parse(args[++i]); break;
        case "--years": years = int.Parse(args[++i]); break;
        case "--detail": detail = true; break;
        case "--quiet": quiet = true; break;
        case "--families": families = true; break;
        case "--kids": kids = true; break;
        case "--grant": grant = args[++i].Split(',', StringSplitOptions.RemoveEmptyEntries); break;
        case "--all": allBands = true; break;
        case "--kin": kinCamps = int.Parse(args[++i]); break;
        case "--profile": Anthro.Sim.Core.Profiler.Enabled = true; break;
        // Testing only: land richness for calibration sweeps.
        case "--chest": Anthro.Sim.Simulation.ChestRisk = double.Parse(args[++i], System.Globalization.CultureInfo.InvariantCulture); break;
        case "--weaning": Anthro.Sim.Simulation.WeaningRisk = double.Parse(args[++i], System.Globalization.CultureInfo.InvariantCulture); break;
        case "--fever": Anthro.Sim.Simulation.FeverRate = double.Parse(args[++i], System.Globalization.CultureInfo.InvariantCulture); break;
        case "--discovery": Anthro.Sim.Simulation.DiscoveryRate = double.Parse(args[++i], System.Globalization.CultureInfo.InvariantCulture); break;
        case "--accident": Anthro.Sim.Simulation.AccidentRate = double.Parse(args[++i], System.Globalization.CultureInfo.InvariantCulture); break;
        case "--plants": Anthro.Sim.World.WorldGen.PlantDensity = double.Parse(args[++i], System.Globalization.CultureInfo.InvariantCulture); break;
    }
}

var webPath = WebLoader.Find();
var web = webPath != null ? WebLoader.Load(webPath) : null;
if (web == null) Console.WriteLine("(discovery-web.json not found; running with built-in starting knowledge)");
else
{
    var problems = web.Validate().ToList();
    Console.WriteLine($"Discovery web: {web.Tiles.Count} skills, {web.Stages.Count} stages, {web.KnownAtStart.Count()} known at start.");
    foreach (var p in problems) Console.WriteLine($"  ! {p}");
}

var region = Region.Create(seed, web, kinCamps: kinCamps);
var sim = region.Founders;
// Testing only: hand everyone on the map some knowledge, to see what a technology does before discovery exists.
foreach (var m in region.People) foreach (var g in grant) m.Knows.Add(g);
if (grant.Length > 0) Console.WriteLine($"Granted: {string.Join(", ", grant)}");
Console.WriteLine($"Seed {seed}. Running {years} years.\n");

// The story follows the founding band; the region's own events (animals, bands appearing and ending) are shown too.
foreach (var b in region.Bands) foreach (var e in b.Chronicle.Entries) Console.WriteLine($"  ■ Y{e.Year,-3} {e.Season,-6} {e.Text}");
if (!quiet)
{
    void Show(ChronicleEntry e)
    {
        if (e.Importance == Importance.Detail && !detail) return;
        string mark = e.Importance == Importance.Major ? "■" : "·";
        Console.WriteLine($"  {mark} Y{e.Year,-3} {e.Season,-6} {e.Text}");
    }
    sim.Chronicle.Written += Show;
    region.Chronicle.Written += Show;
    // Every band's story, labelled, including bands formed later.
    if (allBands)
    {
        var watched = new HashSet<Simulation> { sim };
        void Watch()
        {
            foreach (var b in region.Bands.Where(b => watched.Add(b)))
            {
                var band = b;
                band.Chronicle.Written += e =>
                {
                    if (e.Importance == Importance.Detail && !detail) return;
                    Console.WriteLine($"  {(e.Importance == Importance.Major ? "■" : "·")} Y{e.Year,-3} {e.Season,-6} [{band.Band.Name}] {e.Text}");
                };
            }
        }
        Watch();
        region.Chronicle.Written += _ => Watch();
        region.YearEnded += _ => Watch();
    }
}

sim.YearEnded += y =>
{
    if (kids) foreach (var c in sim.Band.Members.Where(m => m.AgeYears(sim.Clock) >= 2 && m.AgeYears(sim.Clock) < 15)) Console.WriteLine($"     kid {c.Name,-8} age {c.AgeYears(sim.Clock),4:F1} stomach {c.Stomach:F2} reserve {c.Reserve:F2} fed {c.Satiety:F2} health {c.Health:F2} doing {c.Current}");
    if (families) Console.WriteLine($"     food: needed {y.Needed:F0} gathered {y.Gathered:F0} hunted {y.Hunted:F0} fished {y.Fished:F0} eaten-at-camp {y.Eaten:F0} spoiled {y.Spoiled:F0} | adult hours: " + string.Join(", ", y.Hours.OrderByDescending(h => h.Value).Select(h => $"{h.Key} {h.Value}")));
    if (families)
        foreach (var w in sim.Band.Members.Where(m => m.Sex == Anthro.Sim.People.Sex.Female && m.AgeYears(sim.Clock) >= 14))
            Console.WriteLine($"     {w.Name,-8} age {w.AgeYears(sim.Clock),4:F0} partner {(w.Partner?.Name ?? "-"),-8} fed {w.Satiety:F2} health {w.Health:F2} due {w.DueDay} kids<2 {w.Children.Count(c => c.Alive && c.AgeYears(sim.Clock) < 2)}");
    string deaths = y.Deaths.Count == 0 ? "none" : string.Join(", ", y.Deaths.Select(d => $"{d.Value} {d.Key}"));
    string kills = y.Kills.Count == 0 ? "none" : string.Join(", ", y.Kills.OrderByDescending(k => k.Value).Select(k => $"{k.Value} {k.Key}"));
    Console.WriteLine($"── Year {y.Year,3} │ pop {y.Population,3} │ couples {y.Couples,2} │ fed {y.AvgSatiety:P0} health {y.AvgHealth:P0} sick-days {y.SickDays,4} │ standing {y.Valor:F2} give {y.Sharing:F1} │ shared {y.Shared,4:F0} refused {y.Refusals,3} thefts {y.Thefts,2} fights {y.Fights,2} raids {y.Raids} │ born {y.Births,2} │ cook+{y.CookingGain,4:F0} fire-out {y.FireOutDays,3} │ died: {deaths} │ kills: {kills} │ "
                      + $"animals: deer {sim.Ecology.Total(Species.Deer)} (+{sim.Ecology.Away(Species.Deer)} away), boar {sim.Ecology.Total(Species.Boar)}, hares {sim.Ecology.Total(Species.Rabbit)}, mammoths {sim.Ecology.Total(Species.Mammoth)} (+{sim.Ecology.Away(Species.Mammoth)} away), wolves {sim.Ecology.Total(Species.Wolf)}");
};

// One line a year for the whole map: every band's size, and births and deaths across all of them.
region.YearEnded += all =>
{
    var deaths = new Dictionary<string, int>();
    foreach (var (_, y) in all) foreach (var d in y.Deaths) deaths[d.Key] = deaths.TryGetValue(d.Key, out var n) ? n + d.Value : d.Value;
    string died = deaths.Count == 0 ? "none" : string.Join(", ", deaths.OrderByDescending(d => d.Value).Select(d => $"{d.Value} {d.Key}"));
    string bands = string.Join(", ", region.Bands.OrderByDescending(b => b.Band.Members.Count).Select(b => $"{b.Band.Name} {b.Band.Members.Count}" + (b.Band.Dogs > 0 ? $" ({b.Band.Dogs} dogs, fed {b.Band.DogsFed:F1})" : "")));
    Console.WriteLine($"   map │ total {region.People.Count(),3} │ bands {region.Bands.Count}: {bands} │ born {all.Sum(a => a.year.Births)} │ died: {died}");
};

var timer = Stopwatch.StartNew();
region.RunYears(years);
timer.Stop();

Console.WriteLine();
Console.WriteLine(region.Extinct
    ? $"Everyone on the map was gone by year {region.Clock.Year}."
    : $"After {region.Clock.Year} years: {region.People.Count()} people alive in {region.Bands.Count} bands; the founding band {(sim.Extinct ? "is gone" : $"has {sim.Band.Members.Count}")}.");
// Child mortality, the figure real forager demography is most often summarized by: of the children
// born early enough that we know whether they reached 15, how many died as infants and as children.
{
    var everyone = region.AllBands.SelectMany(b => b.Band.Members.Concat(b.Band.Dead)).Distinct().ToList();
    long cutoff = (long)(region.Clock.Year - 15) * Anthro.Sim.Core.SimClock.DaysPerYear;
    var cohort = everyone.Where(p => p.BirthDay >= 0 && p.BirthDay <= cutoff).ToList();
    double AgeAtDeath(Anthro.Sim.People.Person p) => (p.DeathDay - p.BirthDay) / (double)Anthro.Sim.Core.SimClock.DaysPerYear;
    if (cohort.Count > 0)
    {
        double infant = cohort.Count(p => !p.Alive && AgeAtDeath(p) < 1) / (double)cohort.Count;
        double child = cohort.Count(p => !p.Alive && AgeAtDeath(p) < 15) / (double)cohort.Count;
        Console.WriteLine($"Children born in years 0-{region.Clock.Year - 15} ({cohort.Count}): died before 1: {infant:P0}, before 15: {child:P0}.");
    }
    var adults = everyone.Where(p => !p.Alive && AgeAtDeath(p) >= 15).ToList();
    if (adults.Count > 0) Console.WriteLine($"Adults who died ({adults.Count}): mean age {adults.Average(AgeAtDeath):F0}.");
}
// What was discovered, and what's still known now.
{
    var firsts = region.DiscoveryLog.GroupBy(d => d.stage).Select(g => g.OrderBy(d => d.year).First()).OrderBy(d => d.year).ToList();
    var knownNow = region.People.SelectMany(p => p.Knows).Distinct().Where(id => web == null || !web.Stages[id].Known).ToList();
    Console.WriteLine($"Discoveries: {region.DiscoveryLog.Count} ({firsts.Count} different); known on the map now: {knownNow.Count} beyond the start.");
    if (firsts.Count > 0) Console.WriteLine("  first found: " + string.Join(", ", firsts.Select(d => $"{d.stage} (Y{d.year}, {d.band})")));
    if (knownNow.Count > 0) Console.WriteLine("  known now: " + string.Join(", ", knownNow.OrderBy(x => x)));
}
// How each of the biggest bands has come to weigh things (drive averages, 0.5 typical).
foreach (var b in region.Bands.OrderByDescending(b => b.Band.Members.Count).Take(5))
{
    var adults = b.Band.Members.Where(m => m.AgeYears(region.Clock) >= 16).ToList();
    if (adults.Count == 0) continue;
    string drives = string.Join(" ", Enum.GetValues<Anthro.Sim.People.Drive>().Select(d => $"{d.ToString().ToLower()} {adults.Average(a => a.Drives[(int)d]):F2}"));
    double E(Anthro.Sim.People.Act act, bool? seen = null) => adults.Average(a => Anthro.Sim.Simulation.Expects(a, act, Anthro.Sim.People.Relation.Neutral, seen));
    string norms = $"expects: give {E(Anthro.Sim.People.Act.Give):F1} ask {E(Anthro.Sim.People.Act.Ask):F1} take-seen {E(Anthro.Sim.People.Act.Take, true):F1} take-unseen {E(Anthro.Sim.People.Act.Take, false):F1} confront {E(Anthro.Sim.People.Act.Confront):F1} attack {E(Anthro.Sim.People.Act.Attack):F1}";
    Console.WriteLine($"  {b.Band.Name,-8} ({b.Band.Members.Count}): {drives} | {norms}");
}
Console.WriteLine($"Simulated in {timer.Elapsed.TotalSeconds:F1}s.");
if (Environment.GetEnvironmentVariable("ANTHRO_DEBUG_HUNTS") != null)
{
    foreach (var kv in Anthro.Sim.Activities.HuntParty.DebugEnds.OrderByDescending(k => k.Value)) Console.WriteLine($"  hunt: {kv.Key} {kv.Value}");
    var adults = region.People.Where(p => p.AgeYears(region.Clock) >= 16).ToList();
    var hunters = adults.Where(p => p.Expect.Has("Hunt")).ToList();
    var foragers = adults.Where(p => p.Expect.Has("Forage")).ToList();
    if (hunters.Count > 0 && foragers.Count > 0)
        Console.WriteLine($"  expected per hour: forage {foragers.Average(p => p.Expect.Get(Anthro.Sim.Activities.Verb.Forage, 0)):F2} ({foragers.Count} people), hunt {hunters.Average(p => p.Expect.Get(Anthro.Sim.Activities.Verb.Hunt, 0)):F2} ({hunters.Count} people)");
}
if (Anthro.Sim.Core.Profiler.Enabled) foreach (var line in Anthro.Sim.Core.Profiler.Report()) Console.WriteLine(line);
