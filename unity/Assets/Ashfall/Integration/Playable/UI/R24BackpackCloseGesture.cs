namespace Ashfall.Integration.Playable.UI
{
    // A fresh dominant-hand primary press closes only an open backpack, then stays consumed
    // through its release. Focus/tracking loss requires a new neutral sample.
    public sealed class R24BackpackCloseGesture
    {
        bool armed,previousHeld,consumeHeld;
        public bool CloseRequested{get;private set;}
        public bool Sample(bool held,bool eligible,bool backpackOpen)
        {
            CloseRequested=false;
            if(!eligible){armed=false;previousHeld=held;consumeHeld=false;return false;}
            if(consumeHeld){previousHeld=held;if(!held){consumeHeld=false;armed=true;}return true;}
            if(!held){armed=true;previousHeld=false;return false;}
            if(armed&&!previousHeld&&backpackOpen){CloseRequested=true;consumeHeld=true;armed=false;previousHeld=true;return true;}
            previousHeld=true;return false;
        }
    }
}
