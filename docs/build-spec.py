"""Inject discovery-web.json and web-map.js into milestone-1-spec.html.

Run after editing either file:  python build-spec.py
"""
import json
import pathlib
import re

here = pathlib.Path(__file__).parent
page = here / "milestone-1-spec.html"
data = json.loads((here / "discovery-web.json").read_text(encoding="utf-8"))
script = (here / "web-map.js").read_text(encoding="utf-8")

stages = {s["id"] for t in data["tiles"] for s in t["stages"]}
bad = [(s["id"], r) for t in data["tiles"] for s in t["stages"]
       for n in s.get("needs", []) for r in n.get("refs", []) if r not in stages]
if bad:
    raise SystemExit(f"Unknown stage refs: {bad}")

needs = {s["id"]: [r for n in s.get("needs", []) if n["t"] != "Helps" for r in n.get("refs", [])]
         for t in data["tiles"] for s in t["stages"]}
done, path = set(), []
def visit(sid):
    if sid in path:
        raise SystemExit("Circular requirement: " + " -> ".join(path[path.index(sid):] + [sid]))
    if sid in done:
        return
    path.append(sid)
    for r in needs[sid]:
        visit(r)
    path.pop()
    done.add(sid)
for sid in needs:
    visit(sid)

html = page.read_text(encoding="utf-8")
blob = json.dumps(data, ensure_ascii=False).replace("</", "<\\/")
block = (f'<script id="web-data" type="application/json">{blob}</script>\n'
         f"<script>\n{script}</script>\n")
html = re.sub(r"<script id=\"web-data\".*\Z|<script>.*\Z", lambda _: block, html, count=1, flags=re.S)
page.write_text(html, encoding="utf-8")
print(f"built: {len(data['tiles'])} skills, {len(stages)} stages")
