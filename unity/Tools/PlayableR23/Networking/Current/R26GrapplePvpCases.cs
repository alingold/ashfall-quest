using System;using System.Reflection;using Ashfall.Core;using Ashfall.Modules.Networking;using Ashfall.Integration.Playable.Networking;using Ashfall.Integration.Playable.Traversal;
partial class LobbyCases
{
 internal static void R26GrapplePvp()
 {
  int before=checks;
  using(var f=new PullFixture()){
   Need(f.Fire(),"match pull admitted");
   Need(!f.Fire(),"one shooter cannot loop pull while active");
   f.Pair.Host.PublishWeb(f.Pair.Host.GrapplePulls.Length>0?new R25WebState(false,new Float3(),false,new Float3()):null,3);f.Pair.Tick();
   Need(f.Pair.Host.GrapplePulls.Length==0,"shooter release clears victim on host");
   for(int i=0;i<12;i++)Need(!f.Fire(),"repeated firing cannot bypass cooldown/immunity");
   f.Sample();Need(f.Pair.Client.GrapplePulls.Length==0,"release snapshot removes client rope");
  }
  foreach(bool shooter in new[]{false,true})using(var f=new PullFixture()){
   Need(f.Fire(),"damage starts");string actor=shooter?f.Pair.Host.OwnActor:f.Pair.Client.OwnActor;var h=Health(f.Pair.Host,actor);
   Need(f.Pair.Host.ApplyHostHealthLoss(actor,h.Revision,1,0,false).Accepted,"nonlethal host damage applied");f.Pair.Tick();
   Need(f.Pair.Host.GrapplePulls.Length==0,"any health damage cancels pull, either endpoint");
  }
  using(var f=new PullFixture()){
   Need(f.Fire(),"LOS lifecycle starts");typeof(R24LanLobby).GetField("rawWorldClear",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(f.Pair.Host,(Func<Float3,Float3,bool>)delegate{return false;});f.Pair.Tick();
   Need(f.Pair.Host.GrapplePulls.Length==0,"new obstruction cancels tether");
  }
  using(var f=new PullFixture()){
   var h=Health(f.Pair.Host,f.Pair.Client.OwnActor);Need(f.Pair.Host.ApplyHostHealthLoss(h.Actor,h.Revision,h.Health,h.Shield,false).Accepted,"actual death");f.Pair.Tick();Need(!f.Fire(),"dead target refuses pull");
  }
  using(var f=new PullFixture()){
   f.Sample();var pose=Body(f.Hash,Rev(f.Pair.Host),8,0,0,rx:0,ry:1.25f,rz:0);
   pose=new R24LanRawTracking(pose.MapHash,pose.Flags,pose.Root,pose.Head,pose.Left,new R24LanTransform(pose.Right.Position,new R24LanRotation(-.5f,-.5f,-.5f,.5f)),0,0,pose.SpawnRevision);
   Need(f.Pair.Host.PublishRawTracking(pose),"real corrected grip raw sample");
   Need(f.Pair.Host.PublishWeb(new R25WebState(false,new Float3(),false,new Float3()),5),"new corrected right opcode");Need(f.Pair.Host.GrapplePulls.Length==1,"rendered weapon bore resolves correct target");
   Need(!f.Pair.Host.PublishWeb(new R25WebState(false,new Float3(),false,new Float3()),6),"unknown opcode refused");
  }
  using(var f=new PullFixture()){
   for(int i=0;i<100;i++){f.VictimX-=.3f;f.Sample();}
   Need(!f.Fire(),"outside25metres refuses attachment");
  }
  using(var f=new PullFixture()){
   f.GuestAim=true;Need(f.Pair.Host.RegisterWorldWeapon("guest/grapple","grapple",2,new Float3(0,1.2f,.2f)),"client gun registered");
   f.Pair.Client.RequestLoot("cx4",1,true,new Float3(0,1.2f,.2f));for(int i=0;i<4;i++)f.Sample();
   f.Pair.Client.RequestLoot("guest/grapple",1,false,new Float3());for(int i=0;i<4;i++)f.Sample();
   f.Pair.Client.RequestEquipment("guest/grapple",1,false);for(int i=0;i<3;i++)f.Sample();
   f.Pair.Client.PublishWeb(new R25WebState(false,new Float3(),false,new Float3()),1);for(int i=0;i<3;i++)f.Sample();Need(f.Pair.Host.GrapplePulls.Length==1,"authenticated guest shooter admitted");
   f.Pair.Client.PublishWeb(new R25WebState(false,new Float3(),false,new Float3()),3);for(int i=0;i<3;i++)f.Sample();Need(f.Pair.Host.GrapplePulls.Length==0,"guest release traverses real datagram path");
  }
  using(var p=new Pair()){
   for(int i=0;i<6;i++)p.Tick();
   p.Host.BindRawTrackingWorldQuery(delegate{return true;});p.Host.BindCombatWorld(delegate(Float3 o,Float3 d,float r){return r;});p.Host.BindRoomGrappleOrigin(new Float3());p.Client.EnableRoomGrappleTracking();
   Need(p.Host.RoomGrapple&&p.Client.RoomGrapple,"actual admitted room without selected map");
   var host=Body(new string('b',64),0,0,0,0,rx:0,ry:1.25f,rz:0);
   host=new R24LanRawTracking(host.MapHash,host.Flags,host.Root,host.Head,host.Left,host.Right,0,1,0);
   Need(p.Host.PublishRawTracking(host),"room sample admitted at bound menu anchor");
   Need(p.Client.PublishRawTracking(Body(new string('b',64),0,0,0,2.9f)),"room guest sends raw sample");p.Tick();p.Tick();
   R24LanRawTrackingSample combat;Need(!p.Host.TryReadRawTracking(p.Host.OwnActor,p.Now,out combat),"room pose grants no match tracking authority");
   Need(p.Host.PublishWeb(new R25WebState(false,new Float3(),false,new Float3()),1),"room fire publication");
   Need(p.Host.GrapplePulls.Length==0,"Home room has no inventory-free grapple pull");
   p.Tick();p.Tick();Need(p.Client.GrapplePulls.Length==0,"Guest receives no home room pull");
   p.Host.PublishWeb(new R25WebState(false,new Float3(),false,new Float3()),3);p.Tick();p.Tick();Need(p.Client.GrapplePulls.Length==0,"Room release leaves guest without a tether");
   Need(!p.Host.PublishRawTracking(Body(new string('b',64),0,100,0,0)),"room validator rejects teleport");
  }
  using(var f=new PullFixture()){
   Need(f.Pair.Host.BeginRoomLobby(),"HOME transitions real Session epoch");for(int i=0;i<3;i++)f.Pair.Tick();
   f.Pair.Host.BindRoomGrappleOrigin(new Float3(8,0,0));f.Pair.Client.EnableRoomGrappleTracking();
   var host=Body(f.Hash,0,8,0,0,rx:0,ry:1.25f,rz:0,rqy:-.70710677f,rqw:.70710677f);
   host=new R24LanRawTracking(host.MapHash,host.Flags,host.Root,host.Head,host.Left,host.Right,0,1,0);
   Need(f.Pair.Host.PublishRawTracking(host),"HOME accepts room raw after old map retired");f.Pair.Client.PublishRawTracking(Body(f.Hash,0,6,0,0));f.Pair.Tick();f.Pair.Tick();
   f.Pair.Host.PublishWeb(new R25WebState(false,new Float3(),false,new Float3()),1);Need(f.Pair.Host.GrapplePulls.Length==0,"Returning HOME disables grapple pull");f.Pair.Tick();f.Pair.Tick();Need(f.Pair.Client.GrapplePulls.Length==0,"Returning HOME leaves guest without a pull");
  }
  Console.WriteLine("GRAPPLEPVP "+(checks-before)+" focused LAN assertions PASS");
 }
}
