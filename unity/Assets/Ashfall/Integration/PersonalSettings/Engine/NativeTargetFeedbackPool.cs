using System;
using System.Globalization;
namespace Ashfall.Integration.PersonalSettings.Engine
{
    // Engine-free event lifetime; no authority or resource approval is minted here.
    public sealed class NativeTargetFeedbackPool : IDisposable
    {
        public const int Limit=48;
        public const float Lifetime=.85f;
        public struct Event
        {
            public string Target,Text;
            public bool ShieldHit,Active;
            public float Remaining;
            public int Pattern;
            public float Rise { get { return (Lifetime-Remaining)*.14f; } }
            public float Alpha { get { return Math.Min(1,Remaining/.22f); } }
        }
        readonly Event[] entries;
        readonly long[] order;
        long sequence;
        int pattern;
        bool disposed;
        public NativeTargetFeedbackPool(int capacity)
        {
            if(capacity<1||capacity>Limit)throw new ArgumentOutOfRangeException("capacity");
            entries=new Event[capacity];order=new long[capacity];
        }
        public int Capacity { get { return entries.Length; } }
        public Event Read(int index) { return entries[index]; }
        public int Emit(string target,float amount,bool shieldHit) { return Emit(target,amount,shieldHit,-1); }
        public int Emit(string target,float amount,bool shieldHit,int preferredFreeIndex)
        {
            if(disposed||String.IsNullOrEmpty(target)||float.IsNaN(amount)||float.IsInfinity(amount)||amount<=0)return -1;
            int index=-1;long oldest=Int64.MaxValue;
            for(int i=0;i<entries.Length;i++) { if(!entries[i].Active){index=i;break;}if(order[i]<oldest){oldest=order[i];index=i;} }
            if(preferredFreeIndex>=0&&preferredFreeIndex<entries.Length&&!entries[preferredFreeIndex].Active)index=preferredFreeIndex;
            // Order is bounded by active entries; avoid a rollover alias even in long sessions.
            if(sequence==Int64.MaxValue) { Clear();sequence=0;index=0; }
            entries[index]=new Event {Target=target,Text=Math.Max(1,Math.Round((double)amount,MidpointRounding.AwayFromZero)).ToString("0",CultureInfo.InvariantCulture),ShieldHit=shieldHit,Active=true,Remaining=Lifetime,Pattern=pattern};
            pattern=(pattern+1)%4;order[index]=++sequence;return index;
        }
        public void Refresh(float delta)
        {
            if(float.IsNaN(delta)||float.IsInfinity(delta)||delta<0)throw new ArgumentException("delta");
            if(disposed)return;
            for(int i=0;i<entries.Length;i++)if(entries[i].Active) { var e=entries[i];e.Remaining=Math.Max(0,e.Remaining-delta);if(e.Remaining==0)e=new Event();entries[i]=e; }
        }
        // Source clear preserves serial/pattern. Reset removes all old event ownership.
        public void Retire(int index) { entries[index]=new Event();order[index]=0; }
        public void Clear() { Array.Clear(entries,0,entries.Length);Array.Clear(order,0,order.Length); }
        public void Dispose() { Clear();disposed=true; }
    }
}
