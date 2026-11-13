using System;
using Ashfall.Integration.Playable.Input;
class Cases
{
    static int checks;
    static void Need(bool value,string label){checks++;if(!value)throw new Exception(label);}
    static void Main()
    {
        var state=new R23WorldPingState();Need(state.Remaining==0,"Initial marker hidden");
        state.Place();Need(state.Remaining==20,"Source20second placement");
        state.Tick(.5f,true);Need(state.Remaining==19.5f,"Actual seconds consume lifetime");
        state.Tick(-3,true);Need(state.Remaining==19.5f,"Negative delta does not grow lifetime");
        state.Tick(float.NaN,true);Need(state.Remaining==19.5f,"Malformed delta does not corrupt lifetime");
        state.Tick(float.PositiveInfinity,true);Need(state.Remaining==19.5f,"Infinite delta does not corrupt lifetime");
        state.Tick(0,false);Need(state.Remaining==0,"Invalid focus/pause clears pending marker");
        state.Tick(1,true);Need(state.Remaining==0,"Valid recovery does not revive stale ping");
        state.Place();state.Tick(10,true);state.Place();Need(state.Remaining==20,"Replacement refreshes one marker, does not stack lifetimes");
        state.Tick(19.999f,true);Need(state.Remaining>0&&state.Remaining<.002f,"Visible until lifetime boundary");
        state.Tick(.001f,true);Need(state.Remaining==0,"Expires at20seconds");
        state.Place();state.Tick(1000,true);Need(state.Remaining==0,"Large elapsed delta expires safely");
        state.Place();state.Clear();Need(state.Remaining==0,"Map cleanup explicit clear");
        Need(R23WorldPingState.DistanceScale(0)==.7f&&R23WorldPingState.DistanceScale(5)==.7f,"Source near scale minimum");
        Need(Math.Abs(R23WorldPingState.DistanceScale(10)-1.2f)<.00001f,"Source scale distance*.12");
        Need(R23WorldPingState.DistanceScale(50)==6&&R23WorldPingState.DistanceScale(10000)==6,"Source scale maximum");
        Need(R23WorldPingState.DistanceScale(float.NaN)==.7f,"Malformed diagnostic distance bounded");
        Need(R23WorldPingState.WorldLayer(0)&&R23WorldPingState.WorldLayer(8),"World and construction supported");
        Need(!R23WorldPingState.WorldLayer(19)&&!R23WorldPingState.WorldLayer(20),"Shot-only and player-only source geometry excluded");
        Need(!R23WorldPingState.WorldLayer(10)&&!R23WorldPingState.WorldLayer(5),"Actor/UI layers excluded");
        Need(R23WorldPingState.NormalOffset==.035f,"Actual normal offset");
        Need(R23WorldPingState.FontPixels==36&&R23WorldPingState.PixelSize==.001f,"Source marker em contract");
        Console.WriteLine("PASS "+checks+" source world-ping lifetime/scale/layer/cleanup cases");
    }
}
