using Anthro.Runner;
using Anthro.Sim;
using Anthro.Sim.Core;
using Anthro.Sim.People;

namespace Anthro.Tests;

public class SimulationTests
{
    [Fact]
    public void SameSeedReplaysTheSameHistory()
    {
        var a = Simulation.Create(7);
        var b = Simulation.Create(7);
        a.RunYears(5);
        b.RunYears(5);

        Assert.Equal(a.Chronicle.Entries.Select(e => e.ToString()), b.Chronicle.Entries.Select(e => e.ToString()));
        Assert.Equal(a.Band.Members.Count, b.Band.Members.Count);
    }

    [Fact]
    public void DifferentSeedsGiveDifferentHistories()
    {
        var a = Simulation.Create(1);
        var b = Simulation.Create(2);
        a.RunYears(3);
        b.RunYears(3);

        Assert.NotEqual(a.Chronicle.Entries.Select(e => e.Text), b.Chronicle.Entries.Select(e => e.Text));
    }

    [Fact]
    public void EveryoneIsInExactlyOnePlace()
    {
        // Seed 2 used to strand children who left with their family mid-hour back in the old band.
        // Now people also move between real bands: visiting, pairing, joining, striking out alone.
        var sim = Simulation.Create(2);
        for (int day = 0; day < 8 * 360; day++)
        {
            for (int h = 0; h < 24; h++) sim.Step();
            sim.CheckPlaces();
        }
    }

    [Fact]
    public void EveryoneLivesOnTheMap()
    {
        var region = Region.Create(9);
        region.RunYears(10);
        foreach (var p in region.People)
        {
            Assert.InRange(p.X, 0, region.World.Size);
            Assert.InRange(p.Y, 0, region.World.Size);
        }
        Assert.All(region.Bands, b => Assert.True(region.World.InBounds((int)b.Band.CampX, (int)b.Band.CampY)));
    }

    [Fact]
    public void EveryDeathHasACause()
    {
        var region = Region.Create(3);
        region.RunYears(15);
        var dead = region.AllBands.SelectMany(b => b.Band.Dead).ToList();

        Assert.NotEmpty(dead);
        Assert.All(dead, p => Assert.False(string.IsNullOrWhiteSpace(p.DeathCause)));
    }

    [Fact]
    public void TraitsStayInRangeAcrossGenerations()
    {
        var sim = Simulation.Create(4);
        sim.RunYears(20);

        foreach (var p in sim.Band.Members.Concat(sim.Band.Dead))
        {
            Assert.All(p.Mind, v => Assert.InRange(v, 0, 1));
            Assert.All(p.Body, v => Assert.InRange(v, 0, 1));
        }
    }

    [Fact]
    public void ChildrenInheritFromTheirParents()
    {
        // Across the whole map: children are born wherever their families have ended up.
        var region = Region.Create(5);
        region.RunYears(12);
        var born = region.AllBands.SelectMany(b => b.Band.Members.Concat(b.Band.Dead)).Distinct()
            .Where(p => p.Mother != null && p.Father != null).ToList();
        Assert.NotEmpty(born);

        // Inherited traits sit near the parents' average far more often than chance would allow.
        int close = born.Count(c => Math.Abs(c.Mind[0] - (c.Mother!.Mind[0] + c.Father!.Mind[0]) / 2) < 0.2);
        Assert.True(close > born.Count * 0.8, $"{close} of {born.Count} children were close to their parents' average");
    }

    [Fact]
    public void AnimalsKilledAreGoneForGood()
    {
        var sim = Simulation.Create(6);
        var deer = Anthro.Sim.Ecology.Species.Deer;
        int before = sim.Ecology.Away(deer) + sim.Ecology.Total(deer);
        var herd = sim.Ecology.Living(deer).First();
        herd.Count -= 3;
        Assert.Equal(before - 3, sim.Ecology.Away(deer) + sim.Ecology.Total(deer));
    }

    [Fact]
    public void PoissonArrivalsAverageToLambda()
    {
        var rng = new Rng(11);
        double mean = Enumerable.Range(0, 20000).Average(_ => rng.Poisson(4.0));
        Assert.InRange(mean, 3.9, 4.1);
    }

    [Fact]
    public void DiscoveryWebLoadsWithoutBrokenReferences()
    {
        var path = WebLoader.Find();
        Assert.NotNull(path);
        var web = WebLoader.Load(path!);

        Assert.Empty(web.Validate());
        Assert.Contains(web.KnownAtStart, s => s.Id == "fire.make");
        Assert.True(web.Stages.Count >= 90);
    }

    [Fact]
    public void FoundersStartWithTheBandsStartingKnowledge()
    {
        var web = WebLoader.Load(WebLoader.Find()!);
        var sim = Simulation.Create(8, web);
        var start = web.KnownAtStart.Select(s => s.Id).ToHashSet();

        Assert.All(sim.Band.Members, p => Assert.True(start.IsSubsetOf(p.Knows)));
    }

    [Fact]
    public void LandLeftAloneKeepsItsAnimals()
    {
        // No people: twenty years of weather, grazing, winters, births and wolves. Herds rise and fall
        // but nothing is wiped out and nothing runs far past what the land carries.
        var region = Region.Create(4);
        region.Bands.Clear();
        var w = region.World;
        var eco = region.Ecology;
        int lastYear = region.Clock.Year;
        var low = new Dictionary<Anthro.Sim.Ecology.Species, int>();
        while (region.Clock.Year < 20)
        {
            int doy = region.Clock.DayOfYear;
            foreach (var c in w.All()) c.DailyRegrow(w.Climate, doy);
            eco.DailyUpdate(region);
            for (int h = 0; h < SimClock.HoursPerDay; h++) region.Clock.Advance();
            if (region.Clock.Year != lastYear)
            {
                lastYear = region.Clock.Year;
                w.Climate.NewYear(region.Rng);
                foreach (var c in w.All()) c.Wet = w.Climate.Wetness(c.X, c.Y, w.Size);
                foreach (var sp in new[] { Anthro.Sim.Ecology.Species.Deer, Anthro.Sim.Ecology.Species.Boar, Anthro.Sim.Ecology.Species.Rabbit })
                {
                    int n = eco.Total(sp) + eco.Away(sp);
                    low[sp] = Math.Min(low.TryGetValue(sp, out var l) ? l : int.MaxValue, n);
                    Assert.True(n < sp.DensityPerKm2 * 1600 * 1.15, $"{sp.Name}: {n} in year {lastYear}");
                }
            }
        }
        foreach (var kv in low) Assert.True(kv.Value > kv.Key.DensityPerKm2 * 1600 * 0.1, $"{kv.Key.Name} fell to {kv.Value}");
    }

    [Fact]
    public void MarriagesAreMutualAndAmongTheLiving()
    {
        // A person may have several spouses (where their people come to accept it), but every marriage
        // is two-sided, never with the dead, and never between close kin.
        var region = Region.Create(4, kinCamps: 2);
        for (int year = 0; year < 12 && !region.Extinct; year++)
        {
            region.RunYears(1);
            foreach (var p in region.People)
                foreach (var s in p.Spouses)
                {
                    Assert.True(s.Alive, $"{p.Name} is married to the dead {s.Name}");
                    Assert.Contains(p, s.Spouses);
                    Assert.False(p.IsKin(s), $"{p.Name} married kin {s.Name}");
                    Assert.True(p.WedOn.ContainsKey(s));
                }
        }
    }

    [Fact]
    public void PeoplesStartWithTheirOwnCultures()
    {
        // Each people's culture of violence and everyday customs starts from its own history: two
        // peoples on one map shouldn't hold identical views.
        var region = Region.Create(3, otherBands: 1, kinCamps: 1);
        var tribes = region.LivingTribes.ToList();
        Assert.Equal(2, tribes.Count);
        double View(Tribe t, string topic) => region.CampsOf(t).SelectMany(c => c.Band.Members).Average(m => m.Expect.Get(topic, 0));
        var topics = new[] { "bound:neutral", "norm:honour", "norm:rationing", "norm:demand", "wed:more" };
        Assert.Contains(topics, t => Math.Abs(View(tribes[0], t) - View(tribes[1], t)) > 0.05);
    }

    [Fact]
    public void CampsBelongToAPeople()
    {
        // One people to start, in three camps. However camps split and merge, every camp belongs to a
        // people, and there are never more peoples than the cap.
        var region = Region.Create(5, kinCamps: 2);
        Assert.Single(region.LivingTribes);
        for (int year = 0; year < 15 && !region.Extinct; year++)
        {
            region.RunYears(1);
            Assert.All(region.Bands, b => Assert.NotNull(b.Tribe));
            Assert.True(region.LivingTribes.Count() <= Region.MaxTribes);
        }
    }

    [Fact]
    public void BeliefStateInitializesInheritsAndFades()
    {
        var sim = Simulation.Create(42);
        Assert.NotEmpty(sim.Band.Members);
        foreach (var p in sim.Band.Members)
        {
            Assert.InRange(Simulation.GodReal(p), 0.0, 1.0);
            Assert.InRange(Simulation.GodAnswers(p), 0.0, 1.0);
            Assert.InRange(Simulation.GodKind(p), -1.0, 1.0);
        }

        // Fading test: elevated belief decays toward baseline seed (~0.2) over time
        var person = sim.Band.Members[0];
        person.Expect.Absorb(Simulation.GodRealTopic, 0.9, 1.0, 0.2);
        Assert.True(Simulation.GodReal(person) > 0.8);
        double before = Simulation.GodReal(person);

        sim.RunYears(1);
        double after = Simulation.GodReal(person);
        Assert.True(after < before, $"Expected belief to fade from {before}, got {after}");
    }
}
