using System;using System.Collections.Generic;using Ashfall.Core;
namespace Ashfall.Integration.Playable.Networking
{
    public sealed partial class R24LanLobby
    {
        readonly Dictionary<string,double> rawPoseFaults=new Dictionary<string,double>(StringComparer.Ordinal);
        sealed class RawArrival{internal double Started;internal int Count;}
        readonly Dictionary<string,RawArrival> rawArrivals=new Dictionary<string,RawArrival>(StringComparer.Ordinal);
        bool AdmitRawArrival(string actor)
        {
            RawArrival rate;if(!rawArrivals.TryGetValue(actor,out rate)){rate=new RawArrival{Started=clock.Now};rawArrivals.Add(actor,rate);}
            if(clock.Now-rate.Started>1){rate.Started=clock.Now;rate.Count=0;}rate.Count=Math.Min(46,rate.Count+1);return rate.Count<=45;
        }
        public int CorrectedRawTracking{get;private set;}public string LastRawRecoveryFailure{get;private set;}
        void ForgetRawTracking(string actor,bool departed=false){inventoryTracking.Remove(actor);rawTracking.Remove(actor);rawPoseFaults.Remove(actor);if(departed)rawArrivals.Remove(actor);}
        void RecoverRejectedRawTracking(string actor,R24LanRawTrackingHistory history,string reason)
        {
            if(State!=R24LanLobbyState.Hosting||SelectedMap==null||SelectedMap.Rules==null||RoundCountdown>0||history==null||(reason!="body"&&reason!="speed"&&reason!="world-sweep"&&reason!="spawn-anchor"))return;
            R24LanSpawn current;if(!TryReadSpawn(actor,out current))return;
            // Anchor faults re-publish the committed placement; motion faults return to the last accepted root.
            Float3 target;if(reason=="spawn-anchor")target=current.Feet;else{if(history.Latest==null||history.Latest.Pose.SpawnRevision!=current.Revision)return;target=history.Latest.Pose.Root.Position;}
            double first;if(!rawPoseFaults.TryGetValue(actor,out first)){rawPoseFaults.Add(actor,clock.Now);return;}
            if(clock.Now-first<.25)return;
            long id=checked(++tick);var context=new CommandContext(SessionId,OwnActor,"pose-recovery/"+id.ToString(System.Globalization.CultureInfo.InvariantCulture),id,Epoch,true);
            var result=sessions.RelocateRoot(context,actor,current.Revision,target);
            if(result.Accepted){EjectVehicleRider(actor);RemoveWeb(actor);rawPoseFaults.Remove(actor);CorrectedRawTracking++;LastRawRecoveryFailure=null;}
            else LastRawRecoveryFailure=result.Reason;
        }
    }
}
