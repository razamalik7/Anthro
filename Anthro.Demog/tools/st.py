import re,collections as C,statistics as st,sys
for v in sys.argv[1:]:
    rows=C.defaultdict(list)
    for s in range(41,61):
        for l in open(f'{v}_{s}_err.txt'):
            m=re.match(r'RATION (\w+) budget (\d) ranout (\d) held ([\d.]+) rotted ([\d.-]+) kept ([\d.]+) daysleft ([\d.]+)',l)
            if m: rows[m[1]].append([float(x) for x in m.groups()[1:]])
    out=[]
    for k in ['Autumn','Winter','Spring']:
        r=rows[k]; b=[x for x in r if x[0]==1]
        out.append(f"{k}: kept/person med {st.median(x[4] for x in r):.0f} mean {st.mean(x[4] for x in r):.0f}; budgeted {100*len(b)/len(r):.0f}% ranout {100*sum(x[1] for x in b)/max(1,len(b)):.0f}%; rot/person {st.mean(x[3] for x in r):.1f}")
    print(v,' | '.join(out))
