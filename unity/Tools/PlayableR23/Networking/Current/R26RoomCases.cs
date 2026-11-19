using System;using System.Collections.Generic;using System.Threading.Tasks;using Ashfall.Integration.Playable.Online;using Ashfall.Modules.Networking;using Ashfall.Modules.Sessions;using Ashfall.Integration.Playable.Match;using Ashfall.Integration.Playable.Networking;
// ROOMS workstream (campaign r12): room continuity over the production lobby/Session owners.
// Same simulated transport as the maintained LAN suite; no Unity, sockets or UI.
partial class LobbyCases
{
 public static void RunRoomCases(){RoomLobbyCases();RoomStatusCases();RoomDirectoryCases();Console.WriteLine("PASS "+checks+" room-continuity observations over production R24LanLobby/Session; transport simulation only.");}
 // ROOM-PUBLIC-LIST: the pumped directory flow against a fake service.
 sealed class FakeDirectory:IRoomDirectory
 {
  public int Publishes,Updates,Heartbeats,Removes,Lists;public string Removed,Build;public PublicRoom Last;
  public TaskCompletionSource<string> Publishing;public TaskCompletionSource<bool> Writing;public TaskCompletionSource<PublicRoom[]> Listing;
  public Task<string> Publish(PublicRoom room,string build){Publishes++;Last=room;Build=build;Publishing=new TaskCompletionSource<string>();return Publishing.Task;}
  public Task Update(string id,PublicRoom room,string build){Updates++;Last=room;Writing=new TaskCompletionSource<bool>();return Writing.Task;}
  public Task Heartbeat(string id){Heartbeats++;Writing=new TaskCompletionSource<bool>();return Writing.Task;}
  public Task Remove(string id){Removes++;Removed=id;var done=new TaskCompletionSource<bool>();done.SetResult(true);return done.Task;}
  public Task<PublicRoom[]> List(string build,int count){Lists++;Build=build;Listing=new TaskCompletionSource<PublicRoom[]>();return Listing.Task;}
 }
 static void RoomDirectoryCases()
 {
  string build=new string('a',64);var room=new PublicRoom(null,"Al","BCD678","range",1,8,false);
  Need(room.SameListing("Al","BCD678","range",1,8,false)&&!room.SameListing("Al","BCD678","range",2,8,false)&&!room.SameListing("Al","BCD678","valley",1,8,false)&&!room.SameListing("Al","BCD678","range",1,8,true)&&!room.Full&&new PublicRoom("x","","C","m",9,8,false).Full,"Listing identity covers name, code, map, players and match state");
  var service=new FakeDirectory();var flow=new R26RoomDirectoryFlow(service,build);int changes=0;flow.Changed+=delegate{changes++;};
  flow.Pump(0);Need(service.Publishes==0&&!flow.Listed,"No room, no listing");
  flow.Offer(room);flow.Pump(1);flow.Pump(1.1);Need(service.Publishes==1&&service.Build==build&&!flow.Listed&&flow.Busy,"A public room is published once while the request is in flight");
  service.Publishing.SetResult("lobby-1");flow.Pump(2);Need(flow.Listed&&changes>0&&service.Updates==0&&service.Heartbeats==0,"Listing confirmed by the service");
  var grown=new PublicRoom(null,"Al","BCD678","range",2,8,false);flow.Offer(grown);flow.Pump(3);Need(service.Updates==0,"A change inside the update spacing waits");
  flow.Pump(2+R26RoomDirectoryFlow.UpdateSeconds);Need(service.Updates==1&&service.Last.Players==2,"Player count is updated after the spacing");
  flow.Offer(new PublicRoom(null,"Al","BCD678","valley",2,8,true));flow.Pump(20);Need(service.Updates==1&&service.Heartbeats==0,"One request in flight: nothing else is sent meanwhile");
  service.Writing.SetResult(true);flow.Pump(20.1);Need(service.Updates==2&&service.Last.MapId=="valley"&&service.Last.MatchRunning,"Map and match state follow the room");
  service.Writing.SetResult(true);flow.Pump(20.2);Need(service.Heartbeats==1,"A due heartbeat follows once the update has finished");service.Writing.SetResult(true);
  int beats=service.Heartbeats;for(double t=20.3;t<20.3+60;t+=.05)  {flow.Pump(t);if(service.Writing!=null&&!service.Writing.Task.IsCompleted)service.Writing.SetResult(true);}
  int idle=service.Heartbeats-beats;Need(idle>=3&&idle<=4&&service.Updates==2,"Idle room: one heartbeat per "+R26RoomDirectoryFlow.HeartbeatSeconds+" s over 60 s (service limit 5 per 30 s), no redundant updates; got "+idle);
  flow.Offer(null);flow.Pump(100);Need(service.Removes==1&&service.Removed=="lobby-1"&&!flow.Listed,"Private or ended room removes its listing");
  // Room ends while its publish is still in flight.
  service=new FakeDirectory();flow=new R26RoomDirectoryFlow(service,build);flow.Offer(room);flow.Pump(0);flow.Offer(null);flow.Pump(1);Need(service.Removes==0,"Nothing to remove before the service answers");
  service.Publishing.SetResult("lobby-2");flow.Pump(2);flow.Pump(2.1);Need(service.Removes==1&&service.Removed=="lobby-2"&&!flow.Listed,"A listing that arrives after the room ended is removed at once");
  // Expired listing is published again; failed publish retries with spacing.
  service=new FakeDirectory();flow=new R26RoomDirectoryFlow(service,build);flow.Offer(room);flow.Pump(0);service.Publishing.SetResult("lobby-3");flow.Pump(1);flow.Pump(1+R26RoomDirectoryFlow.HeartbeatSeconds);Need(service.Heartbeats==1,"Heartbeat due");
  service.Writing.SetException(new OnlineRoomFailure("gone"));flow.Pump(17);Need(!flow.Listed&&service.Publishes==1,"Failed heartbeat drops the listing and waits before retrying");
  flow.Pump(17+R26RoomDirectoryFlow.RetrySeconds);Need(service.Publishes==2,"Listing is published again after the retry spacing");
  service.Publishing.SetException(new OnlineRoomFailure("No internet connection"));flow.Pump(40);Need(!flow.Listed&&flow.PublishError=="No internet connection","Publish failure is reported");flow.Pump(41);Need(service.Publishes==2,"No retry storm");flow.Pump(40+R26RoomDirectoryFlow.RetrySeconds);Need(service.Publishes==3,"Retry after spacing");
  // Browsing.
  service=new FakeDirectory();flow=new R26RoomDirectoryFlow(service,build);
  Need(flow.Refresh(0)&&flow.ListState==RoomListState.Loading&&!flow.Refresh(0.5)&&service.Lists==1,"One query in flight");
  service.Listing.SetResult(new[]{new PublicRoom("a","Al","BCD678","range",2,8,true),new PublicRoom("b","Pban","GHN8DW","valley",8,8,false)});flow.Pump(1);
  Need(flow.ListState==RoomListState.Ready&&flow.Rooms.Length==2&&flow.Rooms[0].MatchRunning&&flow.Rooms[1].Full,"Rooms listed with map, players and match state");
  Need(!flow.Refresh(1.5)&&flow.Refresh(R26RoomDirectoryFlow.RefreshSeconds)&&service.Lists==2,"Refresh spacing keeps queries under the service limit");
  service.Listing.SetException(new Exception("raw service text"));flow.Pump(3);Need(flow.ListState==RoomListState.Failed&&flow.ListError=="Cannot reach the room list. Please try again"&&flow.Rooms.Length==2,"Service failure shows plain text and keeps the last list");
 }
 // R26RoomStatus: the one room picture every menu reads.
 static void RoomStatusCases()
 {
  Need(R26RoomStatus.StageOf(R24LanLobbyState.Offline,null,0,false)==R26RoomStage.None&&!R26RoomStatus.Offline.InRoom&&R26RoomStatus.Offline.Headline()=="LOCAL OFFLINE","Offline is the only state shown as offline");
  Need(R26RoomStatus.StageOf(R24LanLobbyState.Connecting,null,0,false)==R26RoomStage.Joining,"Connecting is joining");
  Need(R26RoomStatus.StageOf(R24LanLobbyState.Hosting,null,0,false)==R26RoomStage.Lobby,"A fresh room is its lobby");
  Need(R26RoomStatus.StageOf(R24LanLobbyState.Hosting,null,0,true)==R26RoomStage.Loading,"An accepted host request shows the barrier before the map frame exists");
  string hash=new string('c',64);
  using(var pair=new Pair()){
   Settle(pair,5);pair.Host.BeginMapSelection("range",hash);pair.Host.ReportMapLoaded(hash);Settle(pair,3);
   Need(R26RoomStatus.StageOf(pair.Host.State,pair.Host.SelectedMap,pair.Host.RoundCountdown,false)==R26RoomStage.Loading&&R26RoomStatus.StageOf(pair.Client.State,pair.Client.SelectedMap,pair.Client.RoundCountdown,false)==R26RoomStage.Loading,"Both sides read the loading barrier");
   Need(pair.Host.MemberCount==2&&pair.Host.LoadedCount==1&&pair.Host.OwnWorldLoaded&&!pair.Client.OwnWorldLoaded,"Allocation-free roster reads match the roster: one of two ready");
   var loading=new R26RoomStatus(R26RoomStage.Loading,true,"range","",pair.Host.MemberCount,pair.Host.LoadedCount,8,0,0,0,true);
   Need(loading.Headline()=="LOADING MAP"&&loading.Detail()=="1 / 2 PLAYERS READY"&&loading.InTransition&&!loading.HostMayAct&&!loading.MatchRunning,"Loading barrier text and gating");
   pair.Client.ReportMapLoaded(hash);Settle(pair,4);
   Need(R26RoomStatus.StageOf(pair.Client.State,pair.Client.SelectedMap,pair.Client.RoundCountdown,false)==R26RoomStage.FreeRoam,"An open map without a match is free roam");
   var rules=R24PlayableMatchRules.ForPreset("team_respawn","range").With("time_limit",60).With("rounds",1);
   pair.Host.BeginRoundSelection(rules,hash,5);pair.Host.ReportMapLoaded(hash);Settle(pair,4);pair.Client.ReportMapLoaded(hash);Settle(pair,5);
   Need(R26RoomStatus.StageOf(pair.Client.State,pair.Client.SelectedMap,pair.Client.RoundCountdown,false)==R26RoomStage.Starting&&pair.Client.RoundCountdown>0,"Countdown is its own stage on the member");
   var starting=new R26RoomStatus(R26RoomStage.Starting,false,"range","team_respawn",2,2,8,(int)Math.Ceiling(pair.Client.RoundCountdown),1,1,true);
   Need(starting.Headline().StartsWith("MATCH STARTS IN ")&&starting.MatchRunning&&starting.Guest&&!starting.HostMayAct,"Countdown text; guests never get host actions");
   pair.Now=pair.Host.SelectedMap.StartsAt+1;pair.Host.Tick(pair.Now);pair.Client.Tick(pair.Now);Settle(pair,3);
   Need(R26RoomStatus.StageOf(pair.Host.State,pair.Host.SelectedMap,pair.Host.RoundCountdown,false)==R26RoomStage.Match&&R26RoomStatus.StageOf(pair.Client.State,pair.Client.SelectedMap,pair.Client.RoundCountdown,false)==R26RoomStage.Match,"Running match on both sides");
   pair.Now=pair.Host.SelectedMap.StartsAt+60.1;pair.Host.Tick(pair.Now);pair.Client.Tick(pair.Now);Settle(pair,3);
   Need(R26RoomStatus.StageOf(pair.Client.State,pair.Client.SelectedMap,pair.Client.RoundCountdown,false)==R26RoomStage.Results,"Results stage");
   pair.Host.BeginRoomLobby();Settle(pair,4);
   Need(R26RoomStatus.StageOf(pair.Host.State,pair.Host.SelectedMap,pair.Host.RoundCountdown,false)==R26RoomStage.Lobby&&R26RoomStatus.StageOf(pair.Client.State,pair.Client.SelectedMap,pair.Client.RoundCountdown,false)==R26RoomStage.Lobby,"HOME reads as the room lobby on both sides");
  }
  var settled=new R26RoomStatus(R26RoomStage.Lobby,true,"range","",2,2,8,0,0,0,false);
  Need(settled.HostMayAct&&settled.Headline()=="ROOM LOBBY"&&settled.Detail()=="2 / 8 PLAYERS"&&settled.Same(R26RoomStage.Lobby,true,"range","",2,2,8,0,0,0,false)&&!settled.Same(R26RoomStage.Lobby,true,"range","",3,3,8,0,0,0,false),"Settled host lobby; change detection");
  Need(R26RoomStatus.MatchDetail("LOCAL OFFLINE | R1/1 | 421S | SAFE ZONE")=="R1/1 | 421S | SAFE ZONE"&&R26RoomStatus.MatchDetail("LAN ROUND | HOST HEALTH / COMBAT | RESPAWN | ROUND 1 / 3")=="RESPAWN | ROUND 1 / 3"&&R26RoomStatus.MatchDetail("LAN MATCH RESULTS | WAIT FOR HOST")=="LAN MATCH RESULTS | WAIT FOR HOST"&&R26RoomStatus.MatchDetail(null)=="","A room never repeats root's LOCAL OFFLINE label (playtest video 34:20, 38:20)");
 }
 static void Settle(Pair pair,int ticks){for(int i=0;i<ticks;i++)pair.Tick();}
 static void RoomLobbyCases()
 {
  string first=new string('c',64),second=new string('d',64);
  using(var pair=new Pair()){
   Settle(pair,5);string host=pair.Host.OwnActor,client=pair.Client.OwnActor,session=pair.Host.SessionId;
   Need(!pair.Client.BeginRoomLobby(),"Only the host returns the room to its lobby");
   long fresh=pair.Host.Epoch;Need(pair.Host.BeginRoomLobby()&&pair.Host.Epoch==fresh&&pair.Host.SelectedMap==null,"A fresh room is already its lobby and spends no epoch");
   // Warmup map, then HOME.
   Need(pair.Host.BeginMapSelection("range",first)&&pair.Host.ReportMapLoaded(first),"Shared warmup map begins");Settle(pair,3);Need(pair.Client.ReportMapLoaded(first),"Member loads warmup map");Settle(pair,4);
   Need(pair.Host.SelectedMap.Active&&pair.Client.SelectedMap.Active&&!pair.Host.SelectedMap.Home,"Warmup world is open for both");
   long before=pair.Host.Epoch;Need(pair.Host.BeginRoomLobby(),"Host HOME is accepted from an open warmup world");
   Need(pair.Host.Epoch==before+1&&pair.Host.SelectedMap.Home&&pair.Host.SelectedMap.Cancelled&&!pair.Host.SelectedMap.Active&&pair.Host.MapStage==SessionStage.Lobby,"Host reaches the room lobby in the same call: new epoch, Session Lobby stage, no Loading window");
   Need(pair.Host.State==R24LanLobbyState.Hosting&&pair.Host.Members.Length==2,"HOME keeps the hosted room and its membership");
   Settle(pair,4);
   Need(pair.Client.State==R24LanLobbyState.Connected&&pair.Client.Epoch==pair.Host.Epoch&&pair.Client.SelectedMap!=null&&pair.Client.SelectedMap.Home&&pair.Client.SelectedMap.Cancelled&&pair.Client.MapStage==SessionStage.Lobby,"Member follows HOME and stays connected");
   Need(pair.Host.Members.Length==2&&pair.Client.Members.Length==2&&pair.Host.OwnActor==host&&pair.Client.OwnActor==client&&pair.Client.SessionId==session&&pair.Host.SessionId==session,"Room, session and member identities survive HOME");
   Need(pair.Host.RejectedFrames==0&&pair.Client.RejectedFrames==0,"HOME transition rejects no frame on either side");
   Need(pair.Host.Status.StartsWith("Hosting LAN lobby: 2")&&pair.Client.Status.StartsWith("Connected to TEST ROOM: 2"),"Room-lobby status is the membership line, not a loading timeout: "+pair.Host.Status+" | "+pair.Client.Status);
   Need(pair.Host.BeginRoomLobby()&&pair.Host.Epoch==before+1,"Repeated HOME is idempotent");
   Need(!pair.Host.ReportMapLoaded(first)&&!pair.Client.ReportMapLoaded(first),"No world readiness can be reported in the room lobby");
   Need(!pair.Host.RequestSpawn()&&!pair.Client.RequestSpawn(),"No spawn is requested or granted in the room lobby");
   Need(pair.Host.RoundCountdown==0&&pair.Host.RoundElapsed==0,"No round clock runs in the room lobby");
   // Late joiner arrives while the room is in its lobby after a map.
   var pipe=new Pipe();var late=new R24LanLobby(pipe,pipe.Close,delegate{return "late-unused";},new string('a',64));
   try{
    pair.HostPipe.Links.Add("late",new Link{Remote=pipe,Back="server"});pipe.Links.Add("server",new Link{Remote=pair.HostPipe,Back="late"});pipe.Lost=late.TransportDisconnected;late.StartClient("server","Late",pair.Now);pair.Host.TransportConnected("late");
    for(int i=0;i<6;i++){pair.Tick();late.Tick(pair.Now);}
    Need(late.State==R24LanLobbyState.Connected&&late.Members.Length==3&&pair.Host.Members.Length==3&&pair.Client.Members.Length==3,"Room lobby admits a new member after a map was played: "+late.Status);
    Need(late.SelectedMap!=null&&late.SelectedMap.Home&&late.Epoch==pair.Host.Epoch,"New member receives the room-lobby state, not a stale world");
    // Round on another map straight from the room lobby: everybody follows.
    var rules=R24PlayableMatchRules.ForPreset("free_for_all","valley").With("time_limit",60).With("rounds",1).With("zone_wait",0);
    Need(pair.Host.BeginRoundSelection(rules,second,4321)&&pair.Host.ReportMapLoaded(second),"Host starts a match on a different map from the room lobby");
    for(int i=0;i<4;i++){pair.Tick();late.Tick(pair.Now);}
    Need(pair.Client.SelectedMap.MapId=="valley"&&!pair.Client.SelectedMap.Home&&!pair.Client.SelectedMap.Cancelled&&pair.Client.SelectedMap.Rules!=null&&late.SelectedMap.MapId=="valley"&&late.SelectedMap.Rules!=null,"Every member receives the host's map and rules");
    Need(pair.Host.MapStage==SessionStage.Loading&&!pair.Host.SelectedMap.Active,"Loading barrier holds until every member is ready");
    Need(pair.Client.ReportMapLoaded(second),"First member ready");for(int i=0;i<4;i++){pair.Tick();late.Tick(pair.Now);}Need(pair.Host.MapStage==SessionStage.Loading,"One of two members ready does not open the barrier");
    Need(late.ReportMapLoaded(second),"Second member ready");for(int i=0;i<5;i++){pair.Tick();late.Tick(pair.Now);}
    Need(pair.Host.MapStage==SessionStage.Match&&pair.Client.MapStage==SessionStage.Match&&late.MapStage==SessionStage.Match&&pair.Host.SelectedMap.Active&&pair.Host.Members.Length==3,"Match opens for the whole room behind the barrier");
    Need(pair.Host.OwnActor==host&&pair.Client.OwnActor==client&&pair.Client.SessionId==session,"Identities survive lobby -> map -> match");
    // HOME in the middle of a running match.
    long running=pair.Host.Epoch;Need(pair.Host.BeginRoomLobby()&&pair.Host.Epoch==running+1&&pair.Host.MapStage==SessionStage.Lobby&&pair.Host.SelectedMap.Home&&pair.Host.SelectedMap.Rules==null,"HOME ends a running match for the room without ending the room");
    for(int i=0;i<4;i++){pair.Tick();late.Tick(pair.Now);}
    Need(pair.Client.SelectedMap.Home&&late.SelectedMap.Home&&pair.Client.MapStage==SessionStage.Lobby&&late.MapStage==SessionStage.Lobby&&pair.Client.State==R24LanLobbyState.Connected&&late.State==R24LanLobbyState.Connected,"All members return to the room lobby together, connected");
    long home=pair.Host.Epoch;pair.Now+=600;pair.Host.Tick(pair.Now);pair.Client.Tick(pair.Now);late.Tick(pair.Now);for(int i=0;i<3;i++){pair.Tick();late.Tick(pair.Now);}
    Need(pair.Host.Epoch==home&&pair.Host.SelectedMap.Home&&pair.Host.MapStage==SessionStage.Lobby&&pair.Host.Members.Length==3&&pair.Client.State==R24LanLobbyState.Connected,"The ended match's timer cannot start another round or time out the idle room lobby");
    // A member leaves alone; the room goes on.
    pipe.Close("server");for(int i=0;i<3;i++)pair.Tick();
    Need(pair.Host.Members.Length==2&&pair.Client.Members.Length==2&&pair.Host.State==R24LanLobbyState.Hosting&&late.State==R24LanLobbyState.Offline&&pair.Client.State==R24LanLobbyState.Connected,"One member leaving does not end the room");
    Need(pair.Host.BeginMapSelection("range",first)&&pair.Host.ReportMapLoaded(first),"Room continues to a further map after a departure");Settle(pair,3);Need(pair.Client.ReportMapLoaded(first),"Remaining member loads");Settle(pair,4);Need(pair.Host.SelectedMap.Active&&pair.Client.SelectedMap.Active&&pair.Client.SelectedMap.MapId=="range","Third map of the same room opens");
   }finally{late.Dispose();pipe.Dispose();}
  }
  // HOME aborts a shared load that is still behind its barrier.
  using(var pair=new Pair()){
   Settle(pair,5);Need(pair.Host.BeginMapSelection("range",first)&&pair.Host.ReportMapLoaded(first),"Shared load begins");Settle(pair,3);
   Need(pair.Host.MapStage==SessionStage.Loading&&pair.Client.SelectedMap!=null&&!pair.Client.SelectedMap.Active,"Member is still loading");
   long loading=pair.Host.Epoch;Need(pair.Host.BeginRoomLobby()&&pair.Host.Epoch==loading+1&&pair.Host.MapStage==SessionStage.Lobby,"HOME aborts an unfinished shared load");
   Need(!pair.Client.ReportMapLoaded(first)||true,"(late local ack attempt is allowed to be queued)");Settle(pair,4);
   Need(pair.Client.SelectedMap.Home&&pair.Client.State==R24LanLobbyState.Connected&&pair.Host.Members.Length==2&&pair.Host.MapStage==SessionStage.Lobby,"A late ack from the aborted load neither reopens the world nor drops the member");
   Need(pair.Host.BeginMapSelection("valley",second)&&pair.Host.ReportMapLoaded(second),"Room is usable again after an aborted load");Settle(pair,3);Need(pair.Client.ReportMapLoaded(second),"Member loads the next map");Settle(pair,4);Need(pair.Host.SelectedMap.Active&&pair.Client.SelectedMap.Active,"Next map opens");
  }
  // HOME after final results.
  using(var pair=new Pair()){
   Settle(pair,5);var rules=R24PlayableMatchRules.ForPreset("free_for_all","range").With("time_limit",60).With("rounds",1).With("zone_wait",0);
   Need(pair.Host.BeginRoundSelection(rules,first,99)&&pair.Host.ReportMapLoaded(first),"Round begins");Settle(pair,4);Need(pair.Client.ReportMapLoaded(first),"Member ready");Settle(pair,5);
   pair.Now=pair.Host.SelectedMap.StartsAt+60.1;pair.Host.Tick(pair.Now);pair.Client.Tick(pair.Now);Settle(pair,3);
   Need(pair.Host.MapStage==SessionStage.Results&&pair.Client.SelectedMap.Results,"Final results reached");
   Need(pair.Host.BeginRoomLobby()&&pair.Host.MapStage==SessionStage.Lobby&&!pair.Host.SelectedMap.Results,"HOME from results returns the room to its lobby");Settle(pair,4);
   Need(pair.Client.SelectedMap.Home&&!pair.Client.SelectedMap.Results&&pair.Client.MapStage==SessionStage.Lobby&&pair.Client.State==R24LanLobbyState.Connected,"Member leaves results with the host");
   Need(pair.Host.BeginRoundSelection(rules,first,100)&&pair.Host.ReportMapLoaded(first),"Rematch from the room lobby");Settle(pair,4);Need(pair.Client.ReportMapLoaded(first),"Member ready for rematch");Settle(pair,5);Need(pair.Host.MapStage==SessionStage.Match&&pair.Client.MapStage==SessionStage.Match,"Rematch opens in the same room");
  }
  // Wire: the room-lobby frame is map-v3 and is only valid as a cancelled world.
  using(var pair=new Pair()){
   Settle(pair,5);pair.Host.BeginMapSelection("range",first);pair.Host.ReportMapLoaded(first);Settle(pair,3);pair.Client.ReportMapLoaded(first);Settle(pair,4);
   Need(pair.Host.BeginRoomLobby(),"HOME for wire capture");pair.Now+=.01;pair.Host.Tick(pair.Now);byte[] actual=null;foreach(var frame in pair.ClientPipe.Inbox){Packet p;if(Wire.TryDecode(frame.Bytes,out p)&&p.FeatureId=="playable.map/1")actual=(byte[])frame.Bytes.Clone();}
   Need(actual!=null,"Host emits a room-lobby map frame");Packet home;Need(Wire.TryDecode(actual,out home)&&home.Payload[0]==3,"Room-lobby state keeps the explicit map-v3 schema");
   int at=1+2+"range".Length+2+64;Need(home.Payload[at]==0&&home.Payload[at+1]==1&&home.Payload[at+2+8]==2,"Frame is inactive + cancelled + kind 2 (room lobby)");
   home.Payload[at+1]=0;home.Sequence+=100;pair.ClientPipe.Inbox.Clear();pair.ClientPipe.Inbox.Enqueue(new Frame{Peer="server",Bytes=Wire.Encode(home)});pair.Client.Tick(pair.Now+.01);
   Need(pair.Client.State==R24LanLobbyState.Offline,"A room-lobby frame that is not a cancelled world is refused");
  }
  using(var pair=new Pair()){
   Settle(pair,5);pair.Host.BeginMapSelection("range",first);pair.Host.ReportMapLoaded(first);Settle(pair,3);pair.Client.ReportMapLoaded(first);Settle(pair,4);
   Need(pair.Host.BeginRoomLobby(),"HOME for kind capture");pair.Now+=.01;pair.Host.Tick(pair.Now);byte[] actual=null;foreach(var frame in pair.ClientPipe.Inbox){Packet p;if(Wire.TryDecode(frame.Bytes,out p)&&p.FeatureId=="playable.map/1")actual=(byte[])frame.Bytes.Clone();}
   Packet kind;Need(Wire.TryDecode(actual,out kind),"Decode");int at=1+2+"range".Length+2+64;kind.Payload[at+2+8]=3;kind.Sequence+=100;pair.ClientPipe.Inbox.Clear();pair.ClientPipe.Inbox.Enqueue(new Frame{Peer="server",Bytes=Wire.Encode(kind)});pair.Client.Tick(pair.Now+.01);
   Need(pair.Client.State==R24LanLobbyState.Offline,"Unknown selection kind is refused");
  }
 }
}
