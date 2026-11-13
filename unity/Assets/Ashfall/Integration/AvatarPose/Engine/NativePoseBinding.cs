#if UNITY_6000_0_OR_NEWER
using System;
using System.Collections.Generic;
using Ashfall.Modules.Avatar;
using Ashfall.Modules.Player;
using Ashfall.Modules.Sessions;
using UnityEngine;
using UnityEngine.XR;
namespace Ashfall.Integration.AvatarPose
{
    // Trusted exact-export body producer. No default body/ground/pivot success.
    public interface IBodyPoseSource
    {
        string AssemblyHash {get;} string ProfileHash {get;}
        bool TrySample(UnityPlayerDriver driver, FrozenSkinPose left,FrozenSkinPose right,float delta,out float[][] body);
    }
    public interface IBodyPartVisibility { string[] CopySuppressedParts(); }
    public sealed class NativePoseBinding
    {
        static readonly System.Runtime.CompilerServices.ConditionalWeakTable<AvatarView,NativePoseBinding> owners=new System.Runtime.CompilerServices.ConditionalWeakTable<AvatarView,NativePoseBinding>();
        readonly AvatarView view;readonly PoseLease lease=new PoseLease();
        Provider current;
        public NativePoseBinding(AvatarView target){if(target==null)throw new ArgumentNullException("target");view=target;owners.Add(target,this);}
        public Provider Bind(UnityPlayerDriver driver,Session session,PoseIdentity identity,AvatarAssembly assembly,
            SourceBodyProfile profile,VerifiedSkin left,VerifiedSkin right,SourceGripPose leftGrip,SourceGripPose rightGrip,
            IBodyPoseSource body,IGripContactSource contacts,AvatarRenderReceipts receipts,bool pilot)
        {
            if(driver==null||session==null||identity==null||body==null||contacts==null||left==null||right==null||leftGrip==null||rightGrip==null)throw new ArgumentNullException();
            if(!SessionPoseGuard.Current(session,identity)||session.GetConnectionIdentity(identity.Actor)==null||!driver.isActiveAndEnabled)
                throw new InvalidOperationException("Live connection/source attestation required");
            object connection=session.GetConnectionIdentity(identity.Actor),source=driver.PoseSourceIdentity;
            profile.Require(assembly);
            if(assembly.ProductionApproved)throw new InvalidOperationException("Production pose producer/pivot/generation attestation remains unbound");
            foreach(var part in assembly.CopyParts()){if(part.Label=="LeftHand")left.RequireBinding(part);if(part.Label=="RightHand")right.RequireBinding(part);}
            if(identity.AssemblyHash!=assembly.ContentSha256||identity.ProfileHash!=profile.SourceSha256||body.AssemblyHash!=assembly.ContentSha256||body.ProfileHash!=profile.SourceSha256)throw new InvalidOperationException("Exact body/profile identity required");
            if(!assembly.ProductionApproved&&!pilot)throw new InvalidOperationException("Quarantine requires explicit opt-in");
            // Failed replacement leaves former binding/resources intact.
            view.Assemble(assembly,pilot,receipts);
            if(current!=null)current.Dispose();
            object token=lease.Bind(identity,session);
            current=new Provider(this,token,driver,session,identity,left,right,leftGrip,rightGrip,body,contacts,connection,source);
            return current;
        }
        void Hide(object token){if(lease.Owns(token)&&view!=null)view.HidePresentation();}
        public sealed class Provider:IDisposable
        {
            readonly NativePoseBinding host;readonly object token;readonly UnityPlayerDriver driver;readonly Session session;
            readonly PoseIdentity identity;readonly VerifiedSkin left,right;readonly SourceGripPose leftGrip,rightGrip;readonly IBodyPoseSource body;
            readonly object connection,source;readonly IGripContactSource contacts;long sequence;int lastUnityFrame=-1;bool disposed;
            internal Provider(NativePoseBinding host,object token,UnityPlayerDriver driver,Session session,PoseIdentity identity,VerifiedSkin left,VerifiedSkin right,SourceGripPose lg,SourceGripPose rg,IBodyPoseSource body,IGripContactSource contacts,object connection,object source)
            {this.connection=connection;this.source=source;driver.PoseSourceInvalidated+=OnSourceInvalidated;this.host=host;host.view.PresentationInvalidated+=OnSourceInvalidated;this.token=token;this.driver=driver;this.session=session;this.identity=identity;this.left=left;this.right=right;leftGrip=lg;rightGrip=rg;this.body=body;this.contacts=contacts;}
            void OnSourceInvalidated(){Dispose();}
            internal bool MatchesDriver(UnityPlayerDriver candidate)
            {return !disposed&&host.lease.Owns(token)&&System.Object.ReferenceEquals(driver,candidate)&&RefreshLifetime();}
            // Session has no notification port: coordinator must call this after membership publication.
            public bool RefreshLifetime()
            {
                if(disposed||!host.lease.Owns(token))return false;
                if(driver==null||!driver.isActiveAndEnabled||!SessionPoseGuard.Current(session,identity)||!System.Object.ReferenceEquals(source,driver.PoseSourceIdentity)||
                   !System.Object.ReferenceEquals(connection,session.GetConnectionIdentity(identity.Actor))||!host.lease.ValidateCurrent(token,session))
                {Dispose();return false;}return true;
            }
            public void Hide(){host.Hide(token);}
            // Coordinator calls AFTER actual motor/root application, on Unity's main lane.
            // Never use CurrentFrame.HeadPosition: it was sampled before motor movement.
            public bool Pump(float delta,bool firstPerson)
            {
                if(!RefreshLifetime())return false;
                host.Hide(token);
                if(lastUnityFrame==Time.frameCount)return false;lastUnityFrame=Time.frameCount;
                var member=session.Get(identity.Actor);
                if(driver==null||!driver.isActiveAndEnabled||session.Id!=identity.Session||session.Epoch!=identity.Epoch||member==null||member.CanonicalActorId!=identity.CanonicalActor||!PhysicalPose(driver)){Dispose();return false;}
                if(!Finite(delta)||delta<0||delta>.1f)return false;
                try {
                    // Safe input snapshot: paused actions are zero, while physical view stays live.
                    var input=driver.CurrentFrame;
                    bool actions=driver.InteractionsAllowed&&!input.Paused;
                    GripContact lc,rc;if(!contacts.TryRead(true,out lc)||!contacts.TryRead(false,out rc)||lc==null||rc==null)return false;
                    var lp=leftGrip.Update(actions&&input.GripLeft?1:0,actions&&input.TriggerLeft?1:0,lc.Role,lc.Profile,lc.SourceId,delta);
                    var rp=rightGrip.Update(actions&&input.GripRight?1:0,actions&&input.TriggerRight?1:0,rc.Role,rc.Profile,rc.SourceId,delta);
                    float[][] poses;if(!body.TrySample(driver,lp,rp,driver.Paused?0:delta,out poses)||poses==null||poses.Length!=13)return false;
                    var full=new float[16][];Array.Copy(poses,full,13);
                    full[13]=Values(driver.Head.localToWorldMatrix);full[14]=Values(driver.LeftHand.localToWorldMatrix);full[15]=Values(driver.RightHand.localToWorldMatrix);
                    var visibility=body as IBodyPartVisibility;
                    string[] suppressed=visibility==null?null:visibility.CopySuppressedParts();
                    if(suppressed!=null){if(suppressed.Length>16)return false;suppressed=(string[])suppressed.Clone();}
                    var packet=new PoseFrame(identity,++sequence,full,left,lp,right,rp,firstPerson);
                    if(!RefreshLifetime()||!host.lease.AcceptCurrent(token,packet,session,true))return false;
                    // Both skins validated before native mutation. Hide persists on any failure.
                    host.view.PresentHandPose("LeftHand",packet.Left);host.view.PresentHandPose("RightHand",packet.Right);
                    var frame=new Dictionary<string,Matrix4x4>(StringComparer.Ordinal);var labels=AvatarAssembler.RequiredLabels();var frozen=packet.CopyWorld();
                    for(int i=0;i<16;i++)frame.Add(labels[i],Matrix(frozen[i]));
                    host.view.Present(frame,true,firstPerson,suppressed);return true;
                }catch(Exception){host.Hide(token);return false;}
            }
            public void Dispose(){if(disposed)return;disposed=true;host.view.PresentationInvalidated-=OnSourceInvalidated;if(!System.Object.ReferenceEquals(driver,null))driver.PoseSourceInvalidated-=OnSourceInvalidated;host.Hide(token);host.lease.Revoke(token);}
        }
        static bool PhysicalPose(UnityPlayerDriver d)
        {
            if(Vector3.Dot(d.transform.up,Vector3.up)<.99999f)return false;
            if(d.Head==null||d.LeftHand==null||d.RightHand==null||d.TrackingSpace==null||d.Head==d.LeftHand||d.Head==d.RightHand||d.LeftHand==d.RightHand||d.Head.parent!=d.TrackingSpace||d.LeftHand.parent!=d.TrackingSpace||d.RightHand.parent!=d.TrackingSpace||!d.TrackingSpace.IsChildOf(d.transform))return false;
            foreach(var t in new[]{d.transform,d.Head,d.LeftHand,d.RightHand}){
                for(var p=t;p!=null;p=p.parent)if((p.localScale-Vector3.one).sqrMagnitude>1e-8f||!AvatarMath.ValidTrs(Values(p.localToWorldMatrix)))return false;
            }
            if(!d.UseXr)return true;
            return Tracked(XRNode.Head)&&Tracked(XRNode.LeftHand)&&Tracked(XRNode.RightHand);
        }
        static bool Tracked(XRNode node){var d=InputDevices.GetDeviceAtXRNode(node);bool tracked;InputTrackingState state;Vector3 p;Quaternion q;
            return d.isValid&&d.TryGetFeatureValue(CommonUsages.isTracked,out tracked)&&tracked&&d.TryGetFeatureValue(CommonUsages.trackingState,out state)&&(state&(InputTrackingState.Position|InputTrackingState.Rotation))==(InputTrackingState.Position|InputTrackingState.Rotation)&&d.TryGetFeatureValue(CommonUsages.devicePosition,out p)&&d.TryGetFeatureValue(CommonUsages.deviceRotation,out q)&&Finite(p.x)&&Finite(p.y)&&Finite(p.z)&&Finite(q.x)&&Finite(q.y)&&Finite(q.z)&&Finite(q.w)&&Mathf.Abs(q.x*q.x+q.y*q.y+q.z*q.z+q.w*q.w-1)<.001f;}
        static bool Finite(float f){return !Single.IsNaN(f)&&!Single.IsInfinity(f);}
        public static float[] Values(Matrix4x4 m){var a=new float[16];for(int r=0;r<4;r++)for(int c=0;c<4;c++)a[r*4+c]=m[r,c];return a;}
        static Matrix4x4 Matrix(float[] a){var m=new Matrix4x4();for(int r=0;r<4;r++)for(int c=0;c<4;c++)m[r,c]=a[r*4+c];return m;}
    }
}
#endif

