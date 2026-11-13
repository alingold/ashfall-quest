using System;
using System.IO;
using Ashfall.Core;
using Ashfall.RecoveryContracts;
using Ashfall.Modules.WeaponHandling;
public static class LifetimeCheckpointTests
{
    sealed class Host : IAuthorityGate,IWeaponLifetimeAuthority
    {
        public RuleResult Validate(CommandContext c){return c.IsAuthority?RuleResult.Accept():RuleResult.Reject("fixture authority");}
        public RuleResult AuthorizeNew(CommandContext c,string id,WeaponMechanism m,int n){return Validate(c);}
        public RuleResult AuthorizeRestore(CommandContext c,string id,string actor,long epoch){return Validate(c);}
    }
    static int checks;
    static void A(bool ok,string label){checks++;if(!ok)throw new Exception(label);}
    static CommandContext C(string id,long tick=10){return new CommandContext("session","actor",id,tick,1,true);}
    static CheckpointBinding Binding(){return new CheckpointBinding("fixture-build","fixture-catalog","session","actor",1,2,3,4);}
    static LifetimeCheckpointIdentity[] Ids(){return new[]{new LifetimeCheckpointIdentity("instance","finite-entitlement",Binding())};}
    static bool Reject(Action action){try{action();return false;}catch(InvalidDataException){return true;}}
    public static void Main(string[] args)
    {
        var h=new Host();var b=Binding();var ledger=new WeaponLifetimes("session",h);WeaponHandling weapon;
        A(ledger.CreateNew(C("grant"),"instance",WeaponMechanism.PrecisionRifle,3,new FiniteAmmoReserve(20,11),h,out weapon).Accepted,"initial fixture grant only");
        IPreparedWeaponTransfer transfer;
        A(ledger.PrepareTransfer(C("move"),"instance","other",2,out transfer).Accepted&&transfer.Validate().Accepted,"actual validated lifetime reservation");
        A(Reject(delegate{ledger.CaptureRegistryCheckpoint(b,Ids());}),"validated transfer must block capture");transfer.Abort();
        IPreparedWeaponSuspension suspension;
        A(weapon.PrepareSuspension(out suspension).Accepted&&suspension.Validate().Accepted,"actual validated suspension");
        A(Reject(delegate{ledger.CaptureRegistryCheckpoint(b,Ids());}),"validated suspension must block capture");suspension.Abort();
        byte[] image=ledger.CaptureRegistryCheckpoint(b,Ids());
        if(args.Length>0&&args[0]=="write"){
            File.WriteAllBytes(args[1],image);Console.WriteLine("writer active checkpoint persisted; no root adoption");return;
        }
        if(args.Length>0&&args[0]=="read")image=File.ReadAllBytes(args[1]);
        var fresh=WeaponLifetimes.RestoreQuarantinedRegistry(image,b,h,h);
        WeaponHandling restored;WeaponTransfer pending;string actor;long epoch;
        A(fresh.TryReadCheckpointMember("instance",out restored,out pending,out actor,out epoch)&&restored!=null&&pending==null&&actor=="actor"&&epoch==1,"actual fresh registry binding");
        int reserve;long revision;A(restored.TryReadReserve(out reserve,out revision)&&reserve==11&&restored.Snapshot.Loaded==3,"exact finite reserve plus rounds");
        A(!fresh.CreateNew(C("second-grant"),"instance",WeaponMechanism.PrecisionRifle,3,new FiniteAmmoReserve(20,11),h,out weapon).Accepted,"stable identity tombstone denies second grant");
        A(!fresh.Capture(C("grant"),"instance",out pending).Accepted,"original command history retained");
        A(fresh.Capture(C("capture",11),"instance",out pending).Accepted,"legacy pending disposition created");
        byte[] suspended=fresh.CaptureRegistryCheckpoint(b,Ids());var reopened=WeaponLifetimes.RestoreQuarantinedRegistry(suspended,b,h,h);
        A(reopened.TryReadCheckpointMember("instance",out restored,out pending,out actor,out epoch)&&restored==null&&pending!=null,"pending stays pending across decode");
        A(!fresh.Restore(C("foreign",12),"instance",pending,out restored).Accepted,"cold receipt cannot bind old registry");
        A(reopened.Restore(C("resume",12),"instance",pending,out restored).Accepted&&restored.Snapshot.Loaded==3,"actual pending resume preserves rounds");
        A(!reopened.Restore(C("resume-again",12),"instance",pending,out restored).Accepted,"consumed receipt tombstone");
        var truncated=new byte[image.Length-1];Array.Copy(image,truncated,truncated.Length);
        A(Reject(delegate{WeaponLifetimes.RestoreQuarantinedRegistry(truncated,b,h,h);}),"truncation rejected");
        var trailing=new byte[image.Length+1];Array.Copy(image,trailing,image.Length);
        A(Reject(delegate{WeaponLifetimes.RestoreQuarantinedRegistry(trailing,b,h,h);}),"trailing bytes rejected");
        A(Reject(delegate{ledger.CaptureRegistryCheckpoint(b,new LifetimeCheckpointIdentity[0]);}),"missing registry coverage rejects");
        Console.WriteLine("PASS "+checks+" actual lifetime codec assertions; host authority is a fixture; world adoption OPEN");
    }
}
