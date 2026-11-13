"""Read-only Windows heavy-owner check; exact Hub services are not Editors.

This does not acquire lanes, query a headset, stop a process, or establish a
file signature. Actual admission still requires both existing nonblocking locks.
"""
from pathlib import Path
import argparse,datetime,json,subprocess,sys
p=argparse.ArgumentParser();p.add_argument('--receipt',type=Path,required=True);a=p.parse_args()
command=r'''Get-CimInstance Win32_Process | Where-Object { $_.Name -match '^(Unity|Unity Hub|Ashfall.*|Godot.*|blender)\.exe$' -or ($_.Name -match 'python' -and $_.CommandLine -match 'gpu_worker|engine_lane') } | Select-Object ProcessId,ParentProcessId,Name,ExecutablePath,CommandLine | ConvertTo-Json -Compress'''
r=subprocess.run(['powershell.exe','-NoProfile','-Command',command],capture_output=True,text=True,errors='replace',creationflags=subprocess.CREATE_NO_WINDOW)
if r.returncode:raise SystemExit('Process inventory failed; no admission: '+r.stderr)
rows=json.loads(r.stdout)if r.stdout.strip()else[]
if isinstance(rows,dict):rows=[rows]
by_id={row['ProcessId']:row for row in rows};owners=[];services=[]
for row in rows:
 name=(row.get('Name')or'').lower();image=(row.get('ExecutablePath')or'').replace('/','\\').lower();line=(row.get('CommandLine')or'').strip();parent=by_id.get(row['ParentProcessId'],{})
 parent_image=(parent.get('ExecutablePath')or'').replace('/','\\').lower();parent_name=(parent.get('Name')or'').lower()
 hub_image='\\windowsapps\\unitytechnologies.unityhub_'in image and image.endswith('\\app\\resources\\unity.exe')
 # Require the complete image + sole argument + actual Hub parent for the new
 # broker. A command-line substring is never a broad unity.exe exemption.
 auth_image=image==r'c:\users\aling\appdata\local\unity\bin\unity.exe'
 sole_auth=line.lower()in[image+' --internal-auth-broker-serve','"'+image+'" --internal-auth-broker-serve']
 hub_parent=parent_name=='unity hub.exe'and'\\windowsapps\\unitytechnologies.unityhub_'in parent_image
 sole_serve=hub_image and line.lower()in[image+' serve','"'+image+'" serve']
 if name=='unity.exe'and(sole_serve or auth_image and sole_auth and hub_parent):
  services.append(dict(pid=row['ProcessId'],image=image,kind='verified Hub-parent auth broker'if auth_image else'exact Hub serve service',parentPid=row['ParentProcessId'],identity='CIM only; no on-disk hash/signature claim'));continue
 if name=='unity hub.exe':continue
 owners.append(dict(pid=row['ProcessId'],parentPid=row['ParentProcessId'],name=row['Name'],image=image))
report=dict(utc=datetime.datetime.now(datetime.timezone.utc).isoformat(),scope='Win32 process inventory only; no headset query',clear=not owners,heavyOrPlayerOwners=owners,excludedHubServices=services,processesStopped=False,locksAcquired=False)
assert not a.receipt.exists(),'Preserve previous admission evidence'
a.receipt.parent.mkdir(parents=True,exist_ok=True);a.receipt.write_text(json.dumps(report,indent=2)+'\n');print(json.dumps(report,indent=2));sys.exit(0 if report['clear'] else 75)
