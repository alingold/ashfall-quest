#if UNITY_EDITOR && UNITY_6000_0_OR_NEWER
using System;
using System.Collections;
using Ashfall.Core;
using Ashfall.Modules.Player;
using Ashfall.Integration.Playable;
using Ashfall.Integration.Playable.Bots;
using Ashfall.Integration.Playable.UI;
using UnityEngine;
using UnityEditor;
namespace Ashfall.Editor.Playable
{
    public static partial class R24Feedback6237Smoke
    {
        static IEnumerator ValleyMissions(R23PlayableLoop loop,Action<string> note)
        {
            const string key="ashfall.unity.relay-valley.checkpoint.v1";bool had=PlayerPrefs.HasKey(key);string saved=PlayerPrefs.GetString(key,"");
            var original=loop.ValleyBots.Mission;int oldChapter=original.Chapter,oldKills=original.Kills;double oldTime=original.PlaySeconds;
            try
            {
                loop.ReturnToLobby();loop.UI.OpenMaps();Need(loop.UI.ChooseMap("valley"),"Actual Valley card");float until=Time.realtimeSinceStartup+90;while(!loop.Ready&&loop.Failure==null&&Time.realtimeSinceStartup<until)yield return null;Need(loop.Ready&&loop.Map.MapId=="valley","Actual Valley loaded");
                loop.ReturnToLobby();Pointer(loop.UI,Button(loop.UI,"NEW CAMPAIGN"),false);Need(loop.ValleyBots.Mission.Chapter==0&&!loop.Lobby.Visible,"Actual new-campaign UI starts source checkpoint");
                var bots=loop.ValleyBots;int[] expected={4,6,8,6};string carried=String.Join("|",loop.Gameplay.OwnedWeaponActionIds);int capacity=loop.InventoryCapacity;
                for(int chapter=0;chapter<4;chapter++)
                {
                    until=Time.realtimeSinceStartup+40;loop.StepValleyBots(0,true);while(!bots.Ready&&bots.Failure==null&&Time.realtimeSinceStartup<until){loop.StepValleyBots(0,true);yield return null;}
                    Need(bots.Ready&&bots.Mission.Chapter==chapter&&bots.Count==expected[chapter]&&bots.LivingCount==expected[chapter],"Actual authored mission/wave "+chapter);
                    Vector3 supported;Need(ClearBotPlayerSpot(loop,loop.Map.transform.TransformPoint(V3(bots.Mission.Checkpoint)),out supported),"Source checkpoint has actual supported/head-clear floor "+chapter);note("Source checkpoint="+bots.Mission.Checkpoint+" actual supported="+supported);
                    Need(bots.EncounterActive==(chapter>0),"Source chapter alert gate "+chapter);Vector3 screen=loop.Map.transform.TransformPoint(V3(bots.Mission.Screen));string id;Vector3 point;float distance;Need(!loop.ValleyMission.TryTarget(screen,out id,out point,out distance),"Security alive refuses station "+chapter);
                    foreach(var renderer in bots.EncounterTransform.GetComponentsInChildren<Renderer>(true))foreach(var material in renderer.sharedMaterials)if(material!=null)foreach(var message in ShaderUtil.GetShaderMessages(material.shader))Need(message.severity.ToString()!="Error",message.message);
                    for(int i=0;i<expected[chapter];i++){
                        R24KestrelBrain brain;Vector3 position;bool grounded;Need(bots.TryRead(i,out brain,out position,out grounded)&&brain.Variant==bots.Mission.VariantAt(i)&&Vector3.Distance(position,loop.Map.transform.TransformPoint(V3(bots.Mission.SpawnAt(i))))<.001f,"Actual source spawn "+chapter+"/"+i);
                        var collider=FindValleyBody(bots,brain.Variant,i);Need(collider!=null,"Actual live actor collider");if(brain.Variant==3){Need(Mathf.Abs(collider.radius-.85f)<.00001f&&Mathf.Abs(collider.height-2.2f)<.00001f&&Mathf.Abs(collider.center.y-1.1f)<.00001f,"Actual source Warden capsule");var warden=bots.EncounterTransform.GetComponentInChildren<R24WardenVisual>(true);Need(warden!=null&&warden.Ready,"Actual Warden visual constructed");var marker=(Transform)Read(warden,"marker");Need(marker!=null&&marker.parent==null&&marker.GetComponentsInChildren<Collider>(true).Length==0,"Source top-level marker owns no collider/actor scale");var markerRenderers=marker.GetComponentsInChildren<Renderer>(true);Need(markerRenderers.Length>0,"Actual marker render meshes");foreach(var renderer in markerRenderers)Need(renderer.gameObject.layer==R24ValleyBots.CombatLayer,"Detached warning uses published actor rendering role");}string target=bots.TargetId(collider);float before=brain.Health;Need(loop.ApplyTargetDamage(target,20000,new Float3(0,1,-8))&&brain.State==R24KestrelState.Dead,"Actual Root target routing clears mission actor "+chapter+"/"+i);note("Actual mission="+chapter+" slot="+i+" variant="+brain.Variant+" sourcePos="+position+" health="+before+"->"+brain.Health);
                    }
                    Need(bots.LivingCount==0,"All actual chapter actors retired");
                    Vector3 spot=screen+Vector3.forward*1.2f;Need(ClearBotPlayerSpot(loop,spot,out spot),"Actual supported/head-clear station approach "+chapter);loop.Rig.SetSpawn(spot,screen);loop.Gameplay.HolsterCurrent();loop.UI.SetPlaying();
                    int hand=chapter%2;Transform actual=hand==0?loop.Rig.Left:loop.Rig.Right;Vector3 beforeHead=loop.Rig.Head.transform.position;actual.position=screen+Vector3.forward*.02f;Physics.SyncTransforms();
                    Need(loop.ValleyMission.TryTarget(actual.position,out id,out point,out distance)&&distance<.03f,"Actual source exposed screen/LOS "+chapter);
                    var neutral=new HandSample{Tracked=true};loop.PrepareObjectiveGrips(neutral,neutral,true,true,true);var pressed=new HandSample{Tracked=true,Grip=1};loop.PrepareObjectiveGrips(hand==0?pressed:neutral,hand==1?pressed:neutral,true,true,true);
                    Need(bots.Mission.Chapter==chapter+1&&loop.ValleyMission.SuccessfulActivations==chapter+1,"Fresh actual closest-objective grip advances exactly once "+chapter);Need(Vector3.Distance(loop.Rig.Head.transform.position,beforeHead)<.001f,"Advancing checkpoint does not teleport player");Need(String.Join("|",loop.Gameplay.OwnedWeaponActionIds)==carried&&loop.InventoryCapacity==capacity,"Station advance preserves carried identities/capacity without regrant");Need(loop.Paddles==null||!loop.Paddles.State.Active,"Source begin-chapter stows revive paddles");
                    loop.PrepareObjectiveGrips(hand==0?pressed:neutral,hand==1?pressed:neutral,true,true,true);Need(bots.Mission.Chapter==chapter+1,"Held grip cannot cascade");Need(loop.ValleyMission.HasCheckpoint&&loop.ValleyMission.SaveFailure==null,"Actual Unity campaign checkpoint saved");
                    if(chapter==1){int kills=bots.Mission.Kills;double seconds=bots.Mission.PlaySeconds;loop.ReturnToLobby();Pointer(loop.UI,Button(loop.UI,"CONTINUE"),false);Need(bots.Mission.Chapter==2&&bots.Mission.Kills==kills&&Math.Abs(bots.Mission.PlaySeconds-seconds)<.00001,"Actual Continue restores source campaign progress");Need(Vector3.Distance(loop.Rig.transform.position,loop.Map.transform.TransformPoint(V3(bots.Mission.Checkpoint)))<.001f,"Actual Continue places player at source checkpoint");note("Actual Continue UI chapter="+bots.Mission.Chapter+" kills="+kills+" seconds="+seconds+" root="+loop.Rig.transform.position);}
                }
                Need(bots.Mission.Complete&&bots.Mission.Kills==24&&bots.Count==0,"Full normal source campaign ends after24 authored actors");loop.StepValleyBots(.02f,true);Need(loop.Soundscape!=null,"Existing source score owner retained");
                var extraction=loop.ValleyMission.Extraction;Need(extraction!=null&&extraction.Ready,"Actual source extraction owner ready: "+(extraction==null?"not created":extraction.Failure));Need(extraction.BoxCount==48&&extraction.TriangleCount==604&&extraction.RendererCount==13,"Actual source craft mesh counts/batching");Need(extraction.CraftRoot.GetComponentsInChildren<Collider>(true).Length==0,"Source rescue craft adds no collider");
                var borrowed=new System.Collections.Generic.List<Material>();foreach(string keyName in R24ValleyExtractionRecipeKeys()){var material=extraction.SourceMaterial(keyName);Material mapMaterial;Need(material!=null&&loop.Map.TryReadSourceMaterial(material.name,out mapMaterial)&&ReferenceEquals(mapMaterial,material),"Reuse exact map-owned source material "+keyName);borrowed.Add(material);}
                var resources=new System.Collections.Generic.List<UnityEngine.Object>();foreach(UnityEngine.Object value in (IEnumerable)Read(extraction,"owned"))resources.Add(value);
                foreach(var renderer in extraction.CraftRoot.GetComponentsInChildren<Renderer>(true))foreach(var message in ShaderUtil.GetShaderMessages(renderer.sharedMaterial.shader))Need(message.severity.ToString()!="Error",message.message);
                double arrivalBefore=extraction.State.Elapsed;for(int frame=0;frame<52;frame++){loop.StepValleyBots(.25f,false);yield return null;}Need(extraction.State.Landed&&Math.Abs(extraction.State.Elapsed-arrivalBefore-13)<.00001,"Actual world arrival advances13s independent of paused gameplay simulation");var expectedPose=extraction.State.Position;Need(Vector3.Distance(extraction.CraftRoot.localPosition,V3(expectedPose))<.00001f,"Actual craft transform follows source arrival/hover pose");Need(extraction.CraftRoot.GetComponentsInChildren<Light>(true).Length==3,"Source two searchlights/one beacon");note("Actual source rescue craft48boxes/28hulltris/13renderers/3lights/map-material reuse/13s arrival; rendered appearance and radiometry remain unqualified.");note("Actual four source waves/station screen fresh-grip progression/end/checkpoint path. Damage clearing uses actual Root damage API; this does not prove physical gun/melee or Warden attacks. Separate Warden combat/appearance/floors and extraction craft remain unqualified.");
                loop.ReturnToLobby();yield return null;Need(!loop.ValleyMission.Visible&&bots.Count==0,"Actual Home withdraws mission station/actors");Need(loop.ValleyMission.Extraction==null,"Actual Home releases extraction owner");foreach(var material in borrowed)Need(material!=null,"Home must preserve borrowed map material");foreach(var resource in resources)Need(resource==null,"Owned extraction mesh/hull material released");
            }
            finally{loop.ReturnToLobby();original.Restore(oldChapter,oldKills,oldTime);if(had)PlayerPrefs.SetString(key,saved);else PlayerPrefs.DeleteKey(key);PlayerPrefs.Save();}
        }
        static string[] R24ValleyExtractionRecipeKeys(){return new[]{"white","glass","trim","orange","steel","black","yellow","cyan","red"};}
        static CharacterController FindValleyBody(R24ValleyBots bots,int variant,int slot)
        {foreach(var collider in bots.EncounterTransform.GetComponentsInChildren<CharacterController>())if(collider.name=="KESTREL "+variant+" / "+slot)return collider;return null;}
    }
}
#endif
