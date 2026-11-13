using Ashfall.Core;
using Ashfall.Modules.Player;
namespace Ashfall.Integration.Playable
{
    // PlayerInput deliberately takes explicit bindings separately from raw hands.
    // Keep this bridge testable without an attached headset or Unity engine.
    public static class R23InputProjection
    {
        public static bool WheelHeld(HandSample left,HandSample right,int primaryHand){return primaryHand==0?left.Primary:right.Primary;}
        public static bool PingHeld(HandSample left,HandSample right,int primaryHand){return primaryHand==0?right.Secondary:left.Secondary;}
        public static DeviceSample Sample(bool focused,bool headTracked,Float3 head,HandSample left,HandSample right,bool paused,int primaryHand=1,bool reservePrimaryMarkTurn=false)
        {
            bool markHeld=reservePrimaryMarkTurn&&(primaryHand==0?left.Secondary:right.Secondary);
            return new DeviceSample{IsXr=true,Focused=focused,HeadTracked=headTracked,HeadPosition=head,
                Left=left,Right=right,MoveX=left.StickX,MoveY=left.StickY,TurnX=markHeld?0:right.StickX,TurnY=markHeld?0:right.StickY,
                Sprint=left.Click,Jump=primaryHand==1?left.Primary:right.Primary,Reload=right.Click,Ping=PingHeld(left,right,primaryHand),Paused=paused};
        }
    }
}
