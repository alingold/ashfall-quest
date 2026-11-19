using System;using System.Collections.Generic;using Ashfall.Core;using Ashfall.Modules.Networking;using Ashfall.Integration.Playable.Networking;
partial class LobbyCases
{
 sealed class Frame{internal string Peer;internal byte[] Bytes;}
 sealed class Link{internal Pipe Remote;internal string Back;}
 sealed class Pipe:IPumpedTransport
 {
  internal readonly Dictionary<string,Link> Links=new Dictionary<string,Link>();internal readonly Queue<Frame> Inbox=new Queue<Frame>();internal readonly List<Frame> Held=new List<Frame>();internal bool HoldRoster;internal int Pressure;internal Action<string> Lost;internal int Sent,Disposed;
  public void Pump(){}
  public void Send(string peer,byte[] bytes,bool reliable){if(Pressure>0){Pressure--;throw new InvalidOperationException("send-backpressure");}Link link;if(!Links.TryGetValue(peer,out link))throw new InvalidOperationException("unknown-peer");Packet p;if(!Wire.TryDecode(bytes,out p))throw new Exception("Malformed production send");if(!reliable&&p.FeatureId!="playable.presence/1"&&p.FeatureId!="playable.clock/1"&&p.FeatureId!="playable.tracking/2")throw new Exception("Unexpected unreliable feature");MaxFrame(p,bytes.Length);Sent++;var frame=new Frame{Peer=link.Back,Bytes=(byte[])bytes.Clone()};if(HoldRoster&&p.FeatureId=="playable.lobby.roster/1")Held.Add(new Frame{Peer=peer,Bytes=frame.Bytes});else link.Remote.Inbox.Enqueue(frame);}
  public bool TryReceive(out string peer,out byte[] bytes){if(Inbox.Count==0){peer=null;bytes=null;return false;}var f=Inbox.Dequeue();peer=f.Peer;bytes=f.Bytes;return true;}
  internal void Release(int index){var f=Held[index];var link=Links[f.Peer];link.Remote.Inbox.Enqueue(new Frame{Peer=link.Back,Bytes=f.Bytes});Held.RemoveAt(index);}
  internal void Close(string peer){Link link;if(!Links.TryGetValue(peer,out link))return;Links.Remove(peer);link.Remote.Links.Remove(link.Back);if(Lost!=null)Lost(peer);if(link.Remote.Lost!=null)link.Remote.Lost(link.Back);}
  public void Dispose(){Disposed++;Links.Clear();Inbox.Clear();Held.Clear();}
 }
 sealed class Pair:IDisposable
 {
  internal readonly Pipe HostPipe=new Pipe(),ClientPipe=new Pipe();internal readonly R24LanLobby Host,Client;internal double Now;int serial;
  internal Pair(string clientContent=null){Host=new R24LanLobby(HostPipe,HostPipe.Close,Id,new string('a',64));Client=new R24LanLobby(ClientPipe,ClientPipe.Close,Id,clientContent??new string('a',64));HostPipe.Lost=Host.TransportDisconnected;ClientPipe.Lost=Client.TransportDisconnected;Host.StartHost("TEST ROOM","ListenHost","range",new string('b',64));Connect();}
  string Id(){return "actor-"+(++serial);}
  void Connect(){HostPipe.Links.Add("client",new Link{Remote=ClientPipe,Back="server"});ClientPipe.Links.Add("server",new Link{Remote=HostPipe,Back="client"});Client.StartClient("server","Guest",0);Host.TransportConnected("client");}
  internal void Tick(){Now+=.05;Host.Tick(Now);Client.Tick(Now);}
  public void Dispose(){Client.Dispose();Host.Dispose();ClientPipe.Dispose();HostPipe.Dispose();}
 }
 static int checks;static void Need(bool ok,string why){checks++;if(!ok)throw new Exception(why);}
 // Legacy LAN scenarios pin the standard kit (CX4/PX4/M1014) and 1x damage.
 // MatchChecks exercises the live r18 defaults without this fixture override.
 static void LegacyFixtureDefaults(){var d=(Dictionary<string,object>)typeof(Ashfall.Integration.Playable.Match.R24PlayableMatchRules).GetField("Defaults",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static).GetValue(null);d["loadout"]="standard";d["damage"]=1.0f;d["npc_damage"]=1.0f;d["build_damage"]=1.0f;}
 static void Main()
 {
  LegacyFixtureDefaults();
  PendingHealthCases();
  R19Main();RunVehicleSeatChecks();RunHudChecks();
  using(var pair=new Pair()){
   for(int i=0;i<5;i++)pair.Tick();Need(pair.Host.State==R24LanLobbyState.Hosting&&pair.Client.State==R24LanLobbyState.Connected,"Actual Session-backed lobby handshake");Need(pair.Host.Members.Length==2&&pair.Client.Members.Length==2,"Both committed rosters contain2");
   foreach(var m in pair.Host.Members)Need(!m.WorldLoaded,"No fabricated map-ready/grant from lobby membership");Need(pair.Client.SessionId==pair.Host.SessionId&&pair.Client.OwnActor!=pair.Host.OwnActor,"Host assigned distinct actor/session");
   var copy=pair.Client.Members;copy[0]=null;Need(pair.Client.Members[0]!=null,"Public roster array cannot mutate owner");pair.ClientPipe.Close("server");Need(pair.Host.Members.Length==1&&pair.Client.State==R24LanLobbyState.Offline,"Departure updates actual Session and client");
  }
  using(var pair=new Pair(new string('c',64))){for(int i=0;i<5;i++)pair.Tick();Need(pair.Client.State==R24LanLobbyState.Offline&&pair.Host.Members.Length==1,"Content mismatch never adopts roster");}
  using(var pair=new Pair()){
   pair.HostPipe.Pressure=2;for(int i=0;i<8;i++)pair.Tick();Need(pair.Client.State==R24LanLobbyState.Connected&&pair.Host.Members.Length==2,"Retained hello survives send pressure without duplicate join");
  }
  using(var pair=new Pair()){
   pair.HostPipe.HoldRoster=true;for(int i=0;i<3;i++)pair.Tick();Need(pair.Host.Members.Length==2&&pair.Client.Members.Length==0&&pair.Client.State==R24LanLobbyState.Connecting,"Incomplete snapshot not published");Need(pair.HostPipe.Held.Count==2,"One bounded row per member");pair.HostPipe.Release(0);pair.Tick();Need(pair.Client.Members.Length==0,"First row alone cannot fabricate complete roster");pair.HostPipe.Release(0);pair.Tick();Need(pair.Client.Members.Length==2&&pair.Client.State==R24LanLobbyState.Connected,"Complete revision publishes atomically");
  }
  using(var pair=new Pair()){
   pair.Host.Tick(.05); // Source offer queued to client; replace its echo with a spoof.
   pair.Client.Tick(.05);Need(pair.HostPipe.Inbox.Count==1,"One real client hello");var frame=pair.HostPipe.Inbox.Dequeue();Packet packet;Need(Wire.TryDecode(frame.Bytes,out packet),"Hello decode");packet.ActorId="other-player";pair.HostPipe.Inbox.Enqueue(new Frame{Peer=frame.Peer,Bytes=Wire.Encode(packet)});pair.Host.Tick(.1);Need(pair.Host.Members.Length==1&&pair.Host.RejectedFrames==1,"Packet actor cannot override transport-assigned identity");
  }
  using(var pair=new Pair()){
   pair.Client.Tick(10.01);Need(pair.Client.State==R24LanLobbyState.Offline&&pair.Client.Status.Contains("timed out"),"Bounded source10s connect timeout");
  }
  // Capacity includes the real listen-host member. Peers admitted through the
  // same production coordinator, not a stand-in count in the test.
  var hostPipe=new Pipe();int serial=0;var host=new R24LanLobby(hostPipe,hostPipe.Close,delegate{return "cap-"+(++serial);},new string('a',64));hostPipe.Lost=host.TransportDisconnected;host.StartHost("CAPACITY","Host","range",new string('b',64));var clients=new List<R24LanLobby>();var pipes=new List<Pipe>();
  try{
   for(int n=0;n<23;n++){var pipe=new Pipe();var client=new R24LanLobby(pipe,pipe.Close,delegate{return "unused";},new string('a',64));string key="peer"+n;hostPipe.Links.Add(key,new Link{Remote=pipe,Back="server"});pipe.Links.Add("server",new Link{Remote=hostPipe,Back=key});pipe.Lost=client.TransportDisconnected;client.StartClient("server","Guest"+n,n);host.TransportConnected(key);clients.Add(client);pipes.Add(pipe);for(int frame=0;frame<4;frame++){double now=n+frame*.05;host.Tick(now);foreach(var c in clients)c.Tick(now);}}
   Need(host.Members.Length==24,"Twenty-four actual Session members including host");foreach(var c in clients)Need(c.State==R24LanLobbyState.Connected&&c.Members.Length==24,"All clients have latest complete roster");
   var extra=new Pipe();hostPipe.Links.Add("extra",new Link{Remote=extra,Back="server"});extra.Links.Add("server",new Link{Remote=hostPipe,Back="extra"});host.TransportConnected("extra");Need(!hostPipe.Links.ContainsKey("extra")&&host.Members.Length==24,"Twenty-fifth connection refused without evicting active roster");extra.Dispose();
  }finally{foreach(var c in clients)c.Dispose();foreach(var p in pipes)p.Dispose();host.Dispose();hostPipe.Dispose();}
  MaxPlayerCases();PresenceCases();MapCases();RoundCases();RawTrackingCases();SpawnCases();RawRevisionCases();RawRecoveryCases();HostHealthCases();RootHealthViewCases();HomeBoundaryCases();WarmupHealthCases();WarmupRecoveryCases();MatchHealthCases.RunOffline();RegressionCases();SwordMain();ReviveRules(false);Console.WriteLine("PASS "+checks+" production Session-backed LAN lobby/presence/map/round/raw-tracking/spawn observations; transport simulation only, native UTP/UI unrun.");
 }
}
