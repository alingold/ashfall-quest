using System;
using System.IO;
using System.Security.Cryptography;
using Ashfall.Core;
using Ashfall.Modules.Networking;
using Ashfall.Modules.Sessions;
using Ashfall.Integration.Sessions;
namespace Ashfall.Integration.SessionReplication
{
    public static class SourceStateHello
    {
        public const string Extension="sessions.source-state/1";
        public static byte[] Encode(string actor,string session,string content,long epoch)
        {return Wire.Encode(new Packet{Kind=PacketKind.Hello,ActorId=actor,SessionId=session,ContentHash=content,Epoch=epoch,FeatureId=Extension});}
        internal static bool Matches(byte[] bytes,AuthenticatedPeer peer)
        {Packet p;return peer!=null&&Wire.TryDecode(bytes,out p)&&p.Kind==PacketKind.Hello&&p.Protocol==Protocol.Name&&p.Schema==Protocol.Schema&&
            p.ActorId==peer.Actor&&p.SessionId==peer.Session&&p.ContentHash==peer.Content&&p.Epoch==peer.Epoch&&p.FeatureId==Extension&&p.Payload.Length==0;}
    }
    public sealed class SourceStateHost
    {
        private readonly SessionReplicationHost admission;
        private readonly SessionNetworkCoordinator sessions;
        private readonly SessionSourceObservations source;
        private readonly PeerBinding binding;
        private readonly string actor,authority;
        private long lease,sequence=-1,request,minimum;
        private SourceStateView previous;
        private bool busy;
        private double lastNow=-1,lastReply=Double.NegativeInfinity;
        private SessionNetworkCoordinator.ObsoleteBindingReceipt retirement;
        public SourceStateHost(SessionNetworkCoordinator sessions,SessionSourceObservations source,IAuthenticatedPeerPort peers,string actor,string authority,string session,string content,long epoch)
        {if(source==null)throw new ArgumentNullException("source");this.sessions=sessions;this.source=source;this.actor=actor;this.authority=authority;
         binding=new PeerBinding(peers,session,content,epoch);admission=new SessionReplicationHost(sessions,peers,actor,authority,session,content,epoch);}
        public RuleResult Admit(string connection,byte[] hello,JoinRequest join,long tick,out string token,out byte[] response)
        {
            token=null;response=null;if(busy)return RuleResult.Reject("reentry");busy=true;
            try {var p=binding.Resolve(connection,false);if(p==null||p.Actor!=actor||!SourceStateHello.Matches(hello,p))return RuleResult.Reject("negotiation");
                byte[] legacy;var r=admission.Admit(connection,SessionReplicationHello.Encode(actor,binding.Session,binding.Content,binding.Epoch),join,tick,out token,out legacy);
                if(!r.Accepted)return r;binding.Connection=connection;binding.Record=p;lease=sessions.ConnectionGeneration(connection);
                retirement=sessions.CaptureBindingRetirement(connection,lease,p.Generation);
                if(retirement==null)throw new InvalidOperationException("retirement capture");
                response=SourceStateHello.Encode(authority,binding.Session,binding.Content,binding.Epoch);return r;}
            finally{busy=false;}
        }
        private bool Current(string connection)
        {return sessions.Epoch==binding.Epoch&&!admission.DeparturePending&&binding.Current(connection,false)&&sessions.ConnectionGeneration(connection)==lease&&sessions.Get(actor)!=null;}
        private RuleResult Build(CommandContext host,bool full,long id,out byte[][] frames,long watermark=-1)
        {
            frames=null;SourceStateView next;RuleResult r=source.Capture(host,out next);if(!r.Accepted)return r;
            long nextSequence=source.Sequence;
            if(nextSequence<watermark)return RuleResult.Reject("minimum-sequence");
            if(!full&&nextSequence==sequence)return RuleResult.Reject("unchanged");
            byte[] image=SourceStateCodec.Encode(next,full?null:previous);byte[] digest;using(var sha=SHA256.Create())digest=sha.ComputeHash(image);
            int count=(image.Length+SourceStateCodec.Chunk-1)/SourceStateCodec.Chunk;var result=new byte[count][];
            for(int i=0;i<count;i++)using(var m=new MemoryStream())using(var w=new BinaryWriter(m))
            {w.Write((byte)1);w.Write((ushort)i);w.Write((ushort)count);w.Write(image.Length);w.Write(digest);w.Write(image,i*SourceStateCodec.Chunk,Math.Min(SourceStateCodec.Chunk,image.Length-i*SourceStateCodec.Chunk));
             result[i]=ResyncWire.EncodeState(binding.Record.Generation,id,new Packet{Kind=full?PacketKind.Snapshot:PacketKind.Delta,SessionId=binding.Session,ContentHash=binding.Content,
                Epoch=binding.Epoch,ActorId=authority,FeatureId=SourceStateHello.Extension,Sequence=nextSequence,BaseSequence=full?-1:sequence,Payload=m.ToArray()});}
            // Commit only after every packet is successfully bounded/encoded.
            if(!Current(binding.Connection))return RuleResult.Reject("principal");
            previous=next;sequence=nextSequence;frames=result;return RuleResult.Accept();
        }
        public RuleResult Publish(string connection,CommandContext host,out byte[][] frames)
        {frames=null;if(busy)return RuleResult.Reject("reentry");busy=true;try{return Current(connection)?Build(host,previous==null,0,out frames):RuleResult.Reject("principal");}finally{busy=false;}}
        public RuleResult Respond(string connection,byte[] bytes,CommandContext host,double now,out byte[][] frames)
        {
            frames=null;if(busy)return RuleResult.Reject("reentry");busy=true;
            try {if(!Current(connection))return RuleResult.Reject("principal");if(Double.IsNaN(now)||Double.IsInfinity(now)||now<0||now<lastNow)return RuleResult.Reject("clock");lastNow=now;
                ResyncRequest q;if(!ResyncWire.TryDecodeRequest(bytes,out q)||q.Generation!=binding.Record.Generation)return RuleResult.Reject("request-generation");
                if(q.RequestId<request||q.RequestId==request&&q.MinimumSequence!=minimum)return RuleResult.Reject("request-replay");
                if(now-lastReply<.25)return RuleResult.Reject("rate");lastReply=now;
                // Reject impossible watermarks without publishing a replacement baseline.
                var r=Build(host,true,q.RequestId,out frames,q.MinimumSequence);if(r.Accepted){request=q.RequestId;minimum=q.MinimumSequence;}return r;
            }finally{busy=false;}
        }
        public RuleResult Disconnect(string connection,long generation,long tick,bool kicked)
        {if(busy)return RuleResult.Reject("reentry");busy=true;try{var r=admission.Disconnect(connection,generation,tick,kicked);if(binding.Record!=null&&binding.Connection==connection&&binding.Record.Generation==generation)binding.Record=null;return r;}finally{busy=false;}}
        public RuleResult RetireObsolete(long currentEpoch)
        {if(busy)return RuleResult.Reject("reentry");busy=true;try{var r=sessions.RetireObsoleteBinding(retirement,currentEpoch);if(r.Accepted)binding.Record=null;return r;}finally{busy=false;}}
        public RuleResult ReceiveCommand(string connection,long generation,byte[] data,long tick){return admission.ReceiveCommand(connection,generation,data,tick);}
        public bool DispatchOne(out RuleResult r){return admission.DispatchOne(out r);}
    }
}
