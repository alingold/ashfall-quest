using System;
using System.Collections.Generic;
using Ashfall.Core;
namespace Ashfall.Integration.Playable.Expansion
{
    // One tracker per authenticated hand; root-local motion excludes walking damage.
    public sealed class R25FistContact
    {
        readonly HashSet<string> inside=new HashSet<string>(StringComparer.Ordinal);
        readonly Dictionary<string,double> cooldown=new Dictionary<string,double>(StringComparer.Ordinal);
        Float3 previous; bool seeded; double at;
        public float Speed { get; private set; }
        public static float Damage(float speed) { return 8+22*Math.Max(0,Math.Min(1,(speed-1.6f)/2.4f)); }
        public string[] Sample(double now,Float3 local,bool usable,string[] overlaps)
        {
            double dt=now-at;at=now;
            Speed=seeded&&dt>0&&dt<=.15?Length(Sub(local,previous))/(float)dt:0;
            if(Length(Sub(local,previous))>1)Speed=0;previous=local;
            var hits=new List<string>();
            foreach(string key in new List<string>(cooldown.Keys))if(cooldown[key]<=now)cooldown.Remove(key);
            foreach(string id in overlaps)if(usable&&seeded&&Speed>1.6f&&!inside.Contains(id)&&!cooldown.ContainsKey(id)&&cooldown.Count<128){hits.Add(id);cooldown[id]=now+.35;}
            inside.Clear();foreach(string id in overlaps)if(inside.Count<128)inside.Add(id);
            seeded=usable;return hits.ToArray();
        }
        public static Float3 Sub(Float3 a,Float3 b){return new Float3(a.X-b.X,a.Y-b.Y,a.Z-b.Z);}
        public static float Length(Float3 a){return (float)Math.Sqrt(a.X*a.X+a.Y*a.Y+a.Z*a.Z);}
    }
    public sealed class R25ParryWindow
    {
        sealed class Motion { internal double At; internal Float3 Velocity; }
        readonly Queue<Motion> recent=new Queue<Motion>();
        public double Until {get;private set;}
        public void Observe(double now,Float3 velocity){while(recent.Count>0&&now-recent.Peek().At>.15)recent.Dequeue();if(recent.Count>=8)recent.Dequeue();recent.Enqueue(new Motion{At=now,Velocity=velocity});}
        public bool Timed(double now,Float3 toward){foreach(var sample in recent)if(now-sample.At<=.15&&sample.Velocity.X*toward.X+sample.Velocity.Y*toward.Y+sample.Velocity.Z*toward.Z>=2)return true;return false;}
        public void InheritUntil(double until){Until=Math.Max(Until,until);}
        public void Clash(double now,bool timed){Until=Math.Max(Until,now+(timed?.9:.4));}
        public bool Blocked(double now){return now<Until;}
    }
}
