using System;using System.Collections.Generic;using Ashfall.Core;using Ashfall.Modules.Networking;
namespace Ashfall.Integration.Playable.Networking
{
    public sealed partial class R24LanLobby
    {
        const string RawTrackingFeature="playable.tracking/2";
        readonly Dictionary<string,R24LanRawTrackingHistory> inventoryTracking=new Dictionary<string,R24LanRawTrackingHistory>(StringComparer.Ordinal);
        readonly Dictionary<string,R24LanRawTrackingHistory> rawTracking=new Dictionary<string,R24LanRawTrackingHistory>(StringComparer.Ordinal);
        Func<Float3,Float3,bool> rawWorldClear;byte[] pendingRawTracking;long rawSequence;double nextRawTracking;
        public int RejectedRawTracking{get;private set;}public string LastRawTrackingRejection{get;private set;}
        // Host composition only. Query reads the actual loaded world; clients
        // never supply a collision oracle or a gameplay-authorization flag.
        public void BindRawTrackingWorldQuery(Func<Float3,Float3,bool> query)
        {Check();if(State!=R24LanLobbyState.Hosting||query==null||rawWorldClear!=null)throw new InvalidOperationException("One host world query required");rawWorldClear=delegate(Float3 a,Float3 b){return query(a,b)&&ConstructionRayClear(a,b);};}
        bool RawTrackingMapReady(string actor,string hash)
        {
            if(SelectedMap==null||!SelectedMap.Active||SelectedMap.Cancelled||SelectedMap.Results||hash!=SelectedMap.ManifestHash)return false;
            foreach(var m in members)if(m.Actor==actor)return m.WorldLoaded;return false;
        }
        public bool PublishRawTracking(R24LanRawTracking pose)
        {
            Check();if((State!=R24LanLobbyState.Hosting&&State!=R24LanLobbyState.Connected)||pose==null||!pose.BodyValid||!RawTrackingMapReady(OwnActor,pose.MapHash))return false;
            R24LanSpawn current;if(!TryReadSpawn(OwnActor,out current)||pose.SpawnRevision!=current.Revision)return false;
            long sequence=checked(++rawSequence);
            if(State==R24LanLobbyState.Hosting)return StoreRawTracking(OwnActor,sequence,pose);
            pendingRawTracking=Packet(PacketKind.Delta,RawTrackingFeature,OwnActor,pose.Encode(),sequence);return true;
        }
        bool StoreRawTracking(string actor,long sequence,R24LanRawTracking pose)
        {
            if(!RawTrackingMapReady(actor,pose.MapHash)){RejectedRawTracking++;LastRawTrackingRejection="map-readiness";return false;}
            if(!AdmitRawArrival(actor)){RejectedRawTracking++;LastRawTrackingRejection="rate";return false;}
            R24LanSpawn current;var member=ReadAuthorityMember(actor);if(!TryReadSpawn(actor,out current)||pose.SpawnRevision!=current.Revision||member==null||!member.Loaded||!member.HasPosition||member.SpawnRevision!=pose.SpawnRevision){RejectedRawTracking++;LastRawTrackingRejection="spawn-revision";return false;}
            // A host-owned pod ride carries this actor; its path, not client poses, is authoritative.
            if(PodCarried(actor)){RejectedRawTracking++;LastRawTrackingRejection="carried";return false;}
            R24LanRawTrackingHistory history;if(!rawTracking.TryGetValue(actor,out history)){history=new R24LanRawTrackingHistory();rawTracking.Add(actor,history);}
            var rules=SelectedMap.Rules;bool match=rules!=null&&RoundCountdown<=0;float multiplier=rules==null?1:Math.Max(1,Math.Max(rules.Fling,Math.Max(rules.Flight,Math.Max(rules.Walk,rules.Run))));
            // Every new placement revision (spawn, respawn, correction, pod start/exit)
            // clears history; the first sample must start at the host's committed root.
            if(match&&history.Latest==null&&!Anchored(member.Position,pose.Root.Position)){RejectedRawTracking++;LastRawTrackingRejection="spawn-anchor";RecoverRejectedRawTracking(actor,history,"spawn-anchor");return false;}
            if(!VehicleTrackAllowed(actor,pose.Root.Position)){RejectedRawTracking++;LastRawTrackingRejection="vehicle-ceiling";return false;}
            multiplier=PullMotionMultiplier(actor,multiplier);
            if(history.Accept(sequence,clock.Now,pose,match,multiplier,rawWorldClear)){rawPoseFaults.Remove(actor);StoreInventoryTracking(actor,sequence,pose,member.Position,multiplier);SampleCombatPose(actor,history.Latest);if(roundRules!=null)SampleMeleePose(actor,history.Latest);else{R24LanRawTrackingSample checkedMelee;if(TryReadInventoryTracking(actor,clock.Now,out checkedMelee)&&checkedMelee.Sequence==sequence)SampleMeleePose(actor,checkedMelee);}if(roundRules!=null)SampleFoodPose(actor,history.Latest);else{R24LanRawTrackingSample checkedFood;if(TryReadInventoryTracking(actor,clock.Now,out checkedFood)&&checkedFood.Sequence==sequence)SampleFoodPose(actor,checkedFood);}return true;}
            RejectedRawTracking++;LastRawTrackingRejection=history.Rejection;RecoverRejectedRawTracking(actor,history,history.Rejection);return false;
        }
        void ReadRawTracking(Peer peer,Packet packet)
        {
            R24LanRawTracking pose;if(packet.BaseSequence!=-1||!R24LanRawTracking.TryReadEnvelope(packet.Payload,out pose)){RejectedRawTracking++;LastRawTrackingRejection="payload";return;}
            StoreRawTracking(peer.Actor,packet.Sequence,pose);
        }
        void TickRawTracking()
        {
            if(State!=R24LanLobbyState.Connected||pendingRawTracking==null||clock.Now<nextRawTracking)return;
            nextRawTracking=clock.Now+.04;if(SendPresence(serverPeer,pendingRawTracking))pendingRawTracking=null;
        }
        // Host reference history only, never an admission capability. Session
        // spawn/life/owned-item validation must precede any future combat use.
        public bool TryReadRawTracking(string actor,double at,out R24LanRawTrackingSample sample)
        {
            Check();sample=null;R24LanRawTrackingHistory history;R24LanSpawn current;
            return State==R24LanLobbyState.Hosting&&actor!=null&&rawTracking.TryGetValue(actor,out history)&&history.Latest!=null&&TryReadSpawn(actor,out current)&&history.Latest.Pose.SpawnRevision==current.Revision&&clock.Now-history.Latest.Time<=1&&RawTrackingMapReady(actor,history.Latest.Pose.MapHash)&&history.TryNearest(at,out sample);
        }
        void StoreInventoryTracking(string actor,long sequence,R24LanRawTracking pose,Float3 placement,float multiplier){if(roundRules!=null)return;R24LanRawTrackingHistory history;if(!inventoryTracking.TryGetValue(actor,out history)){history=new R24LanRawTrackingHistory();inventoryTracking[actor]=history;}if(history.Latest==null&&!Anchored(placement,pose.Root.Position))return;history.Accept(sequence,clock.Now,pose,true,multiplier,rawWorldClear);}
        bool TryReadInventoryTracking(string actor,double at,out R24LanRawTrackingSample sample){sample=null;R24LanRawTrackingHistory history;R24LanSpawn spawn;return actor!=null&&inventoryTracking.TryGetValue(actor,out history)&&history.Latest!=null&&clock.Now-history.Latest.Time<=1&&TryReadSpawn(actor,out spawn)&&spawn.Revision==history.Latest.Pose.SpawnRevision&&history.TryNearest(at,out sample);}
        void ClearRawTracking(){ClearVehicles();ClearWeb();inventoryTracking.Clear();rawArrivals.Clear();rawPoseFaults.Clear();rawTracking.Clear();pendingRawTracking=null;nextRawTracking=0;}
        public const float SpawnAnchorHorizontal=3,SpawnAnchorVertical=3;
        static bool Anchored(Float3 placement,Float3 root)
        {if(!placement.IsFinite||!root.IsFinite)return false;double x=(double)root.X-placement.X,z=(double)root.Z-placement.Z,y=(double)root.Y-placement.Y;return x*x+z*z<=SpawnAnchorHorizontal*SpawnAnchorHorizontal&&Math.Abs(y)<=SpawnAnchorVertical;}
    }
}
