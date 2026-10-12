#!/usr/bin/env python3
"""
Multi-dimensional analysis tool for Batch C (C1 / PLANNING: no single score).
Evaluates extinction risk, health/starvation, violence/theft, discovery pace,
cohesion, belief, and cultural norms/god-act expectations.
"""
import re, sys, glob, os
import statistics as st

ROOT_DIR = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
RUNS_DIR = os.path.join(ROOT_DIR, "runs")

def parse_run(filepath):
    if not os.path.exists(filepath):
        return None
    content = open(filepath, encoding='utf-8', errors='replace').read()
    
    y_lines = [l for l in content.splitlines() if l.startswith('Y ') or l.startswith('Y  ')]
    if not y_lines:
        return None
        
    years_count = len(y_lines)
    
    # Populations per year to check if ever dipped below 15
    pops = []
    total_births = 0
    total_deaths = 0
    total_starv = 0
    total_viol = 0
    total_asks = 0
    total_quarrels = 0
    total_gangs = 0
    total_duels = 0
    total_ambushes = 0
    total_campmoves = 0
    total_offered = 0.0
    total_splits = 0
    
    for line in y_lines:
        m_pop = re.search(r'pop\s+(\d+)', line)
        if m_pop: pops.append(int(m_pop.group(1)))
        
        m_b = re.search(r'\bb\s+(\d+)', line)
        if m_b: total_births += int(m_b.group(1))
        
        m_d = re.search(r'\bd\s+(\d+)', line)
        if m_d: total_deaths += int(m_d.group(1))
        
        m_starv = re.search(r'starv\s+(\d+)', line)
        if m_starv: total_starv += int(m_starv.group(1))
        
        m_viol = re.search(r'viol\s+(\d+)', line)
        if m_viol: total_viol += int(m_viol.group(1))
        
        m_ask = re.search(r'asked other camps\s+(\d+)', line)
        if m_ask: total_asks += int(m_ask.group(1))
        
        m_splits = re.search(r'splits\s+(\d+)', line)
        if m_splits: total_splits += int(m_splits.group(1))
        
        m_cm = re.search(r'campmoves\s+(\d+)', line)
        if m_cm: total_campmoves += int(m_cm.group(1))
        
        m_off = re.search(r'offered\s+([\d.]+)', line)
        if m_off: total_offered += float(m_off.group(1))
        
        m_fk = re.search(r'fightkinds\s+([^\s|]+)', line)
        if m_fk:
            fk_str = m_fk.group(1)
            qm = re.search(r'quarrel:(\d+)', fk_str)
            if qm: total_quarrels += int(qm.group(1))
            gm = re.search(r'gang:(\d+)', fk_str)
            if gm: total_gangs += int(gm.group(1))
            dm = re.search(r'duel:(\d+)', fk_str)
            if dm: total_duels += int(dm.group(1))
            am = re.search(r'ambush:(\d+)', fk_str)
            if am: total_ambushes += int(am.group(1))

    final_pop = pops[-1] if pops else 0
    min_pop = min(pops) if pops else 0
    dipped_below_15 = 1 if (min_pop < 15) else 0
    extinct = 1 if (years_count < 50 or final_pop == 0) else 0

    # Techs known
    known_matches = re.findall(r'known:\s+(.*)', content)
    unique_techs = 0
    if known_matches:
        last_known = known_matches[-1]
        unique_techs = len([x for x in last_known.split(',') if x.strip()])

    # Communal rites count
    rites_count = len(re.findall(r'Sacred rite held', content))

    # Culture norms and god beliefs at Y50
    cult_matches = re.findall(r'culture \[.*?\].*?honour\s+([\d.-]+)\s+peacemaking\s+([\d.-]+)\s+\|\s+rationing\s+([\d.-]+)\s+demand\s+([\d.-]+)\s+elders\s+([\d.-]+)\s+hunting\s+([\d.-]+)\s+plural\s+([\d.-]+)(?:.*?belief\s+\d+/\d+\s+mean\s+([\d.]+)\s+answers\s+([\d.]+)\s+kind\s+([\d.-]+))?(?:.*?god acts\s+take\s+([\d.-]+)\s+give\s+([\d.-]+)\s+attack\s+([\d.-]+)\s+offer\s+([\d.-]+))?', content)
    
    last_cult = cult_matches[-1] if cult_matches else None
    
    norm_honour = float(last_cult[0]) if last_cult and last_cult[0] else 0.0
    norm_peace = float(last_cult[1]) if last_cult and last_cult[1] else 0.0
    norm_rationing = float(last_cult[2]) if last_cult and last_cult[2] else 0.0
    norm_demand = float(last_cult[3]) if last_cult and last_cult[3] else 0.0
    norm_elders = float(last_cult[4]) if last_cult and last_cult[4] else 0.0
    norm_hunting = float(last_cult[5]) if last_cult and last_cult[5] else 0.0
    norm_plural = float(last_cult[6]) if last_cult and last_cult[6] else 0.0
    
    god_real = float(last_cult[7]) if last_cult and len(last_cult) > 7 and last_cult[7] else 0.0
    god_answers = float(last_cult[8]) if last_cult and len(last_cult) > 8 and last_cult[8] else 0.0
    god_kind = float(last_cult[9]) if last_cult and len(last_cult) > 9 and last_cult[9] else 0.0
    
    god_take = float(last_cult[10]) if last_cult and len(last_cult) > 10 and last_cult[10] else 0.0
    god_give = float(last_cult[11]) if last_cult and len(last_cult) > 11 and last_cult[11] else 0.0
    god_attack = float(last_cult[12]) if last_cult and len(last_cult) > 12 and last_cult[12] else 0.0
    god_offer = float(last_cult[13]) if last_cult and len(last_cult) > 13 and last_cult[13] else 0.0

    # Hours
    pray_hrs = [float(x) for x in re.findall(r' Pray ([\d.]+)', content)]
    offer_hrs = [float(x) for x in re.findall(r' Offer ([\d.]+)', content)]
    avg_pray_h = st.mean(pray_hrs) if pray_hrs else 0.0
    avg_offer_h = st.mean(offer_hrs) if offer_hrs else 0.0

    # Winter fed
    winter_fed = [float(x) for x in re.findall(r'Winter [\d.]+?/([\d.]+)', content)]
    avg_winter_fed = st.mean(winter_fed) if winter_fed else 0.0

    return {
        'pop': final_pop,
        'min_pop': min_pop,
        'dipped_15': dipped_below_15,
        'extinct': extinct,
        'births': total_births,
        'deaths': total_deaths,
        'starv': total_starv,
        'viol': total_viol,
        'asks': total_asks,
        'quarrels': total_quarrels,
        'gangs': total_gangs,
        'duels': total_duels,
        'ambushes': total_ambushes,
        'splits': total_splits,
        'campmoves': total_campmoves,
        'offered_yr': total_offered / max(1, years_count),
        'techs': unique_techs,
        'rites': rites_count,
        'god_real': god_real,
        'god_answers': god_answers,
        'god_kind': god_kind,
        'god_take': god_take,
        'god_give': god_give,
        'god_attack': god_attack,
        'god_offer': god_offer,
        'norm_honour': norm_honour,
        'norm_peace': norm_peace,
        'norm_rationing': norm_rationing,
        'norm_demand': norm_demand,
        'norm_elders': norm_elders,
        'norm_hunting': norm_hunting,
        'pray_h': avg_pray_h,
        'offer_h': avg_offer_h,
        'winter_fed': avg_winter_fed,
    }

def summarize_prefix(prefix):
    pattern = re.compile(r'^(?:.*[/\\])?' + re.escape(prefix) + r'_(\d+)\.txt$')
    files = sorted([f for f in glob.glob(os.path.join(RUNS_DIR, '*.txt')) if pattern.match(f) and not f.endswith('_err.txt')])
    runs = [parse_run(f) for f in files if parse_run(f) is not None]
    if not runs:
        return None
    n = len(runs)
    return {
        'prefix': prefix,
        'n': n,
        'med_pop': st.median(r['pop'] for r in runs),
        'mean_pop': st.mean(r['pop'] for r in runs),
        'total_pop': sum(r['pop'] for r in runs),
        'extinct_count': sum(r['extinct'] for r in runs),
        'dipped_15_count': sum(r['dipped_15'] for r in runs),
        'births': st.mean(r['births'] for r in runs),
        'deaths': st.mean(r['deaths'] for r in runs),
        'starv': st.mean(r['starv'] for r in runs),
        'viol': st.mean(r['viol'] for r in runs),
        'gangs': st.mean(r['gangs'] for r in runs),
        'quarrels': st.mean(r['quarrels'] for r in runs),
        'duels': st.mean(r['duels'] for r in runs),
        'ambushes': st.mean(r['ambushes'] for r in runs),
        'splits': st.mean(r['splits'] for r in runs),
        'campmoves': st.mean(r['campmoves'] for r in runs),
        'offered_yr': st.mean(r['offered_yr'] for r in runs),
        'offer_h': st.mean(r['offer_h'] for r in runs),
        'pray_h': st.mean(r['pray_h'] for r in runs),
        'techs': st.mean(r['techs'] for r in runs),
        'rites': st.mean(r['rites'] for r in runs),
        'god_real': st.mean(r['god_real'] for r in runs),
        'god_answers': st.mean(r['god_answers'] for r in runs),
        'god_kind': st.mean(r['god_kind'] for r in runs),
        'god_take': st.mean(r['god_take'] for r in runs),
        'god_give': st.mean(r['god_give'] for r in runs),
        'god_attack': st.mean(r['god_attack'] for r in runs),
        'god_offer': st.mean(r['god_offer'] for r in runs),
        'norm_rationing': st.mean(r['norm_rationing'] for r in runs),
        'norm_demand': st.mean(r['norm_demand'] for r in runs),
        'winter_fed': st.mean(r['winter_fed'] for r in runs),
    }

def print_multi_dimension_table(prefixes):
    print(f"\n{'='*130}")
    print("MULTI-DIMENSION GOD COMPARISON (Batch C / PLANNING.md)")
    print(f"{'='*130}")
    
    header = (
        f"{'Prefix':<18} | "
        f"{'MedP':>5} {'TotP':>5} {'Dip15':>5} | "
        f"{'Starv':>6} {'WtrFed':>6} {'ViolD':>5} | "
        f"{'Gangs':>6} {'Quarr':>5} | "
        f"{'Moves':>5} {'Splits':>6} | "
        f"{'Techs':>5} {'Rites':>5} | "
        f"{'Real':>5} {'Kind':>5} {'Answ':>5} | "
        f"{'G:Take':>6} {'G:Att':>5}"
    )
    print(header)
    print("-" * 130)
    for p in prefixes:
        s = summarize_prefix(p)
        if not s:
            print(f"{p:<18} | NO DATA")
            continue
        line = (
            f"{s['prefix']:<18} | "
            f"{s['med_pop']:>5.1f} {s['total_pop']:>5d} {s['dipped_15_count']:>5d} | "
            f"{s['starv']:>6.1f} {s['winter_fed']:>6.2f} {s['viol']:>5.1f} | "
            f"{s['gangs']:>6.1f} {s['quarrels']:>5.1f} | "
            f"{s['campmoves']:>5.1f} {s['splits']:>6.1f} | "
            f"{s['techs']:>5.1f} {s['rites']:>5.1f} | "
            f"{s['god_real']:>5.2f} {s['god_kind']:>5.2f} {s['god_answers']:>5.2f} | "
            f"{s['god_take']:>6.2f} {s['god_attack']:>5.2f}"
        )
        print(line)
    print(f"{'='*130}\n")

if __name__ == '__main__':
    prefixes = sys.argv[1:]
    if not prefixes:
        prefixes = [
            'godoff', 'idle', 'kind', 'cruel', 'skilful',
            'godoff2', 'idle2', 'kind2', 'cruel2', 'skilful2'
        ]
    print_multi_dimension_table(prefixes)
