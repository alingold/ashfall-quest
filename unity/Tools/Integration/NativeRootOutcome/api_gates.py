from pathlib import Path
import json,subprocess,hashlib
R=Path(r'C:\Users\aling\Documents\ashfall');S=R/'campaign-staging/20261104-three-chats/unity/NativeRootOutcomeR1/capture05';D=R/'docs/agent-runs/20261104-three-chats/unity/NativeRootOutcomeR1'
T=S/'api-gates';T.mkdir(exist_ok=False);(T/'api_gates.py').write_bytes(Path(__file__).read_bytes());refs=list((S/'references').glob('*.dll'))+[S/'NativeCore.dll',S/'NativeOwner.dll'];results=[]
for name,body,code in [('forge-receipt','return new NativeRootPublication.Receipt(null,.02f);','CS1729'),('forge-terminal','return new NativeRootPublication.TerminalRecord(null,NativeRootTerminalReason.OwnerRetired);','CS1729'),('mutate-receipt','r.Phase=NativeRootPhase.Completed;return r;','CS0200')]:
 p=T/(name+'.cs');p.write_text('using Ashfall.Modules.Player; public static class Forgery {public static object Try(NativeRootPublication.Receipt r){'+body+'}}')
 rsp=T/(name+'.rsp');rsp.write_text('\n'.join(['/nologo','/langversion:5','/warnaserror+','/nostdlib+','/target:library','/out:"'+str(T/(name+'.dll'))+'"']+['/reference:"'+str(x)+'"' for x in refs]+['"'+str(p)+'"']))
 cmd=[r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe','/noconfig','@'+str(rsp)];c=subprocess.run(cmd,capture_output=True,text=True);(T/(name+'.log')).write_text(c.stdout+c.stderr);results.append(dict(argv=cmd,exit=c.returncode,expectedDiagnostic=code,detected=c.returncode!=0 and code in c.stdout,output=c.stdout+c.stderr))
(D/'api-gates.json').write_text(json.dumps(results,indent=2));assert all(x['detected'] for x in results);print('PASS 3 external API forgery/mutation compile gates; native code not executed')
