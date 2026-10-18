using System;
using System.Collections.Generic;
using System.Linq;
using Anthro.Sim.Activities;
using Anthro.Sim.Core;
using Anthro.Sim.People;
using Anthro.Sim.World;

namespace Anthro.Sim.Api
{
    /// <summary>
    /// Four reporting bands for individual faith (D3 / GOD-LAYER).
    /// </summary>
    public enum BeliefBand
    {
        Skeptic,
        Open,
        Believer,
        Devout
    }

    public enum NotificationKind
    {
        ChronicleNotable,
        ChronicleMajor,
        PrayerAsked,
        PrayerAnswered,
        PrayerFailed,
        PrayerEmber,
        ForkBranch,
        PowerCast,
        GodFaded
    }

    public sealed class SimNotification
    {
        public long Tick { get; set; }
        public int Year { get; set; }
        public Season Season { get; set; }
        public int DayOfSeason { get; set; }
        public NotificationKind Kind { get; set; }
        public string Text { get; set; } = "";
        public int X { get; set; }
        public int Y { get; set; }
        public int FocalPersonId { get; set; } = -1;
        public int FocalBandId { get; set; } = -1;
    }

    public sealed class GodStateView
    {
        public long Tick { get; set; }
        public int Year { get; set; }
        public Season Season { get; set; }
        public int DayOfSeason { get; set; }
        public int Hour { get; set; }
        public double Favor { get; set; }
        public double SustainableFavor { get; set; }
        public double GodHelp { get; set; }
        public double GodHarm { get; set; }
        public bool GodFaded { get; set; }
        public int IgnoredEmbers { get; set; }
        public double BlessingCostMultiplier { get; set; }
        public double WrathCostMultiplier { get; set; }
        public double OmenCost { get; set; }
        public double BountyPlantCost { get; set; }
        public double BountyAnimalCost { get; set; }
        public double WeatherCost { get; set; }
        public double BlessedBirthCost { get; set; }
        public double ForkNudgeCost { get; set; }
        public double HealCost { get; set; }
        public double LightningCost { get; set; }
        public bool HealUnlocked { get; set; }
        public bool LightningUnlocked { get; set; }
    }

    public struct WorldCellView
    {
        public int X;
        public int Y;
        public Terrain Terrain;
        public double Elevation;
        public Feature Features;
        public PlantFood Plant;
        public double PlantStock;
        public double PlantCapacity;
        public double FishStock;
        public double FishCapacity;
        public double Wood;
        public double WoodCapacity;
        public double Weir;
        public double Snow;
        public double Temperature;
    }

    public struct HerdView
    {
        public int Id;
        public string SpeciesName;
        public int Count;
        public double X;
        public double Y;
        public double Condition;
        public bool Huntable;
    }

    public sealed class CampView
    {
        public int BandId { get; set; }
        public string Name { get; set; } = "";
        public string TribeName { get; set; } = "";
        public double CampX { get; set; }
        public double CampY { get; set; }
        public int Population { get; set; }
        public int Adults { get; set; }
        public double FoodTotal { get; set; }
        public double DaysOfFood { get; set; }
        public double Tools { get; set; }
        public double Firewood { get; set; }
        public int SkepticCount { get; set; }
        public int OpenCount { get; set; }
        public int BelieverCount { get; set; }
        public int DevoutCount { get; set; }
        public double MeanGodReal { get; set; }
        public double MeanGodAnswers { get; set; }
        public double MeanGodKind { get; set; }
        public double MeanGodGive { get; set; }
        public double MeanGodTake { get; set; }
        public double MeanGodAttack { get; set; }
        public double MeanGodOffer { get; set; }
        public double MeanGodThank { get; set; }
        public IReadOnlyList<string> KnownTechs { get; set; } = Array.Empty<string>();
    }

    public sealed class PersonView
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public Sex Sex { get; set; }
        public double AgeYears { get; set; }
        public bool IsAdult { get; set; }
        public bool Alive { get; set; }
        public int BandId { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
        public Verb CurrentVerb { get; set; }
        public string CurrentStep { get; set; } = "";
        public double Health { get; set; }
        public double Fatigue { get; set; }
        public double Injury { get; set; }
        public double Sickness { get; set; }
        public string? IllnessName { get; set; }
        public double Satiety { get; set; }
        public double Stomach { get; set; }
        public double Reserve { get; set; }
        public double ColdStress { get; set; }
        public double Discontent { get; set; }
        public double Dread { get; set; }
        public bool Blessed { get; set; }
        public bool BlessedNextBirth { get; set; }
        public BeliefBand BeliefBand { get; set; }
        public double GodReal { get; set; }
        public double GodAnswers { get; set; }
        public double GodKind { get; set; }
        public double GodGive { get; set; }
        public double GodTake { get; set; }
        public double GodAttack { get; set; }
        public double GodOffer { get; set; }
        public double GodThank { get; set; }
        public double Credulity { get; set; }
        public double Warmth { get; set; }
        public double Boldness { get; set; }
        public int MotherId { get; set; } = -1;
        public int FatherId { get; set; } = -1;
        public IReadOnlyList<int> SpouseIds { get; set; } = Array.Empty<int>();
        public IReadOnlyList<int> ChildIds { get; set; } = Array.Empty<int>();
        public IReadOnlyDictionary<string, string> ActiveNudges { get; set; } = new Dictionary<string, string>();
    }

    public sealed class PrayerView
    {
        public int Id { get; set; }
        public int AskerId { get; set; }
        public string AskerName { get; set; } = "";
        public int BandId { get; set; }
        public int CampX { get; set; }
        public int CampY { get; set; }
        public PrayerNeed Need { get; set; }
        public int SickTargetId { get; set; } = -1;
        public long StartDay { get; set; }
        public bool IsEmber { get; set; }
        public bool HasOffering { get; set; }
        public bool PowerCasted { get; set; }
        public double InitialSeverity { get; set; }
    }
}
