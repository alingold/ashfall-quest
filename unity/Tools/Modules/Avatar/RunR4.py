from pathlib import Path
import hashlib,json,shutil,subprocess
R=Path(__file__).resolve().parents[4]; S=R/'campaign-staging/20261104-three-chats/unity/AvatarR4'; E=R/'docs/agent-runs/20261104-three-chats/unity/AvatarR4'; C=S/'consumed'; C.mkdir(exist_ok=True)
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest().upper()
# Freeze only actually consumed dependencies. Source textures/poses stay quarantined references.
refs=[R/'unity/Assets/Ashfall/Core/Contracts.cs',R/'unity/Documentation/CONTRACTS-v3.md',R/'campaign-staging/20261104-three-chats/skins/accepted-manifest-v000.json']
for tree in ['game','quest-r22/source']:
 refs += [R/tree/'scripts'/n for n in ['player_body.gd','grip_hand_086.gd','gemini_player_models_099.gd']]
 refs += list((R/tree/'assets/hands/xrtools086').glob('*.gltf'))+list((R/tree/'assets/hands/xrtools086').glob('*Grip*.res'))
refs += [R/'game/assets/hands/xrtools086/textures_glove_caucasian_dark_camo.png',R/'game/assets/hands/xrtools086/textures_glove_fingerless_occlusionRoughnessMetallic.png']
# Exact real glove material maps, not unused xrtools setup material.

for p in refs:
 out=C/p.relative_to(R);out.parent.mkdir(parents=True,exist_ok=True)
 if not out.exists():shutil.copy2(p,out)
records=[dict(path=str(p),sha256=sha(C/p.relative_to(R)),bytes=(C/p.relative_to(R)).stat().st_size) for p in refs]
(E/'consumed-before.json').write_text(json.dumps(records,indent=2))
# Copy existing sealed fixtures; never invoke historical wrappers or modify old evidence.
prior=R/'campaign-staging/20261104-three-chats/unity/reviews/Avatar-r3'
for p in prior.iterdir():
 if p.is_file() and p.suffix in ['.gltf','.glb','.bin','.expected','.closure']:shutil.copy2(p,S/p.name)
for p in ['IndependentTests.cs','PriorR2Independent.cs']:shutil.copy2(prior/p,S/p)
shutil.copy2(R/'campaign-staging/20261104-three-chats/unity/phase05/small-fixes/Avatar/NullPoseAcceptance.cs',S/'NullPoseAcceptance.cs')
for side in ['left','right']:
 for pose in ['Grip 5','Grip','Grip Shaft','Grip 1']:
  p=C/'game/assets/hands/xrtools086'/(side+'_'+pose+'.res');shutil.copy2(p,S/p.name)
  (S/(p.name+'.sha')).write_text(sha(p))
for kind in ['albedo','orm','normal']:
 p=R/'game/assets/models/gemini_players099/textures'/('glove_camo_'+kind+'.png');q=C/p.relative_to(R);q.parent.mkdir(parents=True,exist_ok=True)
 if not q.exists():shutil.copy2(p,q)
 shutil.copy2(q,S/p.name);records.append(dict(path=str(p),sha256=sha(q),bytes=q.stat().st_size))
(E/'consumed-before.json').write_text(json.dumps(records,indent=2))
compiler=Path('C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe')
sources=[C/'unity/Assets/Ashfall/Core/Contracts.cs']+sorted((R/'unity/Assets/Ashfall/Modules/Avatar').rglob('*.cs'))
results=[]
for name in ['AvatarTests','AvatarR2Tests','AvatarR3Tests','IndependentTests','PriorR2Independent','NullPoseAcceptance','AvatarR4Tests']:
 test=S/(name+'.cs') if (S/(name+'.cs')).exists() else R/'unity/Tools/Modules/Avatar'/(name+'.cs')
 exe=S/(name+'.exe');args=[str(compiler),'/nologo','/langversion:5','/warnaserror+','/target:exe','/out:'+str(exe)]+[str(x) for x in sources]+[str(test)]+([str(R/'unity/Tools/Modules/Avatar/AvatarR4Fixtures.cs')] if name=='AvatarR4Tests' else [])
 r=subprocess.run(args,capture_output=True,text=True);log='COMMAND '+' '.join('"'+a+'"' for a in args)+'\n'+r.stdout+r.stderr+'\nCOMPILE EXIT '+str(r.returncode)+'\n'
 if r.returncode:(E/(name+'.log')).write_text(log);raise SystemExit(log)
 r=subprocess.run([str(exe)],cwd=S,capture_output=True,text=True);log+='COMMAND "'+str(exe)+'"\n'+r.stdout+r.stderr+'\nRUN EXIT '+str(r.returncode)+'\n';(E/(name+'.log')).write_text(log);print(name,r.returncode,r.stdout.splitlines()[-2:]);results.append(dict(test=name,exit=r.returncode))
 if r.returncode:raise SystemExit(log)
(E/'results.json').write_text(json.dumps(results,indent=2))
(E/'dependency-drift.json').write_text(json.dumps([dict(x,current=sha(Path(x['path'])),matches=sha(Path(x['path']))==x['sha256']) for x in records],indent=2))
