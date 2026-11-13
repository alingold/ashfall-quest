using System;
using Ashfall.Core;
using Ashfall.Modules.WeaponHandling;
class BeforeProbe {
 sealed class G:IAuthorityGate{public RuleResult Validate(CommandContext c){return RuleResult.Accept();}}
 sealed class A:IAmmoReserve{public int Count=100;public int Available{get{return Count;}}public int Take(int n){int a=Math.Min(n,Count);Count-=a;return a;}public int Refund(int n){Count+=n;return n;}}
 static HandlingInput I(){return new HandlingInput{Enabled=true,PrimaryTracked=true,SupportTracked=true,Desktop=true,ForegripDistance=1};}
 static RuleResult Do(WeaponHandling w,ref HandlingInput i,int n,HandlingAction a){i.Sequence=n;return w.Apply(new CommandContext("s","a","c"+n,n,1,true),a,i);}
 static void Main(){var a=new A();var g=new G();var w=new WeaponHandling("s","a",1,WeaponMechanism.ServiceRifle,a,g);var i=I();i.Trigger=true;Do(w,ref i,1,HandlingAction.Fire);i.DeltaSeconds=.05f;for(int n=2;n<=21;n++)Do(w,ref i,n,HandlingAction.Fire);Console.WriteLine("BEFORE cadence50ms="+w.Snapshot.Shots);w=new WeaponHandling("s","a",1,WeaponMechanism.ServiceRifle,a,g);i=I();Do(w,ref i,1,HandlingAction.ReloadStep);Do(w,ref i,2,HandlingAction.ReloadStep);Do(w,ref i,3,HandlingAction.ReloadStep);Do(w,ref i,4,HandlingAction.Sample);i.Trigger=true;Console.WriteLine("BEFORE zero-time post-rack accepted="+Do(w,ref i,5,HandlingAction.Fire).Accepted);Console.WriteLine("BEFORE bare new epoch loaded="+new WeaponHandling("s","a",2,WeaponMechanism.ServiceRifle,a,g).Snapshot.Loaded);w=new WeaponHandling("s","a",1,WeaponMechanism.ServiceRifle,a,g);i=I();i.Trigger=true;Console.WriteLine("BEFORE negative tick accepted="+w.Apply(new CommandContext("s","a","neg",-1,1,true),HandlingAction.Fire,i).Accepted);}
}
