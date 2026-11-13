using System;
using System.IO;
using System.Runtime.Serialization.Json;
using Ashfall.Core;
using Ashfall.Modules.World;
using Ashfall.Integration.WorldRuntime;

namespace Ashfall.Tools.WorldRuntime
{
    // Analytic test-only content and trust fixtures; cannot authorize source exports.
    public sealed class FixturePolicy : IWorldApproval,IWorldBuildPolicy,IRuntimeAttestation
    {
        public bool Approved=true,Attested=true,Bounds=true;
        public ContentIdentity Expected;
        public Float3 LastFeet;
        public bool IsApproved(ContentIdentity id)
        {return Approved&&id.Version=="analytic-test-only-r1"&&(Expected==null||id.PayloadSha256==Expected.PayloadSha256);}
        public bool Matches(ContentIdentity id){return Attested&&IsApproved(id);}
        public bool ApproveMaterial(ContentIdentity id,LoadedSurface s){return s.MaterialPath=="fixture-only";}
        public bool AllowsSpawn(ContentIdentity id,string mode,string team,Float3 feet){LastFeet=feet;return Bounds&&feet.Y>.1f&&feet.Y<.2f;}
    }
    public static class WorldRuntimeFixture
    {
        public static string Layer(int n){return new[]{"WorldStructure","WorldInteraction","PlayerActor","WorldDecoration"}[n-8];}
        public static MapDocument Document()
        {
            return new MapDocument {schema=1,id="analytic-world-runtime",version="analytic-test-only-r1",sourcePath="C:\\analytic-fixture.json",sourceSha256=new string('a',64),sourceRevision="test-only",approval="accepted",indoor=true,capsules=false,
                semanticsVersion="world-semantics/1",
                surfaces=new[]{
                    Surface("floor-body","structure","building",new float[]{-10,0,-10,10,0,-10,10,0,10,-10,0,10},new[]{0,2,1,0,3,2}),
                    Surface("device-body","trigger","device-owner",new float[]{-1,1,-2,1,1,-2,0,2,-2},new[]{0,1,2})},
                semantics=new[]{new SemanticRecord {sourceId="floor-body",ownerId="building",kind="structure",evidence="analytic floor"},
                    new SemanticRecord {sourceId="device-body",ownerId="device-owner",kind="interaction",evidence="authored box, independent of visible triangle",boxes=new[]{new AuthoredBox {center=new[]{0f,1f,2f},size=new[]{1f,1f,1f}}}}},
                pads=new Pad[0],spawns=new[]{new Spawn {id="spawn",mode="warmup",team="shared",position=new[]{0f,.5f,0f},forward=new[]{0f,0f,-1f}}}};
        }
        static Surface Surface(string id,string role,string owner,float[] vertices,int[] indices)
        {
            var normals=new float[vertices.Length];for(int i=1;i<normals.Length;i+=3)normals[i]=1;
            return new Surface {id=id,colliderRole=role,semanticOwner=owner,materialPath="fixture-only",texturePath="",vertices=vertices,normals=normals,indices=indices,
                uv=new float[0],color=new[]{1f,1f,1f,1f},roughness=.5f,metallic=0,matrix=new[]{1f,0,0,0,0,1f,0,0,0,0,1f,0,0,0,0,1f}};
        }
        public static byte[] Bytes(MapDocument d)
        {using(var stream=new MemoryStream()){new DataContractJsonSerializer(typeof(MapDocument)).WriteObject(stream,d);return stream.ToArray();}}
        public static ApprovedWorld Approved(FixturePolicy policy)
        {var bytes=Bytes(Document());var w=ApprovedWorld.Load(bytes,WorldLoader.Hash(bytes),policy);policy.Expected=w.Identity;return w;}
    }
}
