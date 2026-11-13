#if UNITY_6000_0_OR_NEWER
using System;
using Ashfall.Core;
using Ashfall.Modules.Climbing;
using UnityEngine;
namespace Ashfall.Integration.Playable.Climbing
{
    public struct R23ClimbMotion
    {
        public bool OwnsMotion,Climbing,Mantling,Released,Launch;
        public Vector3 Displacement,Velocity;
    }
    public struct R23ClimbAccepted
    {
        public Vector3 Velocity;public bool Grounded;
    }
    // No Update or hardware pump: the shared root supplies its sampled input once.
    // Native casts are read-only proposals. Root retains the ONLY CharacterController.Move.
    public sealed class R23VRClimbing:MonoBehaviour,IClimbCollision
    {
        Transform root,head,left,right,map;CharacterController body;R23ClimbState state;
        readonly Vector3[] proxies=new Vector3[2];readonly bool[] hasProxy=new bool[2];
        bool refreshLeft,refreshRight;
        Vector3 sampledHead,sampledLeft,sampledRight;
        public float FlingPower=3;
        public Func<Collider,bool> AdditionalStaticTerrain;
        public R23HeadCollision HeadCollision;
        public bool LeftAnchored {get{return state!=null&&state.LeftAnchored;}}
        public bool RightAnchored {get{return state!=null&&state.RightAnchored;}}
        public bool Mantling {get{return state!=null&&state.Mantling;}}
        public Vector3 LeftAnchor {get{return state==null?Vector3.zero:U(state.LeftAnchor);}}
        public Vector3 RightAnchor {get{return state==null?Vector3.zero:U(state.RightAnchor);}}
        public Vector3 LeftContactProxy {get{return hasProxy[0]?proxies[0]:left==null?Vector3.zero:left.position;}}
        public Vector3 RightContactProxy {get{return hasProxy[1]?proxies[1]:right==null?Vector3.zero:right.position;}}
        public string Status {get;private set;}
        public void Initialize(Transform playerRoot,Transform headPose,Transform leftPose,Transform rightPose,CharacterController controller,Transform staticMapRoot)
        {
            if(root!=null)throw new InvalidOperationException("climbing already initialized");
            if(playerRoot==null||headPose==null||leftPose==null||rightPose==null||controller==null||staticMapRoot==null)throw new ArgumentException("live accepted rig/controller/map required");
            root=playerRoot;head=headPose;left=leftPose;right=rightPose;body=controller;map=staticMapRoot;state=new R23ClimbState(this);ResetMotion();
        }
        public void ResetMotion(){if(state!=null)state.Reset();hasProxy[0]=hasProxy[1]=false;refreshLeft=refreshRight=false;Status="Grip a wall with a free hand to climb";}
        public R23ClimbMotion Evaluate(InputFrame input,float seconds,bool leftTracked,bool rightTracked,bool leftFree,bool rightFree,Vector3 previousVelocity)
        {
            var result=new R23ClimbMotion{Velocity=previousVelocity};
            if(state==null||body==null||!body.enabled)return result;
            if(Single.IsNaN(seconds)||Single.IsInfinity(seconds)||seconds<=0||seconds>.1f){ResetMotion();return result;}
            if(!F(root.position).IsFinite||!F(head.position).IsFinite||!F(left.position).IsFinite||!F(right.position).IsFinite){ResetMotion();return result;}
            leftTracked&=Vector3.Distance(left.position,head.position)<=3;rightTracked&=Vector3.Distance(right.position,head.position)<=3;
            bool enabled=input.Tracked&&!input.Paused;
            refreshLeft=enabled&&leftTracked&&leftFree;refreshRight=enabled&&rightTracked&&rightFree;
            if(!refreshLeft)hasProxy[0]=false;if(!refreshRight)hasProxy[1]=false;
            Physics.SyncTransforms();
            input.HeadPosition=F(head.position);input.LeftHandPosition=F(left.position);input.RightHandPosition=F(right.position);
            var options=ClimbOptions.Default;options.Enabled=enabled;options.LeftTracked=leftTracked;options.RightTracked=rightTracked;
            options.LeftReserved=!leftFree;options.RightReserved=!rightFree;
            options.LeftContact=F(refreshLeft?Proxy(0,left.position):left.position);options.RightContact=F(refreshRight?Proxy(1,right.position):right.position);
            sampledHead=head.position;sampledLeft=left.position;sampledRight=right.position;
            options.FlingPower=FlingPower;options.StandingEyeHeight=Mathf.Clamp(head.position.y-root.position.y,.55f,2.2f);options.FloorNormalY=Mathf.Cos(65*Mathf.Deg2Rad);
            var step=state.Evaluate(input,F(root.position),seconds,options);
            result.OwnsMotion=step.OwnsMotion;result.Climbing=step.Climbing;result.Mantling=step.Mantling;result.Released=step.Released;result.Launch=step.Launch;result.Displacement=U(step.RootDisplacement);
            if(step.OwnsMotion)result.Velocity=U(step.Velocity);
            Status=!step.Accepted?"Climb: "+step.Reason:step.Climbing?(LeftAnchored&&RightAnchored?"Climbing with both hands":"Climbing | release grip to fling"):step.Launch?"Released fling":"Grip a wall with a free hand to climb";
            return result;
        }
        public R23ClimbAccepted AcceptMotion(Vector3 actualDisplacement,float seconds,bool grounded,bool ceiling)
        {
            bool acceptedGrounded=grounded;var velocity=state==null?new Float3():state.AcceptMotion(F(actualDisplacement),seconds,grounded,ceiling,out acceptedGrounded);
            // Evaluate sampled these world-space contacts before the sole root moved/turned.
            // Re-query at the accepted poses: translating cached contacts would carry a
            // wall contact through geometry, while leaving them stale trails free hands.
            bool movedHead=(head.position-sampledHead).sqrMagnitude>1e-12f;
            if(refreshLeft){if(movedHead||(left.position-sampledLeft).sqrMagnitude>1e-12f)Proxy(0,left.position);}else hasProxy[0]=false;
            if(refreshRight){if(movedHead||(right.position-sampledRight).sqrMagnitude>1e-12f)Proxy(1,right.position);}else hasProxy[1]=false;
            return new R23ClimbAccepted{Velocity=U(velocity),Grounded=acceptedGrounded};
        }
        public bool TryRayAnchor(Vector3 origin,Vector3 direction,float range,out RaycastHit hit)
        {
            Physics.SyncTransforms();
            return Nearest(origin,direction.normalized*range,out hit);
        }
        public bool AnchorAlive(Collider collider){return Geometry(collider);}
        bool Geometry(Collider collider)
        {
            if(collider==null||collider.gameObject.layer==19||collider.gameObject.layer==20||collider.gameObject.layer==22||!collider.enabled||collider.isTrigger||!collider.gameObject.activeInHierarchy||collider is CharacterController||collider.transform.IsChildOf(root))return false;
            bool additional=AdditionalStaticTerrain!=null&&AdditionalStaticTerrain(collider);
            if(collider.attachedRigidbody!=null&&!additional)return false;
            // Loaded actual map and the existing Activities source-grid BoxCollider panels.
            return additional||collider.transform.IsChildOf(map)||(collider.gameObject.layer==8&&collider is BoxCollider);
        }
        bool Nearest(Vector3 from,Vector3 motion,out RaycastHit closest)
        {
            closest=new RaycastHit();float distance=float.PositiveInfinity,length=motion.magnitude;
            if(length<.000001f)return false;
            foreach(var hit in Physics.RaycastAll(from,motion/length,length,Ashfall.Integration.Playable.Bots.R24RagdollRules.GameplayQueryMask,QueryTriggerInteraction.Ignore))
                if(Geometry(hit.collider)&&hit.distance<distance){distance=hit.distance;closest=hit;}
            // hand_wall_r2 explicitly ray-tests back faces. A reversed native ray
            // supplies the opposite face without changing global Physics flags.
            foreach(var hit in Physics.RaycastAll(from+motion,-motion/length,length,Ashfall.Integration.Playable.Bots.R24RagdollRules.GameplayQueryMask,QueryTriggerInteraction.Ignore))
                if(Geometry(hit.collider)&&length-hit.distance<distance){distance=length-hit.distance;closest=hit;closest.distance=distance;}
            return !float.IsPositiveInfinity(distance);
        }
        Vector3 Proxy(int index,Vector3 desired)
        {
            Vector3 eye=head.position;RaycastHit hit;
            if(Nearest(eye,desired-eye,out hit))
            {Vector3 normal=hit.normal;if(Vector3.Dot(normal,eye-hit.point)<0)normal=-normal;desired=hit.point+normal*(.065f+.004f);}
            Vector3 start=hasProxy[index]?proxies[index]:eye;
            if(Vector3.Distance(start,eye)>Mathf.Max(1.2f,Vector3.Distance(eye,index==0?left.position:right.position)+.15f)||Vector3.Distance(start,index==0?left.position:right.position)>.75f||Nearest(eye,start-eye,out hit))start=eye;
            Vector3 motion=desired-start;float length=motion.magnitude,nearest=float.PositiveInfinity;
            if(length>.000001f)
                foreach(var contact in Physics.SphereCastAll(start,.065f,motion/length,length,Ashfall.Integration.Playable.Bots.R24RagdollRules.GameplayQueryMask,QueryTriggerInteraction.Ignore))
                    if(Geometry(contact.collider)&&contact.distance<nearest)nearest=contact.distance;
            if(!float.IsPositiveInfinity(nearest))desired=start+motion*Mathf.Max(0,nearest/length-.002f);
            proxies[index]=desired;hasProxy[index]=true;return desired;
        }
        public bool TryGrab(Float3 point,float radius,out SurfaceHit hit)
        {
            hit=new SurfaceHit();
            foreach(var collider in Physics.OverlapSphere(U(point),radius,Ashfall.Integration.Playable.Bots.R24RagdollRules.GameplayQueryMask,QueryTriggerInteraction.Ignore))if(Geometry(collider))
            {
                // OverlapSphere performs the actual native mesh contact test. Source anchors the
                // tracked hand, not ClosestPoint (which cannot query a non-convex MeshCollider).
                hit=new SurfaceHit{Point=point,Normal=new Float3(0,1,0),AuthoredClimbable=true};return true;
            }
            return false;
        }
        // Source release mantle requires its calibrated head-only convex collider. The current
        // playable root owns a full CharacterController capsule; no fake support/teleport route.
        public bool Raycast(Float3 from,Float3 to,QueryKind kind,out SurfaceHit hit)
        {hit=new SurfaceHit();RaycastHit native;if(HeadCollision==null||!HeadCollision.Ray(U(from),U(to),out native))return false;hit=new SurfaceHit{Point=F(native.point),Normal=F(native.normal),AuthoredClimbable=true};return true;}
        public bool HeadMotionClear(Float3 headPoint,Float3 motion){return HeadCollision!=null&&HeadCollision.Clear(U(headPoint),U(motion));}
        public MotionResult SweepHead(Float3 headPoint,Float3 motion,float seconds,bool slide)
        {
            if(HeadCollision!=null){bool ceiling;Vector3 safe=HeadCollision.Sweep(U(headPoint),U(motion),slide,out ceiling);return new MotionResult(F(safe),F(safe/Mathf.Max(.001f,seconds)));}
            Vector3 total=Vector3.zero,remaining=U(motion),headOffset=U(headPoint)-head.position;
            Vector3 scale=body.transform.lossyScale;float radius=body.radius*Mathf.Max(Mathf.Abs(scale.x),Mathf.Abs(scale.z));float height=Mathf.Max(radius*2,body.height*Mathf.Abs(scale.y));
            float queryRadius=Mathf.Max(.005f,radius-body.skinWidth),half=Mathf.Max(0,height*.5f-radius);
            Vector3 center=body.transform.TransformPoint(body.center)+headOffset,up=body.transform.up;
            for(int pass=0;pass<(slide?4:1);pass++)
            {
                float length=remaining.magnitude;if(length<.000001f)break;
                RaycastHit closest=new RaycastHit();float nearest=float.PositiveInfinity;
                foreach(var hit in Physics.CapsuleCastAll(center+total+up*half,center+total-up*half,queryRadius,remaining/length,length,Ashfall.Integration.Playable.Bots.R24RagdollRules.GameplayQueryMask,QueryTriggerInteraction.Ignore))
                    if(Geometry(hit.collider)&&hit.distance<nearest){nearest=hit.distance;closest=hit;}
                if(float.IsPositiveInfinity(nearest)){total+=remaining;break;}
                Vector3 safe=remaining/length*Mathf.Max(0,nearest-.001f);total+=safe;remaining-=safe;
                if(!slide||Vector3.Dot(remaining,closest.normal)>=0)break;
                remaining=Vector3.ProjectOnPlane(remaining,closest.normal);
            }
            return new MotionResult(F(total),F(total/Mathf.Max(.001f,seconds)));
        }
        void OnDisable(){ResetMotion();}
        static Float3 F(Vector3 value){return new Float3(value.x,value.y,value.z);}static Vector3 U(Float3 value){return new Vector3(value.X,value.Y,value.Z);}
    }
}
#endif
