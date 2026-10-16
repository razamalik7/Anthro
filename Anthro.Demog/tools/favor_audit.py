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
    files = []
    # If prefix ends with a digit (like skilful2), match strictly that prefix
    if prefix[-1].isdigit():
        pat = re.compile(rf'^{prefix}_\d+\.txt$')
        files = sorted([f for f in glob.glob(os.path.join(RUNS_DIR, f"{prefix}_*.txt")) if pat.match(os.path.basename(f))])
    else:
        # Check if both prefix and prefix2 exist (e.g. skilful and skilful2)
        pat1 = re.compile(rf'^{prefix}_\d+\.txt$')
        pat2 = re.compile(rf'^{prefix}2_\d+\.txt$')
        f1 = sorted([f for f in glob.glob(os.path.join(RUNS_DIR, f"{prefix}_*.txt")) if pat1.match(os.path.basename(f))])
        f2 = sorted([f for f in glob.glob(os.path.join(RUNS_DIR, f"{prefix}2_*.txt")) if pat2.match(os.path.basename(f))])
        files = f1 + f2
        if not files:
            files = f1
    
    if not files:
        return None

    thanks = []
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
        matches = list(re.finditer(r'FAVOR_AUDIT bot (\w+) earned/yr prayer:([\d.]+)(?: thank:([\d.]+))? offer:([\d.]+) rite:([\d.]+) other:([\d.]+) \| spent/yr (.*?) \| broke_pct ([\d.]+)% first_broke_day (-?\d+)', content))
        if not matches:
            continue
        m = matches[-1]
        
        p = float(m.group(2))
        th = float(m.group(3)) if m.group(3) is not None else 0.0
        o = float(m.group(4))
        r = float(m.group(5))
        oth = float(m.group(6))
        spent_str = m.group(7).strip()
        bp = float(m.group(8))
        fbd = int(m.group(9))

        prayers.append(p)
        thanks.append(th)
        offers.append(o)
        rites.append(r)
        others.append(oth)
        totals_earned.append(p + th + o + r + oth)
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
        "thank": st.mean(thanks) if thanks else 0.0,
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

    print("=" * 122)
    print(f"{'Prefix':<12} | {'Earned/yr':<34} | {'Spent/yr':<25} | {'Broke%':<8} | {'First Broke Day':<15} | {'Worlds Broke':<10}")
    print(f"{'':<12} | {'pray   thk   off   rite other total':<34} | {'total  (by power)':<25} | {'':<8} | {'mean / min':<15} | {'(of 20)':<10}")
    print("-" * 122)
    
    for r in results:
        powers = " ".join(f"{k}:{v:.1f}" for k, v in r["power_spents"].items())
        fbd_str = f"{r['first_broke_day_avg']:.0f} / {r['first_broke_day_min']}" if r['first_broke_day_avg'] >= 0 else "never"
        print(f"{r['prefix']:<12} | {r['prayer']:4.1f}  {r['thank']:4.1f}  {r['offer']:4.1f}  {r['rite']:4.1f}  {r['other']:4.1f} {r['total_earned']:5.1f} | {r['total_spent']:5.1f}  {powers:<18} | {r['broke_pct']:6.1f}% | {fbd_str:<15} | {r['worlds_broke']:>2}/{r['n']}")
    print("=" * 122)

    print("\n" + "=" * 115)
    print("ACTS PER YEAR BY SIZE VS TARGET (PLANNING.md / GOD-LAYER.md)")
    print("=" * 115)
    print(f"{'Prefix':<12} | {'Small Acts/yr (Target ~24)':<28} | {'Medium Acts/yr (Target 3-4)':<28} | {'Large Acts/yr (Target ~1)':<26}")
    print("-" * 115)
    for r in results:
        ps = r["power_spents"]
        small = (ps.get("Omen", 0.0) / 4.5) + (ps.get("ForkNudge", 0.0) / 10.0)
        med = (ps.get("BountyPlant", 0.0) / 17.0) + (ps.get("BountyAnimal", 0.0) / 25.0)
        large = (ps.get("Weather", 0.0) / 60.0) + (ps.get("Heal", 0.0) / 40.0) + (ps.get("BlessedBirth", 0.0) / 35.0)
        print(f"{r['prefix']:<12} | {small:>5.2f} acts/yr ({small/24.0*100:>5.1f}% target)   | {med:>5.2f} acts/yr ({med/3.5*100:>5.1f}% target)   | {large:>5.3f} acts/yr ({large/1.0*100:>5.1f}% target)")
    print("=" * 115 + "\n")

if __name__ == "__main__":
    if len(sys.argv) > 1:
        print_audit(sys.argv[1:])
    else:
        # Default all bots on both sets
        print_audit(["idle", "kind", "cruel", "skilful", "idle2", "kind2", "cruel2", "skilful2"])
