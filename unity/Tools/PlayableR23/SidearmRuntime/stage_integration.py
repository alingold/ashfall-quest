from pathlib import Path
import difflib
root=Path.cwd();tools=root/'unity/Tools/PlayableR23/SidearmRuntime';draft=tools/'IntegrationDraft';draft.mkdir(exist_ok=True)
p=root/'unity/Assets/Ashfall/Integration/Playable/HandsWeapons/R23RoundState.cs';before=p.read_text();s=before
s=s.replace('string selectedRifle="mk18";', 'string selectedRifle="mk18",magnumInstanceId;\n        IRevisionedAmmoReserve pistolReserve;\n        public R23MagnumRuntime Magnum {get;private set;}\n        public string ActiveWeaponId {get{return sourceLoadout?selectedRifle:"service_rifle";}}\n        public HandlingSnapshot ActiveSnapshot {get{return selectedRifle=="magnum"&&Magnum!=null?Magnum.Snapshot:Rifle.Snapshot;}}\n        public int ActiveLoaded {get{return selectedRifle=="magnum"&&Magnum!=null?Magnum.Loaded:Rifle.LoadedRounds;}}\n        public int ActiveCapacity {get{return selectedRifle=="magnum"?7:Rifle.MagazineCapacity;}}\n        public int ActiveReserve {get{return selectedRifle=="magnum"&&Magnum!=null?Magnum.Reserve:Rifle.Reserve;}}\n        public string ActiveInstanceId {get{return selectedRifle=="magnum"&&Magnum!=null?Magnum.InstanceId:Rifle.InstanceId;}}\n        // Call only after the existing inventory owner accepts an explicit Magnum grant.\n        // This allocates mechanics, not an inventory slot; repeat calls never refill.\n        public bool EnsureMagnumState(string stableId){if(!sourceLoadout||string.IsNullOrEmpty(stableId)||stableId.Length>128)return false;if(Magnum!=null)return Magnum.InstanceId==stableId;magnumInstanceId=stableId;Magnum=new R23MagnumRuntime(pistolReserve,stableId);return true;}\n        public void SuspendActiveWeapon(){if(selectedRifle=="magnum"&&Magnum!=null)Magnum.SuspendControls();else Rifle.SuspendControls();pendingShot=false;}\n        public void Retire(){foreach(var weapon in rifles.Values)weapon.Retire();if(!sourceLoadout&&Rifle!=null)Rifle.Retire();if(Magnum!=null)Magnum.Retire();pendingShot=false;}')
s=s.replace('public void Reset() {','public void Reset() {\n            if(Magnum!=null)Magnum.Retire();Magnum=null;pistolReserve=R23MagnumRuntime.CreatePistolReserve();')
s=s.replace('Rifle=rifles[selectedRifle];','if(magnumInstanceId!=null)Magnum=new R23MagnumRuntime(pistolReserve,magnumInstanceId);\n                Rifle=rifles[selectedRifle=="magnum"?"mk18":selectedRifle];')
s=s.replace('public bool SelectWeapon(string id) {','public bool SelectWeapon(string id) {\n            if(id=="magnum"){if(!sourceLoadout||Magnum==null)return false;if(selectedRifle==id)return true;SuspendActiveWeapon();Magnum.SuspendControls();selectedRifle=id;return true;}')
s=s.replace('if(next==Rifle)return true;Rifle.SuspendControls();','if(next==Rifle&&selectedRifle==id)return true;SuspendActiveWeapon();')
s=s.replace('public HandlingSnapshot ReadWeapon(string id) {','public HandlingSnapshot ReadWeapon(string id) { if(id=="magnum"&&Magnum!=null)return Magnum.Snapshot;')
s=s.replace('public string WeaponInstanceId(string id) {','public string WeaponInstanceId(string id) { if(id=="magnum"&&Magnum!=null)return Magnum.InstanceId;')
s=s.replace('public int ReadReserve(string id) {','public int ReadReserve(string id) { if(id=="magnum"&&Magnum!=null)return Magnum.Reserve;')
s=s.replace('public bool SuspendWeapon(string id) {','public bool SuspendWeapon(string id) { if(id=="magnum"&&Magnum!=null){Magnum.SuspendControls();pendingShot=false;return true;}')
s=s.replace('public bool Step(float dt,HandlingInput input,bool reload,bool paused) {','public bool Step(float dt,HandlingInput input,bool reload,bool paused) {\n            if(selectedRifle=="magnum")return StepMagnum(dt,new R23MagnumInput(input,primaryButton:reload,triggerBlocksFlick:true),paused);')
pos=s.index('        public HealthSnapshot ReadTarget')
s=s[:pos]+'''        public bool StepMagnum(float dt,R23MagnumInput frame,bool paused) {
            if(!sourceLoadout||selectedRifle!="magnum"||Magnum==null)return false;
            if(float.IsNaN(dt)||float.IsInfinity(dt)||dt<0||dt>.25f)throw new ArgumentOutOfRangeException("dt");
            bool suspended=paused||Health<=0;
            if(!suspended){now+=dt;foreach(var pair in practice)if(pair.Value.ResetLeft>0){pair.Value.ResetLeft=Math.Max(0,pair.Value.ResetLeft-dt);if(pair.Value.ResetLeft==0)ResetTarget(pair.Key);}}
            var input=frame.Mechanics;input.Paused|=suspended;
            pendingShot=Magnum.Step(dt,new R23MagnumInput(input,frame.PrimaryButton,frame.ManualClickAllowed,frame.TriggerBlocksFlick,frame.StickX,frame.StickY,frame.DesktopReload));return pendingShot;
        }
'''+s[pos:]
s=s.replace('sourceLoadout?Rifle.SourceWeaponId:"mk18"','sourceLoadout?selectedRifle:"mk18"')
s=s.replace('weaponId!=Rifle.SourceWeaponId','weaponId!=selectedRifle')
s=s.replace('if(before.Life!=LifeState.Alive||!origin.IsFinite||!point.IsFinite||dx*dx+dy*dy+dz*dz>10000||!query.HasLineOfSight("player",id,origin,point,100))return false;', 'float range=weaponId=="magnum"?R23MagnumRuntime.MaximumRange:100;\n            if(before.Life!=LifeState.Alive||!origin.IsFinite||!point.IsFinite||dx*dx+dy*dy+dz*dz>range*range||!query.HasLineOfSight("player",id,origin,point,range))return false;')
s=s.replace('if(definition==null)return false;float amount=headshot?definition.HeadDamage:definition.BodyDamage;', 'if(definition==null&&weaponId!="magnum")return false;float amount=weaponId=="magnum"?(headshot?R23MagnumRuntime.HeadDamage:R23MagnumRuntime.BodyDamage):(headshot?definition.HeadDamage:definition.BodyDamage);')
s=s.replace('var c=Command();var receipt=health.ApplyDamage(c,c.CommandId,id,before.Revision,healthLoss,shieldLoss,false);', 'var c=Command();DamageReceipt receipt;\n            if(weaponId=="magnum"){if(!Magnum.TryApplyHit(health,c,query,id,origin,point,headshot,out receipt))return false;}else receipt=health.ApplyDamage(c,c.CommandId,id,before.Revision,healthLoss,shieldLoss,false);')
(draft/'R23RoundState.cs').write_text(s)
(tools/'R23RoundState.magnum.patch').write_text(''.join(difflib.unified_diff(before.splitlines(True),s.splitlines(True),fromfile=p.relative_to(root).as_posix(),tofile=p.relative_to(root).as_posix())))
print('Staged exact RoundState patch; live Assets unchanged')
