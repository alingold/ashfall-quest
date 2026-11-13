using System;
using System.IO;
using System.Collections.Generic;
using Ashfall.Core;
using Ashfall.RecoveryContracts;
using Wire=Ashfall.RecoveryContracts.RecoveryWire;
namespace Ashfall.Modules.Pickups
{
    // Owner facts from trusted Register; this does not approve map/catalog provenance.
    public sealed class CanonicalPickupSource
    {
        public readonly string InstanceId,Kind,SemanticOwnerId,Owner,ReservedBy;
        public readonly int Amount;
        public readonly Float3 Position;
        public readonly bool Removed;
        public readonly long ReservationUntil,Revision;
        internal CanonicalPickupSource(string id,string kind,string semantic,string owner,string reserved,int amount,Float3 position,bool removed,long until,long revision)
        {InstanceId=id;Kind=kind;SemanticOwnerId=semantic;Owner=owner;ReservedBy=reserved;Amount=amount;Position=position;Removed=removed;ReservationUntil=until;Revision=revision;}
    }
    public sealed partial class PickupRules
    {
        private bool quarantined;
        private long revision;
        public bool IsQuarantined {get {lock(sync)return quarantined;}}
        public bool HasRetainedCommand(string actor,string command)
        {lock(sync){Dictionary<string,long> history;return actor!=null&&command!=null&&seen.TryGetValue(actor,out history)&&history.ContainsKey(command);}}
        public bool TryReadCanonicalSource(string id,out CanonicalPickupSource source)
        {lock(sync){source=null;Item i;if(id==null||!items.TryGetValue(id,out i))return false;source=new CanonicalPickupSource(i.Id,i.Kind,i.Semantic,i.Owner,i.Reserved,i.Amount,i.Position,i.Removed,i.Until,revision);return true;}}
        private static void Nullable(BinaryWriter w,string s){w.Write(s!=null);if(s!=null)Wire.Text(w,s);}
        private static string Nullable(BinaryReader r){return Wire.Bool(r)?Identity(r):null;}
        private static string Identity(BinaryReader r){string s=Wire.Text(r);Wire.Require(Id(s));return s;}
        private static void Vector(BinaryWriter w,Float3 v){w.Write(v.X);w.Write(v.Y);w.Write(v.Z);}
        private static Float3 Vector(BinaryReader r){return new Float3(Wire.Float(r),Wire.Float(r),Wire.Float(r));}
        // Preserve the actual owner traversal order (not a new alphabetical order).
        private static string[] Keys<T>(Dictionary<string,T> d){return new List<string>(d.Keys).ToArray();}
        private static float Positive(BinaryReader r){float f=Wire.Float(r);Wire.Require(f>=0);return f;}
        internal byte[] CaptureOwner(CheckpointBinding binding)
        {
            lock(sync){
                Wire.Require(binding!=null&&binding.Session==session&&binding.Epoch==epoch&&!executing&&pendingSupply==null&&items.Count<=8192&&users.Count<=64&&seen.Count<=64&&replayCount<=4096);
                CheckFields();
                using(var m=new MemoryStream())using(var w=new BinaryWriter(m)){
                    Wire.Text(w,PickupCheckpointCodec.Version);binding.Write(w);w.Write(hz);w.Write(replayCapacity);w.Write(maxActors);w.Write(maxItems);w.Write(now);w.Write(revision);
                    w.Write(items.Count);foreach(string id in Keys(items)){Item i=items[id];Wire.Text(w,i.Id);Wire.Text(w,i.Kind);Wire.Text(w,i.Semantic);Nullable(w,i.Owner);Nullable(w,i.Reserved);Vector(w,i.Position);w.Write(i.Until);w.Write(i.Removed);w.Write(i.Amount);}
                    w.Write(users.Count);foreach(string actor in Keys(users)){UseState u=users[actor];Wire.Text(w,actor);Nullable(w,u.Held);w.Write((int)u.Phase);w.Write(u.Peels);w.Write(u.Reversals);w.Write(u.Distance);w.Write(u.Seconds);w.Write(u.Soda);Vector(w,u.Previous);Vector(w,u.LastMotion);Vector(w,u.PeelStart);w.Write(u.HaveSample);w.Write(u.Peeling);w.Write(u.Pressed);w.Write(u.AwaitRelease);w.Write(u.SampleTick);}
                    w.Write(seen.Count);foreach(string actor in Keys(seen)){Wire.Text(w,actor);var commands=seen[actor];w.Write(commands.Count);foreach(string id in Keys(commands)){Wire.Text(w,id);w.Write(commands[id]);}}
                    w.Flush();byte[] image=m.ToArray();Wire.Require(image.Length<=Wire.Limit);return image;
                }
            }
        }
        internal static PickupRules RestoreOwner(byte[] image,CheckpointBinding binding,IAuthorityGate gate,IPickupActors actors,IPickupTransactions transactions,IInteractionQuery query)
        {
            Wire.Require(binding!=null);using(var r=Wire.Reader(image)){
                Wire.Require(Wire.Text(r)==PickupCheckpointCodec.Version);binding.Check(r);
                int hz=r.ReadInt32(),replay=r.ReadInt32(),maxActors=r.ReadInt32(),maxItems=r.ReadInt32();
                Wire.Require(hz>=1&&hz<=1000&&replay>0&&maxActors>0&&maxItems>0);
                var p=new PickupRules(binding.Session,binding.Epoch,hz,gate,actors,transactions,query,replay,maxActors,maxItems);p.quarantined=true;p.now=r.ReadInt64();p.revision=r.ReadInt64();Wire.Require(p.now>=0&&p.revision>=0);CheckFields();
                int n=Wire.Count(r,Math.Min(maxItems,8192));for(int j=0;j<n;j++){
                    var i=new Item{Id=Identity(r),Kind=Identity(r),Semantic=Identity(r),Owner=Nullable(r),Reserved=Nullable(r),Position=Vector(r),Until=r.ReadInt64(),Removed=Wire.Bool(r),Amount=r.ReadInt32()};
                    Wire.Require(!p.items.ContainsKey(i.Id)&&i.Until>=0&&(i.Until<=p.now||i.Until-p.now<=p.lease)&&i.Amount>=0&&(IsConsumable(i.Kind)||i.Kind=="backpack"||SupplyAmount(i.Kind)>0)&&(SupplyAmount(i.Kind)>0||i.Amount==0)&&(!i.Removed||i.Owner==null)&&(i.Owner==null||IsConsumable(i.Kind))&&(i.Owner==null||i.Reserved==null));p.items.Add(i.Id,i);
                }
                n=Wire.Count(r,Math.Min(maxActors,64));for(int j=0;j<n;j++){
                    string actor=Identity(r);var u=new UseState{Held=Nullable(r),Phase=(Preparation)r.ReadInt32(),Peels=r.ReadInt32(),Reversals=r.ReadInt32(),Distance=Positive(r),Seconds=Positive(r),Soda=Positive(r),Previous=Vector(r),LastMotion=Vector(r),PeelStart=Vector(r),HaveSample=Wire.Bool(r),Peeling=Wire.Bool(r),Pressed=Wire.Bool(r),AwaitRelease=Wire.Bool(r),SampleTick=r.ReadInt64()};
                    Wire.Require(!p.users.ContainsKey(actor)&&u.Phase>=Preparation.Idle&&u.Phase<=Preparation.Consume&&u.Peels>=0&&u.Peels<=4&&u.Reversals>=0&&u.SampleTick>=0&&u.SampleTick<=p.now);
                    if(u.Held!=null){Item i;Wire.Require(p.items.TryGetValue(u.Held,out i)&&i.Owner==actor&&!i.Removed&&IsConsumable(i.Kind)&&
                        (i.Kind=="shaker"?(u.Phase==Preparation.Shake||u.Phase==Preparation.Consume):(u.Phase==Preparation.Open||u.Phase==Preparation.Consume))&&
                        (!u.Peeling||i.Kind=="banana"));}else Wire.Require(u.Phase==Preparation.Idle);
                    p.users.Add(actor,u);
                }
                n=Wire.Count(r,Math.Min(maxActors,64));for(int j=0;j<n;j++){
                    string actor=Identity(r);Wire.Require(!p.seen.ContainsKey(actor));var commands=new Dictionary<string,long>(StringComparer.Ordinal);int count=Wire.Count(r,Math.Min(replay,4096)-p.replayCount);
                    for(int k=0;k<count;k++){string id=Identity(r);long tick=r.ReadInt64();Wire.Require(!commands.ContainsKey(id)&&tick>=0&&tick<=p.now);commands.Add(id,tick);}p.replayCount+=count;p.seen.Add(actor,commands);
                }
                foreach(var item in p.items.Values)Wire.Require((item.Owner==null||p.users.ContainsKey(item.Owner))&&(item.Reserved==null||p.users.ContainsKey(item.Reserved))&&(!item.Removed||item.Reserved==null));
                foreach(string actor in p.users.Keys)Wire.Require(p.seen.ContainsKey(actor));
                Wire.End(r);return p;
            }
        }
        // Explicit owner schema census. Unknown compiled private state refuses capture.
        private static void Fields(Type type,params string[] fields){Array.Sort(fields,StringComparer.Ordinal);Wire.Fields(type,String.Join("\n",fields));}
        private static void CheckFields()
        {
            Fields(typeof(Item),"Id:System.String","Kind:System.String","Semantic:System.String","Owner:System.String","Reserved:System.String","Position:Ashfall.Core.Float3","Until:System.Int64","Removed:System.Boolean","Amount:System.Int32");
            Fields(typeof(UseState),"Held:System.String","Phase:Ashfall.Modules.Pickups.Preparation","Peels:System.Int32","Reversals:System.Int32","Distance:System.Single","Seconds:System.Single","Soda:System.Single","Previous:Ashfall.Core.Float3","LastMotion:Ashfall.Core.Float3","PeelStart:Ashfall.Core.Float3","HaveSample:System.Boolean","Peeling:System.Boolean","Pressed:System.Boolean","AwaitRelease:System.Boolean","SampleTick:System.Int64");
            Fields(typeof(PickupRules),"sync:System.Object","items:System.Collections.Generic.Dictionary`2[System.String,Ashfall.Modules.Pickups.PickupRules+Item]","users:System.Collections.Generic.Dictionary`2[System.String,Ashfall.Modules.Pickups.PickupRules+UseState]","seen:System.Collections.Generic.Dictionary`2[System.String,System.Collections.Generic.Dictionary`2[System.String,System.Int64]]","actors:Ashfall.Modules.Pickups.IPickupActors","transactions:Ashfall.Modules.Pickups.IPickupTransactions","query:Ashfall.Core.IInteractionQuery","gate:Ashfall.Core.IAuthorityGate","session:System.String","epoch:System.Int64","hz:System.Int32","replayCapacity:System.Int32","maxActors:System.Int32","maxItems:System.Int32","window:System.Int64","lease:System.Int64","now:System.Int64","replayCount:System.Int32","executing:System.Boolean","quarantined:System.Boolean","revision:System.Int64","supplyLifetime:Ashfall.Modules.Pickups.IPickupSupplyLifetime","pendingSupply:Ashfall.Modules.Pickups.PickupRules+PreparedSupplyRemoval");
        }
    }
    public static class PickupCheckpointCodec
    {
        public const string Version="PickupCustody/schema1";
        public static byte[] Capture(PickupRules rules,CheckpointBinding binding){Wire.Require(rules!=null);return rules.CaptureOwner(binding);}
        // Parsing/factory only; trusted digest validation belongs to Integration.
        public static PickupRules Restore(byte[] image,CheckpointBinding binding,IAuthorityGate gate,IPickupActors actors,IPickupTransactions transactions,IInteractionQuery query)
        {Wire.Require(image!=null);return PickupRules.RestoreOwner((byte[])image.Clone(),binding,gate,actors,transactions,query);}
    }
}
