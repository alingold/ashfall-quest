using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
namespace Ashfall.Integration.Playable.World.Destructibles
{
    [Serializable]public sealed class R24TowerPart
    {public int owner=0,triangles=0;public int[] triangleRanges=null;}
    [Serializable]public sealed class R24TowerChunk
    {
        public int chunk=0,material=0,collisionFlags=0,visible=0,vertices=0,indices=0;
        public string materialKey="",indexSha256="",vertexSha256="";
        public int[] staticTriangleRanges=null;public R24TowerPart[] parts=null;
    }
    [Serializable]public sealed class R24TowerManifest
    {
        public int geometryVersion=0,baseChunks=0,basePanels=0;public string baseGeometrySha256="",baseOwnershipSha256="",baseDefinitionIdsSha256="";
        public string[] baseMaterialKeys=null;public R23ArchivePanelDefinition[] definitions=null;public R24TowerChunk[] chunks=null;
    }
    // Partitions existing source triangles; never generates a face or substitutes a box.
    public sealed class R24TowerFixtures
    {
        readonly R24TowerManifest manifest;readonly Dictionary<int,R24TowerChunk> chunks=new Dictionary<int,R24TowerChunk>();readonly HashSet<int> read=new HashSet<int>();
        public int BasePanels {get{return manifest.basePanels;}}
        public int OwnerCount {get{return manifest.definitions.Length;}}
        public R24TowerFixtures(R24TowerManifest data,int count,R23ArchivePanelDefinition[] existing,int[] ownership,string[] materials)
        {
            int panels=existing==null?0:existing.Length;
            if(data==null||data.geometryVersion!=1||data.baseChunks!=count||data.basePanels!=panels||panels>4096||data.definitions==null||data.definitions.Length<1||data.definitions.Length>4096-panels||data.chunks==null||data.chunks.Length==0||data.chunks.Length>count||data.baseMaterialKeys==null||materials==null||data.baseMaterialKeys.Length!=materials.Length)throw new InvalidDataException("Source fixture owner layout changed");
            if(!String.IsNullOrEmpty(data.baseDefinitionIdsSha256)&&!String.Equals(HashDefinitionIds(existing),data.baseDefinitionIdsSha256,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Additional fixture protected owner IDs changed");
            if(ownership==null){ownership=new int[count];for(int i=0;i<count;i++)ownership[i]=-1;}
            if(ownership.Length!=count||!String.Equals(HashOwners(ownership),data.baseOwnershipSha256,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Tower fixture panel ownership changed");
            for(int i=0;i<materials.Length;i++)if(materials[i]!=data.baseMaterialKeys[i])throw new InvalidDataException("Tower fixture material order changed");
            foreach(var chunk in data.chunks)
            {
                if(chunk==null||chunk.chunk<0||chunk.chunk>=count||ownership[chunk.chunk]!=-1||chunks.ContainsKey(chunk.chunk)||chunk.material<0||chunk.material>=materials.Length||chunk.materialKey!=materials[chunk.material]||chunk.parts==null||chunk.parts.Length==0)throw new InvalidDataException("Tower fixture would steal an existing owner or material");
                chunks.Add(chunk.chunk,chunk);
            }
            manifest=data;
        }
        public R23ArchivePanelDefinition[] Definitions(R23ArchivePanelDefinition[] existing)
        {int n=existing==null?0:existing.Length;var result=new R23ArchivePanelDefinition[n+manifest.definitions.Length];if(n>0)Array.Copy(existing,result,n);Array.Copy(manifest.definitions,0,result,n,manifest.definitions.Length);return result;}
        // Compose a later source partition after all protected owner providers.
        // The existing registry and owner indices remain authoritative.
        public R23ArchivePanelDefinition[] Extend(R24TowerManifest data,R23ArchivePanelDefinition[] current,int[] ownership,string[] materials)
        {
            if(current==null||current.Length<BasePanels+OwnerCount||read.Count!=0||data==null||String.IsNullOrEmpty(data.baseDefinitionIdsSha256)||!String.Equals(data.baseGeometrySha256,manifest.baseGeometrySha256,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Additional fixture baseline changed");
            for(int i=0;i<OwnerCount;i++)if(current[BasePanels+i]==null||current[BasePanels+i].id!=manifest.definitions[i].id)throw new InvalidDataException("Protected fixture owner order changed");
            var extra=new R24TowerFixtures(data,manifest.baseChunks,current,ownership,materials);
            var definitions=extra.Definitions(current);var names=new HashSet<string>();
            foreach(var d in definitions)if(d==null||String.IsNullOrEmpty(d.id)||!names.Add(d.id))throw new InvalidDataException("Additional fixture duplicate owner");
            int offset=current.Length-BasePanels;var updates=new Dictionary<int,R24TowerChunk>();
            foreach(var entry in extra.chunks)
            {
                var incoming=entry.Value;bool[] addedStatic=StaticLayout(incoming,extra.OwnerCount),keep=addedStatic;R24TowerChunk old;
                var parts=new List<R24TowerPart>();
                if(chunks.TryGetValue(entry.Key,out old))
                {
                    if(old.material!=incoming.material||old.materialKey!=incoming.materialKey||old.collisionFlags!=incoming.collisionFlags||old.visible!=incoming.visible||old.vertices!=incoming.vertices||old.indices!=incoming.indices||!String.Equals(old.vertexSha256,incoming.vertexSha256,StringComparison.OrdinalIgnoreCase)||!String.Equals(old.indexSha256,incoming.indexSha256,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Additional fixture raw chunk changed");
                    keep=StaticLayout(old,OwnerCount);parts.AddRange(old.parts);
                    for(int i=0;i<keep.Length;i++)if(!addedStatic[i]){if(!keep[i])throw new InvalidDataException("Additional fixture would steal a protected triangle");keep[i]=false;}
                }
                foreach(var part in incoming.parts)parts.Add(new R24TowerPart{owner=offset+part.owner,triangles=part.triangles,triangleRanges=(int[])part.triangleRanges.Clone()});
                updates.Add(entry.Key,new R24TowerChunk{chunk=incoming.chunk,material=incoming.material,materialKey=incoming.materialKey,collisionFlags=incoming.collisionFlags,visible=incoming.visible,vertices=incoming.vertices,indices=incoming.indices,vertexSha256=incoming.vertexSha256,indexSha256=incoming.indexSha256,staticTriangleRanges=Ranges(keep),parts=parts.ToArray()});
            }
            var tail=new R23ArchivePanelDefinition[definitions.Length-BasePanels];Array.Copy(definitions,BasePanels,tail,0,tail.Length);
            foreach(var entry in updates)chunks[entry.Key]=entry.Value;manifest.definitions=tail;return definitions;
        }
        static bool[] StaticLayout(R24TowerChunk chunk,int owners)
        {
            if(chunk.indices<0||chunk.indices%3!=0||chunk.parts==null)throw new InvalidDataException("Additional fixture triangle layout");
            var seen=new bool[chunk.indices/3];var keep=new bool[seen.Length];Claim(chunk.staticTriangleRanges,seen,keep,true);
            foreach(var part in chunk.parts)
            {if(part==null||part.owner<0||part.owner>=owners||part.triangles<=0||Claim(part.triangleRanges,seen,keep,false)!=part.triangles)throw new InvalidDataException("Additional fixture owner layout");}
            foreach(bool found in seen)if(!found)throw new InvalidDataException("Additional fixture leaves a source hole");return keep;
        }
        static int Claim(int[] ranges,bool[] seen,bool[] keep,bool value)
        {
            if(ranges==null||ranges.Length%2!=0)throw new InvalidDataException("Additional fixture ranges");int total=0;
            for(int i=0;i<ranges.Length;i+=2)
            {
                int first=ranges[i],count=ranges[i+1];if(first<0||count<=0||first>seen.Length-count)throw new InvalidDataException("Additional fixture range outside source");
                for(int j=first;j<first+count;j++){if(seen[j])throw new InvalidDataException("Additional fixture repeated source triangle");seen[j]=true;keep[j]=value;total++;}
            }
            return total;
        }
        static int[] Ranges(bool[] keep)
        {
            var result=new List<int>();int at=0;while(at<keep.Length){if(!keep[at]){at++;continue;}int first=at;while(at<keep.Length&&keep[at])at++;result.Add(first);result.Add(at-first);}return result.ToArray();
        }
        static string HashDefinitionIds(R23ArchivePanelDefinition[] definitions)
        {
            using(var bytes=new MemoryStream())using(var writer=new BinaryWriter(bytes))
            {writer.Write(definitions==null?0:definitions.Length);if(definitions!=null)foreach(var d in definitions){if(d==null)throw new InvalidDataException("Null protected fixture definition");byte[] text=System.Text.Encoding.UTF8.GetBytes(d.id??"");writer.Write(text.Length);writer.Write(text);}writer.Flush();return HashSpan(bytes,0,bytes.Length);}
        }
        public R24TowerChunk Read(int chunk,Stream stream,long vertexStart,long indexStart,long end,int material,int flags,bool visible,int vertices,int indices)
        {
            R24TowerChunk value;if(!chunks.TryGetValue(chunk,out value))return null;
            if(read.Contains(chunk)||value.material!=material||value.collisionFlags!=flags||value.visible!=(visible?1:0)||value.vertices!=vertices||value.indices!=indices||indices%3!=0||indexStart-vertexStart!=(long)vertices*48||end-indexStart!=(long)indices*4)throw new InvalidDataException("Tower fixture native chunk header changed");
            if(!String.Equals(HashSpan(stream,vertexStart,indexStart-vertexStart),value.vertexSha256,StringComparison.OrdinalIgnoreCase)||!String.Equals(HashSpan(stream,indexStart,end-indexStart),value.indexSha256,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Tower fixture native triangle source changed");
            read.Add(chunk);return value;
        }
        public static int[][] Partition(R24TowerChunk chunk,int[] original,int owners)
        {
            if(chunk==null||original==null||chunk.parts==null||original.Length!=chunk.indices||original.Length%3!=0)throw new InvalidDataException("Tower fixture triangle layout");
            var seen=new bool[original.Length/3];var result=new int[chunk.parts.Length+1][];result[0]=Select(chunk.staticTriangleRanges,original,seen);
            for(int i=0;i<chunk.parts.Length;i++)
            {var part=chunk.parts[i];if(part==null||part.owner<0||part.owner>=owners)throw new InvalidDataException("Tower fixture owner index");result[i+1]=Select(part.triangleRanges,original,seen);if(result[i+1].Length!=part.triangles*3||part.triangles<=0)throw new InvalidDataException("Tower fixture triangle count");}
            foreach(bool found in seen)if(!found)throw new InvalidDataException("Tower partition would leave a hole");return result;
        }
        static int[] Select(int[] ranges,int[] original,bool[] seen)
        {
            if(ranges==null||ranges.Length%2!=0)throw new InvalidDataException("Tower triangle ranges");var indices=new List<int>();
            for(int i=0;i<ranges.Length;i+=2)
            {
                int first=ranges[i],count=ranges[i+1];if(first<0||count<=0||first>seen.Length-count)throw new InvalidDataException("Tower triangle range outside source");
                for(int j=first;j<first+count;j++){if(seen[j])throw new InvalidDataException("Tower triangle has two owners");seen[j]=true;indices.Add(original[j*3]);indices.Add(original[j*3+1]);indices.Add(original[j*3+2]);}
            }
            return indices.ToArray();
        }
        public void Complete(){if(read.Count!=chunks.Count)throw new InvalidDataException("Tower fixture chunks were not all consumed");}
        static string HashOwners(int[] values){using(var bytes=new MemoryStream()){using(var writer=new BinaryWriter(bytes)){foreach(int value in values)writer.Write(value);writer.Flush();return HashSpan(bytes,0,bytes.Length);}}}
        static string HashSpan(Stream stream,long start,long length)
        {
            if(start<0||length<0||start>stream.Length-length)throw new InvalidDataException("Tower source byte span");long position=stream.Position;
            try{stream.Position=start;using(var hash=SHA256.Create()){var buffer=new byte[32768];while(length>0){int n=stream.Read(buffer,0,(int)Math.Min(buffer.Length,length));if(n<=0)throw new EndOfStreamException();hash.TransformBlock(buffer,0,n,buffer,0);length-=n;}hash.TransformFinalBlock(new byte[0],0,0);return BitConverter.ToString(hash.Hash).Replace("-","").ToLowerInvariant();}}
            finally{stream.Position=position;}
        }
    }
}
