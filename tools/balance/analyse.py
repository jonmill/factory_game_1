import json,re,sys
from collections import defaultdict
src=open(sys.argv[1]).read()
src=re.sub(r'^\s*//.*$','',src,flags=re.M)
d=json.loads(src)
res={r['item'] for r in d['resources']}
rec={}
for r in d['recipes']:
    for o in r['out']:
        rec.setdefault(o,r)
memo={}
def cost(i):
    # returns (rawdict, time_s, depth)
    if i in memo: return memo[i]
    if i in res or i not in rec:
        if i in rec and rec[i]['category']=='orbital':
            r=rec[i]; v=({i:1.0}, r['ticks']/60/r['out'][i],0)
        else: v=({i:1.0},0.0,0)
        memo[i]=v; return v
    r=rec[i]; n=r['out'][i]; raw=defaultdict(float); t=r['ticks']/60; dep=0
    for k,q in r['in'].items():
        cr,ct,cd=cost(k)
        for a,b in cr.items(): raw[a]+=b*q
        t+=ct*q; dep=max(dep,cd)
    v=({a:b/n for a,b in raw.items()}, t/n, dep+1); memo[i]=v; return v
def bag(c):
    raw=defaultdict(float);t=0
    for k,q in c.items():
        cr,ct,_=cost(k)
        for a,b in cr.items(): raw[a]+=b*q
        t+=ct*q
    return sum(raw.values()),t,raw
print("== ITEMS raw / time s / depth")
for it in d['items']:
    i=it['id']; cr,ct,cd=cost(i)
    if cd>0 or i in rec: print(f"{i:24s} {sum(cr.values()):7.1f} {ct:7.1f} d{cd}  {dict((k,round(v,2)) for k,v in cr.items())}")
print("== RECIPE effects")
for r in d['recipes']:
    if not r['out']:
        s,t,_=bag(r['in']); print(f"{r['id']:22s} raw {s:7.1f} time {t+r['ticks']/60:6.1f} {r.get('effect')}")
print("== BUILDINGS raw")
for b in d['buildings']+d['vehicles']:
    if 'cost' in b:
        s,t,_=bag(b['cost']); print(f"{b['id']:20s} {b.get('age',''):10s} raw {s:7.1f} time {t:7.1f}")
print("== TECHS")
ages=defaultdict(lambda:[0,0,0.0,0.0,[]])
tot=defaultdict(float)
for t in d['techs']:
    s,tm,_=bag(t['unitCost']); a=ages[t['age']]
    rep=t.get('repeatable',False)
    print(f"{t['id']:24s} {t['age']:10s} packs={len(t['unitCost'])} units={t['units']:4d} ticks={t['unitTicks']} raw={s*t['units']:8.0f} {'REP' if rep else ''} pre={t.get('prereqs',[])}")
    if not rep:
        a[0]+=1;a[1]+=t['units'];a[2]+=s*t['units'];a[3]+=t['units']*t['unitTicks']/60;a[4].append(t['units'])
        for k,q in t['unitCost'].items(): tot[k]+=q*t['units']
for k,a in ages.items():
    u=sorted(a[4]); print(k,"techs",a[0],"units",a[1],"raw",round(a[2]),"lab-hours",round(a[3]/3600,1),"median",u[len(u)//2] if u else 0, "min/max",u[0] if u else 0,u[-1] if u else 0)
print("cores consumed",dict(tot)); 
print("total raw",sum(a[2] for a in ages.values()),"units",sum(a[1] for a in ages.values()))
print(len(d['items']),"items",len(d['recipes']),"recipes",len(d['buildings']),"buildings",len(d['techs']),"techs")
