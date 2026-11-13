import pathlib,hashlib,json,subprocess
R=pathlib.Path.cwd();S=R/'campaign-staging/20261104-three-chats/unity/ProjectilesR2';D=R/'docs/agent-runs/20261104-three-chats/unity/ProjectilesR2';run=D/'run04';run.mkdir();out=S/'run04';out.mkdir()
p=out/'CatalogHash.cs';p.write_text('static class CatalogHash {public const string Value="'+hashlib.sha256((R/'quest-r22/source/assets/data/one_star_050.json').read_bytes()).hexdigest()+'";}')
before=S/'before';deps=[before/'unity/Assets/Ashfall/Core/Contracts.cs',before/'unity/Assets/Ashfall/Integration/CombatHealthBridge.cs']
for m in ['Combat','HealthStates']:deps+=sorted((before/('unity/Assets/Ashfall/Modules/'+m)).glob('*.cs'))
sources=deps+sorted((R/'unity/Assets/Ashfall/Modules/Projectiles').glob('*.cs'))+[R/'unity/Tools/Modules/Projectiles/ProjectileTests.cs',R/'unity/Tools/Modules/Projectiles/RetryTests.cs',p]
exe=out/'RetryTests.exe';rsp=out/'retry.rsp';rsp.write_text('\n'.join(['/nologo','/langversion:5','/warn:4','/warnaserror+','/target:exe','/main:RetryTests','/out:"'+str(exe)+'"']+['"'+str(x)+'"' for x in sources]))
c=subprocess.run([r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe','@'+str(rsp)],capture_output=True,text=True);(run/'compile.log').write_text(c.stdout+c.stderr);print(c.stdout+c.stderr)
if c.returncode==0:
 t=subprocess.run([str(exe)],capture_output=True,text=True);(run/'tests.log').write_text(t.stdout+t.stderr);print(t.stdout+t.stderr);print('exit',t.returncode)
