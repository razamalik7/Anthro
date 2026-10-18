#!/usr/bin/env python3
"""
Continuous Worker Pool for Anthro Simulations (Batch C).
Keeps all available CPU cores 100% saturated without waiting for batch tails.
"""
import os
import sys
import subprocess
import time
from concurrent.futures import ProcessPoolExecutor, as_completed

DOTNET_EXE = r"C:\Program Files\dotnet\dotnet.exe"
ROOT_DIR = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
RUNS_DIR = os.path.join(ROOT_DIR, "runs")
DEMOG_DLL = os.path.join(RUNS_DIR, "bin", "Demog.dll")

all_batches = [
    # Baselines
    ("godoff", 41, 60, ["ANTHRO_OFF=god"]),
    ("godoff2", 61, 80, ["ANTHRO_OFF=god"]),
    ("idle", 41, 60, ["ANTHRO_GOD=none", "ANTHRO_OFF="]),
    ("idle2", 61, 80, ["ANTHRO_GOD=none", "ANTHRO_OFF="]),
    
    # The Bots (C1 & C2)
    ("kind", 41, 60, ["ANTHRO_GOD=kind", "ANTHRO_OFF="]),
    ("kind2", 61, 80, ["ANTHRO_GOD=kind", "ANTHRO_OFF="]),
    ("cruel", 41, 60, ["ANTHRO_GOD=cruel", "ANTHRO_OFF="]),
    ("cruel2", 61, 80, ["ANTHRO_GOD=cruel", "ANTHRO_OFF="]),
    ("skilful", 41, 60, ["ANTHRO_GOD=skilful", "ANTHRO_OFF="]),
    ("skilful2", 61, 80, ["ANTHRO_GOD=skilful", "ANTHRO_OFF="]),
    
    # Direct acts ablations (C3)
    ("kind_noheal", 41, 60, ["ANTHRO_GOD=kind", "ANTHRO_OFF=heal"]),
    ("kind_noheal2", 61, 80, ["ANTHRO_GOD=kind", "ANTHRO_OFF=heal"]),
    ("cruel_nolightning", 41, 60, ["ANTHRO_GOD=cruel", "ANTHRO_OFF=lightning"]),
    # T11 God goes quiet
    ("quiet", 41, 60, ["ANTHRO_GOD=quiet", "ANTHRO_OFF="]),
    ("quiet2", 61, 80, ["ANTHRO_GOD=quiet", "ANTHRO_OFF="]),
    
    # Pattern Bots (T10C)
    ("charity", 41, 60, ["ANTHRO_GOD=charity", "ANTHRO_OFF="]),
    ("charity2", 61, 80, ["ANTHRO_GOD=charity", "ANTHRO_OFF="]),
    ("demanding", 41, 60, ["ANTHRO_GOD=demanding", "ANTHRO_OFF="]),
    ("demanding2", 61, 80, ["ANTHRO_GOD=demanding", "ANTHRO_OFF="]),
    ("malevolent", 41, 60, ["ANTHRO_GOD=malevolent", "ANTHRO_OFF="]),
    ("malevolent2", 61, 80, ["ANTHRO_GOD=malevolent", "ANTHRO_OFF="]),
    ("stern", 41, 60, ["ANTHRO_GOD=stern", "ANTHRO_OFF="]),
    ("stern2", 61, 80, ["ANTHRO_GOD=stern", "ANTHRO_OFF="]),
    ("capricious", 41, 60, ["ANTHRO_GOD=capricious", "ANTHRO_OFF="]),
    ("capricious2", 61, 80, ["ANTHRO_GOD=capricious", "ANTHRO_OFF="]),
]

def run_world(args):
    seed, prefix, years, kin_camps, extra_args = args
    out_file = os.path.join(RUNS_DIR, f"{prefix}_{seed}.txt")
    err_file = os.path.join(RUNS_DIR, f"{prefix}_{seed}_err.txt")
    
    cmd = [DOTNET_EXE, DEMOG_DLL, str(seed), str(years), str(kin_camps)]
    env_vars = os.environ.copy()
    for arg in extra_args:
        if '=' in arg and not arg.startswith('-'):
            k, v = arg.split('=', 1)
            env_vars[k] = v
        else:
            cmd.append(arg)

    t0 = time.time()
    with open(out_file, "w", encoding="utf-8") as out, open(err_file, "w", encoding="utf-8") as err:
        p = subprocess.Popen(cmd, stdout=out, stderr=err, cwd=RUNS_DIR, env=env_vars)
        ret = p.wait()
    elapsed = time.time() - t0
    return prefix, seed, ret, elapsed

def is_already_done(prefix, seed):
    out_file = os.path.join(RUNS_DIR, f"{prefix}_{seed}.txt")
    if not os.path.exists(out_file):
        return False
    try:
        with open(out_file, "r", encoding="utf-8", errors="replace") as f:
            content = f.read()
            return "Y  50 pop" in content or "Y 50 pop" in content
    except:
        return False

def main():
    os.makedirs(RUNS_DIR, exist_ok=True)
    
    force = "--force" in sys.argv
    args_prefixes = [a for a in sys.argv[1:] if not a.startswith("--")]
    
    batches_to_run = all_batches
    if args_prefixes:
        batches_to_run = [b for b in all_batches if b[0] in args_prefixes]

    pending_tasks = []
    for prefix, s_start, s_end, extra in batches_to_run:
        for seed in range(s_start, s_end + 1):
            if force or not is_already_done(prefix, seed):
                pending_tasks.append((seed, prefix, 50, 2, extra))
                
    total_tasks = len(pending_tasks)
    print(f"Total tasks pending: {total_tasks} across {len(batches_to_run)} batches.")
    if total_tasks == 0:
        print("All selected batches already complete!")
        return

    # Use 21 workers (leaving 3 cores for OS / user / responsiveness on a 24-core CPU)
    num_workers = max(1, min(total_tasks, (os.cpu_count() or 24) - 3))
    print(f"Starting continuous pool with {num_workers} parallel workers...")
    t_start = time.time()
    
    completed_count = 0
    with ProcessPoolExecutor(max_workers=num_workers) as executor:
        futures = {executor.submit(run_world, t): (t[1], t[0]) for t in pending_tasks}
        for future in as_completed(futures):
            prefix, seed, ret, elapsed = future.result()
            completed_count += 1
            print(f"[{completed_count:3d}/{total_tasks:3d}] {prefix}_{seed} finished in {elapsed:5.1f}s (exit {ret})", flush=True)

    print(f"\nALL RUNS COMPLETED in {time.time() - t_start:.1f}s!", flush=True)

if __name__ == '__main__':
    main()
