#if UNITY_6000_0_OR_NEWER
namespace Ashfall.Integration.Playable.HandsWeapons
{
    public sealed partial class R23HandsWeapons
    {
        public bool PlayerHealthViewBound{get{return round!=null&&round.PlayerHealthViewBound;}}
        public void BindPlayerHealthView(IR23PlayerHealthView view){if(round==null)throw new System.InvalidOperationException("Actual round required");round.BindPlayerHealthView(view);}
        public bool UnbindPlayerHealthView(IR23PlayerHealthView view){return round!=null&&round.UnbindPlayerHealthView(view);}
    }
}
#endif
