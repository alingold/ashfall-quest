from pathlib import Path
import subprocess,re
HERE=Path(__file__).resolve().parent;ROOT=HERE.parents[5]
LIVE=ROOT/'unity/Assets/Ashfall/Editor/Playable/R24R6UiSmoke.cs'
text=LIVE.read_text(encoding='utf-8');assert 'public bool AutoFinish=true;'in text and 'if(AutoFinish)R23SmokeControl.Finish(Passed,Json)'in text
assert 'loop.Lobby.MenuSize' in text and 'mesh.sharedMesh.bounds' in text and 'WorldToViewportPoint(corner)'in text
body=text.split('void Capture(Group g',1)[1].split('sealed class Saved',1)[0]
assert 'panel.position='not in body and 'panel.SetPosition'not in body and 'loop.Rig.Head.transform.Set'not in body
CSC=Path('C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe');out=HERE/'CaptureFitCases.exe'
r=subprocess.run([str(CSC),'/nologo','/warn:4','/warnaserror+','/out:'+str(out),str(LIVE),str(HERE/'CaptureFitCases.cs')],capture_output=True,text=True);print(r.stdout+r.stderr);assert r.returncode==0
r=subprocess.run([str(out)],capture_output=True,text=True);print(r.stdout+r.stderr);assert r.returncode==0
r=subprocess.run(['python',str(HERE/'check.py'),'--real-ports'],capture_output=True,text=True);print(r.stdout+r.stderr);assert r.returncode==0
print('Capture-only actual-canvas fit and installed Editor/runtime API PASS. Native/capture/device UNRUN.')
