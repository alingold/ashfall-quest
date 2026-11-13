using System;
using System.Text;
using Ashfall.Core;
using Ashfall.Modules.Networking;
using Ashfall.Modules.Sessions;
using Ashfall.Integration.SessionReplication;
using Ashfall.Integration.NetworkRuntime;
using Ashfall.NetworkRuntimeFixtures;

// Neutral reproduction of the exact R3 domain sequence; no socket substitutes.
public static class DispatchR4Tests
{
    private static int checks;
    private static void Need(bool b,string m) { checks++;if(!b)throw new InvalidOperationException(m); }
    private static void Yes(RuleResult r,string m) { Need(r.Accepted,m+":"+r.Reason); }
    private static SessionRules Rules() { return new SessionRules("source-map","respawn","source-map-hash",2,2,true,false,true,true,5); }
    public static int Main()
    {
        try
        {
            bool reproduced=false;
            try { Sequence(false); }
            catch(InvalidOperationException e) { reproduced=e.Message=="actual-dispatch:disconnected";Console.WriteLine("original-sequence="+e.Message); }
            Need(reproduced,"exact-root-failure-required");
            Sequence(true);
            Need(R2Tests.Main()==0,"retained-R2-regressions");
            Console.WriteLine("PASS DispatchR4 "+checks+" assertions; original failure reproduced; corrected three-generation sequence; native UNRUN");
            return 0;
        }
        catch(Exception e) { Console.WriteLine(e);return 1; }
    }
    private static void Sequence(bool drain)
    {
        var f=new SyntheticSessionFixture();string hash=new string('a',64),token="";
        var host=new SessionRuntimeHost("native-r1",hash,"host",Rules(),f,f,f,f,f,f,f,4,8,100);
        for(int generation=1;generation<=3;generation++)
        {
            long epoch=host.Epoch;
            f.Records["client"]=new AuthenticatedPeer("a","native-r1",hash,epoch,generation,false);
            f.Records["host"]=new AuthenticatedPeer("host","native-r1",hash,epoch,generation,true);
            var client=new SessionRuntimeClient(f,"host","a",f.Records["host"]);
            var channel=host.Open("client",2);string next;
            Yes(channel.Admit(client.Offer(),new JoinRequest(Protocol.Name,1,hash,"a",token,false),generation,out next),"admit");
            if(next!=null)token=next;
            var tx=new ChannelTests.Sender();Yes(channel.FlushOne(tx),"hello");Yes(client.Bind(generation,tx.Bytes),"bind");
            if(drain && generation==2)
            {
#if R4_DIAGNOSTICS
                string before=channel.CaptureDiagnostics();Console.WriteLine("generation=2;point=before-old-discard;"+before);
                Need(host.PendingDispatches==1 && before.Contains("ownerPending=1") && before.Contains("lease:1,auth:1,epoch:0") && before.Contains("currentBinding:False"),"old-owner-is-generation1-not-current2");
#endif
                RuleResult stale=host.DispatchOne(generation);
                Need(!stale.Accepted && stale.Reason=="disconnected" && !host.Get("a").Loaded,"exact-old-owner-consumed-without-publication");
                Need(!host.DispatchOne(generation).Accepted,"queue-empty-before-fresh-ingress");
#if R4_DIAGNOSTICS
                Need(host.PendingDispatches==0 && channel.CaptureDiagnostics().Contains("ownerPending=0"),"discard-keeps-parallel-queues-aligned");
#endif
            }
            Yes(client.PrepareCommand("raw-reused","sessions.loaded/1",Encoding.UTF8.GetBytes("source-map-hash"),2),"prepare");
            Yes(client.FlushCommand(tx),"send");byte[] command=tx.Bytes;Packet p;
            Need(Wire.TryDecode(command,out p) && p.Sequence==1,"fresh-sequence1");
            Need(!channel.Receive(generation+1,command,generation,0).Accepted,"negative-captured-generation-preserved");
            Yes(channel.Receive(generation,command,generation,0),"ingress");
#if R4_DIAGNOSTICS
            Console.WriteLine("generation="+generation+";point=fresh-ingress;"+channel.CaptureDiagnostics());
            if(drain)Need(host.PendingDispatches==1,"exactly-one-fresh-owner");
#endif
            Yes(host.DispatchOne(generation),"actual-dispatch");Need(host.Get("a").Loaded,"real-loaded");
#if R4_DIAGNOSTICS
            Need(host.PendingDispatches==0,"fresh-owner-consumed");
#endif
            Need(!channel.Receive(generation,command,generation,0).Accepted,"duplicate-rejected");
            Yes(client.Request(0,tx),"request");Yes(channel.Receive(generation,tx.Bytes,generation,0),"respond");
            Yes(channel.FlushOne(tx),"flush");Yes(client.Receive(generation,tx.Bytes),"replica");Need(client.View.Loaded,"real-replica-loaded");
            Yes(client.PrepareCommand(generation==2?"queued-retired":"queued-revoked","sessions.loaded/1",Encoding.UTF8.GetBytes("source-map-hash"),3),"prepare-retirement");
            Yes(client.FlushCommand(tx),"send-retirement");Yes(channel.Receive(generation,tx.Bytes,generation,0),"queue-retirement");
            if(generation==2)
            {
                Yes(host.Lifecycle(new CommandContext("native-r1","host","sessions-lifecycle/10",10,epoch,true),LifecycleAction.BeginLoading,Rules(),null,true),"epoch");
                Yes(channel.Close(-1),"retire");
                Need(host.Get("a")!=null && !host.Get("a").Loaded && !host.DispatchOne(11).Accepted,"epoch-retains-membership-retires-work");
            }
            else
            {
                f.Records.Remove("client");
                Need(!host.DispatchOne(generation+1).Accepted && host.Get("a")==null,"revocation-rejects-no-active-dispatch");
                Yes(channel.Close(generation+1),"idempotent-close");
            }
            Yes(client.Close(),"client-close");
        }
    }
}
