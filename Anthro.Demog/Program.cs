using Anthro.Runner;
using Anthro.Sim;
using Anthro.Sim.Core;
using Anthro.Sim.People;
using System.Text.RegularExpressions;

ulong seed = ulong.Parse(args[0]); int years = int.Parse(args[1]);
// The discovery web lives in docs/ at the repo root: look upward from wherever Demog runs.
string webPath = Environment.GetEnvironmentVariable("ANTHRO_WEB") ?? "";
for (var dir = new DirectoryInfo(AppContext.BaseDirectory); webPath == "" && dir != null; dir = dir.Parent)
    if (File.Exists(Path.Combine(dir.FullName, "docs", "discovery-web.json"))) webPath = Path.Combine(dir.FullName, "docs", "discovery-web.json");
var web = WebLoader.Load(webPath);
var region = Region.Create(seed, web, otherBands: args.Length > 4 ? int.Parse(args[4]) : 0, kinCamps: args.Length > 2 ? int.Parse(args[2]) : 0);
if (args.Length > 3 && args[3] != "-") foreach (var m in region.People) foreach (var g in args[3].Split(',')) m.Knows.Add(g);
if (args.Length > 5 && args[5] != "-") region.Policy = BotGod.FromString(args[5]);
if (years == 0)
{
    // What the land could give each season at most: regrowth of 5%/day of the gap to the seasonal target,
    // if every cell were picked bare daily. Food value weighted by richness. Plus fish and game.
    foreach (var season in new[] { Season.Spring, Season.Summer, Season.Autumn, Season.Winter })
    {
        double plants = 0, fish = 0;
        foreach (var c in region.World.All())
        {
            plants += c.PlantCapacity * Anthro.Sim.World.Cell.SeasonFactor(c.Plant, season) * 0.05 * Anthro.Sim.World.Cell.Richness(c.Plant);
            fish += c.FishCapacity * 0.0005;
        }
        Console.WriteLine($"{season}: max plant regrowth {plants:F0} rations/day, resident-fish regrowth ~{fish:F0}/day");
    }
    var cnt = region.World.All().GroupBy(c => c.Terrain).Select(g => $"{g.Key} {g.Count()}");
    Console.WriteLine(string.Join(", ", cnt));
    foreach (var sp in Anthro.Sim.Ecology.Species.All) Console.WriteLine($"{sp.Name}: density {sp.DensityPerKm2}/km2 meat {sp.Meat} birth {sp.BirthRate}");
    return;
}
int partings = 0, leftFor = 0, splits = 0, moves = 0, plurals = 0, campMoves = 0, feasts = 0, fights = 0, peace = 0, routs = 0;
var watched = new HashSet<Simulation>();
string? chron = Environment.GetEnvironmentVariable("ANTHRO_CHRON");
void Watch()
{
    foreach (var b in region.Bands.Where(b => watched.Add(b)))
        b.Chronicle.Written += e => { if (chron != null && Regex.IsMatch(e.Text, chron)) Console.WriteLine($"  C Y{region.Clock.Year} {region.Clock.Season} d{region.Clock.DayOfSeason} [{b.Band.Name}] {e.Text}");  if (e.Text.Contains("moved camp")) campMoves++; if (e.Text.Contains("feast")) feasts++; if (e.Text.Contains(" fought ")) fights++; if (e.Text.Contains("stepped in")) peace++; if (e.Text.Contains("broke and ran")) routs++; if (e.Text.Contains(" as a second ") || e.Text.Contains(" as a third ") || e.Text.Contains(" as another ")) plurals++; if (e.Text.Contains(" parted.")) partings++; else if (Regex.IsMatch(e.Text, @" left \w+ for ")) leftFor++; if (e.Text.Contains("split the band:") && !e.Text.Contains(" split the ")) { } if (e.Text.Contains("to make a camp of their own")) splits++; if (e.Text.Contains(" came to live with the band")) moves++; };
}
Watch();
var yearly = new List<string>();
var seasonFed = new Dictionary<string, List<double>>();
bool trace = Environment.GetEnvironmentVariable("ANTHRO_TRACE") != null;
var lastHours = new Dictionary<Simulation, Dictionary<Anthro.Sim.Activities.Verb, long>>();
var lastGot = new Dictionary<Simulation, double>();
region.YearEnded += all =>
{
    Watch();
    int pop = region.People.Count();
    long now0 = region.Clock.Day;
    int births = all.Sum(a => a.year.Births); int deaths = all.Sum(a => a.year.Deaths.Values.Sum());
    double sat = all.Where(a => a.year.AvgSatiety > 0).Select(a => a.year.AvgSatiety).DefaultIfEmpty(0).Average();
    double need = all.Sum(a => a.year.Needed), got = all.Sum(a => a.year.Gathered + a.year.Hunted + a.year.Fished);
    int starv = all.Sum(a => a.year.Deaths.Where(d => d.Key.StartsWith("starv")).Sum(d => d.Value));
    int viol = all.Sum(a => a.year.Deaths.Where(d => d.Key.StartsWith("killed by") && !d.Key.Contains(" a ") && !Anthro.Sim.Ecology.Species.All.Any(s => d.Key.EndsWith(s.Name))).Sum(d => d.Value));
    Console.Error.WriteLine($"[{seed}] Y{region.Clock.Year} pop {pop}");
    if (chron != null) foreach (var (sim, yr) in all) foreach (var kv in yr.Deaths) Console.WriteLine($"  D Y{region.Clock.Year} [{sim.Band.Name}] {kv.Key} x{kv.Value}");
    // Food accounting for the year: hours and returns by activity, kills, spoilage, animals left, seasonal hunger.
    var hours = new Dictionary<Anthro.Sim.Activities.Verb, long>();
    foreach (var (_, y) in all) foreach (var h in y.Hours) hours[h.Key] = hours.TryGetValue(h.Key, out var v) ? v + h.Value : h.Value;
    long H(Anthro.Sim.Activities.Verb v) => hours.TryGetValue(v, out var x) ? x : 0;
    double gat = all.Sum(a => a.year.Gathered), hun = all.Sum(a => a.year.Hunted), fis = all.Sum(a => a.year.Fished);
    var kills = new Dictionary<string, int>();
    foreach (var (_, y) in all) foreach (var k in y.Kills) kills[k.Key] = kills.TryGetValue(k.Key, out var v) ? v + k.Value : k.Value;
    int adults = region.People.Count(p => p.AgeYears(region.Clock) >= 15);
    long foodHours = H(Anthro.Sim.Activities.Verb.Forage) + H(Anthro.Sim.Activities.Verb.Hunt) + H(Anthro.Sim.Activities.Verb.Fish) + H(Anthro.Sim.Activities.Verb.Trap);
    var eco = region.Founders.Ecology;
    string Animals(Anthro.Sim.Ecology.Species sp) => $"{sp.Name} map {eco.Total(sp)} away {eco.Away(sp)}";
    Console.WriteLine($"  FOOD Y{region.Clock.Year}: need {need:F0}; gathered {gat:F0} ({gat / Math.Max(1, H(Anthro.Sim.Activities.Verb.Forage)):F2}/h over {H(Anthro.Sim.Activities.Verb.Forage)} h), hunted+trapped {hun:F0} ({hun / Math.Max(1, H(Anthro.Sim.Activities.Verb.Hunt) + H(Anthro.Sim.Activities.Verb.Trap)):F2}/h over {H(Anthro.Sim.Activities.Verb.Hunt)}+{H(Anthro.Sim.Activities.Verb.Trap)} h), fished {fis:F0} ({fis / Math.Max(1, H(Anthro.Sim.Activities.Verb.Fish)):F2}/h over {H(Anthro.Sim.Activities.Verb.Fish)} h); eaten {all.Sum(a => a.year.Eaten):F0} spoiled {all.Sum(a => a.year.Spoiled):F0}; food-getting h/adult/day {foodHours / (double)Math.Max(1, adults) / SimClock.DaysPerYear:F1}");
    Console.WriteLine($"    kills: {string.Join(", ", kills.OrderByDescending(k => k.Value).Select(k => $"{k.Key} {k.Value}"))} | {string.Join("; ", Anthro.Sim.Ecology.Species.All.Select(Animals))}");
    Console.WriteLine("    deer books: " + string.Join(", ", eco.Tally.Where(kv => kv.Key.StartsWith("deer") || kv.Key.StartsWith("boar")).Select(kv => $"{kv.Key} {kv.Value}")));
    eco.Tally.Clear();
    Console.WriteLine($"    hours: {string.Join(", ", hours.OrderByDescending(h => h.Value).Select(h => $"{h.Key} {h.Value / (double)Math.Max(1, adults) / SimClock.DaysPerYear:F2}"))} (per adult per day)");
    var known = region.People.SelectMany(p => p.Knows).GroupBy(k => k).Where(g => web.Stages.TryGetValue(g.Key, out var st) && !st.Known).Select(g => $"{g.Key} {g.Count() * 100 / Math.Max(1, pop)}%");
    if (region.Clock.Year % 10 == 0) Console.WriteLine($"    known: {string.Join(", ", known)}");
    if (region.Clock.Year % 10 == 0 || region.Clock.Year == 1)
        foreach (var t in region.LivingTribes)
        {
            var ms = region.CampsOf(t).SelectMany(c => c.Band.Members).Where(m => (now0 - m.BirthDay) / SimClock.DaysPerYear >= 15).ToList();
            if (ms.Count == 0) continue;
            double A(string k, double f) => ms.Average(m => m.Expect.Get(k, f));
            string godCulture = Simulation.Off("god") ? "" : $" | belief {ms.Count(Simulation.IsBeliever)}/{ms.Count} mean {ms.Average(Simulation.GodReal):F2} answers {ms.Average(Simulation.GodAnswers):F2} kind {ms.Average(Simulation.GodKind):F2} | god acts take {A("god:take", 0):F2} give {A("god:give", 0):F2} attack {A("god:attack", 0):F2} offer {A("god:offer", 0):F2}";
            Console.WriteLine($"    culture [{t.Name}]: violence kin {A("bound:kin", 0.8):F2} friend {A("bound:friend", 0.8):F2} stranger {A("bound:neutral", 0.8):F2} outsider {A("bound:outsider", 0.8):F2} | honour {A("norm:honour", 0):F2} peacemaking {A("norm:peacemaking", 0.5):F2} | rationing {A("norm:rationing", 0.6):F2} demand {A("norm:demand", 0.3):F2} elders {A("norm:elders", 1):F2} hunting {A("norm:hunting", 0.4):F2} plural {A("wed:more", 0):F2}" + godCulture);
        }
    Console.WriteLine($"    seasons fed (mean/low10%): {string.Join(" ", seasonFed.Select(kv => $"{kv.Key} {kv.Value.Average():F2}/{kv.Value.OrderBy(x => x).ElementAt(kv.Value.Count / 10):F2}"))}");
    seasonFed.Clear();
    string godY = Simulation.Off("god") ? "" : $" | prayers {all.Sum(a => a.year.PrayersAsked)}/{all.Sum(a => a.year.PrayersResolvedGood)}/{all.Sum(a => a.year.PrayersResolvedBad)} offered {all.Sum(a => a.year.FoodOffered):F1} favor {region.Favor:F1} rep {region.Reputation:F1}";
    Console.WriteLine($"Y{region.Clock.Year,3} pop {pop,4} bands {region.Bands.Count,2} tiny {region.Bands.Count(b => b.Band.Members.Count < 6),2} splits {splits / 2,3} moves {moves,4} dogs {region.Bands.Sum(b => b.Band.Dogs),3} peoples {region.LivingTribes.Count()} camps/people {string.Join("/", region.LivingTribes.Select(t => region.CampsOf(t).Count()))} sizes {string.Join("/", region.Bands.OrderByDescending(b => b.Band.Members.Count).Select(b => b.Band.Members.Count))} b {births,3} d {deaths,3} starv {starv,2} viol {viol,2} fed {sat:F2} food/need {(need > 0 ? got / need : 0):F2} hunted/need {(need > 0 ? all.Sum(a => a.year.Hunted) / need : 0):F2} fished/need {(need > 0 ? all.Sum(a => a.year.Fished) / need : 0):F2} | asked other camps {all.Sum(a => a.year.GuestAsks)} gave {all.Sum(a => a.year.GuestGifts)} carried {all.Sum(a => a.year.GuestFood):F0} abandoned {all.Sum(a => a.year.Abandoned):F0} fightkinds {string.Join(",", all.SelectMany(a => a.year.FightsBy).GroupBy(k => k.Key).Select(g => $"{g.Key}:{g.Sum(x => x.Value)}/{all.Sum(a => a.year.FightDeathsBy.TryGetValue(g.Key, out var d) ? d : 0)}"))} raids {all.Sum(a => a.year.Raids)} | settled: days-at-site {region.Bands.Select(b => b.DaysHere).DefaultIfEmpty(0).Average():F0} (max {region.Bands.Select(b => b.DaysHere).DefaultIfEmpty(0).Max():F0}) weirs {region.World.All().Count(c => c.Weir >= 0.5)} | rationlessons up {all.Sum(a => a.year.RationUp)} down {all.Sum(a => a.year.RationDown)} held {all.Sum(a => a.year.RationHeld)} none {all.Sum(a => a.year.RationNone)} | campmoves {campMoves} feasts {feasts} fights {fights} peacemade {peace} routs {routs}" + godY); campMoves = feasts = fights = peace = routs = 0; Console.Out.Flush();
};
for (int done = 0; done < years; done += 20)
{
    long end = region.Clock.Tick + (long)Math.Min(20, years - done) * SimClock.HoursPerDay * SimClock.DaysPerYear;
    while (region.Clock.Tick < end && !region.Extinct)
    {
        region.Step();
        if (region.Clock.Hour == 0) Watch();
        // Sample everyone's nourishment at noon every 5 days, by season.
        if (region.Clock.Hour == 12 && region.Clock.Day % 5 == 0)
        {
            var key = region.Clock.Season.ToString();
            if (!seasonFed.TryGetValue(key, out var list)) seasonFed[key] = list = new List<double>();
            foreach (var p in region.People) list.Add(p.Satiety);
        }
        // Weekly trace through late winter and spring (ANTHRO_TRACE): what each camp has and does.
        if (trace && region.Clock.Hour == 12 && region.Clock.Day % 7 == 0
            && (region.Clock.Season == Season.Spring || (region.Clock.Season == Season.Winter && region.Clock.DayOfSeason >= 45)))
            foreach (var b in region.Bands.Where(b => b.Band.Members.Count >= 5))
            {
                var hrs = b.ThisYear.Hours.ToDictionary(k => k.Key, k => k.Value);
                lastHours.TryGetValue(b, out var prev); lastHours[b] = hrs;
                int ad = b.Band.Members.Count(m => m.AgeYears(region.Clock) >= 14);
                string top = prev == null ? "" : string.Join(" ", hrs.Select(k => (k.Key, v: k.Value - (prev.TryGetValue(k.Key, out var x) ? x : 0)))
                    .Where(k => k.v > 0).OrderByDescending(k => k.v).Take(7).Select(k => $"{k.Key} {k.v / 7.0 / Math.Max(1, ad):F1}"));
                double gath = b.ThisYear.Gathered + b.ThisYear.Hunted + b.ThisYear.Fished;
                lastGot.TryGetValue(b, out var pg); lastGot[b] = gath;
                var near = region.World.Around(b.Band.CampX, b.Band.CampY, Anthro.Sim.World.WorldMap.FromMeters(3000)).ToList();
                var f = b.Band.Households.Select(h => h.Food).ToList();
                Console.WriteLine($"  T Y{region.Clock.Year} {region.Clock.Season} d{region.Clock.DayOfSeason,2} {b.Band.Name,-10} n{b.Band.Members.Count,3} sat {b.Band.Members.Average(m => m.Satiety):F2} fat {b.Band.Members.Where(m => m.AgeYears(region.Clock) >= 15).Select(m => Physiology.BodyFat(m, m.AgeYears(region.Clock))).DefaultIfEmpty(0).Average():F2} "
                    + $"T {b.CampCell.Temperature,5:F1} snow {b.CampCell.Snow,4:F0} access {near.Average(c => c.SnowAccess):F2} plants3km {near.Sum(c => c.PlantStock):F0} "
                    + $"food st {f.Sum(x => x.Stored):F0} dr {f.Sum(x => x.Dried):F0} mt {f.Sum(x => x.Meat):F0} pl {f.Sum(x => x.Plants):F0} | got/wk {gath - pg:F0} need/wk {b.Band.Members.Count * 7 * 0.9:F0} | {top}");
            }
    }
    Report(); Console.Out.Flush();
}
void Report() {
int Y = region.Clock.Year; double DPY = SimClock.DaysPerYear;
var everyone = region.AllBands.SelectMany(b => b.Band.Members.Concat(b.Band.Dead)).Distinct().ToList();
double Age(Person p, long day) => (day - p.BirthDay) / DPY;
long now = region.Clock.Day;
long from = now - (long)(Y / 2) * (long)DPY;
int[] edges = { 0, 1, 5, 10, 15, 20, 25, 30, 35, 40, 45, 50, 55, 60, 65, 70, 75, 80, 200 };
var expo = new double[edges.Length - 1]; var dth = new int[edges.Length - 1];
var causes = new Dictionary<string, int[]>();
foreach (var p in everyone)
{
    long s = Math.Max(from, Math.Max(0, p.BirthDay)); long e = p.Alive ? now : p.DeathDay;
    if (e <= s) continue;
    for (int i = 0; i < expo.Length; i++)
    {
        double a0 = p.BirthDay + edges[i] * DPY, a1 = p.BirthDay + edges[i + 1] * DPY;
        double o = Math.Min(e, a1) - Math.Max(s, a0); if (o > 0) expo[i] += o / DPY;
    }
    if (!p.Alive && p.DeathDay >= from)
    {
        double a = Age(p, p.DeathDay); int i = Array.FindLastIndex(edges, x => x <= a); if (i >= expo.Length) i = expo.Length - 1;
        dth[i]++;
        int g = a < 5 ? 0 : a < 15 ? 1 : a < 45 ? 2 : 3;
        var c = p.DeathCause ?? "?";
        if (!causes.TryGetValue(c, out var arr)) causes[c] = arr = new int[4];
        arr[g]++;
    }
}
Console.WriteLine($"\nLIFE TABLE (years {Y - Y / 2}-{Y}):  age  exposure  deaths  m(x)/1000  q(x)  l(x)");
double l = 1, e0 = 0; var lx = new List<double>(); var Lx = new List<double>();
for (int i = 0; i < expo.Length; i++)
{
    double m = expo[i] > 0 ? dth[i] / expo[i] : 0; int n = edges[i + 1] - edges[i];
    double q = i == expo.Length - 1 ? 1 : 1 - Math.Exp(-m * n);
    Console.WriteLine($"  {edges[i],3}-{edges[i + 1] - 1,-3} {expo[i],8:F0} {dth[i],6} {m * 1000,9:F1} {q,6:F3} {l,6:F3}");
    lx.Add(l);
    double L = i == expo.Length - 1 ? (m > 0 ? l / m : l * 10) : (m > 0 ? l * q / m : l * n);
    Lx.Add(L); e0 += L; l *= 1 - q;
}
double e15 = Lx.Skip(4).Sum() / lx[4];
Console.WriteLine($"  e0 = {e0:F1}; e15 = {e15:F1}; l(15) = {lx[4]:F2}; l(45) = {lx[10]:F2}");
Console.WriteLine("CAUSES (deaths in window)  0-4 / 5-14 / 15-44 / 45+:");
foreach (var kv in causes.OrderByDescending(k => k.Value.Sum())) Console.WriteLine($"  {kv.Key,-30} {string.Join(" / ", kv.Value)}");

int[] fEdges = { 15, 20, 25, 30, 35, 40, 45, 50 }; var fExpo = new double[7]; var fBirths = new int[7];
var fExpoP = new double[7];
var firstBirth = new List<double>(); var ibi = new List<double>(); var ibiSurv = new List<double>(); var completed = new List<int>();
foreach (var w in everyone.Where(p => p.Sex == Sex.Female))
{
    long s = Math.Max(from, Math.Max(0, w.BirthDay)); long e = w.Alive ? now : w.DeathDay;
    for (int i = 0; i < 7; i++) { double a0 = w.BirthDay + fEdges[i] * DPY, a1 = w.BirthDay + fEdges[i + 1] * DPY; double o = Math.Min(e, a1) - Math.Max(s, a0); if (o > 0) fExpo[i] += o / DPY; }
    var kids = w.Children.Where(c => c.Mother == w).OrderBy(c => c.BirthDay).ToList();
    foreach (var c in kids) if (c.BirthDay >= from) { double a = Age(w, c.BirthDay); int i = Array.FindLastIndex(fEdges, x => x <= a); if (i >= 0 && i < 7) fBirths[i]++; }
    if (w.BirthDay >= 0 && kids.Count > 0 && kids[0].BirthDay >= 0) firstBirth.Add(Age(w, kids[0].BirthDay));
    for (int i = 1; i < kids.Count; i++)
    {
        if (kids[i - 1].BirthDay < 0) continue;
        double gap = (kids[i].BirthDay - kids[i - 1].BirthDay) / DPY; ibi.Add(gap);
        var prev = kids[i - 1]; if (prev.Alive || Age(prev, prev.DeathDay) > gap) ibiSurv.Add(gap);
    }
    if (w.BirthDay >= 0 && (w.Alive ? Age(w, now) : Age(w, w.DeathDay)) >= 45) completed.Add(kids.Count);
}
double tfr = 0; Console.WriteLine("\nFERTILITY  age  woman-yrs births  rate");
for (int i = 0; i < 7; i++) { double r = fExpo[i] > 0 ? fBirths[i] / fExpo[i] : 0; tfr += r * 5; Console.WriteLine($"  {fEdges[i]}-{fEdges[i + 1] - 1}  {fExpo[i],8:F0} {fBirths[i],6} {r,6:F3}"); }
Console.WriteLine($"  TFR {tfr:F2}; first birth median {Med(firstBirth):F1} (n {firstBirth.Count}); interbirth median {Med(ibi):F1} (n {ibi.Count}), after a surviving child {Med(ibiSurv):F1}; completed family (women born in-sim reaching 45) {(completed.Count > 0 ? completed.Average() : 0):F1} (n {completed.Count})");
static double Med(List<double> x) { if (x.Count == 0) return 0; var s = x.OrderBy(v => v).ToList(); return s[s.Count / 2]; }

var adultsNow = region.People.Where(p => Age(p, now) >= 15).ToList();
var womenRepro = adultsNow.Where(p => p.Sex == Sex.Female && Age(p, now) < 45).ToList();
double Part(int a, int b) { var g = womenRepro.Where(w => Age(w, now) >= a && Age(w, now) < b).ToList(); return g.Count == 0 ? 0 : g.Count(w => w.Partner != null) / (double)g.Count; }
Console.WriteLine($"\nPAIRING now: women 15-44 partnered {womenRepro.Count(w => w.Partner != null) / (double)Math.Max(1, womenRepro.Count):P0} (15-19 {Part(15, 20):P0}, 20-24 {Part(20, 25):P0}, 25-34 {Part(25, 35):P0}, 35-44 {Part(35, 45):P0}); men 15-59 partnered {adultsNow.Where(p => p.Sex == Sex.Male && Age(p, now) < 60).Select(p => p.Partner != null ? 1.0 : 0).DefaultIfEmpty(0).Average():P0}");
var fathers = everyone.Where(p => p.Sex == Sex.Female).Select(w => w.Children.Where(c => c.Mother == w && c.Father != null).Select(c => c.Father.Id).Distinct().Count()).Where(n => n > 0).ToList();
int unions = everyone.Count(p => p.PairedDay > 0) / 2;
var married = adultsNow.Where(p => p.Married).ToList();
var hubs = married.Where(p => p.Spouses.Count > 1).ToList();
Console.WriteLine($"  PLURAL now: {hubs.Count(h => h.Sex == Sex.Male)} men with >1 wife, {hubs.Count(h => h.Sex == Sex.Female)} women with >1 husband (of {married.Count} married adults); view of it avg {adultsNow.Select(p => p.Expect.Get("wed:more", 0)).DefaultIfEmpty(0).Average():F2} by camp {string.Join("/", region.Bands.Where(b => b.Band.Members.Any()).Select(b => b.Band.Members.Average(m => m.Expect.Get("wed:more", 0)).ToString("F2")))}; taken over run {plurals}");
Console.WriteLine($"  breakups over run: {partings} parted + {leftFor} left for someone else; mothers with children by >1 father {fathers.Count(n => n > 1)}/{fathers.Count}");
var ages = region.People.Select(p => Age(p, now)).ToList();
Console.WriteLine($"AGE STRUCTURE now ({ages.Count}): <5 {ages.Count(a => a < 5) / (double)ages.Count:P0}, <15 {ages.Count(a => a < 15) / (double)ages.Count:P0}, 15-44 {ages.Count(a => a >= 15 && a < 45) / (double)ages.Count:P0}, 45+ {ages.Count(a => a >= 45) / (double)ages.Count:P0}, 60+ {ages.Count(a => a >= 60) / (double)ages.Count:P0}; density {ages.Count / 1600.0:F3}/km2; bands {region.Bands.Count}, mean size {ages.Count / (double)Math.Max(1, region.Bands.Count):F0}");
if (!Simulation.Off("god"))
{
    double ySim = Math.Max(1.0, region.Clock.Year);
    double pctBroke = region.DaysTotal > 0 ? (double)region.DaysBroke / region.DaysTotal : 0.0;
    var spentList = region.FavorSpentByPower.OrderBy(kv => kv.Key).Select(kv => $"{kv.Key}:{kv.Value / ySim:F2}");
    Console.WriteLine($"FAVOR_AUDIT bot {region.Policy} earned/yr prayer:{region.FavorEarnedPrayer/ySim:F2} offer:{region.FavorEarnedOffer/ySim:F2} rite:{region.FavorEarnedRite/ySim:F2} other:{region.FavorEarnedOther/ySim:F2} | spent/yr {(spentList.Any() ? string.Join(" ", spentList) : "none")} | broke_pct {pctBroke*100:F1}% first_broke_day {region.FirstBrokeDay}");
}
}
