from pathlib import Path
import hashlib,json,uuid
HERE=Path(__file__).resolve().parent;ROOT=HERE.parents[5];LIVE=ROOT/'unity/Assets/Ashfall/Integration/Playable';STAGE=HERE/'Staged'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
p=LIVE/'R23PlayableLoop.GhostRecovery.cs';t=p.read_text()
replacements=[
 ('Map!=null&&R24GhostRecoveryMath.Supported(Map.MapId)','Map!=null&&(R24GhostRecoveryMath.Supported(Map.MapId)||ArchiveGhostRecoveryReady)'),
 ('GhostRecoverySupported?null:"Archive _outside/Magnum-only center resource binding remains pending"','GhostRecoverySupported?null:ArchiveGhostRecoveryFailure??"Preparing actual source archive recovery sites"'),
 ('if(!MatchActive)return;','if(!MatchActive)return;PrepareArchiveGhostRecovery();'),
 ('if(!MatchLocalGhost||!GhostRecoverySupported)return;','if(MatchLocalGhost&&Map.MapId.StartsWith("archive:",System.StringComparison.Ordinal)){TryRecoverArchiveGhost();return;}\n            if(!MatchLocalGhost||!GhostRecoverySupported)return;')]
for a,b in replacements:assert t.count(a)==1,a;t=t.replace(a,b)
(STAGE/p.name).write_text(t)
rows=[]
for p in STAGE.rglob('*.cs'):
 rel=p.relative_to(STAGE).as_posix();dst=LIVE/rel
 rows.append(dict(path=rel,baselineSha256=sha(dst)if dst.exists()else None,stagedSha256=sha(p)))
 meta=p.with_name(p.name+'.meta');original=dst.with_name(dst.name+'.meta')
 if original.exists():meta.write_bytes(original.read_bytes())
 else:meta.write_text('fileFormatVersion: 2\nguid: '+uuid.uuid5(uuid.NAMESPACE_URL,'ashfall/R6/ghost/archive/'+rel).hex+'\nMonoImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n')
(HERE/'candidate.json').write_text(json.dumps(dict(root=str(LIVE),files=rows,payloadSha256=sha(HERE/'Payload/maps.json'),preservedBuiltinMathSha256=sha(LIVE/'Match/R24GhostRecoveryMath.cs'),preservedRigRelocationSha256=sha(LIVE/'R23VRRig.GhostRecovery.cs')),indent=2)+'\n')
print('Staged',len(rows),'archive recovery sources; guarded existing builtin bridge and new metadata, no Assets writes.')
