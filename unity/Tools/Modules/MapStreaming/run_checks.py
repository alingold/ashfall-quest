"""Run frozen MAPREF tests with unique outputs; never regenerate/overwrite inventory evidence."""
import subprocess,json,hashlib,datetime,sys
from pathlib import Path
ROOT=Path(__file__).resolve().parents[4]
STAGE=ROOT/'campaign-staging/20261104-three-chats/unity/MapStreaming/phase03'
OUT=STAGE/'runs'/datetime.datetime.now(datetime.timezone.utc).strftime('%Y%m%dT%H%M%S%fZ')
OUT.mkdir(parents=True,exist_ok=False)
CSC=Path('C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe')
PROD=ROOT/'unity/Assets/Ashfall/Modules/MapStreaming'
TOOLS=ROOT/'unity/Tools/Modules/MapStreaming'
log=[]
def run(cmd):
 p=subprocess.run([str(x) for x in cmd],cwd=ROOT,capture_output=True,text=True)
 log.append(dict(command=[str(x) for x in cmd],exitCode=p.returncode,stdout=p.stdout,stderr=p.stderr))
 (OUT/'commands.json').write_text(json.dumps(log,indent=2))
 print(p.stdout,flush=True)
 if p.returncode:raise SystemExit(p.returncode)
for name in ['MapStreamingTests','AdversarialTests']:
 run([CSC,'/nologo','/langversion:5','/warnaserror','/r:System.Web.Extensions.dll','/out:'+str(OUT/(name+'.exe')),PROD/'Ports.cs',PROD/'MapStream.cs',TOOLS/(name+'.cs')])
run([OUT/'MapStreamingTests.exe',STAGE/'alfa-aglae-souza.mapref.json',STAGE/'battlefield-paintball-gods-pitdevil.mapref.json'])
run([OUT/'MapStreamingTests.exe',*json.loads((ROOT/'docs/agent-runs/20261104-three-chats/unity/MapStreaming/phase03/fixture-paths.json').read_text())])
run([OUT/'AdversarialTests.exe',STAGE/'fixture-5121-5120.mapref.json',STAGE/'fixture-5121-5120.glb',*json.loads((ROOT/'docs/agent-runs/20261104-three-chats/unity/MapStreaming/phase03/malformed-paths.json').read_text())])
print('Evidence:',OUT)
