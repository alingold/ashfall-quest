using System;using Ashfall.Core;using Ashfall.Integration.Playable.Networking;using Ashfall.Integration.Playable.Match;
partial class LobbyCases {
 internal static void SwordMain(){SwordWallBehind();SwordWallBetween();SwordFastSwing();SwordBunched();SwordGuestConfirm();Console.WriteLine("SWORD CASES PASS checks="+checks);}
 // Host combat world with one infinite wall plane at z=wall[0] (NaN = open range).
 static Pair SwordStart(float[] wall,out string hash){var pair=new Pair();for(int i=0;i<5;i++)pair.Tick();pair.Host.BindCombatWorld(delegate(Float3 o,Float3 d,float range){float w=wall[0];if(Single.IsNaN(w))return range;float end=o.Z+d.Z*range;if((o.Z-w)*(end-w)>=0)return range;return (w-o.Z)/d.Z;});hash=new string('b',64);StartRound(pair,new HazardWorld(),R24PlayableMatchRules.ForPreset("free_for_all","range").With("zone_enabled",false).With("loadout","swords").With("protection",0).With("starting_shield",0),hash);return pair;}
 static float SwordHp(Pair pair,R24LanLobby victim){R24LanHealthState h;Need(pair.Host.TryReadHealth(victim.OwnActor,out h),"victim health");return h.Health+h.Shield;}
 static int SwordConfirms(R24LanLobby lobby,string victim){int n=0;R26HitConfirm c;while(lobby.TryDequeueHitConfirm(out c)){Need(c.Victim==victim&&c.Shield+c.Health>0,"Confirm names the struck actor with the applied loss");n++;}return n;}
 // Host approaches the guest (at origin) along x with the katana along +z, as MeleeAuthority does, then cuts in .1 m steps.
 static void SwordApproach(Pair pair,string hash,float stop,out float x){x=8;while(x>stop){pair.Host.RequestEquipment("katana",1,false);pair.Host.PublishRawTracking(Body(hash,Rev(pair.Host),x,0,0,7,rx:0,ry:1.2f,rz:-.6f));pair.Client.PublishRawTracking(Body(hash,Rev(pair.Client),0,0,0));pair.Tick();x=Math.Max(stop,x-.3f);}}
 static void SwordPose(Pair pair,string hash,float x,float hand,bool tick){pair.Host.RequestEquipment("katana",1,false);pair.Host.PublishRawTracking(Body(hash,Rev(pair.Host),x,0,0,7,rx:hand-x,ry:1.2f,rz:-.6f));pair.Client.PublishRawTracking(Body(hash,Rev(pair.Client),0,0,0));if(tick)pair.Tick();}
 static void SwordWallBehind(){var wall=new[]{.35f};string hash;using(var pair=SwordStart(wall,out hash)){float x;SwordApproach(pair,hash,1.5f,out x);SwordPose(pair,hash,x,.9f,true);SwordPose(pair,hash,x,.9f,true);float hp=SwordHp(pair,pair.Client);float hand=.9f;
  // Blade spans z -.475...515: its tip is inside the wall at z=.35 behind the victim for the whole cut.
  for(int i=0;i<8;i++){hand-=.1f;SwordPose(pair,hash,x,hand,true);}for(int i=0;i<3;i++)SwordPose(pair,hash,x,hand,true);
  Need(SwordHp(pair,pair.Client)<hp,"SWORD-HITREG: a blade whose tip touches the wall behind the target still lands");Need(pair.Host.MeleeHostResets==0||pair.Host.MeleeHostLastReset!="map","no map suspension");
  Need(SwordConfirms(pair.Host,pair.Client.OwnActor)==1,"Host attacker gets exactly one hit confirm (damage number) for one landed swing");
  R25MeleeHit m;Need(pair.Host.TryDequeueMeleeHit(out m)&&m.Kind=="blade","Blade contact event still emitted");
  Console.WriteLine("wall-behind: applied="+pair.Host.MeleeHostApplied+" entries="+pair.Host.MeleeHostEntries+" resets="+pair.Host.MeleeHostResets+" samples="+pair.Host.MeleeHostSamples);}}
 static void SwordWallBetween(){var wall=new[]{-.3f};string hash;using(var pair=SwordStart(wall,out hash)){float x;SwordApproach(pair,hash,1.5f,out x);SwordPose(pair,hash,x,.9f,true);SwordPose(pair,hash,x,.9f,true);float hp=SwordHp(pair,pair.Client);float hand=.9f;
  // The wall at z=-.3 stands between the hand (z=-.6) and the victim's body spheres (z=0).
  for(int i=0;i<8;i++){hand-=.1f;SwordPose(pair,hash,x,hand,true);}for(int i=0;i<3;i++)SwordPose(pair,hash,x,hand,true);
  Need(SwordHp(pair,pair.Client)==hp,"SWORD-HITREG: no blade damage through a wall between hand and target");Need(SwordConfirms(pair.Host,pair.Client.OwnActor)==0,"No confirm for a blocked swing");Need(pair.Host.MeleeHostApplied==0,"No applied melee");
  Console.WriteLine("wall-between: applied="+pair.Host.MeleeHostApplied+" entries="+pair.Host.MeleeHostEntries);}}
 static void SwordFastSwing(){var wall=new[]{Single.NaN};string hash;using(var pair=SwordStart(wall,out hash)){float x;SwordApproach(pair,hash,1.2f,out x);
  // Hand .45 m in front of the attacker; the blade yaws from +60 deg (pointing away) to -90 deg (through the chest) in ONE sample: tip travel ~2.15 m.
  Action<double> yaw=delegate(double degrees){double half=degrees*Math.PI/360;pair.Host.RequestEquipment("katana",1,false);pair.Host.PublishRawTracking(Body(hash,Rev(pair.Host),x,0,0,7,rx:-.45f,ry:1.2f,rz:0,rqy:(float)Math.Sin(half),rqw:(float)Math.Cos(half)));pair.Client.PublishRawTracking(Body(hash,Rev(pair.Client),0,0,0));pair.Tick();};
  yaw(60);yaw(60);yaw(60);float hp=SwordHp(pair,pair.Client);Need(pair.Host.MeleeHostApplied==0,"Wind-up pose does not touch");yaw(-90);for(int i=0;i<3;i++)yaw(-90);
  Need(pair.Host.MeleeHostSplit>=1,"Fast swing replayed as sub-frames");Need(SwordHp(pair,pair.Client)<hp,"SWORD-HITREG: a swing faster than 1 m per tracking sample still lands");Need(SwordConfirms(pair.Host,pair.Client.OwnActor)==1,"One confirm for the fast swing");
  Console.WriteLine("fast-swing: applied="+pair.Host.MeleeHostApplied+" split="+pair.Host.MeleeHostSplit+" resets="+pair.Host.MeleeHostResets+" lastReset="+pair.Host.MeleeHostLastReset);}}
 static void SwordBunched(){var wall=new[]{Single.NaN};string hash;using(var pair=SwordStart(wall,out hash)){float x;SwordApproach(pair,hash,1.5f,out x);SwordPose(pair,hash,x,.9f,true);SwordPose(pair,hash,x,.9f,true);float hp=SwordHp(pair,pair.Client);
  // Two samples in one host tick (arrival dt 0): the second one carries the blade into the body.
  SwordPose(pair,hash,x,.5f,false);SwordPose(pair,hash,x,.1f,true);for(int i=0;i<3;i++)SwordPose(pair,hash,x,.1f,true);
  Need(pair.Host.MeleeHostBunched>=1,"Bunched sample counted");Need(SwordHp(pair,pair.Client)<hp,"SWORD-HITREG: samples bunched into one host tick keep the stroke and land");
  Console.WriteLine("bunched: applied="+pair.Host.MeleeHostApplied+" bunched="+pair.Host.MeleeHostBunched+" resets="+pair.Host.MeleeHostResets);}}
 static void SwordGuestConfirm(){var wall=new[]{.9f};string hash;using(var pair=SwordStart(wall,out hash)){float x;SwordApproach(pair,hash,1.5f,out x);
  // Guest (origin) cuts the host (x=1.5) by yawing its blade from +z to +x; a wall at z=.9 is touched by the tip at the start.
  Action<double> yaw=delegate(double degrees){double half=degrees*Math.PI/360;pair.Client.RequestEquipment("katana",1,false);pair.Host.RequestEquipment(null,1,false);pair.Host.PublishRawTracking(Body(hash,Rev(pair.Host),x,0,0));pair.Client.PublishRawTracking(Body(hash,Rev(pair.Client),0,0,0,7,rx:.45f,ry:1.2f,rz:0,rqy:(float)Math.Sin(half),rqw:(float)Math.Cos(half)));pair.Tick();};
  for(int i=0;i<4;i++)yaw(0);float hp=SwordHp(pair,pair.Host);for(int d=15;d<=90;d+=15)yaw(d);for(int i=0;i<6;i++)yaw(90);
  Need(SwordHp(pair,pair.Host)<hp,"Guest blade lands on the host");Need(SwordConfirms(pair.Client,pair.Host.OwnActor)==1&&pair.Client.HitConfirmsReceived==1,"Guest attacker receives exactly one hit confirm (damage number) over the wire");Need(SwordConfirms(pair.Host,pair.Host.OwnActor)==0,"Victim gets no attacker confirm");
  Console.WriteLine("guest-confirm: applied="+pair.Host.MeleeHostApplied+" sent="+pair.Host.HitConfirmsSent+" received="+pair.Client.HitConfirmsReceived);}}
}
