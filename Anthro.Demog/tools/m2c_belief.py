#!/usr/bin/env python3
"""
Belief Growth Analysis Tool (M2c / REVIEW-M1)
Tracks belief progression across years (Y1, 10, 20, 30, 40, 50):
- Mean god:real
- Share of believers (> 0.5)
- Favor income and spending as belief evolves
"""

import os
import sys
import glob
import re
import statistics as st
import numpy as np

TOOLS_DIR = os.path.dirname(os.path.abspath(__file__))
ROOT_DIR = os.path.abspath(os.path.join(TOOLS_DIR, "..", ".."))
RUNS_DIR = os.path.join(ROOT_DIR, "runs")

def parse_belief_trajectory(condition_name):
    """
    Parses belief at Y1, 10, 20, 30, 40, 50 across all 40 seeds (41-80).
    """
    years = [1, 10, 20, 30, 40, 50]
    traj = {y: {"means": [], "shares": [], "answers": [], "kinds": []} for y in years}
    favor_traj = {y: {"favor": [], "prayers": [], "offered": []} for y in years}
    
    files = []
    # Set 1 (41-60)
    for s in range(41, 61):
        f = os.path.join(RUNS_DIR, f"{condition_name}_{s}.txt")
        if os.path.exists(f): files.append(f)
    # Set 2 (61-80)
    for s in range(61, 81):
        f2 = os.path.join(RUNS_DIR, f"{condition_name}2_{s}.txt")
        f1 = os.path.join(RUNS_DIR, f"{condition_name}_{s}.txt")
        f = f2 if os.path.exists(f2) else f1
        if os.path.exists(f): files.append(f)

    for f in files:
        content = open(f, encoding='utf-8', errors='replace').read()
        lines = content.splitlines()
        
        current_year = 0
        for l in lines:
            # Yearly line
            m_y = re.match(r'^Y\s*(\d+)\s+pop', l)
            if m_y:
                current_year = int(m_y.group(1))
                if current_year in years:
                    m_fav = re.search(r'favor\s+([\d.-]+)', l)
                    m_off = re.search(r'offered\s+([\d.-]+)', l)
                    m_pr = re.search(r'prayers\s+(\d+)/(\d+)/(\d+)', l)
                    if m_fav: favor_traj[current_year]["favor"].append(float(m_fav.group(1)))
                    if m_off: favor_traj[current_year]["offered"].append(float(m_off.group(1)))
                    if m_pr: favor_traj[current_year]["prayers"].append(int(m_pr.group(1)))
            
            # Culture line: printed at Y1 and every 10 years
            if 'culture [' in l and '| belief' in l:
                # Year corresponds to latest year processed or check preceding lines
                m_bel = re.search(r'belief\s+(\d+)/(\d+)\s+mean\s+([\d.]+)\s+answers\s+([\d.]+)\s+kind\s+([\d.-]+)', l)
                if m_bel:
                    n_bel = int(m_bel.group(1))
                    n_tot = int(m_bel.group(2))
                    m_real = float(m_bel.group(3))
                    m_answ = float(m_bel.group(4))
                    m_kind = float(m_bel.group(5))
                    
                    share = n_bel / max(1, n_tot)
                    
                    # Associate with closest target year
                    target_y = min(years, key=lambda y: abs(y - current_year))
                    traj[target_y]["means"].append(m_real)
                    traj[target_y]["shares"].append(share)
                    traj[target_y]["answers"].append(m_answ)
                    traj[target_y]["kinds"].append(m_kind)

    return traj, favor_traj, len(files)

def print_belief_report(condition_name):
    traj, favor_traj, n_worlds = parse_belief_trajectory(condition_name)
    if n_worlds == 0:
        print(f"No files found for {condition_name}")
        return

    print("=" * 105)
    print(f"BELIEF GROWTH & FAVOR INCOME OVER TIME: {condition_name.upper()} ({n_worlds} worlds)")
    print("=" * 105)
    print(f"{'Year':<6} | {'Mean god:real':<14} | {'Believers (>0.5)':<18} | {'Answers':<10} | {'Kind':<10} | {'Favor Pool':<12} | {'Offered Rations':<15}")
    print("-" * 105)
    for y in [1, 10, 20, 30, 40, 50]:
        t = traj[y]
        f = favor_traj[y]
        m_real = st.mean(t["means"]) if t["means"] else 0.0
        m_share = st.mean(t["shares"]) if t["shares"] else 0.0
        m_answ = st.mean(t["answers"]) if t["answers"] else 0.0
        m_kind = st.mean(t["kinds"]) if t["kinds"] else 0.0
        m_fav = st.mean(f["favor"]) if f["favor"] else 0.0
        m_off = st.mean(f["offered"]) if f["offered"] else 0.0
        
        print(f"Y{y:<5} | {m_real:14.2f} | {m_share*100:15.1f}% | {m_answ:10.2f} | {m_kind:10.2f} | {m_fav:12.1f} | {m_off:15.1f}")
    print("=" * 105 + "\n")

if __name__ == "__main__":
    cond = sys.argv[1] if len(sys.argv) > 1 else "skilful"
    print_belief_report(cond)
