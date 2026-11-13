from pathlib import Path
import json,subprocess,hashlib,re
R=Path.cwd();B=R/'campaign-staging/20261104-three-chats/unity/PickupSupplyPreparationR1';H=R/'campaign-staging/20261104-three-chats/unity/reviews/PickupReachableReviewR7';O=B/'retained02';O.mkdir();F=O/'inputs';F.mkdir();rows=[];plans={}
order=['Core','RecoveryContracts','Inventory','Weapon','Pickups','Health','Player','Sessions','Transfers','Recovery','Consumables','Reservations','PickupOriginal','PickupBatch','PickupR4','R3','R4','Composition','R5','ColdInventory','IndependentV2','Extra','Reachable']
for name in order:
 source=(H/('current' if name in order[:11] else 'candidate')/(name+'.rsp'))
 lines=[]
 for line in source.read_text(encoding='utf-8-sig').splitlines():
  v=line.strip('"');p=Path(v)
  if p.suffix=='.cs' and p.is_file():
   if name=='Pickups':continue
   b=p.read_bytes();q=F/(hashlib.sha256(b).hexdigest()[:12]+'-'+p.name)
   if not q.exists():q.write_bytes(b)
   rows.append(dict(path=str(p),snapshot=str(q),sha256=hashlib.sha256(b).hexdigest()));v=str(q)
  else:v=v.replace(str(H/'candidate'),str(O)).replace(str(H/'current'),str(O))
  lines.append(v)
 if name=='Pickups':lines += [str(p) for p in (B/'sources/unity/Assets/Ashfall/Modules/Pickups').glob('*.cs')]
 plans[name]=lines+['/reference:'+str(Path('C:/Windows/Microsoft.NET/Framework64/v4.0.30319')/x) for x in ['System.dll','System.Core.dll','System.Runtime.Serialization.dll','System.Xml.dll']]
(O/'inputs.json').write_text(json.dumps(rows,indent=2));results=[]
def call(cmd,label):
 t=subprocess.run(cmd,capture_output=True,text=True);out=t.stdout+t.stderr;(O/(label+'.log')).write_text(out);results.append(dict(label=label,command=cmd,exit=t.returncode,output=out));(O/'results.json').write_text(json.dumps(results,indent=2));print(label,t.returncode,out[-130:].replace('\n',' '));assert t.returncode==0 and not re.search(r'(^FAIL |failures=[1-9]|^DEFECT )',out,re.M)
for name in order:
 rsp=O/(name+'.rsp');rsp.write_text('\n'.join('"'+v+'"' for v in plans[name]));call([r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe','/noconfig','@'+str(rsp)],name+'-compile')
 if (O/(name+'.exe')).exists():
  args=[['capture',str(O/(name+'.bin'))],['restore',str(O/(name+'.bin'))]] if name in ['R5','ColdInventory'] else [['warm',str(O)],['cold',str(O)]] if name in ['Extra','Reachable'] else [[str(O/'R5.bin')]] if name=='IndependentV2' else [[]]
  for i,a in enumerate(args):call([str(O/(name+'.exe'))]+a,name+'-run'+str(i))
