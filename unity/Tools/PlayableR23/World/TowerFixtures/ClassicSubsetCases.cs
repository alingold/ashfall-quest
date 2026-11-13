using System;
using System.IO;
using System.Collections.Generic;
using System.Web.Script.Serialization;
using Ashfall.Integration.Playable.World.Destructibles;
class ClassicSubsetCases
{
    static int checks;static void Need(bool test,string why){if(!test)throw new Exception(why);checks++;}
    static void Main(string[] args)
    {
        var serializer=new JavaScriptSerializer{MaxJsonLength=2097152};var data=serializer.Deserialize<R24TowerManifest>(File.ReadAllText(args[0]));var source=(Dictionary<string,object>)serializer.DeserializeObject(File.ReadAllText(args[0]));var sourceDefinitions=(object[])source["definitions"];var map=(Dictionary<string,object>)serializer.DeserializeObject(File.ReadAllText(args[1]));int count=(int)map["chunks"];
        var materials=(object[])map["materials"];var names=new string[materials.Length];for(int i=0;i<names.Length;i++)names[i]=(string)((Dictionary<string,object>)materials[i])["name"];
        int[] ownership=null;if(map.ContainsKey("chunkPanelIds")){var a=(object[])map["chunkPanelIds"];ownership=new int[a.Length];for(int i=0;i<a.Length;i++)ownership[i]=Convert.ToInt32(a[i]);}
        var loader=new R24TowerFixtures(data,count,new R23ArchivePanelDefinition[0],ownership,names);Need(loader.OwnerCount==337&&loader.Definitions(null).Length==337,"Actual owner loader still truncates source subset");var totals=new int[337];int patched=0;
        using(var stream=File.OpenRead(args[2]))using(var r=new BinaryReader(stream))
        {
            r.ReadInt32();Need(r.ReadInt32()==count,"Actual geometry count");
            for(int chunk=0;chunk<count;chunk++)
            {
                int material=r.ReadInt32(),flags=r.ReadInt32(),visible=r.ReadInt32(),nv=r.ReadInt32(),ni=r.ReadInt32();long vs=stream.Position,ix=vs+(long)nv*48,end=ix+(long)ni*4;stream.Position=end;
                var row=loader.Read(chunk,stream,vs,ix,end,material,flags,visible!=0,nv,ni);if(row==null)continue;patched++;stream.Position=ix;var original=new int[ni];for(int i=0;i<ni;i++)original[i]=r.ReadInt32();var parts=R24TowerFixtures.Partition(row,original,loader.OwnerCount);int emitted=0;foreach(var p in parts)emitted+=p.Length;Need(emitted==ni,"Actual partition lost or added indices");for(int i=0;i<row.parts.Length;i++)totals[row.parts[i].owner]+=parts[i+1].Length/3;stream.Position=end;
            }
            loader.Complete();Need(stream.Position==stream.Length,"Actual source geometry trailing bytes");
        }
        Need(patched==29,"Expected exact29 affected chunks");int glass=0,doors=0,triangles=0;
        for(int i=0;i<337;i++){var definition=(Dictionary<string,object>)sourceDefinitions[i];Need(totals[i]==Convert.ToInt32(definition[definition.ContainsKey("triangles")?"triangles":"sourceTriangles"]),"Source owner triangle conservation");if(i>=18){triangles+=totals[i];if(data.definitions[i].glass)glass++;else doors++;}}
        Need(glass==165&&doors==154&&triangles==5372,"Bound complete source subset differs");
        var state=new R23ArchivePanelState(337);for(int i=0;i<337;i++)Need(state.Damage(i,1)&&state.IsBroken(i)&&!state.Damage(i,1),"Real source owner first-hit/reset state");state.Reset();Need(state.BrokenCount==0,"Real owner reset");
        Console.WriteLine(checks+" actual337-owner loader/partition/index/first-hit/reset checks PASS; native collider/render still pending");
    }
}
