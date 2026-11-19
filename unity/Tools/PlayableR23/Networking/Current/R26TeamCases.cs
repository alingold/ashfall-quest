using System;
using System.Collections.Generic;
using System.Linq;
using Ashfall.Core;
using Ashfall.Modules.Flight;
using Ashfall.Integration.Playable.Networking;
using Ashfall.Integration.Playable.Match;
using Ashfall.Integration.Playable.TeamLaunch;
partial class LobbyCases {
 sealed class DeckFloor:IR24LaunchWorld {
  public bool HasSafeSpawn{get{return false;}}public bool SafeSpawn(Float3 p){return true;}
  public bool RaycastDown(Float3 p,float end,out R24LaunchFloor f){f=new R24LaunchFloor{Point=new Float3(p.X,0,p.Z),NormalY=1};return true;}
 }
 sealed class PlatformPort:IR24LanPodWorld,IR26LanPlatformWorld {
  public Float3 Feet;public bool OnPlatform(Float3 p){return p.Equals(Feet);}
  public bool TryPlan(string actor,int team,string pod,Float3 p,double clock,out CapsulePlan plan){plan=null;return false;}
 }
 static void Teams2(){
  for(int teams=2;teams<=8;teams++){
   for(int pattern=0;pattern<2;pattern++){
    var runtime=new R24TeamLaunchRuntime(42);
    var map=new R24LaunchMap{Id="archive:alpha",Minimum=new Float3(-100,0,-100),Maximum=new Float3(100,0,100),Top=20};
    Need(runtime.Configure(map,new R24LaunchRules{Live=true,Stage="match",Mode="revive",Teams=teams,Epoch=1}),"configure");
    var layout=runtime.Layout;Need(layout.PlatformCount==2&&layout.TeamCount==teams,"physical decks independent of teams");
    var rows=new List<R24LaunchAssignment>();var keys=new HashSet<string>();var feet=new HashSet<Float3>();
    for(int i=0;i<24;i++){
     int team=pattern==0?i%teams:teams-1;var row=layout.Assign("p"+i,team,rows);rows.Add(row);
     Need(row.Team==team&&keys.Add(row.PodId)&&feet.Add(row.Spawn)&&layout.PlatformAt(row.Spawn)==row.Platform,"unique allocated seat");
     var cabin=layout.Pod(row.Platform,row.Slot);var rider=new R24LaunchPlayer{Id=row.PlayerId,Team=team,Alive=true,OnDeck=true,Position=cabin.Origin};
     R24LaunchRide ride;Need(runtime.TryLaunch(rider,row.PodId,0,new[]{new Float3(0,0,0)},null,new Float3(),new DeckFloor(),out ride),"host physical launch");
     Need(ride.Plan.Team==team&&ride.Plan.PodId==row.PodId,"preserve real team in ride");
     rider.Id="other";Need(!runtime.TryLaunch(rider,row.PodId,0,null,null,new Float3(),new DeckFloor(),out ride),"cannot reuse physical cabin");
    }
    Need(layout.Assign("late",0,null,runtime.Used)==null,"spent pods stay unavailable after ground relocation");
    Need(layout.Assign("extra",0,rows)==null,"25th seat refused");Need(rows.Count(r=>r.Platform==0)==12&&rows.Count(r=>r.Platform==1)==12,"12 per deck");
    for(int sites=1;sites<=teams;sites++){
     var labels=R26SpawnSides.Split(Enumerable.Range(0,sites).Select(i=>new Float3(i,0,i)).ToArray(),teams);
     for(int t=0;t<teams;t++)Need(labels.Contains(R26SpawnSides.AnchorSide(t,teams,sites)),"every team has a sparse spawn anchor");
    }
   }
  }
  Console.WriteLine("PASS 14 seat matrices, 336 unique player placements and launches, pod exclusivity, overflow and sparse sides");
  string hash=new string('a',64);int serial=0;double now=0;
  var pipe=new Pipe();var host=new R24LanLobby(pipe,pipe.Close,delegate{return "team"+(++serial);},hash);pipe.Lost=host.TransportDisconnected;
  var guests=new List<R24LanLobby>();var pipes=new List<Pipe>();
  Action tick=delegate{for(int k=0;k<8;k++){now+=.05;host.Tick(now);foreach(var c in guests)c.Tick(now);}};
  Action join=delegate{
   var p=new Pipe();var c=new R24LanLobby(p,p.Close,delegate{return "unused";},hash);string key="peer"+guests.Count;
   pipe.Links.Add(key,new Link{Remote=p,Back="server"});p.Links.Add("server",new Link{Remote=pipe,Back=key});p.Lost=c.TransportDisconnected;
   c.StartClient("server",key,now);host.TransportConnected(key);guests.Add(c);pipes.Add(p);tick();
  };
  try{
   var rules=R24PlayableMatchRules.ForPreset("team_revive","range");host.StartHost("teams","host","range",hash,2,rules);
   var port=new PlatformPort();host.BindSpawnWorld(new MaxWorld{Host=host});host.BindPodWorld(port);
   for(int i=0;i<8;i++)join();
   foreach(int n in new[]{8,7,6,5,4,3,2,0,3}){
    rules=n==0?R24PlayableMatchRules.ForPreset("free_for_all","range"):R24PlayableMatchRules.ForPreset("team_revive","range").With("teams",n);
    Need(host.HostSetRoomTeamCount(rules),"host configures");tick();
    if(n>0){var sizes=Enumerable.Range(0,n).Select(t=>host.Members.Count(m=>m.Team==t));Need(sizes.Max()-sizes.Min()<=1,"balanced on resize");}
    foreach(var c in guests)Need(c.Members.Select(m=>m.Team).SequenceEqual(host.Members.Select(m=>m.Team)),"all guest teams agree");
   }
   foreach(var m in host.Members)Need(host.HostAssignRoomTeam(m.Actor,2),"legal host override");
   join();Need(host.Members.Count(m=>m.Team==2)<=4,"join repairs host imbalance");
   Need(!guests[0].HostAssignRoomTeam(guests[0].OwnActor,2)&&!guests[0].HostSetRoomTeamCount(rules),"client cannot mutate");
   Need(!host.HostAssignRoomTeam(guests[0].OwnActor,3)&&!host.HostAssignRoomTeam("missing",0),"bad edit rejected");
   string edited=guests[0].OwnActor;Need(host.HostAssignRoomTeam(edited,2),"final host edit");tick();
   Need(host.BeginRoundSelection(rules.With("protection",0),hash,42)&&host.ReportMapLoaded(hash),"round start");tick();
   foreach(var c in guests)Need(c.ReportMapLoaded(hash),"guest ready");for(int k=0;k<15;k++)tick();
   Need(!host.HostAssignRoomTeam(edited,0),"match freezes assignment");Need(host.RequestSpawn(),"host spawn");
   foreach(var c in guests)Need(c.RequestSpawn(),"guest spawn request");tick();
   foreach(var m in host.Members){R24LanHealthState h;Need(host.TryReadHealth(m.Actor,out h)&&h.Team==m.Team,"actual health matches team");}
   Need(guests[0].Members.Single(m=>m.Actor==edited).Team==2,"override survives match epoch");
   R24LanSpawn spawn;Need(host.TryReadSpawn(edited,out spawn),"spawn present");port.Feet=spawn.Feet;
   var other=host.Members.First(m=>m.Team!=2).Actor;R24LanHealthState before;Need(host.TryReadHealth(edited,out before),"victim health present");
   Need(host.PlatformProtected(edited)&&!host.PlatformProtected(other),"authority geometry fallback before first tracking");
   Need(!host.ApplyHostHealthLoss(edited,before.Revision,2,0,true).Accepted,"protected victim direct damage");
   foreach(var pair in new[]{new[]{edited,other},new[]{other,edited}}){
    Need(!(bool)InvokeMax(host,"ApplyCombatDamage",pair[0],pair[1],2f,false,int.MinValue,1,null,null),"either protected endpoint rejects damage");
   }
   port.Feet=new Float3(12345,0,0);Need((bool)InvokeMax(host,"ApplyCombatDamage",other,edited,2f,false,int.MinValue,1,null,null),"same damage accepted off platform");
   foreach(var losing in new[]{0,1}){
    foreach(var m in host.Members.Where(m=>m.Team==losing)){
     R24LanHealthState h;Need(host.TryReadHealth(m.Actor,out h),"before elimination");
     Need(host.ApplyHostHealthFall(m.Actor,h.Revision).Accepted,"host eliminates actual actor");
    }
    tick();Need(host.SelectedMap.Results==(losing==1),"last remaining side only");
   }
   foreach(var c in guests){
    var row=c.MatchEndResults.ForActor(c.OwnActor);Need(row!=null&&row.Outcome==(row.Team==2?1:-1),"third-side victory receipt");
   }
   Console.WriteLine("PASS 3-side elimination: two sides alive continues, third team wins; nine personal client receipts");
   Console.WriteLine("PASS host edits, all guest roster snapshots, 2..8 and FFA, join rebalance, match lock, authority Health, and positive/negative damage controls");
  }finally{foreach(var c in guests)c.Dispose();host.Dispose();foreach(var p in pipes)p.Dispose();pipe.Dispose();}
 }
}
