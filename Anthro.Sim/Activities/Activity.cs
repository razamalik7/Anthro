using Anthro.Sim.People;

namespace Anthro.Sim.Activities
{
    public enum Verb { Sleep, Rest, Socialize, Forage, Hunt, Knap, GatherFlint, Care, Play, WatchAndLearn, Eat, Cook, GatherWood, Preserve, Visit, Fish, Ask, Take, Confront, Attack, Rally, Raid, Trap, Pray }

    /// <summary>
    /// Something a person is doing, as a sequence of steps. <see cref="Step"/> names the current
    /// step so a viewer can pick the right animation (e.g. "stalk", "butcher").
    /// </summary>
    public abstract class Activity
    {
        protected readonly Person P;

        protected Activity(Person person) { P = person; }

        public abstract Verb Verb { get; }
        public string Step { get; protected set; } = "";
        public bool Done { get; protected set; }

        /// <summary>Whether a group activity (a hunt) may pull this person away.</summary>
        public virtual bool Interruptible => true;

        public virtual void Begin(Simulation sim) { }

        /// <summary>Advance one in-game hour.</summary>
        public abstract void Hour(Simulation sim);

        public override string ToString() => string.IsNullOrEmpty(Step) ? Verb.ToString() : $"{Verb} ({Step})";
    }

    /// <summary>A fixed-length activity at or near camp.</summary>
    public abstract class TimedActivity : Activity
    {
        int _hoursLeft;

        protected TimedActivity(Person person, int hours) : base(person) { _hoursLeft = hours; }

        public override void Hour(Simulation sim)
        {
            OnHour(sim);
            P.Fatigue = People.Inheritance.Clamp01(P.Fatigue + FatiguePerHour);
            if (--_hoursLeft <= 0) { OnEnd(sim); Done = true; }
        }

        protected virtual double FatiguePerHour => 0.03;
        protected abstract void OnHour(Simulation sim);
        protected virtual void OnEnd(Simulation sim) { }
    }
}
