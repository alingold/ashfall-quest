using System;
using System.Collections.Generic;
using System.IO;
using System.Web.Script.Serialization;
using Ashfall.Integration.Playable.World.SourceArt;
class SourceArtCases
{
 static void Check(bool value,string why){if(!value)throw new Exception(why);}
 static int Faces(int[] keep,R24SourceArtPart[] parts){int n=keep.Length;foreach(var p in parts){Check(p.ProjectedUV3.Length==p.VertexIndices.Length*3,"per-corner source UV3");if(p.Kind.EndsWith("_r10")){Check(p.SourceNormals!=null&&p.SourceNormals.Length==p.ProjectedUV3.Length,"actual per-corner R10 source normals");for(int i=0;i<p.SourceNormals.Length;i+=3){double x=p.SourceNormals[i],y=p.SourceNormals[i+1],z=p.SourceNormals[i+2];Check(Math.Abs(x*x+y*y+z*z-1)<.0001,"normalized decoded source normal");}}else Check(p.SourceNormals==null,"later stage raw normals retained");n+=p.VertexIndices.Length;}return n;}
 static long Key(int a,int b,int c){return ((long)a<<36)|((long)b<<18)|(uint)c;}
 static void Main(string[] args)
 {
  int checkedChunks=0,detailTriangles=0,ownershipCases=0;foreach(string slug in args.Length>2?args[2].Split(','):new[]{"classic-bigbox-vr","alfa-aglae-souza"})
  {
   var serializer=new JavaScriptSerializer{MaxJsonLength=16000000};var data=serializer.Deserialize<R24SourceArtData>(File.ReadAllText(Path.Combine(args[0],slug+".json")));var plan=new R24SourceArtPlan(data,"archive:"+slug,data.chunks);string geometry=Path.Combine(args[1],slug,"geometry.bytes");using(var stream=File.OpenRead(geometry))using(var r=new BinaryReader(stream))
   {
    plan.VerifyGeometry(stream);Check(r.ReadInt32()==0x52323357&&r.ReadInt32()==data.chunks,"unchanged native geometry header");for(int chunk=0;chunk<data.chunks;chunk++)
    {
     r.ReadInt32();r.ReadInt32();r.ReadInt32();int nv=r.ReadInt32(),ni=r.ReadInt32();long start=stream.Position;stream.Position+=nv*48L;long indexStart=stream.Position;var original=new int[ni];for(int i=0;i<ni;i++)original[i]=r.ReadInt32();long end=stream.Position;if(!plan.VerifyChunk(chunk,stream,start,indexStart,end,nv,ni))continue;
     var copy=(int[])original.Clone();int[] keep;R24SourceArtPart[] parts;Check(plan.Select(chunk,original,original,out keep,out parts)&&Faces(keep,parts)==ni,"full source replacement conserves original triangles");checkedChunks++;foreach(var p in parts)detailTriangles+=p.VertexIndices.Length/3;
     var even=new List<int>();var odd=new List<int>();for(int i=0;i<ni;i+=3){var group=(i/3)%2==0?even:odd;group.Add(original[i]);group.Add(original[i+1]);group.Add(original[i+2]);}
     int detailsInOwners=0;foreach(var subset in new[]{even.ToArray(),odd.ToArray()})
     {
      plan.Select(chunk,original,subset,out keep,out parts);Check(Faces(keep,parts)==subset.Length,"tower/destructible subset conserves its own triangles");var allowed=new HashSet<long>();for(int i=0;i<subset.Length;i+=3)allowed.Add(Key(subset[i],subset[i+1],subset[i+2]));foreach(var p in parts){detailsInOwners+=p.VertexIndices.Length;for(int i=0;i<p.VertexIndices.Length;i+=3)Check(allowed.Contains(Key(p.VertexIndices[i],p.VertexIndices[i+1],p.VertexIndices[i+2])),"detail remains under exact existing render owner");}ownershipCases++;
     }
     Check(detailsInOwners==ni-keepCount(plan,chunk,original),"owner splits cover same source detail set");for(int i=0;i<ni;i++)Check(copy[i]==original[i],"collision/original indices never mutate");
    }
   }
  }
  Console.WriteLine("PASS "+checkedChunks+" real map chunk partitions, "+detailTriangles+" source-art triangles and "+ownershipCases+" owner subsets; collision/original arrays unchanged. No engine/render.");
 }
 static int keepCount(R24SourceArtPlan plan,int chunk,int[] original){int[] keep;R24SourceArtPart[] parts;plan.Select(chunk,original,original,out keep,out parts);return keep.Length;}
}
