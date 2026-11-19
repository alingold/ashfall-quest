#if UNITY_6000_0_OR_NEWER || R26_AVATAR_CPU
using UnityEngine;
namespace Ashfall.Integration.Playable.Appearance
{
    // Shared with the license-free CPU harness. Inputs/outputs are presentation points only.
    public static class R26AvatarJointMath
    {
        public const float HeadTiltLimit=25,WristBendLimit=65,NeckSeatAllowance=.02f,SourceEyeY=1.68f;
        public static Vector3 Collar(Vector3 neckBase,Vector3 eye,Quaternion uprightYaw,Vector3 jacketAt,Quaternion jacketRotation,Vector3 jacketScale,Vector3 jacketMin,Vector3 jacketMax)
        {
            // Neutral authored eye-to-neck offset locates the joint near the shoulder line.
            // Clamp into the actual posed jacket, independently of head pitch/roll and chest armour.
            Vector3 desired=eye+uprightYaw*(neckBase-new Vector3(0,SourceEyeY,0));
            Vector3 local=Quaternion.Inverse(jacketRotation)*(desired-jacketAt);
            local=new Vector3(local.x/jacketScale.x,local.y/jacketScale.y,local.z/jacketScale.z);
            local=Vector3.Max(jacketMin,Vector3.Min(jacketMax,local));
            return jacketAt+jacketRotation*Vector3.Scale(local,jacketScale);
        }
        public static void Head(Vector3 neckBase,Vector3 collar,Vector3 eye,Quaternion tracked,Quaternion uprightYaw,out Vector3 at,out Quaternion rotation)
        {
            rotation=Quaternion.RotateTowards(uprightYaw,tracked,HeadTiltLimit);
            Vector3 eyeFromNeck=new Vector3(0,SourceEyeY,0)-neckBase;
            Vector3 seat=collar+Vector3.ClampMagnitude(eye-(collar+rotation*eyeFromNeck),NeckSeatAllowance);
            at=seat-rotation*neckBase;
        }
        static Vector3 Project(Vector3 v,Vector3 axis){return v-axis*Vector3.Dot(v,axis);}
        static Vector3 Perpendicular(Vector3 axis,Vector3 preferred)
        {
            Vector3 p=Project(preferred,axis);
            if(p.sqrMagnitude<.000001f)p=Project(Mathf.Abs(axis.y)<.9f?Vector3.up:Vector3.right,axis);
            return p.normalized;
        }
        public static void Arm(Vector3 shoulder,Vector3 wrist,Vector3 handForward,Vector3 pole,ref Vector3 previousBend,ref Vector3 previousForearm,out Vector3 elbow)
        {
            Vector3 offset=wrist-shoulder;float distance=offset.magnitude;
            Vector3 axis=distance>.0001f?offset/distance:Vector3.down;
            // Analytic equal-length two-bone solve; allow suit stretch beyond human reach.
            float length=Mathf.Max(.32f,distance*.501f);
            Vector3 bend=Project(pole,axis);
            // Transport the last pole through the straight-arm singularity instead of flipping sides.
            Vector3 carried=Perpendicular(axis,previousBend.sqrMagnitude>.5f?previousBend:pole);
            if(bend.sqrMagnitude<.04f){if(Vector3.Dot(bend,carried)<0)bend=-bend;bend=Vector3.Lerp(carried,bend.normalized,Mathf.Clamp01(bend.magnitude/.2f));}
            bend=Perpendicular(axis,bend);previousBend=bend;
            elbow=shoulder+axis*distance*.5f+bend*Mathf.Sqrt(Mathf.Max(0,length*length-distance*distance*.25f));
            Vector3 forearm=(wrist-elbow).normalized;
            // Regularise the redundant elbow solution against its last axis; wrist contact remains exact.
            if(previousForearm.sqrMagnitude>.5f)forearm=Vector3.Lerp(previousForearm,forearm,.15f).normalized;
            if(handForward.sqrMagnitude>.000001f)
            {
                Vector3 hand=handForward.normalized;float cosine=Mathf.Cos(WristBendLimit*Mathf.Deg2Rad);
                if(Vector3.Dot(hand,forearm)<cosine)
                {
                    Vector3 turn=Project(forearm,hand);
                    // At an antipodal wrist, keep the prior cone side. It has no unique shortest arc.
                    if(turn.sqrMagnitude<.04f)
                    {
                        Vector3 carriedTurn=Perpendicular(hand,previousForearm.sqrMagnitude>.5f?previousForearm:-pole);
                        if(Vector3.Dot(turn,carriedTurn)<0)turn=-turn;
                        turn=Vector3.Lerp(carriedTurn,turn.normalized,Mathf.Clamp01(turn.magnitude/.2f));
                    }
                    forearm=hand*cosine+Perpendicular(hand,turn)*Mathf.Sin(WristBendLimit*Mathf.Deg2Rad);
                    // The hand stays fixed. Relax the upper sleeve length when the wrist cone is unreachable.
                }
            }
            elbow=wrist-forearm*length;previousForearm=forearm;
        }
        public static void FitSegment(Vector3 localFrom,Vector3 localTo,Vector3 from,Vector3 to,float diameter,out Vector3 at,out Quaternion rotation,out Vector3 scale)
        {
            Vector3 delta=localTo-localFrom,offset=to-from;float distance=Mathf.Max(.001f,offset.magnitude);
            // A folded upper sleeve can be shorter than its off-axis endpoint separation.
            float width=Mathf.Min(diameter,distance*.5f/Mathf.Max(.0001f,Mathf.Sqrt(delta.x*delta.x+delta.z*delta.z)));
            float radial=(delta.x*delta.x+delta.z*delta.z)*width*width;
            scale=new Vector3(width,Mathf.Sqrt(Mathf.Max(.00000001f,distance*distance-radial))/Mathf.Max(.0001f,delta.y),width);
            rotation=Quaternion.FromToRotation(Vector3.Scale(delta,scale),offset.sqrMagnitude>.000001f?offset:Vector3.down*.001f);
            at=from-rotation*Vector3.Scale(localFrom,scale);
        }
    }
#if UNITY_6000_0_OR_NEWER
    public sealed partial class R23PlayerAppearance
    {
        struct JointSpan {public Vector3 From,To;public JointSpan(Vector3 from,Vector3 to){From=from;To=to;}}
        sealed class JointGeometry
        {
            public Vector3 NeckBase,JacketMin,JacketMax;
            public readonly JointSpan[] Upper=new JointSpan[2],Forearm=new JointSpan[2];
            public readonly Vector2[] SleeveUv=new Vector2[2];
            static JointSpan Span(Kit kit,string label)
            {
                Bounds bounds=new Bounds();bool first=true;
                foreach(var part in kit.Parts)if(part.Label==label){if(first){bounds=part.Mesh.bounds;first=false;}else bounds.Encapsulate(part.Mesh.bounds);}
                Vector3 lowMin=Vector3.one*float.PositiveInfinity,lowMax=Vector3.one*float.NegativeInfinity,highMin=lowMin,highMax=lowMax;
                // Union all surfaces. End-ring centres exclude off-axis armour when fitting sleeves.
                foreach(var part in kit.Parts)if(part.Label==label)foreach(var v in part.Mesh.vertices)
                {
                    if(v.y<=bounds.min.y+.003f){lowMin=Vector3.Min(lowMin,v);lowMax=Vector3.Max(lowMax,v);}
                    if(v.y>=bounds.max.y-.003f){highMin=Vector3.Min(highMin,v);highMax=Vector3.Max(highMax,v);}
                }
                Vector3 low=(lowMin+lowMax)*.5f,high=(highMin+highMax)*.5f;low.y=bounds.min.y;high.y=bounds.max.y;return new JointSpan(low,high);
            }
            public JointGeometry(Kit kit)
            {
                // Jacket bounds include every surface; no ChestPlate is required for a neck joint.
                bool first=true;Bounds jacket=new Bounds();foreach(var part in kit.Parts)if(part.Label=="Jacket"){if(first){jacket=part.Mesh.bounds;first=false;}else jacket.Encapsulate(part.Mesh.bounds);}
                JacketMin=jacket.min;JacketMax=jacket.max;
                // Hair, masks and plumes can hang below the neck. Use the central throat
                // cross-section, sized from the jacket width/depth, rather than their lowest ornament.
                float radius=Mathf.Clamp(Mathf.Min(jacket.size.x,jacket.size.z)*.2f,.04f,.10f),bottom=float.PositiveInfinity;
                foreach(var part in kit.Parts)if(part.Label=="AvatarHead")foreach(var v in part.Mesh.vertices)if(v.x*v.x+v.z*v.z<=radius*radius)bottom=Mathf.Min(bottom,v.y);
                if(float.IsPositiveInfinity(bottom))NeckBase=Span(kit,"AvatarHead").From;
                else
                {
                    Vector3 low=Vector3.one*float.PositiveInfinity,high=Vector3.one*float.NegativeInfinity;
                    foreach(var part in kit.Parts)if(part.Label=="AvatarHead")foreach(var v in part.Mesh.vertices)if(v.x*v.x+v.z*v.z<=radius*radius&&v.y<=bottom+.003f){low=Vector3.Min(low,v);high=Vector3.Max(high,v);}
                    NeckBase=(low+high)*.5f;NeckBase.y=bottom;
                }
                for(int i=0;i<2;i++)
                {
                    string prefix=i==0?"L":"R",upper=prefix+"UpperArm";
                    Upper[i]=Span(kit,upper);Forearm[i]=Span(kit,prefix+"Forearm");
                    foreach(var part in kit.Parts)if(part.Label==upper){SleeveUv[i]=SleeveClothUv(part.Mesh,Upper[i]);break;}
                }
            }
        }
        sealed partial class Kit {public JointGeometry Joints;}
        readonly Transform[] jointUpper=new Transform[2],jointForearm=new Transform[2];
        readonly Vector3[] jointBend=new Vector3[2],jointForearmAxis=new Vector3[2],jointWrist=new Vector3[2];
        Transform jointJacket,jointHead;JointGeometry joints;
        void CacheJointGeometry()
        {
            if(current.Kit.Joints==null)current.Kit.Joints=new JointGeometry(current.Kit);
            joints=current.Kit.Joints;jointJacket=current.Parts["Jacket"];jointHead=current.Parts["AvatarHead"];
            for(int i=0;i<2;i++)
            {
                string prefix=i==0?"L":"R";jointUpper[i]=current.Parts[prefix+"UpperArm"];jointForearm[i]=current.Parts[prefix+"Forearm"];
                var w=i==0?current.Profile.leftWrist:current.Profile.rightWrist;jointWrist[i]=w!=null&&w.Length>=3?new Vector3(w[0],w[1],w[2]):Vector3.zero;
                jointBend[i]=Vector3.zero;jointForearmAxis[i]=Vector3.zero;
            }
            CacheArmSurfaces();
        }
        void PresentJointHead(Quaternion uprightYaw)
        {
            Vector3 collar=R26AvatarJointMath.Collar(joints.NeckBase,head.position,uprightYaw,jointJacket.position,jointJacket.rotation,jointJacket.localScale,joints.JacketMin,joints.JacketMax);
            Vector3 at;Quaternion rotation;R26AvatarJointMath.Head(joints.NeckBase,collar,head.position,head.rotation,uprightYaw,out at,out rotation);
            jointHead.SetPositionAndRotation(at,rotation);jointHead.localScale=Vector3.one;
        }
        void PresentJointArm(int i,Vector3 shoulder,Vector3 rightAxis,Vector3 rear)
        {
            float side=i==0?-1:1;Vector3 s=shoulder+rightAxis*side*.205f;Transform hand=i==0?left:right;
            // All outfits use the same glove art. Centre the cuff on its oval
            // back face, slightly lower than the authored wrist locator.
            Vector3 local=jointWrist[i]+new Vector3(side*.0024f,-.0025f,.0025f);
            Vector3 wrist=Vector3.zero,knuckle=Vector3.zero,edge=Vector3.zero;
            bool art=handArtPointReader!=null&&handArtPointReader(i,local,out wrist)
                &&handArtPointReader(i,local+Vector3.forward*.05f,out knuckle)&&handArtPointReader(i,local+Vector3.right*.05f,out edge);
            if(!art)
            {
                wrist=hand.TransformPoint(local)+(i==0?leftPresentationOffset:rightPresentationOffset);
                knuckle=wrist+hand.TransformVector(Vector3.forward*.05f);edge=wrist+hand.TransformVector(Vector3.right*.05f);
            }
            // Solve and retain redundant IK directions in the torso frame.
            Quaternion body=Quaternion.LookRotation(-rear,Vector3.up),inverse=Quaternion.Inverse(body);
            Vector3 handAxis=(knuckle-wrist).normalized,handRight=(edge-wrist).normalized;
            Vector3 pole=new Vector3(side*.75f,-.55f,-.3f);
            Vector3 elbow;R26AvatarJointMath.Arm(Vector3.zero,inverse*(wrist-s),inverse*handAxis,pole,ref jointBend[i],ref jointForearmAxis[i],out elbow);
            Vector3 seat=s-rightAxis*(side*.105f)-jointJacket.up*.032f;
            PresentArmSurface(i,seat,s,s+body*elbow,wrist,handAxis,handRight,Mathf.Clamp((edge-wrist).magnitude*20,.5f,2),pole,body);
        }
        static void JointSegment(Transform part,JointSpan span,Vector3 from,Vector3 to,float diameter,Quaternion body)
        {
            Vector3 at,scale;Quaternion rotation;Quaternion inverse=Quaternion.Inverse(body);
            R26AvatarJointMath.FitSegment(span.From,span.To,inverse*from,inverse*to,diameter,out at,out rotation,out scale);
            part.SetPositionAndRotation(body*at,body*rotation);part.localScale=scale;
        }
    }
#endif
}
#endif
