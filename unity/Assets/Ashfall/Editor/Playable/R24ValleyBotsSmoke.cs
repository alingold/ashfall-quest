#if UNITY_EDITOR && UNITY_6000_0_OR_NEWER
using System;
using System.Collections;
using System.IO;
using Ashfall.Core;
using Ashfall.Integration.Playable;
using Ashfall.Integration.Playable.Bots;
using Ashfall.Integration.Playable.Explosives;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEditor;

namespace Ashfall.Editor.Playable
{
    public static partial class R24Feedback6237Smoke
    {
        static IEnumerator ValleyBots(R23PlayableLoop loop,Action<string> note)
        {
            GameObject cover=null;var logs=new System.Collections.Generic.List<R24KestrelDischarge>();
            string output=Path.Combine(Path.GetDirectoryName(R23PlayableBuild.Argument("-ashfallVRSmoke")),"valley-bots");Directory.CreateDirectory(output);
            try
            {
                loop.ReturnToLobby();loop.UI.OpenMaps();Need(loop.UI.ChooseMap("valley"),"Actual Valley map card");float until=Time.realtimeSinceStartup+90;
                while(!loop.Ready&&loop.Failure==null&&Time.realtimeSinceStartup<until)yield return null;Need(loop.Ready&&loop.Map.IsLoaded&&loop.Map.MapId=="valley","Actual qualified Valley loader");
                loop.StartPlaying();loop.Gameplay.HolsterCurrent();loop.StepValleyBots(0,true);until=Time.realtimeSinceStartup+60;
                while(!loop.ValleyBots.Ready&&loop.ValleyBots.Failure==null&&Time.realtimeSinceStartup<until){loop.StepValleyBots(0,true);yield return null;}
                var squad=loop.ValleyBots;Need(squad.Ready&&squad.Failure==null&&squad.Count==4&&squad.LivingCount==4,"Actual authored four source bots loaded");
                int[] variants={0,1,2,0};Vector3[] local={new Vector3(-8,.2f,13),new Vector3(8,.2f,23),new Vector3(-3,1.2f,15),new Vector3(3,1.2f,17)};
                for(int i=0;i<4;i++){R24KestrelBrain b;Vector3 p;bool grounded;Need(squad.TryRead(i,out b,out p,out grounded)&&b.Variant==variants[i]&&b.Health==b.MaximumHealth&&Vector3.Distance(p,loop.Map.transform.TransformPoint(local[i]))<.001f,"Exact source spawn/variant/health "+i);}
                Need(!squad.EncounterActive,"Source first encounter dormant before z gate");Need(QualitySettings.skinWeights==SkinWeights.Unlimited,"Actual eight-influence authored soldier rendering");
                for(int i=0;i<100;i++)loop.StepValleyBots(.02f,true);
                for(int i=0;i<4;i++){R24KestrelBrain b;Vector3 p;bool grounded;squad.TryRead(i,out b,out p,out grounded);note("Source bot="+i+" variant="+b.Variant+" pos="+p.ToString("F4")+" grounded="+grounded+" health="+b.Health+" state="+b.State);if(i!=1)Need(grounded,"Actual source ground capsule supported "+i);else Need(p.y>loop.Map.transform.TransformPoint(local[i]).y+.5f,"Actual source Scout hover");}
                Need(!squad.EncounterActive&&squad.Shots==0,"Dormant encounter cannot attack through gate");
                CaptureValleyBots(loop,output,"opening-unmatched.png",loop.Rig.Head.transform.position,loop.Map.transform.TransformPoint(new Vector3(0,1.5f,18)));
                foreach(var renderer in squad.EncounterTransform.GetComponentsInChildren<Renderer>(true))foreach(var material in renderer.sharedMaterials)if(material!=null)foreach(var message in ShaderUtil.GetShaderMessages(material.shader))Need(message.severity.ToString()!="Error",message.message);
                // Retire the three other real guards through the existing explosive
                // effect router, not by changing AI state/health or fake hit flags.
                for(int i=1;i<4;i++){R24KestrelBrain b;Vector3 p;bool grounded;squad.TryRead(i,out b,out p,out grounded);var blast=new R23GrenadeBlast("native-valley-finite/"+i,"frag",i,F3(p+Vector3.up*.7f),.25f,b.MaximumHealth,false);Need(blast.Apply(loop,F3(loop.Rig.Head.transform.position))==1,"Actual Root explosive target routing "+i);Need(b.State==R24KestrelState.Dead,"Real source health terminal "+i);}
                Need(squad.LivingCount==1,"Exactly one real bot remains for isolated discharge controls");
                R24KestrelBrain first;Vector3 at;bool floor;squad.TryRead(0,out first,out at,out floor);
                PlaceNearRealBot(loop,at);loop.StepValleyBots(.02f,true);Need(squad.EncounterActive,"Actual source z gate/encounter activation");
                for(int i=0;i<450&&first.State!=R24KestrelState.Windup;i++){loop.StepValleyBots(.02f,true);if(i%25==0)yield return null;}
                Need(first.State==R24KestrelState.Windup&&first.HasSight,"Actual source FOV+LOS+illumination reaches telegraph");
                while(first.StateTime<first.WindupSeconds-first.FixedDodgeSeconds+.04f)loop.StepValleyBots(.02f,true);
                Vector3 locked=V3(first.LockedAim);Vector3 feet=loop.Rig.transform.position;Vector3 lateral=Vector3.Cross(Vector3.up,locked-at).normalized;
                bool moved=false;foreach(float side in new[]{1f,-1f}){Vector3 candidate=feet+lateral*side*1.3f;if(!ClearBotPlayerSpot(loop,candidate,out candidate))continue;loop.Rig.SetSpawn(candidate,at);moved=true;break;}Need(moved,"Real floor/head-clear late-dodge control");
                Need(Vector3.Distance(loop.Rig.Head.transform.position,locked)>.6f,"Actual HMD pose moved outside source .42 hit radius");
                for(int i=0;i<100&&squad.ReadDischarge(0)==null;i++)loop.StepValleyBots(.02f,true);
                var dodge=squad.ReadDischarge(0);Need(dodge!=null&&dodge.inRange&&!dodge.queryOverflow&&Vector3.Distance(dodge.lockedAim,locked)<.001f&&dodge.appliedPlayerDamage==0,"Actual fixed warning shot does not follow late dodge");logs.Add(dodge);note("Actual late-dodge discharge="+JsonUtility.ToJson(dodge));
                // A finite declared world BoxCollider is inserted after aim locks.
                // This proves the real obstruction query, not all construction/maps.
                for(int i=0;i<500&&!(first.State==R24KestrelState.Windup&&first.StateTime>first.WindupSeconds-first.FixedDodgeSeconds+.02f);i++){loop.StepValleyBots(.02f,true);if(i%25==0)yield return null;}
                Need(first.State==R24KestrelState.Windup,"Second actual warning reached");
                var visual=squad.EncounterTransform.GetComponentsInChildren<R24KestrelVisual>();R24KestrelVisual firstVisual=null;foreach(var v in visual)if(v.transform.parent!=null&&v.transform.parent.name=="KESTREL 0 / 0")firstVisual=v;Need(firstVisual!=null&&firstVisual.Muzzle!=null,"Actual authored muzzle owner");
                locked=V3(first.LockedAim);Vector3 from=firstVisual.Muzzle.position;cover=new GameObject("DECLARED native Valley world cover");cover.layer=0;cover.transform.SetPositionAndRotation((from+locked)*.5f,Quaternion.LookRotation(locked-from,Vector3.up));cover.AddComponent<BoxCollider>().size=new Vector3(1.5f,1.5f,.12f);Physics.SyncTransforms();
                int previous=dodge.sequence;for(int i=0;i<100&&(squad.ReadDischarge(0)==null||squad.ReadDischarge(0).sequence==previous);i++)loop.StepValleyBots(.02f,true);
                var blocked=squad.ReadDischarge(0);Need(blocked!=null&&blocked.sequence>previous&&!blocked.clear&&blocked.obstruction==cover.name&&blocked.appliedPlayerDamage==0&&!blocked.queryOverflow,"Actual world cover absorbs locked shot");logs.Add(blocked);note("Actual declared-cover discharge="+JsonUtility.ToJson(blocked));UnityEngine.Object.DestroyImmediate(cover);cover=null;Physics.SyncTransforms();
                previous=blocked.sequence;for(int i=0;i<600&&(squad.ReadDischarge(0)==null||squad.ReadDischarge(0).sequence==previous);i++){loop.StepValleyBots(.02f,true);if(i%25==0)yield return null;}
                var hit=squad.ReadDischarge(0);Need(hit!=null&&hit.sequence>previous&&hit.clear&&!hit.guarded&&hit.appliedPlayerDamage==12,"Actual stationary source shot applies twelve shield-first damage");logs.Add(hit);note("Actual stationary discharge="+JsonUtility.ToJson(hit));
                CaptureValleyBots(loop,output,"patrol-close-unmatched.png",loop.Rig.Head.transform.position,at+Vector3.up*1.1f);
                squad.TryRead(0,out first,out at,out floor);var lethal=new R23GrenadeBlast("native-valley-final","frag",4,F3(at+Vector3.up*.7f),.25f,first.MaximumHealth,false);Need(lethal.Apply(loop,F3(loop.Rig.Head.transform.position))==1&&squad.LivingCount==0,"Actual final grenade effect retires source bot");
                int generation=squad.Generation;loop.RestartRound();loop.StepValleyBots(0,true);until=Time.realtimeSinceStartup+30;while(!squad.Ready&&squad.Failure==null&&Time.realtimeSinceStartup<until){loop.StepValleyBots(0,true);yield return null;}Need(squad.Ready&&squad.Generation==generation+1&&squad.Count==4&&squad.LivingCount==4&&!squad.EncounterActive,"Actual restart recreates one full source encounter without regrant/duplicate guards");
                loop.ReturnToLobby();yield return null;Need(squad.Count==0&&!squad.Ready,"Actual map withdrawal retires all body/head/art/shot ownership");
                note("Actual Valley first encounter only: four source variants/poses, three ground supports, Scout hover, dormant gate, Root explosive routing, late dodge, finite world cover, stationary hit, restart and leave. No later waves/routes/boss/ragdoll/construction-damage/physical firearm/melee/HMD/network acceptance.");
            }
            finally{if(cover!=null)UnityEngine.Object.Destroy(cover);loop.ReturnToLobby();File.WriteAllText(Path.Combine(output,"discharges.json"),JsonUtility.ToJson(new BotDischarges{cases=logs.ToArray()},true));}
        }
        [Serializable]sealed class BotDischarges{public R24KestrelDischarge[] cases;}
        static Float3 F3(Vector3 p){return new Float3(p.x,p.y,p.z);}
        static Vector3 V3(Float3 p){return new Vector3(p.X,p.Y,p.Z);}
        static bool ClearBotPlayerSpot(R23PlayableLoop loop,Vector3 candidate,out Vector3 supported)
        {
            supported=candidate;RaycastHit ground;if(!Physics.Raycast(candidate+Vector3.up*2,Vector3.down,out ground,4,(1<<0)|(1<<8),QueryTriggerInteraction.Ignore)||ground.normal.y<.7f)return false;
            supported=ground.point+Vector3.up*.04f;return loop.Rig.HeadCollision.Clear(supported+Vector3.up*1.7088f,Vector3.zero);
        }
        static void PlaceNearRealBot(R23PlayableLoop loop,Vector3 bot)
        {
            for(int i=0;i<16;i++){float a=i*Mathf.PI/8;Vector3 spot=bot+new Vector3(Mathf.Sin(a),0,Mathf.Cos(a))*2.8f;if(!ClearBotPlayerSpot(loop,spot,out spot))continue;
                if(Physics.Linecast(bot+Vector3.up*1.4f,spot+Vector3.up*1.7088f,(1<<0)|(1<<8)|(1<<19),QueryTriggerInteraction.Ignore))continue;loop.Rig.SetSpawn(spot,bot);Physics.SyncTransforms();return;}
            throw new InvalidOperationException("No actual supported/clear near-bot observation control");
        }
        static void CaptureValleyBots(R23PlayableLoop loop,string output,string file,Vector3 eye,Vector3 target)
        {
            var go=new GameObject("UNMATCHED actual Valley encounter camera");try{var camera=go.AddComponent<Camera>();camera.enabled=false;camera.GetUniversalAdditionalCameraData().allowXRRendering=false;camera.fieldOfView=58;camera.nearClipPlane=.05f;camera.farClipPlane=250;camera.cullingMask=(1<<0)|(1<<R24ValleyBots.CombatLayer);camera.transform.SetPositionAndRotation(eye,Quaternion.LookRotation(target-eye,Vector3.up));CaptureTacticalLandscape(camera,Path.Combine(output,file));}finally{UnityEngine.Object.Destroy(go);}
        }
    }
}
#endif
