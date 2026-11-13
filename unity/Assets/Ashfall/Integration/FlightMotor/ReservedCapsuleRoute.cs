using System;
using System.IO;
using Ashfall.Core;
using Ashfall.Modules.Flight;
using Ashfall.Integration.MotionRoutes;
namespace Ashfall.Integration.FlightMotor
{
    // Exact immutable CPU route capability. Only the concrete synthetic owner can mint it.
    // This is never a native collision or canonical Session authority certificate.
    public sealed class ReservedCapsuleRoute : ICapsuleRouteValidation, IFlightWorld
    {
        internal readonly SyntheticCapsuleRoutes Owner;
        internal readonly object Actor;
        internal readonly long GeometryRevision;
        public readonly CapsulePlan Plan;
        public readonly string SourceSha256, WorldSha256, BodyId;
        public readonly long Life, Connection, Epoch, Expires;
        internal bool Revoked;
        internal ReservedCapsuleRoute(SyntheticCapsuleRoutes owner,object actor,CapsulePlan plan,byte[] bytes,string body,long life,long connection,long epoch,long expires,long revision,string world)
        {Owner=owner;Actor=actor;Plan=plan;SourceSha256=Ashfall.Modules.World.WorldLoader.Hash(bytes);BodyId=body;Life=life;Connection=connection;Epoch=epoch;Expires=expires;GeometryRevision=revision;WorldSha256=world;}
        public bool ValidateRoute(string actorId,CapsulePlan plan)
        {return Object.ReferenceEquals(Plan,plan)&&Owner.Current(this,actorId);}
        public bool ClearPadContact(string id,Float3 face,Float3 contact){return false;}
        public bool ClearCapsuleExit(string actor,Float3 destination)
        {return Owner.Current(this,actor)&&Owner.Clear(destination,destination);}
        public void Revoke(){Revoked=true;}
        public static byte[] Encode(CapsulePlan p)
        {
            if(p==null)throw new ArgumentNullException("p");
            using(var s=new MemoryStream())using(var w=new BinaryWriter(s))
            {w.Write("ashfall-capsule-route/1");w.Write(p.PodId);w.Write(p.Team);w.Write(p.Slot);Write(w,p.Start);Write(w,p.Clear);Write(w,p.Cruise);Write(w,p.End);Write(w,p.ExitTarget);Write(w,p.RearDirection);w.Write(p.Duration);w.Flush();return s.ToArray();}
        }
        static void Write(BinaryWriter w,Float3 p){w.Write(p.X);w.Write(p.Y);w.Write(p.Z);}
    }
}
