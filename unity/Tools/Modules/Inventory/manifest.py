"""Seal the current candidate changed-file list; excludes its own output to avoid recursion."""
import inventory as tool

roots=[tool.ROOT/'unity/Assets/Ashfall/Modules/Inventory',tool.ROOT/'unity/Tools/Modules/Inventory',tool.OUT,tool.EVIDENCE]
destination=tool.EVIDENCE/'candidate-manifest.json'
files=[]
for root in roots:
    for path in sorted(root.rglob('*')):
        if path.is_file() and path!=destination:
            files.append({'path':path.relative_to(tool.ROOT).as_posix(),'bytes':path.stat().st_size,'sha256':tool.sha(path)})
tool.write(destination,{'schema':1,'status':'candidate awaiting independent review','baseline':tool.BASELINE,'excludes':'candidate-manifest.json itself; all other current owned files included','files':files})
print('Sealed '+str(len(files))+' candidate files')
