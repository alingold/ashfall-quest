using System;
using Ashfall.Core;
using Ashfall.Integration.Playable.Networking;
using Ashfall.Integration.Playable.Exclusion;
partial class LobbyCases
{
    // Round-1 dome-policy regressions, retained as analytic tests of the policy.
    // These do NOT model the new native eye sphere, mesh winding or PhysX.
    // Free space: dot(n,eye)<=d. Native proof lives in headclip2 captures.
    sealed class HfWorld
    {
        public Float3[] N;
        public float[] D;
        public bool GrazingError;
        readonly Float3[] vertices=HullVertices();
        static Float3[] HullVertices()
        {
            // SourceHeadConvex ring sampling, radius/top-to-eye both .12m.
            var result=new Float3[168];
            for(int ring=0;ring<7;ring++)for(int segment=0;segment<24;segment++) {
                double e=ring*Math.PI/12,a=segment*Math.PI/12;
                result[ring*24+segment]=new Float3((float)(Math.Cos(a)*Math.Cos(e)*.12),(float)(Math.Sin(e)*.12),(float)(-Math.Sin(a)*Math.Cos(e)*.12));
            }
            return result;
        }
        float Gap(int i,Float3 p)
        {
            float support=float.NegativeInfinity;
            foreach(Float3 v in vertices) support=Math.Max(support,Dot(N[i],v));
            return D[i]-Dot(N[i],p)-support;
        }
        static float Dot(Float3 a,Float3 b) {return a.X*b.X+a.Y*b.Y+a.Z*b.Z;}
        public bool Clear(Float3 p) {for(int i=0;i<N.Length;i++)if(Gap(i,p)<-.000001f)return false;return true;}
        public Float3 Sweep(Float3 p,Float3 motion)
        {
            float t=1;
            for(int i=0;i<N.Length;i++) {
                float into=Dot(N[i],motion);
                if(into>0) t=Math.Min(t,Math.Max(0,Gap(i,p)/into));
            }
            if(t<1 && GrazingError) t=Math.Min(1,t+.005f/Math.Max(.00001f,R25HeadClearance.Length(motion)));
            return R25HeadClearance.Scale(motion,t);
        }
    }
    static HfWorld HfCeiling(float slope,float height,bool error)
    {
        return new HfWorld {N=new[]{new Float3(slope,1,0),new Float3(0,-1,0)},D=new[]{height,0f},GrazingError=error};
    }
    static void HeadfixClearance()
    {
        foreach(float dt in new[]{1f/72,1f/120,.05f}) {
            var fade=new R25ObstructionFade();
            fade.Step(true,true,dt);
            Need(!fade.FullyFaded,"Timed warning alone cannot protect an abrupt safety breach");
            fade.Protect();
            Need(fade.FullyFaded&&fade.Alpha==1,"Emergency safety breach is opaque immediately");
            for(int i=0;i<1200;i++) {fade.Step(true,true,dt);Need(fade.FullyFaded,"Unsafe view never reopens after a six-second timer");}
            fade.Step(false,false,dt);
            Need(fade.FullyFaded,"Clearance hysteresis keeps first clear frame black");
            for(int i=0;i<240;i++)fade.Step(false,false,dt);
            Need(fade.Alpha==0&&!fade.Obstructed,"A safely clear view recovers without sticking black");
        }
        var recovery=new R25HeadRecoveryPolicy();
        Need(!recovery.TickOpaque(new Float3(),true,0),"Opaque timer starts without relocating");
        Need(!recovery.TickOpaque(new Float3(),true,1.99f),"Opaque recovery waits two seconds");
        Need(recovery.TickOpaque(new Float3(),true,2.01f),"Stationary black view recovers after two seconds");
        Need(!recovery.TickOpaque(new Float3(.02f,0,0),true,3),"Escape progress restarts timer");
        Need(!recovery.TickOpaque(new Float3(.02f,0,0),false,5),"Clear view cancels timer");
        Need(!recovery.TickOpaque(new Float3(.02f,0,0),true,6),"Fresh obstruction gets full timeout");
        recovery.ResetOpaque();
        Need(!recovery.TickOpaque(new Float3(),true,10),"Teleport forgets opaque timer");
        var core=new R25HeadClearance();
        Func<Float3,Float3,Float3> tinySlide=delegate(Float3 p,Float3 d){return new Float3(d.X,0,0);};
        Func<Float3,bool> open=delegate(Float3 p){return true;};
        var tiny=new Float3(.001f,-.003f,0);
        Need(core.Move(new Float3(0,1,0),tiny,tinySlide,open).X==0,"Historical 8mm chord backoff erases an air-acceleration-sized slide");
        Need(core.Move(new Float3(0,1,0),tiny,tinySlide,open,0).X==tiny.X,"Verified native sphere keeps tangential motion with its own normal clearance margin");
        int frames=0,stops=0;
        // X crossing: uphill/downhill and diagonals, walk/sprint, 72/120Hz/hitch.
        foreach(float ramp in new[]{.4f,-.4f})
        foreach(float direction in new[]{-1f,1f})
        foreach(float diagonal in new[]{0f,.5f})
        foreach(float speed in new[]{2f,6f})
        foreach(float dt in new[]{1f/72,1f/120,.05f}) {
            var world=HfCeiling(direction*.5f,2.15f,true);
            Float3 eye=new Float3(0,1.65f,0);
            float feet=0;
            for(int i=0;i<180;i++) {
                Float3 old=eye;
                Float3 wanted=new Float3(direction*speed*dt,0,diagonal*speed*dt);
#if HEADFIX_BEFORE
                Float3 motion=world.Sweep(eye,wanted);
#else
                Float3 motion=core.Move(eye,wanted,world.Sweep,world.Clear);
#endif
                eye=R25HeadClearance.Add(eye,motion);
                float rise=ramp*direction*motion.X;
                Float3 correction;
#if HEADFIX_BEFORE
                correction=world.Sweep(eye,new Float3(0,rise,0));
                if(Math.Abs(correction.Y-rise)<=.015f) eye=R25HeadClearance.Add(eye,correction);
#else
                if(core.Rise(eye,rise,world.Sweep,world.Clear,out correction))eye=R25HeadClearance.Add(eye,correction);
                else eye=old; // walking support is a transaction; blocked rise refuses walk
#endif
                if(R25HeadClearance.Length(R25HeadClearance.Sub(eye,old))<.0001f)stops++;
                feet=eye.Y-1.65f;
                Need(world.Clear(eye),"X crossing invariant frame "+i+" dt="+dt+" speed="+speed);
                Need(eye.Y>=0 && Math.Abs(eye.Y-feet-1.65f)<.0001f,"No floor launch");frames++;
            }
        }
        Need(stops>0,"Crossings stop beneath the inverted ramp");
        // Flat floor, slanted overhang: endpoint guard also runs on a miss.
        var slant=HfCeiling(.5f,2.15f,true);
        Float3 start=new Float3(.7f,1.65f,0);
        Float3 accepted=core.Move(start,new Float3(.2f,0,0),slant.Sweep,slant.Clear);
        Need(slant.Clear(R25HeadClearance.Add(start,accepted)),"Flat floor slanted ceiling");
        var clear=HfCeiling(0,3,false);var low=HfCeiling(0,1.95f,true);
        Float3 raised;
        Need(core.Rise(new Float3(0,1.65f,0),.45f,clear.Sweep,clear.Clear,out raised)&&Math.Abs(raised.Y-.45f)<.00001f,"0.45m kerb clear headroom");
        Need(!core.Rise(new Float3(0,1.65f,0),.45f,low.Sweep,low.Clear,out raised)&&raised.Y==0,"0.45m kerb refused beneath ceiling");
        Float3 stair=new Float3(0,1.65f,0);
        for(int i=0;i<6;i++) {
            if(core.Rise(stair,.1f,low.Sweep,low.Clear,out raised))stair=R25HeadClearance.Add(stair,raised);
            Need(low.Clear(stair),"Stairs low ceiling invariant");
        }
        Need(stair.Y<1.95f-.12f,"Stairs stop below ceiling");
        var beam=HfCeiling(0,1.2f,false);
        Float3 duck=new Float3(0,.9f,0);
        accepted=core.Move(duck,new Float3(1,0,0),beam.Sweep,beam.Clear);
        Need(accepted.X==1 && beam.Clear(R25HeadClearance.Add(duck,accepted)),"Real duck permits beam passage");
        var wall=new HfWorld{N=new[]{new Float3(1,0,0)},D=new[]{1f}};
        foreach(HfWorld world in new[]{wall,slant}) {
            Float3 old=new Float3(.7f,1.65f,0), desired=new Float3(.75f,1.65f,0);
            Float3 safe=core.Move(old,R25HeadClearance.Sub(desired,old),world.Sweep,world.Clear);
            Float3 blocked=R25HeadClearance.Sub(R25HeadClearance.Add(old,safe),desired);
            Float3 push=core.Push(blocked,.06f,delegate(Float3 x){return x;},delegate(Float3 x){return true;});
            Need(world.Clear(R25HeadClearance.Add(desired,push))&&push.Y==0&&R25HeadClearance.Length(push)<=.06001f,"Lean push keeps wall/overhang clear horizontally");
            Need(R25HeadClearance.Length(core.Push(blocked,.06f,delegate(Float3 x){return new Float3();},delegate(Float3 x){return true;}))==0,"Body wall blocks push; fade fallback");
            Need(R25HeadClearance.Length(core.Push(blocked,.06f,delegate(Float3 x){return x;},delegate(Float3 x){return false;}))==0,"Ledge refuses push");
        }
        Need(core.Push(new Float3(0,-.1f,0),.06f,delegate(Float3 x){return x;},delegate(Float3 x){return true;}).Y==0,"Vertical tracked lean never launches root");
        Need(core.Move(new Float3(0,1,0),new Float3(1,0,0),delegate(Float3 p,Float3 d){return d;},delegate(Float3 p){return p.X<.5f;}).X==0,"Even a missed sweep cannot publish overlapping endpoint");
        // Actual production host validator accepts the bounded root push and a
        // stationary safe head. The oracle is an analytic wall segment test.
        var history=new R24LanRawTrackingHistory();
        string hash=new string('b',64);
        Func<Float3,Float3,bool> oracle=delegate(Float3 a,Float3 b){return a.X<1 && b.X<1;};
        var pose=new R24LanRawTracking(hash,7,PoseAt(.7f,0,0),PoseAt(.7f,1.65f,0),PoseAt(.4f,1.1f,0),PoseAt(.9f,1.1f,0),0,0,1);
        Need(history.Accept(1,0,pose,true,1,oracle),"Host initial safe head");
        for(int i=1;i<=72;i++) {
            pose=new R24LanRawTracking(hash,7,PoseAt(.7f-.06f*i,0,0),PoseAt(.7f,1.65f,0),PoseAt(.4f,1.1f,0),PoseAt(.9f,1.1f,0),0,0,1);
            // Production raw tracking is normally sent at <=30Hz, not 72Hz.
            if(i%3==0)Need(history.Accept(i+1,i/72.0,pose,true,1,oracle),"Host accepts safe physical correction at raw send cadence");
        }
        Console.WriteLine("HEADFIX analytic frames="+frames+" stopped="+stops+" skin="+R25HeadClearance.Skin);
    }
}
