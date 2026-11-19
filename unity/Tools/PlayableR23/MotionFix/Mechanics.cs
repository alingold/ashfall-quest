using System;
using Ashfall.Core;
using Ashfall.Modules.Player;
using Ashfall.Modules.Flight;
using Ashfall.Integration.Playable;
static class Mechanics
{
    static int checks;static long tick;
    static void Check(bool okay,string label){if(!okay)throw new Exception(label);checks++;Console.WriteLine("PASS "+label);}
    sealed class Gate:IAuthorityGate,IFlightCollisionAuthorization
    {public RuleResult Validate(CommandContext c){return RuleResult.Accept();}public RuleResult ValidateCollision(CommandContext c,FlightMotionReceipt r,FlightCollisionResult f){return RuleResult.Accept();}}
    static CommandContext C(string id){return new CommandContext("motion-fix","player",id+tick,tick,0,true);}
    static FlightFrame Frame(bool spread)
    {return new FlightFrame{DeltaSeconds=.02f,Tracked=true,InputEnabled=true,XR=true,HandsReady=true,Forward=new Float3(0,0,1),MoveDirection=new Float3(0,0,1),WalkTarget=new Float3(0,0,6.5f),LeftHand=new Float3(spread?-.65f:-.2f,1.2f,0),RightHand=new Float3(spread?.65f:.2f,1.2f,0),DiveForward=!spread};}
    static void Step(FlightMachine flight,Gate gate,ref FlightFrame f)
    {++tick;f.Velocity=flight.Velocity;var result=flight.Step(C("step"),f);if(!result.Accepted)throw new Exception(result.Reason);var accept=flight.ApplyCollisionResult(C("accept"),flight.PendingMotion,new FlightCollisionResult{Velocity=flight.Velocity,Grounded=f.Supported},gate);if(!accept.Accepted)throw new Exception(accept.Reason);}
    static void Main()
    {
        // Reproduce the actual old root's curb energy transfer using the same
        // PlayerMotorFeedback and Locomotion owners, not an invented equation.
        var total=new Float3(0,.12f,.09906f);float dt=.01f;
        var old=PlayerMotorFeedback.Resolve(new RootMotionProposal{Translation=new Float3(0,0,.09906f),OwnsTranslation=true},total,dt,true,false);
        var motor=new Locomotion(new PlayerTuning{WalkMultiplier=2.5f});motor.SetVelocity(old.Velocity);
        var frame=new InputFrame{Tick=1,Sequence=1,Tracked=true,MoveY=1};var oldStep=motor.Step(frame,new MotionEnvironment{Grounded=true,IsXr=true},dt);
        Check(Math.Abs(old.Velocity.Y-12)<.001f&&oldStep.Velocity.Y>11.8f,"old real feedback+motor reproduces12cm curb ->12m/s launch");
        var fixedVelocity=R23RootVelocity.Accept(total,new Float3(0,.12f,0),dt,true,false);motor.SetVelocity(fixedVelocity);frame.Tick=frame.Sequence=2;var fixedStep=motor.Step(frame,new MotionEnvironment{Grounded=true,IsXr=true},dt);
        Check(fixedVelocity.Y==0&&Math.Abs(fixedVelocity.Z-9.906f)<.001f&&Math.Abs(fixedStep.Velocity.Y+.162f)<.00001f,"same owner now keeps support positional; source16.2 gravity unchanged");
        foreach(float seconds in new[]{.001f,.005f,.01f,.02f,.05f,.1f})foreach(float rise in new[]{-.25f,-.12f,0,.004f,.025f,.12f,.5f,1.5f})
        {var accepted=R23RootVelocity.Accept(new Float3(.04f,rise,.08f),new Float3(0,rise,0),seconds,true,false);Check(accepted.Y==0&&Math.Abs(accepted.X-.04f/seconds)<.001f,"supported rise/drop never launches atdt="+seconds+" rise="+rise);}
        Check(Math.Abs(R23RootVelocity.Accept(new Float3(0,.12f,0),new Float3(),.02f,false,false).Y-6)<.00001f,"real unsupported6m/s jump/fling retained");
        Check(R23RootVelocity.Accept(new Float3(0,.12f,0),new Float3(),.02f,false,true).Y==0,"actual ceiling still clips upward velocity");
        Check(Math.Abs(R23RootVelocity.Accept(new Float3(0,-.4f,0),new Float3(),.02f,false,false).Y+20)<.00001f,"real airborne fall momentum retained, no terminal/gravity disguise");
        var gate=new Gate();var flight=new FlightMachine("motion-fix","player",0,gate,6.5f,1);var f=Frame(true);
        for(int i=0;i<150;i++)Step(flight,gate,ref f);
        Check(flight.Mode==FlightMode.Glide&&Math.Abs(flight.Velocity.Y+12f/7)<.00001f,"3s neutral spread flight remains actual glide, no hidden dive timer");
        for(int cycle=0;cycle<8;cycle++)
        {
            f=Frame(true);f.HandsReady=false;for(int i=0;i<7;i++){Step(flight,gate,ref f);Check(flight.Mode==FlightMode.Glide,"140ms tracking gap retained");}f=Frame(false);for(int i=0;i<20;i++){Step(flight,gate,ref f);if(flight.Mode==FlightMode.Dive)throw new Exception("glide-release inherited dive");}
            double u=12.0/7/R26FreefallTuning.BellyTerminalSpeed,belly=-R26FreefallTuning.BellyTerminalSpeed*Math.Tanh(.5*Math.Log((1+u)/(1-u))+R26FreefallTuning.Gravity*.4/R26FreefallTuning.BellyTerminalSpeed);
            Check(flight.Mode==FlightMode.Air&&Math.Abs(flight.Velocity.Y-belly)<.001f,"glide release"+cycle+" uses only genuine gravity+belly drag for400ms (FLIGHT-DIVE r12)");
            f=Frame(true);Step(flight,gate,ref f);Check(flight.Mode==FlightMode.Glide&&Math.Abs(flight.Velocity.Y+12f/7)<.00001f,"reentry"+cycle+" restores controlled descent immediately");
            for(int i=0;i<80;i++)Step(flight,gate,ref f);
            Check(flight.Mode==FlightMode.Glide,"reentered spread flight"+cycle+" does not force dive after1s");
        }
        f=Frame(false);for(int i=0;i<45;i++)Step(flight,gate,ref f);Check(flight.Mode==FlightMode.Air,"genuine arms-folded900ms fall has no early dive");
        for(int i=0;i<15;i++)Step(flight,gate,ref f);Check(flight.Mode==FlightMode.Dive&&flight.Velocity.Y< -11&&flight.Velocity.Y> -R26FreefallTuning.BellyTerminalSpeed,"genuine1.2s folded forward freefall still dives, drag-limited (FLIGHT-DIVE r12)");
        f=Frame(true);Step(flight,gate,ref f);Check(flight.Mode==FlightMode.Glide&&Math.Abs(flight.Velocity.Y+12f/7)<.00001f,"spread hands recover from intentional dive on the same tick");
        f=Frame(false);for(int i=0;i<10;i++)Step(flight,gate,ref f);Check(flight.Mode==FlightMode.Air,"post-dive glide release does not inherit the old dive counter");
        f=Frame(true);f.Supported=true;Step(flight,gate,ref f);Check(flight.Mode==FlightMode.Ground,"supported spread hands remain grounded");
        flight=new FlightMachine("motion-fix","player",0,gate,6.5f,1);++tick;Check(flight.Launch(C("pad"),new Float3(0,0,1),45,true,false,false).Accepted,"authored pad launch remains supported");f=Frame(true);
        for(int i=0;i<14;i++)Step(flight,gate,ref f);Check(flight.Mode==FlightMode.Launch&&Math.Abs(flight.Velocity.Z-45)<.001f&&flight.Velocity.Y==0,"spread hands retain source300ms straight pad clearance");
        for(int i=0;i<4;i++)Step(flight,gate,ref f);Check(flight.Mode==FlightMode.Glide,"pad can hand off into spread glide after actual clearance");
        Console.WriteLine("MotionFix CPU checks="+checks+"; native curb/ramp/free-hand fixture staged separately, not executed.");
    }
}
