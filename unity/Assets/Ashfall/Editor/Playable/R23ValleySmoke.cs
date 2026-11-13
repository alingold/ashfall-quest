#if UNITY_EDITOR
using System;
using System.Collections;
using UnityEditor;
using UnityEngine;
using Ashfall.Integration.Playable;
using Ashfall.Integration.Playable.HandsWeapons;
using Ashfall.Integration.Playable.World;
namespace Ashfall.Editor.Playable
{
    // One rendered map/gameplay comparison. Explicitly XR-disabled; human
    // headset appearance, tracking and controller feel are separate evidence.
    public sealed class R23ValleySmoke
    {
        [Serializable]sealed class Result
        {
            public bool loaded,sourceEnvironment,collisionReadable,movement,pickup,fire,damage,restart,lobbyRestore;
            public int floorCases,physicalShapes,ballisticShapes,playerOnlyShapes;
            public bool headsetVerified=false;public string error="";
        }
        static void Need(bool condition,string message){if(!condition)throw new Exception(message);}
        static void Settle(R23PlayableLoop loop)
        {
            for(int i=0;i<200;i++)loop.Rig.SmokeXrMove(Vector2.zero,.02f);
            Physics.SyncTransforms();RaycastHit floor;
            Need(loop.Rig.HeadCollision.Support(loop.Rig.transform.position,loop.Rig.Head.transform.position,out floor),"Actual source head has no native support at Valley spawn");
            Need(Mathf.Abs(loop.Rig.transform.position.y-floor.point.y)<.06f,"Actor did not settle onto actual Valley collision floor");
            float previous=loop.Rig.transform.position.y;loop.Rig.SmokeXrMove(Vector2.zero,.02f);
            Need(Mathf.Abs(previous-loop.Rig.transform.position.y)<.002f,"Actor continues falling after settling");
        }
        public IEnumerator Start()
        {
            var result=new Result();R23PlayableLoop loop=null;float end=Time.realtimeSinceStartup+90;
            while(Time.realtimeSinceStartup<end){loop=UnityEngine.Object.FindAnyObjectByType<R23PlayableLoop>();if(loop!=null&&(loop.Ready||loop.Failure!=null))break;yield return null;}
            if(loop==null||!loop.Ready){result.error=loop==null?"Loop absent":loop.Failure??"Initial map timeout";R23SmokeControl.Finish(false,JsonUtility.ToJson(result,true));yield break;}
            loop.ExternalProbePump=true;loop.ManualSmoke=true;loop.Rig.enabled=false;loop.Calibration.SuppressPersistence=true;
            loop.SelectMap(R23MapCatalog.ValleyId,true);end=Time.realtimeSinceStartup+120;
            while(!loop.Ready&&loop.Failure==null&&Time.realtimeSinceStartup<end)yield return null;
            try
            {
                Need(loop.Ready&&loop.Map.MapId==R23MapCatalog.ValleyId,loop.Failure??"Valley map load timeout");result.loaded=true;
                foreach(string resource in new[]{"R23ValleyPbr","R23AlpineTerrain","R23ValleySky"})
                {var shader=Resources.Load<Shader>(resource);Need(shader!=null&&shader.isSupported&&!ShaderUtil.ShaderHasError(shader),"Source shader compile failed: "+resource);}
                result.collisionReadable=true;
                foreach(var collider in loop.Map.GetComponentsInChildren<Collider>(true))
                {
                    if(collider.gameObject.layer==R23PlayableMap.BallisticOnlyLayer)result.ballisticShapes++;
                    else if(collider.gameObject.layer==R23PlayableMap.PlayerOnlyLayer)result.playerOnlyShapes++;
                    else result.physicalShapes++;
                    var mesh=collider as MeshCollider;if(mesh!=null)Need(mesh.sharedMesh!=null&&mesh.sharedMesh.isReadable,"Exported map collider must remain readable for cooking");
                }
                Need(result.ballisticShapes>0&&result.physicalShapes>0&&result.playerOnlyShapes==3,"Source collision roles lost");
                var environment=loop.Map.GetComponent<R23ValleyEnvironment>();Need(environment!=null&&environment.Ready&&!environment.ActiveEnvironment,"Hidden Valley changed lobby environment");
                foreach(var offset in new[]{Vector2.zero,new Vector2(1.2f,-.8f)})foreach(float eye in new[]{1.55f,1.9f})
                {
                    loop.Rig.Head.transform.localPosition=new Vector3(offset.x,eye,offset.y);loop.Rig.TrackingOrigin.localPosition=Vector3.zero;
                    loop.StartPlaying();loop.Rig.GameplayBlocked=false;loop.Gameplay.NotifyFocus(true);loop.Gameplay.SetGameplayPaused(false);
                    loop.Calibration.NotifyRootPoseReset();loop.Calibration.RequestRecalibration();for(int i=0;i<8;i++)loop.Calibration.Tick(true,true,false,.05f);
                    Settle(loop);result.floorCases++;loop.RestartRound();loop.Rig.GameplayBlocked=false;Settle(loop);result.floorCases++;
                }
                Need(environment.ActiveEnvironment&&RenderSettings.skybox!=null&&RenderSettings.sun!=null&&RenderSettings.fog,"Source environment did not activate with map");result.sourceEnvironment=true;
                var origin=loop.Rig.transform.position;
                foreach(var direction in new[]{Vector2.up,Vector2.down,Vector2.left,Vector2.right})
                {for(int i=0;i<6;i++)loop.Rig.SmokeXrMove(direction,.05f);if(Vector3.ProjectOnPlane(loop.Rig.transform.position-origin,Vector3.up).magnitude>.1f)break;}
                Need(Vector3.ProjectOnPlane(loop.Rig.transform.position-origin,Vector3.up).magnitude>.1f,"No actual movement in Valley");result.movement=true;
                loop.RestartRound();loop.Rig.GameplayBlocked=false;Settle(loop);loop.Gameplay.NotifyFocus(true);loop.Gameplay.SetGameplayPaused(false);
                loop.Rig.Right.position=loop.Gameplay.WeaponGripPosition;
                loop.Gameplay.TickInput(.02f,new R23HandsInputFrame{LeftTracked=true,RightTracked=true,Grip=.7f});
                Need(loop.Gameplay.HasWeapon,"Grip proximity pickup failed in Valley");result.pickup=true;
                loop.Gameplay.TickControls(.1f,false,false);loop.Gameplay.TickControls(.1f,false,false);int ammo=loop.Gameplay.Ammo;
                Need(loop.Gameplay.Fire(.12f)&&loop.Gameplay.Ammo==ammo-1,"Source firearm/ammo debit failed in Valley");result.fire=true;
                float before=loop.Gameplay.Health+loop.Gameplay.Shield;loop.Gameplay.ApplyPlayerDamage(35);
                Need(Mathf.Abs(before-loop.Gameplay.Health-loop.Gameplay.Shield-35)<.001f,"Actual Health damage failed");result.damage=true;
                loop.Gameplay.ApplyPlayerDamage(1000);loop.RestartRound();loop.Rig.GameplayBlocked=false;Settle(loop);
                Need(loop.Gameplay.Health==100&&!loop.Gameplay.HasWeapon,"Restart did not restore actual round owners");result.restart=true;
                var rotation=loop.Rig.Head.transform.rotation;
                for(int i=0;i<4;i++){loop.Rig.Head.transform.rotation=Quaternion.Euler(0,i*90,0);R23SmokeControl.Capture(loop.Rig.Head,"valley-"+i);}
                loop.Rig.Head.transform.rotation=rotation;loop.ReturnToLobby();
                Need(!environment.ActiveEnvironment&&loop.Lobby.Visible,"Valley environment leaked into lobby");result.lobbyRestore=true;
            }
            catch(Exception error){result.error=error.ToString();Debug.LogException(error);}
            R23SmokeControl.Finish(result.error.Length==0,JsonUtility.ToJson(result,true));
        }
    }
}
#endif
