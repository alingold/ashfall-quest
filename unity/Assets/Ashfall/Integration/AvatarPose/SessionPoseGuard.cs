using Ashfall.Modules.Sessions;
namespace Ashfall.Integration.AvatarPose
{
    // Reads the real Session; no member feet -> remote head/hand reconstruction.
    public static class SessionPoseGuard
    {
        public static bool Current(Session session,PoseIdentity identity)
        {if(session==null||identity==null||session.Id!=identity.Session||session.Epoch!=identity.Epoch)return false;
         var m=session.Get(identity.Actor);return m!=null&&session.GetConnectionIdentity(identity.Actor)!=null&&m.CanonicalActorId==identity.CanonicalActor;}
    }
}
