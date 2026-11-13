using System;
using System.IO;
using System.Security.Cryptography;
using System.Web.Script.Serialization;
using Ashfall.Integration.Playable.World.Destructibles;
class TowerCases
{
    public sealed class MaterialRow {public string name="";}
    public sealed class MapRow {public int version=0,chunks=0;public MaterialRow[] materials=null;public int[] chunkPanelIds=null;public R23ArchivePanelDefinition[] destructibles=null;}
    static int count;static void Check(bool value,string why){count++;if(!value)throw new Exception(why);}
    static void Reject(Action action,string why){bool rejected=false;try{action();}catch(InvalidDataException){rejected=true;}Check(rejected,why);}
    static string Sha(byte[] bytes){using(var h=SHA256.Create())return BitConverter.ToString(h.ComputeHash(bytes)).Replace("-","").ToLowerInvariant();}
    static byte[] Ints(int[] values){using(var m=new MemoryStream()){using(var w=new BinaryWriter(m)){foreach(int i in values)w.Write(i);w.Flush();return m.ToArray();}}}
    static R24TowerManifest Data(int[] indices,byte[] vertices)
    {
        var definitions=new R23ArchivePanelDefinition[18];for(int i=0;i<18;i++)definitions[i]=new R23ArchivePanelDefinition{id="actual-owner-"+i};
        return new R24TowerManifest{geometryVersion=1,baseChunks=1,basePanels=0,baseMaterialKeys=new[]{"vc"},baseOwnershipSha256=Sha(Ints(new[]{-1})),definitions=definitions,chunks=new[]{new R24TowerChunk{chunk=0,material=0,materialKey="vc",collisionFlags=1,visible=1,vertices=6,indices=indices.Length,vertexSha256=Sha(vertices),indexSha256=Sha(Ints(indices)),staticTriangleRanges=new[]{0,1,3,1},parts=new[]{new R24TowerPart{owner=0,triangles=2,triangleRanges=new[]{1,2}}}}}};
    }
    static R24TowerFixtures Owner(R24TowerManifest data){return new R24TowerFixtures(data,1,null,null,new[]{"vc"});}
    static void Actual(string folder)
    {
        var json=new JavaScriptSerializer();json.MaxJsonLength=8*1024*1024;var data=json.Deserialize<R24TowerManifest>(File.ReadAllText(Path.Combine(folder,"tower-fixtures-r5.json")));var map=json.Deserialize<MapRow>(File.ReadAllText(Path.Combine(folder,"map.json")));var materials=new string[map.materials.Length];for(int i=0;i<materials.Length;i++)materials[i]=map.materials[i].name;
        var owner=new R24TowerFixtures(data,map.chunks,map.destructibles,map.chunkPanelIds,materials);int rendered=0,collision=0;var covered=new bool[18];
        using(var stream=File.OpenRead(Path.Combine(folder,"geometry.bytes")))using(var r=new BinaryReader(stream))
        {
            Check(r.ReadInt32()==0x52323357&&r.ReadInt32()==map.chunks,"Actual geometry header");
            for(int chunk=0;chunk<map.chunks;chunk++)
            {
                int material=r.ReadInt32(),flags=r.ReadInt32(),visible=r.ReadInt32(),nv=r.ReadInt32(),ni=r.ReadInt32();long start=stream.Position,index=start+nv*48L,end=index+ni*4L;stream.Position=end;
                var patch=owner.Read(chunk,stream,start,index,end,material,flags,visible!=0,nv,ni);if(patch==null)continue;stream.Position=index;var indices=new int[ni];for(int i=0;i<ni;i++)indices[i]=r.ReadInt32();var partitions=R24TowerFixtures.Partition(patch,indices,18);int total=partitions[0].Length;
                for(int i=0;i<patch.parts.Length;i++){total+=partitions[i+1].Length;covered[patch.parts[i].owner]=true;if(visible!=0)rendered+=partitions[i+1].Length/3;if(flags!=0)collision+=partitions[i+1].Length/3;}
                Check(total==ni,"Actual triangle conservation");stream.Position=end;
            }
            Check(stream.Position==stream.Length,"Actual source stream traversal");
        }
        owner.Complete();foreach(bool found in covered)Check(found,"Actual source owner missing");Check(rendered==2168&&collision==2168,"Actual tower visual/collision area partition counts");Check(owner.Definitions(map.destructibles).Length==data.basePanels+18,"Actual source owner append count");Console.WriteLine(Path.GetFileName(folder)+":18 owners/2168 original render+collision triangles; original geometry unchanged");
    }
    static void Main(string[] args)
    {
        var indices=new[]{0,1,2,3,4,5,3,2,5,0,2,5};var original=(int[])indices.Clone();var vertices=new byte[6*48];for(int i=0;i<vertices.Length;i++)vertices[i]=(byte)(i%127);var data=Data(indices,vertices);
        var owner=Owner(data);Check(owner.Definitions(null).Length==18,"Exact owner count");Reject(owner.Complete,"Incomplete source chunks accepted");
        var parts=R24TowerFixtures.Partition(data.chunks[0],indices,18);Check(parts.Length==2&&parts[0].Length==6&&parts[1].Length==6,"Static/owned partition size");
        for(int i=0;i<indices.Length;i++)Check(indices[i]==original[i],"Original triangle source mutated");
        Check(parts[0][3]==0&&parts[0][4]==2&&parts[0][5]==5&&parts[1][0]==3&&parts[1][3]==3,"Triangle winding/order changed");
        byte[] source=new byte[vertices.Length+indices.Length*4];Array.Copy(vertices,source,vertices.Length);Array.Copy(Ints(indices),0,source,vertices.Length,indices.Length*4);
        using(var stream=new MemoryStream(source)){stream.Position=7;Check(owner.Read(0,stream,0,vertices.Length,source.Length,0,1,true,6,indices.Length)!=null&&stream.Position==7,"Raw source hash changed reader position");owner.Complete();Reject(delegate{owner.Read(0,stream,0,vertices.Length,source.Length,0,1,true,6,indices.Length);},"Same native chunk consumed twice");}
        source[20]^=1;using(var stream=new MemoryStream(source))Reject(delegate{Owner(Data(indices,vertices)).Read(0,stream,0,vertices.Length,source.Length,0,1,true,6,indices.Length);},"Changed native vertices accepted");
        data=Data(indices,vertices);data.chunks[0].staticTriangleRanges=new[]{0,2,3,1};Reject(delegate{R24TowerFixtures.Partition(data.chunks[0],indices,18);},"Overlapping static and owned triangles accepted");
        data=Data(indices,vertices);data.chunks[0].staticTriangleRanges=new[]{0,1};Reject(delegate{R24TowerFixtures.Partition(data.chunks[0],indices,18);},"Missing triangle allowed a hole");
        data=Data(indices,vertices);data.chunks[0].parts[0].owner=18;Reject(delegate{R24TowerFixtures.Partition(data.chunks[0],indices,18);},"Out-of-scope owner accepted");
        data=Data(indices,vertices);Reject(delegate{new R24TowerFixtures(data,1,null,new[]{0},new[]{"vc"});},"Existing pane ownership stolen");
        Reject(delegate{new R24TowerFixtures(Data(indices,vertices),1,null,null,new[]{"brick"});},"Changed material order accepted");
        foreach(string folder in args)Actual(folder);
        Console.WriteLine("PASS "+count+" native-source partition/refusal checks; no Unity execution");
    }
}
