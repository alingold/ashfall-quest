using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Ashfall.Core;

namespace Ashfall.Modules.HealthStates
{
    // Cold reconstruction only. Never applies an old snapshot to a live service.
    public sealed partial class HealthService
    {
        public byte[] CreateCheckpoint(CommandContext host,string sourceBinding)
        {
            if(durable!=null)throw new HealthStorageException("use DurableManifest and RecoverDurableImages; raw checkpoint restore would permit a stale prefix");return Checkpoint(host,sourceBinding,false);
        }
        private byte[] Checkpoint(CommandContext host,string sourceBinding,bool closedRecovery,bool trustedStorage=false)
        {
            lock(sync) {
                CodecFields();
                if(host.ActorId!="host"||!trustedStorage&&!(closedRecovery?ClosedContext(host):Authorize(host).Accepted)||!CheckpointWire.Binding(sourceBinding)||HasPreparedPublication)
                    throw new InvalidOperationException("checkpoint-host-or-reservation");
                foreach(var reservation in respawnReservations)
                    if(reservation!=null&&reservation.Active(world.Now))throw new InvalidOperationException("checkpoint-lifecycle-reservation");
                using(var m=new BoundedCheckpointStream())using(var w=new BinaryWriter(m,Encoding.UTF8)) {
                    w.Write(2);w.Write(sourceBinding);w.Write(session);w.Write(epoch);w.Write(teams);w.Write(respawnMode);
                    w.Write(downedSeconds);w.Write(respawnDelay);w.Write(protection);w.Write(eventSequence);w.Write(latestTick);w.Write(lastTime);
                    w.Write(players.Count);
                    foreach(var p in players.Values) {
                        CheckpointWire.Snapshot(w,Read(p.Id));w.Write(p.HaveSample);w.Write(p.SampleAt);CheckpointWire.Vector(w,p.LastLeft);CheckpointWire.Vector(w,p.LastRight);
                    }
                    w.Write(seen.Count);foreach(var p in seen){w.Write(p.Key);w.Write(p.Value);}
                    w.Write(damage.Count);foreach(var p in damage){w.Write(p.Key);CheckpointWire.Damage(w,p.Value);}
                    w.Write(gains.Count);foreach(var p in gains){w.Write(p.Key);w.Write(p.Value.LogicalId);w.Write(p.Value.TargetId);w.Write((int)p.Value.Outcome);w.Write(p.Value.Health);w.Write(p.Value.Shield);}
                    w.Write(gainCommands.Count);foreach(var p in gainCommands)w.Write(p.Key);
                    w.Write(lifecycleCount);for(int i=0;i<lifecycleCount;i++)w.Write(lifecycleKeys[i]);
                    w.Write(transitions.Count);foreach(var p in transitions)CheckpointWire.Transition(w,p);
                    w.Write(epochIngressClosed);w.Write(epochAbortedFamily.HasValue?(int)epochAbortedFamily.Value:-1);
                    w.Flush();if(m.Length>CheckpointWire.MaximumBytes)throw new InvalidOperationException("checkpoint-size");return m.ToArray();
                }
            }
        }
        // Expected digest/binding must come from the coordinator's trusted checkpoint receipt.
        // A digest supplied by an untrusted sender is not authentication or freshness.
        public static bool TryRestoreCheckpoint(CommandContext host,string sourceBinding,string digest,byte[] bytes,
            int teamCount,bool respawn,IAuthorityGate authority,IHealthWorld environment,IInteractionQuery interactions,
            out HealthService restored,double downed=60,double respawnSeconds=5,double protectionSeconds=3)
        {
            restored=null;
            if(authority==null||environment==null||interactions==null||host.ActorId!="host"||!host.IsAuthority||
                !authority.Validate(host).Accepted||!CheckpointWire.Binding(sourceBinding)||!CheckpointWire.Matches(bytes,digest))return false;
            try {
                CodecFields();
                var s=new HealthService(host.SessionId,host.AuthorityEpoch,teamCount,respawn,authority,environment,interactions,downed,respawnSeconds,protectionSeconds);
                if(!s.Authorize(host).Accepted)return false;
                using(var m=new MemoryStream(bytes,false))using(var r=new HealthCheckpointReader(m)) {
                    if(r.ReadInt32()!=2||CheckpointWire.Text(r)!=sourceBinding||CheckpointWire.Text(r)!=s.session||r.ReadInt64()!=s.epoch||r.ReadInt32()!=teamCount||r.ReadBoolean()!=respawn||
                        r.ReadDouble()!=downed||r.ReadDouble()!=respawnSeconds||r.ReadDouble()!=protectionSeconds)throw new InvalidDataException("checkpoint-identity");
                    s.eventSequence=CheckpointWire.Number(r,0,Int64.MaxValue);s.latestTick=CheckpointWire.Number(r,-1,host.Tick);s.lastTime=r.ReadDouble();
                    if(!Finite(s.lastTime)||s.lastTime< -1||!Finite(environment.Now)||environment.Now<Math.Max(0,s.lastTime))throw new InvalidDataException("checkpoint-clock");
                    int n=CheckpointWire.Count(r,64);
                    for(int i=0;i<n;i++) {
                        var a=CheckpointWire.Snapshot(r);if(a==null||!s.TeamValid(a.Team))throw new InvalidDataException("checkpoint-player");
                        var p=new Player{Id=a.Id,Team=a.Team,Deaths=a.Deaths,Revision=a.Revision,LifeGeneration=a.LifeGeneration,ConnectionGeneration=a.ConnectionGeneration,Life=a.Life,
                            Health=a.Health,Shield=a.Shield,Charge=a.Charge,DownedUntil=a.DownedUntil,RespawnAt=a.RespawnAt,ProtectionUntil=a.ProtectionUntil,ReadyUntil=a.ReadyUntil,Connected=a.Connected,
                            HaveSample=r.ReadBoolean(),SampleAt=r.ReadDouble(),LastLeft=CheckpointWire.Vector(r),LastRight=CheckpointWire.Vector(r)};
                        if(!Finite(p.SampleAt)||p.SampleAt<0||p.SampleAt>Math.Max(0,s.lastTime))throw new InvalidDataException("checkpoint-sample");s.players.Add(p.Id,p);
                    }
                    n=CheckpointWire.Count(r,4096);for(int i=0;i<n;i++)s.seen.Add(CheckpointWire.Text(r),CheckpointWire.Number(r,0,s.latestTick));
                    n=CheckpointWire.Count(r,1000000);for(int i=0;i<n;i++){string key=CheckpointWire.Text(r);var value=CheckpointWire.Damage(r);if(value==null||!s.players.ContainsKey(value.TargetId))throw new InvalidDataException("checkpoint-receipt");s.damage.Add(key,value);}
                    n=CheckpointWire.Count(r,1000000);for(int i=0;i<n;i++) {
                        string key=CheckpointWire.Text(r),id=CheckpointWire.Text(r),target=CheckpointWire.Text(r);int outcome=CheckpointWire.Count(r,2);float h=CheckpointWire.Amount(r,100),sh=CheckpointWire.Amount(r,100);
                        if(!s.players.ContainsKey(target))throw new InvalidDataException("checkpoint-gain");s.gains.Add(key,new GainReceipt(id,target,(GainOutcome)outcome,h,sh));
                    }
                    n=CheckpointWire.Count(r,1000000);for(int i=0;i<n;i++){string gainKey=CheckpointWire.Text(r);s.gainCommands.Add(gainKey,true);}
                    s.lifecycleCount=CheckpointWire.Count(r,8192);for(int i=0;i<s.lifecycleCount;i++){string key=CheckpointWire.Text(r);for(int j=0;j<i;j++)if(s.lifecycleKeys[j]==key)throw new InvalidDataException("checkpoint-lifecycle-duplicate");s.lifecycleKeys[i]=key;}
                    n=CheckpointWire.Count(r,1024);long previous=0;for(int i=0;i<n;i++){var t=CheckpointWire.Transition(r);if(t.SessionId!=s.session||t.AuthorityEpoch!=s.epoch||t.Sequence<=previous||t.Sequence>s.eventSequence||!s.players.ContainsKey(t.After.Id))throw new InvalidDataException("checkpoint-outbox");s.transitions.Enqueue(t);previous=t.Sequence;}
                    s.epochIngressClosed=Ashfall.RecoveryContracts.RecoveryWire.Bool(r);
                    int family=r.ReadInt32();if(family< -1||family>2||!s.epochIngressClosed&&family!=-1)throw new InvalidDataException("checkpoint-closure");s.epochAbortedFamily=family<0?(HealthReservationFamily?)null:(HealthReservationFamily)family;
                    if(m.Position!=m.Length)throw new InvalidDataException("checkpoint-trailing");
                }
                restored=s;return true;
            }catch(InvalidDataException){return false;}catch(IOException){return false;}catch(ArgumentException){return false;}catch(OverflowException){return false;}
        }
        private bool ClosedContext(CommandContext c){return epochIngressClosed&&c.IsAuthority&&c.SessionId==session&&c.AuthorityEpoch==epoch&&c.ActorId=="host"&&!String.IsNullOrEmpty(c.CommandId)&&c.CommandId.Length<=128&&c.Tick>=0;}
        private sealed class BoundedCheckpointStream:MemoryStream {void Bound(long count){if(count>CheckpointWire.MaximumBytes)throw new InvalidOperationException("checkpoint-size");}public override void Write(byte[] buffer,int offset,int count){Bound(Position+count);base.Write(buffer,offset,count);}public override void WriteByte(byte value){Bound(Position+1);base.WriteByte(value);}public override void SetLength(long value){Bound(value);base.SetLength(value);}}
        private sealed class HealthCheckpointReader : BinaryReader {internal HealthCheckpointReader(Stream s):base(s,new UTF8Encoding(false,true)){}public override bool ReadBoolean(){return Ashfall.RecoveryContracts.RecoveryWire.Bool(this);}}
        internal long CheckpointSequence {get {lock(sync){return eventSequence;}}}
    }

    // Explicit bounded wire schema; no runtime type names, reflection or object deserialization.
    internal static class CheckpointWire
    {
        internal const int MaximumBytes=2097152;
        internal static bool Binding(string s){if(s==null||s.Length!=64)return false;foreach(char c in s)if(!Uri.IsHexDigit(c))return false;return true;}
        internal static string Hash(byte[] bytes){using(var h=SHA256.Create())return BitConverter.ToString(h.ComputeHash(bytes)).Replace("-","").ToLowerInvariant();}
        internal static bool Matches(byte[] bytes,string hash){return bytes!=null&&bytes.Length>0&&bytes.Length<=MaximumBytes&&hash!=null&&hash.Length==64&&String.Equals(Hash(bytes),hash,StringComparison.OrdinalIgnoreCase);}
        internal static int Count(BinaryReader r,int maximum){int n=r.ReadInt32();if(n<0||n>maximum)throw new InvalidDataException("checkpoint-count");return n;}
        internal static long Number(BinaryReader r,long minimum,long maximum){long n=r.ReadInt64();if(n<minimum||n>maximum)throw new InvalidDataException("checkpoint-number");return n;}
        internal static string Text(BinaryReader r){return Text(r,512,false);}
        internal static string Text(BinaryReader r,int maximum,bool empty)
        {
            uint length=0;int shift=0;
            while(true){byte b=r.ReadByte();if(shift==28&&(b&240)!=0)throw new InvalidDataException("checkpoint-string-length");length|=(uint)(b&127)<<shift;if((b&128)==0)break;shift+=7;if(shift>28)throw new InvalidDataException("checkpoint-string-length");}
            if(length>(uint)maximum*4||length>r.BaseStream.Length-r.BaseStream.Position)throw new InvalidDataException("checkpoint-string-length");
            byte[] bytes=r.ReadBytes((int)length);string s=new UTF8Encoding(false,true).GetString(bytes);
            if((!empty&&s.Length==0)||s.Length>maximum)throw new InvalidDataException("checkpoint-string");return s;
        }
        internal static float Amount(BinaryReader r,float max){float n=r.ReadSingle();if(Single.IsNaN(n)||Single.IsInfinity(n)||n<0||n>max)throw new InvalidDataException("checkpoint-amount");return n;}
        internal static double Time(BinaryReader r){double n=r.ReadDouble();if(Double.IsNaN(n)||Double.IsInfinity(n)||n<0)throw new InvalidDataException("checkpoint-time");return n;}
        internal static void Vector(BinaryWriter w,Float3 p){w.Write(p.X);w.Write(p.Y);w.Write(p.Z);}
        internal static Float3 Vector(BinaryReader r){var v=new Float3(r.ReadSingle(),r.ReadSingle(),r.ReadSingle());if(!v.IsFinite)throw new InvalidDataException("checkpoint-vector");return v;}
        internal static void Result(BinaryWriter w,RuleResult r){w.Write(r.Accepted);w.Write(r.Reason??"");}
        internal static RuleResult Result(BinaryReader r){bool ok=r.ReadBoolean();string reason=Text(r,512,true);if(ok&&reason!="")throw new InvalidDataException("checkpoint-result");return new RuleResult(ok,reason);}
        internal static void Snapshot(BinaryWriter w,HealthSnapshot s){w.Write(s!=null);if(s==null)return;w.Write(s.Id);w.Write(s.Team);w.Write(s.Deaths);w.Write(s.Revision);w.Write((int)s.Life);w.Write(s.Health);w.Write(s.Shield);w.Write(s.DownedUntil);w.Write(s.RespawnAt);w.Write(s.ProtectionUntil);w.Write(s.ReadyUntil);w.Write(s.Connected);w.Write(s.Charge);w.Write(s.LifeGeneration);w.Write(s.ConnectionGeneration);}
        internal static HealthSnapshot Snapshot(BinaryReader r){if(!r.ReadBoolean())return null;string id=Text(r);int team=r.ReadInt32(),deaths=Count(r,Int32.MaxValue);long rev=Number(r,1,Int64.MaxValue);var life=(LifeState)Count(r,3);float hp=Amount(r,100),sh=Amount(r,100);double d=Time(r),spawn=Time(r),protection=Time(r),ready=Time(r);bool connected=r.ReadBoolean();float charge=Amount(r,1);long lg=Number(r,1,Int64.MaxValue),cg=Number(r,1,Int64.MaxValue);if(id.Length>128||life==LifeState.Alive&&hp<=0||life!=LifeState.Alive&&(hp!=0||sh!=0))throw new InvalidDataException("checkpoint-life");return new HealthSnapshot(id,team,deaths,rev,life,hp,sh,d,spawn,protection,ready,connected,charge,lg,cg);}
        internal static void Damage(BinaryWriter w,DamageReceipt d){w.Write(d!=null);if(d==null)return;w.Write(d.LogicalId);w.Write(d.TargetId);Result(w,d.Result);w.Write(d.Retryable);Snapshot(w,d.Snapshot);}
        internal static DamageReceipt Damage(BinaryReader r){if(!r.ReadBoolean())return null;string id=Text(r),target=Text(r);var result=Result(r);bool retry=r.ReadBoolean();var snapshot=Snapshot(r);if(snapshot!=null&&snapshot.Id!=target)throw new InvalidDataException("checkpoint-damage-target");return new DamageReceipt(id,target,result,retry,snapshot);}
        internal static void Transition(BinaryWriter w,HealthTransition t){w.Write(t.SessionId);w.Write(t.AuthorityEpoch);w.Write(t.Sequence);w.Write(t.Kind);w.Write(t.BeforeRevision);Snapshot(w,t.After);}
        internal static HealthTransition Transition(BinaryReader r){string session=Text(r);long epoch=Number(r,0,Int64.MaxValue),seq=Number(r,1,Int64.MaxValue);string kind=Text(r);long before=Number(r,0,Int64.MaxValue);var after=Snapshot(r);if(after==null||after.Revision!=before+1)throw new InvalidDataException("checkpoint-transition");return new HealthTransition(session,epoch,seq,kind,before,after);}
    }
}

