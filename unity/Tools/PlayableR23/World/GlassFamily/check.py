"""Check the staged glass additions through the real SourceArt plan/API."""
from prepare import *
import subprocess

CSC='C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe'
RUNTIME=ROOT/'unity/Assets/Ashfall/Integration/Playable/World/SourceArt'

def compile(name,files,extra):
 args=['/nologo','/langversion:5','/warnaserror+','/warn:4','/out:'+str(HERE/name)]+extra+[str(p)for p in files];rsp=HERE/(name+'.rsp');rsp.write_text('\n'.join('"'+p+'"'for p in args));r=subprocess.run([CSC,'/noconfig','@'+str(rsp)],capture_output=True,text=True);(HERE/(name+'.log')).write_text(r.stdout+r.stderr);assert r.returncode==0,r.stdout+r.stderr

def main():
 parser=argparse.ArgumentParser();parser.add_argument('--published',action='store_true');published=parser.parse_args().published;plans=LIVE/'SourceArt'if published else OUT
 protected={p:sha(p)for p in (LIVE/'SourceArt').rglob('*')if p.is_file()};protected.update({p:sha(p)for p in RUNTIME.rglob('*')if p.is_file()});total=0;source_total=0;native=0;prior=0;negative=0
 for slug in source.PLANS:
  report=json.loads((OUT/(slug+'-checks.json')).read_text());base=LIVE/'SourceArt'/(slug+'.json');assert sha(base)==report['stagedPlanSha256'if published else'basePlanSha256'];data=json.loads((plans/(slug+'.json')).read_text());assert data['geometrySha256']==sha(LIVE/'Maps'/slug/'geometry.bytes')and sha(LIVE/'Maps'/slug/'map.json')==report['mapManifestSha256'];assert sha(OUT/(slug+'.json'))==report['stagedPlanSha256']
  assert report['sourceTrianglesUnmatched']==0 and report['sourceTrianglesMatched']==report['sourceCensus']['clearSourceTriangles'];assert report['maxCornerDistance']<=.005
  total+=report['newClearGlassTriangles'];source_total+=report['sourceTrianglesMatched'];native+=report['alreadyNativeClearGlassTriangles'];prior+=report['alreadySourceArtGlassTriangles'];negative+=report['sourceCensus']['opaqueAttachmentParts']
  if slug=='alfa-aglae-souza':assert report['newClearGlassTriangles']==0 and report['alreadyNativeClearGlassTriangles']==1460 and base.read_bytes()==(OUT/(slug+'.json')).read_bytes(),'Already-correct ALFA native pane ownership must not be duplicated'
  current_glass={(p['chunk'],t)for p in data['patches']for part in p['parts']if part['kind']=='clear_glass'for t in part['triangles']}
  if slug=='classic-bigbox-vr':
   assert {(185,351),(185,352),(186,284),(186,285)}.issubset(current_glass)
   assert not current_glass.intersection({(266,4977),(266,6978),(270,18228)}),'Actual opaque frame/shutter/attachment controls must remain opaque'
 assert total==11700 and source_total==13164 and native==1460 and prior==4 and negative>20000
 compile('GlassFamily.Cases.exe',[RUNTIME/'R24SourceArtPlan.cs',HERE.parent/'SourceArt/Cases.cs'],['/target:exe']+['/reference:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/'+n+'.dll'for n in ['System','System.Core','System.Web.Extensions']])
 r=subprocess.run([str(HERE/'GlassFamily.Cases.exe'),str(plans),str(LIVE/'Maps'),','.join(source.PLANS)],capture_output=True,text=True);(HERE/('published-owner-checks.txt'if published else'owner-checks.txt')).write_text(r.stdout+r.stderr);print(r.stdout+r.stderr);assert r.returncode==0
 editor=Path('C:/Program Files/Unity/Hub/Editor/6000.6.8f1/Editor/Data');refs=list((editor/'Managed/UnityEngine').glob('*.dll'))+list((editor/'NetStandard/ref/2.1.0').glob('*.dll'));compile('GlassFamily.Native.dll',list(RUNTIME.glob('*.cs')),['/target:library','/nostdlib+','/define:UNITY_6000_0_OR_NEWER']+['/reference:'+str(p)for p in refs])
 assert all(sha(p)==digest for p,digest in protected.items())
 result=dict(passed=True,published=published,maps=9,matchedActualSourcePaneTriangles=source_total,newClearGlassTriangles=total,existingNativeClearGlassTrianglesPreserved=native,existingClassicFourSourceArtTrianglesPreserved=prior,sourceOpaqueAttachmentPartsExcluded=negative,actualClassicOpaqueControlsPreserved=True,allPreviousSourceArtAndCollisionOwnershipPreserved=True,installedApiCompile=True,liveAssetsFilesUnchanged=len(protected),assetsWritten=False,engineRun=False,nativeVisualConfirmationPending=True)
 (HERE/('published-checks.json'if published else'checks.json')).write_text(json.dumps(result,indent=2)+'\n');print(json.dumps(result,indent=2))

if __name__=='__main__':main()
