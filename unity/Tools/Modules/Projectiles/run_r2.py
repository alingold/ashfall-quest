import pathlib,json,subprocess,hashlib,shutil
R=pathlib.Path.cwd();S=R/'campaign-staging/20261104-three-chats/unity/ProjectilesR2';D=R/'docs/agent-runs/20261104-three-chats/unity/ProjectilesR2'
run=D/'run01';run.mkdir(exist_ok=False);out=S/'run01';out.mkdir()
before=S/'before'; deps=[before/'unity/Assets/Ashfall/Core/Contracts.cs',before/'unity/Assets/Ashfall/Integration/CombatHealthBridge.cs']
for m in ['Combat','HealthStates']:deps+=sorted((before/('unity/Assets/Ashfall/Modules/'+m)).glob('*.cs'))
review=R/'campaign-staging/20261104-three-chats/unity/reviews/Projectiles-r1'
for name in ['Adversarial-v3.cs','CurrentGains.cs']:shutil.copyfile(review/name,S/name)
def execute(name,sources,main):
 exe=out/(name+'.exe');rsp=out/(name+'.rsp');rsp.write_text('\n'.join(['/nologo','/langversion:5','/warn:4','/warnaserror+','/target:exe','/main:'+main,'/out:"'+str(exe)+'"']+['"'+str(p)+'"' for p in sources]))
 c=subprocess.run([r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe','@'+str(rsp)],capture_output=True,text=True);(run/(name+'-compile.log')).write_text(c.stdout+c.stderr)
 print(name,'compile',c.returncode,c.stdout+c.stderr)
 if c.returncode:return
 t=subprocess.run([str(exe)],capture_output=True,text=True);(run/(name+'-tests.log')).write_text(t.stdout+t.stderr);print(t.stdout+t.stderr)
base=deps+sorted((before/'unity/Assets/Ashfall/Modules/Projectiles').glob('*.cs'))+[before/'unity/Tools/Modules/Projectiles/ProjectileTests.cs',S/'Adversarial-v3.cs']
execute('baseline47',base,'ProjectileTests');execute('baseline37',base,'Adversarial')
candidate=deps+sorted((R/'unity/Assets/Ashfall/Modules/Projectiles').glob('*.cs'))+[R/'unity/Tools/Modules/Projectiles/ProjectileTests.cs',S/'Adversarial-v3.cs',S/'CurrentGains.cs']
execute('candidate47',candidate,'ProjectileTests');execute('candidate37',candidate,'Adversarial');execute('candidate-gains',candidate,'CurrentGains')
