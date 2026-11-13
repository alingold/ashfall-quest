from pathlib import Path
import json,subprocess
r=Path.cwd();o=r/'campaign-staging/20261104-three-chats/unity/InventoryRecoveryR1';rec=json.loads((r/'docs/agent-runs/20261104-three-chats/unity/InventoryRecoveryR1/before.json').read_text());src=o/'Inspect.cs';assert not src.exists(), 'Existing inspection evidence is immutable'
src.write_text('using System;using System.Reflection;class Inspect {static void Main(){foreach(Type t in new Type[]{typeof(Ashfall.Modules.InventoryItems.Inventory),typeof(Ashfall.Modules.WeaponHandling.WeaponHandling),typeof(Ashfall.Modules.WeaponHandling.FiniteAmmoReserve),typeof(Ashfall.Modules.WeaponHandling.WeaponHandling).Assembly.GetType("Ashfall.Modules.WeaponHandling.ClosureFlick")}){Console.WriteLine("TYPE "+t.FullName);foreach(var f in t.GetFields(BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public|BindingFlags.DeclaredOnly))Console.WriteLine(f.Name+":"+f.FieldType.ToString());}}}')
cmd=[r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe','/nologo','/langversion:5','/warnaserror+','/out:'+str(o/'Inspect.exe'),str(src)]+[v['snapshot'] for v in rec]
p=subprocess.run(cmd,capture_output=True,text=True);print(p.stdout,p.stderr);p.check_returncode();p=subprocess.run([str(o/'Inspect.exe')],capture_output=True,text=True);(o/'fields.txt').write_text(p.stdout);print(p.stdout)

