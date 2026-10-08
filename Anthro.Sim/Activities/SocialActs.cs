using Anthro.Sim.People;

namespace Anthro.Sim.Activities
{
    /// <summary>Going to another hearth and asking for food. They answer as they choose.</summary>
    public sealed class AskFor : TimedActivity
    {
        readonly Household _hearth;
        readonly double _amount;
        public AskFor(Person p, Household hearth, double amount) : base(p, 1) { _hearth = hearth; _amount = amount; Step = "asking for food"; }
        public override Verb Verb => Verb.Ask;
        protected override double FatiguePerHour => 0;
        protected override void OnHour(Simulation sim) { }
        protected override void OnEnd(Simulation sim)
        {
            if (sim.Band.Households.Contains(_hearth)) sim.DoAsk(P, _hearth, _amount);
        }
    }

    /// <summary>Taking food from a hearth that isn't yours. What it turns out to mean depends on who saw.</summary>
    public sealed class TakeFrom : TimedActivity
    {
        readonly Household _hearth;
        readonly double _amount;
        public TakeFrom(Person p, Household hearth, double amount) : base(p, 1) { _hearth = hearth; _amount = amount; Step = "at another hearth"; }
        public override Verb Verb => Verb.Take;
        protected override double FatiguePerHour => 0;
        protected override void OnHour(Simulation sim) { }
        protected override void OnEnd(Simulation sim)
        {
            if (sim.Band.Households.Contains(_hearth)) sim.DoTake(P, _hearth, _amount);
        }
    }

    /// <summary>Facing someone over a wrong.</summary>
    public sealed class Confront : TimedActivity
    {
        readonly Person _other;
        public Confront(Person p, Person other) : base(p, 1) { _other = other; Step = $"confronting {other.Name}"; }
        public override Verb Verb => Verb.Confront;
        protected override double FatiguePerHour => 0.02;
        protected override void OnHour(Simulation sim) { }
        protected override void OnEnd(Simulation sim)
        {
            if (_other.Alive && sim.Band.Members.Contains(_other)) sim.DoConfront(P, _other);
        }
    }

    /// <summary>Going for someone without words: revenge, or rage.</summary>
    public sealed class Assault : TimedActivity
    {
        readonly Person _other;
        public Assault(Person p, Person other) : base(p, 1) { _other = other; Step = $"going for {other.Name}"; }
        public override Verb Verb => Verb.Attack;
        protected override double FatiguePerHour => 0.05;
        protected override void OnHour(Simulation sim) { }
        protected override void OnEnd(Simulation sim)
        {
            if (_other.Alive && sim.Band.Members.Contains(_other)) sim.DoAttack(P, _other, Simulation.LatestWrong(P, _other));
        }
    }

    /// <summary>
    /// Following someone one hates out of camp, to fall on them where nobody will see.
    /// </summary>
    public sealed class Ambush : Activity
    {
        readonly Person _other;
        int _travel;
        bool _struck;
        public Ambush(Person p, Person other) : base(p) { _other = other; }
        public override Verb Verb => Verb.Attack;
        public override bool Interruptible => false;

        public override void Begin(Simulation sim)
        {
            _travel = System.Math.Max(1, sim.TravelHours(World.WorldMap.Distance(P.X, P.Y, _other.X, _other.Y), P));
            Step = $"slipping out of camp after {_other.Name}";
        }

        public override void Hour(Simulation sim)
        {
            P.Fatigue = Inheritance.Clamp01(P.Fatigue + 0.04);
            if (--_travel > 0) return;
            if (!_struck)
            {
                _struck = true;
                // Still out there, and still alone?
                if (_other.Alive && sim.Band.Members.Contains(_other) && Simulation.OutAlone(_other))
                {
                    P.X = _other.X; P.Y = _other.Y;
                    sim.DoAmbush(P, _other);
                }
                _travel = System.Math.Max(1, sim.TravelHours(World.WorldMap.Distance(P.X, P.Y, sim.Band.CampX, sim.Band.CampY), P));
                Step = "walking back to camp";
                return;
            }
            P.X = sim.Band.CampX; P.Y = sim.Band.CampY;
            Done = true;
        }
    }

    /// <summary>Calling on others to go together and take a hearth's food by force.</summary>
    public sealed class Rally : TimedActivity
    {
        readonly Household _hearth;
        public Rally(Person p, Household hearth) : base(p, 1) { _hearth = hearth; Step = "calling others together"; }
        public override Verb Verb => Verb.Rally;
        protected override double FatiguePerHour => 0.03;
        protected override void OnHour(Simulation sim) { }
        protected override void OnEnd(Simulation sim)
        {
            if (sim.Band.Households.Contains(_hearth)) sim.DoRally(P, _hearth);
        }
    }

    /// <summary>Calling others to go and raid another band.</summary>
    public sealed class Raid : TimedActivity
    {
        readonly Simulation _target;
        public Raid(Person p, Simulation target) : base(p, 1) { _target = target; Step = $"calling for a raid on the {target.Band.Name}"; }
        public override Verb Verb => Verb.Raid;
        protected override double FatiguePerHour => 0.03;
        protected override void OnHour(Simulation sim) { }
        protected override void OnEnd(Simulation sim)
        {
            if (!_target.Extinct && sim.Region.Bands.Contains(_target)) sim.DoWarRaid(P, _target);
        }
    }
}
