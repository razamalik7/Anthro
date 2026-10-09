import sys
import os

ROOT_DIR = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
RUNS_DIR = os.path.join(ROOT_DIR, "runs")
ANA_PY = os.path.join(ROOT_DIR, "Anthro.Demog", "tools", "ana.py")

prefixes = ["base", "idle", "base2", "idle2", "godoff_self", "godoff_self2", "kind", "kind2", "kind_nooffer", "kind_norite", "kind_nopray", "kind_bountyonly", "kind_weatheronly"]

print("Wait for batches to complete, then I will run ana.py on them")
