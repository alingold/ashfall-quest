#if UNITY_EDITOR && UNITY_6000_0_OR_NEWER
using System;
using System.Collections;
using System.IO;
using Ashfall.Integration.Playable;
using Ashfall.Integration.Playable.Bots;
using Ashfall.Integration.Playable.HandsWeapons;
using Ashfall.Modules.Player;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEditor;

namespace Ashfall.Editor.Playable
{
    public static partial class R24Feedback6237Smoke
    {
        static IEnumerator LoadBotCombatMap(R23PlayableLoop loop)
        {
            loop.ReturnToLobby();loop.UI.OpenMaps();Need(loop.UI.ChooseMap("valley"),"Actual Valley card");float end=Time.realtimeSinceStartup+90;
            while(!loop.Ready&&loop.Failure==null&&Time.realtimeSinceStartup<end)yield return null;Need(loop.Ready&&loop.Map.MapId=="valley","Actual loaded Valley");loop.StartPlaying();loop.Gameplay.HolsterCurrent();loop.StepValleyBots(0,true);
            end=Time.realtimeSinceStartup+60;while(!loop.ValleyBots.Ready&&loop.ValleyBots.Failure==null&&Time.realtimeSinceStartup<end){loop.StepValleyBots(0,true);yield return null;}Need(loop.ValleyBots.Ready,"Authored live bot art/collision ready: "+loop.ValleyBots.Failure);
            for(int i=0;i<100;i++)loop.StepValleyBots(.02f,true);
        }
        static IEnumerator ValleyGunHit(R23PlayableLoop loop,Action<string> note,bool verifyDamageNumbers=false)
        {
            try
            {
                var load=LoadBotCombatMap(loop);while(load.MoveNext())yield return load.Current;
                var squad=loop.ValleyBots;R24KestrelBrain bot;Vector3 at;bool grounded;Need(squad.TryRead(0,out bot,out at,out grounded)&&grounded,"Actual supported patrol target");PlaceNearRealBot(loop,at);
                var controllers=squad.EncounterTransform.GetComponentsInChildren<CharacterController>();CharacterController target=null;foreach(var c in controllers)if(c.name=="KESTREL 0 / 0")target=c;Need(target!=null,"Actual live source bot body collider");
                string targetId=squad.TargetId(target);Need(targetId!=null,"Live semantic melee/explosive/body owner");
                // SetSpawn disarms input and the manual parent does not run Root
                // autonomously. Execute genuine neutral Root frames BEFORE equip.
                var neutral=new HandSample{Tracked=true};for(int i=0;i<8;i++){loop.Rig.SmokeTrackedHands(neutral,neutral);loop.Rig.SmokeXrMove(Vector2.zero,.02f);loop.SmokeUiUpdate();}
                string cx4=null;var owned=new System.Collections.Generic.List<string>();foreach(string id in loop.Gameplay.OwnedWeaponActionIds){var state=loop.Gameplay.ReadWeaponState(id);string profile=loop.Gameplay.ResolveWeaponProfile(id);owned.Add(id+":"+profile+" loaded="+state.Loaded);if(profile=="cx4")cx4=id;}
                var last=loop.Gameplay.BotProbeInput;note("BEFORE EQUIP owned="+string.Join(";",owned.ToArray())+" selected="+cx4+" ready="+loop.Gameplay.Ready+" hp="+loop.Gameplay.Health+" paused="+loop.Gameplay.BotProbePaused+" focus="+loop.Gameplay.BotProbeFocused+" snapshot="+loop.Gameplay.BotProbeHasInput+" rightTracked="+last.Right.Tracked+" rightReserved="+last.Right.Reserved+" RigReady="+loop.Rig.RightInputReady+" page="+loop.UI.Page);
                bool equipped=cx4!=null&&loop.Gameplay.EquipOwned(cx4,1);note("Actual selected CX4 EquipOwned="+equipped+" active="+loop.Gameplay.ActiveWeaponActionId+" hasWeapon="+loop.Gameplay.HasWeapon);Need(equipped,"Actual source-carried CX4, no grant");
                loop.Rig.Right.SetPositionAndRotation(loop.Rig.Head.transform.position+Vector3.right*.25f-Vector3.up*.25f,Quaternion.identity);loop.Rig.Left.position=loop.Rig.Head.transform.position-Vector3.right*.5f;
                R23ShotPose pose=new R23ShotPose();float damping=loop.Gameplay.MotionDamping;
                // Hold each supplied controller pose while the UNCHANGED source
                // aim filter settles. Repeated full-error rotations while it was
                // still lagging caused the old fixture's oscillating controller.
                int settleTicks=Mathf.Max(8,Mathf.CeilToInt((.006f+.09f*damping*damping)*8/.02f));
                for(int correction=0;correction<=6;correction++){
                    for(int i=0;i<settleTicks;i++)loop.StepItemInput(.02f,neutral,neutral,true,false);
                    loop.Gameplay.RefreshFeedbackPresentation();Need(loop.Gameplay.TryReadDisplayedShotPose(out pose),"Actual displayed CX4 pose");Vector3 direction=(target.bounds.center-V3(pose.Origin)).normalized;
                    note("Actual aim correction="+correction+" damping="+damping+" neutralTicks="+settleTicks+" origin="+V3(pose.Origin)+" forward="+V3(pose.Forward)+" targetCenter="+target.bounds.center+" angle="+Vector3.Angle(V3(pose.Forward),direction));
                    if(correction<6)loop.Rig.Right.rotation=Quaternion.FromToRotation(V3(pose.Forward),direction)*loop.Rig.Right.rotation;
                }
                float angular=Vector3.Angle(V3(pose.Forward),target.bounds.center-V3(pose.Origin));RaycastHit aimed;
                Need(target.Raycast(new Ray(V3(pose.Origin),V3(pose.Forward)),out aimed,180),"Actual displayed bore intersects intended source body before fire; angle="+angular);
                float beforeHealth=bot.Health;var before=loop.Gameplay.ReadWeaponState(cx4);int worldHits=0;Collider actualCollider=null;float damage=0;Action<Collider,float> hit=delegate(Collider c,float amount){worldHits++;actualCollider=c;damage=amount;};loop.Gameplay.WorldHit+=hit;
                bool accepted;try{var trigger=new HandSample{Tracked=true,Trigger=1};accepted=loop.StepItemInput(.001f,neutral,trigger,true,false);}finally{loop.Gameplay.WorldHit-=hit;}
                var after=loop.Gameplay.ReadWeaponState(cx4);Need(accepted&&after.Shots==before.Shots+1&&after.Loaded==before.Loaded-1,"Exactly one actual owned CX4 discharge/round");Need(worldHits==1&&actualCollider!=null&&squad.TargetId(actualCollider)==targetId,"Actual ballistic trace resolves live bot owner");Need(damage>0&&Mathf.Abs(beforeHealth-bot.Health-damage)<.0001f,"Root WorldHit applies exact source body damage once");Need(squad.EncounterActive,"Actual accepted gunshot wakes source encounter");
                note("Actual CX4 action="+cx4+" target="+targetId+" collider="+actualCollider.name+" nativeShots="+before.Shots+"->"+after.Shots+" loaded="+before.Loaded+"->"+after.Loaded+" botHP="+beforeHealth+"->"+bot.Health+" damage="+damage+" hits="+worldHits+" actualAimDegrees="+angular);
                if(verifyDamageNumbers){var proof=BotDamageNumberC1Proof(loop,damage,note);while(proof.MoveNext())yield return proof.Current;}
            }
            finally{loop.Gameplay.HolsterCurrent();loop.ReturnToLobby();}yield return null;
        }
        static IEnumerator ValleyBotOptics(R23PlayableLoop loop,Action<string> note)
        {
            var oldLayers=new System.Collections.Generic.Dictionary<Renderer,bool>();
            try
            {
                var load=LoadBotCombatMap(loop);while(load.MoveNext())yield return load.Current;
                Transform target=null;foreach(Transform child in loop.ValleyBots.EncounterTransform)if(child.name=="KESTREL 0 / 0")target=child;
                note("Actual encounter children="+loop.ValleyBots.EncounterTransform.childCount+" exactLiteralNameOwner="+(target==null?"NULL":target.name));Need(target!=null,"Real source patrol visual owner");foreach(var r in target.GetComponentsInChildren<Renderer>(true))oldLayers.Add(r,r.enabled);
                Camera spr=loop.Gameplay.BotProbeSprCamera,awp=loop.Gameplay.BotProbeAwpCamera;note("Actual typed camera owners SPR="+(spr==null?"NULL":spr.name)+" AWP="+(awp==null?"NULL":awp.name));Need(spr!=null&&awp!=null,"Actual existing SPR and AWP optical camera instances");
                string output=Path.Combine(Path.GetDirectoryName(R23PlayableBuild.Argument("-ashfallVRSmoke")),"valley-optics");Directory.CreateDirectory(output);
                foreach(var camera in new[]{spr,awp}){
                    Need((camera.cullingMask&(1<<R24ValleyBots.CombatLayer))!=0&&(camera.cullingMask&(1<<18))==0,"Actor role included while own gun/hand art excluded");
                    Vector3 position=camera.transform.position;Quaternion rotation=camera.transform.rotation;float fov=camera.fieldOfView;bool enabled=camera.enabled;var extra=camera.GetUniversalAdditionalCameraData();bool xr=extra.allowXRRendering;RenderTexture original=camera.targetTexture;
                    var image=RenderTexture.GetTemporary(768,768,24,RenderTextureFormat.ARGB32);Texture2D pixels=null;var previous=RenderTexture.active;
                    try{
                        camera.enabled=false;extra.allowXRRendering=false;camera.transform.SetPositionAndRotation(target.position+new Vector3(0,1.2f,3),Quaternion.LookRotation(new Vector3(0,-.2f,-3),Vector3.up));camera.fieldOfView=58;camera.targetTexture=image;
                        foreach(var pair in oldLayers)pair.Key.enabled=false;camera.Render();RenderTexture.active=image;pixels=new Texture2D(768,768,TextureFormat.RGB24,false);pixels.ReadPixels(new Rect(0,0,768,768),0,0);pixels.Apply();var control=pixels.GetPixels32();
                        foreach(var pair in oldLayers)pair.Key.enabled=pair.Value;camera.Render();RenderTexture.active=image;pixels.ReadPixels(new Rect(0,0,768,768),0,0);pixels.Apply();var actual=pixels.GetPixels32();int changed=0;for(int i=0;i<actual.Length;i++)if(Mathf.Abs(actual[i].r-control[i].r)+Mathf.Abs(actual[i].g-control[i].g)+Mathf.Abs(actual[i].b-control[i].b)>12)changed++;
                        string name=camera==spr?"spr-actor-unmatched.png":"awp-actor-unmatched.png";File.WriteAllBytes(Path.Combine(output,name),pixels.EncodeToPNG());Need(changed>100,"Actual optical camera renders actor pixels; changed="+changed);note("Actual camera="+camera.name+" mask="+camera.cullingMask+" actor-v-hidden pixelDifference="+changed+" sourceRTdepth="+image.depth+". Declared fixture pose/FOV only; no reticle/alignment/HMD proof.");
                        foreach(var pair in oldLayers)foreach(var material in pair.Key.sharedMaterials)if(material!=null)foreach(var error in ShaderUtil.GetShaderMessages(material.shader))Need(error.severity.ToString()!="Error",error.message);
                    }finally{foreach(var pair in oldLayers)if(pair.Key!=null)pair.Key.enabled=pair.Value;camera.transform.SetPositionAndRotation(position,rotation);camera.fieldOfView=fov;camera.enabled=enabled;extra.allowXRRendering=xr;camera.targetTexture=original;RenderTexture.active=previous;if(pixels!=null)UnityEngine.Object.Destroy(pixels);RenderTexture.ReleaseTemporary(image);}
                }
            }
            finally{foreach(var pair in oldLayers)if(pair.Key!=null)pair.Key.enabled=pair.Value;loop.ReturnToLobby();}yield return null;
        }
    }
}
#endif
