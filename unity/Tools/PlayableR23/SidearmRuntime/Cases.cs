using System;
using Ashfall.Core;
using Ashfall.Modules.WeaponHandling;
using Ashfall.Modules.HealthStates;
using Ashfall.Integration.Playable.HandsWeapons;
static class Cases
{
    static int count;
    static void Check(bool ok,string name){if(!ok)throw new Exception(name);Console.WriteLine("PASS "+name);count++;}
    static HandlingInput Input(bool trigger=false,bool grip=false,Float3 point=new Float3(),bool left=false){return new HandlingInput{Enabled=true,PrimaryTracked=true,SupportTracked=true,Trigger=trigger,Grip=grip,SupportLocal=point,ForegripDistance=1,LeftHanded=left,HasPrimaryPose=true,PrimaryForward=new Float3(0,0,1),PrimaryUp=new Float3(0,1,0)};}
    static bool Step(R23MagnumRuntime gun,float dt=.02f,HandlingInput input=new HandlingInput(),bool click=false,bool flickBlock=false,float x=0,float y=0,bool desktop=false){if(!input.Enabled&& !input.Paused&&!input.HeadObstructed&&!input.PrimaryClimbing)input=Input();return gun.Step(dt,new R23MagnumInput(input,click,true,flickBlock,x,y,desktop));}
    static bool Fire(R23MagnumRuntime gun){Step(gun,.25f,Input());return Step(gun,.084f,Input(true));}
    static void Open(R23MagnumRuntime gun){Step(gun);Step(gun,click:true);}
    static void Load(R23MagnumRuntime gun,bool left=false){Step(gun,input:Input(false,false,R23MagnumRuntime.Offer(left),left));Step(gun,input:Input(false,true,R23MagnumRuntime.Offer(left),left));Step(gun,input:Input(false,true,R23MagnumRuntime.OpenPoint(left),left));}
    static void Close(R23MagnumRuntime gun,bool left=false){var p=R23MagnumRuntime.OpenPoint(left);Step(gun,input:Input(false,false,p,left));Step(gun,input:Input(false,true,p,left));Step(gun,input:Input(false,true,new Float3(p.X,p.Y+.051f,p.Z),left));}
    sealed class World:IAuthorityGate,IHealthWorld,IInteractionQuery{
        public bool Visible=true;long sequence;public double Now{get{return 2;}}public bool InMatch{get{return true;}}public bool TeamChangesAllowed{get{return false;}}
        public CommandContext Command(){return new CommandContext("health","player","hit/"+(++sequence),sequence,0,true);}
        public RuleResult Validate(CommandContext c){return RuleResult.Accept();}public bool TryGetPose(string id,out RescuePose pose){pose=new RescuePose();return false;}public bool HasLineOfSight(string a,string b,Float3 o,Float3 p,float r){return Visible;}
    }
    static int Main(){try{
        var pool=R23MagnumRuntime.CreatePistolReserve();var gun=new R23MagnumRuntime(pool,"owned/magnum");
        Check(gun.Loaded==7&&gun.Reserve==1200&&gun.ChamberMask==127,"source starter has7 native chambers and finite shared pistol1200");
        Check(!Step(gun,input:Input(true)),"selection requires trigger release");
        Check(Fire(gun)&&gun.Loaded==6&&gun.ChamberMask==126,"actual native accepted shot removes exactly one source chamber");
        Check(!Step(gun,.25f,Input(true))&&gun.Loaded==6,"source semi-auto never repeats held trigger");
        Step(gun,.01f);Check(!Step(gun,.01f,Input(true)),"source3rps cadence prevents early re-press");
        for(int i=0;i<6;i++)Check(Fire(gun),"native chamber discharge "+i);
        Check(gun.Loaded==0&&gun.ChamberMask==0&&gun.Snapshot.Phase==ReloadPhase.CylinderOpen,"last native shot opens cylinder without ejector/fake spent rounds");
        Load(gun);Check(gun.Loaded==7&&pool.Available==1193&&gun.Snapshot.Phase==ReloadPhase.CylinderLoaded&&!gun.ReloadHandOccupied,"physical forward .06 load debits actual shared reserve once");
        Step(gun,input:Input(false,true,new Float3(0,.2f,.1f)));Check(gun.Snapshot.Phase==ReloadPhase.CylinderLoaded,"fresh support grip required for separate closing stroke");
        Close(gun);Check(gun.Snapshot.Phase==ReloadPhase.Ready&&gun.ChamberMask==127,"support upward .05 closes source cylinder");
        var partialPool=R23MagnumRuntime.CreatePistolReserve(1);var partial=new R23MagnumRuntime(partialPool,"partial");Fire(partial);Fire(partial);Open(partial);Load(partial);
        Check(partial.Loaded==6&&partial.Reserve==0&&partial.ChamberMask==125,"partial cylinder load fills real empty slots from finite reserve only");Close(partial);Fire(partial);
        Check(partial.ChamberMask==124&&partial.Loaded==5,"closing resets source chamber index; fire scans remaining live slots");
        int rounds=partial.Loaded;partial.SuspendControls();Step(partial);Check(partial.Loaded==rounds&&partial.Reserve==0,"holster/drop/recollect suspension preserves per-instance native ammo and chamber mask");
        var left=new R23MagnumRuntime(R23MagnumRuntime.CreatePistolReserve(2),"left");Fire(left);Open(left);Load(left,true);Close(left,true);
        Check(left.Loaded==7&&left.Reserve==1&&left.Snapshot.Phase==ReloadPhase.Ready&&R23MagnumRuntime.Offer(true).X==.18f,"left primary mirrors offer/cylinder reach, retains actual physical reload");
        var flick=new R23MagnumRuntime(R23MagnumRuntime.CreatePistolReserve(),"flick");Open(flick);Load(flick);Step(flick);Step(flick);var motion=Input();motion.PrimaryPoint=new Float3(.06f,0,0);Step(flick,.05f,motion);
        Check(flick.Snapshot.Phase==ReloadPhase.Ready,"neutral-armed player-right primary flick closes loaded cylinder");
        Open(flick);Load(flick);Step(flick,flickBlock:true);Step(flick,flickBlock:true);Step(flick,.05f,motion,flickBlock:true);
        Check(flick.Snapshot.Phase==ReloadPhase.CylinderLoaded,"raw trigger>.2 blocks flick despite being below firing threshold");
        var paused=new R23MagnumRuntime(R23MagnumRuntime.CreatePistolReserve(),"pause");Fire(paused);float cooldown=paused.Snapshot.CooldownSeconds;var unavailable=Input(true);unavailable.Paused=true;Step(paused,.25f,unavailable);
        Check(paused.Loaded==6&&paused.Snapshot.CooldownSeconds==cooldown&&!Step(paused,.25f,Input(true)),"pause freezes native time and held trigger cannot fire on resume");
        unavailable=Input(true);unavailable.PrimaryTracked=false;Step(paused,.1f,unavailable);Check(!Step(paused,.25f,Input(true))&&paused.Loaded==6,"tracking loss requires trigger release without ammo grants");
        unavailable=Input(true);unavailable.PrimaryClimbing=true;Check(!Step(paused,.25f,unavailable),"primary climb reservation blocks Magnum fire");
        unavailable=Input(true);unavailable.HeadObstructed=true;Check(!Step(paused,.25f,unavailable),"actual head obstruction blocks Magnum fire");
        var shared=R23MagnumRuntime.CreatePistolReserve(4);var a=new R23MagnumRuntime(shared,"a",0);var b=new R23MagnumRuntime(shared,"b",0);Load(a);Load(b);
        Check(a.Loaded==4&&b.Loaded==0&&shared.Available==0&&b.Snapshot.Phase==ReloadPhase.CylinderOpen,"two native instances share one finite pistol pool, no negative/unlimited fallback");
        var blocked=new R23MagnumRuntime(R23MagnumRuntime.CreatePistolReserve(),"support",0);Step(blocked);var support=Input(false,true,R23MagnumRuntime.Offer(false));support.SupportReserved=true;Step(blocked,input:support);support.SupportLocal=R23MagnumRuntime.OpenPoint(false);Step(blocked,input:support);
        Check(blocked.Loaded==0&&blocked.Reserve==1200&&!blocked.ReloadHandOccupied,"support climbing/reservation blocks cartridge acquisition");
        var stick=new R23MagnumRuntime(R23MagnumRuntime.CreatePistolReserve(),"stick");Step(stick);Step(stick,y:-.8f);Check(stick.Snapshot.Phase==ReloadPhase.CylinderOpen,"source primary stick-down opens cylinder");
        var desktopGun=new R23MagnumRuntime(R23MagnumRuntime.CreatePistolReserve(),"desktop");Step(desktopGun);Step(desktopGun,desktop:true);Step(desktopGun);Step(desktopGun,desktop:true);Step(desktopGun);Step(desktopGun,desktop:true);
        Check(desktopGun.Snapshot.Phase==ReloadPhase.Ready&&desktopGun.Loaded==7&&desktopGun.Reserve==1200,"explicit desktop reload advances open/load/close without refill of live rounds");
        var world=new World();var health=new HealthService("health",0,0,false,world,world,world,60,5,0);health.Register(world.Command(),"target",-1,false);health.DrainTransitions();var damageGun=new R23MagnumRuntime(R23MagnumRuntime.CreatePistolReserve(),"damage");DamageReceipt receipt;
        Fire(damageGun);Check(damageGun.TryApplyHit(health,world.Command(),world,"target",new Float3(),new Float3(0,0,150),false,out receipt)&&receipt.Snapshot.Shield==37,"real HealthService applies source63 body damage inside180m source range");health.DrainTransitions();
        Check(!damageGun.TryApplyHit(health,world.Command(),world,"target",new Float3(),new Float3(0,0,1),false,out receipt),"one native shot cannot be applied twice");
        Fire(damageGun);Check(damageGun.TryApplyHit(health,world.Command(),world,"target",new Float3(),new Float3(0,0,1),true,out receipt)&&receipt.Snapshot.Shield==0&&receipt.Snapshot.Health==56,"real HealthService applies exact81 head damage across shield/health");health.DrainTransitions();
        world.Visible=false;Fire(damageGun);Check(!damageGun.TryApplyHit(health,world.Command(),world,"target",new Float3(),new Float3(0,0,1),true,out receipt)&&health.Read("target").Health==56,"map occlusion blocks damage without refunding consumed native shot");health.DrainTransitions();
        gun.Retire();Check(!Step(gun,input:Input(true)),"retired pre-restart owner cannot fire");var restart=new R23MagnumRuntime(R23MagnumRuntime.CreatePistolReserve(),"round2/magnum");Check(restart.Loaded==7&&restart.Reserve==1200,"explicit new-round reset creates source starter; selection never does");
#if PRODUCTION_ROUND
        var round=new R23RoundState(new[]{"dummy"},world,true);world.Visible=true;
        Check(!round.SelectWeapon("magnum"),"round cannot select an ungranted Magnum or allocate an inventory slot");
        Check(round.EnsureMagnumState("round/magnum")&&round.SelectWeapon("magnum")&&round.ActiveCapacity==7&&round.ActiveLoaded==7&&round.ActiveReserve==1200,"production RoundState explicitly allocates and selects native Magnum state");
        round.StepMagnum(.25f,new R23MagnumInput(Input()),false);round.StepMagnum(.084f,new R23MagnumInput(Input(true)),false);
        Check(round.Hit("dummy",new Float3(),new Float3(0,0,150),false,"magnum")&&round.LastTargetDamage("dummy")==63,"production RoundState source damage path accepts native Magnum shot at150m");
        Check(!round.Magnum.TryApplyHit(health,world.Command(),world,"target",new Float3(),new Float3(0,0,1),false,out receipt),"round damage consumes the runtime pending shot too");
        int remaining=round.ActiveLoaded;Check(round.EnsureMagnumState("round/magnum")&&round.ActiveLoaded==remaining,"repeated explicit grant-state lookup cannot refill Magnum");
        round.SelectWeapon("mk18");round.SelectWeapon("magnum");Check(round.ActiveLoaded==remaining&&round.ReadReserve("magnum")==1200,"round switching returns same persistent native instance/reserve");
        var prior=round.Magnum;round.Reset();Check(round.ActiveLoaded==7&&round.ActiveReserve==1200&&round.Magnum!=prior&&!Step(prior,input:Input(true)),"explicit round restart retires previous Magnum and resets granted native starter");
#endif
        var feedback=new R23WeaponFeedbackState("magnum");feedback.AcceptedShot(1,false);
        Check(Math.Abs(feedback.Distance-.039f*1.25f)<.00001f&&feedback.ReticleWidth(false)==134&&feedback.ReticleWidth(true)==88,"source Magnum pistol-scaled .039m recoil and chevron134/88px rest widths");
        feedback.Advance(.09f);Check(Math.Abs(feedback.PitchRadians-8.47f*(float)Math.PI/180*1.25f)<.00001f,"source Magnum8.47degree signed pulse peaks at90ms");
        Check(Math.Abs(R23WeaponFeedbackState.MagnumPulse(.24f))<.00001f&&Math.Abs(R23WeaponFeedbackState.MagnumPulse(.31f)+.18f)<.00001f&&R23WeaponFeedbackState.MagnumPulse(.5f)==0,"source Magnum pulse zero240ms, undershoot310ms, end500ms");
        var bloom=new R23WeaponFeedbackState("magnum");bloom.AcceptedShot(1,false);bloom.Advance(.062f);var supportedBloom=new R23WeaponFeedbackState("magnum");supportedBloom.AcceptedShot(1,true);supportedBloom.Advance(.062f);
        Check(Math.Abs(bloom.Bloom-225)<.0001f&&Math.Abs(supportedBloom.Bloom-200)<.0001f,"source Magnum chevron accepted-shot bloom225/200px at62ms");
        feedback.ClearPresentation();Check(feedback.Distance==0&&feedback.PitchRadians==0&&!feedback.Flash&&feedback.AcceptedShots==1,"Magnum holster/suppression feedback clears without resetting actual native serial");
        var pump=new R23HandsInputFrame{LeftTracked=true,RightTracked=true,LeftStickX=-.4f,LeftStickY=-.8f,RightStickX=.3f,RightStickY=.6f,ReloadPressed=true};var immutable=pump.Snapshot;pump.LeftStickY=0;
        Check(immutable.Left.StickY==-.8f&&immutable.Right.StickX==.3f&&immutable.Right.StickClick&&immutable.Right.Primary,"immutable dominant thumbstick fields preserve source stick-down and legacy ReloadPressed compatibility");
        Console.WriteLine(count+" focused source Magnum cases passed");return 0;
    }catch(Exception error){Console.WriteLine(error);return 1;}}
}
