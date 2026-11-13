using System;
using Ashfall.Integration.Playable.AudioTraversal;
class ClimbCases
{
    static int checks;static void Need(bool okay,string why){checks++;if(!okay)throw new Exception(why);}
    static R23TraversalSample Pull(){return new R23TraversalSample{Enabled=true,Climbing=true,HandAnchored=true,VerticalSpeed=.2f};}
    static int Count(R23TraversalState state,R23TraversalSample sample,int ticks,float dt=.01f){int n=0;for(int i=0;i<ticks;i++){var result=state.Tick(sample,dt);if(result.ClimbContact)n++;Need(result.Footfall==R23Footfall.None,"climbing never emits footstep");}return n;}
    static void Main()
    {
        var state=new R23TraversalState();var pull=Pull();Need(Count(state,pull,54)==0,"no early climb contact");Need(state.Tick(pull,.01f).ClimbContact,"contact at .55 active seconds");Need(!state.Tick(pull,.01f).ClimbContact,"no epsilon-trigger catchup burst");
        state.Reset();Need(Count(state,pull,30)==0,"first short pull");var still=pull;still.VerticalSpeed=0;Need(Count(state,still,1000)==0,"stationary anchored hold silent");Need(Count(state,pull,24)==0&&state.Tick(pull,.01f).ClimbContact,"hold preserves active-motion progress");
        state.Reset();Count(state,pull,30);var release=still;release.HandAnchored=false;state.Tick(release,.01f);Need(Count(state,pull,54)==0&&state.Tick(pull,.01f).ClimbContact,"wall release discards partial clock");
        state.Reset();Count(state,pull,30);var pause=pull;pause.Enabled=false;Need(!state.Tick(pause,.1f).ClimbContact,"pause silent");Need(Count(state,pull,54)==0&&state.Tick(pull,.01f).ClimbContact,"pause resumes fresh clock");
        foreach(float speed in new[]{0f,.1f,.12f}){state.Reset();var sample=Pull();sample.VerticalSpeed=speed;Need(Count(state,sample,200)==0,"strict .12m/s minimum");}
        state.Reset();var diagonal=Pull();diagonal.VerticalSpeed=.1f;diagonal.HorizontalSpeed=.1f;Need(Count(state,diagonal,55)==1,"full accepted3D speed controls pull clock");
        state.Reset();var mantle=Pull();mantle.HandAnchored=false;Need(Count(state,mantle,200)==0,"automatic mantle without held anchor makes no cloth contact");
        state.Reset();Need(state.Tick(pull,12).ClimbContact&&!state.Tick(still,.01f).ClimbContact,"long stall emits at most one contact");
        foreach(float bad in new[]{float.NaN,float.PositiveInfinity,-1f,0f})Need(!state.Tick(pull,bad).ClimbContact,"invalid time silent/reset");
        state.Reset();var invalid=Pull();invalid.VerticalSpeed=float.NaN;Need(!state.Tick(invalid,.1f).ClimbContact,"invalid motion silent/reset");
        Console.WriteLine("PASS "+checks+" climbing movement-clock assertions; CPU accepted samples only, no native/HMD/audibility claim.");
    }
}
