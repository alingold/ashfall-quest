using System;
using System.IO;
using Ashfall.Modules.World;
namespace Ashfall.Modules.MapStreaming
{
    public sealed class SourceProvenance
    {
        public readonly string MapId, Path, Revision, Sha256, ManifestSha256;
        public readonly long Length;
        internal SourceProvenance(string map,string path,string revision,string sha,string manifest,long length)
        {MapId=map;Path=path;Revision=revision;Sha256=sha;ManifestSha256=manifest;Length=length;}
    }
    // Uses the actual World boundary, never grants approval or assigns collider/material/spawn roles.
    public static class WorldMapAdapter
    {
        static void Bind(MapStream stream,ContentIdentity id)
        {
            var p=stream.Provenance;
            if(id==null||id.MapId!=p.MapId||id.SourceRevision!=p.Revision||!String.Equals(id.SourceSha256,p.Sha256,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("World/source provenance mismatch");
        }
        public static void StreamQuarantined(MapStream stream,QuarantinedWorld world,ITriangleSink sink,int batchTriangles)
        {
            if(stream==null||world==null)throw new ArgumentNullException("stream/world");
            Bind(stream,world.Identity);stream.StreamAll(sink,batchTriangles);
        }
        public static void StreamApproved(MapStream stream,ApprovedWorld world,IWorldApproval approval,ITriangleSink sink,int batchTriangles)
        {
            if(stream==null||world==null)throw new ArgumentNullException("stream/world");
            world.Revalidate(approval);Bind(stream,world.Identity);
            // OpenPilot has no approval promotion capability; even an approved sibling cannot promote it.
            if(stream.ApprovalDisposition!="accepted")throw new InvalidDataException("Pilot geometry remains quarantined; approved export/appearance contract required");
            stream.StreamAll(sink,batchTriangles);
        }
    }
}
