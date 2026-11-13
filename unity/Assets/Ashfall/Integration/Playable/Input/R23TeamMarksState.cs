using System;
namespace Ashfall.Integration.Playable.Input
{
    // Pure production state: source team_marks.gd consume_state, including the
    // neutral re-arm, strict hold threshold and retained press-target selection.
    public sealed class R23TeamMarksState
    {
        public const float HoldSeconds=.10f,Lifetime=30,OuterRadius=.34f,InnerRadius=.102f,DisplayScale=1.2f;
        public bool Armed {get;private set;}
        public bool Held {get;private set;}
        public bool WheelOpen {get;private set;}
        public bool Began {get;private set;}
        public bool StickChoice {get;private set;}
        public bool ExplicitChoice {get;private set;}
        public int Selected {get;private set;}
        public int Preferred {get;private set;}
        float elapsed;
        public R23TeamMarksState(){Selected=Preferred=2;}
        public void Cancel(){Armed=false;Held=WheelOpen=Began=StickChoice=ExplicitChoice=false;elapsed=0;}
        // -1 means no release. A release returns the selected source icon index;
        // the presentation owner still requires the surface captured on press.
        public int Sample(float seconds,bool pressed,bool eligible,float x,float y)
        {
            Began=false;
            if(!eligible){Cancel();return -1;}
            if(!Armed){if(!pressed)Armed=true;return -1;}
            if(pressed&&!Held){Held=Began=true;elapsed=0;Selected=Preferred;ExplicitChoice=false;}
            if(Held&&pressed)
            {
                elapsed+=Finite(seconds)?Math.Max(0,seconds):0;WheelOpen=elapsed>HoldSeconds+.0000001f;
                StickChoice=WheelOpen&&Finite(x)&&Finite(y)&&x*x+y*y>.25f*.25f;
                if(StickChoice){Selected=Sector(x,y);ExplicitChoice=true;}
            }
            else if(Held)
            {
                int result=Selected;if(ExplicitChoice)Preferred=Selected;
                Held=WheelOpen=StickChoice=false;return result;
            }
            return -1;
        }
        public void Point(int index){if(WheelOpen&&!StickChoice&&index>=0&&index<8){Selected=index;ExplicitChoice=true;}}
        public static int Sector(float x,float y)
        {int i=(int)Math.Floor((Math.PI*.5-Math.Atan2(y,x)+Math.PI/8)/(Math.PI/4));return (i%8+8)%8;}
        public static float AngularWidth(int index){return (float)(2*Math.Tan((index==4?5:4)*Math.PI/360));}
        public static bool Paint(int index){return index>=3&&index<=5;}
        public static bool WorldLayer(int layer){return layer==0||layer==8||layer==19||layer==20;}
        public static int ColumnCount(float depth,float originalMarkY,int index)
        {
            if(!Finite(depth)||depth<=.001f||!Finite(originalMarkY))return 0;
            double width=AngularWidth(index)*depth,start=.5+width*1.25;
            return (int)Math.Max(0,Math.Min(512,Math.Floor((Math.Max(0,80-originalMarkY)-start)/(width*1.37))+1));
        }
        public static bool Finite(float x){return !float.IsNaN(x)&&!float.IsInfinity(x);}
        public static string Title(int index){return Titles[Valid(index)];}
        public static string Action(int index){return Actions[Valid(index)];}
        public static string ColorName(int index){return Names[Valid(index)];}
        public static string ColorHex(int index){return Colors[Valid(index)];}
        static int Valid(int i){if(i<0||i>=8)throw new ArgumentOutOfRangeException("index");return i;}
        static readonly string[] Titles={"HOLD","ENEMY","PING","EMOTE (LOCAL MARK)","TARGET","EMOTE 2","HELP","GO HERE"};
        static readonly string[] Actions={"hold","enemy","location","emote_local","target","emote_alt","help","go_here"};
        static readonly string[] Names={"yellow","red","blue","white","white","white","purple","green"};
        static readonly string[] Colors={"ffd338","ff4848","329dff","eeeeee","eeeeee","eeeeee","be79ed","6ce36a"};
    }
    public sealed class R23TeamMarkLifetime
    {
        public bool Active {get;private set;}public float Age {get;private set;}
        public float Remaining {get{return Active?Math.Max(0,R23TeamMarksState.Lifetime-Age):0;}}
        public void Place(){Age=0;Active=true;}
        public void Clear(){Age=0;Active=false;}
        public void Tick(float seconds){if(Active&&R23TeamMarksState.Finite(seconds))Age+=Math.Max(0,seconds);if(Age>R23TeamMarksState.Lifetime)Active=false;}
    }
}
