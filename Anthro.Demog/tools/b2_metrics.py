#!/usr/bin/env python3
import re, sys, glob, os
import statistics as st

def parse_run(filepath):
    if not os.path.exists(filepath):
        return None
    content = open(filepath, encoding='utf-8', errors='replace').read()
    
    # Yearly lines:
    # Y  50 pop  ... b  12 d  10 starv  2 viol  0 ... | asked other camps 15 gave ... fightkinds quarrel:4/0 ... campmoves 5 ... offered 0.0 ...
    y_lines = re.findall(r'\nY\s*(\d+)\s+pop\s+(\d+).*?b\s+(\d+)\s+d\s+(\d+)\s+starv\s+(\d+)\s+viol\s+(\d+).*?asked other camps\s+(\d+).*?(?:fightkinds\s+(.*?)\s+raids)?.*?campmoves\s+(\d+)(?:.*?offered\s+([\d.]+))?', content)
    
    if not y_lines:
        return None
        
    years_count = len(y_lines)
    final_pop = int(y_lines[-1][1])
    total_births = sum(int(y[2]) for y in y_lines)
    total_deaths = sum(int(y[3]) for y in y_lines)
    total_starv = sum(int(y[4]) for y in y_lines)
    total_viol = sum(int(y[5]) for y in y_lines)
    total_asks = sum(int(y[6]) for y in y_lines)
    
    total_quarrels = 0
    for y in y_lines:
        fk = y[7]
        if fk:
            qm = re.search(r'quarrel:(\d+)', fk)
            if qm:
                total_quarrels += int(qm.group(1))
                
    total_campmoves = sum(int(y[8]) for y in y_lines)
    
    total_offered = 0.0
    for y in y_lines:
        if len(y) > 9 and y[9]:
            total_offered += float(y[9])
    food_offered_per_yr = total_offered / max(1, years_count)
    
    # Culture lines for belief at Y50:
    # culture [Adok]: ... | belief 0/45 mean 0.19 answers 0.10 kind 0.14
    cult_matches = re.findall(r'culture \[.*?\].*?mean\s+([\d.]+)\s+answers\s+([\d.]+)\s+kind\s+([\d.-]+)', content)
    final_god_real = float(cult_matches[-1][0]) if cult_matches else 0.0
    final_god_answers = float(cult_matches[-1][1]) if cult_matches else 0.0
    final_god_kind = float(cult_matches[-1][2]) if cult_matches else 0.0
    
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
        'births': total_births,
        'deaths': total_deaths,
        'starv': total_starv,
        'viol': total_viol,
        'asks': total_asks,
        'quarrels': total_quarrels,
        'campmoves': total_campmoves,
        'offered_yr': food_offered_per_yr,
        'god_real': final_god_real,
        'god_answers': final_god_answers,
        'god_kind': final_god_kind,
        'pray_h': avg_pray_h,
        'offer_h': avg_offer_h,
        'winter_fed': avg_winter_fed,
        'extinct': 1 if years_count < 50 else 0
    }

def summarize_prefix(prefix):
    files = sorted(glob.glob(f'runs/{prefix}_*[0-9].txt'))
    runs = [parse_run(f) for f in files if parse_run(f) is not None]
    if not runs:
        return None
    n = len(runs)
    return {
        'prefix': prefix,
        'n': n,
        'med_pop': st.median(r['pop'] for r in runs),
        'mean_pop': st.mean(r['pop'] for r in runs),
        'sum_pop': sum(r['pop'] for r in runs),
        'births': st.mean(r['births'] for r in runs),
        'starv': st.mean(r['starv'] for r in runs),
        'deaths': st.mean(r['deaths'] for r in runs),
        'viol': st.mean(r['viol'] for r in runs),
        'asks': st.mean(r['asks'] for r in runs),
        'quarrels': st.mean(r['quarrels'] for r in runs),
        'campmoves': st.mean(r['campmoves'] for r in runs),
        'offered_yr': st.mean(r['offered_yr'] for r in runs),
        'god_real': st.mean(r['god_real'] for r in runs),
        'god_answers': st.mean(r['god_answers'] for r in runs),
        'god_kind': st.mean(r['god_kind'] for r in runs),
        'pray_h': st.mean(r['pray_h'] for r in runs),
        'offer_h': st.mean(r['offer_h'] for r in runs),
        'winter_fed': st.mean(r['winter_fed'] for r in runs),
        'extinct': sum(r['extinct'] for r in runs)
    }

if __name__ == '__main__':
    prefixes = sys.argv[1:]
    print(f"{'Prefix':<18} {'MedPop':>6} {'Births':>7} {'Starv':>7} {'Deaths':>7} {'Off/Yr':>7} {'Off_h':>7} {'Pray_h':>7} {'Real':>6} {'Kind':>6} {'Asks':>6} {'Quarrel':>7} {'Moves':>6}")
    print("-" * 105)
    for p in prefixes:
        s = summarize_prefix(p)
        if s:
            print(f"{s['prefix']:<18} {s['med_pop']:>6.1f} {s['births']:>7.1f} {s['starv']:>7.1f} {s['deaths']:>7.1f} {s['offered_yr']:>7.1f} {s['offer_h']:>7.3f} {s['pray_h']:>7.3f} {s['god_real']:>6.2f} {s['god_kind']:>6.2f} {s['asks']:>6.1f} {s['quarrels']:>7.1f} {s['campmoves']:>6.1f}")
        else:
            print(f"{p:<18} NO DATA")
