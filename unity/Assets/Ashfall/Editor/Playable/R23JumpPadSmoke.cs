#if UNITY_EDITOR
using System;
using UnityEngine;
using Ashfall.Integration.Playable;
using Ashfall.Modules.Flight;
namespace Ashfall.Editor.Playable
{
    public static class R23JumpPadSmoke
    {
        static bool ClearStandingRise(R23PlayableLoop loop,Vector3 feet)
        {
            Vector3 bottom=feet+Vector3.up*.26f,top=feet+Vector3.up*1.65f;
            foreach(var c in Physics.OverlapCapsule(bottom,top,.249f,Physics.AllLayers,QueryTriggerInteraction.Ignore))if(c.transform.IsChildOf(loop.Map.transform))return false;
            foreach(var hit in Physics.CapsuleCastAll(bottom,top,.249f,Vector3.up,1.02f,Physics.AllLayers,QueryTriggerInteraction.Ignore))if(hit.collider.transform.IsChildOf(loop.Map.transform))return false;
            return true;
        }
        public static void Run(R23PlayableLoop loop)
        {
            if(!loop.JumpPads.Ready||loop.JumpPads.PadCount!=115||loop.JumpPads.TiltedCount!=44)throw new Exception("Actual ALFA authored pad census changed");
            Vector3 center=Vector3.zero;bool found=false;
            foreach(var source in loop.JumpPads.AuthoredPads)
            {
                if(source.Pad.Normal.Y<.99f)continue;var p=source.Pad.Center;Vector3 at=new Vector3(p.X,p.Y,p.Z);
                if(ClearStandingRise(loop,at)){center=at;found=true;break;}
            }
            if(!found)throw new Exception("No real upward ALFA pad with native standing/launch clearance found");
            bool previous=loop.Rig.UseSourceHeadHull;
            try
            {
                foreach(bool sourceHull in new[]{false,true})
                {
                    loop.Rig.UseSourceHeadHull=sourceHull;loop.Activities.ResetRound();loop.Climbing.ResetMotion();loop.JumpPads.SetMap(loop.Map.MapId);loop.Rig.SetSpawn(center,center+Vector3.forward);Physics.SyncTransforms();
                    for(int i=0;i<12;i++)loop.Rig.SmokeXrMove(Vector2.zero,.02f);
                    Vector3 feet=new Vector3(loop.Rig.Head.transform.position.x,loop.Rig.transform.position.y,loop.Rig.Head.transform.position.z);
                    if(!loop.JumpPads.Step(feet,.02f,true,false,false))throw new Exception("Actual ALFA pad did not dispatch through the real Flight owner (source hull="+sourceHull+")");
                    float before=loop.Rig.transform.position.y;loop.Rig.SmokeXrMove(Vector2.zero,.02f);
                    if(loop.Rig.transform.position.y-before<.5f||loop.Activities.Mode!=FlightMode.Launch)throw new Exception("Actual pad launch failed to move the sole root publisher (source hull="+sourceHull+")");
                }
                Debug.Log("R23 AUTHORED PAD PASS: actual ALFA115/44 census, native contact/clearance, real45m/s Flight and physical root launch with capsule and source head hull");
            }
            finally{loop.Rig.UseSourceHeadHull=previous;loop.Activities.ResetRound();loop.Climbing.ResetMotion();loop.JumpPads.SetMap(loop.Map.MapId);loop.Rig.SetSpawn(loop.Map.GameSpawn,loop.Map.Bounds.center);}
        }
    }
}
#endif
