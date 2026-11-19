#if UNITY_6000_0_OR_NEWER
using Ashfall.Integration.Playable.Match;
using Ashfall.Modules.HealthStates;
using UnityEngine;
namespace Ashfall.Integration.Playable
{
    public sealed partial class R23PlayableLoop
    {
        R24GhostAppearance localGhostAppearance;System.Func<Collider,bool> localGhostActorFilter;
        LifeState ActualLocalLife{get{return Gameplay==null||Gameplay.LocalMatchHealth==null?LifeState.Spectator:Gameplay.LocalMatchHealth.Life;}}
        public bool MatchLocalGhost{get{return R24GhostLifePolicy.Ghost(ActualMatchRules!=null,ActualLocalLife);}}
        public bool MatchLocalMotionAllowed{get{return R24GhostLifePolicy.Motion(ActualMatchRules!=null,MatchActive,ActualLocalLife);}}
        public bool MatchLocalItemsAllowed{get{return !PlatformUnarmed&&R24GhostLifePolicy.Items(ActualMatchRules!=null,MatchActive,ActualLocalLife);}}
        public R24GhostAppearance LocalGhostAppearance{get{return localGhostAppearance;}}
        void SyncMatchGhost()
        {
            bool ghost=MatchLocalGhost;
            if(localGhostActorFilter==null&&Gameplay!=null)localGhostActorFilter=Gameplay.IsSourceActorLayer4;
            if(Rig!=null&&!Rig.SetSourceGhostMotion(ghost,localGhostActorFilter))
            {SetMatchStatus("LOCAL GHOST WAITING FOR ACTUAL SOURCE HEAD HULL");return;}
            if(localGhostAppearance==null)
            {
                if(!ghost)return;
                var node=new GameObject("Actual R13 local body and glove ghost overrides");node.transform.SetParent(transform,false);localGhostAppearance=node.AddComponent<R24GhostAppearance>();
            }
            localGhostAppearance.SetRoots(Appearance==null?null:Appearance.CurrentVisualRoot,Gameplay==null?null:Gameplay.SourceLeftGloveRoot,Gameplay==null?null:Gameplay.SourceRightGloveRoot);
            if(!localGhostAppearance.SetEnabled(ghost))SetMatchStatus(localGhostAppearance.Failure);
            if(localGhostAppearance.Active)localGhostAppearance.Refresh();
        }
        void RestoreMatchGhost()
        {if(localGhostAppearance!=null)localGhostAppearance.Restore();if(Rig!=null)Rig.SetSourceGhostMotion(false,null);}
        void DisposeMatchGhost()
        {RestoreMatchGhost();if(localGhostAppearance!=null)Destroy(localGhostAppearance.gameObject);localGhostAppearance=null;}
    }
}
#endif
