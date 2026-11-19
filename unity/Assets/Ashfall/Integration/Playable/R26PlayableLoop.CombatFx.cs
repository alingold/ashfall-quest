#if UNITY_6000_0_OR_NEWER
using UnityEngine;
namespace Ashfall.Integration.Playable
{
    public sealed partial class R23PlayableLoop
    {
        public UI.R26DamageDirection IncomingDamageDirection{get;private set;}
        public bool IncomingDamageVisible{get{return Ready&&roundStarted&&!switchingMap&&Lobby!=null&&!Lobby.Visible&&Rig!=null&&!Rig.Paused&&!PageBlocksGameplay;}}
        void InitializeCombatFx(){IncomingDamageDirection=new GameObject("R26 incoming fire directions").AddComponent<UI.R26DamageDirection>();IncomingDamageDirection.transform.SetParent(transform,false);IncomingDamageDirection.Initialize(this);Gameplay.BindIncomingDirection(IncomingDamageDirection);}
    }
}
#endif
