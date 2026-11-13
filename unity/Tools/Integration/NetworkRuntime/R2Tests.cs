#if ASHFALL_BINDING_RETIREMENT_API
using System;
using System.Text;
using Ashfall.Core;
using Ashfall.Modules.Networking;
using Ashfall.Modules.Sessions;
using Ashfall.Integration.Sessions;
using Ashfall.Integration.SessionReplication;
using Ashfall.Integration.NetworkRuntime;
using Ashfall.NetworkRuntimeFixtures;
public static class R2Tests
{
    private static int checks;
    private static readonly string Hash=ChannelTests.Hash;
    private static void Need(bool b,string m) { checks++;if(!b)throw new Exception(m); }
    private static void Yes(RuleResult r) { Need(r.Accepted,r.Reason); }
    private static SessionRules Rules() { return new SessionRules("source-map","respawn","source-map-hash",2,2,true,false,true,true,5); }
    private static AuthenticatedPeer Peer(string actor,long epoch,long gen,bool authority) { return new AuthenticatedPeer(actor,"native-r1",Hash,epoch,gen,authority); }
    private static JoinRequest Join(string actor,string token) { return new JoinRequest(Protocol.Name,1,Hash,actor,token,false); }
    private static byte[] Offer(string actor,long epoch) { return SessionReplicationHello.Encode(actor,"native-r1",Hash,epoch); }
    private static byte[] Cmd(string actor,long epoch,long seq,string id)
    { return Wire.Encode(new Packet {Kind=PacketKind.Command,SessionId="native-r1",ContentHash=Hash,ActorId=actor,Epoch=epoch,Tick=2,Sequence=seq,CommandId=id,FeatureId="sessions.loaded/1",Payload=Encoding.UTF8.GetBytes("source-map-hash")}); }
    public static int Main()
    {
        try { EpochRetirement(); CleanupBeforeRehello(); Composition(); SharedRevocation();Console.WriteLine("PASS NetworkRuntimeR2 "+checks+" actual API assertions; patched frozen coordinator, no native execution");return 0; }
        catch(Exception e) { Console.WriteLine(e);return 1; }
    }
    private static void EpochRetirement()
    {
        var f=new SyntheticSessionFixture();var s=ChannelTests.Coordinator(f,4);
        var lane=new SessionHostLane(s,f,2,new CoordinatorBindingRetirement(s));
        for(int i=0;i<40;i++)
        {
            long epoch=s.Epoch;string key="epoch/"+i;var p=Peer("a",epoch,i+1,false);f.Records[key]=p;
            var h=lane.CreateChannel(key,p,"host",2);string token;
            Yes(h.Admit(Offer("a",epoch),Join("a",""),i+1,out token));
            var receipt=s.CaptureBindingRetirement(key,s.ConnectionGeneration(key),p.Generation);
            Need(receipt!=null,"captured exact binding");
            Need(!s.RetireObsoleteBinding(receipt,epoch).Accepted,"same epoch cannot retire");
            Need(s.CaptureBindingRetirement(key,s.ConnectionGeneration(key),p.Generation+1)==null,"foreign auth generation");
            Yes(h.Receive(p.Generation,Cmd("a",epoch,1,"before-epoch"),i+1,0));
            Yes(s.Lifecycle(new CommandContext("native-r1","host","sessions-lifecycle/"+(i+1),i+1,epoch,true),LifecycleAction.BeginLoading,Rules(),null,true));
            var member=s.Get("a");Need(member!=null && !member.Loaded,"lifecycle preserves/reset real member");
            Need(!s.RetireObsoleteBinding(receipt,s.Epoch+1).Accepted && s.RetainedBindingCount==1,"future preflight preserves binding");
            var other=ChannelTests.Coordinator(new SyntheticSessionFixture(),2);Need(!other.RetireObsoleteBinding(receipt,s.Epoch).Accepted,"foreign coordinator receipt");
            // Same physical key rehello BEFORE old cleanup. The new binding survives.
            f.Records[key]=Peer("a",s.Epoch,i+101,false);
            var replacement=lane.CreateChannel(key,f.Records[key],"host",2);
            Yes(replacement.Admit(Offer("a",s.Epoch),Join("a",""),i+2,out token));
            long newGeneration=s.ConnectionGeneration(key);Need(newGeneration>0,"new real lease");
            Yes(h.Close(-1));Yes(h.Close(-1));Need(h.State==ChannelState.Closed,"obsolete channel retires without domain departure");
            Yes(s.RetireObsoleteBinding(receipt,s.Epoch));Yes(s.RetireObsoleteBinding(receipt,s.Epoch));
            Need(s.RetainedBindingCount==1 && s.ConnectionGeneration(key)==newGeneration && member.CanonicalActorId==s.Get("a").CanonicalActorId && member.SpawnRevision==s.Get("a").SpawnRevision,"replacement retained");
            Need(s.Get("a")!=null && !s.Get("a").Loaded,"retirement preserves membership and stale queue never applies");
            Need(!h.Receive(p.Generation,Cmd("a",epoch,1,"late"),i+2,0).Accepted,"old callback rejects");
            Need(!replacement.Receive(p.Generation,Cmd("a",s.Epoch,1,"fresh"),i+2,0).Accepted,"old generation rejects replacement");
            Yes(replacement.Receive(i+101,Cmd("a",s.Epoch,1,"fresh"),i+2,0));Yes(lane.DispatchOne(i+2));Need(s.Get("a").Loaded,"new epoch sequence1 publishes");
            Yes(replacement.Close(i+3));Need(s.Get("a")==null && s.RetainedBindingCount==0,"bounded binding history reclaimed");
        }
    }
    private static void Composition()
    {
        var f=new SyntheticSessionFixture();var host=new SessionRuntimeHost("native-r1",Hash,"host",Rules(),f,f,f,f,f,f,f,4,8,100);
        string token="";
        for(int i=1;i<=12;i++)
        {
            f.Records["physical"]=Peer("a",0,i,false);f.Records["authority"]=Peer("host",0,i,true);
            var client=new SessionRuntimeClient(f,"authority","a",f.Records["authority"]);var h=host.Open("physical",2);
            string next;Yes(h.Admit(client.Offer(),Join("a",token),i,out next));if(next!=null)token=next;
            var tx=new ChannelTests.Sender();Yes(h.FlushOne(tx));Yes(client.Bind(i,tx.Bytes));
            byte[] payload=Encoding.UTF8.GetBytes("source-map-hash");
            Yes(client.PrepareCommand("same-raw-id","sessions.loaded/1",payload,2));payload[0]=0;
            tx.Block=true;Need(!client.FlushCommand(tx).Accepted,"retained native pressure");
            Need(!client.PrepareCommand("overwrite","sessions.loaded/1",new byte[0],2).Accepted,"no pending overwrite");
            tx.Block=false;Yes(client.FlushCommand(tx));Packet p;Need(Wire.TryDecode(tx.Bytes,out p) && p.Sequence==1,"new wire numbering after reconnect");
            Yes(h.Receive(i,tx.Bytes,i,0));Yes(host.DispatchOne(i));Need(host.Get("a").Loaded,"actual composed mutation");
            Need(!h.Receive(i,tx.Bytes,i,0).Accepted,"in-generation duplicate rejected");
            Yes(client.Request(0,tx));Yes(h.Receive(i,tx.Bytes,i,0));Yes(h.FlushOne(tx));Yes(client.Receive(i,tx.Bytes));Need(client.View.Loaded,"actual client view");
            Yes(h.Close(i+1));Yes(client.Close());
        }
    }
    private static void CleanupBeforeRehello()
    {
        var f=new SyntheticSessionFixture();var host=new SessionRuntimeHost("native-r1",Hash,"host",Rules(),f,f,f,f,f,f,f,4,8,100);
        f.Records["old"]=Peer("a",0,1,false);var old=host.Open("old",1);string token;
        Yes(old.Admit(Offer("a",0),Join("a",""),1,out token));
        Yes(old.Receive(1,Cmd("a",0,1,"queued-before-lifecycle"),2,0));
        Yes(host.Lifecycle(new CommandContext("native-r1","host","sessions-lifecycle/10",10,0,true),LifecycleAction.BeginLoading,Rules(),null,true));
        Yes(old.Close(-1));Need(host.Get("a")!=null && !host.Get("a").Loaded,"retire before rehello preserves member");
        bool denied=false;try { host.Open("unprovisioned",1); }catch(ArgumentException) { denied=true; }Need(denied,"no generation from client without registry");
        f.Records["replacement"]=Peer("a",1,2,false);var replacement=host.Open("replacement",1);
        Yes(replacement.Admit(Offer("a",1),Join("a",""),11,out token));Need(token==null,"existing epoch member no new token minted");
        f.Records["duplicate"]=Peer("a",1,3,false);var duplicate=host.Open("duplicate",1);
        Need(!duplicate.Admit(Offer("a",1),Join("a",""),11,out token).Accepted,"active duplicate still rejected by real network lease");Yes(duplicate.Close(11));
        Yes(old.Close(11));Need(host.Get("a")!=null,"late old cleanup preserves new member");
        Need(!host.DispatchOne(11).Accepted && !host.Get("a").Loaded,"old epoch queued work gone");
        Yes(replacement.Receive(2,Cmd("a",1,1,"queued-before-lifecycle"),11,0));Yes(host.DispatchOne(11));Need(host.Get("a").Loaded,"new physical key epoch rehello sequence1");
        Yes(replacement.Close(12));
    }
    private static void SharedRevocation()
    {
        foreach(bool registryLoss in new[]{true,false})
        {
            var f=new SyntheticSessionFixture();var host=new SessionRuntimeHost("native-r1",Hash,"host",Rules(),f,f,f,f,f,f,f,4,8,100);
            f.Records["a"]=Peer("a",0,1,false);f.Records["b"]=Peer("b",0,2,false);
            var a=host.Open("a",2);var b=host.Open("b",2);string token;
            Yes(a.Admit(Offer("a",0),Join("a",""),1,out token));Yes(b.Admit(Offer("b",0),Join("b",""),1,out token));
            Yes(a.Receive(1,Cmd("a",0,1,"a"),2,0));Yes(b.Receive(2,Cmd("b",0,1,"b"),2,0));
            if(registryLoss)f.Records.Remove("a");else Need(!a.Close(-1).Accepted,"failed cleanup retry pending");
            Need(!host.DispatchOne(-1).Accepted && !host.Get("a").Loaded && !host.Get("b").Loaded,"all revocations before shared dispatch");
            Need(!host.DispatchOne(2).Accepted && host.Get("a")==null && !host.Get("b").Loaded,"stale owner discarded");
            Yes(host.DispatchOne(2));Need(host.Get("b").Loaded,"healthy recipient remains dispatchable");
        }
    }
}
#endif
