using System;
using Ashfall.Modules.Avatar;
namespace Ashfall.Integration.AvatarPose
{
    public sealed class PoseIdentity
    {
        public readonly string Session, Actor, CanonicalActor, AssemblyHash, Style, Tier, Export, ProfileHash;
        public readonly long Epoch, Connection, Tracking;
        public PoseIdentity(string session,string actor,string canonical,long epoch,long connection,long tracking,
            AvatarAssembly assembly,AvatarManifest manifest,SourceBodyProfile profile)
        {
            Need(assembly!=null&&profile!=null&&manifest!=null,"assembly/profile"); profile.Require(assembly);
            Need(ManifestXml.ContentHash(manifest)==assembly.ContentSha256,"export substitution");string export=manifest.ExportVersion;
            foreach(string s in new[]{session,actor,canonical,export})Need(!String.IsNullOrWhiteSpace(s)&&s.Length<=256,"identity budget");
            Need(epoch>=0&&connection>=0&&tracking>=0,"generation");
            Session=session;Actor=actor;CanonicalActor=canonical;Epoch=epoch;Connection=connection;Tracking=tracking;
            AssemblyHash=assembly.ContentSha256;Style=assembly.StyleId;Tier=assembly.Tier;Export=export;ProfileHash=profile.SourceSha256;
        }
        public bool Same(PoseIdentity b) {return b!=null&&Session==b.Session&&Actor==b.Actor&&CanonicalActor==b.CanonicalActor&&Epoch==b.Epoch&&Connection==b.Connection&&Tracking==b.Tracking&&AssemblyHash==b.AssemblyHash&&Style==b.Style&&Tier==b.Tier&&Export==b.Export&&ProfileHash==b.ProfileHash;}
        internal static void Need(bool ok,string reason){if(!ok)throw new AvatarValidationException(reason);}
    }
    public sealed class PoseFrame
    {
        public readonly PoseIdentity Identity; public readonly long Sequence;
        public readonly FrozenSkinPose Left,Right; public readonly bool FirstPerson;
        readonly float[][] world;
        public PoseFrame(PoseIdentity identity,long sequence,float[][] poses,VerifiedSkin left,ISkinPose leftPose,VerifiedSkin right,ISkinPose rightPose,bool firstPerson)
        {
            PoseIdentity.Need(identity!=null&&sequence>=0&&poses!=null&&poses.Length==16,"complete frame");
            // Shape and numeric admission precede owned clones. Trusted callbacks may allocate externally.
            foreach(float[] p in poses)PoseIdentity.Need(p!=null&&p.Length==16&&AvatarMath.ValidTrs(p),"world TRS");
            PoseIdentity.Need(left!=null&&right!=null&&leftPose!=null&&rightPose!=null,"two hands");
            var l=leftPose.CopyLocalMatrices();var r=rightPose.CopyLocalMatrices();
            PoseIdentity.Need(l!=null&&r!=null&&l.Length==left.NodeCount&&r.Length==right.NodeCount,"hand counts");
            Left=new FrozenSkinPose(left,l);Right=new FrozenSkinPose(right,r);
            world=new float[16][];for(int i=0;i<16;i++)world[i]=(float[])poses[i].Clone();
            Identity=identity;Sequence=sequence;FirstPerson=firstPerson;
        }
        public float[][] CopyWorld(){var a=new float[16][];for(int i=0;i<16;i++)a[i]=(float[])world[i].Clone();return a;}
    }
    // One arbiter per AvatarView. A stale lease has no hide or disposal authority.
    public sealed class PoseLease
    {
        object owner,connection;Ashfall.Modules.Sessions.Session boundSession;PoseIdentity identity;long sequence=-1;
        public object Bind(PoseIdentity next){if(next==null)throw new ArgumentNullException("next");owner=new object();identity=next;sequence=-1;connection=null;boundSession=null;return owner;}
        // Captures the actual opaque incarnation on the trusted lane; numeric labels attest nothing.
        public object Bind(PoseIdentity next,Ashfall.Modules.Sessions.Session session)
        {
            if(!SessionPoseGuard.Current(session,next))throw new AvatarValidationException("No current connection");
            object live=session.GetConnectionIdentity(next.Actor);
            if(live==null)throw new AvatarValidationException("No connection identity");
            object token=Bind(next);boundSession=session;connection=live;return token;
        }
        public bool ValidateCurrent(object token,Ashfall.Modules.Sessions.Session session)
        {
            if(!Owns(token))return false;
            if(connection!=null&&Object.ReferenceEquals(boundSession,session)&&SessionPoseGuard.Current(session,identity)&&
               Object.ReferenceEquals(connection,session.GetConnectionIdentity(identity.Actor)))return true;
            Revoke(token);return false;
        }
        public bool Owns(object token){return token!=null&&Object.ReferenceEquals(token,owner);}
        public bool Accept(object token,PoseFrame frame){if(!Owns(token)||frame==null||!identity.Same(frame.Identity)||frame.Sequence<=sequence)return false;sequence=frame.Sequence;return true;}
        public bool AcceptCurrent(object token,PoseFrame frame,Ashfall.Modules.Sessions.Session session,bool physicalTracking)
        {return ValidateCurrent(token,session)&&physicalTracking&&Accept(token,frame);}
        public void Revoke(object token){if(Owns(token)){owner=null;identity=null;connection=null;boundSession=null;sequence=-1;}}
    }
}
