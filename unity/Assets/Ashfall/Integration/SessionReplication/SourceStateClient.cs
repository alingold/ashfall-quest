using System;
using System.IO;
using System.Security.Cryptography;
using Ashfall.Core;
using Ashfall.Modules.Networking;
namespace Ashfall.Integration.SessionReplication
{
    // Single serialized consumer lane; one bounded incomplete image, atomic View swap.
    public sealed class SourceStateClient
    {
        private readonly IAuthenticatedPeerPort peers;
        private readonly string actor,session,content;
        private string connection,authority;
        private long generation,epoch,offeredEpoch,request=1;
        private bool connected,offered,busy;
        private double lastNow=-1,lastRequest=Double.NegativeInfinity;
        private long sentRequest;
        private sealed class Assembly
        {internal long Sequence,Base,Request;internal PacketKind Kind;internal int Count,Length,Received;internal byte[] Digest;internal byte[][] Chunks;}
        private Assembly pending;
        public SourceStateView View {get;private set;}
        public long Sequence {get;private set;}
        public bool NeedsSnapshot {get;private set;}
        public long Epoch {get{return epoch;}}
        public SourceStateClient(IAuthenticatedPeerPort peers,string actor,string session,string content,long epoch)
        {if(peers==null||!Wire.IsText(actor,64,false)||!Wire.IsText(session,64,false)||!Wire.IsHash(content)||epoch<0)throw new ArgumentException("configuration");
         this.peers=peers;this.actor=actor;this.session=session;this.content=content;this.epoch=epoch;Sequence=-1;NeedsSnapshot=true;}
        public byte[] Offer(long nextEpoch)
        {if(busy||nextEpoch<epoch)throw new InvalidOperationException("offer");offered=true;offeredEpoch=nextEpoch;return SourceStateHello.Encode(actor,session,content,nextEpoch);}
        private bool Current(string c)
        {AuthenticatedPeer p;return connected&&c==connection&&peers.TryResolve(c,out p)&&p!=null&&p.Authority&&p.Actor==authority&&p.Generation==generation&&p.Session==session&&p.Content==content&&p.Epoch==epoch;}
        public RuleResult Bind(string c,byte[] hello)
        {
            if(busy)return RuleResult.Reject("reentry");busy=true;
            try{AuthenticatedPeer p;if(!offered||!peers.TryResolve(c,out p)||p==null||!p.Authority||p.Generation<=generation||p.Epoch<epoch||p.Epoch!=offeredEpoch||p.Session!=session||p.Content!=content||
                authority!=null&&p.Actor!=authority||!SourceStateHello.Matches(hello,p))return RuleResult.Reject("negotiation");
                if(request==Int64.MaxValue)return RuleResult.Reject("request-capacity");
                if(p.Epoch>epoch){View=null;Sequence=-1;epoch=p.Epoch;}
                connection=c;authority=p.Actor;generation=p.Generation;connected=true;offered=false;pending=null;NeedsSnapshot=true;request++;sentRequest=0;lastRequest=Double.NegativeInfinity;return RuleResult.Accept();
            }finally{busy=false;}
        }
        private void Require(){pending=null;if(!NeedsSnapshot){if(request==Int64.MaxValue){connected=false;return;}request++;}NeedsSnapshot=true;}
        // Root calls on reliable-delivery timeout or lifecycle invalidation, including
        // loss of an entire final delta for which no later packet can expose a gap.
        public RuleResult Invalidate()
        {if(busy)return RuleResult.Reject("reentry");Require();return RuleResult.Accept();}
        public RuleResult Disconnect(string c,long g)
        {if(busy)return RuleResult.Reject("reentry");if(c!=connection||g!=generation)return RuleResult.Reject("generation");connected=false;Require();return RuleResult.Accept();}
        public RuleResult Request(double now,out byte[] bytes)
        {bytes=null;if(busy)return RuleResult.Reject("reentry");busy=true;try{
            if(Double.IsNaN(now)||Double.IsInfinity(now)||now<0||now<lastNow)return RuleResult.Reject("clock");lastNow=now;
            if(!Current(connection))return RuleResult.Reject("principal");
            // A partial image has no timer of its own: requesting recovery explicitly
            // abandons it, so missing final fragments cannot strand the receiver.
            if(pending!=null)Require();if(!NeedsSnapshot)return RuleResult.Reject("synchronized");
            if(sentRequest==request&&now-lastRequest<1)return RuleResult.Reject("retry-time");
            bytes=ResyncWire.EncodeRequest(new ResyncRequest(generation,request,Sequence));sentRequest=request;lastRequest=now;return RuleResult.Accept();
        }finally{busy=false;}}
        public RuleResult Receive(string c,byte[] bytes)
        {
            if(busy)return RuleResult.Reject("reentry");busy=true;
            try {
                if(!Current(c))return RuleResult.Reject("principal");long g,id;byte[] body;Packet p;
                if(!ResyncWire.TryDecodeState(bytes,out g,out id,out body)||g!=generation||!Wire.TryDecode(body,out p)||p.Protocol!=Protocol.Name||p.Schema!=Protocol.Schema||p.ActorId!=authority||
                    p.SessionId!=session||p.ContentHash!=content||p.Epoch!=epoch||p.FeatureId!=SourceStateHello.Extension)return RuleResult.Reject("frame");
                bool full=p.Kind==PacketKind.Snapshot,recovery=full&&NeedsSnapshot&&id==request&&sentRequest==request;
                if(id!=0&&!recovery||full&&NeedsSnapshot&&!recovery||!full&&id!=0)return RuleResult.Reject("resync-request");
                if(p.Sequence<0||p.Sequence<Sequence||p.Sequence==Sequence&&!recovery)return RuleResult.Reject("replay");
                if(!full&&(NeedsSnapshot||p.BaseSequence!=Sequence)){Require();return RuleResult.Reject("snapshot-required");}
                if(full&&p.BaseSequence!= -1)return RuleResult.Reject("snapshot-base");
                if(p.Payload.Length<41)return RuleResult.Reject("fragment");int index,count,length;byte[] digest,chunk;
                using(var m=new MemoryStream(p.Payload,false))using(var r=new BinaryReader(m))
                {if(r.ReadByte()!=1)return RuleResult.Reject("schema");index=r.ReadUInt16();count=r.ReadUInt16();length=r.ReadInt32();digest=r.ReadBytes(32);chunk=r.ReadBytes(p.Payload.Length-41);}
                if(length<12||length>SourceStateCodec.Maximum||count<1||count>SourceStateCodec.MaxChunks||count!=(length+SourceStateCodec.Chunk-1)/SourceStateCodec.Chunk||index>=count||
                    chunk.Length!=Math.Min(SourceStateCodec.Chunk,length-index*SourceStateCodec.Chunk))return RuleResult.Reject("fragment-length");
                string hash=Convert.ToBase64String(digest);
                if(pending!=null&&(pending.Sequence!=p.Sequence||pending.Base!=p.BaseSequence||pending.Request!=id||pending.Kind!=p.Kind||pending.Length!=length||Convert.ToBase64String(pending.Digest)!=hash))
                {Require();return RuleResult.Reject("interleaved-image");}
                if(pending==null)pending=new Assembly{Sequence=p.Sequence,Base=p.BaseSequence,Request=id,Kind=p.Kind,Count=count,Length=length,Digest=digest,Chunks=new byte[count][]};
                if(pending.Chunks[index]!=null)
                {if(Convert.ToBase64String(pending.Chunks[index])!=Convert.ToBase64String(chunk)){Require();return RuleResult.Reject("fragment-conflict");}return RuleResult.Reject("fragment-replay");}
                pending.Chunks[index]=chunk;pending.Received++;if(pending.Received!=count)return RuleResult.Accept();
                var image=new byte[length];int offset=0;foreach(byte[] part in pending.Chunks){Buffer.BlockCopy(part,0,image,offset,part.Length);offset+=part.Length;}
                byte[] actual;using(var sha=SHA256.Create())actual=sha.ComputeHash(image);
                if(Convert.ToBase64String(actual)!=hash){Require();return RuleResult.Reject("digest");}
                SourceStateView next;if(!SourceStateCodec.Decode(image,full?null:View,out next)){Require();return RuleResult.Reject("source-state");}
                if(View!=null&&next.LifecycleSequence<View.LifecycleSequence){Require();return RuleResult.Reject("lifecycle-replay");}
                // Re-resolve after complete validation, before the only publication.
                if(!Current(c)){Require();return RuleResult.Reject("principal");}
                View=next;Sequence=p.Sequence;NeedsSnapshot=false;pending=null;return RuleResult.Accept();
            }finally{busy=false;}
        }
    }
}
