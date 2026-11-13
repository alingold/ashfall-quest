from pathlib import Path
import hashlib,json
R=Path(r'C:\Users\aling\Documents\ashfall');D=R/'docs/agent-runs/20261104-three-chats/unity/NativeRootOutcomeR1';S=R/'campaign-staging/20261104-three-chats/unity/NativeRootOutcomeR1';T=R/'unity/Tools/Integration/NativeRootOutcome';P=R/'unity/Assets/Ashfall/Modules/Player/Engine/NativeRootPublication.cs';M=R/'unity/Assets/Ashfall/Modules/Player/Engine/UnityExclusivePlayerMotor.cs'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
assert M.read_bytes()==(S/'before/UnityExclusivePlayerMotor.cs').read_bytes()
symbols=[]
for n,line in enumerate(P.read_text().splitlines(),1):
 if any(x in line for x in ['public static bool Valid','public static NativeRootPublication CreateIsolated','public RuleResult PrepareProduction','public RuleResult PrepareIsolated','public NativeRootStatus Publish','public RuleResult Account','public TerminalRecord Retire','public sealed class Receipt','public void Dispose()']):symbols.append(dict(path=str(P),line=n,source=line.strip()))
(D/'source-symbols.json').write_text(json.dumps(symbols,indent=2));(D/'legacy-preservation.json').write_text(json.dumps(dict(path=str(M),sha256=sha(M),before=str(S/'before/UnityExclusivePlayerMotor.cs'),identical=True),indent=2))
files=[P,M]+[p for root in [D,S,T] for p in root.rglob('*') if p.is_file() and p.name not in ['SHA256-manifest.json','SHA256-manifest.sha256']]
records=[dict(path=str(p.relative_to(R)).replace('\\','/'),bytes=p.stat().st_size,sha256=sha(p)) for p in sorted(set(files))]
(D/'SHA256-manifest.json').write_text(json.dumps(records,indent=2));print('Sealed',len(records),'artifacts')
