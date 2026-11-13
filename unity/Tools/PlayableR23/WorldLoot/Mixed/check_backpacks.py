"""Finite bonus-bag policy checks; retains old six-draw method bytes."""
from pathlib import Path
import hashlib,json,subprocess
ROOT=Path(__file__).resolve().parents[5];B=ROOT/'unity/Assets/Ashfall'
OUT=Path('D:/ashfall-r6-staging/cache-drink-feedback-v001/cpu');OUT.mkdir(parents=True,exist_ok=False)
relative=Path('Assets/Ashfall/Integration/Playable/World/Loot/R24MixedLootRecipe.cs')
before=Path('D:/ashfall-r6-staging/cache-drink-feedback-v001/before')/relative
after=ROOT/'unity'/relative;marker=b'public static R24CachePickup[] Cache('
assert before.read_bytes().split(marker,1)[1]==after.read_bytes().split(marker,1)[1], 'Original six item draws changed'
program='''using System;using Ashfall.Integration.Playable.World.Loot;
class Check {static void Main(){int count=0;for(int i=0;i<20000;i++){bool bag=R24MixedLootRecipe.Backpack("archive:alfa-aglae-souza",i);if(bag)count++;if(bag!=R24MixedLootRecipe.Backpack("archive:alfa-aglae-souza",i))throw new Exception("determinism");if(R24MixedLootRecipe.Backpack("archive:magnum-chaosvr",i)||R24MixedLootRecipe.Backpack("archive:magnum-3-chaosvr",i))throw new Exception("source special map");}if(count<4700||count>5300)throw new Exception("bonus distribution");bool rejected=false;try{R24MixedLootRecipe.Backpack("map",-1);}catch(ArgumentOutOfRangeException){rejected=true;}if(!rejected)throw new Exception("negative index");Console.WriteLine("bonus="+count+"/20000; deterministic; both Magnum maps preserved; invalid index refused; old six draws byte-identical");}}
'''
(OUT/'Cases.cs').write_text(program)
files=[B/'Core/Contracts.cs',B/'Integration/Playable/Activities/R23BuildLightningRecipe.cs',B/'Integration/Playable/World/Loot/R23LootRecipe.cs',B/'Integration/Playable/World/Loot/R24MixedLootRecipe.cs',B/'Integration/Playable/World/R24ArchiveMapPolicy.cs',OUT/'Cases.cs']
args=['C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe','/nologo','/warnaserror+','/out:'+str(OUT/'Cases.exe')]+[str(p) for p in files]
r=subprocess.run(args,capture_output=True,text=True);(OUT/'compile.log').write_text(r.stdout+r.stderr);assert r.returncode==0,r.stdout+r.stderr
r=subprocess.run([str(OUT/'Cases.exe')],capture_output=True,text=True);(OUT/'cases.log').write_text(r.stdout+r.stderr);assert r.returncode==0,r.stdout+r.stderr
(OUT/'result.json').write_text(json.dumps(dict(passed=True,nativeExecuted=False,output=r.stdout,oldSixMethodUnchanged=True),indent=2)+'\n');print(r.stdout)
