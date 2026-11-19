#if UNITY_6000_0_OR_NEWER
using Ashfall.Core;
using Ashfall.Integration.Playable.Activities;
using Ashfall.Integration.Playable.Climbing;
using Ashfall.Integration.Playable.Traversal;
using UnityEngine;
using UnityEngine.XR;
namespace Ashfall.Integration.Playable
{
    public sealed partial class R23VRRig
    {
        readonly R25JetPack jetPack=new R25JetPack();
        readonly R25WebRope[] webRopes={new R25WebRope(),new R25WebRope()};
        readonly Collider[] webColliders=new Collider[2];
        readonly Vector3[] webNormals=new Vector3[2];
        readonly R25GrappleInput[] grappleInput={new R25GrappleInput(),new R25GrappleInput()};
        public HandsWeapons.R23HandsWeapons GrappleHands;
        int grappleShotHand=-1;
        public int TakeGrappleShot(){int hand=grappleShotHand;grappleShotHand=-1;return hand;}
        public void StopGrappleShot(int hand){if(hand>=0&&hand<2){webRopes[hand].Detach();webAim[hand]=false;}}
        readonly Vector3[] webPreviousHand=new Vector3[2];
        readonly bool[] webHaveHand=new bool[2];
        readonly bool[] webAim=new bool[2];
        readonly Vector3[] webCandidate=new Vector3[2];
        readonly bool[] webCandidateValid=new bool[2];
        Vector3 grappleProbeRequested;
        bool traversalOwned,traversalRearm;
        R25TraversalPresentation traversalView;
        public System.Func<Match.R24PlayableMatchRules> ReadTraversalRules;
        public bool TraversalInputAllowed=true;
        public bool Jetting {get{return jetPack.Power>0;}}
        public R25WebState WebState {get{return new R25WebState(webRopes[0].Attached,webRopes[0].Attached?webRopes[0].Anchor:new Float3(),webRopes[1].Attached,webRopes[1].Attached?webRopes[1].Anchor:new Float3());}}
        public void CancelTraversal()
        {
            grappleShotHand=-1;jetPack.Cancel();traversalOwned=false;traversalRearm=true;
            for(int i=0;i<2;i++){webRopes[i].Detach();webAim[i]=false;grappleInput[i].Cancel();webHaveHand[i]=false;}
            if(traversalView!=null)traversalView.Hide();
        }
        bool EvaluateTraversal(InputFrame frame,float dt,bool climb,bool anchored,Vector3 previous,float heading,out Vector3 displacement,out Vector3 velocity)
        {
            traversalSeconds=dt;displacement=Vector3.zero;velocity=previous;traversalOwned=false;
            bool blocked=!TraversalInputAllowed||!frame.Tracked||frame.Paused||GameplayBlocked||MenuLocomotion||SourceGhostMotion||CarrierActive;
            var rules=ReadTraversalRules==null?null:ReadTraversalRules();
            if(blocked){CancelTraversal();return false;}
            if(traversalRearm){if(frame.Jump||LeftInput.Trigger>.2f||RightInput.Trigger>.2f)return false;traversalRearm=false;}
            if(traversalView==null){traversalView=gameObject.AddComponent<R25TraversalPresentation>();traversalView.Initialize();}
            for(int i=0;i<2;i++){
                Vector3 aim=Vector3.zero;Quaternion rotation=Quaternion.identity;
                float trigger=i==0?LeftInput.Trigger:RightInput.Trigger;
                bool equipped=GrappleHands!=null&&GrappleHands.TryReadGrappleMuzzle(i,out aim,out rotation);
                bool free=equipped&&!climb&&(i==0?!LeftObjectiveGripReserved&&input.LeftReady&&LeftInput.Tracked:!RightObjectiveGripReserved&&input.RightReady&&RightInput.Tracked);
                bool held=grappleInput[i].Held;bool fire=grappleInput[i].Step(free&&(rules==null||rules.GrappleGun),trigger);
                if(held&&!grappleInput[i].Held)grappleShotHand=3;
                if(fire)grappleShotHand=i+4;
                Vector3 wrist=aim;
                webAim[i]=grappleInput[i].Aiming;
                RaycastHit hit;bool valid=false;
                if(webAim[i]&&Climbing!=null&&Climbing.TryRayAnchor(aim,rotation*Vector3.forward,45,out hit)){
                    valid=hit.distance>=2.5f;webCandidate[i]=hit.point;
                    if(valid&&fire&&webRopes[i].Fire(F(wrist),F(hit.point))){
                        webColliders[i]=hit.collider;webNormals[i]=Vector3.Dot(hit.normal,wrist-hit.point)<0?-hit.normal:hit.normal;
                        traversalView.Cue("shoot",wrist);
                    }
                }
                bool obstruction=false;
                if(webRopes[i].Attached&&Climbing!=null){Vector3 segment=U(webRopes[i].Anchor)-wrist;RaycastHit obstacle;obstruction=Climbing.TryRayAnchor(wrist,segment,Mathf.Max(0,segment.magnitude-.08f),out obstacle);}
                bool was=webRopes[i].Attached;float flight=webRopes[i].FlightRemaining;
                webRopes[i].Tick(dt,grappleInput[i].Held,Climbing!=null&&Climbing.AnchorAlive(webColliders[i]),obstruction);
                if(was&&!webRopes[i].Attached)traversalView.Cue("release",wrist);
                if(webRopes[i].Attached&&flight>0&&webRopes[i].FlightRemaining<=0){traversalView.Cue("attach",wrist);TraversalHaptic(i,.25f);}
                if(webRopes[i].Attached&&webRopes[i].FlightRemaining<=0){
                    Vector3 toward=(Head.transform.position-wrist).normalized;
                    Vector3 local=transform.InverseTransformPoint(wrist);
                    float tug=webHaveHand[i]?Vector3.Dot(transform.TransformVector(local-webPreviousHand[i])/dt,toward):0;
                    displacement=ReelToHandReach(i,U(webRopes[i].Reel(F(Head.transform.position),trigger,tug,dt)));
                    velocity=displacement/dt;
                    if(webRopes[i].Zipped)traversalView.Cue("zip-loop",wrist);
                    webPreviousHand[i]=local;webHaveHand[i]=true;traversalOwned=true;
                    TraversalHaptic(i,Mathf.Clamp01(trigger)*.09f);
                }else webHaveHand[i]=false;
                webCandidateValid[i]=valid;
            }
            var travel=R23TravelHeading.Resolve(heading,0,frame.MoveX,frame.MoveY);
            bool glide=Vector2.Distance(new Vector2(Left.localPosition.x,Left.localPosition.z),new Vector2(Right.localPosition.x,Right.localPosition.z))>.95890625f;
            Vector3 dir=glide?U(travel.Forward):U(travel.Direction);
            Vector3 j=U(jetPack.Step(dt,(rules==null||rules.Jetpack)&&!climb&&!traversalOwned,grounded,anchored,frame.Jump,glide,F(previous),F(dir),tuning.WalkMultiplier*Ashfall.Modules.Player.PlayerTuning.WalkSpeed*(12f/6.5f)));
            if(Jetting){velocity=j;displacement=j*dt;traversalOwned=true;TraversalHaptic(PrimaryHand==0?1:0,.045f);}
            grappleProbeRequested=displacement;
            return traversalOwned;
        }
        Vector3 ReelToHandReach(int gunHand,Vector3 displacement)
        {
            bool left=gunHand==1;
            if(left?LeftHandOccupied||LeftObjectiveGripReserved||!input.LeftReady||!LeftInput.Tracked:RightHandOccupied||RightObjectiveGripReserved||!input.RightReady||!RightInput.Tracked)return displacement;
            Vector3 hand=left?Left.position:Right.position;
            if((hand-Head.transform.position).sqrMagnitude>1.5f*1.5f)return displacement;
            float fraction=R25WebRope.ReachFraction(F(hand),webRopes[gunHand].Anchor,F(webNormals[gunHand]),F(displacement),R23VRClimbing.GrappleReach);
            Vector3 shortened=displacement*fraction;
            // A ray hit's plane extends past its edges. Stop only for real contact with the hooked collider.
            return fraction<1&&Climbing.GrappleHandInReach(hand+shortened,webColliders[gunHand])?shortened:displacement;
        }
        void PresentTraversal()
        {
            if(traversalView==null||traversalRearm)return;
            for(int i=0;i<2;i++){Vector3 origin=Vector3.zero;Quaternion rotation=Quaternion.identity;bool shown=GrappleHands!=null&&GrappleHands.TryReadGrappleMuzzle(i,out origin,out rotation);if(!shown)origin=i==0?Left.position:Right.position;traversalView.Web(i,origin,U(webRopes[i].Anchor),webRopes[i].Attached,webRopes[i].FlightRemaining,Vector3.Distance(origin,U(webRopes[i].Anchor)),webCandidateValid[i]&&!webRopes[i].Attached,webCandidate[i],i==0?LeftInput.Trigger:RightInput.Trigger);}
            traversalView.Jet(Head.transform.position,transform.rotation,Jetting,jetPack.Power,jetPack.Fuel,PrimaryHand==0?Right:Left);
        }
        Vector3 SweptTraversalMotion(Vector3 requested,out bool hitCeiling)
        {
            Vector3 feet=transform.position,eye=Head.transform.position;
            hitCeiling=false;
            if(!HeadCollision.HeadClear(eye))return Vector3.zero;
            // Traversal displacement is a request, never permission to cross geometry.
            requested=Vector3.ClampMagnitude(requested,(Jetting?32:R25WebRope.MaxReelSpeed)*traversalSeconds);
            Vector3 bodySafe=HeadCollision.TraversalBodyMotion(feet,eye,requested);
            Vector3 safe=VerifiedHeadMotion(eye,bodySafe,true,false,out hitCeiling);
            // Head sliding may change direction: sweep the body again, then
            // verify the resulting head path and both endpoints unconditionally.
            safe=HeadCollision.TraversalBodyMotion(feet,eye,safe);
            bool hit;safe=VerifiedHeadMotion(eye,safe,false,false,out hit);hitCeiling|=hit;
            return HeadCollision.HeadClear(eye+safe)&&HeadCollision.PullBodyClear(feet+safe,eye+safe)?safe:Vector3.zero;
        }
        float traversalSeconds;
        static Float3 TraversalAccepted(Vector3 physical,float dt,float cap)
        {return R25WebRope.Accept(F(physical),dt,Mathf.Min(R25WebRope.MaxReelSpeed,cap));}
        static void TraversalHaptic(int hand,float strength){var device=InputDevices.GetDeviceAtXRNode(hand==0?XRNode.LeftHand:XRNode.RightHand);if(device.isValid)device.SendHapticImpulse(0,strength,.04f);}
    }
}
#endif
