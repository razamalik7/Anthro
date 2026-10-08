using System.Collections.Generic;

namespace Anthro.Sim.People
{
    /// <summary>
    /// The small social acts people can do to each other. There's no "steal" or "raid": taking food
    /// from a hearth that isn't yours is just Take, and what it means comes from who saw it and what
    /// they did about it.
    /// </summary>
    public enum Act { Ask, Give, Refuse, Take, Confront, Attack, Return }

    /// <summary>How the actor stands toward the person or household an act is aimed at.</summary>
    public enum Relation { Kin, Friend, Neutral, Distrusted, Outsider }

    /// <summary>Something someone did to someone: the unit of memory, gossip and learning.</summary>
    public sealed class Deed
    {
        public long Day;
        public Person Actor = null!;
        public Act Act;
        /// <summary>Who it was done to (for a take: an adult of the household taken from).</summary>
        public Person? Target;
        public Household? Hearth;
        public double Amount;
        /// <summary>What the deed has brought its doer so far: what it got them, plus everything that followed.</summary>
        public double Payoff;
        /// <summary>The learning topic for the actor: the act in its context ("act:take:neutral:seen").</summary>
        public string Context = "";
        /// <summary>A deed this one answers (a confrontation answers a take), so its consequences reach the right lesson.</summary>
        public Deed? Answers;
        /// <summary>Done away from camp (an ambush): only the people there know of it.</summary>
        public bool Far;
        /// <summary>Whether anyone besides the actor saw it.</summary>
        public bool Witnessed;
        /// <summary>Who has already answered this deed (refused, confronted, struck back): each counts against it once.</summary>
        public readonly HashSet<Person> AnsweredBy = new HashSet<Person>();
    }

    /// <summary>A deed as one person knows it: seen with their own eyes, or heard from someone.</summary>
    public readonly struct Remembered
    {
        public readonly Deed Deed;
        public readonly bool Firsthand;
        public Remembered(Deed deed, bool firsthand) { Deed = deed; Firsthand = firsthand; }
    }
}
