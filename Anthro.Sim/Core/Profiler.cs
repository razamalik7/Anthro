using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Anthro.Sim.Core
{
    /// <summary>Opt-in timing of named sections, for finding where simulation time goes.</summary>
    public static class Profiler
    {
        public static bool Enabled;

        static readonly Dictionary<string, (long ticks, long calls)> _totals = new Dictionary<string, (long, long)>();

        public readonly struct Scope : IDisposable
        {
            readonly string? _name;
            readonly long _start;

            public Scope(string? name) { _name = name; _start = name == null ? 0 : Stopwatch.GetTimestamp(); }

            public void Dispose()
            {
                if (_name == null) return;
                long elapsed = Stopwatch.GetTimestamp() - _start;
                _totals.TryGetValue(_name, out var t);
                _totals[_name] = (t.ticks + elapsed, t.calls + 1);
            }
        }

        public static Scope Measure(string name) => new Scope(Enabled ? name : null);

        /// <summary>A section named by a prefix and a value; the name is only built when profiling is on.</summary>
        public static Scope Measure<T>(string prefix, T value) where T : struct => new Scope(Enabled ? prefix + value : null);

        public static IEnumerable<string> Report() =>
            _totals.OrderByDescending(kv => kv.Value.ticks)
                .Select(kv => $"{kv.Key,-28} {kv.Value.ticks * 1000.0 / Stopwatch.Frequency,10:F0} ms  {kv.Value.calls,10} calls");
    }
}
