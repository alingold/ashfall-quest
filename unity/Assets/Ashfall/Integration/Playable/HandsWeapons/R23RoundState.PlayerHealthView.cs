using System;using Ashfall.Modules.HealthStates;
namespace Ashfall.Integration.Playable.HandsWeapons
{
    // Trusted composition port. Read returns immutable presentation only;
    // mutations, when supported, must reach the actual authoritative producer.
    public interface IR23PlayerHealthView
    {HealthSnapshot Read();bool ApplyDamage(float amount,bool direct,string cause);bool ApplyFall();}
    public sealed partial class R23RoundState
    {
        IR23PlayerHealthView playerHealthView;
        public bool PlayerHealthViewBound{get{return playerHealthView!=null;}}
        public void BindPlayerHealthView(IR23PlayerHealthView view)
        {if(view==null||playerHealthView!=null)throw new InvalidOperationException("One trusted player Health view required");playerHealthView=view;SuspendActiveWeapon();}
        public bool UnbindPlayerHealthView(IR23PlayerHealthView view)
        {if(view==null||!Object.ReferenceEquals(playerHealthView,view))return false;playerHealthView=null;return true;}
    }
}
