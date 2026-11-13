using System;
namespace Ashfall.Integration.Playable.JumpPads
{
    // healing_stations_r18: one shared ready-at timestamp per source station.
    public sealed class R23HealthStationRecharge
    {
        public const double Cooldown=15;
        readonly double[] readyAt;
        public R23HealthStationRecharge(int count){if(count<0)throw new ArgumentOutOfRangeException("count");readyAt=new double[count];}
        public double Remaining(int index,double now){return Valid(index,now)?Math.Max(0,Math.Min(Cooldown,readyAt[index]-now)):0;}
        public bool TryRestore(int index,double now,Func<bool> actualRestore)
        {
            if(!Valid(index,now)||actualRestore==null||now<readyAt[index]||!actualRestore())return false;
            readyAt[index]=now+Cooldown;return true;
        }
        bool Valid(int index,double now){return index>=0&&index<readyAt.Length&&!Double.IsNaN(now)&&!Double.IsInfinity(now)&&now>=0;}
    }
}
