from pathlib import Path
import subprocess,sys
HERE=Path(__file__).resolve().parent;ROOT=HERE.parents[3];BASE=ROOT/'unity/Assets/Ashfall';CSC=Path('C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe')
sources=[BASE/'Core/Contracts.cs',BASE/'Integration/Playable/R23VRRig.cs',HERE/'Mechanics.cs']+list((BASE/'Modules/Player').glob('*.cs'))+list((BASE/'Modules/Flight').glob('*.cs'))
args=['/nologo','/langversion:5','/warn:4','/warnaserror+','/target:exe','/out:'+str(HERE/'mechanics.exe'),'/reference:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/System.dll','/reference:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/System.Core.dll']+[str(p)for p in sources]
rsp=HERE/'mechanics.rsp';rsp.write_text('\n'.join('"'+x+'"'for x in args));compile_result=subprocess.run([str(CSC),'/noconfig','@'+str(rsp)],capture_output=True,text=True);(HERE/'compile.log').write_text(compile_result.stdout+compile_result.stderr);print(compile_result.stdout,compile_result.stderr);assert compile_result.returncode==0
result=subprocess.run([str(HERE/'mechanics.exe')],capture_output=True,text=True);(HERE/'results.txt').write_text(result.stdout+result.stderr);print(result.stdout,result.stderr);assert result.returncode==0
print('CPU only. Native/API integration compile and future engine fixture remain explicit separate checks.')
