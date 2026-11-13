using Ashfall.Integration.ConstructionRuntime;
// External canonical lifecycle fixture, not Networking/Player integration proof.
public sealed class FixtureHostClock:IConstructionHostState
{
    public long Now,Life=1,Connection=1,Root=1,Pose=1;
    public string Authority="authority/1";public bool Active=true;
    readonly string session;
    public FixtureHostClock(string session){this.session=session;}
    public long Tick {get{return Now;}}
    public bool TryRead(string actor,out HostBuilderStamp stamp)
    {stamp=new HostBuilderStamp(session,3,actor,Authority,Life,Connection,Root,Pose,Active);return true;}
}
