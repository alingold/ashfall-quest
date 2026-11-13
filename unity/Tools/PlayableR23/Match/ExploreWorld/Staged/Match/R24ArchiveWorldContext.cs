using System;
namespace Ashfall.Integration.Playable.Match
{
    // One current world-plan identity, not an actor/inventory authority. Explore
    // ignores unrelated match-clock revisions; actual match round epochs matter.
    public sealed class R24ArchiveWorldContext
    {
        public long Generation{get;private set;}public bool Active{get;private set;}
        public string MapId{get;private set;}public object GeometryToken{get;private set;}
        public object SourceFrame{get;private set;}public bool Match{get;private set;}public long MatchEpoch{get;private set;}
        public bool Update(bool active,string mapId,object geometry,object sourceFrame,bool match,long epoch,bool frameChanged=false)
        {
            if(!active){if(!Active)return false;Generation++;Active=false;MapId=null;SourceFrame=null;GeometryToken=null;return true;}
            long effective=match?epoch:0;
            bool changed=!Active||!String.Equals(MapId,mapId,StringComparison.Ordinal)||!Object.ReferenceEquals(GeometryToken,geometry)||!Object.ReferenceEquals(SourceFrame,sourceFrame)||Match!=match||MatchEpoch!=effective||frameChanged;
            if(changed){Generation++;Active=true;MapId=mapId;GeometryToken=geometry;SourceFrame=sourceFrame;Match=match;MatchEpoch=effective;}
            return changed;
        }
    }
}
