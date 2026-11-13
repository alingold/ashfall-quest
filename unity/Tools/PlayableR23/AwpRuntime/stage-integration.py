"""Rebase scoped AWP deltas on CURRENT live sources; writes own Candidate only."""
from pathlib import Path
import base64,hashlib,json,re
T=Path(__file__).resolve().parent;R=T.parents[3];P=R/'unity/Assets/Ashfall/Integration/Playable';C=T/'Candidate';C.mkdir(exist_ok=True);pins={}
def read(rel):
 p=P/rel;s=p.read_text(encoding='utf-8');pins[rel]=hashlib.sha256(p.read_bytes()).hexdigest();return s
def rep(s,a,b,n=1):
 assert s.count(a)>=n,(a,s.count(a));return s.replace(a,b,n)
def save(rel,s):(C/Path(rel).name).write_text(s,encoding='utf-8')
s=read('HandsWeapons/R23SourceRifleOwner.cs');s=rep(s,'var result=id=="mk18"','var result=id=="awp"?Awp:id=="mk18"');s=rep(s,'Ump,P90,Mp5,Mp9}','Ump,P90,Mp5,Mp9,Awp}');save('R23SourceRifleOwner.cs',s)
s=read('HandsWeapons/R23RoundState.cs');s=rep(s,'ActiveSnapshot {get{return ','ActiveSnapshot {get{return selectedRifle=="awp"?Awp.Snapshot:');s=rep(s,'ActiveLoaded {get{return ','ActiveLoaded {get{return selectedRifle=="awp"?Awp.Snapshot.Loaded:')
s=rep(s,'foreach(var dt in dt11s.Values)dt.GripToggle=enabled;','foreach(var dt in dt11s.Values)dt.GripToggle=enabled;foreach(var awp in awps.Values)awp.GripToggle=enabled;')
s=rep(s,'if(profile=="magnum")magnums.Add','if(profile=="awp")awps.Add(actionId,new R24AwpOwner(Command(),nativeId,reserve,this));else if(profile=="magnum")magnums.Add')
s=rep(s,'instances.Add(actionId,new R23WeaponInstanceInfo(actionId,nativeId,profile,stars,stats));instanceOrder.Add(actionId);usedActions.Add(actionId);usedNativeIds.Add(nativeId);return true;\n        }','R24AwpOwner configuredAwp;if(awps.TryGetValue(actionId,out configuredAwp))configuredAwp.GripToggle=gripToggle;instances.Add(actionId,new R23WeaponInstanceInfo(actionId,nativeId,profile,stars,stats));instanceOrder.Add(actionId);usedActions.Add(actionId);usedNativeIds.Add(nativeId);return true;\n        }')
s=rep(s,'instances.Remove(actionId);','R24AwpOwner removedAwp;if(awps.TryGetValue(actionId,out removedAwp)){removedAwp.Retire();awps.Remove(actionId);}instances.Remove(actionId);')
s=rep(s,'public void Retire(){retiredRound=true;','public void Retire(){retiredRound=true;foreach(var awp in awps.Values)awp.Retire();')
s=rep(s,'Retire();havePaddlePose=false;','Retire();awps.Clear();LastAwpShot=null;havePaddlePose=false;')
s=rep(s,'Rifle=rifles[selectedRifle=="magnum"','Rifle=rifles[selectedRifle=="awp"||selectedRifle=="magnum"')
s=rep(s,'public HandlingSnapshot ReadWeapon(string id){','public HandlingSnapshot ReadWeapon(string id){R24AwpOwner awp;if(id!=null&&awps.TryGetValue(id,out awp))return awp.Snapshot;')
s=rep(s,'bool found=false;','bool found=false;R24AwpOwner awp;if(awps.TryGetValue(id,out awp)){awp.SuspendControls();found=true;}')
s=rep(s,'if(R24MeleeProfiles.ForId(selectedRifle)!=null){AdvanceSmawWorld','if(selectedRifle=="awp")return StepAwp(dt,input,reload?(input.Desktop?HandlingAction.ReloadStep:HandlingAction.Eject):HandlingAction.Sample,new R23ShotPose(new Float3(),new Float3(1,0,0),new Float3(0,1,0),new Float3(0,0,1)),paused);\n            if(R24MeleeProfiles.ForId(selectedRifle)!=null){AdvanceSmawWorld');save('R23RoundState.cs',s)
s=read('HandsWeapons/R23WeaponInstances.cs');s=rep(s,'case "mk18":stats=','case "awp":stats=new R23RarityStats(3,new float[]{70,78,87,95,104}[i],new float[]{117,132,148,164,180}[i],1,1);return true;\n            case "mk18":stats=');save('R23WeaponInstances.cs',s)
s=read('HandsWeapons/R23WeaponPickupRules.cs');s=rep(s,'switch(id){case "dt11"','switch(id){case "awp":return 5;case "dt11"');save('R23WeaponPickupRules.cs',s)
s=read('HandsWeapons/R23WeaponPoseMath.cs');s=rep(s,'id=="katana"||id=="excalibur"','id=="awp"||id=="katana"||id=="excalibur"');save('R23WeaponPoseMath.cs',s)
s=read('HandsWeapons/R23WeaponFeedbackState.cs');s=rep(s,'readonly SourceCatalog5Feedback catalog;','readonly R24AwpFeedback awp;readonly SourceCatalog5Feedback catalog;');s=rep(s,'if(extra)catalog=','if(weaponId=="awp")awp=new R24AwpFeedback();if(extra)catalog=');s=rep(s,'else if(weaponId!="mk18"','else if(weaponId!="awp"&&weaponId!="mk18"')
for name in ['Distance','PitchRadians','YawRadians','Flash','AcceptedShots']:s=rep(s,'return catalog!=null?catalog.'+name,'return awp!=null?awp.'+name+':catalog!=null?catalog.'+name)
s=rep(s,'if(catalog!=null){catalog.Advance(seconds);return;}','if(awp!=null){awp.Advance(seconds);return;}if(catalog!=null){catalog.Advance(seconds);return;}');s=rep(s,'if(catalog!=null)return catalog.AcceptedShot','if(awp!=null)return awp.AcceptedShot(nativeShots,supported);if(catalog!=null)return catalog.AcceptedShot');s=rep(s,'if(catalog!=null){catalog.ClearPresentation();return;}','if(awp!=null){awp.ClearPresentation();return;}if(catalog!=null){catalog.ClearPresentation();return;}');s=rep(s,'if(catalog!=null){catalog.Reset();return;}','if(awp!=null){awp.Reset();return;}if(catalog!=null){catalog.Reset();return;}');save('R23WeaponFeedbackState.cs',s)
s=read('HandsWeapons/R23Audio.cs');s=rep(s,'source.Stop();source.clip=choices','float gain=shot?(weapon=="awp"?.8912509f:.3548f):(weapon=="awp"?.31622777f:.1995f);sourceGains[source]=gain;source.volume=gain*sfxVolume;if(shot)source.maxDistance=weapon=="awp"?120:85;source.Stop();source.clip=choices');save('R23Audio.cs',s)
s=read('HandsWeapons/R23HandsWeapons.cs');assert '"awp"' not in re.search(r'weaponIds=\{([^\r\n]+)\};',s)[1]
def append(name,extra):
 global s
 pattern=r'(\b'+name+r'=\{)([^\r\n]+)(\};)';m=re.search(pattern,s);assert m,name;s=s[:m.start()]+m[1]+m[2]+','+extra+m[3]+s[m.end():]
for name,extra in [('magazineParts','new List<Renderer>()'),('boltParts','new List<Transform>()'),('magazineHomes','new Vector3(.0002487176f,-.0128027467f,.1061082762f)'),('weaponIds','"awp"'),('modelScales','1f'),('sourceMuzzles','new Vector3(0,.047f,1.01f)'),('feedbackStates','new R23WeaponFeedbackState("awp")')]:append(name,extra)
s=s.replace('new Transform[22]','new Transform[23]');s=rep(s,'{"dt11",new Vector3(1.2f,0,.75f)}','{"dt11",new Vector3(1.2f,0,.75f)},{"awp",new Vector3(1.55f,0,.75f)}')
s=rep(s,'return index==21&&dt11View','return index==AwpIndex&&awpView!=null&&awpView.IsTransient(item)||index==21&&dt11View')
s=rep(s,'return HasWeapon&&round!=null&&(IsDt11?round.Dt11.ReloadGripInProgress:','return HasWeapon&&round!=null&&(IsAwp?round.Awp.ReloadGripInProgress:IsDt11?round.Dt11.ReloadGripInProgress:')
s=rep(s,'if(IsDt11)return ReloadStage','if(IsAwp)return ReloadStage==ReloadPhase.NeedsBolt?"RELEASE / GRIP BOLT / PULL BACK":ReloadStage==ReloadPhase.NeedsMagazine?"GRIP OFFERED MAGAZINE / PUSH UP":ReloadStage==ReloadPhase.HeldMagazine?"PUSH MAGAZINE UP":"RIGHT STICK CLICK: RELOAD";if(IsDt11)return ReloadStage')
s=rep(s,'yield return LoadDt11Presentation();visuals.ReleaseLoadedBytes();','yield return LoadDt11Presentation();yield return LoadAwpPresentation();visuals.ReleaseLoadedBytes();')
s=rep(s,'if(WeaponDefinition(weaponIds[i])!=null)yield return visuals.Load','if(WeaponDefinition(weaponIds[i])!=null&&weaponIds[i]!="awp")yield return visuals.Load')
s=s.replace('if(WeaponDefinition(weaponIds[i])==null)continue;','if(WeaponDefinition(weaponIds[i])==null||weaponIds[i]=="awp")continue;')
s=rep(s,'PresentDt11(0);if(weapon!=null)','PresentDt11(0);PresentAwp();if(weapon!=null)')
s=rep(s,'if(profile=="dt11"){dt11View.SynchronizeInstance','if(profile=="awp")awpView.Present(state,null,false);if(profile=="dt11"){dt11View.SynchronizeInstance')
s=rep(s,'if(profile=="dt11")dt11View.SynchronizeInstance(round.Dt11);','if(profile=="awp")PresentAwp();if(profile=="dt11")dt11View.SynchronizeInstance(round.Dt11);')
s=rep(s,'AdvanceSmawProjectiles(dt);if(IsDt11)','AdvanceSmawProjectiles(dt);if(IsAwp)return TickAwp(dt,fire,reload);if(IsDt11)')
s=s.replace('dt11Controls.Reset();','dt11Controls.Reset();awpControls.Reset();')
s=rep(s,'if(!FeedbackAllowed){ClearFeedbackPresentation();return;}','if(!FeedbackAllowed){if(awpView!=null)awpView.ClearTransient();ClearFeedbackPresentation();return;}')
s=rep(s,'ResolveDisplayedAim();PresentDisplayedAim();','ResolveDisplayedAim();PresentAwp();PresentDisplayedAim();')
s=rep(s,'Ready=false;','Ready=false;if(awpView!=null)awpView.ClearTransient();')
s=rep(s,'void ClearFeedbackPresentation(){','void ClearFeedbackPresentation(){if(awpView!=null)awpView.ClearTransient();')
save('R23HandsWeapons.cs',s)
s=read('HandsWeapons/R23HandsWeapons.DisplayedPose.cs');s=rep(s,'if(IsMelee){RenderKnifePose();return;}','if(IsAwp){RenderAwpPose();return;}if(IsMelee){RenderKnifePose();return;}');s=rep(s,'ActiveWeaponId!="spr"&&','ActiveWeaponId!="awp"&&ActiveWeaponId!="spr"&&');save('R23HandsWeapons.DisplayedPose.cs',s)
s=read('Gallery/R23LobbyWeaponGallery.cs');s=rep(s,'"cyberblade","dt11"}','"cyberblade","dt11","awp"}');s=rep(s,'public bool IsWeaponReady(string id){','public bool IsWeaponReady(string id){if(id=="awp")return AwpReady;');s=rep(s,'public string WeaponFailure(string id){','public string WeaponFailure(string id){if(id=="awp")return awpFailure??Failure;');s=rep(s,'return dt11Selected?dt11Session.Active','return awpSelected?awpSession.Active:dt11Selected?dt11Session.Active');s=rep(s,'return dt11Selected?"dt11"','return awpSelected?"awp":dt11Selected?"dt11"');s=rep(s,'int count=Dt11Visible?1:0;foreach(var model','int count=(AwpVisible?1:0)+(Dt11Visible?1:0);foreach(var model');s=rep(s,'if(Dt11Visible)return "dt11";','if(AwpVisible)return "awp";if(Dt11Visible)return "dt11";');s=rep(s,'get {if(dt11Selected)return Dt11State;','get {if(awpSelected)return AwpState;if(dt11Selected)return Dt11State;');s=rep(s,'yield return LoadDt11GallerySafely();','yield return LoadDt11GallerySafely();yield return LoadAwpGallerySafely();')
s=rep(s,'dt11Selected=id=="dt11";','awpSelected=id=="awp";awpSession.Hide();if(awpSelected)awpSession.Show();if(awpRoot!=null)awpRoot.gameObject.SetActive(false);dt11Selected=id=="dt11";');s=rep(s,'if(dt11Selected){}else if(knifeSelected)','if(awpSelected){}else if(dt11Selected){}else if(knifeSelected)');s=rep(s,'Release();dt11Session.Hide();','Release();awpSession.Hide();if(awpRoot!=null)awpRoot.gameObject.SetActive(false);dt11Session.Hide();');s=rep(s,'void DisplayDock(){','void DisplayDock(){if(awpSelected){DockAwp();return;}');s=rep(s,'int hand=holder;ReleaseDt11();','int hand=holder;ReleaseAwp();ReleaseDt11();');s=rep(s,'if(dt11Audio!=null)dt11Audio.SetSfxVolume(volume);','if(dt11Audio!=null)dt11Audio.SetSfxVolume(volume);if(awpAudio!=null)awpAudio.SetSfxVolume(volume);');s=rep(s,'if(dt11Selected)return SampleDt11','if(awpSelected)return SampleAwp(seconds,input,focused,primaryHand);if(dt11Selected)return SampleDt11');s=rep(s,'StopAllCoroutines();DisposeDt11();','StopAllCoroutines();DisposeAwp();DisposeDt11();');s=rep(s,'if(collider.gameObject.layer==20)return true;','if(collider.gameObject.layer==20||collider.gameObject.layer==21||collider.gameObject.layer==22)return true;');save('R23LobbyWeaponGallery.cs',s)
# Append just the genuine source icon; do not regenerate shared icon payloads.
s=read('Input/R23WeaponWheelIcons.cs');needle='switch(id) {';assert needle in s;s=rep(s,needle,needle+'case "awp":return Convert.FromBase64String("'+base64.b64encode((T/'Payload/awp-icon.png').read_bytes()).decode()+'");');save('R23WeaponWheelIcons.cs',s)
(T/'candidate-inputs.json').write_text(json.dumps(pins,indent=2)+'\n');print('Scoped AWP candidates staged against current live DT11/22 baseline; Assets unchanged.')

# Shared audio manifest is staged as a collision-checked additive merge, not edited.
audioPath=R/'unity/Assets/StreamingAssets/PlayableR23/HandsWeapons/audio.json'
audio=json.loads(audioPath.read_text());fragment=json.loads((T/'awp-audio-cues.json').read_text());keys={(c['weapon'],c['action'])for c in audio['cues']}
for cue in fragment['cues']:
 assert (cue['weapon'],cue['action'])not in keys
 audio['cues'].append(cue)
(C/'audio.json').write_text(json.dumps(audio,indent=2)+'\n');pins['STREAMING/audio.json']=hashlib.sha256(audioPath.read_bytes()).hexdigest();(T/'candidate-inputs.json').write_text(json.dumps(pins,indent=2)+'\n')
