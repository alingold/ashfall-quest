using System;using System.Collections.Generic;using Ashfall.Core;using Ashfall.Modules.HealthStates;
namespace Ashfall.Integration.Playable.HandsWeapons
{
    public sealed partial class R23RoundState
    {
        RescuePose currentPaddlePose;bool havePaddlePose;readonly Dictionary<string,R24ReviveTarget> reviveContacts=new Dictionary<string,R24ReviveTarget>();
        public bool ReviveModeAvailable{get{return !PlayerHealthViewBound&&health!=null&&health.Read("player")!=null&&health.Read("player").Team>=0;}}
        public void SamplePaddleAuthority(float dt,bool active,bool tracked,bool paused,bool facing,Float3 left,Float3 right,Float3 localLeft,Float3 localRight){if(PlayerHealthViewBound){havePaddlePose=false;return;}if(!active){havePaddlePose=false;health.CancelRevive(Command());return;}AdvanceSmawWorld(dt,paused);currentPaddlePose=new RescuePose{Tracked=tracked,Paused=paused,PaddlesActive=active,Facing=facing,Left=left,Right=right,LocalLeft=localLeft,LocalRight=localRight};havePaddlePose=true;health.SamplePaddles(Command());}
        // Current local round is a free-for-all with no downed allies. Return the
        // actual finite set; never register a friendly actor to make gear appear useful.
        public bool RegisterReviveContacts(R24ReviveTarget target){if(PlayerHealthViewBound||target==null||String.IsNullOrEmpty(target.Id)||target.Id=="player"||!target.Basis.Valid||!target.LeftPad.IsFinite||!target.RightPad.IsFinite||health.Read(target.Id)==null)return false;reviveContacts[target.Id]=target;return true;}
        public R24ReviveTarget[] ReadRevivableAllies(){var result=new List<R24ReviveTarget>();if(PlayerHealthViewBound)return result.ToArray();var actor=health.Read("player");if(actor==null||actor.Team<0||actor.Life!=LifeState.Alive)return result.ToArray();foreach(var contact in reviveContacts.Values){var target=health.Read(contact.Id);if(target!=null&&target.Connected&&target.Team==actor.Team&&target.Life==LifeState.Downed)result.Add(contact);}return result.ToArray();}
        public bool TryCompleteActualRevive(string id){if(PlayerHealthViewBound||String.IsNullOrEmpty(id))return false;var target=health.Read(id);return target!=null&&health.Revive(Command(),id,target.Revision).Accepted;}
    }
}
