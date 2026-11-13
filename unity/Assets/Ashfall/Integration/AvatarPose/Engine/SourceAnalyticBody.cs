#if UNITY_6000_0_OR_NEWER
using System;
using Ashfall.Modules.Avatar;
using Ashfall.Modules.Player;
using UnityEngine;
namespace Ashfall.Integration.AvatarPose
{
    public interface ISourceGroundQuery
    {
        // Query-only actual world implementation; false means unavailable, never fabricated ground.
        bool TryGround(Vector3 from,Vector3 to,Transform excludedActor,out bool hit,out Vector3 point);
    }
    // Exact player_body.gd analytic primitive convention, quarantined until immutable
    // accepted export pivot descriptors exist. Not a universal skinned-body solver.
    public sealed class SourceAnalyticBody:IBodyPoseSource,IBodyPartVisibility
    {
        public string AssemblyHash {get;private set;} public string ProfileHash {get;private set;}
        string[] suppressed=new string[0];
        public string[] CopySuppressedParts(){return (string[])suppressed.Clone();}
        readonly SourceBodyProfile profile;readonly VerifiedSkin left,right;readonly Matrix4x4 lm,rm;
        readonly ISourceGroundQuery ground;float phase;Quaternion yaw=Quaternion.identity;
        public SourceAnalyticBody(AvatarAssembly assembly,SourceBodyProfile profile,VerifiedSkin left,VerifiedSkin right,ISourceGroundQuery ground)
        {
            profile.Require(assembly);
            if(assembly.ProductionApproved)throw new InvalidOperationException("Accepted rig pivot descriptors unavailable; analytic pilot only");
            if(ground==null)throw new ArgumentNullException("ground");
            this.profile=profile;this.left=left;this.right=right;this.ground=ground;AssemblyHash=assembly.ContentSha256;ProfileHash=profile.SourceSha256;
            lm=Matrix(SourceGripMount.Create(left,true));rm=Matrix(SourceGripMount.Create(right,false));
            foreach(var p in assembly.CopyParts())if(p.Label=="LeftHand"||p.Label=="RightHand"){
                var wanted=p.Label=="LeftHand"?lm:rm;var actual=p.CopyMount();for(int i=0;i<16;i++)if(Mathf.Abs(actual[i]-wanted[i/4,i%4])>1e-6f)throw new InvalidOperationException("Source glove mount must be present exactly once in assembly");
            }
            foreach(var p in assembly.CopyParts())if(p.Label=="AvatarHead"){
                var expected=RenderMounts.StandingHeadToEye();var actual=p.CopyMount();for(int i=0;i<16;i++)if(Mathf.Abs(actual[i]-expected[i])>1e-6f)throw new InvalidOperationException("Standing head mount must be applied exactly once by AvatarView");
            }
        }
        public bool TrySample(UnityPlayerDriver d,FrozenSkinPose l,FrozenSkinPose r,float delta,out float[][] body)
        {
            body=null;var hidden=new System.Collections.Generic.List<string>();Vector3 head=d.Head.position;float eye=Mathf.Clamp(head.y-d.transform.position.y,.55f,2.2f);
            Vector3 forward=d.Head.forward;forward.y=0;if(forward.sqrMagnitude>.001f)yaw=Quaternion.LookRotation(forward.normalized,Vector3.up);
            Vector3 rightAxis=yaw*Vector3.right,rear=yaw*Vector3.back;
            Vector3 hip=new Vector3(head.x,d.transform.position.y+Mathf.Min(eye*.49f,eye-.28f),head.z)+rear*profile.BodyRear;
            Vector3 shoulder=head-Vector3.up*Mathf.Min(profile.EyeToShoulder,eye*.25f)+rear*profile.BodyRear;
            float chest=Mathf.Max(.16f,shoulder.y-hip.y);var frame=new Matrix4x4[13];
            frame[0]=Matrix4x4.TRS(hip+Vector3.up*chest*.5f,yaw,new Vector3(1,chest,1));
            frame[1]=Matrix4x4.TRS(hip+Vector3.up*chest*.58f-rear*.18f,yaw,new Vector3(1,Mathf.Min(1,chest/.30f),1));
            if(profile.ChestPlateFollowsJacket)frame[1]=frame[0]*Matrix4x4.TRS(new Vector3(0,.04f/.55f,.17f),Quaternion.identity,new Vector3(1,1/.55f,1));
            frame[2]=Matrix4x4.TRS(hip,yaw,Vector3.one);
            var cc=d.GetComponent<CharacterController>();if(cc==null)return false;
            float speed=d.Paused?0:new Vector2(cc.velocity.x,cc.velocity.z).magnitude;
            float nextPhase=phase+Mathf.Max(0,delta)*Mathf.Min(speed,10)*2.2f;
            for(int i=0;i<2;i++){
                float side=i==0?-1:1;int start=i==0?3:8;
                Vector3 s=shoulder+rightAxis*side*.205f;
                Vector3 wrist=d.UseXr?Wrist(i==0?left:right,i==0?l:r,i==0?d.LeftHand:d.RightHand,i==0?lm:rm,i==0):head+yaw*new Vector3(side*.29f,-.46f,.40f);
                Vector3 offset=wrist-s;float distance=Mathf.Max(.001f,offset.magnitude),length=Mathf.Max(.32f,distance*.501f);Vector3 axis=offset/distance;
                Vector3 hint=rightAxis*side*.75f+rear*.3f-Vector3.up*.55f,bend=hint-axis*Vector3.Dot(hint,axis);if(bend.sqrMagnitude<.001f)bend=Vector3.Cross(axis,Vector3.up);bend.Normalize();
                Vector3 elbow=s+axis*distance*.5f+bend*Mathf.Sqrt(Mathf.Max(0,length*length-distance*distance*.25f));
                // Suppress each eye-enveloping sleeve independently, preserving the rest of the body.
                if(Distance(head,s,elbow)<=.17f)hidden.Add(i==0?"LUpperArm":"RUpperArm");
                if(Distance(head,elbow,wrist)<=.17f)hidden.Add(i==0?"LForearm":"RForearm");
                frame[start]=Segment(s,elbow,.105f);frame[start+1]=Segment(elbow,wrist,.085f);
                float wave=Mathf.Sin(nextPhase+i*Mathf.PI),stride=wave*Mathf.Min(speed*.045f,.25f);
                Vector3 foot=new Vector3(head.x,d.transform.position.y+.07f,head.z)+rightAxis*side*.115f+rear*(.10f+stride),point;bool hit;
                if(!ground.TryGround(foot+Vector3.up*.35f,foot-Vector3.up*.5f,d.transform,out hit,out point))return false;
                if(hit)foot.y=point.y+.07f;foot.y+=Mathf.Max(0,wave)*Mathf.Min(speed*.012f,.065f);
                Vector3 leg=hip+rightAxis*side*.105f,knee=(leg+foot)*.5f-rear*Mathf.Min(.12f,eye*.10f);
                frame[start+2]=Segment(leg,knee,.14f);frame[start+3]=Segment(knee,foot,.115f);frame[start+4]=Matrix4x4.TRS(foot-rear*.045f,yaw,Vector3.one);
            }
            var output=new float[13][];for(int i=0;i<13;i++){output[i]=NativePoseBinding.Values(frame[i]);if(!AvatarMath.ValidTrs(output[i]))return false;}
            phase=nextPhase;suppressed=hidden.ToArray();body=output;return true;
        }
        static Vector3 Wrist(VerifiedSkin skin,FrozenSkinPose pose,Transform hand,Matrix4x4 mount,bool left){int node=Array.IndexOf(skin.CopyNames(),left?"Wrist_L":"Wrist_R");var world=skin.WorldMatrices(pose.CopyLocalMatrices());return (hand.localToWorldMatrix*mount*Matrix(world[node])).MultiplyPoint3x4(Vector3.zero);}
        static float Distance(Vector3 p,Vector3 a,Vector3 b){Vector3 v=b-a;return Vector3.Distance(p,a+v*Mathf.Clamp01(Vector3.Dot(p-a,v)/Mathf.Max(v.sqrMagnitude,1e-12f)));}
        static Matrix4x4 Segment(Vector3 a,Vector3 b,float width){Vector3 v=b-a;return Matrix4x4.TRS((a+b)*.5f,v.sqrMagnitude>1e-6f?Quaternion.FromToRotation(Vector3.up,v.normalized):Quaternion.identity,new Vector3(width,Mathf.Max(.001f,v.magnitude),width));}
        static Matrix4x4 Matrix(float[] a){var m=new Matrix4x4();for(int r=0;r<4;r++)for(int c=0;c<4;c++)m[r,c]=a[r*4+c];return m;}
    }
}
#endif
