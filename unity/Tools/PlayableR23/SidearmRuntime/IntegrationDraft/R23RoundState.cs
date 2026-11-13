using System;
using System.Collections.Generic;
using Ashfall.Core;
using Ashfall.Modules.HealthStates;
using Ashfall.Integration.DesktopPlaytest.Combat;
using Ashfall.Modules.WeaponHandling;
namespace Ashfall.Integration.Playable.HandsWeapons
{
    // Local pilot lane reuses existing rifle, combat and health owners. No network authority.
    public sealed class R23RoundState : IAuthorityGate, IHealthWorld
    {
        readonly IInteractionQuery query;
        readonly string[] targets;
        readonly bool sourceLoadout;
        readonly Dictionary<string,LocalRifleSandbox> rifles=new Dictionary<string,LocalRifleSandbox>();
        string selectedRifle="mk18",magnumInstanceId;
        IRevisionedAmmoReserve pistolReserve;
        public R23MagnumRuntime Magnum {get;private set;}
        public string ActiveWeaponId {get{return sourceLoadout?selectedRifle:"service_rifle";}}
        public HandlingSnapshot ActiveSnapshot {get{return selectedRifle=="magnum"&&Magnum!=null?Magnum.Snapshot:Rifle.Snapshot;}}
        public int ActiveLoaded {get{return selectedRifle=="magnum"&&Magnum!=null?Magnum.Loaded:Rifle.LoadedRounds;}}
        public int ActiveCapacity {get{return selectedRifle=="magnum"?7:Rifle.MagazineCapacity;}}
        public int ActiveReserve {get{return selectedRifle=="magnum"&&Magnum!=null?Magnum.Reserve:Rifle.Reserve;}}
        public string ActiveInstanceId {get{return selectedRifle=="magnum"&&Magnum!=null?Magnum.InstanceId:Rifle.InstanceId;}}
        // Call only after the existing inventory owner accepts an explicit Magnum grant.
        // This allocates mechanics, not an inventory slot; repeat calls never refill.
        public bool EnsureMagnumState(string stableId){if(!sourceLoadout||string.IsNullOrEmpty(stableId)||stableId.Length>128)return false;if(Magnum!=null)return Magnum.InstanceId==stableId;magnumInstanceId=stableId;Magnum=new R23MagnumRuntime(pistolReserve,stableId);return true;}
        public void SuspendActiveWeapon(){if(selectedRifle=="magnum"&&Magnum!=null)Magnum.SuspendControls();else Rifle.SuspendControls();pendingShot=false;}
        public void Retire(){foreach(var weapon in rifles.Values)weapon.Retire();if(!sourceLoadout&&Rifle!=null)Rifle.Retire();if(Magnum!=null)Magnum.Retire();pendingShot=false;}
        HealthService health;
        long sequence;
        double now;
        bool pendingShot,resettingPractice;
        sealed class Target { public float Last,Total,ResetLeft;public bool Head; }
        readonly Dictionary<string,Target> practice=new Dictionary<string,Target>();
        public LocalRifleSandbox Rifle { get; private set; }
        public int Hits { get; private set; }
        public float Health { get { return health.Read("player").Health; } }
        public float Shield { get { return health.Read("player").Shield; } }
        public HealthSnapshot PlayerHealthState { get { return health.Read("player"); } }
        public float MaxHealth { get { return 100; } }
        public float MaxShield { get { return 100; } }
        public R23RoundState(string[] ids,IInteractionQuery interactions,bool useSourceLoadout=false) { targets=ids;query=interactions;sourceLoadout=useSourceLoadout;Reset(); }
        CommandContext Command() { return new CommandContext("r23-player","player","damage/"+(++sequence),sequence,0,true); }
        public RuleResult Validate(CommandContext c) { return c.IsAuthority&&c.SessionId=="r23-player"&&c.ActorId=="player"&&c.AuthorityEpoch==0?RuleResult.Accept():RuleResult.Reject("local player only"); }
        public double Now { get { return now; } }
        public bool InMatch { get { return !resettingPractice; } }
        public bool TeamChangesAllowed { get { return false; } }
        public bool TryGetPose(string id,out RescuePose pose) { pose=new RescuePose();return false; }
        public void Reset() {
            if(Magnum!=null)Magnum.Retire();Magnum=null;pistolReserve=R23MagnumRuntime.CreatePistolReserve();
            if(sourceLoadout){foreach(var rifle in rifles.Values)rifle.Retire();rifles.Clear();}else if(Rifle!=null)Rifle.Retire();now=0;sequence=0;Hits=0;pendingShot=false;practice.Clear();
            // The existing rifle still owns every ammunition debit and reload step.
            // Target health lives here so an individual source dummy can reset without refilling it.
            if(sourceLoadout) {
                var reserve=new R23RifleReserve();var sniperReserve=new R23RifleReserve();
                foreach(var definition in new[]{SourceRifleDefinition.Mk18,SourceRifleDefinition.Akm,SourceRifleDefinition.Spr})rifles.Add(definition.Id,new LocalRifleSandbox(new string[0],query,definition,definition.AmmoFamily=="sniper"?sniperReserve:reserve,"r23-loadout/"+definition.Id));
                if(magnumInstanceId!=null)Magnum=new R23MagnumRuntime(pistolReserve,magnumInstanceId);
                Rifle=rifles[selectedRifle=="magnum"?"mk18":selectedRifle];
            }else Rifle=new LocalRifleSandbox(new string[0],query);
            health=new HealthService("r23-player",0,0,false,this,this,query,60,5,0);
            health.Register(Command(),"player",-1,false);
            foreach(var id in targets){health.Register(Command(),id,-1,false);practice.Add(id,new Target());}
            health.DrainTransitions();
        }
        public bool Step(float dt,bool trigger,bool reload,bool paused) {
            return Step(dt,new HandlingInput{Enabled=true,PrimaryTracked=true,SupportTracked=true,Desktop=true,Trigger=trigger},reload,paused);
        }
        public bool SelectWeapon(string id) {
            if(id=="magnum"){if(!sourceLoadout||Magnum==null)return false;if(selectedRifle==id)return true;SuspendActiveWeapon();Magnum.SuspendControls();selectedRifle=id;return true;}
            LocalRifleSandbox next;if(!sourceLoadout||!rifles.TryGetValue(id,out next))return false;
            if(next==Rifle&&selectedRifle==id)return true;SuspendActiveWeapon();next.SuspendControls();Rifle=next;selectedRifle=id;pendingShot=false;return true;
        }
        public HandlingSnapshot ReadWeapon(string id) { if(id=="magnum"&&Magnum!=null)return Magnum.Snapshot; LocalRifleSandbox rifle;if(!sourceLoadout||!rifles.TryGetValue(id,out rifle))throw new ArgumentException("Unknown source weapon");return rifle.Snapshot; }
        public string WeaponInstanceId(string id) { if(id=="magnum"&&Magnum!=null)return Magnum.InstanceId; LocalRifleSandbox rifle;if(!sourceLoadout||!rifles.TryGetValue(id,out rifle))throw new ArgumentException("Unknown source weapon");return rifle.InstanceId; }
        public int ReadReserve(string id) { if(id=="magnum"&&Magnum!=null)return Magnum.Reserve; LocalRifleSandbox rifle;if(!sourceLoadout||!rifles.TryGetValue(id,out rifle))throw new ArgumentException("Unknown source weapon");return rifle.Reserve; }
        public bool SuspendWeapon(string id) { if(id=="magnum"&&Magnum!=null){Magnum.SuspendControls();pendingShot=false;return true;} LocalRifleSandbox rifle;if(!sourceLoadout||!rifles.TryGetValue(id,out rifle))return false;rifle.SuspendControls();if(rifle==Rifle)pendingShot=false;return true; }
        public bool Step(float dt,HandlingInput input,bool reload,bool paused) {
            if(selectedRifle=="magnum")return StepMagnum(dt,new R23MagnumInput(input,primaryButton:reload,triggerBlocksFlick:true),paused);
            if(float.IsNaN(dt)||float.IsInfinity(dt)||dt<0||dt>.25f)throw new ArgumentOutOfRangeException("dt");
            bool suspended=paused||Health<=0;
            if(!suspended){now+=dt;foreach(var pair in practice)if(pair.Value.ResetLeft>0){pair.Value.ResetLeft=Math.Max(0,pair.Value.ResetLeft-dt);if(pair.Value.ResetLeft==0)ResetTarget(pair.Key);}}
            pendingShot=Rifle.Step(dt,input,reload,suspended);return pendingShot;
        }
        public bool StepMagnum(float dt,R23MagnumInput frame,bool paused) {
            if(!sourceLoadout||selectedRifle!="magnum"||Magnum==null)return false;
            if(float.IsNaN(dt)||float.IsInfinity(dt)||dt<0||dt>.25f)throw new ArgumentOutOfRangeException("dt");
            bool suspended=paused||Health<=0;
            if(!suspended){now+=dt;foreach(var pair in practice)if(pair.Value.ResetLeft>0){pair.Value.ResetLeft=Math.Max(0,pair.Value.ResetLeft-dt);if(pair.Value.ResetLeft==0)ResetTarget(pair.Key);}}
            var input=frame.Mechanics;input.Paused|=suspended;
            pendingShot=Magnum.Step(dt,new R23MagnumInput(input,frame.PrimaryButton,frame.ManualClickAllowed,frame.TriggerBlocksFlick,frame.StickX,frame.StickY,frame.DesktopReload));return pendingShot;
        }
        public HealthSnapshot ReadTarget(string id) { return practice.ContainsKey(id)?health.Read(id):null; }
        public float LastTargetDamage(string id) { Target t;return practice.TryGetValue(id,out t)?t.Last:0; }
        public float TotalTargetDamage(string id) { Target t;return practice.TryGetValue(id,out t)?t.Total:0; }
        public bool LastTargetHeadshot(string id) { Target t;return practice.TryGetValue(id,out t)&&t.Head; }
        public bool ResetTarget(string id) {
            Target t;if(!practice.TryGetValue(id,out t))return false;
            var before=health.Read(id);RuleResult result;
            // Explicit offline fixture reset; never respawns the player or grants ammunition.
            resettingPractice=true;try{result=health.Spawn(Command(),id,before.Revision,100,100);}finally{resettingPractice=false;}
            if(!result.Accepted)return false;t.Last=t.Total=t.ResetLeft=0;t.Head=false;health.DrainTransitions();return true;
        }
        public bool Hit(string id,Float3 origin,Float3 point) { return Hit(id,origin,point,false,sourceLoadout?selectedRifle:"mk18"); }
        public bool Hit(string id,Float3 origin,Float3 point,bool headshot,string weaponId) {
            if(!pendingShot)return false;pendingShot=false;
            Target target;if((sourceLoadout&&weaponId!=selectedRifle)||!practice.TryGetValue(id,out target))return false;var before=health.Read(id);
            float dx=point.X-origin.X,dy=point.Y-origin.Y,dz=point.Z-origin.Z;
            float range=weaponId=="magnum"?R23MagnumRuntime.MaximumRange:100;
            if(before.Life!=LifeState.Alive||!origin.IsFinite||!point.IsFinite||dx*dx+dy*dy+dz*dz>range*range||!query.HasLineOfSight("player",id,origin,point,range))return false;
            // one_star0991.json, first rarity: MK18 22/33; AKM 20/30. No invented head multiplier.
            var definition=SourceRifleDefinition.ForId(weaponId);if(definition==null&&weaponId!="magnum")return false;float amount=weaponId=="magnum"?(headshot?R23MagnumRuntime.HeadDamage:R23MagnumRuntime.BodyDamage):(headshot?definition.HeadDamage:definition.BodyDamage);
            float shieldLoss=Math.Min(before.Shield,amount),healthLoss=Math.Min(before.Health,amount-shieldLoss);
            var c=Command();DamageReceipt receipt;
            if(weaponId=="magnum"){if(!Magnum.TryApplyHit(health,c,query,id,origin,point,headshot,out receipt))return false;}else receipt=health.ApplyDamage(c,c.CommandId,id,before.Revision,healthLoss,shieldLoss,false);
            if(!receipt.Result.Accepted)return false;
            target.Last=(before.Health-receipt.Snapshot.Health)+(before.Shield-receipt.Snapshot.Shield);target.Total+=target.Last;target.Head=headshot;target.ResetLeft=1.5f;Hits++;health.DrainTransitions();return true;
        }
        public void Damage(float amount) {
            if(float.IsNaN(amount)||float.IsInfinity(amount)||amount<=0||Health<=0)return;
            var before=health.Read("player");float shieldLoss=sourceLoadout?Math.Min(before.Shield,amount):0,healthLoss=Math.Min(before.Health,amount-shieldLoss);
            var c=Command();health.ApplyDamage(c,c.CommandId,"player",before.Revision,healthLoss,shieldLoss,false);
            health.DrainTransitions();
        }
        public bool ApplyPlayerGain(float healthGain,float shieldGain) {
            if(float.IsNaN(healthGain)||float.IsInfinity(healthGain)||float.IsNaN(shieldGain)||float.IsInfinity(shieldGain)||healthGain<0||healthGain>100||shieldGain<0||shieldGain>100||(healthGain==0&&shieldGain==0))return false;
            return health.Serialize(delegate {
                var before=health.Read("player");
                if(before==null||before.Life!=LifeState.Alive||before.Health<=0||((healthGain==0||before.Health>=100)&&(shieldGain==0||before.Shield>=100)))return false;
                var command=Command();HealthService.PreparedGain prepared;
                if(!health.PrepareGain(command,command.CommandId,"player",before.Revision,before.LifeGeneration,before.ConnectionGeneration,healthGain,shieldGain,out prepared))return false;
                if(prepared.Proposed.Outcome!=GainOutcome.Applied||!prepared.Validate()){prepared.Abort();return false;}
                var receipt=prepared.CommitValidated();health.DrainTransitions();return receipt.Outcome==GainOutcome.Applied&&(receipt.Health>0||receipt.Shield>0);
            });
        }
    }
}
