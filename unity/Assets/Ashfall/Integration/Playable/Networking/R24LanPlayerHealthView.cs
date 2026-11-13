using System;using Ashfall.Modules.HealthStates;using Ashfall.Integration.Playable.HandsWeapons;using Ashfall.Integration.Playable.Match;
namespace Ashfall.Integration.Playable.Networking
{
    // Captures the real lobby/map epoch, never a client-supplied HealthService.
    // Absolute host deadlines are expressed in the existing Root round clock.
    public sealed class R24LanPlayerHealthView:IR23PlayerHealthView,IDisposable
    {
        readonly R24LanLobby lobby;readonly string session,actor,map;readonly long epoch;readonly double starts;readonly int round;readonly R24PlayableMatchRules rules;
        R24LanHealthState observed;HealthSnapshot presentation;bool disposed;
        public R24LanPlayerHealthView(R24LanLobby lobby)
        {
            if(lobby==null||lobby.SelectedMap==null||lobby.SelectedMap.SourceRules==null||(lobby.State!=R24LanLobbyState.Hosting&&lobby.State!=R24LanLobbyState.Connected))throw new ArgumentException("Actual LAN loaded-map binding required");
            this.lobby=lobby;session=lobby.SessionId;actor=lobby.OwnActor;epoch=lobby.Epoch;map=lobby.MapManifestHash;starts=lobby.SelectedMap.StartsAt;round=lobby.SelectedMap.Round;rules=lobby.SelectedMap.SourceRules;
        }
        bool Current{get{return !disposed&&(lobby.State==R24LanLobbyState.Hosting||lobby.State==R24LanLobbyState.Connected)&&lobby.SessionId==session&&lobby.OwnActor==actor&&lobby.Epoch==epoch&&lobby.MapManifestHash==map&&lobby.SelectedMap!=null&&lobby.SelectedMap.Epoch==epoch&&!lobby.SelectedMap.Cancelled&&lobby.SelectedMap.Round==round&&lobby.SelectedMap.StartsAt==starts;}}
        double Relative(double value){return value<=0?0:Math.Max(0,value-starts);}
        public HealthSnapshot Read()
        {
            R24LanHealthState actual;if(!Current||!lobby.TryReadHealth(actor,out actual)||!lobby.OwnsHealth(actual)){observed=null;presentation=null;return null;}
            if(!Object.ReferenceEquals(actual,observed)){
                observed=actual;presentation=new HealthSnapshot(actual.CanonicalActor,actual.Team,actual.Deaths,actual.Revision,actual.Life,actual.Health,actual.Shield,Relative(actual.DownedUntil),Relative(actual.RespawnAt),Relative(actual.ProtectionUntil),Relative(actual.ReadyUntil),actual.Connected,actual.Charge,actual.LifeGeneration,actual.ConnectionGeneration);
            }
            return presentation;
        }
        public bool ApplyDamage(float amount,bool direct,string cause)
        {
            var before=Read();if(before==null||lobby.State!=R24LanLobbyState.Hosting||before.Life!=LifeState.Alive||Single.IsNaN(amount)||Single.IsInfinity(amount)||amount<=0)return false;
            if(cause!="zone")amount*=rules.Damage;if(amount<=0)return false;
            float shield=direct?0:Math.Min(before.Shield,amount),hp=Math.Min(before.Health,amount-shield);
            var accepted=lobby.ApplyHostHealthLoss(actor,before.Revision,hp,shield,true);var after=Read();return accepted.Accepted&&after!=null&&(after.Health<before.Health||after.Shield<before.Shield);
        }
        public bool ApplyFall()
        {var before=Read();return before!=null&&lobby.State==R24LanLobbyState.Hosting&&lobby.ApplyHostHealthFall(actor,before.Revision).Accepted;}
        public void Dispose(){disposed=true;observed=null;presentation=null;}
    }
}
