import os, re, glob, math, random
import numpy as np

RUNS_DIR = os.path.abspath("runs")

def load_world_data(prefix1, prefix2):
    """Loads 40 worlds for a condition: seeds 41-60 from prefix1, seeds 61-80 from prefix2."""
    worlds = {}
    
    # Set 1 (41-60)
    for s in range(41, 61):
        f = os.path.join(RUNS_DIR, f"{prefix1}_{s}.txt")
        if os.path.exists(f):
            worlds[s] = parse_world_file(f)
            
    # Set 2 (61-80)
    for s in range(61, 81):
        f = os.path.join(RUNS_DIR, f"{prefix2}_{s}.txt")
        if os.path.exists(f):
            worlds[s] = parse_world_file(f)
            
    return worlds

def parse_world_file(filepath):
    with open(filepath, "r", encoding="utf-8", errors="replace") as f:
        text = f.read()
        
    data = {
        "filepath": filepath,
        "years": {},
        "devotion": {},
        "culture": {},
        "belief_bands": {},
        "favor_audit": None,
        "techs_known": 0,
        "total_starv": 0,
        "total_viol": 0,
        "total_theft": 0,
        "total_births": 0,
        "total_deaths": 0,
        "total_quarrels": 0,
        "total_moves": 0,
        "total_splits": 0,
        "pray_hours": 0.0,
        "dips_below_15": 0,
        "extinct": False,
        "winter_fed": []
    }
    
    # Y lines
    y_matches = re.findall(r"Y\s*(\d+)\s+pop\s+(\d+).*?b\s+(\d+)\s+d\s+(\d+)\s+starv\s+(\d+)\s+viol\s+(\d+)\s+fed\s+([\d.]+)", text)
    for ym in y_matches:
        yr = int(ym[0])
        pop = int(ym[1])
        b = int(ym[2])
        d = int(ym[3])
        starv = int(ym[4])
        viol = int(ym[5])
        fed = float(ym[6])
        data["years"][yr] = {
            "pop": pop, "b": b, "d": d, "starv": starv, "viol": viol, "fed": fed
        }
        data["total_births"] += b
        data["total_deaths"] += d
        data["total_starv"] += starv
        data["total_viol"] += viol
        if pop < 15 and pop > 0:
            data["dips_below_15"] += 1
            
    # Check extinction
    if len(y_matches) > 0:
        last_yr = int(y_matches[-1][0])
        last_pop = int(y_matches[-1][1])
        data["final_pop"] = last_pop if last_yr == 50 else 0
        if last_yr < 50 or last_pop == 0:
            data["extinct"] = True
    else:
        data["final_pop"] = 0
        data["extinct"] = True

    # Gang thefts
    gang_matches = re.findall(r"gang:(\d+)", text)
    data["total_theft"] = sum(int(x) for x in gang_matches)
    
    # Quarrels / duels
    duel_matches = re.findall(r"duel:(\d+)", text)
    quarrel_matches = re.findall(r"quarrel:(\d+)", text)
    data["total_quarrels"] = sum(int(x) for x in duel_matches) + sum(int(x) for x in quarrel_matches)
    
    # Camp moves & splits
    moves_matches = re.findall(r"campmoves\s+(\d+)", text)
    splits_matches = re.findall(r"splits\s+(\d+)", text)
    data["total_moves"] = sum(int(x) for x in moves_matches)
    data["total_splits"] = sum(int(x) for x in splits_matches)
    
    # Hours: Pray
    pray_matches = re.findall(r"Pray\s+([\d.]+)", text)
    if pray_matches:
        data["pray_hours"] = float(pray_matches[-1])

    # Winter fed
    wf_matches = re.findall(r"Winter\s+([\d.]+)/", text)
    data["winter_fed"] = [float(x) for x in wf_matches]

    # Techs known
    tech_matches = re.findall(r"known:\s*(.*)", text)
    if tech_matches:
        last_known = tech_matches[-1].strip()
        data["techs_known"] = len([t for t in last_known.split(",") if t.strip()])

    # Devotion lines
    dev_matches = re.findall(r"devotion\s+Y(\d+):\s+spring\s+([\d.]+)\s+summer\s+([\d.]+)\s+autumn\s+([\d.]+)\s+winter\s+([\d.]+)\s+\|\s+fed\s+([\d.]+)\s+hungry\s+([\d.]+)", text)
    for dm in dev_matches:
        yr = int(dm[0])
        data["devotion"][yr] = {
            "spring": float(dm[1]), "summer": float(dm[2]), "autumn": float(dm[3]), "winter": float(dm[4]),
            "fed": float(dm[5]), "hungry": float(dm[6])
        }

    # Belief bands lines:
    bb_matches = re.findall(r"belief_bands\s+\[(.*?)\]\s+Y(\d+):\s+skeptic\s+([\d.]+)%\s+open\s+([\d.]+)%\s+believer\s+([\d.]+)%\s+devout\s+([\d.]+)%\s+\|\s+god spread\s+take\s+([\d.]+)\s+give\s+([\d.]+)\s+attack\s+([\d.]+)\s+offer\s+([\d.]+)\s+\|\s+between_camp_sd\s+([\d.]+)", text)
    for bbm in bb_matches:
        tribe = bbm[0]
        yr = int(bbm[1])
        data["belief_bands"][yr] = {
            "skeptic": float(bbm[2]), "open": float(bbm[3]), "believer": float(bbm[4]), "devout": float(bbm[5]),
            "spread_take": float(bbm[6]), "spread_give": float(bbm[7]), "spread_attack": float(bbm[8]), "spread_offer": float(bbm[9]),
            "between_camp_sd": float(bbm[10])
        }

    # Culture / god acts: associate with year from preceding FOOD Y<yr>:
    lines = text.splitlines()
    cur_yr = 0
    for l in lines:
        fm = re.search(r"FOOD\s+Y(\d+):", l)
        if fm:
            cur_yr = int(fm.group(1))
        cm = re.search(r"culture\s+\[(.*?)\]:.*?\|\s+god acts\s+take\s+([-\d.]+)\s+give\s+([-\d.]+)\s+attack\s+([-\d.]+)\s+offer\s+([-\d.]+)\s+refuse\s+([-\d.]+)\s+thank\s+([-\d.]+)", l)
        if cm and cur_yr > 0:
            data["culture"][cur_yr] = {
                "take": float(cm.group(2)), "give": float(cm.group(3)), "attack": float(cm.group(4)),
                "offer": float(cm.group(5)), "refuse": float(cm.group(6)), "thank": float(cm.group(7))
            }

    # FAVOR_AUDIT: take the last one (at final year)
    fa_matches = re.findall(r"FAVOR_AUDIT\s+bot\s+(\w+)\s+earned/yr\s+prayer:([-\d.]+)\s+thank:([-\d.]+)\s+offer:([-\d.]+)\s+rite:([-\d.]+)\s+other:([-\d.]+)\s+\|\s+spent/yr\s+(.*?)\s+\|\s+broke_pct\s+([\d.]+)%\s+first_broke_day\s+([-\d.]+)(?:\s+\|\s+help\s+([-\d.]+)\s+harm\s+([-\d.]+))?", text)
    if fa_matches:
        last_fa = fa_matches[-1]
        spent_str = last_fa[6]
        # Parse powers
        powers = {}
        for p_item in spent_str.split():
            if ":" in p_item:
                k, v = p_item.split(":")
                powers[k] = float(v)
                
        # Calculate acts/yr:
        omen_cost = 5.0
        bounty_cost = 25.0
        large_cost = 75.0
        
        small_acts = powers.get("Omen", 0.0) / omen_cost
        med_acts = (powers.get("BountyPlant", 0.0) + powers.get("BountyAnimal", 0.0)) / bounty_cost
        large_acts = (powers.get("Weather", 0.0) + powers.get("BlessedBirth", 0.0) + powers.get("Heal", 0.0) + powers.get("Lightning", 0.0)) / large_cost
        
        data["favor_audit"] = {
            "bot": last_fa[0],
            "earned_prayer": float(last_fa[1]),
            "earned_thank": float(last_fa[2]),
            "earned_offer": float(last_fa[3]),
            "earned_rite": float(last_fa[4]),
            "earned_other": float(last_fa[5]),
            "spent": spent_str,
            "powers": powers,
            "small_acts": small_acts,
            "med_acts": med_acts,
            "large_acts": large_acts,
            "broke_pct": float(last_fa[7]),
            "first_broke_day": float(last_fa[8]),
            "help": float(last_fa[9]) if last_fa[9] else 0.0,
            "harm": float(last_fa[10]) if last_fa[10] else 0.0
        }

    return data

def bootstrap_ci_paired(diffs, n_resamples=5000, ci=0.95):
    """Computes paired percentage difference with 95% bootstrap CI."""
    if len(diffs) == 0:
        return 0.0, 0.0, 0.0
    means = []
    n = len(diffs)
    diffs_arr = np.array(diffs)
    for _ in range(n_resamples):
        sample = np.random.choice(diffs_arr, size=n, replace=True)
        means.append(np.mean(sample))
    low_p = (1.0 - ci) / 2.0 * 100
    high_p = (1.0 + ci) / 2.0 * 100
    mean_val = np.mean(diffs_arr)
    ci_low = np.percentile(means, low_p)
    ci_high = np.percentile(means, high_p)
    return mean_val, ci_low, ci_high

def bootstrap_mean_ci(values, n_resamples=5000, ci=0.95):
    """Computes mean and 95% bootstrap CI for a single series."""
    if len(values) == 0:
        return 0.0, 0.0, 0.0
    arr = np.array(values)
    means = []
    n = len(arr)
    for _ in range(n_resamples):
        sample = np.random.choice(arr, size=n, replace=True)
        means.append(np.mean(sample))
    low_p = (1.0 - ci) / 2.0 * 100
    high_p = (1.0 + ci) / 2.0 * 100
    return np.mean(arr), np.percentile(means, low_p), np.percentile(means, high_p)

def paired_metric(cond_worlds, base_worlds, fn):
    """Computes paired percentage change: (cond - base) / base * 100 per world."""
    pct_diffs = []
    abs_diffs = []
    for s in cond_worlds:
        if s in base_worlds:
            c_val = fn(cond_worlds[s])
            b_val = fn(base_worlds[s])
            abs_diffs.append(c_val - b_val)
            if b_val != 0:
                pct_diffs.append((c_val - b_val) / b_val * 100.0)
            else:
                pct_diffs.append(0.0 if c_val == 0 else 100.0)
    return pct_diffs, abs_diffs

def print_secular_table(cond_name, cond_worlds, godoff_worlds):
    metrics = [
        ("Pop (Y50)", lambda d: d["final_pop"]),
        ("Births", lambda d: d["total_births"]),
        ("Starvation", lambda d: d["total_starv"]),
        ("Gang Thefts", lambda d: d["total_theft"]),
        ("Violent Deaths", lambda d: d["total_viol"]),
        ("Quarrels", lambda d: d["total_quarrels"]),
        ("Camp Moves", lambda d: d["total_moves"]),
        ("Camp Splits", lambda d: d["total_splits"]),
        ("Techs Known", lambda d: d["techs_known"]),
        ("Pray Hours/d", lambda d: d["pray_hours"]),
        ("Winter Fed", lambda d: np.mean(d["winter_fed"]) if d["winter_fed"] else 0.0),
        ("Dips < 15", lambda d: d["dips_below_15"])
    ]
    
    print(f"\n=== Secular Comparison: {cond_name} vs godoff (40 worlds) ===")
    for name, fn in metrics:
        pcts, abs_d = paired_metric(cond_worlds, godoff_worlds, fn)
        m, lo, hi = bootstrap_ci_paired(pcts)
        # Classify as REAL if 0 is outside CI, else NOISE
        status = "REAL" if (lo > 0 or hi < 0) else "NOISE"
        sign = "+" if m > 0 else ""
        print(f"| {name:<15} | {sign}{m:5.1f}% (95% CI {lo:5.1f}% to {hi:5.1f}%, 40 worlds) [{status}] |")

def main():
    np.random.seed(42)
    random.seed(42)
    
    print("Loading world files...")
    godoff = load_world_data("godoff", "godoff2")
    idle = load_world_data("idle", "idle2")
    skilful = load_world_data("skilful", "skilful2")
    quiet = load_world_data("quiet", "quiet2")
    charity = load_world_data("charity", "charity2")
    demanding = load_world_data("demanding", "demanding2")
    malevolent = load_world_data("malevolent", "malevolent2")
    stern = load_world_data("stern", "stern2")
    capricious = load_world_data("capricious", "capricious2")
    
    conditions = {
        "idle": idle,
        "skilful": skilful,
        "quiet": quiet,
        "charity": charity,
        "demanding": demanding,
        "malevolent": malevolent,
        "stern": stern,
        "capricious": capricious
    }
    
    print(f"Loaded: godoff ({len(godoff)} worlds)")
    for name, data in conditions.items():
        print(f"Loaded: {name} ({len(data)} worlds)")
        
    for name, data in conditions.items():
        print_secular_table(name, data, godoff)

    print("\n" + "="*80)
    print("=== BELIEF BANDS TABLE (Y1, Y10, Y25, Y50) ===")
    print("="*80)
    for cname in ["idle", "skilful", "quiet", "charity", "demanding", "malevolent", "stern", "capricious"]:
        data = conditions[cname]
        for yr in [1, 10, 25, 50]:
            skep = [d["belief_bands"][yr]["skeptic"] for d in data.values() if yr in d["belief_bands"]]
            opn = [d["belief_bands"][yr]["open"] for d in data.values() if yr in d["belief_bands"]]
            bel = [d["belief_bands"][yr]["believer"] for d in data.values() if yr in d["belief_bands"]]
            dev = [d["belief_bands"][yr]["devout"] for d in data.values() if yr in d["belief_bands"]]
            
            s_m, s_lo, s_hi = bootstrap_mean_ci(skep)
            o_m, o_lo, o_hi = bootstrap_mean_ci(opn)
            b_m, b_lo, b_hi = bootstrap_mean_ci(bel)
            d_m, d_lo, d_hi = bootstrap_mean_ci(dev)
            
            bc_sd = [d["belief_bands"][yr]["between_camp_sd"] for d in data.values() if yr in d["belief_bands"]]
            bc_m, bc_lo, bc_hi = bootstrap_mean_ci(bc_sd)
            
            sp_take = [d["belief_bands"][yr]["spread_take"] for d in data.values() if yr in d["belief_bands"]]
            sp_give = [d["belief_bands"][yr]["spread_give"] for d in data.values() if yr in d["belief_bands"]]
            sp_atk = [d["belief_bands"][yr]["spread_attack"] for d in data.values() if yr in d["belief_bands"]]
            sp_off = [d["belief_bands"][yr]["spread_offer"] for d in data.values() if yr in d["belief_bands"]]
            
            t_m = np.mean(sp_take) if sp_take else 0
            g_m = np.mean(sp_give) if sp_give else 0
            a_m = np.mean(sp_atk) if sp_atk else 0
            o_m_sp = np.mean(sp_off) if sp_off else 0
            
            print(f"| {cname:<11} | Y{yr:<2} | {s_m:4.1f}% [{s_lo:4.1f}-{s_hi:4.1f}] | {o_m:4.1f}% [{o_lo:4.1f}-{o_hi:4.1f}] | {b_m:4.1f}% [{b_lo:4.1f}-{b_hi:4.1f}] | {d_m:4.1f}% [{d_lo:4.1f}-{d_hi:4.1f}] | {t_m:4.2f}/{g_m:4.2f}/{a_m:4.2f}/{o_m_sp:4.2f} | {bc_m:4.2f} [{bc_lo:4.2f}-{bc_hi:4.2f}] |")

    print("\n" + "="*80)
    print("=== DEVOTION BY YEAR (Y1, Y10, Y25, Y50) ===")
    print("="*80)
    for cname in ["skilful", "quiet", "idle"]:
        data = conditions[cname]
        for yr in [1, 10, 25, 50]:
            sp = [d["devotion"][yr]["spring"] for d in data.values() if yr in d["devotion"]]
            sm = [d["devotion"][yr]["summer"] for d in data.values() if yr in d["devotion"]]
            au = [d["devotion"][yr]["autumn"] for d in data.values() if yr in d["devotion"]]
            wi = [d["devotion"][yr]["winter"] for d in data.values() if yr in d["devotion"]]
            fed = [d["devotion"][yr]["fed"] for d in data.values() if yr in d["devotion"]]
            hg = [d["devotion"][yr]["hungry"] for d in data.values() if yr in d["devotion"]]
            
            print(f"| {cname:<8} | Y{yr:<2} | sp {np.mean(sp):4.2f}, sm {np.mean(sm):4.2f}, au {np.mean(au):4.2f}, wi {np.mean(wi):4.2f} | fed {np.mean(fed):5.2f}, hg {np.mean(hg):4.2f}/ad/yr |")

    print("\n" + "="*80)
    print("=== GOD ACTS CULTURE PROFILE (Y25 & Y50) ===")
    print("="*80)
    for cname in ["idle", "skilful", "quiet", "charity", "demanding", "malevolent", "stern", "capricious"]:
        data = conditions[cname]
        for yr in [25, 50]:
            takes = [d["culture"][yr]["take"] for d in data.values() if yr in d["culture"]]
            gives = [d["culture"][yr]["give"] for d in data.values() if yr in d["culture"]]
            atks = [d["culture"][yr]["attack"] for d in data.values() if yr in d["culture"]]
            offs = [d["culture"][yr]["offer"] for d in data.values() if yr in d["culture"]]
            refs = [d["culture"][yr]["refuse"] for d in data.values() if yr in d["culture"]]
            thks = [d["culture"][yr]["thank"] for d in data.values() if yr in d["culture"]]
            
            print(f"| {cname:<11} | Y{yr:<2} | take {np.mean(takes):+5.2f} give {np.mean(gives):+5.2f} attack {np.mean(atks):+5.2f} offer {np.mean(offs):+5.2f} refuse {np.mean(refs):+5.2f} thank {np.mean(thks):+5.2f} |")

    print("\n" + "="*80)
    print("=== FAVOR AUDIT & REPUTATION (HELP / HARM) ===")
    print("="*80)
    for cname, data in conditions.items():
        pray = [d["favor_audit"]["earned_prayer"] for d in data.values() if d["favor_audit"]]
        thk = [d["favor_audit"]["earned_thank"] for d in data.values() if d["favor_audit"]]
        off = [d["favor_audit"]["earned_offer"] for d in data.values() if d["favor_audit"]]
        rite = [d["favor_audit"]["earned_rite"] for d in data.values() if d["favor_audit"]]
        oth = [d["favor_audit"]["earned_other"] for d in data.values() if d["favor_audit"]]
        brk = [d["favor_audit"]["broke_pct"] for d in data.values() if d["favor_audit"]]
        f_brk = [d["favor_audit"]["first_broke_day"] for d in data.values() if d["favor_audit"]]
        hlp = [d["favor_audit"]["help"] for d in data.values() if d["favor_audit"]]
        hrm = [d["favor_audit"]["harm"] for d in data.values() if d["favor_audit"]]
        sm_acts = [d["favor_audit"]["small_acts"] for d in data.values() if d["favor_audit"]]
        md_acts = [d["favor_audit"]["med_acts"] for d in data.values() if d["favor_audit"]]
        lg_acts = [d["favor_audit"]["large_acts"] for d in data.values() if d["favor_audit"]]
        
        tot_earn = np.mean(pray) + np.mean(thk) + np.mean(off) + np.mean(rite) + np.mean(oth)
        print(f"| {cname:<11} | Earned: {tot_earn:6.1f}/yr (pray {np.mean(pray):4.1f}, thk {np.mean(thk):5.1f}, off {np.mean(off):5.1f}) | Acts/yr: sm {np.mean(sm_acts):5.2f}, med {np.mean(md_acts):4.2f}, lg {np.mean(lg_acts):4.2f} | Broke: {np.mean(brk):4.1f}% (first d{np.mean(f_brk):.0f}) | Help: {np.mean(hlp):5.1f} | Harm: {np.mean(hrm):5.1f} |")

if __name__ == "__main__":
    main()
