from source import *
import shutil,re
BASE=ROOT/'unity/Assets/Ashfall/Integration/Playable';C=HERE/'Candidate';C.mkdir(exist_ok=True);BASELINES={}
def out(relative,data):
 p=C/relative;p.parent.mkdir(parents=True,exist_ok=True);p.write_text(data);return p
def edit(relative):
 p=ROOT/relative;BASELINES[relative]=sha(p);return p.read_text(encoding='utf-8-sig')
def replace(s,a,b,count=1):
 assert s.count(a)==count,(a,s.count(a),count);return s.replace(a,b)
H='unity/Assets/Ashfall/Integration/Playable/HandsWeapons/'
s=edit(H+'R23WeaponInstances.cs');s=replace(s,'case "m60":stats=', 'case "uzis":stats=new R23RarityStats(2*(20+4*i),14,14,1,1);return true;\n            case "matadors":stats=new R23RarityStats(16,20+4*i,20+4*i,1,1);return true;\n            case "m60":stats=');out(H+'R23WeaponInstances.cs',s)
s=edit(H+'R23WeaponPickupRules.cs');s=replace(s,'switch(id){case "m60":','switch(id){case "uzis":return 11;case "matadors":return 12;case "m60":');out(H+'R23WeaponPickupRules.cs',s)
s=edit(H+'R23RoundState.cs')
s=replace(s,'get{return selectedRifle=="m60"?M60.Snapshot:','get{return IsDualProfile(selectedRifle)?DualSnapshot(selectedAction):selectedRifle=="m60"?M60.Snapshot:')
s=replace(s,'get{return selectedRifle=="m60"?M60.Loaded:','get{return IsDualProfile(selectedRifle)?Dual.Loaded:selectedRifle=="m60"?M60.Loaded:')
s=replace(s,'var definition=SourceRifleDefinition.ForId(profile);if(definition==null', 'if(IsDualProfile(profile))return RegisterDual(actionId,nativeId,profile,stars,stats);var definition=SourceRifleDefinition.ForId(profile);if(definition==null')
s=replace(s,'if(actionId==null||actionId==selectedAction||!instances.ContainsKey(actionId))return false;','if(actionId==null||actionId==selectedAction||!instances.ContainsKey(actionId))return false;RetireDual(actionId);')
s=replace(s,'public void Retire(){retiredRound=true;','public void Retire(){RetireDuals();retiredRound=true;')
s=replace(s,'Retire();m60s.Clear();','Retire();ClearDuals();m60s.Clear();')
s=replace(s,'RegisterWeaponInstance("m1014","m1014",1,"r23-loadout/m1014");','RegisterWeaponInstance("uzis","uzis",1,"r24-loadout/uzis");RegisterWeaponInstance("matadors","matadors",1,"r24-loadout/matadors");RegisterWeaponInstance("m1014","m1014",1,"r23-loadout/m1014");')
s=replace(s,'Rifle=rifles[selectedRifle=="m60"','Rifle=rifles[IsDualProfile(selectedRifle)||selectedRifle=="m60"')
s=replace(s,'public HandlingSnapshot ReadWeapon(string id){','public HandlingSnapshot ReadWeapon(string id){if(ReadDualInstance(id)!=null)return DualSnapshot(id);')
s=replace(s,'if(knives.ContainsKey(id))return 0;','if(IsDualProfile(info.ProfileId))return reserves[info.ProfileId=="uzis"?"smg":"shells"].Available;if(knives.ContainsKey(id))return 0;')
s=replace(s,'if(id==null)return false;bool found=false;','if(id==null)return false;bool found=false;if(ReadDualInstance(id)!=null){SuspendDual(id);found=true;}')
s=replace(s,'public bool Step(float dt,HandlingInput input,bool reload,bool paused) {','public bool Step(float dt,HandlingInput input,bool reload,bool paused) {\n            if(IsDualProfile(selectedRifle)){SuspendDual(selectedAction);return false;} // Requires both actual hand frames through StepDual.')
out(H+'R23RoundState.cs',s)
# The generic feedback holder is not used to drive either dual gun. Allow its
# existing lifecycle reset/clear calls; per-hand source feedback remains separate.
s=edit(H+'R23WeaponFeedbackState.cs');s=replace(s,'else if(weaponId!="m60"','else if(weaponId!="uzis"&&weaponId!="matadors"&&weaponId!="m60"');out(H+'R23WeaponFeedbackState.cs',s)
s=edit(H+'R23HandsWeapons.cs')
# Fixed positional tables get two authentic profile entries; no existing index moves.
s=replace(s,'new Transform[24]','new Transform[26]',5)
for field,addition in [('magazineParts',',new List<Renderer>(),new List<Renderer>()'),('boltParts',',new List<Transform>(),new List<Transform>()'),('magazineHomes',',Vector3.zero,Vector3.zero'),('weaponIds',',"uzis","matadors"'),('modelScales',',.65436f,.9f'),('sourceMuzzles',',new Vector3(0,.045f,.46f),new Vector3(0,.045f,.37f)'),('feedbackStates',',new R23WeaponFeedbackState("uzis"),new R23WeaponFeedbackState("matadors")')]:
 pattern=r'(readonly [^\n]+\b'+field+r'=\{)([^\n]+)(\};)';matches=list(re.finditer(pattern,s));assert len(matches)==1,field;m=matches[0];s=s[:m.start()]+m[1]+m[2]+addition+m[3]+s[m.end():]
s=replace(s,'get { return HasWeapon&&primaryHand==0; }','get { return HasWeapon&&(IsDual||primaryHand==0); }')
s=replace(s,'get { return HasWeapon&&primaryHand==1; }','get { return HasWeapon&&(IsDual||primaryHand==1); }')
s=replace(s,'public static string WeaponDisplayName(string id) {','public static string WeaponDisplayName(string id) { if(R23RoundState.IsDualProfile(id))return new R24DualDefinition(id).Label;')
s=replace(s,'public static int WeaponCapacity(string id) {','public static int WeaponCapacity(string id) { if(R23RoundState.IsDualProfile(id))return new R24DualDefinition(id).Capacity*2;')
s=replace(s,'public static string WeaponAmmoFamily(string id){','public static string WeaponAmmoFamily(string id){if(R23RoundState.IsDualProfile(id))return new R24DualDefinition(id).AmmoFamily;')
s=replace(s,'get{return IsDt11?40:IsMelee?', 'get{return IsDual?180:IsDt11?40:IsMelee?')
s=replace(s,'if(bladeAudio!=null)bladeAudio.SetVolume(sfxVolume);','if(bladeAudio!=null)bladeAudio.SetVolume(sfxVolume);foreach(var dual in dualViews)if(dual!=null&&dual.View!=null)dual.View.SetSfxVolume(sfxVolume);')
s=replace(s,'get { return HasWeapon&&round!=null&&(IsShotgun?', 'get { return HasWeapon&&round!=null&&(IsDual||IsShotgun?')
# Parenthesize the paired early answer independently of shotgun precedence.
s=replace(s,'(IsDual||IsShotgun?round.M1014.ReloadGripInProgress||round.ActiveSnapshot.Supported:round.ActiveSnapshot.ClaimsSupport)', '(IsDual||(IsShotgun?round.M1014.ReloadGripInProgress||round.ActiveSnapshot.Supported:round.ActiveSnapshot.ClaimsSupport))')
s=replace(s,'public string ReloadInstruction { get {','public string ReloadInstruction { get { if(IsDual)return "FLICK EACH OCCUPIED HAND UP TO RELOAD";')
s=replace(s,'get { return IsDt11&&round!=null?', 'get { return IsDual?WeaponGripPosition:IsDt11&&round!=null?',3)
s=replace(s,'if(HasWeapon&&!presentationSuppressed&&primaryHand==hand&&selectedWeapon<weaponModels.Count){','Vector3 dualAt;Quaternion dualRotation;if(IsDual&&DualGlove(hand,out dualAt,out dualRotation)){visual.position=dualAt;visual.rotation=dualRotation;}else if(HasWeapon&&!presentationSuppressed&&primaryHand==hand&&selectedWeapon<weaponModels.Count&&!IsDual){')
s=replace(s,'yield return LoadM60Presentation();visuals.ReleaseLoadedBytes();','yield return LoadM60Presentation();yield return LoadDualPresentation();visuals.ReleaseLoadedBytes();')
s=replace(s,'PresentDt11(0);PresentAwp();PresentM60();','PresentDt11(0);PresentAwp();PresentM60();SyncDualPresentation();')
s=replace(s,'if(IsMelee){SyncKnifePresentation();return;}if(IsSmaw){SyncSmawPresentation();return;}','if(IsDual){if(gunFeedback!=null)gunFeedback.Hide();RenderDualPose();return;}if(IsMelee){SyncKnifePresentation();return;}if(IsSmaw){SyncSmawPresentation();return;}')
s=replace(s,'if(CanClaimWorldGrip!=null&&!CanClaimWorldGrip(id,hand))return false;','if(CanClaimWorldGrip!=null&&!CanClaimWorldGrip(id,hand))return false;if(R23RoundState.IsDualProfile(ResolveWeaponProfile(id))&&!DualCustodyAvailable(hand))return false;')
s=replace(s,'if(!Ready||index<0||!ownedWeapons.Contains(id)||gameplayPaused||!hadFocus||!HandAvailable(hand)||Health<=0)return false;','if(!Ready||index<0||!ownedWeapons.Contains(id)||gameplayPaused||!hadFocus||!HandAvailable(hand)||Health<=0||R23RoundState.IsDualProfile(ResolveWeaponProfile(id))&&(!HandAvailable(1-hand)||!DualCustodyAvailable(hand)))return false;')
s=replace(s,'ApplyWeaponSelection();UpdateWeaponPresentation();\n        }\n        public bool EquipOwned','ApplyWeaponSelection();if(IsDual)round.EquipDual(primaryHand,true,true);UpdateWeaponPresentation();\n        }\n        public bool EquipOwned')
s=replace(s,'i==selectedWeapon&&weaponIds[i]!="smaw"','i==selectedWeapon&&weaponIds[i]!="smaw"&&!R23RoundState.IsDualProfile(weaponIds[i])')
s=replace(s,'if(IsDt11||IsMagnum||IsShotgun||WeaponDefinition(ActiveWeaponId)!=null','if(IsDual||IsDt11||IsMagnum||IsShotgun||WeaponDefinition(ActiveWeaponId)!=null')
s=replace(s,'AdvanceSmawProjectiles(dt);if(IsM60)','AdvanceSmawProjectiles(dt);if(IsDual)return TickDual(dt,fire,reload);if(IsM60)')
s=replace(s,'hadFocus&&!inputTrackingUnavailable&&!weaponHandReserved&&isActiveAndEnabled&&Health>0','hadFocus&&(IsDual||!inputTrackingUnavailable&&!weaponHandReserved)&&isActiveAndEnabled&&Health>0')
s=replace(s,'void ClearFeedbackPresentation(){','void ClearFeedbackPresentation(){SuspendDualPresentation();')
s=replace(s,'if(IsMelee){SyncKnifePresentation();ApplyFreeHandContactOffsets();return;}','if(IsDual){SyncDualPresentation();RenderDualPose();ApplyFreeHandContactOffsets();return;}if(IsMelee){SyncKnifePresentation();ApplyFreeHandContactOffsets();return;}')
s=replace(s,'(IsMelee||IsSmaw?Vector3.zero:', '(IsDual||IsMelee||IsSmaw?Vector3.zero:')
s=replace(s,'void Update() {\n            if(impactMarks', 'void Update() {\n            UpdateDualTraces();if(impactMarks')
s=replace(s,'Ready=false;if(m60View!=null)','Ready=false;SuspendDualPresentation();if(m60View!=null)')
# Public diagnostics reflect dominant independent source feedback, not the unused generic holder.
for name,old,new in [('VisualRecoilDistance','ActiveFeedback.Distance*ActiveRecoilScale','round.ReadDualFeedback(activeActionId,0).Distance'),('VisualRecoilPitchRadians','ActiveFeedback.PitchRadians*ActiveRecoilScale','round.ReadDualFeedback(activeActionId,0).Pitch'),('FeedbackAcceptedShots','ActiveFeedback.AcceptedShots','round.Dual.TotalShots')]:
 s=replace(s,'{get{return '+old+';}}','{get{return IsDual&&round!=null&&round.Dual!=null?'+new+':'+old+';}}')
out(H+'R23HandsWeapons.cs',s)
s=edit(H+'R23HandsWeapons.DisplayedPose.cs')
s=replace(s,'if(IsAwp){RenderAwpPose();return;}','if(IsDual){RenderDualPose();return;}if(IsAwp){RenderAwpPose();return;}')
s=replace(s,'if(IsMelee||IsSmaw)return;','if(IsDual||IsMelee||IsSmaw)return;')
s=replace(s,'RenderDisplayedPose();ResolveDisplayedAim();PresentDisplayedAim();ApplyFreeHandContactOffsets();','if(IsDual&&DualReady){ActiveDualView.RefreshTrackedPresentation();RenderDualPose();ApplyFreeHandContactOffsets();return;}RenderDisplayedPose();ResolveDisplayedAim();PresentDisplayedAim();ApplyFreeHandContactOffsets();')
out(H+'R23HandsWeapons.DisplayedPose.cs',s)

G='unity/Assets/Ashfall/Integration/Playable/Gallery/'
s=edit(G+'R23LobbyWeaponGallery.cs')
s=replace(s,'"dt11","awp","m60"};','"dt11","awp","m60","uzis","matadors"};')
s=replace(s,'public bool IsWeaponReady(string id){','public bool IsWeaponReady(string id){if(R23RoundState.IsDualProfile(id))return DualReady(id);')
s=replace(s,'public string WeaponFailure(string id){','public string WeaponFailure(string id){if(R23RoundState.IsDualProfile(id))return DualFailure(id)??Failure;')
s=replace(s,'get { return m60Selected?m60Session.Active:','get { return DualSelected?dualSession.Active:m60Selected?m60Session.Active:')
s=replace(s,'get { return m60Selected?"m60":','get { return DualSelected?dualProfile:m60Selected?"m60":')
s=replace(s,'int count=(M60Visible?1:0)','int count=(DualVisible?1:0)+(M60Visible?1:0)')
s=replace(s,'if(VisiblePreviewCount!=1)return null;','if(VisiblePreviewCount!=1)return null;if(DualVisible)return dualProfile;')
s=replace(s,'public R23GalleryViewState ViewState { get {','public R23GalleryViewState ViewState { get {if(DualSelected)return DualState;')
s=replace(s,'yield return LoadM60GallerySafely();}','yield return LoadM60GallerySafely();yield return LoadDualGallerySafely();}')
s=replace(s,'if(id!="dt11"&&R24MeleeProfiles','if(!R23RoundState.IsDualProfile(id)&&id!="dt11"&&R24MeleeProfiles')
s=replace(s,'awpSelected=id=="awp";','SelectDual(id);awpSelected=id=="awp";')
s=replace(s,'if(m60Selected){}else if(awpSelected){}','if(DualSelected){}else if(m60Selected){}else if(awpSelected){}')
s=replace(s,'public void Hide(){bool wasActive=Active;Release();','public void Hide(){bool wasActive=Active;Release();HideDual();')
s=replace(s,'void DisplayDock(){if(m60Selected)','void DisplayDock(){if(DualSelected){DockDual();return;}if(m60Selected)')
s=replace(s,'void Release(){int hand=holder;','void Release(){int hand=holder;ReleaseDual();')
s=replace(s,'volume=Mathf.Clamp01(value);foreach(var model','volume=Mathf.Clamp01(value);foreach(var dual in dualGallery)if(dual!=null&&dual.View!=null)dual.View.SetSfxVolume(volume);foreach(var model')
s=replace(s,'if(awpSelected)return SampleAwp(seconds','if(DualSelected)return SampleDual(seconds,input,focused,primaryHand);if(awpSelected)return SampleAwp(seconds')
s=replace(s,'void OnDestroy(){StopAllCoroutines();','void OnDestroy(){StopAllCoroutines();DisposeDual();')
out(G+'R23LobbyWeaponGallery.cs',s)
I='unity/Assets/Ashfall/Integration/Playable/Input/'
s=edit(I+'R23WeaponWheelState.cs');s=replace(s,'{switch(profile){case "mk18":','{switch(profile){case "uzis":return "DUAL UZIS";case "matadors":return "DUAL MATADORS";case "mk18":');out(I+'R23WeaponWheelState.cs',s)
import base64
s=edit(I+'R23WeaponWheelIcons.cs');cases=''.join('case "'+id+'":return Convert.FromBase64String("'+base64.b64encode((HERE/'Payload'/('dual-'+id+'-icon.png')).read_bytes()).decode()+'");'for id in ['uzis','matadors']);s=replace(s,'switch(id) {','switch(id) {'+cases);out(I+'R23WeaponWheelIcons.cs',s)
A='unity/Assets/StreamingAssets/PlayableR23/HandsWeapons/audio.json';baseline=json.loads(edit(A));fragment=json.loads((HERE/'Payload/dual-audio-cues.json').read_text());assert not any(row['weapon'] in ['uzis','matadors']for row in baseline['cues']);baseline['cues']+=fragment['cues'];out(A,json.dumps(baseline,indent=2)+'\n')

# More shared adaptations are added below. Unique code and source payload stay
# inside this exclusive candidate; Root owns any final live publication.
for p in sorted((HERE/'Staged').glob('*.cs')):out((G if p.name=='R24DualGallery.cs' else H)+p.name,p.read_text())
for p in sorted((HERE/'Payload').iterdir()):
 if p.is_file():
  target=C/'unity/Assets/StreamingAssets/PlayableR23/HandsWeapons'/p.name;target.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(p,target)
for p in sorted((C/'unity/Assets').rglob('*')):
 if not p.is_file() or p.suffix=='.meta':continue
 relative=p.relative_to(C).as_posix()
 if relative in BASELINES:continue
 assert not (ROOT/relative).exists(),'New dual name already present: '+relative
 guid=hashlib.md5(('ashfall-r6-dual-runtime/'+relative).encode()).hexdigest();p.with_name(p.name+'.meta').write_text('fileFormatVersion: 2\nguid: '+guid+'\n')
(C/'BASELINES.json').write_text(json.dumps(BASELINES,indent=2)+'\n')
print('Staged',len(BASELINES),'guarded shared files and unique dual code/payload')

