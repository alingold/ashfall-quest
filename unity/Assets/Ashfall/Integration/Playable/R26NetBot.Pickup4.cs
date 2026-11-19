#if UNITY_6000_0_OR_NEWER
using System;using System.Collections;using System.Collections.Generic;using UnityEngine;using Ashfall.Integration.Playable.Networking;using Ashfall.Integration.Playable.HandsWeapons;using Ashfall.Integration.Playable.Match;
namespace Ashfall.Integration.Playable {
 // PICKUP4 evidence (-ashfallNetBotPickup4 true): every peer takes backpacks to the cap, fills every slot with
 // guns, shields and food in warmup and in a started match, then drops each kind through the wheel hold and the
 // backpack page. Fixtures are native world items; discovery, admission, custody and drops stay on the normal paths.
 // Script steps: take:<backpack|gun|soda|shaker|banana>, wheel:<gun|kind>, bag:<gun|kind>[:trigger], full, match, fall, wait.
 public sealed partial class R25NetBot {
  const string Pickup4Script="take:backpack,take:soda,match,take:backpack,take:backpack,take:gun,take:soda,take:shaker,full,wheel:shaker,take:banana,full,wheel:soda,wheel:gun,bag:banana,bag:soda,bag:gun,take:gun,take:gun,take:soda,take:shaker,full,take:frag,wheel:frag,take:gun,ledger,ghost:gun,ledger,take:gun,ledger,wait:3,ledger";
  readonly List<string> pickup4Pass=new List<string>(),pickup4Fail=new List<string>();readonly Dictionary<string,string> pickup4Audits=new Dictionary<string,string>(StringComparer.Ordinal);int pickup4Serial;
  internal void Pickup4Begin(){StartCoroutine(Pickup4Run());}
  int Pickup4Count(string cls){if(cls=="backpack")return root.InventoryCapacity;if(cls=="gun")return root.Gameplay.OwnedWeaponActionIds.Length;int n=0;if(cls=="frag"){foreach(var g in root.Explosives.OwnedItems)if(g.Kind==cls)n++;return n;}foreach(var item in root.Consumables.OwnedItems)if(item.Kind==cls)n++;return n;}
  string Pickup4Owned(string cls){if(cls=="gun"){var ids=root.Gameplay.OwnedWeaponActionIds;return ids.Length==0?null:ids[ids.Length-1];}if(cls=="frag"){foreach(var g in root.Explosives.OwnedItems)if(g.Kind==cls)return g.Id;return null;}foreach(var item in root.Consumables.OwnedItems)if(item.Kind==cls)return item.Id;return null;}
  bool Pickup4Near(Vector3 p,ref float best){Vector3 head=root.Rig.Head.transform.position;float d=(p-root.Rig.transform.position).sqrMagnitude;if(d>=best||Physics.Linecast(head,p,1<<0,QueryTriggerInteraction.Ignore))return false;best=d;return true;}
  bool Pickup4Target(string cls,out Vector3 at,out string found){float best=9;at=Vector3.zero;found=null;Vector3 p,c;Quaternion q;bool flag;
   if(cls=="backpack"){foreach(string id in root.Supplies.WorldItemIds)if(id.Contains("backpack")&&root.Supplies.TryReadWorldItem(id,out p,out flag)&&!flag&&Pickup4Near(p,ref best)){at=p;found=id;}}
   else if(cls=="gun"){string instance;foreach(string id in root.Gameplay.WorldWeaponActionIds)if(root.Gameplay.TryReadWorldWeapon(id,out instance,out c,out p,out q)&&Pickup4Near(c,ref best)){at=c;found=id;}}
   else if(cls=="frag"){foreach(string id in root.Explosives.WorldItemIds)if(root.Explosives.TryReadWorldItem(id,out p,out q,out flag)&&!flag&&Pickup4Near(p,ref best)){at=p;found=id;}}
   else foreach(string id in root.Consumables.WorldItemIds){var info=root.Consumables.InspectItem(id);if(info!=null&&!info.Removed&&info.Owner==null&&info.Kind==cls&&root.Consumables.TryReadWorldItem(id,out p,out q,out flag)&&!flag&&Pickup4Near(p,ref best)){at=p;found=id;}}
   return found!=null;}
  void Pickup4Fixtures(R24LanLobby lobby,bool match){string[] kinds=match?new[]{"backpack","backpack","backpack","gun","gun","gun","soda","soda","soda","shaker","shaker","banana","banana","frag"}:new[]{"backpack","soda","gun"};
   foreach(var member in lobby.Members){R24LanPoseSnapshot pose;if(!lobby.TryReadPresence(member.Actor,out pose))continue;Vector3 origin=V(pose.Pose.Root.Position);int made=0;
    for(int i=0;i<kinds.Length;i++){double a=Math.PI*2*i/kinds.Length;Vector3 at=origin+new Vector3((float)Math.Cos(a)*.8f,.9f,(float)Math.Sin(a)*.8f);string id="p4/"+kinds[i]+"/"+(++pickup4Serial);bool ok;
     if(kinds[i]=="backpack")ok=root.Supplies.Spawn(id,"backpack",at,1);else if(kinds[i]=="gun")ok=root.Gameplay.SpawnWorldWeapon("cx4",1,at,Quaternion.identity,out id);else if(kinds[i]=="frag")ok=root.Explosives.SpawnWorldItem(id,"frag",1,at,Quaternion.identity);else ok=root.Consumables.Spawn(id,kinds[i],at,false);if(ok)made++;}
    Write("pickup4-fixture",(match?"match ":"warmup ")+member.Actor+" made="+made+"/"+kinds.Length);}}
  // SHIELD-STACKS evidence: unowned ground foods sharing one spot, locally and in the host registry.
  void Pickup4Ground(R24LanLobby lobby,string tag){var ids=new List<string>();var at=new List<Vector3>();Vector3 p;Quaternion q;bool owned;
   foreach(string id in root.Consumables.WorldItemIds){var info=root.Consumables.InspectItem(id);if(info!=null&&!info.Removed&&info.Owner==null&&root.Consumables.TryReadWorldItem(id,out p,out q,out owned)&&!owned){ids.Add(id+":"+info.Kind);at.Add(p);}}
   int pairs=0;string sample="";for(int i=0;i<ids.Count;i++)for(int j=i+1;j<ids.Count;j++){Vector3 d=at[i]-at[j];if(d.x*d.x+d.z*d.z<.0625f&&Mathf.Abs(d.y)<1.5f){pairs++;if(pairs<=4)sample+=" ["+ids[i]+" + "+ids[j]+" @"+at[i].ToString("F2")+" dy="+d.y.ToString("F2")+"]";}}
   var rows=lobby.SharedFoods;int free=0,rowPairs=0;for(int i=0;i<rows.Length;i++){if(rows[i].Owner!=""||rows[i].Consumed)continue;free++;for(int j=i+1;j<rows.Length;j++)if(rows[j].Owner==""&&!rows[j].Consumed&&Pickup4Stacked(V(rows[i].Position),V(rows[j].Position)))rowPairs++;}
   Write("pickup4-ground",tag+" localFoods="+ids.Count+" localStacks="+pairs+" lobbyFoods="+free+" lobbyStacks="+rowPairs+sample);}
  static bool Pickup4Stacked(Vector3 a,Vector3 b){Vector3 d=a-b;return d.x*d.x+d.z*d.z<.0625f&&Mathf.Abs(d.y)<1.5f;}
  IEnumerator Pickup4Run(){
   while(root==null||log==null||root.LanSession.Lobby==null){if(closed)yield break;yield return null;}
   enabled=false;requestedMap=true;string custom=Arg("-ashfallNetBotPickup4Script");string[] script=(custom.Length>0?custom:Pickup4Script).Split(',');string preset=Arg("-ashfallNetBotPickup4Preset");if(preset.Length==0)preset="free_for_all";
   bool pickup5=System.Array.Exists(script,s=>s.StartsWith("swap:"));if(pickup5)ReadRouteHeadset();
   int wanted;if(!Int32.TryParse(Arg("-ashfallNetBotPickup4Peers"),out wanted))wanted=2;string mapArg=Arg("-ashfallNetBotMap");
   float started=Time.realtimeSinceStartup,entered=-1,stepStart=-1,liveAt=-1,nextAudit=0,gripClock=0;int step=0,baseline=0;bool opened=false,warmFixtures=false,matchFixtures=false,requested=false,acted=false,grounded=false,fell=false;string stepItem=null;
   while(!closed&&Time.realtimeSinceStartup-started<duration+150){
    if(probe!=null)probe.Frame();float now=Time.realtimeSinceStartup,dt=Mathf.Clamp(Time.unscaledDeltaTime,0,.1f);var lobby=root.LanSession.Lobby;
    if(lobby==null){if(entered>=0){Write("pickup4-session-ended",root.UI.LanStatus);break;}yield return null;continue;}
    if(lobby.MemberCount>1)connected=true;bool hosting=lobby.State==R24LanLobbyState.Hosting;
    if(mode=="host"&&!opened&&lobby.MemberCount>=wanted&&now-started>3){string map=mapArg.Length>0&&World.R23MapCatalog.Find(mapArg)!=null?mapArg:root.Map.MapId;opened=root.LanSession.Pickup4OpenWarmup(map);Write("pickup4-transition","warmup "+map+" requested="+opened+" members="+lobby.MemberCount);}
    bool active=root.Ready&&!root.Lobby.Visible&&!root.LanSession.MapEntryBlocked&&lobby.SelectedMap!=null&&lobby.SelectedMap.Active&&!lobby.SelectedMap.Cancelled&&!lobby.SelectedMap.Home;
    if(active&&entered<0){entered=now;playBegan=now;if(probe!=null)probe.PlayBegan=now;played=true;Write("pickup4-entry","warmup "+root.Map.MapId);}
    if(!active||entered<0){yield return null;continue;}
    float age=now-entered;bool live=root.Pickup4Live;if(live&&liveAt<0){liveAt=now;Write("pickup4-live",root.Pickup4State);}
    if(hosting){if(!warmFixtures&&age>=4){warmFixtures=true;if(!pickup5)Pickup4Fixtures(lobby,false);}if(!matchFixtures&&live&&now-liveAt>=3){matchFixtures=true;if(pickup5)Pickup5Fixtures(lobby);else Pickup4Fixtures(lobby,true);}
     if(now>=nextAudit){nextAudit=now+.5f;foreach(var member in lobby.Members){string audit=lobby.SlotAudit(member.Actor),last;if(!pickup4Audits.TryGetValue(member.Actor,out last)||last!=audit){pickup4Audits[member.Actor]=audit;Write("pickup4-host-audit",member.Actor+(member.Actor==lobby.OwnActor?" (host) ":" ")+audit);}}}}
    if(live&&!grounded&&now-liveAt>=2){grounded=true;Pickup4Ground(lobby,"match-start");}if(live&&now-liveAt>=40&&now-liveAt<40.1f+dt)Pickup4Ground(lobby,"match-40s");
    Vector3 head=root.Rig.transform.position+Vector3.up*1.7f,left=head+root.Rig.transform.right*-.3f+root.Rig.transform.forward*.3f-Vector3.up*.4f,right=head+root.Rig.transform.right*.3f+root.Rig.transform.forward*.4f-Vector3.up*.4f;
    Quaternion aim=root.Rig.transform.rotation;float grip=0;bool primary=false,leftGrip=false;
    if(step<script.Length&&age>=6&&root.Gameplay.Health>0){
     string[] parts=script[step].Split(':');string kind=parts[0],cls=parts.Length>1?parts[1]:"";if(stepStart<0){stepStart=now;acted=false;stepItem=null;gripClock=0;baseline=cls.Length>0?Pickup4Count(cls):0;}
     float t=now-stepStart;bool done=false,ok=false;string note="";
     if(t>=1.2f){
      if(kind=="swap")Pickup5Step(lobby,cls,t,ref left,ref right,ref aim,ref grip,ref leftGrip,out done,out ok,out note);
      else if(kind=="take"){Vector3 target;string id;if(Pickup4Target(cls,out target,out id)){stepItem=id;gripClock+=dt;if(cls=="gun"&&t>=4&&(int)(t*2)!=(int)((t-dt)*2))Write("pickup4-take",script[step]+" "+id+" "+root.Pickup3TargetState(id)+" "+root.Pickup4Objective+" | "+root.Pickup3State);right=target-root.Rig.transform.forward*.12f;aim=Quaternion.LookRotation(target-right)*Quaternion.Inverse(R23TrackedGripPose.LocalRotation);grip=gripClock%2<1.6f?.9f:0;}
       if(Pickup4Count(cls)>baseline){done=ok=true;}else if(t>16){done=true;note=stepItem==null?"no-target":"refused";}}
      else if(kind=="wheel"){string id=Pickup4Owned(cls);if(stepItem==null)stepItem=id;int slot=stepItem==null?-1:root.WeaponWheel.Pickup4SlotOf(cls=="gun"?stepItem:cls);if(now>=gripClock){gripClock=now+.5f;Write("pickup4-wheel",script[step]+" t="+t.ToString("F1")+" slot="+slot+" "+root.WeaponWheel.Pickup4Slot(slot)+" "+root.WeaponWheel.Pickup3State);}
       if(slot>=0&&t<7){primary=true;if(root.WeaponWheel.Active){var point=root.WeaponWheel.Pickup3SlotPoint(slot);aim=Quaternion.LookRotation(point-right)*Quaternion.Inverse(R23TrackedGripPose.LocalRotation);}}
       if(Pickup4Count(cls)<baseline){done=ok=true;}else if(t>9){done=true;note=slot<0?"no-slot":"not-dropped wheel="+root.WeaponWheel.Pickup3State;}}
      else if(kind=="bag"){if(stepItem==null)stepItem=Pickup4Owned(cls);if(t<1.4f)root.Pickup4OpenBackpack();else if(!acted&&t>=2){acted=true;note="gesture="+(stepItem!=null&&root.Pickup4BagGesture(stepItem,false));Write("pickup4-bag",script[step]+" "+stepItem+" "+note+" page="+root.UI.Page);}
       if(Pickup4Count(cls)<baseline){done=ok=true;}else if(t>6){done=true;note+=" not-dropped";}if(done)root.Pickup4BagGesture("backpack/close",false);}
      else if(kind=="ghost"){Vector3 target=Vector3.zero;string id;if(hosting&&stepItem==null&&Pickup4Target("gun",out target,out id))stepItem=id;string ident;Vector3 p0;Quaternion q0;bool seen=stepItem!=null&&root.Gameplay.TryReadWorldWeapon(stepItem,out ident,out target,out p0,out q0);if(seen&&!acted)right=target-root.Rig.transform.forward*.12f;
       if(!acted&&t>=2.2f){acted=true;if(seen){root.LanSession.PublishPickup4Tracking();note="injected="+lobby.RequestLoot(stepItem,1,false,new Ashfall.Core.Float3(target.x,target.y,target.z))+" refusal="+lobby.LastPickupRejection+" hostUsed="+lobby.SlotAuditUsed(lobby.OwnActor)+" localUsed="+root.Pickup4Used;}else note="no-target";Write("pickup4-ghost",note);}
       if(!hosting||t>=5){done=true;ok=!hosting||lobby.SlotAuditUsed(lobby.OwnActor)==root.Pickup4Used&&root.LanSession.OwnGhostReleases>0;note+=" after hostUsed="+lobby.SlotAuditUsed(lobby.OwnActor)+" localUsed="+root.Pickup4Used+" ghostReleases="+root.LanSession.OwnGhostReleases;}}
      else if(kind=="ledger"){done=true;ok=!hosting||lobby.SlotAuditUsed(lobby.OwnActor)==root.Pickup4Used&&lobby.SlotAuditCapacity(lobby.OwnActor)==root.InventoryCapacity;note="hostUsed="+(hosting?lobby.SlotAuditUsed(lobby.OwnActor):-1)+" localUsed="+root.Pickup4Used+" ghostReleases="+root.LanSession.OwnGhostReleases;}
      else if(kind=="full"){done=true;ok=root.Pickup4Used==root.InventoryCapacity&&(!hosting||lobby.SlotAuditUsed(lobby.OwnActor)==root.Pickup4Used&&lobby.SlotAuditCapacity(lobby.OwnActor)==root.InventoryCapacity);}
      else if(kind=="match"){if(mode=="host"&&!requested&&t>=(pickup5?14:8)){requested=true;var rules=R24PlayableMatchRules.ForPreset(preset,root.Map.MapId).With("zone_enabled",false);bool routed=root.LanSession.RouteRoundRequest(rules);Write("pickup4-transition","START MATCH "+preset+" routed="+routed);}
       if(live&&now-liveAt>=5){done=ok=true;}else if(t>120){done=true;note="match-not-live";}}
      else if(kind=="fall"){if(hosting&&!fell){fell=true;foreach(var member in lobby.Members){R24LanHealthState life;if(lobby.TryReadHealth(member.Actor,out life)){var result=lobby.ApplyHostHealthFall(member.Actor,life.Revision);Write("pickup4-fall",member.Actor+" accepted="+result.Accepted+" reason="+result.Reason);}}}if(t>12){done=ok=true;fell=false;}}
      else if(t>(cls.Length>0?Single.Parse(cls,System.Globalization.CultureInfo.InvariantCulture):3)){done=ok=true;}
     }
     if(done){string label=step+":"+script[step];(ok?pickup4Pass:pickup4Fail).Add(label);Write("pickup4-step",label+" ok="+ok+" t="+(now-stepStart).ToString("F1")+" item="+(stepItem??"none")+" "+note+" | local "+root.Pickup4State+(hosting?" | host "+lobby.SlotAudit(lobby.OwnActor)+" refusal="+lobby.LastPickupRejection:""));step++;stepStart=-1;}
    }
    if(pickup5){RouteHeadset(now,false);if(routeSeated>0&&!routeSeatedDone&&age>=1.2f&&root.Rig.Calibration!=null){routeSeatedDone=true;root.Rig.Calibration.RequestRecalibration();}}
    root.Rig.NetBotSample(Vector2.zero,pickup5?routeHeight:1.7f,left,right,aim,leftGrip?0:grip,0,dt,leftGrip?grip:0,pickup5&&swapStep!=null&&swapStep.EndsWith("0")?(Quaternion?)aim:null);if(pickup5)ObserveWindowRoute(now);root.Rig.Pickup4Primary(primary);
    if(captures<2&&liveAt>=0&&now-liveAt>=(captures==0?12:60))Capture(440+captures++);
    if(step>=script.Length&&stepStart<0){if(mode!="host"||age>=duration)break;}else if(age>=duration+60)break;
    yield return null;
   }
   for(int i=step;i<script.Length;i++)pickup4Fail.Add(i+":"+script[i]+"(not-run)");bool passed=pickup4Fail.Count==0&&pickup4Pass.Count>0;
   Write("pickup4-summary","passed="+pickup4Pass.Count+" failed="+pickup4Fail.Count+" "+(root!=null?root.Pickup4State:""),pickup4Pass.ToArray(),pickup4Fail.ToArray());
   closed=true;Passed=passed;Finished=true;root.LanSession.Leave();if(probe!=null){probe.Dispose();probe=null;}log.Dispose();log=null;if(!Application.isEditor)Application.Quit(passed?0:2);
  }
 }
}
#endif
