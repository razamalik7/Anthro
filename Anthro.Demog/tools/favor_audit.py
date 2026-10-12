#!/usr/bin/env python3
"""
Favor audit analysis tool for Batch A (T10 A1).
Extracts favor earned by source, favor spent by power,
share of days broke, and when in the run the god was broke.
"""
import re, sys, glob, os
import statistics as st

ROOT_DIR = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
RUNS_DIR = os.path.join(ROOT_DIR, "runs")

def parse_prefix(prefix):
    pat = re.compile(rf'^{prefix}_\d+\.txt$')
    files = sorted([f for f in glob.glob(os.path.join(RUNS_DIR, f"{prefix}_*.txt")) if pat.match(os.path.basename(f))])
    
    if not files:
        return None

    prayers = []
    offers = []
    rites = []
    others = []
    totals_earned = []
    broke_pcts = []
    first_broke_days = []
    
    power_spents = {}

    for f in files:
        content = open(f, encoding='utf-8', errors='replace').read()
        m = re.search(r'FAVOR_AUDIT bot (\w+) earned/yr prayer:([\d.]+) offer:([\d.]+) rite:([\d.]+) other:([\d.]+) \| spent/yr (.*?) \| broke_pct ([\d.]+)% first_broke_day (-?\d+)', content)
        if not m:
            continue
        
        p = float(m.group(2))
        o = float(m.group(3))
        r = float(m.group(4))
        oth = float(m.group(5))
        spent_str = m.group(6).strip()
        bp = float(m.group(7))
        fbd = int(m.group(8))

        prayers.append(p)
        offers.append(o)
        rites.append(r)
        others.append(oth)
        totals_earned.append(p + o + r + oth)
        broke_pcts.append(bp)
        first_broke_days.append(fbd)

        if spent_str != "none":
            for item in spent_str.split():
                if ':' in item:
                    k, v = item.split(':', 1)
                    if k not in power_spents: power_spents[k] = []
                    power_spents[k].append(float(v))

    n = len(prayers)
    if n == 0:
        return None

    # Fill 0 for runs that spent 0 on a power
    for k in power_spents:
        while len(power_spents[k]) < n:
            power_spents[k].append(0.0)

    total_spent_per_run = [sum(power_spents[k][i] for k in power_spents) for i in range(n)] if power_spents else [0.0]*n

    broke_days_valid = [d for d in first_broke_days if d >= 0]

    return {
        "prefix": prefix,
        "n": n,
        "prayer": st.mean(prayers),
        "offer": st.mean(offers),
        "rite": st.mean(rites),
        "other": st.mean(others),
        "total_earned": st.mean(totals_earned),
        "total_spent": st.mean(total_spent_per_run),
        "power_spents": {k: st.mean(power_spents[k]) for k in sorted(power_spents)},
        "broke_pct": st.mean(broke_pcts),
        "first_broke_day_avg": st.mean(broke_days_valid) if broke_days_valid else -1,
        "first_broke_day_min": min(broke_days_valid) if broke_days_valid else -1,
        "worlds_broke": len(broke_days_valid),
    }

def print_audit(prefixes):
    results = [parse_prefix(p) for p in prefixes]
    results = [r for r in results if r is not None]
    
    if not results:
        print("No audit data found.")
        return

    print("=" * 115)
    print(f"{'Prefix':<12} | {'Earned/yr':<28} | {'Spent/yr':<25} | {'Broke%':<8} | {'First Broke Day':<15} | {'Worlds Broke':<10}")
    print(f"{'':<12} | {'pray   off   rite other total':<28} | {'total  (by power)':<25} | {'':<8} | {'mean / min':<15} | {'(of 20)':<10}")
    print("-" * 115)
    
    for r in results:
        powers = " ".join(f"{k}:{v:.1f}" for k, v in r["power_spents"].items())
        fbd_str = f"{r['first_broke_day_avg']:.0f} / {r['first_broke_day_min']}" if r['first_broke_day_avg'] >= 0 else "never"
        print(f"{r['prefix']:<12} | {r['prayer']:4.1f}  {r['offer']:4.1f}  {r['rite']:4.1f}  {r['other']:4.1f} {r['total_earned']:5.1f} | {r['total_spent']:5.1f}  {powers:<18} | {r['broke_pct']:6.1f}% | {fbd_str:<15} | {r['worlds_broke']:>2}/{r['n']}")
    print("=" * 115)

if __name__ == "__main__":
    if len(sys.argv) > 1:
        print_audit(sys.argv[1:])
    else:
        # Default all bots on both sets
        print_audit(["idle", "kind", "cruel", "skilful", "idle2", "kind2", "cruel2", "skilful2"])
