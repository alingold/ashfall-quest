using System;
using System.Collections.Generic;
using Ashfall.Core;
using Ashfall.Modules.Construction;
using Ashfall.Integration.ConstructionRuntime;
public static class LifecycleGates
{
    // Reuse real World serialization/binding fixtures and their deterministic deck.
    // These delegates are external canonical state ports, not live network evidence.
    public static int Run(Func<FixtureHostClock,RuntimeHost> create,Func<float,BuilderState> pose)
    {
        int count=0;Action<bool,string> check=(b,n)=>{if(!b)throw new Exception(n);count++;Console.WriteLine("PASS "+n);};
        Action<FixtureHostClock>[] changes={c=>c.Life++,c=>c.Connection++,c=>c.Root++,c=>c.Pose++,c=>c.Authority="authority/2",c=>c.Active=false,c=>c.Now=21};
        foreach(var change in changes)
        {
            var c=new FixtureHostClock("s");var h=create(c);ConstructionLease old,fresh;
            check(h.Claim("actor","left",pose(1),20,out old),"claim current canonical stamp");
            change(c);var cmd=new ConstructionCommand(new CommandContext("s","actor","lifecycle",0,3,true),0);
            check(!h.Place(cmd,old).Accepted&&h.Snapshot().Length==0&&h.Charges("actor")==999&&h.Pending()==null,"stale queued intent preserves panels cost outbox");
            check(h.Preview("actor",old)==null,"stale preview rejected");
            c.Active=true;check(h.Claim("actor","right",pose(1),40,out fresh),"fresh hand reacquisition");
            check(!h.Place(cmd,old).Accepted,"reacquisition does not authorize queued previous token");
            check(h.Place(cmd,fresh).Accepted&&h.Charges("actor")==998&&h.Snapshot().Length==1,"fresh token mutates actual construction");
        }
        {
            var c=new FixtureHostClock("s");var h=create(c);ConstructionLease a,b;h.Claim("actor","left",pose(1),20,out a);h.Release("actor");h.Claim("actor","left",pose(1),20,out b);
            var cmd=new ConstructionCommand(new CommandContext("s","actor","release",0,3,true),0);
            check(!h.Place(cmd,a).Accepted&&h.Place(cmd,b).Accepted,"release ABA token invalidation");
            check(!h.Place(cmd).Accepted,"legacy unbound placement fails closed");
        }
        {
            var c=new FixtureHostClock("s");c.Now=10;var h=create(c);ConstructionLease a;h.Claim("actor","left",pose(1),20,out a);c.Now=9;
            check(h.Preview("actor",a)==null,"clock regression fails closed");c.Now=11;
            check(!h.Claim("actor","left",pose(1),20,out a),"regressed clock cannot silently rearm host");
        }
        return count;
    }
}
