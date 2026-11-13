using System;
namespace Ashfall.Integration.Playable.Traversal
{
    // A held tool owns trigger only. Cancellation/equip while squeezed requires neutral.
    public sealed class R25GrappleInput
    {
        bool previous,ready;
        public bool Aiming {get;private set;}
        public bool Held {get;private set;}
        public void Cancel(){ready=false;previous=false;Aiming=Held=false;}
        public bool Step(bool equipped,float trigger)
        {
            if(!equipped||float.IsNaN(trigger)||float.IsInfinity(trigger)||trigger<0||trigger>1){Cancel();return false;}
            Aiming=true;
            if(trigger<=.2f)ready=true;
            Held=ready&&trigger>.5f;
            bool fire=Held&&!previous;previous=Held;return fire;
        }
    }
}
