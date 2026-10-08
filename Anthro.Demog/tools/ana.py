import re,statistics as st,sys,glob
def load(v):
    out=[]
    for f in sorted(glob.glob(f'{v}_*[0-9].txt')):
        s=f
        t=open(f,encoding='utf-8',errors='replace').read()
        ys=re.findall(r'\nY\s*(\d+) pop\s+(\d+).*?starv\s+(\d+) viol\s+(\d+) fed ([\d.]+)',t)
        r=[float(x) for x in re.findall(r'rationing ([\d.]+)',t)]
        L=re.findall(r'rationlessons up (\d+) down (\d+) held (\d+) none (\d+)',t)
        les=[sum(int(x[i]) for x in L) for i in range(4)] if L else [0]*4
        cm=sum(int(x) for x in re.findall(r'campmoves (\d+)',t)); gang=sum(int(x) for x in re.findall(r'gang:(\d+)',t)); viol=sum(int(x) for x in re.findall(r' viol\s+(\d+)',t))
        out.append(dict(cm=cm,gang=gang,viol=viol,s=s,pop=int(ys[-1][1]) if ys else 0,starv=sum(int(y[2]) for y in ys),r=r,les=les,yrs=len(ys)))
    return out
for v in sys.argv[1:]:
    o=load(v)
    traj=[st.mean(x['r'][i] for x in o if len(x['r'])>i) for i in range(6)]
    print(v,'medpop',st.median(x['pop'] for x in o),'total',sum(x['pop'] for x in o),'starv',sum(x['starv'] for x in o),'ext',sum(1 for x in o if x['yrs']<50),'campmoves',sum(x['cm'] for x in o),'gang',sum(x['gang'] for x in o),'viol',sum(x['viol'] for x in o),'ration Y1,10..50',' '.join(f'{a:.2f}' for a in traj),'lessons',[sum(x['les'][i] for x in o) for i in range(4)])
    print('   per-world end:',' '.join(f"{x['r'][-1]:.2f}" for x in o if x['r']))
