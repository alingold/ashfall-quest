#if UNITY_EDITOR
using System;
using UnityEngine;
using Ashfall.Modules.Flight;
using Ashfall.Modules.ConstructionCandidate;
using Ashfall.Integration.Playable;
namespace Ashfall.Editor.Playable
{
    public static class R23ActivitySmoke
    {
        public static void Run(R23PlayableLoop loop)
        {
            var rig=loop.Rig;float side=CandidateGeometry.Side;
            Vector3 site=new Vector3(side*1000+side*.5f,0,side*.5f);
            var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.name="Editor-only activity fixture floor";floor.transform.SetParent(loop.Map.transform,false);floor.transform.position=site-Vector3.up*.1f;floor.transform.localScale=new Vector3(24,.2f,24);
            try
            {
                rig.SetSpawn(site,site+Vector3.forward);rig.RightHandOccupied=rig.LeftHandOccupied=false;loop.Activities.ResetRound();loop.Climbing.ResetMotion();
                rig.Left.position=site+new Vector3(-.35f,1.2f,.2f);rig.Right.position=site+new Vector3(.3f,1.2f,.2f);rig.Right.rotation=Quaternion.identity;
                Physics.SyncTransforms();for(int i=0;i<8;i++)rig.SmokeXrHands(0,0,.02f);
                int charges=loop.Activities.Balance;rig.SmokeXrHands(0,1,.02f);rig.SmokeXrHands(0,0,.02f);
                Need(loop.Activities.PanelCount>0&&loop.Activities.Balance<charges,"Native held-trigger construction did not place/debit: "+loop.Activities.BuildStatus);
                rig.Left.position=rig.transform.position+new Vector3(0,1.2f,1.35f);Physics.SyncTransforms();rig.SmokeXrHands(1,0,.02f);
                Need(loop.Climbing.LeftAnchored,"Native free-hand grip did not anchor on the built wall: "+loop.Climbing.Status);
                float before=rig.transform.position.y;rig.Left.position-=Vector3.up*.12f;rig.SmokeXrHands(1,0,.02f);
                Need(rig.transform.position.y>before+.03f,"Climb pull did not pass through the sole CharacterController owner");
                // Keep hands together for the fling observation. A hand still
                // by the chest and one at the wall exceeds source glide spread.
                rig.Right.position=rig.Left.position+Vector3.right*.2f;
                rig.SmokeXrHands(0,0,.02f);float released=rig.transform.position.y;rig.SmokeXrHands(0,0,.02f);
                Need(rig.transform.position.y>released+.02f,"Accepted upward release fling was lost");
                rig.Left.position=rig.Head.transform.position-Vector3.right*.7f;rig.Right.position=rig.Head.transform.position+Vector3.right*.7f;
                Vector3 glideStart=rig.transform.position;rig.SmokeXrHands(0,0,.02f);
                Need(loop.Activities.Mode==FlightMode.Glide&&Vector3.ProjectOnPlane(rig.transform.position-glideStart,Vector3.up).magnitude>.05f,"Spread-hand glide did not move the actual controller");
                CheckControllerHeading(loop,site);
                Debug.Log("R23 composed native construction/debit, wall-climb/pull, release-fling and spread-hand glide PASS");
            }
            finally
            {
                floor.SetActive(false);UnityEngine.Object.Destroy(floor);loop.Activities.ResetRound();loop.Climbing.ResetMotion();rig.SetSpawn(loop.Map.Spawn,loop.Map.Bounds.center);
            }
        }
        static void CheckControllerHeading(R23PlayableLoop loop,Vector3 site)
        {
            var rig=loop.Rig;var oldLeft=rig.Left.rotation;var oldRight=rig.Right.rotation;
            try
            {
                loop.Activities.ResetRound();loop.Climbing.ResetMotion();rig.SetSpawn(site,site+Vector3.forward);rig.LeftHandOccupied=rig.RightHandOccupied=false;
                rig.Left.position=site+new Vector3(-.3f,1.2f,.2f);rig.Right.position=site+new Vector3(.3f,1.2f,.2f);
                rig.Left.rotation=Quaternion.Euler(80,0,0);rig.Right.rotation=Quaternion.identity;
                rig.ApplySettings(new Ashfall.Integration.Playable.UI.R23UiSettings(forward:Ashfall.Integration.Playable.UI.R23Forward.LeftController));
                for(int i=0;i<8;i++)rig.SmokeXrMove(Vector2.zero,.02f);
                foreach(float pitch in new[]{80f,89f,90f,91f,100f})
                {
                    rig.Left.rotation=Quaternion.Euler(pitch,0,0);Vector3 before=rig.transform.position;rig.SmokeXrMove(Vector2.up,.02f);
                    Need(rig.transform.position.z>before.z+.01f,"Actual controller-relative root reversed/stopped at pitch "+pitch);
                }
                Debug.Log("R23 CONTROLLER HEADING PASS: actual input/motor/native root keeps forward travel through80/89/90/91/100-degree controller pitch");
            }
            finally{rig.ApplySettings(loop.UI.Settings);rig.Left.rotation=oldLeft;rig.Right.rotation=oldRight;}
        }
        static void Need(bool condition,string message){if(!condition)throw new Exception(message);}
    }
}
#endif
