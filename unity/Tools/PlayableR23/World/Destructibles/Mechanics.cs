using System;
using Ashfall.Integration.Playable.World.Destructibles;
static class Cases
{
    static int count;
    static void Need(bool condition,string name){count++;if(!condition)throw new Exception(name);}
    static void Main()
    {
        var state=new R23ArchivePanelState(930);Need(state.Count==930&&state.BrokenCount==0,"actual authored assembly count");
        Need(state.Damage(0,.001f)&&state.IsBroken(0)&&state.BrokenCount==1,"source ignores magnitude and first accepted impact breaks");
        Need(!state.Damage(0,55)&&state.BrokenCount==1,"same retired panel cannot break twice");
        Need(!state.IsBroken(1),"unrelated source panel remains intact");
        foreach(float value in new[]{0,-1,float.NaN,float.PositiveInfinity,float.NegativeInfinity})Need(!state.Damage(1,value),"invalid impact rejected");
        Need(!state.Damage(-1,1)&&!state.Damage(930,1),"foreign panel identity rejected");
        for(int i=1;i<930;i++)Need(state.Damage(i,22),"each actual panel independently breaks");
        Need(state.BrokenCount==930,"all real assembly states retired");state.Reset();Need(state.BrokenCount==0,"restart clears retirement");
        for(int i=0;i<930;i++)Need(!state.IsBroken(i),"restart restores original panel state");
        Need(state.Damage(0,20)&&state.BrokenCount==1,"source panel can break again after restart");
        Console.WriteLine("PASS "+count+" source first-hit/reset cases; CPU only.");
    }
}
