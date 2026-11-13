from pathlib import Path
import subprocess
T = Path(__file__).resolve().parent
R = T.parents[3]
H = R / 'unity/Assets/Ashfall/Integration/Playable/HandsWeapons'
G = R / 'unity/Assets/Ashfall/Integration/Playable/Gallery'
sources = sorted(set([R / x for x in (R / 'unity/Tools/PlayableR23/HandsWeapons/sources.txt').read_text().splitlines()] + list(H.glob('*.cs')) + list(G.glob('*.cs'))))
args = ['/nologo', '/langversion:5', '/warn:4', '/warnaserror+', '/target:exe', '/out:' + str(T / 'Native141220Cases.exe'), '/reference:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/System.dll', '/reference:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/System.Core.dll'] + [str(p) for p in sources + [T / 'Native141220Cases.cs']]
rsp = T / 'Native141220Cases.rsp'
rsp.write_text('\n'.join('"' + arg + '"' for arg in args))
compile_result = subprocess.run(['C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe', '/noconfig', '@' + str(rsp)], capture_output=True, text=True)
assert compile_result.returncode == 0, compile_result.stdout + compile_result.stderr
result = subprocess.run([str(T / 'Native141220Cases.exe')], capture_output=True, text=True)
print(result.stdout + result.stderr)
assert result.returncode == 0
