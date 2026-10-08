namespace Anthro.Sim.People
{
    /// <summary>
    /// A kind of sickness. These are world facts: how it spreads, how fast it worsens, how much it
    /// hurts, and how long the body remembers it afterward. Who catches it and who dies of it comes
    /// from people's contacts, immunity, age, nourishment and care.
    /// </summary>
    /// Severities are set so that, untreated, each kills about the share of cases it did before
    /// modern medicine: childhood diarrhea ~2-3% of toddler cases, pneumonia ~15% of toddlers and
    /// ~25-30% of the old, fevers ~5% of toddlers and ~1% of adults (checked by simulating courses).
    public sealed class Disease
    {
        public string Name = "";
        /// <summary>Used in "died of …".</summary>
        public string DiedOf = "";
        /// <summary>Chance per hour of close contact that a fully sick person passes it on. Zero = not catching.</summary>
        public double Contagion;
        /// <summary>How fast it worsens each day before the body's response catches up.</summary>
        public double Growth;
        /// <summary>Health lost per day at full severity.</summary>
        public double Harm;
        /// <summary>Protection left after recovering (0 to 1), and how fast it fades per day.</summary>
        public double ImmunityGain, ImmunityFade;
        /// <summary>Extra danger for small children (gut sickness is deadliest for the young).</summary>
        public double ChildRisk = 1;

        public static readonly Disease Cold = new Disease
        {
            Name = "cold", DiedOf = "a cold", Contagion = 0.06, Growth = 0.03, Harm = 0.02, ImmunityGain = 0.4, ImmunityFade = 0.01,
        };

        public static readonly Disease Fever = new Disease
        {
            Name = "fever", DiedOf = "fever", Contagion = 0.03, Growth = 0.07, Harm = 0.05, ImmunityGain = 0.9, ImmunityFade = 0.001,
        };

        public static readonly Disease Flux = new Disease
        {
            Name = "flux", DiedOf = "the flux", Contagion = 0.01, Growth = 0.05, Harm = 0.015, ImmunityGain = 0.3, ImmunityFade = 0.005, ChildRisk = 1.5,
        };

        public static readonly Disease WoundRot = new Disease
        {
            Name = "wound rot", DiedOf = "an infected wound", Growth = 0.08, Harm = 0.06,
        };

        public static readonly Disease BadMeat = new Disease
        {
            Name = "bad meat", DiedOf = "bad meat", Growth = 0.04, Harm = 0.04, ChildRisk = 1.5,
        };

        /// <summary>A complication: the already sick, chilled to the bone. What winter kills the old with.</summary>
        public static readonly Disease LungSickness = new Disease
        {
            Name = "lung sickness", DiedOf = "lung sickness", Contagion = 0.005, Growth = 0.08, Harm = 0.07, ImmunityGain = 0.2, ImmunityFade = 0.01,
        };

        public static readonly Disease[] Catching = { Cold, Fever, Flux, LungSickness };
    }
}
