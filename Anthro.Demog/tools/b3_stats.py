import re, sys, glob, statistics as st

def load(prefix):
    births = []
    starv = []
    deaths = []
    cm = []
    pray = []
    offer = []
    fed_winter = []
    for f in sorted(glob.glob(f'runs/{prefix}_*[0-9].txt')):
        t = open(f, encoding='utf-8', errors='replace').read()
        ys = re.findall(r'\nY\s*(\d+) pop\s+(\d+).*?b\s+(\d+) d\s+(\d+) starv\s+(\d+)', t)
        births.append(sum(int(y[2]) for y in ys))
        starv.append(sum(int(y[4]) for y in ys))
        deaths.append(sum(int(y[3]) for y in ys))
        
        cm_vals = sum(int(x) for x in re.findall(r'campmoves (\d+)', t))
        cm.append(cm_vals)
        
        pray_vals = [float(x) for x in re.findall(r' Pray ([\d.]+)', t)]
        offer_vals = [float(x) for x in re.findall(r' Offer ([\d.]+)', t)]
        pray.append(st.mean(pray_vals) if pray_vals else 0)
        offer.append(st.mean(offer_vals) if offer_vals else 0)
        
        winter_vals = [float(x) for x in re.findall(r'Winter [\d.]+?/([\d.]+)', t)]
        fed_winter.append(st.mean(winter_vals) if winter_vals else 0)
        
    return {
        'b': st.mean(births) if births else 0,
        's': st.mean(starv) if starv else 0,
        'd': st.mean(deaths) if deaths else 0,
        'cm': st.mean(cm) if cm else 0,
        'pray': st.mean(pray) if pray else 0,
        'offer': st.mean(offer) if offer else 0,
        'win': st.mean(fed_winter) if fed_winter else 0
    }

print(f"{'Run':<20} {'Births':>8} {'Starv':>8} {'Deaths':>8} {'CampMv':>8} {'Pray_h':>8} {'Off_h':>8} {'WinFed':>8}")
for p in sys.argv[1:]:
    res = load(p)
    print(f"{p:<20} {res['b']:>8.1f} {res['s']:>8.1f} {res['d']:>8.1f} {res['cm']:>8.1f} {res['pray']:>8.3f} {res['offer']:>8.3f} {res['win']:>8.3f}")
