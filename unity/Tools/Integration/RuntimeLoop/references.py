from pathlib import Path
import json,hashlib
root=Path(r'C:\Users\aling\Documents\ashfall');base=root/'campaign-staging/20261104-three-chats/unity/RuntimeLoopR1/r5'
rel=Path('campaign-staging/20261104-three-chats/unity/World/r3/inputs/battlefield-paintball-gods-pitdevil.json');p=base/'reference-map.json';p.write_bytes((root/rel).read_bytes())
refs=['docs/TWO-CHAT-20-AGENT-OVERRIDE.md','docs/HILL-CLIMB-ORCHESTRATION.md','docs/OVERNIGHT-AGENT-PLAN-2026-11-03.md','docs/AGENT-DISPATCH-BRIEFS-2026-11-03.md','docs/agent-runs/20261104-0020/preflight.json','unity/Documentation/CONTRACTS-v1.md','unity/Documentation/CONTRACTS-v2.md','unity/Documentation/CONTRACTS-v3.md','unity/Documentation/COLLISION-LAYERS-v1.md','docs/agent-runs/20261104-three-chats/unity/phase05-ownership.json','docs/agent-runs/20261104-three-chats/unity/accepted-neutral-v1.json','docs/agent-runs/20261104-three-chats/unity/accepted-neutral-v2.json','docs/agent-runs/20261104-three-chats/unity/ParityExpansion/r3/source-references.json','docs/agent-runs/20261104-three-chats/unity/ParityExpansion/r3/run-07/expanded.log','docs/agent-runs/20261104-three-chats/world/accepted-handoff-v2.json']
raise SystemExit('Development capture disabled: candidate sealed; new revision required')
records=[]
for rel in refs:
 p=root/rel;d=p.read_bytes();records.append({'path':rel,'sha256':hashlib.sha256(d).hexdigest(),'bytes':len(d)})
 if p.suffix=='.json':
  j=json.loads(d.decode('utf-8-sig'));print(rel, list(j)[:9] if isinstance(j,dict) else len(j))
(base/'reference-manifest.json').write_text(json.dumps(records,indent=2))
p=root/'unity/Tools/Integration/RuntimeLoop/Scenario.cs';s=p.read_text();s=s.replace('var doc=new MapDocument','byte[] actualPilot=File.ReadAllBytes(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"reference-map.json"));bool actualDenied=false;try{ApprovedWorld.Load(actualPilot,WorldLoader.Hash(actualPilot),this);}catch(InvalidDataException){actualDenied=true;}Check(actualDenied,"real Paintball pilot rejected by actual approval boundary");\n        var doc=new MapDocument')
s=s.replace('scenario.SessionComposition();return','scenario.SessionComposition();Console.WriteLine("FINAL "+checks+" PASS 0 FAIL; 8 UNKNOWN groups; complete=false; strict=2");return');p.write_text(s)
p=root/'unity/Assets/Ashfall/Integration/RuntimeLoop/HostLoop.cs';s=p.read_text().replace('else if(f.Action==RuntimeAction.None)action=RuleResult.Accept();','else { var selected=inventory.Inventory(a.Id); if(selected!=null&&!String.IsNullOrEmpty(selected.SelectedInstanceId)) inventory.ApplyWeapon(C(a.Id,"safety",tick),selected.SelectedInstanceId,HandlingAction.Sample,new HandlingInput{Sequence=input.Sequence,DeltaSeconds=dt,Enabled=false,Paused=true,Desktop=true}); if(f.Action==RuntimeAction.None)action=RuleResult.Accept(); }');p.write_text(s)
