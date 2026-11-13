#if UNITY_EDITOR && UNITY_6000_0_OR_NEWER
using System;
using System.Collections;
using UnityEngine;
using Ashfall.Integration.Playable;
using Ashfall.Integration.Playable.HandsWeapons;
using Ashfall.Integration.Playable.World;
namespace Ashfall.Editor.Playable
{
    // A coordinated, XR-disabled native entry test. Runs the actual map loader,
    // source menu spawn callbacks and player collision; never supplies a floor.
    public sealed class R23MapEntrySmoke
    {
        [Serializable]sealed class Result
        {
            public string mapId="",error="";
            public bool loaded,movement,pickup,fire,damage,restart,lobbyRestore,headsetVerified=false;
            public int floorCases,meshColliders;public float cameraFarDistance;
            public string[] screenshots=new string[0];
        }
        static void Need(bool okay,string message){if(!okay)throw new InvalidOperationException(message);}
        static void Settle(R23PlayableLoop loop)
        {
            float start=loop.Rig.transform.position.y;
            for(int i=0;i<180;i++)loop.Rig.SmokeXrMove(Vector2.zero,.02f);
            Physics.SyncTransforms();RaycastHit floor;
            Need(loop.Rig.HeadCollision.Support(loop.Rig.transform.position,loop.Rig.Head.transform.position,out floor),"No native source floor under actual head/body footprint");
            Need(start-loop.Rig.transform.position.y<.6f&&Mathf.Abs(loop.Rig.transform.position.y-floor.point.y)<.06f,"Source spawn did not settle onto its actual floor");
            float y=loop.Rig.transform.position.y;for(int i=0;i<20;i++)loop.Rig.SmokeXrMove(Vector2.zero,.02f);
            Need(Mathf.Abs(y-loop.Rig.transform.position.y)<.002f,"Stationary actor continues falling");
        }
        public IEnumerator Start()
        {
            var result=new Result{mapId=R23PlayableBuild.Argument("-ashfallEntryMap")};
            var choice=R23MapCatalog.Find(result.mapId);if(choice!=null)result.mapId=choice.Id;
            R23PlayableLoop loop=null;float end=Time.realtimeSinceStartup+90;
            while(Time.realtimeSinceStartup<end){loop=UnityEngine.Object.FindAnyObjectByType<R23PlayableLoop>();if(loop!=null&&(loop.Ready||loop.Failure!=null))break;yield return null;}
            if(choice==null||loop==null||!loop.Ready){result.error=choice==null?"Unknown explicit entry map":loop==null?"Playable root absent":loop.Failure??"Initial map timeout";R23SmokeControl.Finish(false,JsonUtility.ToJson(result,true));yield break;}
            loop.ManualSmoke=true;loop.ExternalProbePump=true;loop.Rig.enabled=false;loop.Calibration.SuppressPersistence=true;
            loop.SelectMap(choice.Id,true);end=Time.realtimeSinceStartup+180;
            while(!loop.Ready&&loop.Failure==null&&Time.realtimeSinceStartup<end)yield return null;
            try
            {
                Need(loop.Ready&&loop.Map.IsLoaded&&loop.Map.MapId==choice.Id,loop.Failure??"Explicit source map timed out");result.loaded=true;
                foreach(var collider in loop.Map.GetComponentsInChildren<MeshCollider>(true))
                {Need(collider.sharedMesh!=null&&collider.sharedMesh.isReadable&&collider.sharedMesh.vertexCount>0,"Map collider unavailable for packaged mesh cooking");result.meshColliders++;}
                Need(result.meshColliders>0,"Source mesh collision missing");
                result.cameraFarDistance=loop.Rig.Head.farClipPlane;
                Need(Mathf.Abs(result.cameraFarDistance-loop.Map.CameraFarDistance)<.01f,"Map camera distance was not applied");
                if(choice.Id==R23MapCatalog.ClassicId)Need(Mathf.Abs(result.cameraFarDistance-4887.541f)<.05f,"Classic source far-distance clipped to old map default");
                foreach(bool gameSpawn in new[]{false,true})foreach(var offset in new[]{Vector2.zero,new Vector2(1.2f,-.8f)})foreach(float eye in new[]{.65f,1.9f})
                {
                    loop.ReturnToLobby();loop.Rig.Head.transform.localPosition=new Vector3(offset.x,eye,offset.y);loop.Rig.TrackingOrigin.localPosition=Vector3.zero;
                    loop.UI.ShowReady();loop.UI.OpenSpawnChoice();Need(loop.UI.ConfirmMapSpawn(gameSpawn),"Actual source menu refused spawn choice");
                    loop.Rig.GameplayBlocked=false;loop.Gameplay.NotifyFocus(true);loop.Gameplay.SetGameplayPaused(false);
                    loop.Calibration.RequestRecalibration();for(int i=0;i<8;i++)loop.Calibration.Tick(true,true,false,.05f);
                    Need(Mathf.Abs(loop.Rig.Head.transform.position.y-loop.Rig.transform.position.y-1.7088f)<.002f,"Current-posture calibration lost on map entry");
                    Settle(loop);result.floorCases++;loop.RestartRound();loop.Rig.GameplayBlocked=false;Settle(loop);result.floorCases++;
                }
                Vector3 start=loop.Rig.transform.position;
                foreach(var direction in new[]{Vector2.up,Vector2.down,Vector2.left,Vector2.right})
                {for(int i=0;i<8;i++)loop.Rig.SmokeXrMove(direction,.05f);if(Vector3.ProjectOnPlane(loop.Rig.transform.position-start,Vector3.up).magnitude>.1f)break;}
                Need(Vector3.ProjectOnPlane(loop.Rig.transform.position-start,Vector3.up).magnitude>.1f,"No actual player movement at source spawn");result.movement=true;
                loop.RestartRound();loop.Rig.GameplayBlocked=false;Settle(loop);loop.Gameplay.NotifyFocus(true);loop.Gameplay.SetGameplayPaused(false);
                loop.Gameplay.TickInput(.02f,new R23HandsInputFrame{LeftTracked=true,RightTracked=true});loop.Rig.Right.position=loop.Gameplay.WeaponGripPosition;
                loop.Gameplay.TickInput(.02f,new R23HandsInputFrame{LeftTracked=true,RightTracked=true,RightGrip=.8f});Need(loop.Gameplay.HasWeapon,"Actual grip pickup failed");result.pickup=true;
                loop.Gameplay.TickControls(.2f,false,false);int ammo=loop.Gameplay.Ammo;
                Need(loop.Gameplay.Fire(.12f)&&loop.Gameplay.Ammo==ammo-1,"Actual source shot or ammo debit failed");result.fire=true;
                float total=loop.Gameplay.Health+loop.Gameplay.Shield;loop.Gameplay.ApplyPlayerDamage(35);Need(Mathf.Abs(total-loop.Gameplay.Health-loop.Gameplay.Shield-35)<.001f,"Actual damage failed");result.damage=true;
                loop.Gameplay.ApplyPlayerDamage(1000);loop.RestartRound();loop.Rig.GameplayBlocked=false;Settle(loop);Need(loop.Gameplay.Health==100&&!loop.Gameplay.HasWeapon,"Death/restart owner reset failed");result.restart=true;
                result.screenshots=new string[4];Quaternion rotation=loop.Rig.Head.transform.rotation;
                try{for(int i=0;i<4;i++){loop.Rig.Head.transform.rotation=Quaternion.Euler(0,i*90,0);result.screenshots[i]=R23SmokeControl.Capture(loop.Rig.Head,"entry-"+choice.Id.Replace(':','-')+"-"+i);}}
                finally{loop.Rig.Head.transform.rotation=rotation;}
                loop.ReturnToLobby();Need(loop.Lobby.Visible&&loop.UI.Page==Ashfall.Integration.Playable.UI.R23UiPage.Ready,"Map failed to return to actual lobby");result.lobbyRestore=true;
            }
            catch(Exception error){result.error=error.ToString();Debug.LogException(error);}
            R23SmokeControl.Finish(result.error.Length==0&&result.floorCases==16,JsonUtility.ToJson(result,true));
        }
    }
}
#endif
