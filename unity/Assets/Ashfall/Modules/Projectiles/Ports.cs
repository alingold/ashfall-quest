using Ashfall.Core;
namespace Ashfall.Modules.Projectiles
{
    public enum RadialPolicy { SourceThroughWallsUnresolved, StructuralLineOfSightStaged, SourceRadialV1 }
    public enum DeliveryDisposition { Applied, TerminalNoOp, Retryable, Expired }
    public static class ProjectileDelivery
    {
        // Unknown producer failures stay pending until the original event expires.
        public static DeliveryDisposition Classify(RuleResult result)
        {
            if(result.Accepted)return DeliveryDisposition.Applied;
            switch(result.Reason) {
                case "event-expired": return DeliveryDisposition.Expired;
                case "state": case "friendly-fire": case "occluded":
                case "collision-lifecycle": case "loss-or-protection":
                case "Collision target lifecycle": return DeliveryDisposition.TerminalNoOp;
                default: return DeliveryDisposition.Retryable;
            }
        }
    }
    public interface ISourceProjectileDamage
    {
        RuleResult RegisterSource(CommandContext host,string launchId,long serial,long expires,string instance,string owner,string kind,long generation);
    }
    // Trusted host-only ports; never bind these methods to client dispatch. Queries must
    // return complete bounded results, include structures, and be pure under the host lane.
    public interface IProjectileWorld
    {
        string MapRevision { get; }
        bool Sweep(Float3 from,Float3 to,float radius,string excludedOwner,out Contact contact);
        bool Overlap(Float3 center,float radius,out Target[] targets);
    }
    public sealed class Contact
    {
        public readonly float Fraction;
        public readonly Float3 Normal;
        public readonly string BodyId;
        public Contact(float fraction,Float3 normal,string bodyId){Fraction=fraction;Normal=normal;BodyId=bodyId;}
    }
    public sealed class Target
    {
        public readonly string Id,Provenance;
        public readonly Float3 Center;
        public Target(string id,Float3 center,string provenance){Id=id;Center=center;Provenance=provenance;}
    }
    public interface IProjectileDamage
    {
        RuleResult Register(CommandContext host,string launchId,long serial,long expires,string instance,string owner,string kind,long generation);
        RuleResult Radial(CommandContext host,string launchId,int subhit,Float3 center,float radius,float power,bool falloff,Target[] targets,RadialPolicy policy);
    }
}
