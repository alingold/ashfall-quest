#if UNITY_EDITOR && UNITY_6000_0_OR_NEWER && ASHFALL_BINDING_RETIREMENT_API
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using Ashfall.Core;
using Ashfall.Modules.Networking;
using Ashfall.Modules.Sessions;
using Ashfall.Integration.NetworkRuntime;
using Unity.Collections;
using UnityEngine;
namespace Ashfall.NetworkRuntimeFixtures
{
    // Root invokes only under engine_lane.py. This entry point never accepts a
    // success callback or a supplied memory metric. No global census is inferred.
    public static class NativeFixtureR3
    {
        public sealed class Result
        {
            public readonly int Checks, Failures, Unknowns;
            public readonly string Diagnostics;
            internal Result(int checks,int failures,string diagnostics)
            { Checks=checks;Failures=failures;Unknowns=1;Diagnostics=diagnostics; }
        }
        private sealed class RunState
        {
            internal int Checks,Failures,LeakLogs;
            internal readonly List<string> Lines=new List<string>();
            internal readonly List<LoopbackEndpoint> Owned=new List<LoopbackEndpoint>();
            internal void Need(bool value,string id)
            { Checks++;if(!value) { Failures++;throw new InvalidOperationException(id); } }
            internal void Yes(RuleResult r,string id) { Need(r.Accepted,id+":"+r.Reason); }
            internal LoopbackEndpoint New(int capacity)
            { var e=new LoopbackEndpoint(capacity);Owned.Add(e);Need(e.DriverCreated && e.ConnectionArrayCreated && !e.ConnectionReferenceCreated,"allocated-exact-handles");return e; }
            internal void Log(string message,string stack,LogType type)
            {
                // Log callbacks may occur on native worker threads. Capture only;
                // do not invoke Unity or transport APIs from this callback.
                if(message==null)return;
                if(message.IndexOf("leak",StringComparison.OrdinalIgnoreCase)>=0 ||
                   message.IndexOf("has not been deallocated",StringComparison.OrdinalIgnoreCase)>=0)
                    Interlocked.Increment(ref LeakLogs);
            }
            internal void Released(LoopbackEndpoint e)
            { e.Dispose();e.Dispose();Need(e.Disposed && !e.DriverCreated && !e.ConnectionArrayCreated && !e.ConnectionReferenceCreated,"exact-owned-release"); }
            internal void Case(string id,Action body)
            {
                int before=Failures;
                try { body();Lines.Add("case="+id+";failures=0"); }
                catch(Exception e)
                { if(before==Failures)Failures++;Lines.Add("case="+id+";failures="+(Failures-before)+";exception="+e.GetType().Name+";reason="+e.Message.Replace('\n',' ').Replace('\r',' ')); }
                finally
                {
                    foreach(var e in Owned)try { Released(e); }catch(Exception x) { Failures++;Lines.Add("cleanup-failure="+x.GetType().Name); }
                    Owned.Clear();
                }
            }
        }
        private const int Attempts=2000;
        private static void Pump(LoopbackEndpoint a,LoopbackEndpoint b)
        { a.Pump();b.Pump();Thread.Sleep(1); }
        private static void Connect(RunState s,LoopbackEndpoint server,LoopbackEndpoint client)
        {
            client.ConnectLoopback(server.ListenEphemeral().Port);
            for(int i=0;i<Attempts && (!server.Connected || !client.Connected);i++)Pump(server,client);
            s.Need(server.Connected && client.Connected && server.ConnectionReferenceCreated && client.ConnectionReferenceCreated,"actual-connected");
        }
        private static byte[] Receive(RunState s,LoopbackEndpoint from,LoopbackEndpoint to)
        {
            for(int i=0;i<Attempts;i++) { Pump(from,to);byte[] b;if(to.TryReceive(out b))return b; }
            s.Need(false,"receive-timeout");return null;
        }
        private static byte[] Transfer(RunState s,LoopbackEndpoint from,LoopbackEndpoint to,byte[] b)
        {
            bool sent=false;
            for(int i=0;i<Attempts;i++) { if(from.TrySend(b).Accepted) { sent=true;break; }Pump(from,to); }
            s.Need(sent,"bounded-send");return Receive(s,from,to);
        }
        private static void Retry(RunState s,Func<RuleResult> send,LoopbackEndpoint a,LoopbackEndpoint b)
        {
            for(int i=0;i<Attempts;i++) { if(send().Accepted)return;Pump(a,b); }
            s.Need(false,"retained-frame-send-timeout");
        }
        private static SessionRules Rules()
        { return new SessionRules("source-map","respawn","source-map-hash",2,2,true,false,true,true,5); }
        public static void RunForRoot()
        {
            Result result=Run();
            Debug.Log(result.Diagnostics);
            if(result.Failures!=0)throw new InvalidOperationException("NetworkNativeLifecycleR3;failures="+result.Failures+";unknowns="+result.Unknowns);
            // A normal return proves only the declared checks. UNKNOWN census,
            // late leak diagnostics and all external gates remain in the report.
        }
        public static Result Run()
        {
            var s=new RunState();NativeLeakDetectionMode previous=NativeLeakDetection.Mode;
            Application.logMessageReceivedThreaded+=s.Log;
            try
            {
                NativeLeakDetection.Mode=NativeLeakDetectionMode.EnabledWithStackTrace;
                s.Case("diagnostics-enabled",delegate { s.Need(NativeLeakDetection.Mode==NativeLeakDetectionMode.EnabledWithStackTrace,"native-leak-detection-mode"); });
                s.Case("never-started",delegate
                {
                    var e=s.New(8);s.Released(e);bool rejected=false;
                    try { e.Pump(); }catch(ObjectDisposedException) { rejected=true; }
                    s.Need(rejected,"terminal-use-after-dispose");
                });
                s.Case("fake-r2-census-rejected",delegate
                {
                    bool rejected=false,called=false;
                    try { typeof(NativeFixtureR2).GetMethod("Run").Invoke(null,new object[]{new Action<string>(delegate { called=true; })}); }
                    catch(System.Reflection.TargetInvocationException e) { rejected=e.InnerException is NotSupportedException; }
                    s.Need(rejected && !called,"fake-census-cannot-report-success");
                });
                for(int stage=1;stage<=3;stage++)
                {
                    int fault=stage;
                    s.Case("partial-allocation-"+stage,delegate
                    {
                        bool caught=false;
                        try { var e=new LoopbackEndpoint(8,fault);s.Owned.Add(e); }
                        catch(InvalidOperationException e) { caught=e.Message.StartsWith("fixture-after-",StringComparison.Ordinal); }
                        s.Need(caught && LoopbackEndpoint.LastFailedConstructionReleased,"failed-construction-exact-release");
                    });
                }
                s.Case("actual-session-reconnect-epoch",delegate { Sessions(s); });
                s.Case("bounded-pressure-recovery",delegate { Pressure(s); });
                for(int mode=0;mode<2;mode++)
                {
                    int invalid=mode;
                    s.Case("invalid-ingress-"+mode,delegate
                    {
                        var server=s.New(1);var client=s.New(8);Connect(s,server,client);
                        if(invalid==0)s.Yes(client.SendOversizeFixture(),"oversize-injection");
                        else { s.Yes(client.TrySend(new byte[]{1}),"overflow-first");s.Yes(client.TrySend(new byte[]{2}),"overflow-second"); }
                        for(int n=0;n<Attempts && !server.Lost;n++)Pump(client,server);
                        s.Need(server.Lost && !server.Connected && !server.ConnectionReferenceCreated,"invalid-ingress-retires-reference");
                        byte[] ignored;s.Need(!server.TryReceive(out ignored),"invalid-queue-cleared");
                    });
                }
            }
            catch(Exception e) { s.Failures++;s.Lines.Add("fixture-exception="+e.GetType().Name); }
            finally { Application.logMessageReceivedThreaded-=s.Log;NativeLeakDetection.Mode=previous; }
            if(s.LeakLogs!=0)s.Failures++;
            s.Lines.Add("native-leak-log-count="+s.LeakLogs.ToString(CultureInfo.InvariantCulture));
            s.Lines.Add("global-native-allocation-census=UNKNOWN;owned-handles-only=true;delayed-editor-leak-report=EXTERNAL");
            s.Lines.Add("checks="+s.Checks+";failures="+s.Failures+";unknowns=1;native-runtime-executed=true;authentication=SYNTHETIC;M1=OPEN");
            return new Result(s.Checks,s.Failures,String.Join("\n",s.Lines.ToArray()));
        }
        private static void Sessions(RunState s)
        {
            var f=new SyntheticSessionFixture();string hash=new string('a',64);
            var host=new SessionRuntimeHost("native-r1",hash,"host",Rules(),f,f,f,f,f,f,f,4,8,100);
            string token="";
            for(int generation=1;generation<=3;generation++)
            {
                long epoch=host.Epoch;
                f.Records["client"]=new Ashfall.Integration.SessionReplication.AuthenticatedPeer("a","native-r1",hash,epoch,generation,false);
                f.Records["host"]=new Ashfall.Integration.SessionReplication.AuthenticatedPeer("host","native-r1",hash,epoch,generation,true);
                var client=new SessionRuntimeClient(f,"host","a",f.Records["host"]);var channel=host.Open("client",2);
                var server=s.New(32);var remote=s.New(32);Connect(s,server,remote);string next;
                s.Yes(channel.Admit(Transfer(s,remote,server,client.Offer()),new JoinRequest(Protocol.Name,1,hash,"a",token,false),generation,out next),"actual-admission");if(next!=null)token=next;
                Retry(s,delegate { return channel.FlushOne(server); },server,remote);s.Yes(client.Bind(generation,Receive(s,server,remote)),"actual-bind");
                s.Lines.Add("generation="+generation+";point=bound;"+channel.CaptureDiagnostics());
                if(generation==2)
                {
                    // The last channel closed with one revoked command queued.
                    // No active channel could dispatch it then. Preserve the real
                    // one-item rejection before testing this generation's command.
                    s.Need(host.PendingDispatches==1,"exact-retired-owner-pending");
                    RuleResult retired=host.DispatchOne(generation);
                    s.Lines.Add("generation="+generation+";point=discard-old-owner;reason="+retired.Reason+";"+channel.CaptureDiagnostics());
                    s.Need(!retired.Accepted && retired.Reason=="disconnected" && host.PendingDispatches==0 && !host.Get("a").Loaded,"retired-owner-consumed-without-publication");
                }
                s.Need(host.PendingDispatches==0,"empty-before-generation-command");
                s.Yes(client.PrepareCommand("raw-reused","sessions.loaded/1",System.Text.Encoding.UTF8.GetBytes("source-map-hash"),2),"prepare");
                Retry(s,delegate { return client.FlushCommand(remote); },remote,server);
                byte[] command=Receive(s,remote,server);Packet packet;
                s.Need(Wire.TryDecode(command,out packet) && packet.Sequence==1,"fresh-numbering-one");
                s.Need(!channel.Receive(generation+1,command,generation,0).Accepted,"captured-generation-rejects");
                s.Yes(channel.Receive(generation,command,generation,0),"actual-ingress");
                s.Lines.Add("generation="+generation+";point=before-dispatch;"+channel.CaptureDiagnostics());
                RuleResult dispatched=host.DispatchOne(generation);
                s.Lines.Add("generation="+generation+";point=after-dispatch;reason="+dispatched.Reason+";"+channel.CaptureDiagnostics());
                s.Yes(dispatched,"actual-dispatch");
                s.Need(host.Get("a").Loaded,"actual-loaded");s.Need(!channel.Receive(generation,command,generation,0).Accepted,"duplicate-rejects");
                Retry(s,delegate { return client.Request(0,remote); },remote,server);
                s.Yes(channel.Receive(generation,Receive(s,remote,server),generation,0),"request");Retry(s,delegate { return channel.FlushOne(server); },server,remote);
                s.Yes(client.Receive(generation,Receive(s,server,remote)),"replication");s.Need(client.View.Loaded,"actual-client-view");
                if(generation==2)
                {
                    // Queue real work before retirement; a replacement must never execute it.
                    s.Yes(client.PrepareCommand("queued-retired","sessions.loaded/1",System.Text.Encoding.UTF8.GetBytes("source-map-hash"),3),"prepare-retired");
                    Retry(s,delegate { return client.FlushCommand(remote); },remote,server);
                    s.Yes(channel.Receive(generation,Receive(s,remote,server),generation,0),"queue-old-epoch");
                    s.Yes(host.Lifecycle(new CommandContext("native-r1","host","sessions-lifecycle/10",10,epoch,true),LifecycleAction.BeginLoading,Rules(),null,true),"actual-epoch-advance");
                    s.Yes(channel.Close(-1),"actual-binding-retirement");
                    s.Lines.Add("generation="+generation+";point=epoch-retirement;"+channel.CaptureDiagnostics());
                    s.Need(host.Get("a")!=null && !host.Get("a").Loaded && !host.DispatchOne(11).Accepted,"old-epoch-queue-retired");
                }
                else
                {
                    s.Yes(client.PrepareCommand("queued-revoked","sessions.loaded/1",System.Text.Encoding.UTF8.GetBytes("source-map-hash"),3),"prepare-revoked");
                    Retry(s,delegate { return client.FlushCommand(remote); },remote,server);
                    s.Yes(channel.Receive(generation,Receive(s,remote,server),generation,0),"queue-revoked");
                    f.Records.Remove("client");s.Need(!host.DispatchOne(generation+1).Accepted && host.Get("a")==null,"revoked-owner-queue-rejects");
                    s.Lines.Add("generation="+generation+";point=revocation;"+channel.CaptureDiagnostics());
                    s.Need(host.PendingDispatches==1,"revoked-work-retained-until-dispatchable");
                    s.Yes(channel.Close(generation+1),"closed-idempotent");
                }
                s.Yes(client.Close(),"client-close");remote.Disconnect();
                s.Need(!remote.ConnectionReferenceCreated,"local-disconnect-reference-reset");
                for(int n=0;n<Attempts && !server.Lost;n++)Pump(remote,server);
                s.Need(server.Lost && !server.ConnectionReferenceCreated,"remote-disconnect-reference-reset");
                s.Released(server);s.Released(remote);
            }
        }
        private static void Pressure(RunState s)
        {
            var server=s.New(256);var client=s.New(256);Connect(s,server,client);
            int sent=0;while(sent<4096 && server.TrySend(new byte[]{91}).Accepted)sent++;
            s.Need(sent>0 && sent<4096,"native-send-pressure-bounded");
            // Drain within the actual PopEvent loop: a full reliable native burst
            // cannot overflow the smaller managed inbox before it is drained.
            int received=0;
            for(int n=0;n<Attempts && received<sent;n++)
            {
                server.Pump();client.PumpAndDrainFixture(delegate(byte[] b)
                { s.Need(b.Length==1 && b[0]==91,"pressure-payload");received++; });
                Thread.Sleep(1);
            }
            s.Need(received==sent && !client.Lost,"all-accepted-pressure-recovered");
            s.Need(Transfer(s,server,client,new byte[]{92})[0]==92,"post-pressure-delivery");
        }
    }
}
#endif
