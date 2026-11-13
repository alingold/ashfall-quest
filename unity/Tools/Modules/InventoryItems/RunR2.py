from pathlib import Path
import subprocess,json,hashlib
r=Path('C:/Users/aling/Documents/ashfall')
out=r/'campaign-staging/20261104-three-chats/unity/InventoryItems/r2'
csc=Path('C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe')
sources=[r/'unity/Assets/Ashfall/Core/Contracts.cs']+sorted((r/'unity/Assets/Ashfall/Modules/InventoryItems').glob('*.cs'))
results=[]
for name in ['InventoryTests','ReviewTests','SortParity','TransactionTests']:
    test=(r/'unity/Tools/Modules/InventoryItems'/(name+'.cs')) if name in ['InventoryTests','TransactionTests'] else out/(name+'.cs')
    rsp=out/(name+'.rsp')
    args=['/nologo','/langversion:5','/warnaserror+','/target:exe','/out:"'+str(out/(name+'.exe'))+'"']+['"'+str(p)+'"' for p in sources+[test]]
    rsp.write_text('\n'.join(args)+'\n')
    commands=[[str(csc),'@'+str(rsp)],[str(out/(name+'.exe'))]]
    for kind,command in zip(['compile','run'],commands):
        result=subprocess.run(command,capture_output=True,text=True,cwd=r)
        (out/(name+('.compiler.log' if kind=='compile' else '.log'))).write_text(result.stdout+result.stderr,encoding='utf-8')
        results.append(dict(suite=name,kind=kind,command=command,exit=result.returncode))
        print(name,kind,result.returncode,(result.stdout+result.stderr)[-350:])
        if result.returncode:break
(out/'commands-results.json').write_text(json.dumps(results,indent=2))

import sys
sys.exit(1 if any(row['exit'] for row in results) else 0)
