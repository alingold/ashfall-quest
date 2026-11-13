import pathlib,hashlib,json,shutil,subprocess,sys
R=pathlib.Path.cwd(); S=R/'campaign-staging/20261104-three-chats/unity/SourceExplosiveAmmoR2'; D=R/'docs/agent-runs/20261104-three-chats/unity/SourceExplosiveAmmoR2'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
if not (S/'frozen').exists():
 rows=[]
 folders=['Core','RecoveryContracts','Modules/WeaponHandling','Modules/InventoryItems','Modules/HealthStates','Modules/Combat','Integration/ProjectileWeapons']
 for folder in folders:
  for p in sorted((R/'unity/Assets/Ashfall'/folder).glob('*.cs')):
   q=S/'frozen'/p.relative_to(R);q.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(p,q);rows.append({'original':p.relative_to(R).as_posix(),'sha256':sha(q)})
 for root in ['game','quest-r22/source']:
  for name in ['explosive_weapon.gd','explosive_projectile.gd','weapon_inventory.gd','weapon.gd','grenade_ballistics.gd']:
   p=R/root/'scripts'/name;q=S/'frozen'/p.relative_to(R);q.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(p,q);rows.append({'original':p.relative_to(R).as_posix(),'sha256':sha(q)})
 (D/'inputs.json').write_text(json.dumps(rows,indent=2))
 # Parse complete coordination ledgers without dumping their huge per-file arrays.
 evidence=[]
 for p in sorted((R/'docs/agent-runs/20261104-three-chats/unity').glob('accepted-neutral-v*.json'))+[R/'docs/agent-runs/20261104-three-chats/unity/phase21/source-index.json']:
  data=json.loads(p.read_text(encoding='utf-8-sig'));evidence.append({'path':str(p.relative_to(R)),'sha256':sha(p),'keys':list(data) if isinstance(data,dict) else len(data)})
 (D/'coordination-read.json').write_text(json.dumps(evidence,indent=2))
name=sys.argv[1];O=S/name;O.mkdir();E=D/name;E.mkdir();results=[]
def compile(name,src,refs=()):
 out=O/name;args=['/nologo','/langversion:5','/warn:4','/warnaserror+','/target:'+('exe' if name.endswith('.exe') else 'library'),'/out:"'+str(out)+'"']+['/reference:"'+str(x)+'"' for x in refs]+['"'+str(x)+'"' for x in src];rsp=O/(name+'.rsp');rsp.write_text('\n'.join(args));cmd=[r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe','@'+str(rsp)];p=subprocess.run(cmd,capture_output=True,text=True);(E/(name+'.log')).write_text(p.stdout+p.stderr);results.append({'command':cmd,'exit':p.returncode});print(name,p.returncode,p.stdout[-2500:]);(E/'commands.json').write_text(json.dumps(results,indent=2));
 if p.returncode:sys.exit(1)
 return out
def src(folder):return sorted((S/'frozen/unity/Assets/Ashfall'/folder).glob('*.cs'))
core=compile('Core.dll',src('Core'));wire=compile('Recovery.dll',src('RecoveryContracts'),[core]);inv=compile('Inventory.dll',src('Modules/InventoryItems'),[core,wire]);weapon=compile('Weapon.dll',src('Modules/WeaponHandling'),[core,wire]);health=compile('Health.dll',src('Modules/HealthStates'),[core,wire]);combat=compile('Combat.dll',src('Modules/Combat'),[core,wire,health]);refs=[core,wire,inv,weapon,health,combat];launch=compile('Launch.dll',src('Integration/ProjectileWeapons'),refs);refs.append(launch)
leaf=R/'unity/Assets/Ashfall/Integration/SourceExplosiveAmmo/ExplosiveAmmoAdmission.cs';test=R/'unity/Tools/Integration/SourceExplosiveAmmo/Tests.cs'
for p in [leaf,test,pathlib.Path(__file__).resolve()]:shutil.copyfile(p,O/p.name)
lib=compile('Ammo.dll',[O/leaf.name],refs);exe=compile('Tests.exe',[O/test.name],refs+[lib]);p=subprocess.run([str(exe)],capture_output=True,text=True);(E/'tests.log').write_text(p.stdout+p.stderr);print(p.stdout,p.stderr);results.append({'command':[str(exe)],'exit':p.returncode});
if p.returncode:sys.exit(1)
# Mutation changes the unsupported source return to successful admission, without
# adding a real debit/launch. External tests must detect this false authority.
s=(O/leaf.name).read_text();mutant=O/'FalseSuccess.cs';mutant.write_text(s.replace('return RuleResult.Reject(item.DefinitionId == "smaw" ? MissingRocket : MissingThrow);','return RuleResult.Accept();'))
m=compile('Mutant.dll',[mutant],refs);me=compile('MutantTests.exe',[O/test.name],refs+[m]);p=subprocess.run([str(me)],capture_output=True,text=True);(E/'mutant.log').write_text(p.stdout+p.stderr);results.append({'command':[str(me)],'exit':p.returncode,'expected':'nonzero assertion failure'});print('mutant',p.returncode)
mutation=O/'mutant-exclusion';mutation.mkdir();(E/'mutant-exclusion').mkdir()
for dep in refs:shutil.copyfile(dep,mutation/dep.name)
wp=src('Modules/WeaponHandling'); changed=mutation/'PreparedWeapons.cs';text=next(p for p in wp if p.name=='PreparedWeapons.cs').read_text();old='if(FireReservation!=null && (!(FireReservation is IPreparedWeaponFire)||now<=FireReservationExpiry))return false;';assert old in text;changed.write_text(text.replace(old,''));mw=compile('mutant-exclusion/Weapon.dll',[changed if p.name=='PreparedWeapons.cs' else p for p in wp],[core,wire]);mutrefs=[mutation/p.name for p in refs];ml=compile('mutant-exclusion/Ammo.dll',[O/leaf.name],mutrefs);mt=compile('mutant-exclusion/Tests.exe',[O/test.name],mutrefs+[ml]);mp=subprocess.run([str(mt)],capture_output=True,text=True);(E/'mutant-exclusion.log').write_text(mp.stdout+mp.stderr);results.append({'command':[str(mt)],'exit':mp.returncode,'expected':'nonzero actual fire/suspension exclusion assertion'});assert mp.returncode
(E/'commands.json').write_text(json.dumps(results,indent=2));(E/'outputs.json').write_text(json.dumps([{'path':str(p.relative_to(R)),'sha256':sha(p)} for p in O.iterdir() if p.is_file()],indent=2));
drift=[dict(row,liveSha256=sha(R/row['original'])) for row in json.loads((D/'inputs.json').read_text()) if sha(R/row['original'])!=row['sha256']];(E/'dependency-drift.json').write_text(json.dumps(drift,indent=2));sys.exit(0 if p.returncode else 1)

