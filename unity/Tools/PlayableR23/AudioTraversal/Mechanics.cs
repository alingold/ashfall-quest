using System;
using Ashfall.Integration.Playable.AudioTraversal;
static class TraversalCases
{
    static int cases;
    static void Check(bool ok,string label){cases++;if(!ok)throw new Exception(label);}
    static bool Near(float a,float b){return Math.Abs(a-b)<.00001f;}
    static R23TraversalSample Walk(){return new R23TraversalSample{Enabled=true,Grounded=true,HorizontalSpeed=4};}
    static int Count(R23TraversalState state,R23TraversalSample sample,float seconds,int frames){int count=0;for(int i=0;i<frames;i++)if(state.Tick(sample,seconds).Footfall!=R23Footfall.None)count++;return count;}
    static void Main()
    {
        var state=new R23TraversalState();var sample=Walk();
        Check(Count(state,sample,.01f,47)==0,"walk before .48");Check(state.Tick(sample,.01f).Footfall==R23Footfall.Walk,"walk at .48");
        Check(Count(state,sample,.01f,47)==0,"no epsilon catch-up second step");Check(state.Tick(sample,.01f).Footfall==R23Footfall.Walk,"second walk at .96");
        state.Reset();sample.Sprinting=true;Check(Count(state,sample,.01f,32)==0,"sprint before .33");Check(state.Tick(sample,.01f).Footfall==R23Footfall.Sprint,"sprint at .33");
        state.Reset();sample=Walk();sample.HorizontalSpeed=1000;Check(Count(state,sample,.01f,480)==10,"source high walk gain remains cadence bounded");
        state.Reset();Check(state.Tick(sample,10).Footfall==R23Footfall.Walk,"stall emits one event");Check(state.Tick(sample,.001f).Footfall==R23Footfall.None,"no catch-up burst");
        state.Reset();sample=Walk();sample.HorizontalSpeed=.35f;Check(Count(state,sample,.1f,10)==0,"stationary threshold silent");
        sample=Walk();sample.Crouched=true;Check(Count(state,sample,.1f,10)==0,"source crouched walking silent");
        sample=Walk();sample.Climbing=true;Check(Count(state,sample,.1f,10)==0,"climbing has no footsteps");
        sample=Walk();sample.Gliding=true;Check(Count(state,sample,.1f,10)==0,"gliding has no footsteps");
        state.Reset();sample=Walk();sample.Grounded=false;sample.VerticalSpeed=-3;state.Tick(sample,.01f);sample.Grounded=true;sample.VerticalSpeed=0;
        Check(state.Tick(sample,.01f).Footfall==R23Footfall.Landing,"accepted support transition after real descent");Check(state.Tick(sample,.01f).Footfall==R23Footfall.None,"landing once");
        foreach(float impact in new float[]{-2.8f,-1,0,4}){state.Reset();sample=Walk();sample.Grounded=false;sample.VerticalSpeed=impact;state.Tick(sample,.01f);sample.Grounded=true;sample.VerticalSpeed=0;Check(state.Tick(sample,.01f).Footfall==R23Footfall.None,"source non-impact landing silent");}
        foreach(int block in new int[]{0,1}){state.Reset();sample=Walk();sample.Grounded=false;sample.VerticalSpeed=-20;state.Tick(sample,.01f);sample.Grounded=true;sample.Crouched=block==0;sample.Climbing=block==1;Check(state.Tick(sample,.01f).Footfall==R23Footfall.None,"crouch/climb suppresses landing");}
        state.Reset();sample=Walk();Check(state.Tick(sample,.01f).Footfall==R23Footfall.None,"spawn support is no landing");
        state.Reset();sample=Walk();sample.Gliding=true;sample.Grounded=false;sample.HorizontalSpeed=10.5f;
        var output=state.Tick(sample,.1f);Check(Near(output.GlideGain,.2f)&&Near(output.GlidePitch,.91f),"glide source 2/s fade and pitch");
        state.Tick(sample,.4f);output=state.Tick(sample,.1f);Check(Near(output.GlideGain,1)&&Near(output.GlidePitch,1.15f),"glide saturated gain");
        sample.Gliding=false;output=state.Tick(sample,.1f);Check(Near(output.GlideGain,.8f),"release fades instead of restarting");
        sample.Gliding=true;output=state.Tick(sample,.1f);Check(Near(output.GlideGain,1),"reentry continues fade state");
        sample.HorizontalSpeed=2.5f;output=state.Tick(sample,.5f);Check(Near(output.GlideGain,0)&&Near(output.GlidePitch,.85f),"slow glide target zero");
        sample.HorizontalSpeed=6.5f;output=state.Tick(sample,.5f);Check(Near(output.GlideGain,.5f),"source speed gain midpoint");
        sample.Enabled=false;output=state.Tick(sample,.1f);Check(output.Footfall==R23Footfall.None&&output.GlideGain==0,"pause/focus suspends both clocks");
        sample=Walk();Check(Count(state,sample,.01f,47)==0,"resume has no deferred footstep");
        state.Reset();sample=Walk();sample.Grounded=false;sample.VerticalSpeed=-20;state.Tick(sample,.01f);sample.Enabled=false;state.Tick(sample,.01f);sample=Walk();Check(state.Tick(sample,.01f).Footfall==R23Footfall.None,"pause discards stale landing velocity");
        foreach(float invalid in new float[]{float.NaN,float.PositiveInfinity,0,-1})Check(state.Tick(Walk(),invalid).Footfall==R23Footfall.None,"invalid dt silent");
        sample=Walk();sample.HorizontalSpeed=float.NaN;Check(state.Tick(sample,.1f).Footfall==R23Footfall.None,"invalid motion silent");
        foreach(string key in new string[]{"METAL deck","steel support","grate"})Check(R23TraversalState.SurfaceType(key)=="metal","source metal routing");
        foreach(string key in new string[]{"wood","Timber"})Check(R23TraversalState.SurfaceType(key)=="wood","source wood routing");
        foreach(string key in new string[]{"gravel","dirt","stone"})Check(R23TraversalState.SurfaceType(key)=="gravel","source gravel routing");
        Check(R23TraversalState.SurfaceType(null)=="concrete"&&R23TraversalState.SurfaceType("map collider")=="concrete","default concrete");
        Console.WriteLine("PASS "+cases+" actual traversal state cases; CPU only.");
    }
}
