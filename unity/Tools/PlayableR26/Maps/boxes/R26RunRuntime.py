import subprocess,json
from pathlib import Path
out=Path('/home/al/ashfall/campaign/boxes/runtime');rig='/home/al/ashfall/tools/mp-rig/mp_rig.py';build='/home/al/ashfall/builds/r12b-boxes-20261115-2113'
results=[]
for short,mid in [('magnum','archive:magnum-chaosvr'),('viral','archive:viral-gods-ghostlymostly88'),('flash','archive:flash-gods-pitdevil'),('badlands','archive:badlands-gods-pitdevil'),('alfa','archive:alfa-aglae-souza')]:
 folder=out/short;args=[rig,'run','--build',build,'--map',mid,'--minutes','.35','--mode','solo','--roam','none','--out',str(folder),'--host-args','-ashfallBoxesEvidence '+str(folder/'views')]
 print('START',short,flush=True)
 with (out/(short+'-run.log')).open('w') as log:r=subprocess.run(args,cwd='/home/al/ashfall/unity',stdout=log,stderr=subprocess.STDOUT,timeout=360)
 results.append(dict(mapId=mid,exitCode=r.returncode));(out/'run-results.json').write_text(json.dumps(results,indent=1)+'\n');print('DONE',short,r.returncode,flush=True)
