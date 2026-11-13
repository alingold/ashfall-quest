#if UNITY_EDITOR && UNITY_6000_0_OR_NEWER
using System;
using UnityEngine;
using Ashfall.Core;
using Ashfall.Modules.ConstructionCandidate;
using Ashfall.Integration.Playable;
using Ashfall.Integration.Playable.HandsWeapons;
namespace Ashfall.Editor.Playable
{
    public static class R23CarriedBuildDamageSmoke
    {
        static void Need(bool okay,string message){if(!okay)throw new Exception(message);}
        public static void Run(R23PlayableLoop loop)
        {
            var rig=loop.Rig;var game=loop.Gameplay;var activity=loop.Activities;
            float side=CandidateGeometry.Side;Vector3 site=new Vector3(side*1000+side*.5f,0,side*.5f);
            var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.name="Editor-only committed-shot support";floor.transform.SetParent(loop.Map.transform,false);floor.transform.position=site-Vector3.up*.1f;floor.transform.localScale=new Vector3(24,.2f,24);
            try
            {
                activity.ResetRound();loop.Climbing.ResetMotion();rig.SetSpawn(site,site+Vector3.forward);game.ResetRound();game.NotifyFocus(true);game.SetGameplayPaused(false);activity.TickStructure(0,false);
                rig.Right.position=site+new Vector3(.3f,1.2f,.2f);rig.Right.rotation=Quaternion.identity;Physics.SyncTransforms();
                long tick=rig.LastFrame.Tick;
                activity.TickConstruction(new InputFrame{Tick=++tick,Tracked=true},true,true,false,false);
                activity.TickConstruction(new InputFrame{Tick=++tick,Tracked=true,TriggerRight=true},true,true,false,false);
                activity.TickConstruction(new InputFrame{Tick=++tick,Tracked=true},true,true,false,false);
                Need(activity.PanelCount==1,"Actual trigger/release failed to create single native cover: "+activity.BuildStatus);
                var panel=activity.PanelSnapshot[0];int charges=activity.Balance;
                var neutral=new R23HandsInputFrame{LeftTracked=true,RightTracked=true};game.TickInput(.02f,neutral);
                Need(game.EquipOwned("mk18",1),"Real source MK18 did not equip for committed-shot check");
                Transform muzzle=null;foreach(var part in rig.Right.GetComponentsInChildren<Transform>())if(part.name=="Actual source muzzle")muzzle=part;
                Need(muzzle!=null,"Actual source bore absent");
                Vector3 center=new Vector3(panel.Shape.Center.X,panel.Shape.Center.Y,panel.Shape.Center.Z);
                rig.Right.position=center-Vector3.forward*2;rig.Right.rotation=Quaternion.Inverse(R23TrackedGripPose.LocalRotation);
                for(int i=0;i<5;i++)rig.Right.rotation=Quaternion.FromToRotation(muzzle.forward,(center-muzzle.position).normalized)*rig.Right.rotation;
                Physics.SyncTransforms();game.TickInput(.02f,neutral);int before=game.Ammo;
                var firing=neutral;firing.RightTrigger=.8f;
                for(int i=0;i<40&&game.Ammo==before;i++)game.TickInput(.02f,firing);
                Need(game.Ammo<before,"Carried weapon did not publish a committed shot");
                var after=activity.PanelSnapshot;
                Need(after.Length==1&&Mathf.Abs(panel.Health-after[0].Health-(before-game.Ammo)*22)<.001f,"Committed MK18 ray/WorldHit did not debit real source panel health");
                // MK18 commits one three-round burst per press; release between subsequent bursts.
                for(int i=0;i<400&&activity.PanelCount>0;i++)game.TickInput(.02f,i%32==0?neutral:firing);
                Need(activity.PanelCount==0&&activity.Balance==charges,"Actual carried shots did not withdraw destroyed cover without refund");
                RaycastHit hit;Need(!Physics.Raycast(center-Vector3.forward*.5f,Vector3.forward,out hit,1,Physics.AllLayers,QueryTriggerInteraction.Ignore),"Destroyed carried-shot panel still blocks physical rays");
                Debug.Log("R23 CARRIED COVER PASS: actual source MK18 ammo debit, nearest bore hit, production WorldHit subscription, owner HP, destruction/collider withdrawal and no charge refund");
            }
            finally
            {
                floor.SetActive(false);UnityEngine.Object.Destroy(floor);activity.ResetRound();game.ResetRound();rig.SetSpawn(loop.Map.GameSpawn,loop.Map.Bounds.center);Physics.SyncTransforms();
            }
        }
    }
}
#endif
