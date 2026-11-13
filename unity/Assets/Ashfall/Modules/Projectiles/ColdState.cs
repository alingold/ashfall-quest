using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Reflection;
using System.Security.Cryptography;
using Ashfall.Core;
namespace Ashfall.Modules.Projectiles
{
    // Host-lane serialization only. Expected digest/build/catalog/freshness must come
    // from a trusted root receipt. This utility never authenticates such a receipt.
    internal static class ProjectileWire
    {
        internal const int Limit=4*1024*1024;
        internal sealed class BoundedStream : MemoryStream
        {
            public override void Write(byte[] b,int offset,int count){Need(count>=0&&Position<=Limit-count);base.Write(b,offset,count);}
            public override void WriteByte(byte b){Need(Position<Limit);base.WriteByte(b);}
        }
        internal static void Need(bool b){if(!b)throw new InvalidDataException("projectile-cold-state");}
        internal static void Fields(Type t,string fields){var a=new List<string>();foreach(var f in t.GetFields(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.DeclaredOnly))a.Add(f.Name);a.Sort(StringComparer.Ordinal);Need(String.Join(" ",a.ToArray())==fields);}
        internal static string Hash(byte[] b){using(var h=SHA256.Create())return BitConverter.ToString(h.ComputeHash(b)).Replace("-","").ToLowerInvariant();}
        internal static void Text(BinaryWriter w,string s){Need(s!=null);byte[] b=new UTF8Encoding(false,true).GetBytes(s);Need(b.Length<=1024);w.Write(b.Length);w.Write(b);}
        internal static string Text(BinaryReader r){int n=Count(r,1024);byte[] b=r.ReadBytes(n);Need(b.Length==n);return new UTF8Encoding(false,true).GetString(b);}
        internal static int Count(BinaryReader r,int max){int n=r.ReadInt32();Need(n>=0&&n<=max);return n;}
        internal static bool Bool(BinaryReader r){byte b=r.ReadByte();Need(b<2);return b==1;}
        internal static float Float(BinaryReader r){float f=r.ReadSingle();Need(ProjectileSimulation.Finite(f));return f;}
        internal static void V(BinaryWriter w,Float3 v){Need(v.IsFinite);w.Write(v.X);w.Write(v.Y);w.Write(v.Z);}
        internal static Float3 V(BinaryReader r){return new Float3(Float(r),Float(r),Float(r));}
    }
    public sealed partial class ProjectileSimulation
    {
        bool coldQuarantine;
        public bool ColdQuarantined {get{return coldQuarantine;}}
        public long LogicalTick {get{return tick;}}
        static void ColdFields()
        {
            ProjectileWire.Fields(typeof(ProjectileSimulation),"<LastFailure>k__BackingField capacity coldQuarantine damage epoch gate host map owners policy session shots tick world");
            ProjectileWire.Fields(typeof(Shot),"Age Born Command Ended Expires Generation Id Instance Kind LaunchOwner Pending Position Power Rarity Roll Serial StuckAt Velocity ZoneTicks");
            ProjectileWire.Fields(typeof(PendingBlast),"Attempt Center Falloff Power Radius Sub Targets");
        }
        // Does not call queries, damage, publication or any ammo API.
        public byte[] CaptureCold(CommandContext c,string build,string catalog,long generation)
        {
            ColdFields();ProjectileWire.Need(c.IsAuthority&&(coldQuarantine||Authorized(c))&&c.SessionId==session&&c.ActorId==host&&c.AuthorityEpoch==epoch&&c.Tick==tick&&generation>0);
            using(var stream=new ProjectileWire.BoundedStream())using(var w=new BinaryWriter(stream)) {
                w.Write(0x35435041);w.Write(1);ProjectileWire.Text(w,build);ProjectileWire.Text(w,catalog);w.Write(generation);
                ProjectileWire.Text(w,session);w.Write(epoch);ProjectileWire.Text(w,host);ProjectileWire.Text(w,map);w.Write(capacity);w.Write((int)policy);w.Write(tick);
                ProjectileWire.Text(w,LastFailure??"");w.Write(shots.Count);
                foreach(var s in shots.Values){ProjectileWire.Text(w,s.Id);ProjectileWire.Text(w,s.Kind);ProjectileWire.Text(w,s.Instance??"");ProjectileWire.Text(w,s.LaunchOwner);ProjectileWire.Text(w,s.Command);w.Write(s.Serial);w.Write(s.Generation);w.Write(s.Born);w.Write(s.Rarity);
                    ProjectileWire.V(w,s.Position);ProjectileWire.V(w,s.Velocity);w.Write(s.Age);w.Write(s.StuckAt);w.Write(s.ZoneTicks);w.Write(s.Power);w.Write(s.Roll);w.Write(s.Ended);w.Write(s.Expires);
                    string owner;bool admitted=owners.TryGetValue(s.Id,out owner);w.Write(admitted);if(admitted)ProjectileWire.Text(w,owner);
                    w.Write(s.Pending.Count);foreach(var b in s.Pending){w.Write(b.Sub);ProjectileWire.V(w,b.Center);w.Write(b.Radius);w.Write(b.Power);w.Write(b.Falloff);w.Write(b.Attempt);w.Write(b.Targets.Length);foreach(var t in b.Targets){ProjectileWire.Text(w,t.Id);ProjectileWire.V(w,t.Center);ProjectileWire.Text(w,t.Provenance);}}
                }
                w.Flush();ProjectileWire.Need(stream.Length<=ProjectileWire.Limit);return stream.ToArray();
            }
        }
        // Always fresh and permanently quarantined. No API accepts a bool/callback to
        // reopen it; Combat issuer/physical-policy restoration is still unavailable.
        public static ProjectileSimulation RestoreCold(byte[] image,string expectedDigest,string build,string catalog,long generation,
            string sessionId,long authorityEpoch,string hostActor,string mapRevision,IAuthorityGate authority,IProjectileWorld queries,IProjectileDamage application)
        {
            ColdFields();ProjectileWire.Need(image!=null&&image.Length<=ProjectileWire.Limit);image=(byte[])image.Clone();ProjectileWire.Need(ProjectileWire.Hash(image)==expectedDigest);
            using(var r=new BinaryReader(new MemoryStream(image,false),new UTF8Encoding(false,true))){
                ProjectileWire.Need(r.ReadInt32()==0x35435041&&r.ReadInt32()==1&&ProjectileWire.Text(r)==build&&ProjectileWire.Text(r)==catalog&&r.ReadInt64()==generation&&generation>0);
                ProjectileWire.Need(ProjectileWire.Text(r)==sessionId&&r.ReadInt64()==authorityEpoch&&ProjectileWire.Text(r)==hostActor&&ProjectileWire.Text(r)==mapRevision);
                int maximum=r.ReadInt32();int p=r.ReadInt32();ProjectileWire.Need(p==1||p==2);
                var result=new ProjectileSimulation(sessionId,authorityEpoch,hostActor,mapRevision,authority,queries,application,(RadialPolicy)p,maximum);result.coldQuarantine=true;result.tick=r.ReadInt64();ProjectileWire.Need(result.tick>=0);
                string failure=ProjectileWire.Text(r);result.LastFailure=failure.Length==0?null:failure;int count=ProjectileWire.Count(r,maximum);
                for(int i=0;i<count;i++){
                    var s=new Shot{Id=ProjectileWire.Text(r),Kind=ProjectileWire.Text(r),Instance=ProjectileWire.Text(r),LaunchOwner=ProjectileWire.Text(r),Command=ProjectileWire.Text(r),Serial=r.ReadInt64(),Generation=r.ReadInt64(),Born=r.ReadInt64(),Rarity=r.ReadInt32()};
                    ProjectileWire.Need(s.Id.Length>0&&s.Id.Length<=64&&s.LaunchOwner.Length>0&&s.Generation>0&&s.Born>=0&&s.Born<=result.tick&&s.Born<=Int64.MaxValue-600&&s.Rarity>=1&&s.Rarity<=5&&(s.Kind=="frag"||s.Kind=="smaw"||s.Kind=="zone")&&!result.shots.ContainsKey(s.Id));
                    s.Position=ProjectileWire.V(r);s.Velocity=ProjectileWire.V(r);s.Age=r.ReadInt32();s.StuckAt=r.ReadInt32();s.ZoneTicks=r.ReadInt32();s.Power=ProjectileWire.Float(r);s.Roll=ProjectileWire.Float(r);s.Ended=ProjectileWire.Bool(r);s.Expires=r.ReadInt64();
                    float power=s.Kind=="frag"?150:s.Kind=="zone"?5:new float[]{60,84,109,134,160}[s.Rarity-1];
                    // Live admission caps speed at240.001. Each gravity step adds
                    // .49; .001 per step conservatively covers float roundoff.
                    double maximumSpeed=240.001+(s.Kind=="smaw"?.001:.491)*s.Age;
                    ProjectileWire.Need(Length(s.Position)<=1000000&&s.Age>=0&&s.Age<=600&&Length(s.Velocity)<=maximumSpeed&&s.StuckAt>=-1&&s.StuckAt<=s.Age&&s.ZoneTicks>=0&&s.ZoneTicks<=20&&s.Power==power&&s.Roll>=0&&s.Expires==s.Born+600);
                    bool admitted=ProjectileWire.Bool(r);if(admitted){string owner=ProjectileWire.Text(r);ProjectileWire.Need(owner==s.LaunchOwner);result.owners.Add(s.Id,owner);}else ProjectileWire.Need(s.Ended);
                    int pending=ProjectileWire.Count(r,21);var subs=new HashSet<int>();for(int j=0;j<pending;j++){
                        var b=new PendingBlast{Sub=r.ReadInt32(),Center=ProjectileWire.V(r),Radius=ProjectileWire.Float(r),Power=ProjectileWire.Float(r),Falloff=ProjectileWire.Bool(r),Attempt=r.ReadInt64()};
                        ProjectileWire.Need(admitted&&b.Sub>=0&&b.Sub<=20&&subs.Add(b.Sub)&&b.Radius>0&&b.Radius<=15&&b.Power==s.Power&&b.Attempt>=-1&&b.Attempt<=result.tick&&Length(b.Center)<=1000000);
                        ProjectileWire.Need(b.Attempt>=s.Born&&Length(Sub(b.Center,s.Position))==0);
                        if(s.Kind=="zone")ProjectileWire.Need(b.Sub>=1&&b.Sub<=19&&b.Radius==15*(1-b.Sub/20f)&&!b.Falloff&&b.Sub<=s.ZoneTicks);
                        else ProjectileWire.Need(b.Sub==0&&b.Falloff&&b.Radius==(s.Kind=="frag"?10:4));
                        int targets=ProjectileWire.Count(r,256);b.Targets=new Target[targets];var ids=new HashSet<string>(StringComparer.Ordinal);for(int k=0;k<targets;k++){string id=ProjectileWire.Text(r);Float3 point=ProjectileWire.V(r);string provenance=ProjectileWire.Text(r);ProjectileWire.Need(id.Length>0&&id.Length<=64&&ids.Add(id)&&provenance==mapRevision&&Length(Sub(point,b.Center))<=b.Radius);b.Targets[k]=new Target(id,point,provenance);}s.Pending.Add(b);
                    }
                    result.shots.Add(s.Id,s);
                }
                ProjectileWire.Need(r.BaseStream.Position==r.BaseStream.Length);return result;
            }
        }
    }
}
