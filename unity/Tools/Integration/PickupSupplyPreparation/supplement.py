from pathlib import Path
import subprocess,json
R=Path.cwd();B=R/'campaign-staging/20261104-three-chats/unity/PickupSupplyPreparationR1';C=B/'cpu05';O=B/'supplement01';O.mkdir()
s=(C/'ActualSupplyOwnerTests.rsp').read_text().replace(str(C/'ActualSupplyOwnerTests.exe'),str(C/'ActualSupplyOwnerTestsFixed.exe'));s+='\n/r:"C:\\Windows\\Microsoft.NET\\Framework64\\v4.0.30319\\System.Runtime.Serialization.dll"\n/r:"C:\\Windows\\Microsoft.NET\\Framework64\\v4.0.30319\\System.Xml.dll"\n';rsp=O/'ActualSupplyOwnerTests.rsp';rsp.write_text(s)
cmd=[r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe','/noconfig','@'+str(rsp)];t=subprocess.run(cmd,capture_output=True,text=True);(O/'compile.log').write_text(t.stdout+t.stderr);print(t.returncode,t.stdout);assert t.returncode==0
x=subprocess.run([str(C/'ActualSupplyOwnerTestsFixed.exe')],capture_output=True,text=True);(O/'tests.log').write_text(x.stdout+x.stderr);print(x.returncode,x.stdout[-500:]);(O/'results.json').write_text(json.dumps(dict(command=cmd,compileExit=t.returncode,executionExit=x.returncode),indent=2));assert x.returncode==0
