using System;
using Ashfall.Core;
using Ashfall.Modules.Flight;
// FLIGHT-DIVE (playtest 2026-11-15) CPU checks. Integrates the production FlightMachine at fixed
// 72/90/120 Hz exactly as the rig feeds it (last accepted velocity in, no collision) and asserts
// drag terminals, time-to-terminal, gentle stick ramp, frame-rate independence, unchanged ascent,
// mode semantics, prepared-path parity and the host movement-validation envelope.
sealed class R26FreefallTests : IAuthorityGate
{
    int checks; long tick;
    const float G=R26FreefallTuning.Gravity, VB=R26FreefallTuning.BellyTerminalSpeed, VD=R26FreefallTuning.DiveTerminalSpeed;
    static readonly int[] Rates={72,90,120};
    public RuleResult Validate(CommandContext c){return RuleResult.Accept();}
    CommandContext C(){tick++;return new CommandContext("r26","a","c"+tick,tick,0,true);}
    void Check(bool ok,string label){if(!ok)throw new Exception("FAIL "+label);checks++;Console.WriteLine("PASS "+label);}
    void Near(double a,double b,double tol,string label){Check(Math.Abs(a-b)<=tol,label+" (got "+a.ToString("F4")+", want "+b.ToString("F4")+" +/-"+tol+")");}
    FlightMachine M(){return new FlightMachine("r26","a",0,this,6.5f,1);}
    // Arms folded (no glide), XR tracked, optional forward stick.
    static FlightFrame F(float dt,float axis,bool legacy)
    {
        bool fwd=axis>.0001f||legacy;
        return new FlightFrame{DeltaSeconds=dt,InputEnabled=true,Tracked=true,XR=true,HandsReady=true,Forward=new Float3(0,0,1),
            LeftHand=new Float3(-.2f,1.2f,0),RightHand=new Float3(.2f,1.2f,0),DiveAxis=axis,DiveForward=legacy||axis>.65f,
            MoveDirection=fwd?new Float3(0,0,1):new Float3(),WalkTarget=fwd?new Float3(0,0,6.5f):new Float3()};
    }
    Float3 StepOnce(FlightMachine m,FlightFrame f){f.Velocity=m.Velocity;var r=m.Step(C(),f);if(!r.Accepted)throw new Exception("step rejected: "+r.Reason);return m.Velocity;}
    // Runs `seconds` of frames; records descending speed at every 1/6 s (a frame boundary at all three rates).
    double[] Run(FlightMachine m,int hz,double seconds,float axis,bool legacy,out double peakAccel,out double peakDecel,out double maxSpeed)
    {
        float dt=1f/hz;int frames=(int)Math.Round(seconds*hz),per=hz/6;var samples=new double[frames/per+1];
        double prev=-m.Velocity.Y;peakAccel=0;peakDecel=0;maxSpeed=prev;samples[0]=prev;
        for(int i=1;i<=frames;i++)
        {
            double s=-StepOnce(m,F(dt,axis,legacy)).Y;peakAccel=Math.Max(peakAccel,(s-prev)/dt);peakDecel=Math.Max(peakDecel,(prev-s)/dt);maxSpeed=Math.Max(maxSpeed,s);prev=s;
            if(i%per==0)samples[i/per]=s;
        }
        return samples;
    }
    static double FirstAt(double[] samples,double speed){for(int i=0;i<samples.Length;i++)if(samples[i]>=speed)return i/6.0;return Double.PositiveInfinity;}
    void SameAcrossRates(double[][] runs,double tol,string label)
    {
        double worst=0;for(int r=1;r<runs.Length;r++)for(int i=0;i<runs[0].Length;i++)worst=Math.Max(worst,Math.Abs(runs[r][i]-runs[0][i]));
        Check(worst<=tol,label+" 72/90/120 Hz max |dv|="+worst.ToString("F4")+" m/s <= "+tol);
    }
    void Run()
    {
        // Tuning and closed forms.
        Near(R26Freefall.TerminalSpeed(0),VB,1e-3,"belly terminal constant");Near(R26Freefall.TerminalSpeed(1),VD,1e-3,"head-down terminal constant");
        Check(G==13f&&VB==70&&VD==100,"13 m/s2 descent and r18 terminal speeds (belly "+VB+", dive "+VD+")");
        Near(R26Freefall.DiveCommand(R26FreefallTuning.DiveStickDeadZone,false),0,1e-6,"stick at dead zone keeps belly pitch");
        Near(R26Freefall.DiveCommand(1,false),1,1e-6,"full stick commands head-down");Near(R26Freefall.DiveCommand(0,true),1,1e-6,"boolean-only callers get full dive");
        Near(R26Freefall.DiveCommand(-1,false),0,1e-6,"pull back never dives");
        float avg;float p=R26Freefall.StepPitch(0,1,.1f,out avg);Near(p,.1f/R26FreefallTuning.DiveTuckSeconds,1e-6,"pitch ramps at the tuck rate");Near(avg,p*.5f,1e-6,"ramp average is the step midpoint");
        // Exactness: one 0.1 s step equals ten 0.01 s steps at constant pitch (closed-form drag solution).
        float one=R26Freefall.Fall(-3,0,.1f),ten=-3;for(int i=0;i<10;i++)ten=R26Freefall.Fall(ten,0,.01f);Near(one,ten,2e-4,"closed-form drag is step-size independent");
        float over=R26Freefall.Fall(-75,0,.1f);Check(over> -75&&over< -VB,"above-terminal descent decelerates without undershooting terminal");
        // Ascent keeps the exact source gravity step: pads and jumps are bit-identical.
        foreach(int hz in Rates){float dt=1f/hz,v=45;bool exact=true;for(int i=0;i<200&&v-R26FreefallTuning.AscentGravity*dt>=0;i++){float n=R26Freefall.Fall(v,1,dt);exact&=n==v-16.2f*dt;v=n;}Check(exact,"ascent is pure source gravity at "+hz+" Hz");}
        foreach(int hz in Rates)
        {
            float dt=1f/hz;var m=M();m.Step(C(),new FlightFrame{DeltaSeconds=dt,InputEnabled=true,Tracked=true,Forward=new Float3(0,0,1),Supported=false,Velocity=new Float3(0,6,0)});
            double y=0,apex=0;Float3 v=m.Velocity;y+=v.Y*dt;for(int i=0;i<3*hz;i++){v=StepOnce(m,F(dt,0,false));y+=v.Y*dt;apex=Math.Max(apex,y);}
            float ov=6;double oy=0,oapex=0;for(int i=0;i<=3*hz&&ov>0;i++){ov-=16.2f*dt;oy+=ov*dt;oapex=Math.Max(oapex,oy);}
            Near(apex,oapex,1e-4,"6 m/s jump apex identical to source gravity at "+hz+" Hz ("+apex.ToString("F3")+" m)");
        }
        // Belly freefall from rest.
        var belly=new double[3][];
        for(int r=0;r<3;r++)
        {
            int hz=Rates[r];double acc,dec,max;var m=M();belly[r]=Run(m,hz,40,0,false,out acc,out dec,out max);
            Near(belly[r][240],VB,.02,"belly terminal after 40 s at "+hz+" Hz");Check(max<=VB+.01,"belly never exceeds terminal at "+hz+" Hz (max "+max.ToString("F3")+")");
            Check(acc<=G+.01,"drag never adds acceleration beyond gravity at "+hz+" Hz");
            double t90=FirstAt(belly[r],.9*VB);Check(t90>=7.9&&t90<=8.2,"belly reaches 90% terminal in 7.9..8.2 s at "+hz+" Hz (t="+t90.ToString("F2")+")");
            for(int k=1;k<=3;k++)Near(belly[r][6*k],VB*Math.Tanh(G*k/VB),.02,"belly speed matches tanh closed form at "+k+" s, "+hz+" Hz");
            Check(m.Mode==FlightMode.Air,"no stick: belly freefall stays Air");
        }
        SameAcrossRates(belly,.02,"belly freefall from rest");
        // Full head-down dive from settled belly terminal: gradual, bounded, frame-rate independent.
        var dive=new double[3][];var release=new double[3][];
        for(int r=0;r<3;r++)
        {
            int hz=Rates[r];double acc,dec,max,a2,d2,m2;var m=M();Run(m,hz,40,0,false,out acc,out dec,out max);
            dive[r]=Run(m,hz,40,1,false,out acc,out dec,out max);
            Check(m.Mode==FlightMode.Dive,"held forward stick reports Dive at "+hz+" Hz");
            Near(dive[r][240],VD,.05,"head-down terminal after 40 s at "+hz+" Hz");Check(max<=VD+.01,"dive never exceeds head-down terminal at "+hz+" Hz (max "+max.ToString("F3")+")");
            Check(acc<=12,"dive acceleration is gentle at "+hz+" Hz (peak "+acc.ToString("F2")+" m/s2; old model 112)");
            Check(dive[r][3]<=VB+2.5,"0.5 s after full stick speed is still near belly ("+dive[r][3].ToString("F2")+" m/s)");
            double t=FirstAt(dive[r],VB+.9*(VD-VB));Check(t>=9.8&&t<=12.3,"dive closes 90% of belly->head-down gap in 9.8..12.3 s at "+hz+" Hz (t="+t.ToString("F2")+")");
            release[r]=Run(m,hz,40,0,false,out a2,out d2,out m2);
            Near(release[r][240],VB,.05,"releasing the stick returns to belly terminal at "+hz+" Hz");Check(d2<=17,"arch-out deceleration stays comfortable at "+hz+" Hz (peak "+d2.ToString("F2")+" m/s2)");
            Check(m.Mode==FlightMode.Air,"released stick reports Air");
        }
        SameAcrossRates(dive,.05,"full dive from belly terminal");SameAcrossRates(release,.05,"stick release back to belly");
        // Partial stick gives a proportional, intermediate terminal; dead zone stays belly.
        foreach(int hz in Rates)
        {
            double acc,dec,max;var m=M();float half=(R26FreefallTuning.DiveStickDeadZone+R26FreefallTuning.DiveStickFull)*.5f;
            var s=Run(m,hz,40,half,false,out acc,out dec,out max);Near(s[s.Length-1],R26Freefall.TerminalSpeed(.5f),.05,"half stick terminal ("+R26Freefall.TerminalSpeed(.5f).ToString("F2")+" m/s) at "+hz+" Hz");
            Check(s[s.Length-1]>VB+3&&s[s.Length-1]<VD-3,"half stick terminal lies between belly and head-down");
            m=M();s=Run(m,hz,40,R26FreefallTuning.DiveStickDeadZone*.8f,false,out acc,out dec,out max);Near(s[s.Length-1],VB,.02,"stick inside dead zone keeps belly terminal at "+hz+" Hz");
            m=M();var legacy=Run(m,hz,40,0,true,out acc,out dec,out max);m=M();var analog=Run(m,hz,40,1,false,out acc,out dec,out max);
            Near(legacy[legacy.Length-1],analog[analog.Length-1],1e-4,"boolean DiveForward equals full analog stick at "+hz+" Hz");
        }
        // Jump off and hold forward from the start: source 1 s gate, then gradual pitch-in.
        var hold=new double[3][];
        for(int r=0;r<3;r++)
        {
            int hz=Rates[r];double acc,dec,max;var m=M();float dt=1f/hz;int gate=-1;
            for(int i=1;i<=hz;i++){StepOnce(m,F(dt,1,false));if(m.Mode==FlightMode.Dive&&gate<0)gate=i;}
            Check(gate<0||gate>=hz-6,"dive is not available before ~1 s of freefall at "+hz+" Hz");
            hold[r]=Run(m,hz,9,1,false,out acc,out dec,out max);Check(m.Mode==FlightMode.Dive,"dive engaged after the gate at "+hz+" Hz");
            Check(hold[r][3]<20,"0.5 s after the gate speed is still below belly terminal ("+hold[r][3].ToString("F2")+" m/s; old model ~70)");
            Check(max<=VD+.01&&acc<=G+.01,"jump-and-dive bounded by head-down terminal and gravity at "+hz+" Hz");
        }
        // The 1 s gate opens on a frame boundary, so allow one frame (~14 ms) of quantization.
        SameAcrossRates(hold,.25,"jump then hold forward");
        // Mode transitions keep working.
        {
            float dt=1f/90;var m=M();for(int i=0;i<4*90;i++)StepOnce(m,F(dt,1,false));Check(m.Mode==FlightMode.Dive&&-m.Velocity.Y>30,"dive setup");
            var f=F(dt,1,false);f.InventoryOpen=true;float before=-m.Velocity.Y;StepOnce(m,f);Check(m.Mode==FlightMode.Air&&Math.Abs(-m.Velocity.Y-before)<.2f,"inventory cancels dive without a speed snap");
            for(int i=0;i<90;i++)StepOnce(m,F(dt,1,false));
            f=F(dt,1,false);f.LeftHand=new Float3(-.65f,1.2f,0);f.RightHand=new Float3(.65f,1.2f,0);f.DiveAxis=0;f.DiveForward=false;StepOnce(m,f);
            Check(m.Mode==FlightMode.Glide&&Math.Abs(m.Velocity.Y+m.GlideSpeed/7)<1e-5,"neutral stick with spread arms recovers wingsuit glide");
            for(int i=0;i<14;i++)StepOnce(m,F(dt,1,false));Check(m.Mode==FlightMode.Air&&-m.Velocity.Y<m.GlideSpeed/7+2.1f,"glide release starts belly, no inherited dive pitch");
            for(int i=0;i<3*90;i++)StepOnce(m,F(dt,1,false));
            f=F(dt,1,false);f.Supported=true;f.Velocity=new Float3();m.Step(C(),f);Check(m.Mode==FlightMode.Ground,"landing returns to Ground");
            f=F(dt,1,false);f.Velocity=new Float3(0,-.1f,0);StepOnce(m,f);for(int i=0;i<30;i++)StepOnce(m,F(dt,1,false));Check(m.Mode==FlightMode.Air,"landing reset the dive gate");
            m=M();for(int i=0;i<4*90;i++)StepOnce(m,F(dt,1,false));
            Check(m.Launch(C(),new Float3(0,3,4),45,true,false,false).Accepted&&m.Mode==FlightMode.Launch,"pad launch from a dive accepted");
            Near(m.Velocity.Y,27,1e-4,"pad replaces dive velocity (y)");Near(m.Velocity.Z,36,1e-4,"pad replaces dive velocity (z)");
            for(int i=0;i<27;i++)StepOnce(m,F(dt,1,false));Check(m.Mode==FlightMode.Launch&&Math.Abs(m.Velocity.Y-27)<1e-4,"pad face clearance unchanged");
            float vy=m.Velocity.Y;StepOnce(m,F(dt,1,false));Check(m.Velocity.Y==vy-16.2f*dt,"pad ascent after clearance is pure source gravity");
        }
        // ParityHarness FLIGHT-PAD-DIVE-01 setup (that harness does not compile on this tree): one 1 s frame, boolean dive.
        {
            var m=M();var f=new FlightFrame{DeltaSeconds=1,Tracked=true,InputEnabled=true,Forward=new Float3(0,0,1),Velocity=new Float3(0,-10,0),DiveForward=true};
            Check(m.Step(C(),f).Accepted&&m.Mode==FlightMode.Dive,"parity fixture: one-second boolean dive setup reaches Dive");
            Check(m.Launch(C(),new Float3(0,3,4),45,true,false,false).Accepted&&m.Mode==FlightMode.Launch,"parity fixture: pad launch clears Dive");
            Near(m.Velocity.Y,27,1e-4,"parity fixture: launch velocity y");Near(m.Velocity.Z,36,1e-4,"parity fixture: launch velocity z");
        }
        // Prepared (host root transaction) path threads pitch exactly like Step.
        {
            float dt=1f/72;var a=M();var b=M();bool same=true;
            for(int i=0;i<8*72;i++)
            {
                var f=F(dt,i<5*72?1:0,false);f.Velocity=a.Velocity;a.Step(C(),f);
                f.Velocity=b.Velocity;var c=C();PreparedFlightStep plan;var r=b.PrepareStep(c,f,out plan);Float3 produced;
                if(!r.Accepted||!b.PublishPreparedStep(c,plan,0,new Float3(),out produced).Accepted)throw new Exception("prepared step rejected");
                same&=a.Velocity.Y==b.Velocity.Y&&a.Mode==b.Mode;
            }
            Check(same,"prepared step path matches Step through dive and release");
        }
        // Robustness and host envelope.
        {
            var m=M();var f=F(.1f,0,false);f.Velocity=new Float3(0,-10000,0);Check(m.Step(C(),f).Accepted&&-m.Velocity.Y<10000&&-m.Velocity.Y>VB,"absurd descent decelerates toward terminal, stays bounded");
            f=F(.01f,0,false);f.DiveAxis=float.NaN;Check(!m.Step(C(),f).Accepted,"NaN dive axis rejected");f.DiveAxis=3;Check(!m.Step(C(),f).Accepted,"out-of-range dive axis rejected");
            double worstDown=0,worstHorizontal=0;
            foreach(int hz in Rates){float dt=1f/hz;m=M();for(int i=0;i<60*hz;i++){var v=StepOnce(m,F(dt,1,false));worstDown=Math.Max(worstDown,-v.Y);worstHorizontal=Math.Max(worstHorizontal,Math.Sqrt(v.X*v.X+v.Z*v.Z));}}
            // R24LanRawTracking.Accept: horizontal <= max(3, 55*dt), down <= max(3, 360*dt). Old model reached 356 m/s down after ~22 s.
            Check(worstDown<=VD+.01&&worstDown<360,"60 s dive stays under host 360 m/s descent cap (max "+worstDown.ToString("F2")+")");
            Check(worstHorizontal<55,"dive horizontal stays under host 55 m/s cap (max "+worstHorizontal.ToString("F2")+")");
        }
        {
            // FLY4 (FLY-CLIFF): run off an edge with the arms spread and the stick held forward.
            float dt=1f/72;
            Func<float,bool,bool,bool,FlightFrame> W=delegate(float axis,bool supported,bool spread,bool near)
            {
                var f=F(dt,axis,false);f.DiveForward=false;f.Supported=supported;f.NearGround=near;
                if(spread){f.LeftHand=new Float3(-.75f,.8f,-.05f);f.RightHand=new Float3(.75f,.8f,-.05f);}
                return f;
            };
            var m=M();for(int i=0;i<30;i++)StepOnce(m,W(1,true,true,false));
            Check(m.Mode==FlightMode.Ground,"running with spread arms on the ground stays Ground");
            StepOnce(m,W(1,false,true,false));
            Check(m.Mode==FlightMode.Glide,"run-off with spread arms and the stick held glides in the first airborne frame");
            bool held=true;for(int i=0;i<144;i++){StepOnce(m,W(1,false,true,false));held&=m.Mode==FlightMode.Glide;}
            Check(held,"the glide holds for 2 s while the running stick stays forward");
            for(int i=0;i<3;i++)StepOnce(m,W(0,false,true,false));StepOnce(m,W(1,false,true,false));
            Check(m.Mode==FlightMode.Glide,"a 42 ms stick dropout does not re-arm the dive");
            for(int i=0;i<7;i++)StepOnce(m,W(0,false,true,false));
            Check(m.Mode==FlightMode.Glide,"releasing the stick keeps the glide");
            StepOnce(m,W(1,false,true,false));
            Check(m.Mode==FlightMode.Glide,"a fresh push after a release keeps the wings open");
            for(int i=0;i<80;i++)StepOnce(m,W(1,false,true,false));
            Check(m.Mode==FlightMode.Glide,"forward still glides after the dive gate with arms spread");
            StepOnce(m,W(0,false,true,false));Check(m.Mode==FlightMode.Glide,"releasing the stick in the dive reopens the glide");

            R26GlideEntry.CarriedStickRule=false;
            m=M();for(int i=0;i<30;i++)StepOnce(m,W(1,true,true,false));StepOnce(m,W(1,false,true,false));
            Check(m.Mode==FlightMode.Glide,"archived carried-stick switch cannot disable XR glide");
            R26GlideEntry.CarriedStickRule=true;

            m=M();for(int i=0;i<30;i++)StepOnce(m,W(0,true,true,false));StepOnce(m,W(0,false,true,false));
            Check(m.Mode==FlightMode.Glide,"neutral stick: stepping off glides in the first airborne frame");
            StepOnce(m,W(1,false,true,false));
            Check(m.Mode==FlightMode.Glide,"neutral at take-off: the first push keeps glide");

            m=M();for(int i=0;i<30;i++)StepOnce(m,W(1,true,false,false));
            for(int i=0;i<80;i++)StepOnce(m,W(1,false,false,false));
            Check(m.Mode==FlightMode.Dive,"arms folded: the held stick still dives after the gate (unchanged)");

            // Step-downs: no glide within the entry clearance while descending.
            m=M();for(int i=0;i<30;i++)StepOnce(m,W(1,true,true,false));
            bool flies=true;for(int i=0;i<10;i++){StepOnce(m,W(1,false,true,true));flies&=m.Mode==FlightMode.Glide;}
            Check(flies,"unsupported near-floor frames glide");
            StepOnce(m,W(1,true,true,false));Check(m.Mode==FlightMode.Ground,"and lands");
            StepOnce(m,W(1,false,true,true));StepOnce(m,W(1,false,true,false));
            Check(m.Mode==FlightMode.Glide,"once the floor is beyond the clearance the glide opens in that frame");
            StepOnce(m,W(1,false,true,true));
            Check(m.Mode==FlightMode.Glide,"an engaged glide flies on to touchdown near the ground");
            m=M();for(int i=0;i<30;i++)StepOnce(m,W(0,true,true,false));
            var up=W(0,false,true,true);up.Velocity=new Float3(0,3,0);Check(m.Step(C(),up).Accepted&&m.Mode==FlightMode.Glide,"a jump (rising) still opens the glide near the ground");
            R26GlideEntry.StepDownRule=false;
            m=M();for(int i=0;i<30;i++)StepOnce(m,W(1,true,true,false));StepOnce(m,W(1,false,true,true));
            Check(m.Mode==FlightMode.Glide,"r14 rule (control): a step-down opened the glide");
            R26GlideEntry.StepDownRule=true;

            // Seated or standing: only the horizontal hand separation matters.
            foreach(float y in new[]{.45f,.8f,1.45f})
            {
                m=M();for(int i=0;i<30;i++)StepOnce(m,W(1,true,true,false));
                var f=W(1,false,true,false);f.LeftHand=new Float3(-.75f,y,-.05f);f.RightHand=new Float3(.75f,y-.05f,-.05f);StepOnce(m,f);
                Check(m.Mode==FlightMode.Glide,"hands at "+y.ToString("F2")+" m above the feet glide in the first airborne frame");
            }
        }
        Console.WriteLine("TOTAL "+checks+" R26 freefall checks PASS");
    }
    static int Main(){try{new R26FreefallTests().Run();return 0;}catch(Exception e){Console.Error.WriteLine(e.Message);Console.Error.WriteLine(e.StackTrace);return 1;}}
}
