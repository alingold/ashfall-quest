from health_epoch_r2 import R,S,D
import json,subprocess,shutil,hashlib
base=S/'final02';out=S/'supplement';out.mkdir();results=[]
for p in base.glob('*.dll'):shutil.copyfile(p,out/p.name)
def run(cmd):
 p=subprocess.run(list(map(str,cmd)),capture_output=True,text=True);results.append(dict(command=list(map(str,cmd)),exit=p.returncode,stdout=p.stdout,stderr=p.stderr));(out/'commands.json').write_text(json.dumps(results,indent=2));print(p.returncode,p.stdout[-600:],p.stderr[-600:]);p.check_returncode()
def compile(name,sources):
 rsp=out/(name+'.rsp');rsp.write_text('\n'.join(['/nologo','/langversion:5','/warn:4','/warnaserror+','/target:exe','/out:"'+str(out/(name+'.exe'))+'"']+['/r:"'+str(out/(n+'.dll'))+'"' for n in ['Core','Recovery','HealthOwner']]+['"'+str(x)+'"' for x in sources]));run([r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe','@'+str(rsp)])
q=out/'HealthEpochNativeFixture.cs';shutil.copyfile(R/'unity/Tools/Modules/HealthStates/HealthEpochNativeFixture.cs',q)
entry=out/'FixtureMain.cs';entry.write_text('class MainFixture {static void Main(){Ashfall.Tools.HealthStates.HealthEpochNativeFixture.Run();System.Console.WriteLine("PASS callable native fixture CPU execution only");}}')
compile('Fixture',[q,entry]);run([out/'Fixture.exe'])
q=out/'ArchiveTests.cs';shutil.copyfile(R/'unity/Tools/Integration/HealthArchiveRecords/Tests.cs',q)
compile('Archive',[q]);run([out/'Archive.exe',out])
# Current comparison is a separate record, never substituted into final02 results.
drift=[]
for x in json.loads((base/'inputs.json').read_text()):
 p=__import__('pathlib').Path(x['source']);actual=hashlib.sha256(p.read_bytes()).hexdigest()
 if actual!=x['sha256']:drift.append(dict(source=str(p),frozen=x['sha256'],current=actual))
(D/'current-drift.json').write_text(json.dumps(drift,indent=2));print('current drift',len(drift))
