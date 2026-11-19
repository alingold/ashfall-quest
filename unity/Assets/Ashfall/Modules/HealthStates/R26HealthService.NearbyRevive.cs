using System;using Ashfall.Core;
namespace Ashfall.Modules.HealthStates
{
    public sealed partial class HealthService
    {
        // Either electrode can touch the torso. Use the displayed paddle centre and the
        // tracked head, so calibration and a seated rig translate both into world space.
        internal static Float3 TorsoPoint(Float3 pad,Float3 chest)
        {return new Float3(chest.X,Math.Max(chest.Y-.35f,Math.Min(chest.Y+.55f,pad.Y)),chest.Z);}
        internal static bool TouchesTorso(Float3 pad,Float3 chest)
        {return pad.IsFinite&&chest.IsFinite&&Distance(pad,TorsoPoint(pad,chest))<=.35;}
        bool ClearPad(string actor,string target,Float3 pad,Float3 chest)
        {return TouchesTorso(pad,chest)&&query.HasLineOfSight(actor,target,pad,TorsoPoint(pad,chest),1);}
        internal RuleResult ReviveNearby(CommandContext c,string target,long revision,bool enemyRevive=false,bool paddles=false)
        {return AtomicHealth(delegate{lock(sync){
            RuleResult r=Admit(c);if(!r.Accepted)return r;Player actor,p;RescuePose a,t;
            if(!world.InMatch||!players.TryGetValue(c.ActorId,out actor)||!Living(actor)||!players.TryGetValue(target,out p)||!p.Connected||p.Life!=LifeState.Downed||p.Revision!=revision||teams==0||(!enemyRevive&&actor.Team!=p.Team)||actor==p)return RuleResult.Reject("revive-state");
            if(!world.TryGetPose(c.ActorId,out a)||!world.TryGetPose(target,out t))return RuleResult.Reject("pose");
            if(paddles){
                if(actor.ReadyUntil<=world.Now)return RuleResult.Reject("paddles-not-ready");
                if(!Usable(a)||!(ClearPad(c.ActorId,target,a.Left,t.Chest)||ClearPad(c.ActorId,target,a.Right,t.Chest)))return RuleResult.Reject("paddle-contact");
            }else if(!a.BodyCenter.IsFinite||!t.BodyCenter.IsFinite||Distance(a.BodyCenter,t.BodyCenter)>1||!query.HasLineOfSight(c.ActorId,target,a.BodyCenter,t.BodyCenter,1))return RuleResult.Reject("contacts-or-occlusion");
            p.LifeGeneration++;p.Life=LifeState.Alive;p.Health=50;p.Shield=0;p.DownedUntil=0;p.RespawnAt=0;p.ProtectionUntil=world.Now+protection;p.Revision++;
            Cancel(p);Publish(p,revision,"revive");return RuleResult.Accept();
        }});}
    }
}
