#if UNITY_6000_0_OR_NEWER
using System;
using UnityEngine;
namespace Ashfall.Integration.Playable.Input
{
    // Immutable local receipt; no transport, inventory or damage authority.
    public sealed class R23TeamMark
    {
        public readonly string Id,Action,Color;public readonly int Index;
        public readonly Vector3 Position,Normal;public readonly bool SurfaceDecal;
        public int Schema {get{return 1;}}public bool Networked {get{return false;}}
        public float LifetimeSeconds {get{return 30;}}
        internal R23TeamMark(int serial,int index,Vector3 point,Vector3 normal)
        {Id="local:"+serial;Index=index;Action=R23TeamMarksState.Action(index);Color=R23TeamMarksState.ColorName(index);Position=point;Normal=normal;SurfaceDecal=R23TeamMarksState.Paint(index);}
    }
    // Port of original Opus R23 team_marks.gd. Root supplies corrected aim,
    // primary-hand B/Y and raw primary stick; it retains tracking/locomotion.
    public sealed class R23TeamMarks:MonoBehaviour
    {
        readonly R23TeamMarksState state=new R23TeamMarksState();readonly R23TeamMarkLifetime lifetime=new R23TeamMarkLifetime();
        Transform head,actor,rack;R23TeamMarksView view;AudioSource voice;AudioClip ping;
        Vector3 capturedPoint,capturedNormal,pressForward;bool captured,tactical,initialized;int serial;float sfxVolume=1;
        public bool Ready {get;private set;}public string Failure {get;private set;}
        public bool WheelOpen {get{return state.WheelOpen;}}public bool InputHeld {get{return state.Held;}}
        public int SelectedIndex {get{return state.Selected;}}public int PreferredIndex {get{return state.Preferred;}}
        public bool WeaponContextAvailable {get{return false;}}
        public string WeaponContextStatus {get{return "Dropped-weapon mark disabled: no actual pickup context provider is bound.";}}
        public R23TeamMark Current {get;private set;}
        public float Remaining {get{return lifetime.Remaining;}}
        public bool MarkVisible {get{return view!=null&&view.MarkVisible;}}
        public int VisibleColumnDots {get{return view==null?0:view.DotCount;}}
        public float IconWorldWidth {get{return view==null?0:view.IconWidth;}}
        public Vector3 IconWorldPosition {get{return view==null?Vector3.zero:view.IconPosition;}}
        public int AudioEvents {get;private set;}
        public bool SourceAudioReady {get{return ping!=null&&ping.samples==17280&&ping.frequency==48000;}}
        public event Action<R23TeamMark> MarkCreated;
        public event Action<bool> WheelOpenChanged;
        public void Initialize(Transform viewHead,Transform ownerActor,Transform ownerRack=null)
        {
            if(initialized)throw new InvalidOperationException("Team marks already initialized");
            if(viewHead==null||ownerActor==null)throw new ArgumentException("Head and actor required");
            initialized=true;head=viewHead;actor=ownerActor;rack=ownerRack;
            try
            {
                view=new R23TeamMarksView(head);
                ping=AudioClip.Create("Original R23 mark_ping0993_00.wav",17280,1,48000,false);
                if(!ping.SetData(R23TeamMarksAssets.PingSamples(),0))throw new InvalidOperationException("Source ping PCM decode failed");
                var go=new GameObject("Source local mark 2D SFX voice");go.transform.SetParent(transform,false);voice=go.AddComponent<AudioSource>();
                voice.playOnAwake=false;voice.loop=false;voice.spatialBlend=0;voice.dopplerLevel=0;voice.ignoreListenerPause=false;SetSfxVolume(sfxVolume);
                Ready=true;
            }
            catch(Exception e){Failure=e.ToString();Release();Debug.LogError("Source local team marks unavailable: "+Failure);}
        }
        // Source volume_db=-8, SFX bus supplied by the persisted UI value.
        // Master is already applied to AudioListener by root; never multiply twice.
        public void SetSfxVolume(float value)
        {sfxVolume=R23TeamMarksState.Finite(value)?Mathf.Clamp01(value):0;if(voice!=null)voice.volume=Mathf.Pow(10,-8f/20)*sfxVolume;}
        // eligible = input enabled + focus/tracking/weapon-hand readiness + alive
        // + !paused/complete + !owned-wheel/backpack/field-menu selection.
        // Return true only when this sample creates a mark on release.
        public bool Sample(float seconds,bool secondaryHeld,Vector2 stick,Ray correctedRay,bool eligible,Vector3 locomotionAnchor)
        {
            if(!Ready)return false;bool wasOpen=WheelOpen;
            eligible=eligible&&isActiveAndEnabled&&head!=null&&actor!=null&&Finite(correctedRay.origin)&&Finite(correctedRay.direction)&&correctedRay.direction.sqrMagnitude>.000001f&&Finite(locomotionAnchor);
            Vector2 choice=stick;
            if(choice.magnitude<.25f&&state.Held)
            {
                var horizontal=pressForward;horizontal.y=0;if(horizontal.sqrMagnitude<.001f)horizontal=Vector3.forward;horizontal.Normalize();
                Vector3 right=Vector3.Cross(Vector3.up,horizontal),delta=correctedRay.direction.normalized-pressForward;
                choice=new Vector2(Vector3.Dot(delta,right),Vector3.Dot(delta,Vector3.up))*3;
            }
            int release=state.Sample(seconds,secondaryHeld,eligible,choice.x,choice.y);
            if(state.Began)
            {
                pressForward=correctedRay.direction.normalized;
                captured=Surface(correctedRay,false,out capturedPoint,out capturedNormal);
            }
            bool placed=release>=0&&captured&&Place(capturedPoint,capturedNormal,release);
            if(!state.Held)captured=false;
            if(WheelOpen)
            {
                if(!wasOpen)view.Open(correctedRay,locomotionAnchor);else view.Follow(locomotionAnchor);
                int pointed=view.Point(correctedRay);state.Point(pointed);view.Highlight(state.Selected,pointed);
            }
            else view.HideWheel();
            if(wasOpen!=WheelOpen&&WheelOpenChanged!=null)WheelOpenChanged(WheelOpen);
            return placed;
        }
        // Support-hand world_ping.gd shortcut delegates to the preferred team
        // mark but uses its world/construction-only query (no targets/panels).
        // Root supplies the neutral-rearmed support press edge and eligibility.
        public bool TryPlace(Ray correctedRay)
        {
            if(!Ready||!isActiveAndEnabled||head==null||actor==null)return false;
            Vector3 point,normal;return Surface(correctedRay,true,out point,out normal)&&Place(point,normal,state.Preferred);
        }
        bool Surface(Ray ray,bool supportShortcut,out Vector3 point,out Vector3 normal)
        {
            point=normal=Vector3.zero;if(!Finite(ray.origin)||!Finite(ray.direction)||ray.direction.sqrMagnitude<.000001f)return false;
            var camera=head.GetComponent<Camera>();float range=Mathf.Max(100000,camera==null?0:camera.farClipPlane*2);
            // Source mask1|4|8 -> Unity world0, construction8, player/target20,
            // ballistic panels19. Own actor and own rack always excluded.
            int mask=(1<<0)|(1<<8);if(!supportShortcut)mask|=(1<<19)|(1<<20);
            float nearest=float.PositiveInfinity;bool found=false,backfaces=Physics.queriesHitBackfaces;
            try
            {
                Physics.SyncTransforms();Physics.queriesHitBackfaces=true;
                foreach(var hit in Physics.RaycastAll(ray.origin,ray.direction.normalized,range,mask,QueryTriggerInteraction.Ignore))
                {
                    var c=hit.collider;if(c==null||Owned(c.transform)||hit.distance>=nearest||!Finite(hit.point)||!Finite(hit.normal)||hit.normal.sqrMagnitude<.0001f)continue;
                    point=hit.point;normal=hit.normal;nearest=hit.distance;found=true;
                }
            }
            finally{Physics.queriesHitBackfaces=backfaces;}
            return found;
        }
        bool Owned(Transform t){return t==actor||t.IsChildOf(actor)||(rack!=null&&(t==rack||t.IsChildOf(rack)));}
        bool Place(Vector3 point,Vector3 normal,int index)
        {
            if(!Finite(point)||!Finite(normal)||normal.sqrMagnitude<.0001f||index<0||index>=8)return false;
            Current=new R23TeamMark(++serial,index,point,normal);lifetime.Place();view.Place(point,normal,index);view.UpdateMark(tactical);
            // At most one voice. Replace the short previous ping, no accumulating
            // AudioSource objects and no UI/gameplay sound dependency.
            voice.Stop();voice.clip=ping;voice.Play();AudioEvents++;
            if(MarkCreated!=null)MarkCreated(Current);return true;
        }
        // Feed unscaled seconds even while paused/ineligible: original records
        // expire by wall-clock time, independently of mark-wheel eligibility.
        public void Tick(float unscaledSeconds,bool tacticalMap=false)
        {
            if(!Ready)return;tactical=tacticalMap;lifetime.Tick(unscaledSeconds);
            if(!lifetime.Active){Current=null;view.ClearMark();}else view.UpdateMark(tactical);
        }
        // Pause/focus invalidation cancels the held gesture and demands neutral;
        // source local marks persist and age. Map/reset calls ClearAll instead.
        public void CancelInput(){bool was=WheelOpen;state.Cancel();captured=false;if(view!=null)view.HideWheel();if(was&&WheelOpenChanged!=null)WheelOpenChanged(false);}
        public void ClearAll(){CancelInput();lifetime.Clear();Current=null;if(view!=null)view.ClearMark();if(voice!=null)voice.Stop();}
        public Vector3 FixtureSectorPoint(int index,float radius=.225f){if(!Ready||index<0||index>7)throw new InvalidOperationException("Ready wheel and sector0..7 required");return view.SectorPoint(index,radius);}
        public Vector3 WheelWorldPosition {get{return view==null?Vector3.zero:view.WheelPosition;}}
        public void RefreshPresentation(){if(Ready){view.FitHeader();view.UpdateMark(tactical);}}
        void LateUpdate(){RefreshPresentation();}
        static bool Finite(Vector3 v){return R23TeamMarksState.Finite(v.x)&&R23TeamMarksState.Finite(v.y)&&R23TeamMarksState.Finite(v.z);}
        void OnApplicationFocus(bool focused){if(!focused)CancelInput();}
        void OnApplicationPause(bool paused){if(paused)CancelInput();}
        void OnDisable(){ClearAll();}
        void Release(){Ready=false;if(view!=null){view.Dispose();view=null;}if(voice!=null)Destroy(voice.gameObject);if(ping!=null)Destroy(ping);}
        void OnDestroy(){ClearAll();Release();}
    }
}
#endif
