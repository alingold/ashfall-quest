"""Independent PCG/djb2 vectors + cache activation boundaries. No engine."""
from pathlib import Path
import json,struct,subprocess,hashlib
R=Path(__file__).resolve().parents[4];B=R/'unity/Assets/Ashfall'
O=R/'campaign-staging/20261104-three-chats/unity/playable-r23/world-loot-cpu';O.mkdir(parents=True,exist_ok=True)
MASK=(1<<64)-1
def f32(v):return struct.unpack('<f',struct.pack('<f',v))[0]
def source_hash(s):
    h=5381
    for c in s:
        if not ord(c):break
        h=(h*33+ord(c))&0xffffffff
    return h
class Pcg:
    def __init__(self,seed):self.state=0;self.draw();self.state=(self.state+seed)&MASK;self.draw()
    def draw(self):
        old=self.state;self.state=(old*6364136223846793005+2885390081777926815)&MASK
        x=(((old>>18)^old)>>27)&0xffffffff;r=old>>59
        return ((x>>r)|(x<<((-r)&31)))&0xffffffff
    def below(self,n):
        threshold=((1<<32)-n)%n
        while True:
            x=self.draw()
            if x>=threshold:return x%n
    def value(self):
        e=self.draw()
        if not e:return 0.
        return f32(f32(self.draw()|0x80000001)*2.**(-32-(32-e.bit_length())))
ACTIVE=[0,1,2,3,5,6,7,8,9,10,11,12,13,14,15,16,17,18,19,20,21,23,24,25,26,27,28,29]
MELEE={14,15,16,29};EXP={26,27,28}
def cache(m,i,magnum=False):
    r=Pcg(source_hash(m)+i*7907);chosen=[];result=[]
    for slot in range(6):
        pool=[7] if magnum else [c for c in ACTIVE if c not in chosen and (not MELEE.intersection(chosen) or c not in MELEE) and (not EXP.intersection(chosen) or c not in EXP)]
        c=pool[r.below(len(pool))];chosen.append(c)
        stars=1+sum(r.value()>limit for limit in [.45,.75,.91,.98]);result.append((c,stars))
    return result
maps=['archive:battlefield-paintball-gods-pitdevil','archive:alfa-aglae-souza','archive:classic-bigbox-vr','range','valley']
lines=[]
for m in maps:
    for i in [0,1,11,12,129,337,65535]:
        lines.append(m+'|'+str(i)+'|'+','.join(str(v) for p in cache(m,i) for v in p))
(O/'vectors.txt').write_text('\n'.join(lines)+'\n')
source=r'''using System;using System.IO;using System.Collections.Generic;using Ashfall.Core;using Ashfall.Integration.Playable.World.Loot;
class Check{
 static int count;static void Need(bool p,string s){count++;if(!p)throw new Exception(s);}
 static void Main(string[] args){
  foreach(string line in File.ReadAllLines(args[0])){var p=line.Split('|');int index=int.Parse(p[1]);var expected=p[2].Split(',');var actual=R23LootRecipe.Cache(p[0],index);var used=new HashSet<int>();int melee=0,explosive=0;
   for(int i=0;i<6;i++){Need(actual[i].Catalog==int.Parse(expected[i*2])&&actual[i].Stars==int.Parse(expected[i*2+1]),"cross-language RNG vector "+line);Need(used.Add(actual[i].Catalog),"duplicate catalog");Need(actual[i].Slot==i,"slot placement index");if(new List<int>{14,15,16,29}.Contains(actual[i].Catalog))melee++;if(new List<int>{26,27,28}.Contains(actual[i].Catalog))explosive++;}Need(melee<=1&&explosive<=1,"restricted source classes");
  }
  Need(R23LootRecipe.SourceHash("abc")==193485963u,"djb2 addition");Need(R23LootRecipe.SourceHash("a\U0001f600")!=R23LootRecipe.SourceHash("a"),"UTF32 string hashing");
  foreach(var p in R23LootRecipe.Cache("only",0,true))Need(p.Catalog==7,"Magnum-only keeps source repeated catalog");
  var points=new Float3[30];for(int i=0;i<points.Length;i++)points[i]=new Float3(i,0,0);var a=new R23CacheActivation(points);points[0]=new Float3(999,999,999);
  var first=a.Activate(new[]{new Float3(0,0,0)},true);Need(first.Length==12&&first[0]==0&&first[11]==11,"initial12 source-order and immutable inputs");
  var second=a.Activate(new[]{new Float3(0,0,0)},false);Need(second.Length==2&&second[0]==12&&second[1]==13,"runtime2 no repeated sites");
  Need(a.Activate(new[]{new Float3(999,999,999)},false).Length==0,"far player cannot activate");
  var edge=new R23CacheActivation(new[]{new Float3(35,0,0),new Float3(0,35,0),new Float3(0,0,65)});
  Need(edge.Activate(new[]{new Float3(0,0,0)},true).Length==0,"35m radius strict and three-dimensional");Need(edge.Activate(new[]{new Float3(0,0,0)},false).Length==2,"65m radius strict");
  var copy=new R23CacheActivation(new[]{new Float3(0,0,0)});Need(copy.Activate(new[]{new Float3(100,0,0),new Float3(0,0,0)},true).Length==1,"any source player activates exactly once");Need(copy.Activate(new[]{new Float3(0,0,0)},true).Length==0,"repeat initial cannot duplicate caches");
  bool rejected=false;try{R23LootRecipe.Cache("map",-1);}catch(ArgumentOutOfRangeException){rejected=true;}Need(rejected,"negative index refusal");Console.WriteLine(count+" PASS");
 }}'''
(O/'Check.cs').write_text(source)
files=[B/'Core/Contracts.cs',B/'Integration/Playable/Activities/R23BuildLightningRecipe.cs',B/'Integration/Playable/World/Loot/R23LootRecipe.cs',B/'Integration/Playable/World/R24ArchiveMapPolicy.cs',O/'Check.cs']
csc=Path('C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe')
cmd=[str(csc),'/nologo','/langversion:5','/warnaserror+','/out:'+str(O/'Check.exe')]+[str(f) for f in files]
c=subprocess.run(cmd,capture_output=True,text=True);print(c.stdout+c.stderr);assert c.returncode==0
x=subprocess.run([str(O/'Check.exe'),str(O/'vectors.txt')],capture_output=True,text=True);print(x.stdout+x.stderr);assert x.returncode==0
(O/'result.json').write_text(json.dumps(dict(result=x.stdout.strip(),sourceHashes={str(p.relative_to(R)):hashlib.sha256(p.read_bytes()).hexdigest() for p in files[:3]},oracle='Independent Python PCG/djb2 implementation using Godot source algorithm; actual native Godot oracle not run',engineRun=False),indent=2))
