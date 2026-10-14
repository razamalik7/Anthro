#!/usr/bin/env python3
import glob, re, os, sys
import numpy as np

TOOLS_DIR = os.path.dirname(os.path.abspath(__file__))
ROOT_DIR = os.path.abspath(os.path.join(TOOLS_DIR, "..", ".."))
RUNS_DIR = os.path.join(ROOT_DIR, "runs")

def bootstrap_ci(arr, n_boot=10000):
    if len(arr) == 0: return 0.0, 0.0, 0.0
    arr = np.array(arr)
    mean = float(np.mean(arr))
    boot = np.random.choice(arr, size=(n_boot, len(arr)), replace=True)
    b_means = np.mean(boot, axis=1)
    lo = float(np.percentile(b_means, 2.5))
    hi = float(np.percentile(b_means, 97.5))
    return mean, lo, hi

def parse_belief_bands(condition):
    res = {y: {'skeptic': [], 'open': [], 'believer': [], 'devout': [], 'take': [], 'give': [], 'attack': [], 'offer': [], 'camp_sd': []} for y in [1, 10, 25, 50]}
    devotion = {y: {'spring': [], 'summer': [], 'autumn': [], 'winter': [], 'fed': [], 'hungry': []} for y in [1, 10, 25, 50]}
    
    seeds = []
    for s in range(41, 61):
        f = os.path.join(RUNS_DIR, f"{condition}_{s}.txt")
        if os.path.exists(f): seeds.append((s, f))
    for s in range(61, 81):
        f = os.path.join(RUNS_DIR, f"{condition}2_{s}.txt")
        if not os.path.exists(f): f = os.path.join(RUNS_DIR, f"{condition}_{s}.txt")
        if os.path.exists(f): seeds.append((s, f))
        
    for s, path in seeds:
        with open(path, "r", encoding="utf-8", errors="replace") as f:
            for line in f:
                m = re.search(r'belief_bands\s+\[(.*?)\]\s+Y(\d+):\s+skeptic\s+([\d\.]+)%\s+open\s+([\d\.]+)%\s+believer\s+([\d\.]+)%\s+devout\s+([\d\.]+)%\s+\|\s+god spread take ([\d\.]+) give ([\d\.]+) attack ([\d\.]+) offer ([\d\.]+)\s+\|\s+between_camp_sd\s+([\d\.]+)', line)
                if m:
                    y = int(m.group(2))
                    if y in res:
                        res[y]['skeptic'].append(float(m.group(3)))
                        res[y]['open'].append(float(m.group(4)))
                        res[y]['believer'].append(float(m.group(5)))
                        res[y]['devout'].append(float(m.group(6)))
                        res[y]['take'].append(float(m.group(7)))
                        res[y]['give'].append(float(m.group(8)))
                        res[y]['attack'].append(float(m.group(9)))
                        res[y]['offer'].append(float(m.group(10)))
                        res[y]['camp_sd'].append(float(m.group(11)))
                m2 = re.search(r'devotion\s+Y(\d+):\s+spring\s+([\d\.]+)\s+summer\s+([\d\.]+)\s+autumn\s+([\d\.]+)\s+winter\s+([\d\.]+)\s+\|\s+fed\s+([\d\.]+)\s+hungry\s+([\d\.]+)', line)
                if m2:
                    y = int(m2.group(1))
                    if y in devotion:
                        devotion[y]['spring'].append(float(m2.group(2)))
                        devotion[y]['summer'].append(float(m2.group(3)))
                        devotion[y]['autumn'].append(float(m2.group(4)))
                        devotion[y]['winter'].append(float(m2.group(5)))
                        devotion[y]['fed'].append(float(m2.group(6)))
                        devotion[y]['hungry'].append(float(m2.group(7)))
                        
    return res, devotion, len(seeds)

def print_condition_summary(cond_name):
    bands, dev, n = parse_belief_bands(cond_name)
    print(f"=== {cond_name.upper()} ({n} worlds) ===")
    for y in [1, 10, 25, 50]:
        n_w = len(bands[y]['skeptic'])
        skep = bootstrap_ci(bands[y]['skeptic'])
        op = bootstrap_ci(bands[y]['open'])
        bel = bootstrap_ci(bands[y]['believer'])
        devout = bootstrap_ci(bands[y]['devout'])
        csd = bootstrap_ci(bands[y]['camp_sd'])
        print(f"Y{y:2d} ({n_w}w): skeptic {skep[0]:.1f}% [{skep[1]:.1f}-{skep[2]:.1f}], open {op[0]:.1f}% [{op[1]:.1f}-{op[2]:.1f}], believer {bel[0]:.1f}% [{bel[1]:.1f}-{bel[2]:.1f}], devout {devout[0]:.1f}% [{devout[1]:.1f}-{devout[2]:.1f}] | between_camp_sd {csd[0]:.2f} [{csd[1]:.2f}-{csd[2]:.2f}]")
        
        take_s = bootstrap_ci(bands[y]['take'])
        give_s = bootstrap_ci(bands[y]['give'])
        att_s = bootstrap_ci(bands[y]['attack'])
        off_s = bootstrap_ci(bands[y]['offer'])
        print(f"     within-camp sd: take {take_s[0]:.2f}, give {give_s[0]:.2f}, attack {att_s[0]:.2f}, offer {off_s[0]:.2f}")
        
        sp = bootstrap_ci(dev[y]['spring'])
        sm = bootstrap_ci(dev[y]['summer'])
        au = bootstrap_ci(dev[y]['autumn'])
        wi = bootstrap_ci(dev[y]['winter'])
        fd = bootstrap_ci(dev[y]['fed'])
        hg = bootstrap_ci(dev[y]['hungry'])
        print(f"     devotion/ad/y: sp {sp[0]:.2f}, sm {sm[0]:.2f}, au {au[0]:.2f}, wi {wi[0]:.2f} | fed {fd[0]:.2f}, hg {hg[0]:.2f}")
    print()

def main():
    print_condition_summary("skilful")
    print_condition_summary("idle")
    print_condition_summary("quiet")

if __name__ == '__main__':
    main()
