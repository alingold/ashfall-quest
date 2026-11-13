from pathlib import Path
import subprocess,json,hashlib,argparse
from analyze import HERE,ROOT,source
from PIL import Image
CSC='C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe';runtime=HERE/'staged/runtime';payload=HERE/'staged/payload'
p=argparse.ArgumentParser();p.add_argument('--all',action='store_true');p.add_argument('--published',action='store_true');p.add_argument('--window-candidate',action='store_true');p.add_argument('--alpha-roads-candidate',action='store_true');args=p.parse_args()
if args.published:runtime=ROOT/'unity/Assets/Ashfall/Integration/Playable/World/SourceArt';payload=ROOT/'unity/Assets/StreamingAssets/PlayableR23/SourceArt'
if args.window_candidate:runtime=HERE/'staged/window-candidate/runtime';payload=HERE/'staged/window-candidate/payload'
if args.alpha_roads_candidate:payload=HERE/'staged/alpha-roads'
def compile(name,files,extra):
 args=['/nologo','/langversion:5','/warnaserror+','/warn:4','/out:'+str(HERE/name)]+extra+[str(p)for p in files];rsp=HERE/(name+'.rsp');rsp.write_text('\n'.join('"'+p+'"'for p in args));r=subprocess.run([CSC,'/noconfig','@'+str(rsp)],capture_output=True,text=True);(HERE/(name+'.log')).write_text(r.stdout+r.stderr);assert r.returncode==0,r.stdout+r.stderr
compile('Cases.exe',[runtime/'R24SourceArtPlan.cs',HERE/'Cases.cs'],['/target:exe']+['/reference:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/'+name+'.dll'for name in ['System','System.Core','System.Web.Extensions']])
command=[str(HERE/'Cases.exe'),str(payload),str(ROOT/'unity/Assets/StreamingAssets/PlayableR23/Maps')]
if args.all:
 from analyze import PLANS
 command.append(','.join(PLANS))
elif args.window_candidate:command.append('classic-bigbox-vr')
elif args.alpha_roads_candidate:command.append('alfa-aglae-souza')
r=subprocess.run(command,capture_output=True,text=True);(HERE/('checks-published.txt'if args.published else'checks-all.txt'if args.all else'checks.txt')).write_text(r.stdout+r.stderr);print(r.stdout+r.stderr);assert r.returncode==0
image_payload=HERE/'staged/payload'if args.alpha_roads_candidate else payload
for row in json.loads((image_payload/'images.json').read_text()):assert hashlib.sha256(Image.open(image_payload/row['file']).convert('RGBA').tobytes()).hexdigest()==row['decodedRgbaSha256']and hashlib.sha256(source(row['sourceResource']).read_bytes()).hexdigest()==row['sourceSha256']
editor=Path('C:/Program Files/Unity/Hub/Editor/6000.6.8f1/Editor/Data');refs=list((editor/'Managed/UnityEngine').glob('*.dll'))+list((editor/'NetStandard/ref/2.1.0').glob('*.dll'));compile('SourceArt.Native.dll',list(runtime.glob('*.cs')),['/target:library','/nostdlib+','/define:UNITY_6000_0_OR_NEWER']+['/reference:'+str(p)for p in refs]);print('PASS installed Unity API; source ETC2-decoded image pixels. Shader/render/native engine not run.')
