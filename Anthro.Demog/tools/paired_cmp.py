#!/usr/bin/env python3
"""
Paired-Comparison Tool (M1 / REVIEW-T10A2-T10B)
Compares condition runs against canonical godoff references across 40 seeds (41-80).
Computes per-seed log ratio, 95% bootstrap CI, and reports formatted claims:
  "X: +a% (95% CI b% to c%, 40 worlds)"
"""

import os
import sys
import glob
import re
import numpy as np

# Ensure tool directory is in path to import c_metrics
TOOLS_DIR = os.path.dirname(os.path.abspath(__file__))
ROOT_DIR = os.path.abspath(os.path.join(TOOLS_DIR, "..", ".."))
RUNS_DIR = os.path.join(ROOT_DIR, "runs")
sys.path.insert(0, TOOLS_DIR)
import c_metrics

METRICS = [
    ("pop", "Pop (Y50)", False),
    ("births", "Births", False),
    ("starv", "Starvation", False),
    ("gangs", "Gang Thefts", True),
    ("viol", "Violent Deaths", True),
    ("quarrels", "Quarrels", True),
    ("campmoves", "Camp Moves", True),
    ("splits", "Camp Splits", True),
    ("techs", "Techs Known", True),
    ("pray_h", "Pray Hours/d", True),
    ("winter_fed", "Winter Fed", False),
]

def load_canonical_godoff():
    godoff_runs = {}
    for s in range(41, 61):
        f = os.path.join(RUNS_DIR, f"godoff_{s}.txt")
        r = c_metrics.parse_run(f)
        if r is None:
            raise FileNotFoundError(f"Missing canonical reference: {f}")
        godoff_runs[s] = r

    for s in range(61, 81):
        f = os.path.join(RUNS_DIR, f"godoff2_{s}.txt")
        r = c_metrics.parse_run(f)
        if r is None:
            raise FileNotFoundError(f"Missing canonical reference: {f}")
        godoff_runs[s] = r

    return godoff_runs

def find_condition_runs(condition_name):
    """
    Find runs for condition across seeds 41-80.
    Checks:
      seeds 41-60: <condition>_{seed}.txt
      seeds 61-80: <condition>2_{seed}.txt, or fallback to <condition>_{seed}.txt
    """
    cond_runs = {}
    
    # Set 1 (41-60)
    for s in range(41, 61):
        f = os.path.join(RUNS_DIR, f"{condition_name}_{s}.txt")
        if os.path.exists(f):
            r = c_metrics.parse_run(f)
            if r: cond_runs[s] = r

    # Set 2 (61-80)
    for s in range(61, 81):
        f2 = os.path.join(RUNS_DIR, f"{condition_name}2_{s}.txt")
        f1 = os.path.join(RUNS_DIR, f"{condition_name}_{s}.txt")
        f = f2 if os.path.exists(f2) else f1
        if os.path.exists(f):
            r = c_metrics.parse_run(f)
            if r: cond_runs[s] = r

    return cond_runs

def compute_paired_comparison(cond_runs, godoff_runs, metric_key, can_be_zero=False, n_boot=10000, rng_seed=42):
    seeds = sorted(list(set(cond_runs.keys()) & set(godoff_runs.keys())))
    if len(seeds) == 0:
        return None

    X = np.array([cond_runs[s][metric_key] for s in seeds], dtype=float)
    Y = np.array([godoff_runs[s][metric_key] for s in seeds], dtype=float)
    N = len(seeds)

    # Per-seed log ratio
    if can_be_zero or (X.min() <= 0 or Y.min() <= 0):
        # Count data with potential zeros: Laplace pseudo-count (+1)
        r = np.log((X + 1.0) / (Y + 1.0))
    else:
        r = np.log(X / Y)

    mean_r = float(r.mean())
    mean_pct = (np.exp(mean_r) - 1.0) * 100.0

    # 95% Bootstrap confidence interval
    rng = np.random.default_rng(rng_seed)
    boot_indices = rng.choice(N, size=(n_boot, N), replace=True)
    boot_means = r[boot_indices].mean(axis=1)
    boot_pcts = (np.exp(boot_means) - 1.0) * 100.0

    ci_low, ci_high = np.percentile(boot_pcts, [2.5, 97.5])

    # Status: Real effect if 0 is excluded from 95% CI
    is_real = (ci_low > 0 and ci_high > 0) or (ci_low < 0 and ci_high < 0)

    # Formatting helper
    def fmt_pct(val):
        return f"+{val:.1f}%" if val >= 0 else f"{val:.1f}%"

    claim_str = f"{fmt_pct(mean_pct)} (95% CI {fmt_pct(ci_low)} to {fmt_pct(ci_high)}, {N} worlds)"

    return {
        "metric": metric_key,
        "n_worlds": N,
        "x_sum": float(X.sum()),
        "y_sum": float(Y.sum()),
        "x_mean": float(X.mean()),
        "y_mean": float(Y.mean()),
        "mean_log_ratio": mean_r,
        "mean_pct": mean_pct,
        "ci_low": float(ci_low),
        "ci_high": float(ci_high),
        "is_real": is_real,
        "claim": claim_str,
    }

def analyze_condition(condition_name, godoff_runs):
    cond_runs = find_condition_runs(condition_name)
    n = len(cond_runs)
    if n == 0:
        print(f"No runs found for condition '{condition_name}'")
        return None

    results = []
    for key, label, can_zero in METRICS:
        res = compute_paired_comparison(cond_runs, godoff_runs, key, can_be_zero=can_zero)
        if res:
            res["label"] = label
            results.append(res)

    return results

def print_condition_report(condition_name, results):
    if not results: return
    n_worlds = results[0]["n_worlds"]
    print(f"\n{'='*110}")
    print(f"PAIRED COMPARISON: {condition_name.upper()} vs GODOFF (Canonical Baseline, {n_worlds} worlds)")
    print(f"{'='*110}")
    print(f"{'Metric':<18} | {'Cond Total':>11} | {'Godoff Total':>12} | {'Paired Effect (95% Bootstrap CI)':<42} | {'Status':<6}")
    print("-" * 110)
    for r in results:
        status = "REAL" if r["is_real"] else "NOISE"
        x_tot = f"{r['x_sum']:,.1f}" if r['x_sum'] % 1 != 0 else f"{int(r['x_sum']):,}"
        y_tot = f"{r['y_sum']:,.1f}" if r['y_sum'] % 1 != 0 else f"{int(r['y_sum']):,}"
        print(f"{r['label']:<18} | {x_tot:>11} | {y_tot:>12} | {r['claim']:<42} | {status:<6}")
    print(f"{'='*110}\n")

def print_markdown_table(condition_name, results):
    if not results: return
    n_worlds = results[0]["n_worlds"]
    print(f"### {condition_name.capitalize()} vs Godoff ({n_worlds} worlds)")
    print("| Metric | Cond Total | Godoff Total | Paired Effect (95% CI, 40 worlds) | Verdict |")
    print("|:---|:---:|:---:|:---|:---:|")
    for r in results:
        status = "**REAL**" if r["is_real"] else "NOISE"
        x_tot = f"{r['x_sum']:,.1f}" if r['x_sum'] % 1 != 0 else f"{int(r['x_sum']):,}"
        y_tot = f"{r['y_sum']:,.1f}" if r['y_sum'] % 1 != 0 else f"{int(r['y_sum']):,}"
        print(f"| **{r['label']}** | {x_tot} | {y_tot} | {r['claim']} | {status} |")
    print()

def main():
    args = sys.argv[1:]
    conditions = [a for a in args if not a.startswith("--")]
    md_mode = "--md" in args

    if not conditions:
        conditions = ["idle", "kind", "skilful"]

    godoff_runs = load_canonical_godoff()

    for cond in conditions:
        res = analyze_condition(cond, godoff_runs)
        if res:
            if md_mode:
                print_markdown_table(cond, res)
            else:
                print_condition_report(cond, res)

if __name__ == "__main__":
    main()
