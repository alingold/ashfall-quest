using System;
using System.Collections.Generic;
using Ashfall.Core;
namespace Ashfall.Modules.WeaponHandling
{
    // Additive owner API: existing service-rifle counters/stages remain the only ammunition state.
    // No fields are added to WeaponHandling, so its default desktop layout/behaviour is unchanged.
    public sealed partial class SourceRifleDefinition
    {
        public readonly string Id,DisplayName,AmmoFamily;
        public readonly bool SemiAutomatic,Pistol,VrOpeningOnly,TopFeed,SourceManualControls,BoltAction,S85Insertion;
        public readonly float OfferReach,InsertReach,HandleReach,HandlePull;
        public readonly Float3 SupportPoint;public readonly float MaximumRange;
        public readonly float BodyDamage,HeadDamage;
        public readonly int Capacity,BurstSize;
        public readonly float Interval,PostBurst,ModelScale,ReloadScale;
        public readonly Float3 Offer,Insert,Charging;
        SourceRifleDefinition(string id,int capacity,int burst,float interval,float recovery,float scale,Float3 offer,Float3 insert,Float3 charging,string name=null,string ammo=null,bool? semi=null,float body=-1,float head=-1,float range=100,float reloadScale=-1,bool topFeed=false,bool boltAction=false,bool s85Insertion=false,float offerReach=.10f,float insertReach=.10f,float handleReach=.18f,float handlePull=.075f,bool? sourceManualControls=null,bool? vrOpeningOnly=null)
        {Id=id;DisplayName=name??(id=="mk18"?"MK18 / M4A1":id=="akm"?"AKM":"SPR (2x)");AmmoFamily=ammo??(id=="spr"?"sniper":"rifle");SemiAutomatic=semi??(id=="spr");BodyDamage=body>=0?body:id=="spr"?55:id=="mk18"?22:20;HeadDamage=head>=0?head:id=="spr"?92:id=="mk18"?33:30;Pistol=id=="px4";SourceManualControls=sourceManualControls??(ammo=="smg");BoltAction=boltAction;S85Insertion=s85Insertion;OfferReach=offerReach;InsertReach=insertReach;HandleReach=handleReach;HandlePull=handlePull;TopFeed=topFeed;VrOpeningOnly=vrOpeningOnly??(id=="cx4"||id=="px4"||SourceManualControls);ReloadScale=reloadScale>0?reloadScale:scale;SupportPoint=Pistol?new Float3(0,-.065f,.095f):new Float3(0,-.025f,.36f);MaximumRange=range;Capacity=capacity;BurstSize=burst;Interval=interval;PostBurst=recovery;ModelScale=scale;Offer=offer;Insert=insert;Charging=charging;}
        public static readonly SourceRifleDefinition Mk18=new SourceRifleDefinition("mk18",18,3,.08f,.24f,.92f,new Float3(0,-.278f,.15f),new Float3(0,-.18f,.15f),new Float3(-.045f,.064f,.10f),range:180);
        public static readonly SourceRifleDefinition Akm=new SourceRifleDefinition("akm",20,1,1f/11f,0,.945f,new Float3(0,-.278f,.20337826f),new Float3(0,-.18f,.20337826f),new Float3(.03773216f,.06181725f,.21453953f),range:180);
        // Rarity/catalog overwrite spr.gd setup10: first rarity is4. Source charging rest=ZERO,
        // grip is current Bolt mesh AABB centre; offer is raised by .127 physical metres.
        public static readonly SourceRifleDefinition Spr=new SourceRifleDefinition("spr",4,1,.32f,0,1.32f,new Float3(0,-.278f+.127f/1.32f,.15f),new Float3(0,-.18f,.15f),new Float3(.0001754565f,.068560276f,-.0114008505f),range:180);
        // Original INITIAL_OWNED indexes0/2; magazine/slide homes are current downloaded GLBs.
        public static readonly SourceRifleDefinition Cx4=new SourceRifleDefinition("cx4",25,1,1f/6f,0,.855f,new Float3(0,-.278f,-.0107801325f),new Float3(0,-.18f,-.0107801325f),new Float3(-.045f,.064f,.10f),"CX4","rifle",false,19,23,180);
        public static readonly SourceRifleDefinition Px4=new SourceRifleDefinition("px4",16,1,.21f,0,.7f,new Float3(0,-.278f,.091493664f),new Float3(0,-.18f,.091493664f),new Float3(0,.03602748f,.15576320f),"PX4","pistol",true,30,45,180);
        public static readonly SourceRifleDefinition Ump=new SourceRifleDefinition("ump",28,1,1f/14f,0,0.7182f,new Float3(6.497910122e-05f,-0.2268583542f,0.1539753314f),new Float3(6.497910122e-05f,-0.1062007542f,0.1539753314f),new Float3(-0.045f,0.064f,0.1f),"UMP","smg",false,19,23,180,1f,false);
        public static readonly SourceRifleDefinition P90=new SourceRifleDefinition("p90",45,1,1f/15f,0,0.72f,new Float3(1.25047113e-08f,0.1259907253f,-0.3401075027f),new Float3(1.25047113e-08f,0.05543516979f,-0.09955194718f),new Float3(-0.05f,0.064f,0.1f),"P90","smg",false,20,22,180,0.72f,true);
        public static readonly SourceRifleDefinition Mp5=new SourceRifleDefinition("mp5",21,1,1f/13f,0,0.684f,new Float3(0f,-0.278f,0.2434360492f),new Float3(0f,-0.18f,0.2434360492f),new Float3(-0.045f,0.064f,0.1f),"MP5","smg",false,22,33,180,0.684f,false);
        public static readonly SourceRifleDefinition Mp9=new SourceRifleDefinition("mp9",24,1,1f/12.5f,0,1f,new Float3(0f,-0.278f,0.03798250389f),new Float3(0f,-0.18f,0.03798250389f),new Float3(-0.045f,0.064f,0.1f),"SILENCED MP9","smg",false,21,26,180,1f,false);
        public Float3 ReloadGuide {get{return S85Insertion?new Float3(Offer.X,Offer.Y+.20f/ReloadScale,Offer.Z):Insert;}}
        public Float3 ChargingPoint(bool left){return TopFeed&&left?new Float3(-Charging.X,Charging.Y,Charging.Z):Charging;}
        public static SourceRifleDefinition ForId(string id) { var result=id=="awp"?Awp:id=="mk18"?Mk18:id=="akm"?Akm:id=="spr"?Spr:id=="cx4"?Cx4:id=="px4"?Px4:id=="ump"?Ump:id=="p90"?P90:id=="mp5"?Mp5:id=="mp9"?Mp9:null; if(result==null)ResolveCatalogProfile(id,ref result);return result; }
        public static SourceRifleDefinition[] All {get{var profiles=new List<SourceRifleDefinition>(new[]{Mk18,Akm,Spr,Cx4,Px4,Ump,P90,Mp5,Mp9,Awp});AppendCatalogProfiles(profiles);return profiles.ToArray();}}
        static partial void ResolveCatalogProfile(string id,ref SourceRifleDefinition result);
        static partial void AppendCatalogProfiles(List<SourceRifleDefinition> profiles);
        public SourceRifleDefinition WithRarity(int capacity,float body,float head){return new SourceRifleDefinition(this,capacity,body,head);}
        SourceRifleDefinition(SourceRifleDefinition source,int capacity,float body,float head){Id=source.Id;DisplayName=source.DisplayName;AmmoFamily=source.AmmoFamily;SemiAutomatic=source.SemiAutomatic;Pistol=source.Pistol;VrOpeningOnly=source.VrOpeningOnly;TopFeed=source.TopFeed;SourceManualControls=source.SourceManualControls;BoltAction=source.BoltAction;S85Insertion=source.S85Insertion;OfferReach=source.OfferReach;InsertReach=source.InsertReach;HandleReach=source.HandleReach;HandlePull=source.HandlePull;SupportPoint=source.SupportPoint;MaximumRange=source.MaximumRange;BodyDamage=body;HeadDamage=head;Capacity=capacity;BurstSize=source.BurstSize;Interval=source.Interval;PostBurst=source.PostBurst;ModelScale=source.ModelScale;ReloadScale=source.ReloadScale;Offer=source.Offer;Insert=source.Insert;Charging=source.Charging;}

    }
    public sealed partial class WeaponHandling
    {
        internal WeaponHandling(string sessionId,string actorId,long authorityEpoch,IAmmoReserve reserve,IAuthorityGate authority,SourceRifleDefinition definition)
            :this(sessionId,actorId,authorityEpoch,WeaponMechanism.ServiceRifle,reserve,authority)
        { if(definition==null)throw new ArgumentNullException("definition");capacity=definition.Capacity;interval=definition.Interval; }
        bool MatchesSourceRifle(SourceRifleDefinition definition) { return definition!=null&&mechanism==WeaponMechanism.ServiceRifle&&capacity==definition.Capacity&&interval==definition.Interval; }
        public void SuspendSourceRifleControls(SourceRifleDefinition definition) {
            if(!MatchesSourceRifle(definition)||retired)return;
            Suspend();overrun=0;revision++;
        }
        public RuleResult ApplySourceRifle(CommandContext command,HandlingAction action,HandlingInput input,SourceRifleDefinition definition,bool finalBurstRound) {
            if(!MatchesSourceRifle(definition)||FireReservation!=null||!Enum.IsDefined(typeof(HandlingAction),action))return RuleResult.Reject("source rifle unavailable");
            // Native Apply owns replay/authority/time, recoil recovery, fire and automatic empty eject.
            // Suppress only its generic gesture dispatch; source geometry follows immediately below.
            long before=revision,shotBefore=shots;bool oldGrip=previousGrip;
            bool opening=action==HandlingAction.Eject||(action==HandlingAction.ReloadStep&&phase==ReloadPhase.Ready);
            if(action==HandlingAction.Sample||opening)input.Trigger=false;
            var result=Apply(command,action==HandlingAction.ReloadStep&&!opening?HandlingAction.ReloadStep:HandlingAction.Fire,input);
            if(revision==before||!input.Enabled||input.Paused||!input.PrimaryTracked||input.PrimaryClimbing||input.HeadObstructed)return result;
            if(opening)result=Eject();
            if(!input.Desktop&&input.SupportTracked&&!input.SupportReserved)SourceRifleGesture(input,definition,oldGrip);
            if(finalBurstRound&&definition.BurstSize>1&&shots>shotBefore){cooldown=definition.PostBurst;overrun=0;}
            // Preserve the admitted CX4/PX4 cadence policy; the four SMG source
            // profiles retain actual weapon.gd automatic overrun compensation.
            if((definition.Id=="cx4"||definition.Id=="px4")&&shots>shotBefore){cooldown=definition.Interval;overrun=0;}
            if(definition.SemiAutomatic&&shots>shotBefore)releaseTrigger=true;
            if(definition.BoltAction&&shots>shotBefore&&loaded>0){seated=loaded;loaded=0;chambered=false;contact=false;SetStage(ReloadPhase.NeedsRack);releaseGrip=releaseTrigger=true;}
            if(phase!=ReloadPhase.Ready){supported=false;latched=false;}
            return action==HandlingAction.Sample?RuleResult.Accept():result;
        }
        void SourceRifleGesture(HandlingInput input,SourceRifleDefinition definition,bool oldGrip) {
            if(definition.TopFeed){SourceTopFeedGesture(input,definition,oldGrip);return;}
            if(releaseGrip)return;bool fresh=input.Grip&&!oldGrip;
            if(phase==ReloadPhase.NeedsMagazine&&fresh&&SourceRifleReach(input.SupportLocal,definition.Offer,definition.OfferReach,definition.ReloadScale))Grab(input.SupportLocal);
            else if(phase==ReloadPhase.HeldMagazine) {
                if(!input.Grip)SetStage(ReloadPhase.NeedsMagazine);
                else if(input.SupportLocal.Y-grab.Y>=.06f&&(definition.S85Insertion?SourceS85Insertion(input.SupportLocal,definition):SourceRifleReach(input.SupportLocal,definition.Insert,definition.InsertReach,definition.ReloadScale)))Insert();
            }
            else if(phase==ReloadPhase.NeedsRack) {
                if(fresh&&!contact&&SourceRifleReach(input.SupportLocal,definition.Charging,definition.HandleReach,definition.ReloadScale)){contact=true;boltGrab=input.SupportLocal;}
                if(!input.Grip){contact=false;boltTravel=0;}
                if(contact&&input.Grip){float pull=boltGrab.Z-input.SupportLocal.Z;boltTravel=Math.Max(0,Math.Min(.09f,pull));if(pull>=definition.HandlePull)Cycle();}
            }
            previousGrip=input.Grip;
        }
        static bool SourceS85Insertion(Float3 point,SourceRifleDefinition d){float x=(point.X-d.Offer.X)*d.ReloadScale,y=(point.Y-d.Offer.Y)*d.ReloadScale,z=(point.Z-d.Offer.Z)*d.ReloadScale;return y>=.045f&&y<=.40f&&x*x+z*z<=.16f*.16f;}
        void SourceTopFeedGesture(HandlingInput input,SourceRifleDefinition definition,bool oldGrip){
            if(releaseGrip)return;bool fresh=input.Grip&&!oldGrip;var handle=definition.ChargingPoint(input.LeftHanded);
            if(phase==ReloadPhase.NeedsMagazine&&fresh&&SourceRifleReach(input.SupportLocal,definition.Offer,.25f,definition.ReloadScale))Grab(input.SupportLocal);
            else if(phase==ReloadPhase.HeldMagazine){if(!input.Grip)SetStage(ReloadPhase.NeedsMagazine);else if(input.SupportLocal.Z-grab.Z>=.10f&&SourceRifleReach(input.SupportLocal,definition.Insert,.25f,definition.ReloadScale))Insert();}
            else if(phase==ReloadPhase.NeedsRack){if(fresh&&!contact&&SourceRifleReach(input.SupportLocal,handle,.12f,definition.ReloadScale)){contact=true;boltGrab=input.SupportLocal;}if(!input.Grip){contact=false;boltTravel=0;}if(contact&&input.Grip){float pull=boltGrab.Z-input.SupportLocal.Z;boltTravel=Math.Max(0,Math.Min(.09f,pull));if(pull>=definition.HandlePull)Cycle();}}
            previousGrip=input.Grip;
        }
        public static bool SourceRifleReach(Float3 point,Float3 center,float radius,float scale) {
            if(!point.IsFinite||!center.IsFinite||!Finite(radius)||!Finite(scale)||radius<=0||scale<=0)return false;
            float x=(point.X-center.X)*scale,y=(point.Y-center.Y)*scale,z=(point.Z-center.Z)*scale;
            if(x*x+y*y+z*z>.34f*.34f)return false;x=Math.Max(0,Math.Abs(x)-.035f);if(z>0)z=Math.Max(0,z-.04f);
            float reach=Math.Min(radius+.055f,.30f);return x*x+y*y+z*z<reach*reach;
        }
    }
    public sealed partial class WeaponLifetimes
    {
        public RuleResult CreateSourceRifle(CommandContext command,string instanceId,SourceRifleDefinition definition,IAmmoReserve reserve,IAuthorityGate authority,out WeaponHandling weapon) {
            weapon=null;
            if(!Valid(command,instanceId)||instanceId.Length>128||entries.ContainsKey(instanceId)||definition==null||reserve==null||authority==null)return RuleResult.Reject("invalid source rifle lifetime");
            var result=authority.Validate(command);if(!result.Accepted)return result;
            result=approval.AuthorizeNew(command,instanceId,WeaponMechanism.ServiceRifle,definition.Capacity);if(!result.Accepted)return result;
            weapon=new WeaponHandling(session,command.ActorId,command.AuthorityEpoch,reserve,authority,definition);weapon.InitializeMagazine(definition.Capacity);weapon.RecordEnvelope(command);
            entries.Add(instanceId,new Entry{Active=weapon,Actor=command.ActorId,Epoch=command.AuthorityEpoch});return RuleResult.Accept();
        }
    }
}
namespace Ashfall.Integration.Playable.HandsWeapons
{
    // weapon_rack.gd INITIAL_RESERVES.rifle=2400, MAX_RESERVE=0, shared by MK18 and AKM.
    public sealed class R23RifleReserve : Ashfall.Modules.WeaponHandling.IRevisionedAmmoReserve
    {
        int available;long revision;
        public R23RifleReserve(int initial=2400) { if(initial<0)throw new ArgumentOutOfRangeException("initial");available=initial; }
        public int Available { get { return available; } }
        public long Revision { get { return revision; } }
        public int Take(int requested) { if(requested<=0||revision==long.MaxValue)return 0;int amount=Math.Min(requested,available);if(amount>0){available-=amount;revision++;}return amount; }
        public int Refund(int requested) { if(requested<=0||revision==long.MaxValue)return 0;int amount=(int)Math.Min(requested,(long)int.MaxValue-available);if(amount>0){available+=amount;revision++;}return amount; }
    }
}
