using System;
using System.Collections.Generic;
using System.IO;
using Ashfall.Core;
using Ashfall.Modules.Player;
using Ashfall.Modules.HealthStates;
using Ashfall.Modules.Sessions;
using Ashfall.Modules.Modes;
using Ashfall.Modules.Presentation;
using Ashfall.Integration.PersonalSettings;
public static class PresentationR9Tests
{
    static long tick;static int count;
    static void C(bool b,string message){if(!b)throw new Exception(message);count++;}
    static void N(float a,float b){C(Math.Abs(a-b)<.0002f,"expected "+b+" actual "+a);}
    sealed class Actions:ISettingsUiActions{public void SetPaused(bool b){}public bool CanRequest(string a){return false;}public void Request(string a,long o){}}
    static MotionStep Step(Locomotion m,bool sprint,bool moving)
    {tick++;return m.Step(new InputFrame{Tick=tick,Sequence=tick,Tracked=true,Sprint=sprint,MoveY=moving?1:0},new MotionEnvironment{IsXr=true,Grounded=true},.1f);}
    public static int Main(string[] args)
    {
        try{
            Directory.CreateDirectory(args[0]);
            var a=new PersonalSettingsAdapter(Path.Combine(args[0],"r9.ashfall-settings.json"),SettingsSource.Quest,new Actions());a.CaptureSettings();
            var b=new PlayerSettingsBinding(new PlayerInput());b.Apply(a.Effective);var m=b.CreateLocomotion();
            N(m.StaminaMaximum,6);N(m.SprintStamina,6);Step(m,true,true);N(m.SprintStamina,5.9f);
            m.CancelMotion();Step(m,false,false);N(m.SprintStamina,6);
            C(a.CommitSettings(new Dictionary<string,string>{{"sprint_stamina_seconds","2"},{"sprint_recovery_seconds","20"}}).Accepted,"durable tuning");
            var old=(TypedPlayerSettingsPort)m.Settings;b.Apply(a.Effective);N(old.StaminaSeconds,6);N(((TypedPlayerSettingsPort)m.Settings).StaminaSeconds,2);
            C(Object.ReferenceEquals(m,b.CreateLocomotion()),"same actual motor");m.ResetMotion();N(m.SprintStamina,2);
            for(int i=0;i<19;i++)Step(m,i==0,true);N(m.SprintStamina,.1f);
            Step(m,false,true);N(m.SprintStamina,0);C(m.SprintTier==0,"duration expires actual tier");
            Step(m,false,false);N(m.SprintStamina,.01f);Step(m,true,true);C(m.SprintTier==0,"quarter capacity eligibility");
            for(int i=0;i<50;i++)Step(m,false,false);C(m.SprintStamina>.5f,"recovery progresses");Step(m,true,true);C(m.SprintTier==1,"quarter threshold opens");
            // A prepared calculation must remain detached and reject settings replacement.
            float before=m.SprintStamina;PreparedPlayerStep plan;tick++;
            C(m.PrepareStep(new InputFrame{Tick=tick,Sequence=tick,Tracked=true,MoveY=1},new MotionEnvironment{Grounded=true,IsXr=true},.1f,out plan).Accepted,"prepare");N(m.SprintStamina,before);
            b.Apply(a.Effective);C(!plan.Validate().Accepted,"immutable capability replacement invalidates plan");plan.Abort();N(m.SprintStamina,before);
            // Failed gain validation must leave the original input retryable.
            var saved=m.Settings;m.Settings=new BadGain();tick++;var frame=new InputFrame{Tick=tick,Sequence=tick,Tracked=true,MoveY=1};
            bool rejected=false;try{m.Step(frame,new MotionEnvironment{Grounded=true,IsXr=true},.1f);}catch(ArgumentException){rejected=true;}
            C(rejected,"invalid gain rejects");N(m.SprintStamina,before);m.Settings=saved;m.Step(frame,new MotionEnvironment{Grounded=true,IsXr=true},.1f);C(true,"exact input retry");
            var skin=new PersonalSettingsAdapter(Path.Combine(args[0],"skin.ashfall-settings.json"),SettingsSource.SkinRuntime,new Actions());skin.CaptureSettings();
            C(skin.Effective.CreatePlayerTuning().MaximumWalkMultiplier==3&&a.Effective.CreatePlayerTuning().MaximumWalkMultiplier==5,"distinct source profiles");
            var w=new EnvironmentPorts();var hp=new HealthService("s",0,0,true,w,w,w);
            C(hp.Register(new CommandContext("s","p","reg",1,0,true),"p",-1,false).Accepted,"health");hp.DrainTransitions();
            var session=new Session("s","content",new SessionRules("map","respawn","hash",0,8,true,false,true,true,5),w,w,w,w,w);string token;
            C(session.Join(new CommandContext("s","p","join",2,0,true),new JoinRequest(Protocol.Name,Protocol.Schema,"content","name",null,false),out token).Accepted,"session");
            var round=new RoundController(new ModeRules("respawn",0,10,120,1),w,"p","s",0,w,64);C(round.StartRound(new CommandContext("s","p","start",3,0,true),0).Accepted,"round");
            var hud=new HudModel();var state=new PresentationStateBinding(hp,session,round,"p",delegate{return w.Now;},hud);var audio=new AudioSink();var host=new CommittedPresentationBinding(state,m,audio);var sink=new TargetSink();
            host.BindTarget("p",new TargetFeedbackBinding(hp,"s",0,"p",hud,sink));hud.Update(host.Capture(),false,0);N(hud.Sprint,m.SprintStamina/m.StaminaMaximum);
            HealthTransition local=null;for(int i=0;i<2;i++){
                var beforeHealth=hp.Read("p");C(hp.ApplyLoss(new CommandContext("s","p","damage"+i,4+i,0,true),"p",beforeHealth.Revision,i==0?7:11,0,false).Accepted,"actual committed loss");
                foreach(var e in hp.DrainTransitions()){host.ObserveCommitted(e);if(i==1)local=e;}}
            C(sink.Count==0,"observation never attribution");host.AuthorizeLocalOutgoing(local);host.AuthorizeLocalOutgoing(local);N(sink.Amount,11);C(sink.Count==1,"exact local once");
            hud.Update(host.Capture(),false,0);N(hud.Incoming,18);N(hud.Health,.82f);host.PublishAudio(a.Effective);C(audio.Count==5,"all five source buses");
            Console.WriteLine("R9 "+count+" PASS");return 0;
        }catch(Exception e){Console.WriteLine(e);return 1;}
    }
    sealed class BadGain:IPlayerSettingsPort{public ForwardReference ForwardReference{get{return ForwardReference.Head;}}public float GroundSpeedGain(float g){return float.NaN;}}
    sealed class TargetSink:ITargetFeedbackPort{public int Count;public float Amount;public void Emit(string id,float amount,bool shield){Count++;Amount=amount;}}
    sealed class AudioSink:IPersonalAudioPort{public int Count;public void ApplyBus(string b,float g,float db,bool muted){Count++;}}
}
