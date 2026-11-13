#if UNITY_6000_0_OR_NEWER
using System;
using System.Collections;
using System.Collections.Generic;
using Ashfall.Core;
using Ashfall.Modules.WeaponHandling;
using Ashfall.Integration.Playable.HandsWeapons;
using UnityEngine;
namespace Ashfall.Integration.Playable.Gallery
{
    public struct R23GalleryViewState
    {
        public readonly string WeaponId,Label,Status,Error;
        public readonly bool Active,Ready,Loading,Live,ScopeActive;
        public readonly int HoldingHand,Ammo,Capacity,Reserve,PreviewShots;
        public readonly HandlingSnapshot? NativeState;
        public R23GalleryViewState(string id,string label,string status,string error,bool active,bool ready,bool loading,bool live,bool scope,int hand,int ammo,int capacity,int reserve,int shots,HandlingSnapshot? state)
        {WeaponId=id;Label=label;Status=status;Error=error;Active=active;Ready=ready;Loading=loading;Live=live;ScopeActive=scope;HoldingHand=hand;Ammo=ammo;Capacity=capacity;Reserve=reserve;PreviewShots=shots;NativeState=state;}
    }
    // Source lobby_gallery_091. Caller supplies input/focus even while its gameplay is menu-paused.
    // This component never calls carried gameplay, inventory, health or world-damage mutation APIs.
    public sealed partial class R23LobbyWeaponGallery:MonoBehaviour
    {
        readonly string[] ids={"mk18","akm","spr","magnum","cx4","px4","m1014","ump","p90","mp5","mp9","1911","tec9","rfb","fal","s85","smaw","knife","katana","excalibur","cyberblade"};
        readonly Dictionary<string,string> extraFailures=new Dictionary<string,string>();
        readonly Dictionary<string,R23GalleryModel> models=new Dictionary<string,R23GalleryModel>();
        readonly List<R23GalleryModel> pendingModels=new List<R23GalleryModel>();
        readonly R23GallerySession session=new R23GallerySession();
        readonly R23MagnumGallerySession magnumSession=new R23MagnumGallerySession();
        readonly R23M1014GallerySession m1014Session=new R23M1014GallerySession();
        R23M1014GalleryModel m1014Model;bool m1014Selected;string m1014Failure;
        readonly List<LineRenderer> pelletTraces=new List<LineRenderer>();
        R23MagnumGalleryModel magnumModel;bool magnumSelected;string magnumFailure;
        readonly bool[] previousGrip={true,true};
        Camera head;Transform left,right,actorRoot,content;
        R23Visuals visuals;LineRenderer trace;Material traceMaterial;
        Vector3 dockPosition;Quaternion dockRotation;int holder=-1;bool initialized,previousReload;float traceUntil,volume=1;string lastStatus="";
        public bool Ready { get; private set; }
        public bool Loading {get{return initialized&&!IsWeaponReady(ViewedWeaponId)&&string.IsNullOrEmpty(WeaponFailure(ViewedWeaponId));}}
        public bool IsWeaponReady(string id){if(R24MeleeProfiles.ForId(id)!=null)return knifeReady;if(id=="smaw")return SmawReady;if(id=="magnum")return MagnumReady;if(id=="m1014")return M1014Ready;if(id==null)return Ready;return Ready&&models.ContainsKey(id);}
        public string WeaponFailure(string id){if(R24MeleeProfiles.ForId(id)!=null)return knifeFailure??Failure;if(id=="smaw")return smawFailure??Failure;if(id=="magnum")return magnumFailure??Failure;if(id=="m1014")return m1014Failure??Failure;string issue;return id!=null&&extraFailures.TryGetValue(id,out issue)?issue:Failure;}
        public string Failure { get; private set; }
        public bool Active { get { return knifeSelected?knifeActive:smawSelected?smawActive:m1014Selected?m1014Session.Active:magnumSelected?magnumSession.Active:session.Active; } }
        public int HoldingHand { get { return holder; } }
        public string ViewedWeaponId { get { return knifeSelected?knifeProfile:smawSelected?"smaw":m1014Selected?R23M1014Definition.Id:magnumSelected?R23MagnumRuntime.Id:session.Definition==null?null:session.Definition.Id; } }
        public bool MagnumReady {get{return magnumModel!=null&&magnumModel.Ready;}}
        public bool MagnumPreviewVisible {get{return magnumModel!=null&&magnumModel.Visible;}}
        public string MagnumFailure {get{return magnumFailure;}}
        public bool M1014Ready {get{return m1014Model!=null&&m1014Model.Ready;}}
        public bool M1014PreviewVisible {get{return m1014Model!=null&&m1014Model.Visible;}}
        public int VisiblePreviewCount {get{int count=0;foreach(var model in models.Values)if(model.Root.gameObject.activeInHierarchy)count++;if(MagnumPreviewVisible)count++;if(M1014PreviewVisible)count++;if(SmawVisible)count++;if(KnifeVisible)count++;return count;}}
        public string VisiblePreviewId {get{if(VisiblePreviewCount!=1)return null;if(KnifeVisible)return knifeProfile;if(SmawVisible)return "smaw";if(MagnumPreviewVisible)return "magnum";if(M1014PreviewVisible)return "m1014";foreach(var pair in models)if(pair.Value.Root.gameObject.activeInHierarchy)return pair.Key;return null;}}
        public string[] AvailableWeaponIds { get { return (string[])ids.Clone(); } }
        public event Action<string> ViewedWeaponChanged;
        public event Action<bool> ActiveChanged;
        public event Action<int,bool> HoldingChanged;
        public event Action<R23GalleryViewState> ViewStateChanged;
        public R23GalleryViewState ViewState { get {if(knifeSelected)return KnifeState;if(smawSelected)return SmawState;
            if(m1014Selected)
            {
                var owner=m1014Session.Owner;string issue=m1014Failure??Failure,message=issue;
                if(string.IsNullOrEmpty(message))message=Loading?"LOADING SOURCE M1014":!Active?"GALLERY CLOSED":owner==null?"GRIP TO INSPECT / RELEASE TO RETURN":owner.Snapshot.Loaded+" / 6 | RESERVE "+owner.Reserve+" | "+(owner.Snapshot.Phase==ReloadPhase.Ready?"TRIGGER FIRES / RIGHT STICK CLICK RELOADS":owner.Snapshot.Phase==ReloadPhase.NeedsMagazine?"GRIP SHELL PACK / PUSH UP":"RELEASE / GRIP HANDLE / PULL BACK");
                return new R23GalleryViewState("m1014","M1014",message,issue,Active,Ready&&M1014Ready,Loading,m1014Session.Live,false,holder,owner==null?0:owner.Snapshot.Loaded,6,owner==null?0:owner.Reserve,m1014Session.PreviewShots,owner==null?(HandlingSnapshot?)null:owner.Snapshot);
            }
            if(magnumSelected)
            {
                var owner=magnumSession.Owner;string issue=magnumFailure??Failure,message=issue;
                if(string.IsNullOrEmpty(message))message=Loading?"LOADING SOURCE MAGNUM":!Active?"GALLERY CLOSED":owner==null?"GRIP TO INSPECT / RELEASE TO RETURN":owner.Loaded+" / 7 | RESERVE "+owner.Reserve+" | "+(owner.Snapshot.Phase==ReloadPhase.Ready?"TRIGGER FIRES / STICK CLICK OPENS CYLINDER":owner.ReloadInstruction);
                return new R23GalleryViewState("magnum","MAGNUM",message,issue,Active,Ready&&MagnumReady,Loading,magnumSession.Live,false,holder,owner==null?0:owner.Loaded,7,owner==null?0:owner.Reserve,magnumSession.PreviewShots,owner==null?(HandlingSnapshot?)null:owner.Snapshot);
            }
            var definition=session.Definition;string profileIssue=WeaponFailure(ViewedWeaponId),status=profileIssue;
            if(string.IsNullOrEmpty(status))status=Loading?"LOADING SOURCE GALLERY":!Active?"GALLERY CLOSED":!session.Live?"GRIP TO INSPECT / RELEASE TO RETURN":session.Loaded+" / "+definition.Capacity+"  |  "+(session.Reserve<0?"PREVIEW AMMO UNLIMITED":"RESERVE "+session.Reserve)+"  |  "+ReloadHint();
            R23GalleryModel model;bool scoped=definition!=null&&models.TryGetValue(definition.Id,out model)&&model.ScopeActive;
            return new R23GalleryViewState(ViewedWeaponId,definition==null?"":definition.DisplayName,status,profileIssue,Active,IsWeaponReady(ViewedWeaponId),Loading,session.Live,scoped,holder,session.Loaded,definition==null?0:definition.Capacity,session.Reserve,session.PreviewShots,session.State);
        } }
        string ReloadHint(){if(!session.State.HasValue)return "";switch(session.State.Value.Phase){case ReloadPhase.NeedsMagazine:return "GRIP OFFERED MAGAZINE";case ReloadPhase.HeldMagazine:return session.Definition!=null&&session.Definition.TopFeed?"PUSH MAGAZINE FORWARD":"PUSH MAGAZINE UP";case ReloadPhase.NeedsRack:return "RELEASE / GRIP HANDLE / PULL BACK";default:return "TRIGGER FIRES / RIGHT STICK CLICK RELOADS";}}
        public void Initialize(Camera view,Transform leftHand,Transform rightHand,Transform playerRoot=null) {
            if(initialized)throw new InvalidOperationException("Gallery already initialized");if(view==null||leftHand==null||rightHand==null)throw new ArgumentException("Head and two tracked hands required");
            initialized=true;head=view;left=leftHand;right=rightHand;actorRoot=playerRoot;content=new GameObject("Independent source lobby gallery").transform;content.SetParent(transform,false);visuals=new R23Visuals();
            trace=new GameObject("Source harmless preview097 trace").AddComponent<LineRenderer>();trace.transform.SetParent(content,false);trace.positionCount=2;trace.useWorldSpace=true;trace.startWidth=.0025f;trace.endWidth=.0025f;
            traceMaterial=new Material(Shader.Find("Universal Render Pipeline/Unlit"));traceMaterial.SetColor("_BaseColor",new Color(1,.894f,.627f));trace.sharedMaterial=traceMaterial;trace.enabled=false;StartCoroutine(LoadSafely());
        }
        IEnumerator LoadAll(){foreach(string id in new[]{"mk18","akm","spr"}){var model=new R23GalleryModel(SourceRifleDefinition.ForId(id),content);models.Add(id,model);yield return model.Load(visuals,head,content,Clear,volume,Nearest);model.Hide();}visuals.ReleaseLoadedBytes();Ready=true;if(Active)DisplayDock();Publish();yield return LoadMagnum();yield return LoadExtra("cx4");yield return LoadExtra("px4");yield return LoadM1014();foreach(string id in new[]{"ump","p90","mp5","mp9","1911","tec9","rfb","fal","s85"})yield return LoadExtra(id);yield return LoadSmawGallerySafely();yield return LoadKnifeGallerySafely();}
        IEnumerator LoadMagnum()
        {
            var jobs=new Stack<IEnumerator>();
            try{magnumModel=new R23MagnumGalleryModel(content);jobs.Push(magnumModel.Load(volume));}catch(Exception error){magnumFailure=error.ToString();}
            while(jobs.Count>0&&magnumFailure==null)
            {
                bool moved=false;object value=null;try{moved=jobs.Peek().MoveNext();if(moved)value=jobs.Peek().Current;}catch(Exception error){magnumFailure=error.ToString();}
                if(magnumFailure!=null)break;
                if(!moved){var done=jobs.Pop() as IDisposable;if(done!=null)done.Dispose();continue;}
                var child=value as IEnumerator;if(child!=null)jobs.Push(child);else yield return value;
            }
            while(jobs.Count>0){var done=jobs.Pop() as IDisposable;if(done!=null)done.Dispose();}
            if(magnumFailure!=null){if(magnumModel!=null)magnumModel.Hide();Debug.LogWarning("Source Magnum gallery unavailable: "+magnumFailure);}else if(Active&&magnumSelected)DisplayDock();Publish();
        }
        IEnumerator LoadM1014()
        {
            var jobs=new Stack<IEnumerator>();try{m1014Model=new R23M1014GalleryModel(content);jobs.Push(m1014Model.Load(volume));}catch(Exception e){m1014Failure=e.ToString();}
            while(jobs.Count>0&&m1014Failure==null)
            {
                bool moved=false;object value=null;try{moved=jobs.Peek().MoveNext();if(moved)value=jobs.Peek().Current;}catch(Exception e){m1014Failure=e.ToString();}
                if(m1014Failure!=null)break;if(!moved){var done=jobs.Pop() as IDisposable;if(done!=null)done.Dispose();continue;}
                var nested=value as IEnumerator;if(nested!=null)jobs.Push(nested);else yield return value;
            }
            while(jobs.Count>0){var done=jobs.Pop() as IDisposable;if(done!=null)done.Dispose();}
            if(m1014Failure!=null){if(m1014Model!=null)m1014Model.Hide();Debug.LogWarning("Source M1014 gallery unavailable: "+m1014Failure);}else if(Active&&m1014Selected)DisplayDock();Publish();
        }
        IEnumerator LoadExtra(string id)
        {
            R23GalleryModel model=null;string issue=null;var jobs=new Stack<IEnumerator>();
            try{var definition=SourceRifleDefinition.ForId(id);if(definition==null)throw new InvalidOperationException("Actual source definition unavailable: "+id);model=new R23GalleryModel(definition,content);pendingModels.Add(model);jobs.Push(model.Load(visuals,head,content,Clear,volume,Nearest));}catch(Exception error){issue=error.ToString();}
            while(jobs.Count>0&&issue==null)
            {
                bool moved=false;object value=null;try{moved=jobs.Peek().MoveNext();if(moved)value=jobs.Peek().Current;}catch(Exception error){issue=error.ToString();}
                if(issue!=null)break;if(!moved){var done=jobs.Pop() as IDisposable;if(done!=null)done.Dispose();continue;}
                var child=value as IEnumerator;if(child!=null)jobs.Push(child);else yield return value;
            }
            while(jobs.Count>0){var done=jobs.Pop() as IDisposable;if(done!=null)done.Dispose();}
            if(model!=null)pendingModels.Remove(model);
            if(issue!=null){extraFailures[id]=issue;if(model!=null)model.Dispose();Debug.LogWarning("Source "+id+" gallery unavailable: "+issue);}
            else{models.Add(id,model);visuals.ReleaseLoadedBytes();model.SetVolume(volume);model.Hide();if(Active&&ViewedWeaponId==id)DisplayDock();}Publish();
        }
        IEnumerator LoadSafely() {
            var stack=new Stack<IEnumerator>();stack.Push(LoadAll());
            while(stack.Count>0){bool next=false;object value=null;try{next=stack.Peek().MoveNext();if(next)value=stack.Peek().Current;}catch(Exception error){Failure=error.Message;Hide();Publish();Debug.LogException(error);yield break;}if(!next){var done=stack.Pop() as IDisposable;if(done!=null)done.Dispose();continue;}var nested=value as IEnumerator;if(nested!=null)stack.Push(nested);else yield return value;}
        }
        public bool Show(string id) {
            if(!initialized)throw new InvalidOperationException("Initialize gallery first");if(R24MeleeProfiles.ForId(id)==null&&id!="smaw"&&id!="magnum"&&id!="m1014"&&SourceRifleDefinition.ForId(id)==null){Hide();return false;}if(Active&&ViewedWeaponId==id)return true;
            bool wasActive=Active;Release();foreach(var model in models.Values)model.Hide();if(magnumModel!=null)magnumModel.Hide();if(m1014Model!=null)m1014Model.Hide();
            knifeSelected=R24MeleeProfiles.ForId(id)!=null;if(knifeSelected){knifeProfile=id;knifeRoot=knifeRoots.ContainsKey(id)?knifeRoots[id]:null;}knifeActive=knifeSelected;if(knifeRoot!=null)knifeRoot.gameObject.SetActive(false);smawSelected=id=="smaw";smawActive=smawSelected;if(smawDock!=null)smawDock.gameObject.SetActive(false);magnumSelected=id=="magnum";m1014Selected=id=="m1014";session.Hide();magnumSession.Hide();m1014Session.Hide();if(knifeSelected){}else if(smawSelected){smawShots=0;}else if(m1014Selected)m1014Session.Show();else if(magnumSelected)magnumSession.Show();else if(!session.Show(id))return false;
            Vector3 forward=head.transform.forward;Vector3 up=U(R23GalleryRules.ViewUp(F(forward)));dockPosition=head.transform.position+U(R23GalleryRules.DockOffset(F(forward)));dockRotation=Quaternion.LookRotation(forward,up)*Quaternion.AngleAxis(R23GalleryRules.UnityYawDegrees,Vector3.up);
            previousGrip[0]=previousGrip[1]=true;previousReload=true;if(Ready)DisplayDock();if(!wasActive&&ActiveChanged!=null)ActiveChanged(true);if(ViewedWeaponChanged!=null)ViewedWeaponChanged(id);Publish();return true;
        }
        public void Hide(){bool wasActive=Active;Release();knifeActive=false;if(knifeRoot!=null)knifeRoot.gameObject.SetActive(false);smawActive=false;if(smawDock!=null)smawDock.gameObject.SetActive(false);session.Hide();magnumSession.Hide();m1014Session.Hide();foreach(var model in models.Values)model.Hide();if(magnumModel!=null)magnumModel.Hide();if(m1014Model!=null)m1014Model.Hide();ClearTraces();if(wasActive&&ActiveChanged!=null)ActiveChanged(false);if(wasActive&&ViewedWeaponChanged!=null)ViewedWeaponChanged(null);Publish();}
        void DisplayDock(){if(knifeSelected){DockKnife();return;}if(smawSelected){DockSmaw();return;}if(m1014Selected){if(M1014Ready)m1014Model.Dock(content,dockPosition,dockRotation);return;}if(magnumSelected){if(MagnumReady)magnumModel.Dock(content,dockPosition,dockRotation);return;}R23GalleryModel model;if(ViewedWeaponId!=null&&models.TryGetValue(ViewedWeaponId,out model))model.Dock(content,dockPosition,dockRotation);}
        void Release(){int hand=holder;ReleaseKnife();ReleaseSmaw();session.Release();magnumSession.Release();m1014Session.Release();ClearTraces();holder=-1;if(Ready&&Active)DisplayDock();if(hand>=0&&HoldingChanged!=null)HoldingChanged(hand,false);}
        public void SetSfxVolume(float value){if(float.IsNaN(value)||float.IsInfinity(value))throw new ArgumentOutOfRangeException("value");volume=Mathf.Clamp01(value);foreach(var model in models.Values)model.SetVolume(volume);if(magnumModel!=null)magnumModel.SetVolume(volume);if(m1014Model!=null)m1014Model.SetVolume(volume);if(smawPreview!=null)smawPreview.SetSfxVolume(volume);if(knifeAudio!=null)knifeAudio.SetVolume(volume);}
        public bool Sample(float seconds,R23HandsInputFrame input,bool focused=true,int primaryHand=1){return Sample(seconds,input.Snapshot,focused,primaryHand);}
        public bool Sample(float seconds,R23HandsInputSnapshot input,bool focused=true,int primaryHand=1) {
            if(float.IsNaN(seconds)||float.IsInfinity(seconds)||seconds<0||seconds>.25f||primaryHand<0||primaryHand>1)throw new ArgumentOutOfRangeException("seconds/primaryHand");
            if(!Active||!Ready||!IsWeaponReady(ViewedWeaponId)){previousGrip[0]=input.Left.GripHeld;previousGrip[1]=input.Right.GripHeld;return false;}
            if(knifeSelected)return SampleKnife(seconds,input,focused,primaryHand);
            if(smawSelected)return SampleSmaw(seconds,input,focused,primaryHand);
            if(m1014Selected)return SampleM1014(seconds,input,focused,primaryHand);
            if(magnumSelected)return SampleMagnum(seconds,input,focused,primaryHand);
            var dominant=input.Hand(primaryHand);var selected=models[ViewedWeaponId];
            if(holder>=0){var hand=input.Hand(holder);if(!focused||!hand.Tracked||hand.Reserved||!hand.GripHeld)Release();}
            if(holder<0&&dominant.GripHeld&&!previousGrip[primaryHand]) {
                var hand=primaryHand==0?left:right;Vector3 delta=selected.Root.position-hand.position;float distance=delta.magnitude;float dot=distance<.000001f?1:Vector3.Dot(R23TrackedGripPose.Forward(hand),delta/distance);
                if(R23GalleryRules.CanGrab(distance,dot,dominant.Tracked,dominant.Reserved,focused)&&session.Grab()){holder=primaryHand;selected.Attach(hand,primaryHand==0?right:left,primaryHand==0);previousReload=input.ReloadRequested||input.Right.Tracked&&input.Right.StickClick;if(HoldingChanged!=null)HoldingChanged(holder,true);}
            }
            bool shot=false;
            if(holder>=0&&session.Live) {
                var firing=input.Hand(holder);var support=input.Hand(1-holder);var supportTransform=holder==0?right:left;var state=session.State.Value;
                var controls=R23HandRoleRules.Project(input,holder,F(selected.SupportLocal(supportTransform.position)),selected.ForegripDistance(supportTransform.position));bool reload=input.ReloadRequested||input.Right.Tracked&&input.Right.StickClick;
                shot=session.Definition.SourceManualControls||session.Definition.Id=="rfb"?session.StepSource(seconds,controls,reload,firing.StickX,firing.StickY,focused):session.Step(seconds,controls,reload&&!previousReload,focused,firing.TriggerReleased);previousReload=reload;var after=session.State.Value;
                if(state.Phase!=after.Phase)selected.Cue(ReloadCue(state.Phase,after.Phase));
                if(shot){selected.Cue("shot",true);PreviewTrace(selected.Muzzle.position,selected.Muzzle.forward);}
                selected.Apply(after,focused,support.Tracked&&!support.Reserved,seconds);
            }
            previousGrip[0]=input.Left.GripHeld;previousGrip[1]=input.Right.GripHeld;Publish();return shot;
        }
        bool SampleM1014(float seconds,R23HandsInputSnapshot input,bool focused,int primaryHand)
        {
            var primary=input.Hand(primaryHand);
            if(holder>=0){var holding=input.Hand(holder);if(!focused||!holding.Tracked||holding.Reserved||!holding.GripHeld)Release();}
            if(holder<0&&primary.GripHeld&&!previousGrip[primaryHand])
            {
                var hand=primaryHand==0?left:right;var delta=m1014Model.Root.position-hand.position;float distance=delta.magnitude,dot=distance<.000001f?1:Vector3.Dot(R23TrackedGripPose.Forward(hand),delta/distance);
                if(R23GalleryRules.CanGrab(distance,dot,primary.Tracked,primary.Reserved,focused)&&m1014Session.Grab()){holder=primaryHand;m1014Model.Attach(hand);if(HoldingChanged!=null)HoldingChanged(holder,true);}
            }
            bool shot=false;
            if(holder>=0&&m1014Session.Live)
            {
                var firing=input.Hand(holder);var support=holder==0?right:left;
                var controls=R23HandRoleRules.Project(input,holder,F(m1014Model.SupportLocal(support.position)),m1014Model.ForegripDistance(support.position));
                R23M1014Volley volley;shot=m1014Session.Step(seconds,controls,input.ReloadRequested||input.Right.Tracked&&input.Right.StickClick,firing.StickX,firing.StickY,m1014Model.FiringPose(),focused,out volley);
                var owner=m1014Session.Owner;if(!string.IsNullOrEmpty(owner.ReloadCue))m1014Model.Cue(owner.ReloadCue);
                if(shot){m1014Model.Cue("shot",true);PreviewVolley(volley);}
                m1014Model.Present(owner,focused,holder,seconds);
            }
            previousGrip[0]=input.Left.GripHeld;previousGrip[1]=input.Right.GripHeld;Publish();return shot;
        }
        bool SampleMagnum(float seconds,R23HandsInputSnapshot input,bool focused,int primaryHand)
        {
            if(!MagnumReady){previousGrip[0]=input.Left.GripHeld;previousGrip[1]=input.Right.GripHeld;return false;}
            var primary=input.Hand(primaryHand);
            if(holder>=0){var holding=input.Hand(holder);if(!focused||!holding.Tracked||holding.Reserved||!holding.GripHeld){Release();if(trace!=null)trace.enabled=false;}}
            if(holder<0&&primary.GripHeld&&!previousGrip[primaryHand])
            {
                var hand=primaryHand==0?left:right;Vector3 delta=magnumModel.Root.position-hand.position;float distance=delta.magnitude,aim=distance<.000001f?1:Vector3.Dot(R23TrackedGripPose.Forward(hand),delta/distance);
                if(R23GalleryRules.CanGrab(distance,aim,primary.Tracked,primary.Reserved,focused)&&magnumSession.Grab()){holder=primaryHand;magnumModel.Attach(hand);if(HoldingChanged!=null)HoldingChanged(holder,true);}
            }
            bool shot=false;
            if(holder>=0&&magnumSession.Live)
            {
                var firing=input.Hand(holder);var support=holder==0?right:left;var hand=holder==0?left:right;
                var controls=R23HandRoleRules.Project(input,holder,F(magnumModel.SupportLocal(support.position)),magnumModel.ForegripDistance(support.position));
                var basis=actorRoot==null?content:actorRoot;var corrected=hand.rotation*R23TrackedGripPose.LocalRotation;
                controls.HasPrimaryPose=true;controls.PrimaryPoint=F(basis.InverseTransformPoint(hand.position));controls.PrimaryForward=F(basis.InverseTransformDirection(corrected*Vector3.forward));controls.PrimaryUp=F(basis.InverseTransformDirection(corrected*Vector3.up));
                shot=magnumSession.Step(seconds,new R23MagnumInput(controls,primaryButton:input.ReloadRequested||input.Right.Tracked&&input.Right.StickClick,triggerBlocksFlick:firing.TriggerPressed||firing.Trigger>.2f,stickX:firing.StickX,stickY:firing.StickY),focused);
                var owner=magnumSession.Owner;if(owner.LastReloadCue.Length>0)magnumModel.Cue(owner.LastReloadCue);
                if(shot){magnumModel.Cue("shot",true);PreviewTrace(magnumModel.Muzzle.position,magnumModel.Muzzle.forward);}
                magnumModel.Present(owner,focused,holder,support);
            }
            previousGrip[0]=input.Left.GripHeld;previousGrip[1]=input.Right.GripHeld;Publish();return shot;
        }
        static string ReloadCue(ReloadPhase before,ReloadPhase after){if(before==ReloadPhase.Ready&&(after==ReloadPhase.NeedsBolt||after==ReloadPhase.NeedsRack))return "";switch(after){case ReloadPhase.NeedsMagazine:return "mag_eject";case ReloadPhase.HeldMagazine:return "pouch";case ReloadPhase.NeedsRack:return "mag_insert";case ReloadPhase.Ready:return "bolt";default:return "";}}
        void PreviewTrace(Vector3 origin,Vector3 direction){RaycastHit hit;Vector3 end=origin+direction*40;if(Nearest(origin,direction,40,out hit))end=hit.point;trace.SetPosition(0,origin);trace.SetPosition(1,end);trace.enabled=true;traceUntil=Time.unscaledTime+.07f;}
        void PreviewVolley(R23M1014Volley volley)
        {
            ClearTraces();var directions=volley.Directions;
            while(pelletTraces.Count<directions.Length){var line=new GameObject("Source harmless M1014 gallery pellet trace").AddComponent<LineRenderer>();line.transform.SetParent(content,false);line.positionCount=2;line.useWorldSpace=true;line.startWidth=line.endWidth=.0025f;line.sharedMaterial=traceMaterial;line.enabled=false;pelletTraces.Add(line);}
            var origin=U(volley.Origin);for(int i=0;i<directions.Length;i++){Vector3 direction=U(directions[i]),end=origin+direction*volley.Range;RaycastHit hit;if(Nearest(origin,direction,volley.Range,out hit))end=hit.point;pelletTraces[i].SetPosition(0,origin);pelletTraces[i].SetPosition(1,end);pelletTraces[i].enabled=true;}traceUntil=Time.unscaledTime+.07f;
        }
        void ClearTraces(){if(trace!=null)trace.enabled=false;foreach(var line in pelletTraces)if(line!=null)line.enabled=false;}
        bool Excluded(Collider collider){if(collider.gameObject.layer==20)return true;var t=collider.transform;return t==content||t.IsChildOf(content)||t==head.transform||t.IsChildOf(head.transform)||t==left||t.IsChildOf(left)||t==right||t.IsChildOf(right)||(actorRoot!=null&&(t==actorRoot||t.IsChildOf(actorRoot)));}
        bool Nearest(Vector3 origin,Vector3 direction,float range,out RaycastHit nearest){nearest=new RaycastHit();float best=float.PositiveInfinity;foreach(var hit in Physics.RaycastAll(origin,direction,range,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore))if(!Excluded(hit.collider)&&hit.distance<best){best=hit.distance;nearest=hit;}return best<float.PositiveInfinity;}
        bool Clear(Vector3 origin,Vector3 point){RaycastHit hit;Vector3 ray=point-origin;return !Nearest(origin,ray.normalized,ray.magnitude,out hit);}
        void Publish(){var state=ViewState;string key=state.WeaponId+"|"+state.Status+"|"+state.HoldingHand+"|"+state.PreviewShots+"|"+state.ScopeActive;if(key==lastStatus)return;lastStatus=key;if(ViewStateChanged!=null)ViewStateChanged(state);}
        void Update(){if(Time.unscaledTime>=traceUntil)ClearTraces();}
        void OnDisable(){if(initialized)Hide();}
        void OnDestroy(){StopAllCoroutines();ReleaseKnife();DisposeKnifeGallery();if(knifeAudio!=null)knifeAudio.Dispose();ReleaseSmaw();session.Hide();magnumSession.Hide();m1014Session.Hide();if(m1014Model!=null)m1014Model.Dispose();if(magnumModel!=null)magnumModel.Dispose();foreach(var model in pendingModels)model.Dispose();pendingModels.Clear();foreach(var model in models.Values)model.Dispose();if(visuals!=null)visuals.Dispose();if(content!=null)Destroy(content.gameObject);if(traceMaterial!=null)Destroy(traceMaterial);}
        static Float3 F(Vector3 value){return new Float3(value.x,value.y,value.z);}static Vector3 U(Float3 value){return new Vector3(value.X,value.Y,value.Z);}
    }
}
#endif
