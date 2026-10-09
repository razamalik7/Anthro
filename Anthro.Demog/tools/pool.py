#!/usr/bin/env python3
"""
Continuous Worker Pool for Anthro Simulations.
Keeps all available CPU cores 100% saturated without waiting for batch tails.
"""
import os
import sys
import subprocess
import time
from concurrent.futures import ProcessPoolExecutor, as_completed

DOTNET_EXE = r"C:\Program Files\dotnet\dotnet.exe"
ROOT_DIR = os.path.abspath(os.path.dirname(__file__))
RUNS_DIR = os.path.join(ROOT_DIR, "runs")
DEMOG_DLL = os.path.join(RUNS_DIR, "bin", "Demog.dll")

all_batches = [
    # B0a & B0b baselines
    ("godoff", 41, 60, ["ANTHRO_OFF=god"]),
    ("godoff2", 61, 80, ["ANTHRO_OFF=god"]),
    ("idle", 41, 60, ["ANTHRO_OFF="]),
    ("idle2", 61, 80, ["ANTHRO_OFF="]),
    ("self_off", 41, 60, ["ANTHRO_OFF=god,selfmemory"]),
    ("self_off2", 61, 80, ["ANTHRO_OFF=god,selfmemory"]),
    
    # B3 Kind bot and ablations
    ("kind", 41, 60, ["ANTHRO_GOD=kind", "ANTHRO_OFF="]),
    ("kind2", 61, 80, ["ANTHRO_GOD=kind", "ANTHRO_OFF="]),
    ("kind_nooffer", 41, 60, ["ANTHRO_GOD=kind", "ANTHRO_OFF=offer"]),
    ("kind_nooffer2", 61, 80, ["ANTHRO_GOD=kind", "ANTHRO_OFF=offer"]),
    ("kind_norite", 41, 60, ["ANTHRO_GOD=kind", "ANTHRO_OFF=rite"]),
    ("kind_norite2", 61, 80, ["ANTHRO_GOD=kind", "ANTHRO_OFF=rite"]),
    ("kind_nopray", 41, 60, ["ANTHRO_GOD=kind", "ANTHRO_OFF=pray"]),
    ("kind_nopray2", 61, 80, ["ANTHRO_GOD=kind", "ANTHRO_OFF=pray"]),
    ("kind_bountyonly", 41, 60, ["ANTHRO_GOD=kind", "ANTHRO_OFF=weather,omen,bless,nudge"]),
    ("kind_bountyonly2", 61, 80, ["ANTHRO_GOD=kind", "ANTHRO_OFF=weather,omen,bless,nudge"]),
    ("kind_weatheronly", 41, 60, ["ANTHRO_GOD=kind", "ANTHRO_OFF=bounty,omen,bless,nudge"]),
    ("kind_weatheronly2", 61, 80, ["ANTHRO_GOD=kind", "ANTHRO_OFF=bounty,omen,bless,nudge"]),
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
    # Check if reaches Y 50
    try:
        with open(out_file, "r", encoding="utf-8", errors="replace") as f:
            content = f.read()
            return "Y  50 pop" in content or "Y 50 pop" in content
    except:
        return False

def main():
    os.makedirs(RUNS_DIR, exist_ok=True)
    
    # Build list of pending runs across all batches
    pending_tasks = []
    for prefix, s_start, s_end, extra in all_batches:
        for seed in range(s_start, s_end + 1):
            if not is_already_done(prefix, seed):
                pending_tasks.append((seed, prefix, 50, 2, extra))
                
    total_tasks = len(pending_tasks)
    print(f"Total tasks pending: {total_tasks} across all batches.")
    if total_tasks == 0:
        print("All batches already complete!")
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
