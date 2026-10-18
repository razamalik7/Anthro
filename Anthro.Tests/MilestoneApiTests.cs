using System;
using System.Linq;
using Anthro.Sim;
using Anthro.Sim.Api;
using Anthro.Sim.Core;
using Anthro.Sim.People;
using Xunit;

namespace Anthro.Tests;

public class MilestoneApiTests
{
    [Fact]
    public void MilestoneScenarioInitializesExpectedWorldAndViews()
    {
        var session = MilestoneScenario.StartNew(seed: 42, bandSize: 30, kinCamps: 2);

        var god = session.GetGodState();
        Assert.Equal(50.0, god.Favor, 3);
        Assert.Equal(0.0, god.GodHelp, 3);
        Assert.Equal(0.0, god.GodHarm, 3);
        Assert.False(god.GodFaded);

        var camps = session.GetCamps();
        Assert.Equal(3, camps.Count); // 1 founding band + 2 kin camps

        var people = session.GetPeople();
        Assert.True(people.Count >= 60);
        Assert.All(people, p =>
        {
            Assert.True(p.Alive);
            Assert.NotNull(p.Name);
            Assert.InRange(p.GodReal, 0.05, 0.40);
        });

        var herds = session.GetHerds();
        Assert.NotEmpty(herds);

        var cell = session.GetCell((int)camps[0].CampX, (int)camps[0].CampY);
        Assert.Equal((int)camps[0].CampX, cell.X);
        Assert.Equal((int)camps[0].CampY, cell.Y);
    }

    [Fact]
    public void TimeControlsAndNotificationsWorkThroughGameSession()
    {
        var session = MilestoneScenario.StartNew(seed: 42);
        session.HoursPerTick = 6;
        session.IsPaused = true;

        // When paused, StepTick does not advance
        Assert.Equal(0, session.StepTick());

        session.IsPaused = false;
        long before = session.Region.Clock.Tick;
        Assert.Equal(6, session.StepTick());
        Assert.Equal(before + 6, session.Region.Clock.Tick);

        var camp = session.GetCamps()[0];
        session.CastOmen((int)camp.CampX, (int)camp.CampY, "A golden eagle circled the fire", isGood: true);
        session.StepHours(1);

        var notes = session.DrainNotifications();
        Assert.Contains(notes, n => n.Kind == NotificationKind.PowerCast && n.Text.Contains("golden eagle"));
        Assert.Empty(session.DrainNotifications());
    }

    [Fact]
    public void AllPlayerPowersAndForkNudgeExecuteThroughSession()
    {
        var session = MilestoneScenario.StartNew(seed: 42);
        session.Region.Favor = 500.0;

        var camp = session.GetCamps()[0];
        int cx = (int)camp.CampX;
        int cy = (int)camp.CampY;

        // 1. Omen + Bounty + Weather
        session.CastOmen(cx, cy, "Starfall", isGood: true);
        session.CastBounty(cx, cy, animals: false);
        session.CastBounty(cx, cy, animals: true);
        session.CastWeather(Season.Winter, +0.4);
        session.StepHours(1);

        var godAfter = session.GetGodState();
        Assert.True(godAfter.GodHelp >= 3.5);
        Assert.True(godAfter.Favor < 500.0);

        // 2. Blessed Birth & Fork Nudge
        var adultWoman = session.GetPeople().First(p => p.Sex == Sex.Female && p.IsAdult);
        session.CastBlessedBirth(adultWoman.Id);
        session.CastForkNudge(adultWoman.Id, "marry:yes");
        session.StepHours(1);

        var updatedWoman = session.GetPerson(adultWoman.Id)!;
        Assert.True(updatedWoman.BlessedNextBirth);
        Assert.Equal("yes", updatedWoman.ActiveNudges["marry"]);

        // 3. Unlock Heal & Lightning when believers >= 5 and Help/Harm >= 4.0
        foreach (var p in session.Region.People.Take(8))
        {
            p.Expect.Absorb(Simulation.GodRealTopic, 0.85, 1.0, 0.2);
        }
        session.Region.GodHelp = 5.0;
        session.Region.GodHarm = 5.0;

        var targetPerson = session.Region.People.First(p => p.Alive && p.Id != adultWoman.Id);
        targetPerson.Injury = 0.5;
        targetPerson.Health = 0.5;

        session.CastHeal(targetPerson.Id);
        session.StepHours(1);
        Assert.Equal(0.0, targetPerson.Injury, 3);
        Assert.True(targetPerson.Health >= 0.85);

        var smiteTarget = session.Region.People.Last(p => p.Alive && p.Id != targetPerson.Id);
        session.CastLightning(smiteTarget.Id);
        session.StepHours(1);
        Assert.False(smiteTarget.Alive);
    }

    [Fact]
    public void SaveAndLoadProducesBitForBitIdenticalTrajectory()
    {
        var sessionA = MilestoneScenario.StartNew(seed: 77, bandSize: 28, kinCamps: 2);

        // Run 250 hours with several player interventions at different ticks
        sessionA.StepHours(50);
        var camp0 = sessionA.GetCamps()[0];
        sessionA.CastOmen((int)camp0.CampX, (int)camp0.CampY, "Morning sign", isGood: true);

        sessionA.StepHours(100);
        sessionA.CastBounty((int)camp0.CampX, (int)camp0.CampY, animals: false);

        var person0 = sessionA.GetPeople()[0];
        sessionA.CastForkNudge(person0.Id, "raid:no");

        sessionA.StepHours(100);

        // Save at hour 250
        byte[] bytes = GameSave.SaveToBytes(sessionA);
        var header = GameSave.ReadHeader(bytes);
        Assert.Equal(sessionA.Region.Clock.Tick, header.Tick);
        Assert.Equal(3, header.CommandCount);

        // Load into sessionB
        var sessionB = GameSave.LoadFromBytes(bytes);
        Assert.Equal(sessionA.Region.Clock.Tick, sessionB.Region.Clock.Tick);
        Assert.Equal(sessionA.Region.Favor, sessionB.Region.Favor, 9);
        Assert.Equal(sessionA.Region.GodHelp, sessionB.Region.GodHelp, 9);
        Assert.Equal(sessionA.Region.GodHarm, sessionB.Region.GodHarm, 9);

        // Step both forward another 250 hours (to 500 hours total) and compare state bit-for-bit
        sessionA.StepHours(250);
        sessionB.StepHours(250);

        Assert.Equal(sessionA.Region.Clock.Tick, sessionB.Region.Clock.Tick);
        Assert.Equal(sessionA.Region.Favor, sessionB.Region.Favor, 12);
        Assert.Equal(sessionA.Region.Rng.NextUInt(), sessionB.Region.Rng.NextUInt());
        Assert.Equal(sessionA.Region.GodRng.NextUInt(), sessionB.Region.GodRng.NextUInt());

        var peopleA = sessionA.GetPeople();
        var peopleB = sessionB.GetPeople();
        Assert.Equal(peopleA.Count, peopleB.Count);

        for (int i = 0; i < peopleA.Count; i++)
        {
            Assert.Equal(peopleA[i].Id, peopleB[i].Id);
            Assert.Equal(peopleA[i].X, peopleB[i].X, 9);
            Assert.Equal(peopleA[i].Y, peopleB[i].Y, 9);
            Assert.Equal(peopleA[i].Health, peopleB[i].Health, 9);
            Assert.Equal(peopleA[i].Satiety, peopleB[i].Satiety, 9);
            Assert.Equal(peopleA[i].GodReal, peopleB[i].GodReal, 9);
            Assert.Equal(peopleA[i].CurrentVerb, peopleB[i].CurrentVerb);
        }
    }

    [Fact]
    public void PrayerAndMisinterpretationLoopWorksThroughSession()
    {
        var session = MilestoneScenario.StartNew(seed: 42);
        var band = session.Region.Bands[0];
        var asker = band.Band.Members.First(band.Grown);

        // 1. Register an urgent hunger prayer and verify GetActivePrayers()
        asker.Reserve = 0.5;
        asker.Satiety = 0.25;
        band.RegisterPrayer(asker, PrayerNeed.Hunger);

        var openPrayers = session.GetActivePrayers();
        Assert.Single(openPrayers);
        Assert.Equal(asker.Id, openPrayers[0].AskerId);
        Assert.Equal(PrayerNeed.Hunger, openPrayers[0].Need);

        // 2. Player casts a good omen and feeds the asker -> prayer resolves as answered on the daily check
        double answersBefore = Simulation.GodAnswers(asker);
        session.CastOmen(openPrayers[0].CampX, openPrayers[0].CampY, "Rain on dry earth", isGood: true);
        session.StepHours(1);
        asker.Reserve = 15.0;
        asker.Satiety = 0.90;
        session.StepHours(24);

        Assert.Empty(session.GetActivePrayers());
        Assert.True(Simulation.GodAnswers(asker) > answersBefore);
        Assert.Contains(session.DrainNotifications(), n => n.Kind == NotificationKind.PrayerAnswered);

        // 3. Witnessed theft followed by wrathful omen lowers god:take in camp view
        double takeBefore = session.GetCamp(band.Id)!.MeanGodTake;
        band.HappenHere(new Deed { Day = session.Region.Clock.Day, Actor = asker, Act = Act.Take, Amount = 2.0 });
        session.CastOmen((int)band.Band.CampX, (int)band.Band.CampY, "Dark thunder over the thief", isGood: false);
        session.StepHours(1);

        double takeAfter = session.GetCamp(band.Id)!.MeanGodTake;
        Assert.True(takeAfter < takeBefore);
    }
}
