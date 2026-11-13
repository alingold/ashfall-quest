#if UNITY_EDITOR && UNITY_6000_0_OR_NEWER
using System;
using System.Collections;
using System.Collections.Generic;
using Ashfall.Modules.WeaponHandling;
using UnityEngine;
using UnityEngine.Rendering;
namespace Ashfall.Integration.Playable.HandsWeapons
{
    // Caller owns the existing manual smoke: stop its normal rig/input pump first.
    // Requires a held weapon, owned/chambered SPR and a clear metre in front of the head.
    // Runs only when explicitly iterated by an editor smoke. No grants/reset, HMD writes or file IO.
    public static class R23SprNativeSmoke
    {
        public sealed class Result
        {
            public bool Passed,Equipped,Aligned,EyeTargets,ReticleRendered,OffAxisBlocked,PausedBlocked,FocusBlocked,Holstered,AmmoPreserved;
            public int LeftFrames,RightFrames,ProbeFrames,ReticlePixels;
            public string Error="";
        }
        public static IEnumerator Run(R23HandsWeapons gameplay,Camera head,Transform left,Transform right,Result result,Action<RenderTexture,string> capture=null,int primaryHand=1) {
            if(result==null)throw new ArgumentNullException("result");
            var runner=new Runner(gameplay,head,left,right,result,capture,primaryHand);var work=runner.Execute();
            while(true) {
                bool more=false;object current=null;
                try { more=work.MoveNext();if(more)current=work.Current; }
                catch(Exception error) { result.Error=error.ToString();result.Passed=false; }
                if(!more)break;yield return current;
            }
            var disposable=work as IDisposable;if(disposable!=null)disposable.Dispose();
        }
        sealed class Runner
        {
            readonly R23HandsWeapons gameplay;readonly Camera head;readonly Transform left,right;readonly Result result;readonly Action<RenderTexture,string> capture;readonly int hand;
            readonly Dictionary<string,HandlingSnapshot> before=new Dictionary<string,HandlingSnapshot>();readonly Dictionary<string,int> reserves=new Dictionary<string,int>();
            R23SprScope scope;Camera probe;RenderTexture probeImage;int oldLayer;bool prepared,oldExternal;string oldWeapon;int oldHand;Vector3 leftPosition,rightPosition;Quaternion leftRotation,rightRotation;
            public Runner(R23HandsWeapons g,Camera h,Transform l,Transform r,Result result,Action<RenderTexture,string> capture,int hand){gameplay=g;head=h;left=l;right=r;this.result=result;this.capture=capture;this.hand=hand;}
            static void Need(bool value,string message){if(!value)throw new InvalidOperationException("SPR native smoke: "+message);}
            R23HandsInputSnapshot Neutral { get { return new R23HandsInputSnapshot(new R23HandInput(true,false,0,0),new R23HandInput(true,false,0,0)); } }
            Transform Primary { get { return hand==0?left:right; } }
            void Tick(){gameplay.TickInput(0,Neutral);Physics.SyncTransforms();scope.RefreshFixture();}
            void Rendered(ScriptableRenderContext context,Camera camera){int eye=scope.FixtureEyeIndex(camera);if(eye==0)result.LeftFrames++;if(eye==1)result.RightFrames++;if(camera==probe)result.ProbeFrames++;}
            bool CamerasOff { get { return !scope.Active&&!scope.FixtureLensVisible&&!scope.FixtureCamera(0).enabled&&!scope.FixtureCamera(1).enabled; } }
            void Unchanged(){foreach(var pair in before){var after=gameplay.ReadWeaponState(pair.Key);var initial=pair.Value;Need(after.Loaded==initial.Loaded&&after.Seated==initial.Seated&&after.Chambered==initial.Chambered&&after.Phase==initial.Phase&&after.Shots==initial.Shots&&gameplay.ReadReserve(pair.Key)==reserves[pair.Key],"ammo/chamber/stage changed for "+pair.Key);}}
            static Color32[] Pixels(RenderTexture target) {
                RenderTexture active=RenderTexture.active;Texture2D image=null;
                try{RenderTexture.active=target;image=new Texture2D(target.width,target.height,TextureFormat.RGBA32,false);image.ReadPixels(new Rect(0,0,target.width,target.height),0,0,false);image.Apply(false);return image.GetPixels32();}
                finally{RenderTexture.active=active;if(image!=null)UnityEngine.Object.Destroy(image);}
            }
            public IEnumerator Execute() {
                Need(gameplay!=null&&head!=null&&left!=null&&right!=null&&hand>=0&&hand<=1,"existing gameplay/head/two hands required");
                Need(gameplay.Ready&&gameplay.HasWeapon&&gameplay.OwnsWeapon("spr")&&!gameplay.FixturePaused&&gameplay.FixtureFocused,"begin in active manual smoke with a weapon held and SPR owned");
                Need(gameplay.ReadWeaponState("spr").Phase==ReloadPhase.Ready,"SPR must start in a completed reload stage");
                scope=gameplay.SprScopeFixture;Need(scope!=null,"source scope component missing");
                oldWeapon=gameplay.ActiveWeaponId;oldHand=gameplay.PrimaryHand;oldExternal=gameplay.ExternalInputPump;oldLayer=scope.FixtureLensLayer;leftPosition=left.position;rightPosition=right.position;leftRotation=left.rotation;rightRotation=right.rotation;
                foreach(string id in gameplay.AvailableWeaponIds){before.Add(id,gameplay.ReadWeaponState(id));reserves.Add(id,gameplay.ReadReserve(id));}
                prepared=true;
                try {
                    gameplay.ExternalInputPump=true;gameplay.TickInput(0,Neutral);Need(gameplay.EquipOwned("spr",hand),"actual source SPR equip failed");result.Equipped=gameplay.ActiveWeaponId=="spr"&&gameplay.PrimaryHand==hand;
                    Primary.rotation=head.transform.rotation;Vector3 offset=scope.FixtureLensPosition-Primary.position;Primary.position=head.transform.position+head.transform.forward*.20f-offset;
                    (hand==0?right:left).position=head.transform.position-head.transform.up*.5f+head.transform.right*(hand==0?.4f:-.4f);Tick();Need(scope.Active,"source eye-relief/alignment/occlusion gate did not open");result.Aligned=true;
                    Need(scope.FixtureShaderSupported,"lens shader unsupported or failed to compile");
                    // Isolate the real aperture for a small GPU reticle probe; restore its layer below.
                    scope.FixtureLensLayer=31;probeImage=new RenderTexture(520,307,24,RenderTextureFormat.ARGB32);probeImage.name="SPR native smoke aperture capture";probeImage.Create();
                    probe=new GameObject("SPR editor smoke aperture camera").AddComponent<Camera>();probe.stereoTargetEye=StereoTargetEyeMask.None;probe.targetTexture=probeImage;probe.cullingMask=1<<31;probe.clearFlags=CameraClearFlags.SolidColor;probe.backgroundColor=Color.black;probe.nearClipPlane=.01f;probe.farClipPlane=3;probe.depth=head.depth-5;probe.allowHDR=false;probe.allowMSAA=false;
                    probe.transform.position=head.stereoEnabled?(Vector3)head.GetStereoViewMatrix(Camera.StereoscopicEye.Left).inverse.GetColumn(3):head.transform.position;probe.transform.LookAt(scope.FixtureLensPosition,head.transform.up);
                    probe.fieldOfView=2*Mathf.Atan(scope.FixtureLensScale.y*.55f/Vector3.Distance(probe.transform.position,scope.FixtureLensPosition))*Mathf.Rad2Deg;
                    RenderPipelineManager.endCameraRendering+=Rendered;
                    for(int frame=0;frame<30&&(result.LeftFrames<2||result.RightFrames<2||result.ProbeFrames<2);frame++){Tick();yield return null;}
                    var l=scope.FixtureCamera(0);var r=scope.FixtureCamera(1);var lt=scope.FixtureImage(0);var rt=scope.FixtureImage(1);
                    result.EyeTargets=result.LeftFrames>=2&&result.RightFrames>=2&&lt!=rt&&lt.IsCreated()&&rt.IsCreated()&&l.targetTexture==lt&&r.targetTexture==rt&&lt.width==520&&lt.height==307&&rt.width==520&&rt.height==307&&l.stereoTargetEye==StereoTargetEyeMask.None&&r.stereoTargetEye==StereoTargetEyeMask.None;
                    Need(result.EyeTargets,"distinct per-eye source cameras/targets did not render");Need(Pixels(lt).Length==520*307&&Pixels(rt).Length==520*307,"optical targets could not be read back");
                    foreach(var pixel in Pixels(probeImage))if(pixel.r>100&&pixel.r>pixel.g*1.8f&&pixel.r>pixel.b*1.5f)result.ReticlePixels++;
                    result.ReticleRendered=result.ProbeFrames>=2&&result.ReticlePixels>2;Need(result.ReticleRendered,"real aperture did not render its red source reticle");
                    if(capture!=null){capture(lt,"spr-left-optical");capture(rt,"spr-right-optical");capture(probeImage,"spr-aperture-reticle");}
                    Vector3 aligned=Primary.position;Primary.position+=head.transform.right*.20f;Tick();yield return null;result.OffAxisBlocked=CamerasOff;Need(result.OffAxisBlocked,"off-axis lens retained active cameras");Primary.position=aligned;Tick();
                    gameplay.SetGameplayPaused(true);scope.RefreshFixture();yield return null;result.PausedBlocked=CamerasOff&&!gameplay.Fire(.1f);Need(result.PausedBlocked,"paused SPR scope/fire remained active");Unchanged();
                    gameplay.SetGameplayPaused(false);gameplay.NotifyFocus(false);scope.RefreshFixture();yield return null;result.FocusBlocked=CamerasOff&&!gameplay.Fire(.1f);Need(result.FocusBlocked,"unfocused scope/fire remained active");
                    gameplay.NotifyFocus(true);Tick();Need(gameplay.HolsterCurrent(),"holster failed");scope.RefreshFixture();yield return null;result.Holstered=CamerasOff&&!gameplay.Fire(.1f);Need(result.Holstered,"holstered scope/fire remained active");Unchanged();result.AmmoPreserved=true;result.Passed=true;
                }
                finally {
                    RenderPipelineManager.endCameraRendering-=Rendered;
                    if(prepared){scope.FixtureLensLayer=oldLayer;gameplay.NotifyFocus(true);gameplay.SetGameplayPaused(false);gameplay.TickInput(0,Neutral);gameplay.EquipOwned(oldWeapon,oldHand);left.position=leftPosition;left.rotation=leftRotation;right.position=rightPosition;right.rotation=rightRotation;gameplay.ExternalInputPump=oldExternal;Physics.SyncTransforms();scope.RefreshFixture();}
                    if(probe!=null){probe.enabled=false;UnityEngine.Object.Destroy(probe.gameObject);}if(probeImage!=null){probeImage.Release();UnityEngine.Object.Destroy(probeImage);}
                }
            }
        }
    }
}
#endif
