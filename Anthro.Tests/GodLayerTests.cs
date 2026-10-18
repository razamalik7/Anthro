using System;
using System.Linq;
using Anthro.Runner;
using Anthro.Sim;
using Anthro.Sim.Core;
using Anthro.Sim.People;
using Xunit;

namespace Anthro.Tests;

public class GodLayerTests
{
    [Fact]
    public void GodHelpHarmSplitWorks()
    {
        var sim = Simulation.Create(42);
        var region = sim.Region;

        Assert.Equal(0.0, region.GodHelp);
        Assert.Equal(0.0, region.GodHarm);
        Assert.Equal(0.0, region.Reputation);

        // Add help and harm
        region.GodHelp = 4.5;
        region.GodHarm = 2.0;

        Assert.Equal(4.5, region.GodHelp);
        Assert.Equal(2.0, region.GodHarm);
        Assert.Equal(2.5, region.Reputation);
    }

    [Fact]
    public void PatternBotsPolicyParsing()
    {
        Assert.Equal(GodPolicy.Charity, BotGod.FromString("charity"));
        Assert.Equal(GodPolicy.Demanding, BotGod.FromString("demanding"));
        Assert.Equal(GodPolicy.Malevolent, BotGod.FromString("malevolent"));
        Assert.Equal(GodPolicy.Stern, BotGod.FromString("stern"));
        Assert.Equal(GodPolicy.Capricious, BotGod.FromString("capricious"));
        Assert.Equal(GodPolicy.Skilful, BotGod.FromString("skilful"));
        Assert.Equal(GodPolicy.Quiet, BotGod.FromString("quiet"));
    }

    [Fact]
    public void PatternBotsExecuteWithoutErrors()
    {
        var policies = new[]
        {
            GodPolicy.Charity,
            GodPolicy.Demanding,
            GodPolicy.Malevolent,
            GodPolicy.Stern,
            GodPolicy.Capricious
        };

        foreach (var policy in policies)
        {
            var sim = Simulation.Create(42);
            sim.Region.Policy = policy;
            // Step 48 hours to exercise bot step logic
            for (int h = 0; h < 48; h++)
            {
                sim.Step();
            }
            Assert.True(sim.Region.People.Any());
        }
    }

    [Fact]
    public void WitnessedDeedsTrackedInHappen()
    {
        var sim = Simulation.Create(42);
        var actor = sim.Band.Members.First();
        var deed = new Deed
        {
            Actor = actor,
            Act = Act.Give,
            Day = sim.Clock.Day,
            Amount = 1.0
        };

        sim.HappenHere(deed);

        Assert.Equal(sim.Clock.Tick, sim.LastWitnessedGiveTick);
        Assert.Equal(sim.Clock.Day, sim.LastGiveDay);
        Assert.NotNull(sim.LastWitnessedDeed);
        Assert.Equal(Act.Give, sim.LastWitnessedDeed!.Act);
    }
}
