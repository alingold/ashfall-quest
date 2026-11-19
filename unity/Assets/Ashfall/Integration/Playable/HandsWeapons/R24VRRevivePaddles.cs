#if UNITY_6000_0_OR_NEWER
using System;using System.Collections;using System.Collections.Generic;using Ashfall.Core;using UnityEngine;using UnityEngine.Rendering;
namespace Ashfall.Integration.Playable.HandsWeapons
{
    // Reusable source gear; it consumes no carried slot or HP. The caller must
    // holster competing tools on Equip and supply only actual revivable targets.
    public sealed class R24VRRevivePaddles:MonoBehaviour
    {
        readonly R24RevivePaddlesState state=new R24RevivePaddlesState();R23Visuals art;R24ItemAudio audio;Transform left,right,leftArt,rightArt,meter,fill,worldFx;TextMesh label;Font font;AudioSource chargeVoice,readyVoice,dischargeVoice;Material arcsMaterial,meterMaterial,fontMaterial;Mesh corona,discharge;MeshRenderer coronaRenderer,dischargeRenderer;float fxRemaining,fxAge,coronaAge,volume=1;Vector3[] ends;bool initialized,paused;Func<R24ReviveTarget[]> targets;Func<Float3,Float3,bool> lineClear;Func<string,bool> completeRevive;
        public R24RevivePaddlesState State{get{return state;}}public bool Ready{get;private set;}public bool Loading{get;private set;}public string Failure{get;private set;}
        public bool CoronaVisible{get{return coronaRenderer!=null&&coronaRenderer.enabled;}}public bool DischargeVisible{get{return dischargeRenderer!=null&&dischargeRenderer.enabled;}}public bool MeterVisible{get{return meter!=null&&meter.gameObject.activeInHierarchy;}}public Vector3 LeftPaddlePosition{get{return left==null?Vector3.zero:left.position;}}public Vector3 RightPaddlePosition{get{return right==null?Vector3.zero:right.position;}}
        internal bool SharedTouch;
        float sharedCharge,sharedRemaining;
        public float PresentedCharge{get{return SharedTouch?sharedCharge:state.Charge;}}
        public float PresentedRemaining{get{return SharedTouch?sharedRemaining:state.Remaining;}}
        internal void PresentSharedCharge(float charge,float remaining){sharedCharge=charge;sharedRemaining=remaining;}
        void PresentChargeSound(AudioSource voice,float charge,float duration)
        {
            var clip=audio.Clip("revive-charge");if(clip==null)return;
            voice.pitch=clip.length/duration;
            if(voice.isPlaying)return;
            audio.Play(voice,"revive-charge",Mathf.Clamp01(charge)*clip.length);
        }
        void PresentReadySound(AudioSource voice,float remaining)
        {
            var clip=audio.Clip("revive-ready");
            float offset=Mathf.Max(0,R24RevivePaddlesState.ReadyWindow-remaining);
            if(clip==null||offset>=clip.length||voice.isPlaying)return;
            voice.pitch=1;audio.Play(voice,"revive-ready",offset);
        }
        internal AudioSource MakeRemoteChargeVoice(Transform pad)
        {
            var voice=pad.gameObject.AddComponent<AudioSource>();
            voice.playOnAwake=false;voice.spatialBlend=1;voice.dopplerLevel=0;voice.minDistance=1;voice.maxDistance=8;
            return voice;
        }
        internal void PresentRemoteChargeSound(AudioSource voice,float charge,float remaining,bool changed)
        {
            voice.volume=volume*.22f;
            if(!changed)return;
            voice.Stop();
            if(remaining>0)PresentReadySound(voice,remaining);
            else if(charge>0)PresentChargeSound(voice,charge,R24RevivePaddlesState.Slowest);
        }
        internal Mesh MakeRemoteCorona(Transform pad,out MeshRenderer renderer)
        {return MakeMesh("remote charged corona",pad,out renderer);}
        internal void DrawRemoteCorona(Transform pad,Mesh mesh,MeshRenderer renderer,int tick)
        {AppendCorona(pad,tick,true);Commit(mesh,renderer);}
        internal Transform CopyPaddle(Transform glove,int hand)
        {
            var copy=Instantiate(hand==0?leftArt:rightArt,glove,false);return copy;
        }
        internal void PlayRescue(AudioSource voice)
        {
            voice.spatialBlend=1;voice.dopplerLevel=0;voice.minDistance=1;voice.maxDistance=12;
            voice.volume=volume*.25f;audio.Play(voice,"revive-discharge");
        }
        internal void PlayOwnRescue(Vector3 at)
        {dischargeVoice.transform.position=at;PlayRescue(dischargeVoice);}
        public event Action<string> Revived;public event Action Charged;
        public void Initialize(Func<R24ReviveTarget[]> actualEligibleTargets,Func<Float3,Float3,bool> actualLineClear,Func<string,bool> actualCompletedRevive){if(initialized)throw new InvalidOperationException("paddles already initialized");if(actualEligibleTargets==null||actualLineClear==null||actualCompletedRevive==null)throw new ArgumentNullException("actual revive callbacks");initialized=true;targets=actualEligibleTargets;lineClear=actualLineClear;completeRevive=actualCompletedRevive;Loading=true;StartCoroutine(Guard(Load()));}
        IEnumerator Guard(IEnumerator work){var stack=new Stack<IEnumerator>();stack.Push(work);while(stack.Count>0){object value;try{var top=stack.Peek();if(!top.MoveNext()){stack.Pop();continue;}value=top.Current;}catch(Exception ex){Failure=ex.Message;Loading=false;yield break;}var nested=value as IEnumerator;if(nested!=null)stack.Push(nested);else yield return value;}Loading=false;Ready=true;}
        IEnumerator Load(){art=new R23Visuals();audio=new R24ItemAudio();audio.SetVolume(volume);left=Node("R5 left hospital paddle",transform);right=Node("R5 right hospital paddle",transform);leftArt=Node("Quarter-turn paddle artwork",left);rightArt=Node("Quarter-turn paddle artwork",right);leftArt.localRotation=rightArt.localRotation=Quaternion.AngleAxis(90,Vector3.forward);yield return art.Load("revive-paddle",leftArt);yield return art.Load("revive-paddle",rightArt);yield return audio.Load("revive-charge","revive-ready","revive-discharge");chargeVoice=audio.Voice(transform,"source recorded charge",-13,8);readyVoice=audio.Voice(transform,"source ready window",-13,8);dischargeVoice=audio.Voice(transform,"source completed discharge",-12,8);
            var shader=Resources.Load<Shader>("R23GunFeedback");var arcShader=Resources.Load<Shader>("R24ReviveArcs");font=Resources.Load<Font>("R23GodotUI");if(shader==null||arcShader==null||font==null)throw new InvalidOperationException("source paddle feedback resources missing");meter=Node("R5 charge meter",transform);meter.gameObject.layer=5;meterMaterial=new Material(shader);meterMaterial.SetColor("_Color",new Color(.082f,.145f,.169f));meterMaterial.SetFloat("_Depth",8);var backing=Box("meter backing",meter,new Vector3(.29f,.023f,.003f),Vector3.zero,meterMaterial);backing.GetComponent<MeshRenderer>().sortingOrder=114;var fillMaterial=new Material(meterMaterial);fillMaterial.SetColor("_Color",new Color(0,.663f,.859f));fill=Box("charge fill",meter,new Vector3(.286f,.021f,.002f),new Vector3(0,0,-.003f),fillMaterial);fill.GetComponent<MeshRenderer>().sortingOrder=115;var text=Node("source charge text",meter);text.gameObject.layer=5;text.localPosition=new Vector3(0,.03f,-.004f);text.localRotation=Quaternion.Euler(0,180,0);label=text.gameObject.AddComponent<TextMesh>();label.font=font;label.fontSize=22;label.characterSize=.00065f*10;label.anchor=TextAnchor.MiddleCenter;label.alignment=TextAlignment.Center;fontMaterial=new Material(shader);fontMaterial.SetTexture("_MainTex",font.material.mainTexture);fontMaterial.SetFloat("_Font",1);fontMaterial.SetFloat("_Depth",8);label.GetComponent<MeshRenderer>().sharedMaterial=fontMaterial;label.GetComponent<MeshRenderer>().sortingOrder=116;
            arcsMaterial=new Material(arcShader);worldFx=new GameObject("R5 source paddle electrical arcs").transform;corona=MakeMesh("charged corona",worldFx,out coronaRenderer);discharge=MakeMesh("completed rescue discharge",worldFx,out dischargeRenderer);Stow();art.ReleaseLoadedBytes();}
        static Transform Node(string name,Transform parent){var t=new GameObject(name).transform;t.SetParent(parent,false);return t;}
        static Vector3 V(Float3 a){return new Vector3(a.X,a.Y,a.Z);}static Float3 F(Vector3 a){return new Float3(a.x,a.y,a.z);}static Quaternion Q(R23ShotPose p){return Quaternion.LookRotation(V(p.Forward),V(p.Up));}
        Transform Box(string name,Transform parent,Vector3 size,Vector3 at,Material material){var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.layer=parent.gameObject.layer;var c=go.GetComponent<Collider>();c.enabled=false;Destroy(c);go.transform.SetParent(parent,false);go.transform.localScale=size;go.transform.localPosition=at;go.GetComponent<MeshRenderer>().sharedMaterial=material;return go.transform;}
        Mesh MakeMesh(string name,Transform parent,out MeshRenderer renderer){var go=new GameObject(name);go.layer=19;go.transform.SetParent(parent,false);var mesh=new Mesh();mesh.MarkDynamic();go.AddComponent<MeshFilter>().sharedMesh=mesh;renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterial=arcsMaterial;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;return mesh;}
        public bool Equip(bool allowed){if(!Ready||!allowed)return false;state.Equip();audio.Stop();coronaAge=0;return true;}
        public void Stow(){state.Stow();if(audio!=null){if(chargeVoice!=null)chargeVoice.Stop();if(readyVoice!=null)readyVoice.Stop();}Hide();}
        public void Restart(){state.Restart();Stow();fxRemaining=0;if(dischargeRenderer!=null)dischargeRenderer.enabled=false;}
        void Hide(){if(left!=null)left.gameObject.SetActive(false);if(right!=null)right.gameObject.SetActive(false);if(meter!=null)meter.gameObject.SetActive(false);if(coronaRenderer!=null)coronaRenderer.enabled=false;}
        public void SetSfxVolume(float value){volume=Mathf.Clamp01(value);if(audio!=null)audio.SetVolume(volume);}
        // Corrected grip fallback is source paddle_pose when no live skeleton is available.
        // A caller with wrist/knuckle poses may supply the actual source fingertip frame.
        public static R23ShotPose FromCorrectedGrip(R23ShotPose grip){return new R23ShotPose(grip.Point(new Float3(0,0,.10f)),grip.Right,grip.Up,grip.Forward);}
        public static R23ShotPose FromGloveLandmarks(R23ShotPose correctedGrip,Float3 wrist,Float3 knuckle){var forward=V(knuckle)-V(wrist);if(!correctedGrip.Valid||!wrist.IsFinite||!knuckle.IsFinite||forward.sqrMagnitude<.000001f)return FromCorrectedGrip(correctedGrip);forward.Normalize();var up=V(correctedGrip.Up);if(Mathf.Abs(Vector3.Dot(up,forward))>.95f)up=V(correctedGrip.Right);var right=Vector3.Cross(up,forward).normalized;up=Vector3.Cross(forward,right);return new R23ShotPose(F(V(knuckle)+forward*.055f),F(right),F(up),F(forward));}
        static Float3 ActorLocal(R23ShotPose actor,Float3 point){var d=new Float3(point.X-actor.Origin.X,point.Y-actor.Origin.Y,point.Z-actor.Origin.Z);return new Float3(R23WeaponPoseMath.Dot(d,actor.Right),R23WeaponPoseMath.Dot(d,actor.Up),R23WeaponPoseMath.Dot(d,actor.Forward));}
        public bool Step(float dt,bool allowed,bool gameplayPaused,R23ShotPose actor,R23ShotPose leftPaddle,R23ShotPose rightPaddle,Camera actualHead){if(!Ready)return false;paused=gameplayPaused;if(!paused&&fxRemaining>0){fxAge+=dt;fxRemaining=Mathf.Max(0,fxRemaining-dt);DrawDischarge();}else if(dischargeRenderer!=null)dischargeRenderer.enabled=false;bool valid=allowed&&actor.Valid&&leftPaddle.Valid&&rightPaddle.Valid;bool completed=state.Step(dt,valid,paused,ActorLocal(actor,leftPaddle.Origin),ActorLocal(actor,rightPaddle.Origin),leftPaddle.Forward,rightPaddle.Forward);if(paused||!state.Active||!state.Visible){Hide();if(chargeVoice!=null)chargeVoice.Stop();if(readyVoice!=null)readyVoice.Stop();if(paused&&dischargeVoice!=null)dischargeVoice.Pause();return false;}if(dischargeVoice!=null)dischargeVoice.UnPause();
            left.gameObject.SetActive(true);right.gameObject.SetActive(true);left.SetPositionAndRotation(V(leftPaddle.Origin),Q(leftPaddle));right.SetPositionAndRotation(V(rightPaddle.Origin),Q(rightPaddle));var midpoint=(left.position+right.position)*.5f;chargeVoice.transform.position=readyVoice.transform.position=dischargeVoice.transform.position=midpoint;
            if(completed&&!SharedTouch){chargeVoice.Stop();if(Charged!=null)Charged();}
            bool charged=PresentedRemaining>0;
            if(charged||!state.Facing)chargeVoice.Stop();else PresentChargeSound(chargeVoice,PresentedCharge,state.ChargeDuration);
            if(charged)PresentReadySound(readyVoice,PresentedRemaining);else readyVoice.Stop();
            PresentMeter(actualHead);if(charged){coronaAge+=dt;DrawCorona();}else coronaRenderer.enabled=false;
            if(SharedTouch){
                // The host also checks the periodic accepted poses. Promptly publish brief
                // local contact only during the host's advertised ready window.
                if(charged)foreach(var target in targets()){
                    var chest=target.Basis.Origin;
                    if(Ashfall.Modules.HealthStates.HealthService.TouchesTorso(leftPaddle.Origin,chest)||Ashfall.Modules.HealthStates.HealthService.TouchesTorso(rightPaddle.Origin,chest)){completeRevive(target.Id);break;}
                }
                return false;
            }
            bool revived=false;foreach(var target in targets()??new R24ReviveTarget[0])if(state.TryRevive(leftPaddle.Origin,rightPaddle.Origin,target,lineClear,completeRevive)){ends=new[]{left.position,V(target.LeftPad),right.position,V(target.RightPad),V(target.LeftPad),V(target.RightPad)};fxRemaining=.55f;fxAge=0;audio.Play(dischargeVoice,"revive-discharge");if(Revived!=null)Revived(target.Id);revived=true;break;}
            if(fxRemaining>0)DrawDischarge();return revived;
        }
        void PresentMeter(Camera head){if(head==null){meter.gameObject.SetActive(false);return;}Vector3 forward=Vector3.ProjectOnPlane(head.transform.forward,Vector3.up).normalized;if(forward.sqrMagnitude<.5f)forward=Vector3.forward;var rotation=Quaternion.LookRotation(forward,Vector3.up);meter.SetPositionAndRotation(head.transform.position+rotation*new Vector3(-.05f,-.875f,2),rotation);meter.localScale=Vector3.one*2.5f;meter.gameObject.SetActive(true);float progress=PresentedRemaining>0?PresentedRemaining/R24RevivePaddlesState.ReadyWindow:PresentedCharge;fill.localScale=new Vector3(.286f*Mathf.Max(.0001f,progress),.021f,.002f);fill.localPosition=new Vector3(-.143f*(1-progress),0,-.003f);fontMaterial.SetTexture("_MainTex",font.material.mainTexture);label.text=PresentedRemaining>0?"READY "+PresentedRemaining.ToString("F1")+"s":"CHARGE "+Mathf.RoundToInt(PresentedCharge*100)+"%";}
        readonly List<Vector3> vertices=new List<Vector3>();readonly List<Color> colors=new List<Color>();readonly List<int> indices=new List<int>();
        void Quad(Vector3 a,Vector3 b,Vector3 side,Color color){int start=vertices.Count;vertices.Add(a-side);vertices.Add(a+side);vertices.Add(b+side);vertices.Add(a-side);vertices.Add(b+side);vertices.Add(b-side);for(int i=0;i<6;i++){colors.Add(color);indices.Add(start+i);}}
        void Commit(Mesh mesh,MeshRenderer renderer){mesh.Clear();mesh.SetVertices(vertices);mesh.SetColors(colors);mesh.SetTriangles(indices,0);mesh.RecalculateBounds();renderer.enabled=vertices.Count>0;vertices.Clear();colors.Clear();indices.Clear();}
        void DrawCorona(){int tick=Mathf.FloorToInt(coronaAge*28);AppendCorona(leftArt,tick,false);AppendCorona(rightArt,tick,false);Commit(corona,coronaRenderer);}
        void AppendCorona(Transform paddle,int tick,bool local)
        {
            var rng=new System.Random(unchecked(tick*7919+paddle.GetEntityId().GetHashCode()));
            var side=(local?Vector3.up:paddle.up)*.0018f;
            for(int strand=0;strand<12;strand++){float phase=(float)rng.NextDouble()*Mathf.PI*2,sweep=(.7f+(float)rng.NextDouble()*.9f)*Mathf.PI;Vector3 previous=Vector3.zero;
                for(int j=0;j<=20;j++){float t=j/20f,a=phase+t*sweep,jitter=(float)rng.NextDouble()*.028f-.014f;Vector3 next=new Vector3(Mathf.Cos(a)*(.075f+jitter),Mathf.Sin(a)*(.10f+jitter),.02f-((float)rng.NextDouble()*.05f-.025f));
                    if(!local)next=paddle.TransformPoint(next);
                    if(j>0)Quad(previous,next,side,strand%4==0?Color.white:new Color(.282f,.729f,1));previous=next;
                }
            }
        }
        void DrawDischarge(){if(ends==null)return;float alpha=Mathf.Clamp01(fxRemaining/.2f);for(int path=0;path<3;path++){var start=ends[path*2];var finish=ends[path*2+1];var side=Vector3.Cross(Vector3.up,(finish-start).normalized).normalized;if(side.sqrMagnitude<.001f)side=Vector3.right;for(int strand=0;strand<5;strand++){var previous=start;for(int segment=1;segment<=12;segment++){float t=segment/12f,jitter=Mathf.Sin(t*81+strand*7+Mathf.Floor(fxAge*35)*11)*.045f*Mathf.Sin(t*Mathf.PI);var next=Vector3.Lerp(start,finish,t)+side*jitter+Vector3.up*Mathf.Cos(t*63+strand+Mathf.Floor(fxAge*35)*13)*.02f*Mathf.Sin(t*Mathf.PI);Quad(previous,next,side*(strand==0?.003f:.0015f),strand==0?new Color(1,1,1,alpha):new Color(.4f,.8f,1,alpha));previous=next;}}}Commit(discharge,dischargeRenderer);}
        void OnDisable(){Stow();}void OnDestroy(){Stow();if(art!=null)art.Dispose();if(audio!=null)audio.Dispose();if(corona!=null)Destroy(corona);if(discharge!=null)Destroy(discharge);if(worldFx!=null)Destroy(worldFx.gameObject);if(arcsMaterial!=null)Destroy(arcsMaterial);if(meterMaterial!=null)Destroy(meterMaterial);if(fontMaterial!=null)Destroy(fontMaterial);if(fill!=null)Destroy(fill.GetComponent<MeshRenderer>().sharedMaterial);}
    }
}
#endif
