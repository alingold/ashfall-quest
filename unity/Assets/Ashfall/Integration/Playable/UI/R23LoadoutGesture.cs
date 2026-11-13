using System;

namespace Ashfall.Integration.Playable.UI
{
    public enum R23LoadoutAction { Equip, Drop }
    public sealed class R23LoadoutIntent
    {
        public readonly string Id;
        public readonly int Hand;
        public readonly R23LoadoutAction Action;
        internal R23LoadoutIntent(string id,int hand,R23LoadoutAction action){Id=id;Hand=hand;Action=action;}
    }
    // User requested release activation and press-drag scrolling. Capture the
    // original identity, cancel clicks after a drag, and retain neutral admission.
    public sealed class R23LoadoutGesture
    {
        public const float DragSlop=.018f;
        sealed class HandState {public bool Armed,Trigger,Grip,Pending,GripOwner,Dragged,Cancelled,CanScroll,HavePoint;public string Id;public float StartX,StartY,LastY;}
        readonly HandState[] hands={new HandState(),new HandState()};
        public void Reset(){for(int i=0;i<2;i++)hands[i]=new HandState();}
        public void CancelPendingClicks(){foreach(var hand in hands)if(hand.Pending)hand.Cancelled=true;}
        public R23LoadoutIntent Sample(int hand,string hoveredId,bool trigger,bool grip,bool tracked,bool loadoutOpen)
        {float unused;return Sample(hand,hoveredId,trigger,grip,tracked,loadoutOpen,0,0,true,false,out unused);}
        public R23LoadoutIntent Sample(int hand,string hoveredId,bool trigger,bool grip,bool tracked,bool loadoutOpen,float x,float y,bool pointerValid,bool canScroll,out float scrollMetres)
        {
            if(hand<0||hand>1)throw new ArgumentOutOfRangeException("hand");
            scrollMetres=0;var state=hands[hand];
            if(!tracked||!loadoutOpen){hands[hand]=new HandState();return null;}
            pointerValid=pointerValid&&!float.IsNaN(x)&&!float.IsInfinity(x)&&!float.IsNaN(y)&&!float.IsInfinity(y);
            if(!state.Armed)
            {
                if(!trigger&&!grip)state.Armed=true;
                state.Trigger=trigger;state.Grip=grip;return null;
            }
            bool gripPress=grip&&!state.Grip,triggerPress=trigger&&!state.Trigger;
            state.Trigger=trigger;state.Grip=grip;
            if(!state.Pending)
            {
                if(!gripPress&&!triggerPress)return null;
                state.Pending=true;state.GripOwner=gripPress;state.Id=hoveredId;state.CanScroll=canScroll&&pointerValid;
                state.Cancelled=!pointerValid;state.Dragged=false;state.HavePoint=pointerValid;state.StartX=x;state.StartY=state.LastY=y;return null;
            }
            bool ownerHeld=state.GripOwner?grip:trigger;
            if(pointerValid)
            {
                float dx=x-state.StartX,dy=y-state.StartY;
                if(!state.Dragged&&dx*dx+dy*dy>=DragSlop*DragSlop){state.Dragged=true;if(state.CanScroll&&state.HavePoint)scrollMetres=dy;}
                else if(state.Dragged&&state.CanScroll&&state.HavePoint)scrollMetres=y-state.LastY;
                state.LastY=y;state.HavePoint=true;
            }
            else{state.Cancelled=true;state.HavePoint=false;}
            if(ownerHeld)return null;
            var intent=!state.Cancelled&&!state.Dragged&&state.Id!=null&&hoveredId==state.Id?new R23LoadoutIntent(state.Id,hand,state.GripOwner?R23LoadoutAction.Equip:R23LoadoutAction.Drop):null;
            state.Pending=false;state.Armed=!trigger&&!grip;return intent;
        }
    }
}
