using System;
using Ashfall.Integration.Playable.Input;
static class Cases
{
    static int count;
    static void Need(bool okay,string text){if(!okay)throw new Exception(text);count++;Console.WriteLine("PASS "+text);}
    static R23GripObjective Gun(int hand,float distance){return new R23GripObjective(R23GripObjectiveKind.Weapon,"mk18",hand,distance);}
    static R23GripObjective Supply(int hand,float distance){return new R23GripObjective(R23GripObjectiveKind.Consumable,"shaker-world",hand,distance);}
    static R23GripObjective Cap(int hand,float distance){return new R23GripObjective(R23GripObjectiveKind.Open,"open",hand,distance);}
    static void Neutral(R23ObjectiveGripArbiter a){a.Step(true,true,true,0,0,null);}
    static int Main()
    {
        var a=new R23ObjectiveGripArbiter();var conflict=new[]{Supply(1,1.4f),Gun(1,.3f)};
        Need(!a.Step(true,true,true,0,1,conflict).Valid,"Cold/recovered held grip cannot claim a source objective");
        Neutral(a);var choice=a.Step(true,true,true,0,.65f,conflict);
        Need(choice.Kind==R23GripObjectiveKind.Weapon&&choice.Hand==1,"Closer gun wins over earlier-sampled farther consumable");
        Need(a.Reserved(1)&&!a.Reserved(0),"Chosen source hand is reserved against the same wall-grab gesture");
        Need(!a.Step(true,true,true,0,1,new[]{Supply(1,.1f)}).Valid,"A held grip cannot acquire a second newly closer objective");
        a.Step(true,true,true,0,.54f,null);Need(!a.Step(true,true,true,0,1,conflict).Valid,"Partially relaxed .54 grip cannot rearm");
        a.Step(true,true,true,0,.2f,null);Need(!a.Reserved(1),"Source inclusive .2 neutral releases its claim");
        choice=a.Step(true,true,true,0,.65f,new[]{Gun(1,1.4f),Supply(1,.3f)});
        Need(choice.Kind==R23GripObjectiveKind.Consumable,"Closer actual supply wins over the weapon provider");
        Neutral(a);choice=a.Step(true,true,true,.8f,.8f,new[]{Gun(0,.8f),Supply(1,.3f)});
        Need(choice.Hand==1&&choice.Kind==R23GripObjectiveKind.Consumable,"Two simultaneous fresh hands dispatch only the nearest objective");
        Need(a.Reserved(0)&&a.Reserved(1),"Both eligible fresh source hands retain claims even though only one target dispatches");
        Need(!a.Step(true,true,true,.8f,.8f,new[]{Gun(0,.1f)}).Valid,"The losing simultaneous hand cannot claim later while still held");
        a.Step(true,true,true,0,.8f,null);Need(!a.Reserved(0)&&a.Reserved(1),"Neutral releases only its physical hand");
        choice=a.Step(true,true,true,.65f,.8f,new[]{Gun(0,.5f),Supply(1,.1f)});Need(choice.Hand==0,"A nonfresh closer hand cannot steal a fresh other-hand event");
        a.Step(true,false,true,1,0,null);Need(!a.Reserved(0),"Tracking loss clears source claims and requires a new neutral");
        Need(!a.Step(true,true,true,1,0,new[]{Gun(0,.2f)}).Valid,"Restored tracking while grip held cannot acquire");
        Neutral(a);Need(a.Step(true,true,true,.65f,0,new[]{Gun(0,2)}).Valid,"Actual source remote reach includes exactly two metres");
        Neutral(a);Need(!a.Step(true,true,true,.65f,0,new[]{Gun(0,2.001f)}).Valid&&!a.Reserved(0),"Beyond-source reach never claims or reserves");
        Neutral(a);choice=a.Step(true,true,true,0,1,new[]{Gun(1,.4f),Cap(1,.2f)});Need(choice.Kind==R23GripObjectiveKind.Open,"A nearer exposed cap beats a world pickup");
        Neutral(a);Need(a.Step(true,true,true,0,1,new[]{Cap(1,.35f)}).Valid,"Exposed cap uses source .35 metre contact reach");
        Neutral(a);Need(!a.Step(true,true,true,0,1,new[]{Cap(1,.351f)}).Valid,"Cap contact cannot borrow the two-metre world reach");
        Neutral(a);choice=a.Step(true,true,true,1,1,new[]{Gun(0,.3f),Supply(1,.3f)});Need(choice.Hand==0,"Equal distances retain source physical-left sample order");
        Neutral(a);choice=a.Step(true,true,true,0,1,new[]{Gun(1,.3f),Cap(1,.3f)});Need(choice.Kind==R23GripObjectiveKind.Weapon,"Equal target distances retain source provider order");
        a.Step(false,true,true,0,0,null);Need(!a.Step(true,true,true,0,1,conflict).Valid,"Pause/menu closure while held cannot invent a fresh input");
        Neutral(a);Need(!a.Step(true,true,true,0,1,new[]{Gun(1,float.NaN),Supply(1,float.PositiveInfinity)}).Valid,"Nonfinite provider distances cannot steal a claim");
        a.Reset();Need(!a.Reserved(0)&&!a.Reserved(1)&&!a.Step(true,true,true,1,1,conflict).Valid,"Round reset clears claims without arming already held controllers");
        Console.WriteLine(count+" source objective grip conflict/rearm/hand-ownership checks passed; native dispatch pending");return 0;
    }
}
