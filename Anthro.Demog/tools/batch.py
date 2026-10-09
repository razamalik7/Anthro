#!/usr/bin/env python3
"""
Runs Demog simulations in parallel across a range of seeds and saves output to runs/<prefix>_<seed>.txt.
Usage:
    python Anthro.Demog/tools/batch.py <prefix> [start_seed=41] [end_seed=60] [years=50] [kinCamps=2] [extra_demog_args...]
"""
import sys
import os
import subprocess
import time
from concurrent.futures import ProcessPoolExecutor, as_completed

DOTNET_EXE = r"C:\Program Files\dotnet\dotnet.exe"
ROOT_DIR = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
RUNS_DIR = os.path.join(ROOT_DIR, "runs")
DEMOG_DLL = os.path.join(RUNS_DIR, "bin", "Demog.dll")

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
    return seed, ret, elapsed

def main():
    if len(sys.argv) < 2:
        print("Usage: batch.py <prefix> [start_seed=41] [end_seed=60] [years=50] [kinCamps=2] [extra_args...]")
        sys.exit(1)

    prefix = sys.argv[1]
    start_seed = int(sys.argv[2]) if len(sys.argv) > 2 else 41
    end_seed = int(sys.argv[3]) if len(sys.argv) > 3 else 60
    years = int(sys.argv[4]) if len(sys.argv) > 4 else 50
    kin_camps = int(sys.argv[5]) if len(sys.argv) > 5 else 2
    extra_args = sys.argv[6:] if len(sys.argv) > 6 else []

    os.makedirs(RUNS_DIR, exist_ok=True)
    seeds = list(range(start_seed, end_seed + 1))
    tasks = [(s, prefix, years, kin_camps, extra_args) for s in seeds]

    print(f"Starting batch '{prefix}' for seeds {start_seed}..{end_seed} ({years} years, {kin_camps} kin camps)...")
    t_start = time.time()
    
    # Run with up to 16 parallel workers
    with ProcessPoolExecutor(max_workers=min(len(seeds), 16)) as executor:
        futures = {executor.submit(run_world, t): t[0] for t in tasks}
        for future in as_completed(futures):
            seed, ret, elapsed = future.result()
            print(f"  [seed {seed:2d}] finished in {elapsed:5.1f}s (exit {ret})")

    total_time = time.time() - t_start
    print(f"Batch completed in {total_time:.1f}s.")

    # Run ana.py
    ana_script = os.path.join(ROOT_DIR, "Anthro.Demog", "tools", "ana.py")
    if os.path.exists(ana_script):
        print("\n--- Analysis Output ---")
        p = subprocess.run([sys.executable, ana_script, prefix], cwd=RUNS_DIR, text=True, capture_output=True)
        print(p.stdout)
        if p.stderr:
            print("ana.py stderr:", p.stderr)

if __name__ == "__main__":
    main()
