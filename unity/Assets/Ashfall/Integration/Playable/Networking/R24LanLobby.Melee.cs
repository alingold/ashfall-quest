using System;using System.Collections.Generic;using System.IO;using Ashfall.Core;using Ashfall.Integration.Playable.HandsWeapons;using Ashfall.Modules.HealthStates;using Ashfall.Modules.ConstructionCandidate;
namespace Ashfall.Integration.Playable.Networking {
 public sealed partial class R24LanLobby {
  const string EquipmentCommand="playable.equipment/1";
  sealed class EquippedWeapon {internal string Id;internal int Hand;internal bool Suppressed,Blocked,Swept;internal double At,SampleAt,AwpCueAt=double.NegativeInfinity;internal long Life=-1;internal R24MeleeRuntime Blade;internal R23ShotPose Model;internal Float3 LastBase,LastTip,LastGrip;}
  readonly Dictionary<string,EquippedWeapon> equipment=new Dictionary<string,EquippedWeapon>(StringComparer.Ordinal);
  // A declaration selects existing custody only. No lease, pose, damage or item is minted.
  public bool RequestEquipment(string id,int hand,bool suppressed,bool blocked=false){Check();if(hand<0||hand>1||id!=null&&!Ashfall.Modules.Networking.Wire.IsText(id,128,false))return false;if(State==R24LanLobbyState.Hosting)return ApplyEquipment(OwnActor,id,hand,suppressed,blocked);if(State!=R24LanLobbyState.Connected)return false;using(var m=new MemoryStream())using(var w=new BinaryWriter(m,Utf8)){w.Write(id!=null);if(id!=null)Write(w,id,128);w.Write((byte)hand);w.Write(suppressed);w.Write(blocked);return QueueGameCommand(EquipmentCommand,m.ToArray());}}
  bool ApplyEquipment(string actor,string id,int hand,bool suppressed,bool blocked){if(PlatformProtected(actor)){id=null;suppressed=true;blocked=true;}CombatActor owner;if(!InventoryLive||hand<0||hand>1||!combat.TryGetValue(actor,out owner)||id!=null&&(!owner.Weapons.ContainsKey(id)||owner.Weapons[id].Profile=="smaw"&&owner.Weapons[id].Ammo==0&&owner.Weapons[id].RocketReloads==0))return false;EquippedWeapon prior;if(!equipment.TryGetValue(actor,out prior)||prior.Id!=id||prior.Hand!=hand||prior.Suppressed!=suppressed||prior.Blocked!=blocked){if(prior!=null&&prior.Blade!=null)prior.Blade.Retire();prior=new EquippedWeapon{Id=id,Hand=hand,Suppressed=suppressed,Blocked=blocked,AwpCueAt=prior==null?double.NegativeInfinity:prior.AwpCueAt};equipment[actor]=prior;CombatWeapon weapon;if(id!=null&&owner.Weapons.TryGetValue(id,out weapon)){var profile=R24MeleeProfiles.ForId(weapon.Profile);if(profile!=null)prior.Blade=new R24MeleeRuntime(id,profile);}}prior.At=clock.Now;return true;}
  void ReadEquipment(string actor,byte[] bytes){try{using(var m=new MemoryStream(bytes,false))using(var r=new BinaryReader(m,Utf8)){string id=r.ReadBoolean()?Read(r,128):null;int hand=r.ReadByte();bool suppressed=r.ReadBoolean(),blocked=r.ReadBoolean();if(m.Position==m.Length)ApplyEquipment(actor,id,hand,suppressed,blocked);}}catch(InvalidDataException){}catch(IOException){}catch(ArgumentException){}}
  bool ControlsBlocked(string actor){EquippedWeapon e;return PlatformProtected(actor)||equipment.TryGetValue(actor,out e)&&clock.Now-e.At<=.5&&e.Blocked;}
  bool HandOccupied(string actor,int hand){EquippedWeapon e;CombatActor c;return equipment.TryGetValue(actor,out e)&&!e.Suppressed&&e.Id!=null&&clock.Now-e.At<=.5&&combat.TryGetValue(actor,out c)&&c.Weapons.ContainsKey(e.Id)&&(c.Weapons[e.Id].Profile!="smaw"||c.Weapons[e.Id].Ammo>0||c.Weapons[e.Id].RocketReloads>0)&&(e.Hand==hand||c.Weapons[e.Id].Hands!=null);}
  void ClearEquipment(){ClearMeleeExpansion();foreach(var e in equipment.Values)if(e.Blade!=null)e.Blade.Retire();equipment.Clear();}
  static R23ShotPose Basis(R24LanTransform t){return new R23ShotPose(t.Position,Rotate(t.Rotation,new Float3(1,0,0)),Rotate(t.Rotation,new Float3(0,1,0)),Rotate(t.Rotation,new Float3(0,0,1)));}
  static R23ShotPose GripBasis(R24LanTransform t){return new R23ShotPose(t.Position,Rotate(t.Rotation,new Float3(1,0,0)),Rotate(t.Rotation,new Float3(0,0,1)),Rotate(t.Rotation,new Float3(0,-1,0)));}
  bool MapBladeClear(Float3 a,Float3 b){float distance=(float)Math.Sqrt(Distance2(a,b));if(distance<.00001f)return true;float hit=ballisticTrace==null?0:ballisticTrace(a,Normalize(Sub(b,a)),distance);return !Single.IsNaN(hit)&&hit>=distance-.005f;}
  bool MeleeVisible(Float3 from,Float3 to,string panel){float length=(float)Math.Sqrt(Distance2(from,to));if(length<.00001f)return true;if(ballisticTrace==null)return false;var d=Normalize(Sub(to,from));float block=ballisticTrace(from,d,length);if(Single.IsNaN(block)||block<length-.005f)return false;float distance;string first=NearestSharedPanel(from,d,length,out distance);return first==null||first==panel;}
  // ---------- SWORD-HITREG (r15): host blade sampling ----------
  // The host replays every held blade from ~24 Hz raw tracking arrivals. Four rules made real swings miss:
  //  1. the whole blade was suspended whenever hand->base or hand->tip crossed map geometry, so a 1.1 m sword that touched the wall
  //     behind a target, the floor or a pillar lost its sweep history and the reseed swallowed the contact. The Godot source
  //     (melee_sword.gd _find_contacts) never tests the blade against the map: only head->hand and hand->target occlusion matter.
  //  2. two tracking samples delivered in one host tick (relay/Wi-Fi bunching) have arrival dt 0 and suspended the blade again.
  //  3. a fast swing moves the tip more than 1 m between two 24 Hz samples; R24MeleeRuntime treats that as a tracking jump (the
  //     source rule was written for 72-90 Hz local sampling). Such samples are now replayed as several interpolated sub-frames.
  //  4. targets were tested only at their newest pose, while the attacker swings at an avatar shown ~0.1 s in the past.
  // Walls still block: a contact needs head->hand clear and hand->body-sphere-centre clear on the host map (MeleeVisible).
  const float MeleeSplitTravel=.45f,MeleeNominalDelta=.04f,MeleeBunchDelta=.02f;const int MeleeSplitLimit=8;const double MeleeRewind=.12;
  static readonly string[] NoMelee=new string[0];
  public int MeleeHostSamples{get;private set;}public int MeleeHostResets{get;private set;}public int MeleeHostBunched{get;private set;}public int MeleeHostOccluded{get;private set;}public int MeleeHostSplit{get;private set;}public int MeleeHostEntries{get;private set;}public int MeleeHostQueued{get;private set;}public int MeleeHostApplied{get;private set;}public int MeleeHostRewound{get;private set;}public string MeleeHostLastReset{get;private set;}
  void MeleeReset(EquippedWeapon e,string reason){if(e.Swept||e.Blade.HistoryValid){MeleeHostResets++;MeleeHostLastReset=reason;}e.Blade.SuspendControls();e.Swept=false;}
  bool MeleeBody(Float3[] v,int[] t,Float3 origin,Float3 head,out Float3 sphere,out float radius){for(int i=0;i<3;i++){float drop=i==0?0:i==1?.45f:.92f;radius=i==0?.17f:i==1?.29f:.27f;sphere=new Float3(head.X,head.Y-drop,head.Z);if(R24LanMeleeGeometry.Sphere(v,t,sphere,radius)&&MeleeVisible(origin,sphere,null))return true;}sphere=new Float3();radius=0;return false;}
  string[] MeleeOverlaps(string actor,R23ShotPose model,Float3 a,Float3 b,Float3 c,Float3 d,float margin,Dictionary<string,Float3> bladeContacts=null){List<KeyValuePair<string,double>> found=null;Float3[] v=null;int[] triangles=null;int hull=0; // 0 not built, 1 built, 2 degenerate
   // Broadphase: every hull point is a base on a-c plus at most one blade length, so nothing farther than reach can touch it. The hull is built only when a body or panel is that close.
   var mid=R24KnifeMath.Scale(R24KnifeMath.Add(a,c),.5f);double reach=Math.Sqrt(Distance2(a,c))*.5+Math.Max(Math.Sqrt(Distance2(a,b)),Math.Sqrt(Distance2(c,d)))+margin+.06,body=(reach+.8)*(reach+.8);
   foreach(var member in sessions.CaptureMembers()){if(member.ActorId==actor)continue;var health=hostHealth.Read(member.CanonicalActorId);R24LanRawTrackingSample raw;if(health==null||!health.Connected||health.Life!=LifeState.Alive||!Fresh(member.ActorId,clock.Now,out raw))continue;var head=raw.Pose.Head.Position;var past=head;bool rewind=false;R24LanRawTrackingHistory history;R24LanRawTrackingSample old;
    // Lag compensation: also accept the body where this attacker still saw it a moment ago (same life and placement only).
    if(roundRules!=null&&rawTracking.TryGetValue(member.ActorId,out history)&&history.TryNearest(clock.Now-MeleeRewind,out old)&&old.Pose.SpawnRevision==raw.Pose.SpawnRevision&&clock.Now-old.Time<=.3&&Distance2(old.Pose.Head.Position,head)>.0025){past=old.Pose.Head.Position;rewind=true;}
    bool nearNow=Distance2(mid,new Float3(head.X,head.Y-.45f,head.Z))<=body,nearPast=rewind&&Distance2(mid,new Float3(past.X,past.Y-.45f,past.Z))<=body;if(!nearNow&&!nearPast)continue;
    if(hull==0)hull=R24KnifeHull.TryBuild(R24KnifeMath.HullPoints(a,b,c,d,margin+.001f),out v,out triangles)?1:2;if(hull==2)return NoMelee;
    Float3 sphere=new Float3();float radius=0;bool hit=nearNow&&MeleeBody(v,triangles,model.Origin,head,out sphere,out radius);if(!hit&&nearPast&&MeleeBody(v,triangles,model.Origin,past,out sphere,out radius)){hit=true;MeleeHostRewound++;}
    if(hit){string id="actor/"+member.ActorId;if(found==null)found=new List<KeyValuePair<string,double>>();found.Add(new KeyValuePair<string,double>(id,Distance2(model.Origin,head)));if(bladeContacts!=null)bladeContacts.Add(id,R24LanMeleeGeometry.SphereContact(v,triangles,sphere,radius,model.Origin));}}
   foreach(var panel in sharedPanels.Values){bool near=false;foreach(var box in CandidateGeometry.Boxes(panel.Shape)){var half=CandidateGeometry.HalfSize(box);double r=reach+Math.Sqrt((double)half.X*half.X+(double)half.Y*half.Y+(double)half.Z*half.Z);if(Distance2(mid,box.Center)<=r*r){near=true;break;}}if(!near)continue;
    if(hull==0)hull=R24KnifeHull.TryBuild(R24KnifeMath.HullPoints(a,b,c,d,margin+.001f),out v,out triangles)?1:2;if(hull==2)return NoMelee;
    if(R24LanMeleeGeometry.Box(v,triangles,panel.Shape)&&MeleeVisible(model.Origin,panel.Shape.Center,panel.Id)){if(found==null)found=new List<KeyValuePair<string,double>>();found.Add(new KeyValuePair<string,double>("panel/"+panel.Id,Distance2(model.Origin,panel.Shape.Center)));}}
   if(found==null)return NoMelee;
   found.Sort(delegate(KeyValuePair<string,double> x,KeyValuePair<string,double> y){int order=x.Value.CompareTo(y.Value);return order!=0?order:String.CompareOrdinal(x.Key,y.Key);});var ids=new string[found.Count];for(int i=0;i<ids.Length;i++)ids[i]=found[i].Key;return ids;
  }
  void SampleMeleePose(string actor,R24LanRawTrackingSample sample){SampleFists(actor,sample);EquippedWeapon e;if(!equipment.TryGetValue(actor,out e)||e.Blade==null)return;var member=sessions.Get(actor);var health=member==null?null:hostHealth.Read(member.CanonicalActorId);CombatActor owner;bool usable=InventoryLive&&!PlatformProtected(actor)&&!e.Suppressed&&!e.Blocked&&clock.Now-e.At<=.5&&health!=null&&health.Connected&&health.Life==LifeState.Alive&&combat.TryGetValue(actor,out owner)&&owner.Weapons.ContainsKey(e.Id)&&!sample.Pose.Paddles&&(sample.Pose.Flags&(e.Hand==0?2:4))!=0;
   float dt=(float)(sample.Time-e.SampleAt);e.SampleAt=sample.Time;MeleeHostSamples++;
   if(!usable||dt<0||dt>.15f||e.Life!=health.LifeGeneration){MeleeReset(e,!usable?"unusable":dt<0||dt>.15f?"sample-gap":"life");e.Life=health==null?-1:health.LifeGeneration;if(!usable)return;dt=MeleeNominalDelta;}
   // Arrival spacing is not motion time: samples bunched into one host tick keep the stroke and use the publish interval.
   else if(dt<MeleeBunchDelta){dt=MeleeNominalDelta;MeleeHostBunched++;}
   if(rawWorldClear==null){MeleeReset(e,"host-world");return;}
   var hand=e.Hand==0?sample.Pose.Left:sample.Pose.Right;var grip=GripBasis(hand);e.Model=grip;var root=Basis(sample.Pose.Root);var a=e.Model.Point(e.Blade.Profile.Base);var b=e.Model.Point(e.Blade.Profile.Tip);
   // A hand behind map geometry (head->hand blocked) cannot strike or guard, but it no longer erases the stroke: history keeps advancing and contact state is frozen until the hand is clear again.
   bool occluded=!rawWorldClear(sample.Pose.Head.Position,hand.Position);if(occluded)MeleeHostOccluded++;
   var other=e.Hand==0?sample.Pose.Right:sample.Pose.Left;bool tracked=(sample.Pose.Flags&(e.Hand==0?4:2))!=0;FoodPreparation food;bool reserved=foodPreparations.TryGetValue(actor,out food)&&food.Hand!=e.Hand||GrenadeHandOccupied(actor,1-e.Hand);e.Blade.UpdateSupport(!occluded,tracked,reserved,(e.Hand==0?sample.Pose.RightGrip:sample.Pose.LeftGrip)>.7f,(float)Math.Sqrt(Distance2(other.Position,e.Model.Point(R24MeleeRuntime.SupportPoint))));
   if(!occluded)SampleBladeClash(actor,e,a,b,sample);
   var localA=R24KnifeMath.Local(root,a);var localB=R24KnifeMath.Local(root,b);var localGrip=R24KnifeMath.Local(root,hand.Position);int parts=1;
   if(e.Swept&&e.Blade.HistoryValid){float travel=Math.Max(R24KnifeMath.Length(R24KnifeMath.Sub(localA,e.LastBase)),R24KnifeMath.Length(R24KnifeMath.Sub(localB,e.LastTip)));if(travel>MeleeSplitTravel&&travel<=MeleeSplitTravel*MeleeSplitLimit){parts=(int)Math.Ceiling(travel/MeleeSplitTravel);MeleeHostSplit++;}}
   for(int part=1;part<=parts;part++){Float3 partA=a,partB=b,partGrip=hand.Position;
    if(part<parts){float t=(float)part/parts;var before=R24KnifeMath.Sub(e.LastTip,e.LastBase);var after=R24KnifeMath.Sub(localB,localA);var lower=R24KnifeMath.Lerp(e.LastBase,localA,t);var along=R24KnifeMath.Scale(R24KnifeMath.Slerp(R24KnifeMath.Unit(before),R24KnifeMath.Unit(after),t),R24KnifeMath.Length(before)+(R24KnifeMath.Length(after)-R24KnifeMath.Length(before))*t);partA=root.Point(lower);partB=root.Point(R24KnifeMath.Add(lower,along));partGrip=root.Point(R24KnifeMath.Lerp(e.LastGrip,localGrip,t));}
    if(!MeleeFrame(actor,e,new R23ShotPose(partGrip,grip.Right,grip.Up,grip.Forward),root,partA,partB,dt/parts,occluded))break;}
   e.LastBase=localA;e.LastTip=localB;e.LastGrip=localGrip;e.Swept=true;
  }
  // One blade frame (a whole tracking sample or one interpolated part of it). False when the runtime dropped its history.
  bool MeleeFrame(string actor,EquippedWeapon e,R23ShotPose model,R23ShotPose root,Float3 a,Float3 b,float dt,bool occluded){
   if(occluded){if(e.Blade.HistoryValid)e.Blade.BeginFrame(dt,true,root,a,b,model.Origin,NoMelee);return true;} // unseeded blades wait for a clear hand, so a seed never records an empty contact set
   var sweep=e.Blade.BeginFrame(dt,true,root,a,b,model.Origin,MeleeOverlaps(actor,model,a,b,a,b,0));if(sweep==null)return e.Blade.HistoryValid;
   var bladeContacts=new Dictionary<string,Float3>(StringComparer.Ordinal);
   for(int step=0;step<sweep.Steps;step++){bladeContacts.Clear();Float3 oldA,oldB,nextA,nextB;sweep.ReadStep(step,out oldA,out oldB,out nextA,out nextB);var swept=MeleeOverlaps(actor,model,oldA,oldB,nextA,nextB,0,bladeContacts);var current=MeleeOverlaps(actor,model,nextA,nextB,nextA,nextB,0);var retained=MeleeOverlaps(actor,model,nextA,nextB,nextA,nextB,R24MeleeRuntime.ExitHysteresis);foreach(var entry in e.Blade.SampleStep(sweep,step,swept,current,retained)){MeleeHostEntries++;e.Blade.ApplyContact(entry,delegate(string target,float damage){if(MeleeBlocked(actor))return false;if(target.StartsWith("panel/",StringComparison.Ordinal)){DamageSharedPanel(target.Substring(6),damage);return true;}if(!QueueMeleeDamage(actor,target.Substring(6),damage,bladeContacts[target],e.Hand,"blade"))return false;MeleeHostQueued++;return true;});}}
   return true;
  }
  float GuardDistance(string shooter,Float3 from,Float3 direction,float range){foreach(var pair in equipment){if(pair.Key==shooter)continue;var e=pair.Value;var member=sessions.Get(pair.Key);var h=member==null?null:hostHealth.Read(member.CanonicalActorId);CombatActor custody;R24LanRawTrackingSample raw;if(e.Id==null||e.Blade==null||h==null||h.Life!=LifeState.Alive||!combat.TryGetValue(pair.Key,out custody)||!custody.Weapons.ContainsKey(e.Id)||e.Blade==null||e.Suppressed||e.Blocked||clock.Now-e.At>.5||!Fresh(pair.Key,clock.Now,out raw)||clock.Now-e.SampleAt>.15)continue;Ashfall.Modules.Combat.BulletGuard guard;if(e.Blade.TryReadBulletGuard(e.Model,true,out guard)){float fraction;if(guard.Intersects(from,R24KnifeMath.Add(from,R24KnifeMath.Scale(direction,range)),out fraction))range=Math.Min(range,range*fraction);}}return range;}
    // Transient mechanical cues are presentation only. The host supplies position
    // and accepts them only from the tracked holder of a current AWP instance.
    internal struct R26RemoteAwpCue
    {
        internal string Actor;internal byte Cue;internal Float3 At;internal long Epoch;internal double Time;
    }
    readonly Queue<R26RemoteAwpCue> remoteAwpCues=new Queue<R26RemoteAwpCue>();
    const string AwpCueFeature="playable.awp.cue/1";
    long awpCueSequence,receivedAwpCueSequence;
    internal bool RequestAwpCue(byte cue)
    {
        Check();if(cue>3)return false;
        if(State==R24LanLobbyState.Hosting)return ApplyAwpCue(OwnActor,cue);
        return State==R24LanLobbyState.Connected&&QueueGameCommand(AwpCueFeature,new[]{cue});
    }
    bool ReadAwpCueCommand(Peer peer,Ashfall.Modules.Networking.Packet packet)
    {
        if(packet.FeatureId!=AwpCueFeature)return false;
        if(packet.BaseSequence!=-1||packet.Sequence<=peer.LastGameCommand)return true;
        peer.LastGameCommand=packet.Sequence;
        if(packet.Payload.Length==1)ApplyAwpCue(peer.Actor,packet.Payload[0]);
        return true;
    }
    bool ApplyAwpCue(string actor,byte cue)
    {
        EquippedWeapon held;CombatActor owner;CombatWeapon weapon;R24LanRawTrackingSample raw;
        double now=clock.Now;
        if(cue>3||!equipment.TryGetValue(actor,out held)||held.Id==null||held.Suppressed||held.Blocked||
            now-held.At>.5||now-held.AwpCueAt<.08||!ExplosivePose(actor,held.Hand,out raw,out owner)||
            !owner.Weapons.TryGetValue(held.Id,out weapon)||weapon.Profile!="awp")return false;
        held.AwpCueAt=now;
        Float3 at=held.Hand==0?raw.Pose.Left.Position:raw.Pose.Right.Position;
        EnqueueAwpCue(new R26RemoteAwpCue{Actor=actor,Cue=cue,At=at,Epoch=Epoch,Time=now});
        using(var s=new MemoryStream())using(var w=new BinaryWriter(s,Utf8)){
            Write(w,actor,64);w.Write(cue);w.Write(at.X);w.Write(at.Y);w.Write(at.Z);w.Write(now);
            var frame=Packet(Ashfall.Modules.Networking.PacketKind.Snapshot,AwpCueFeature,OwnActor,s.ToArray(),checked(++awpCueSequence));
            foreach(var peer in peers)if(RemotePeerReady(peer.Value)&&peer.Value.Actor!=actor)Send(peer.Key,frame);
        }
        return true;
    }
    void ReadAwpCueSnapshot(Ashfall.Modules.Networking.Packet packet)
    {
        if(packet.BaseSequence!=-1||packet.Sequence<=receivedAwpCueSequence)return;
        try{using(var s=new MemoryStream(packet.Payload,false))using(var r=new BinaryReader(s,Utf8)){
            string actor=Read(r,64);byte cue=r.ReadByte();var at=new Float3(r.ReadSingle(),r.ReadSingle(),r.ReadSingle());double stamp=r.ReadDouble();
            if(s.Position!=s.Length||cue>3||!at.IsFinite||Double.IsNaN(stamp)||Double.IsInfinity(stamp)||
                stamp>EstimatedHostTime+.15||EstimatedHostTime-stamp>.75||!HasMember(actor))return;
            receivedAwpCueSequence=packet.Sequence;
            EnqueueAwpCue(new R26RemoteAwpCue{Actor=actor,Cue=cue,At=at,Epoch=Epoch,Time=stamp});
        }}catch(InvalidDataException){}catch(IOException){}catch(ArgumentException){}
    }
    void EnqueueAwpCue(R26RemoteAwpCue cue)
    {if(cue.Actor==OwnActor)return;if(remoteAwpCues.Count>=32)remoteAwpCues.Dequeue();remoteAwpCues.Enqueue(cue);}
    internal bool TryDequeueAwpCue(out R26RemoteAwpCue cue)
    {
        Check();while(remoteAwpCues.Count>0){cue=remoteAwpCues.Dequeue();if(cue.Epoch==Epoch&&EstimatedHostTime-cue.Time<=.75)return true;}
        cue=default(R26RemoteAwpCue);return false;
    }
 }
}
