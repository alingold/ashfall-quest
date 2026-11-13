from source import *
C=HERE/'Candidate';baselines=json.loads((C/'BASELINES.json').read_text());files=[]
for p in sorted((C/'unity/Assets').rglob('*')):
 if p.is_file():files.append(dict(path=p.relative_to(C).as_posix(),sha256=sha(p),bytes=p.stat().st_size))
manifest=dict(sourceManifestSha256=sha(FROZEN/'SOURCE-MANIFEST.json'),sourceInputs=json.loads((HERE/'source-port.json').read_text())['inputs'],baseProfiles=24,candidateProfiles=26,baselines=baselines,files=files,tests=dict(core=138,integratedRound=29,sourceAssets=83,installedUnityApiTargets=3),nativeRun=False)
(C/'CANDIDATE.json').write_text(json.dumps(manifest,indent=2)+'\n');print('Guarded candidate',len(files),'files;',sum(x['bytes']for x in files),'bytes;',len(baselines),'exact existing baselines')
