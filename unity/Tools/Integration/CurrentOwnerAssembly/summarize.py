"""Rooted diagnostic and frozen package/source-symbol maps; no live recapture."""
import re
import sys
from pathlib import Path
import current_owner as c
run=c.BASE/sys.argv[1];plan=c.load(run/'plan.json');cap=c.load(run/'capture.json');units=plan['units']
out=run/'analysis';out.mkdir()
graph={n:a['projectReferences'] for n,a in units.items()}
# Tarjan SCCs distinguish a real shared partial split from downstream blocking.
index={};low={};stack=[];on=set();groups=[]
def visit(n):
    index[n]=low[n]=len(index);stack.append(n);on.add(n)
    for d in graph[n]:
        if d not in index:visit(d);low[n]=min(low[n],low[d])
        elif d in on:low[n]=min(low[n],index[d])
    if low[n]==index[n]:
        g=[]
        while True:
            d=stack.pop();on.remove(d);g.append(d)
            if d==n:break
        if len(g)>1:groups.append(sorted(g))
for n in graph:
    if n not in index:visit(n)
c.dump(out/'cycles.json',groups)
symbols=[]
for name,a in units.items():
    for s in a['sources']:
        for i,line in enumerate((run/'snapshot'/s).read_text(encoding='utf-8-sig').splitlines(),1):
            if re.search(r'\b(namespace|using|public|internal)\b|VerifyCached|PlayerInput.Finite|WithNetworkConfigParameters',line):symbols.append({'unit':name,'source':s,'line':i,'text':line,'sourceSha256':c.sha((run/'snapshot'/s).read_bytes())})
c.dump(out/'source-symbols.json',symbols)
for profile in ('cpu','guarded'):
    results=c.load(run/'validation02'/profile/'results.json')
    def roots(n,seen):
        if n in seen:return ['cycle:'+n]
        r=results[n]
        if r==0:return []
        if isinstance(r,int):return ['compiler:'+n]
        if 'cycle' in r:return ['cycle:'+'->'.join(r['cycle'])]
        if 'blocked' in r:return sorted({v for d in r['blocked'] for v in roots(d,seen|{n})})
        return [str(r)]
    failures={n:{'disposition':r,'roots':roots(n,set())} for n,r in results.items() if r!=0}
    for n,f in failures.items():
        log=run/'validation02'/profile/n/'compiler.log'
        if log.exists():f['diagnostics']=log.read_text(errors='replace')
    c.dump(out/(profile+'-rooted-failures.json'),failures)
# Resolve package GUID edges from frozen metadata only, retaining unresolved edges.
catalog={};guid={};versions=[]
for r in cap['files']:
    s=r['path'];p=run/'snapshot'/s
    if '/PackageCache/' not in s:continue
    if s.endswith('/package.json'):
        d=c.load(c.safe(p));versions.append({'name':d.get('name'),'version':d.get('version'),'path':s,'sha256':r['sha256']})
    if s.endswith('.asmdef'):
        d=c.load(c.safe(p));m=run/'snapshot'/(s+'.meta');text=c.safe(m).read_text() if c.safe(m).exists() else '';g=re.search(r'^guid:\s*(\w+)',text,re.M)
        catalog[d['name']]={'source':s,'sha256':r['sha256'],'definition':d,'guid':g[1] if g else None}
        if g:guid[g[1]]=d['name']
for n,a in catalog.items():a['resolvedReferences']=[guid.get(r[5:],'UNRESOLVED:'+r) if r.startswith('GUID:') else r for r in a['definition'].get('references',[])]
c.dump(out/'installed-package-identities.json',versions)
c.dump(out/'declared-package-edges.json',{n:{'packages':[{ 'name':k,'identity':catalog.get(k),'dll':next((r for r in cap['readonlyReferences'] if Path(r['path']).name==k+'.dll'),None)} for k in a['packageReferences']]} for n,a in units.items() if a['packageReferences']})
# Exhaustive tool C# enumeration prevents the two executed fixtures from being
# mistaken for complete gameplay regression or native/TestRunner acceptance.
selected={s for f in plan['fixtures'] for s in f['sources']}
c.dump(out/'fixture-coverage.json',[{'source':r['path'],'sha256':r['sha256'],'disposition':'selected declared fixture' if r['path'] in selected else 'NOT_RUN: owner fixture needs explicit dependency/main/inputs enrollment; not a passing gate'} for r in cap['files'] if r['path'].startswith('unity/Tools/') and r['path'].endswith('.cs')])
print('analysis complete')
