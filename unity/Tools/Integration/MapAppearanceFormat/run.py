"""Compiler and CPU only. Fresh output names required; no Unity launch."""
import pathlib,json,subprocess,hashlib,sys,copy,struct,zlib
R=pathlib.Path(__file__).resolve().parents[4];O=R/'campaign-staging/20261104-three-chats/unity/MapAppearanceFormatR4';D=R/'docs/agent-runs/20261104-three-chats/unity/MapAppearanceFormatR4'
out=O/sys.argv[1];doc=D/sys.argv[1];out.mkdir();doc.mkdir()
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest().upper()
def freeze(p):
 q=out/(str(len(list(out.glob('*.cs'))))+'-'+p.name);q.write_bytes(p.read_bytes());return q
deps=[freeze(pathlib.Path(x['r4pin'])) for x in json.loads((D/'dependencies.json').read_text())]
own=[freeze(p) for p in (R/'unity/Assets/Ashfall/Integration/MapNative').glob('*.cs')]
legacy=[freeze(p) for p in (R/'unity/Tools/Integration/MapNative').glob('*.cs')]
tests=freeze(R/'unity/Tools/Integration/MapAppearanceFormat/FormatTests.cs');nativefixture=freeze(R/'unity/Tools/Integration/MapAppearanceFormat/FormatNativeFixture.cs')
fw=pathlib.Path(r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319');u=pathlib.Path(r'C:\Program Files\Unity\Hub\Editor\6000.6.8f1\Editor\Data')
neutral=[fw/(n+'.dll') for n in ['mscorlib','System','System.Core','System.Runtime.Serialization','System.Xml','System.Xml.Linq']];native=[u/'NetStandard/ref/2.1.0/netstandard.dll']+list((u/'Managed/UnityEngine').glob('*.dll'))
commands=[]
def run(args,expected=0):
 r=subprocess.run(list(map(str,args)),capture_output=True,text=True);commands.append(dict(args=list(map(str,args)),exit=r.returncode,stdout=r.stdout,stderr=r.stderr));(doc/'commands.json').write_text(json.dumps(commands,indent=2));print(r.stdout,r.stderr,flush=True);assert (r.returncode==0)==(expected==0);return r
def compile(name,sources,refs,exe=False,on=False):
 target=out/(name+('.exe' if exe else '.dll'));lines=['/nologo','/nostdlib+','/langversion:5','/warn:4','/warnaserror+','/target:'+('exe' if exe else 'library'),'/out:"'+str(target)+'"']
 if on:lines+=['/define:UNITY_6000_0_OR_NEWER']
 lines+=['/reference:"'+str(p)+'"' for p in refs]+['"'+str(p)+'"' for p in sources];rsp=out/(name+'.rsp');rsp.write_text('\n'.join(lines));run([fw/'csc.exe','/noconfig','@'+str(rsp)]);return target
(doc/'inputs.json').write_text(json.dumps([dict(path=str(p),sha256=sha(p)) for p in deps+own+legacy+[tests,nativefixture,pathlib.Path(__file__)]+neutral+native+[fw/'csc.exe',pathlib.Path(sys.executable)]],indent=2))
dep=compile('Dependencies',deps,neutral);product=compile('MapNative',own,neutral+[dep]);test=compile('FormatTests',[tests],neutral+[dep,product],True);run([test])
compile('Native',deps+own+legacy+[nativefixture],native,on=True)
cpu=compile('LegacyCpu',deps+own+legacy,neutral,True)
def chunk(t,b):return struct.pack('>I',len(b))+t+b+struct.pack('>I',zlib.crc32(t+b))
png=out/'2x2.png';png.write_bytes(b'\x89PNG\r\n\x1a\n'+chunk(b'IHDR',struct.pack('>IIBBBBB',2,2,8,6,0,0,0))+chunk(b'IDAT',zlib.compress(b'\0'+bytes([255,0,0,255,0,255,0,255])+b'\0'+bytes([0,0,255,255,255,255,255,255])))+chunk(b'IEND',b''));run([cpu,png]);run([cpu,R/'campaign-staging/20261104-three-chats/unity/MapStreamingR6/adapter.mapref.json',R/'campaign-staging/20261104-three-chats/unity/MapStreamingR6/world.json'])
if '--full' in sys.argv:
 args=[];template=json.loads((R/'campaign-staging/20261104-three-chats/unity/MapStreamingR6/world.json').read_text())
 for name,total,zero in [('alfa-aglae-souza',1081487,252),('battlefield-paintball-gods-pitdevil',453011,0)]:
  p=O/(name+'.mapref.json');m=json.loads(p.read_text());w=copy.deepcopy(template);w.update(id=m['mapId'],sourcePath=m['source']['path'],sourceSha256=m['source']['sha256'],sourceRevision=m['source']['revision'],indoor=m['policy']['indoor'],capsules=False);wp=out/(name+'.test-world.json');wp.write_text(json.dumps(w));args.extend([p,wp,total,zero])
 run([cpu]+args)
# Compile product mutations, then reuse unchanged separately compiled external tests.
format_source=next(p for p in own if p.name.endswith('AppearanceFormatR4.cs'))
for name,old,new in [('dimension','width<=maxDimension&&height<=maxDimension','width<=4096&&height<=4096'),('mipbudget','n=checked(n+(long)width*height*4)','n=checked((long)width*height*4)')]:
 mutant=out/(name+'.cs');s=format_source.read_text();assert old in s;mutant.write_text(s.replace(old,new));dll=compile('Mutant'+name,[mutant],neutral)
 mt=compile('Test'+name,[tests],neutral+[dll],True);run([mt],expected=1)
(doc/'outputs.json').write_text(json.dumps([dict(path=str(p),bytes=p.stat().st_size,sha256=sha(p)) for p in out.iterdir() if p.is_file()],indent=2))
(doc/'dependency-drift.json').write_text(json.dumps([dict(path=x['original'],pinned=x['sha256'],current=sha(R/x['original'])) for x in json.loads((D/'dependencies.json').read_text())],indent=2))
