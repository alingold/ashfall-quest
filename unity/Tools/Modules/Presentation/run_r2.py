"""Run fresh neutral tests in a NEW owned output folder; never touch frozen r1/r2 binaries."""
import sys, subprocess, json, hashlib
from pathlib import Path
ROOT=Path(__file__).resolve().parents[4]
STAGING=ROOT/'campaign-staging/20261104-three-chats/unity/Presentation'
CSC=Path('C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe')

def run(output):
    output=Path(output).resolve()
    if STAGING.resolve() not in output.parents:
        raise ValueError('Output must be below owned Presentation staging')
    output.mkdir(parents=True,exist_ok=False)
    sources=[ROOT/'unity/Assets/Ashfall/Core/Contracts.cs']+sorted((ROOT/'unity/Assets/Ashfall/Modules/Presentation').glob('*.cs'))
    tests={'BuilderTests':ROOT/'unity/Tools/Modules/Presentation/PresentationTests.cs',
           'AdversarialTests':ROOT/'campaign-staging/20261104-three-chats/unity/reviews/Presentation-r1/AdversarialTests.cs',
           'R2Tests':ROOT/'unity/Tools/Modules/Presentation/PresentationR2Tests.cs'}
    results=[]
    for name,test in tests.items():
        exe=output/(name+'.exe');rsp=output/(name+'.rsp')
        rsp.write_text('\n'.join(['/nologo','/langversion:5','/warnaserror+','/target:exe','/out:"'+str(exe)+'"']+['"'+str(p)+'"' for p in sources+[test]])+'\n')
        compile_result=subprocess.run([str(CSC),'@'+str(rsp)],capture_output=True,text=True)
        (output/(name+'-compile.log')).write_text(compile_result.stdout+compile_result.stderr)
        if compile_result.returncode: raise RuntimeError(compile_result.stdout+compile_result.stderr)
        result=subprocess.run([str(exe)],capture_output=True,text=True)
        (output/(name+'.log')).write_text(result.stdout+result.stderr)
        print(result.stdout,end='')
        results.append(dict(name=name,compileExit=compile_result.returncode,testExit=result.returncode,exeSha256=hashlib.sha256(exe.read_bytes()).hexdigest()))
    (output/'results.json').write_text(json.dumps(results,indent=2)+'\n')
    return 1 if any(r['testExit'] for r in results) else 0

if __name__=='__main__':
    if len(sys.argv)!=2: raise SystemExit('usage: run_r2.py NEW_ABSOLUTE_OUTPUT_DIRECTORY')
    raise SystemExit(run(sys.argv[1]))
