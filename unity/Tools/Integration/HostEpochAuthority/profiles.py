from pathlib import Path
import hashlib,json
r=Path.cwd();d=r/'docs/agent-runs/20261104-three-chats/unity/HostEpochAuthorityR1';records=json.loads((d/'inputs.json').read_text())
canonical='\n'.join(str(Path(v['source']).relative_to(r)).replace('\\','/')+' '+v['sha256'] for v in records)
digest=hashlib.sha256(canonical.encode()).hexdigest()
p=r/'unity/Assets/Ashfall/Integration/HostEpochAuthority/SourceProfiles.cs'
assert not p.exists()
p.write_text('namespace Ashfall.Integration.HostEpochAuthority {\n public static class SourceProfiles {\n public const string Build="'+digest+'";\n public const string Health="HealthStates/2 + prepared publication/checkpoint schema1";\n public const string Combat="Combat-r2/2 + checkpoint schema1";\n public const string Inventory="InventoryRecovery/schema1";\n public const string Weapon="WeaponHandling-r3/1 + PreparedFire";\n public const string Session="Sessions/4 + prepared join/life/match";\n }\n}\n')
(d/'source-profile.txt').write_text(canonical+'\naggregate-sha256 '+digest+'\n')
print(digest)
