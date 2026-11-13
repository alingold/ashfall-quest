from pathlib import Path
import hashlib,json,uuid
HERE=Path(__file__).resolve().parent;ROOT=HERE.parents[4];LIVE=ROOT/'unity/Assets/Ashfall/Integration/Playable/World';STAGE=HERE/'Runtime';pins={}
def read(rel):
 p=LIVE/rel;data=p.read_bytes();key=str(p.relative_to(ROOT));sha=hashlib.sha256(data).hexdigest()
 baseline=HERE/'baseline.json'
 if baseline.exists():assert json.loads(baseline.read_text()).get(key)==sha,'Source changed; refuse to rebase silently: '+key
 pins[key]=sha;return data.decode('utf-8-sig')
def write(rel,s):(STAGE/rel).parent.mkdir(parents=True,exist_ok=True);(STAGE/rel).write_text(s,encoding='utf-8')
s=read('R23PlayableMap.cs')
s=s.replace('public int BrokenDestructibleCount', 'public bool SourceCutApplied {get{return IsLoaded&&sourceCuts!=null;}}\n        public int SourceCutOwnerCount {get{return sourceCuts==null?0:sourceCuts.NewOwners;}}\n        public int SourceCutMeshCount {get;private set;}public int SourceCutColliderCount {get;private set;}public int SourceCutOrientedMeshCount {get;private set;}public int SourceCutTriangleCount {get;private set;}\n        public int BrokenDestructibleCount',1)
s=s.replace('mesh.UploadMeshData(true);\n        }','mesh.UploadMeshData(true);\n                    if(sourceCut!=null){SourceCutMeshCount++;SourceCutTriangleCount+=indices.Length/3;if(sourceCut.Precision)SourceCutOrientedMeshCount++;}\n        }',1)
s=s.replace('collider.sharedMesh = colliderMesh;','collider.sharedMesh = colliderMesh;if(sourceCut!=null)SourceCutColliderCount++;',1)
s=s.replace('destructibles=null;towerFixtures=null;','SourceCutMeshCount=SourceCutColliderCount=SourceCutOrientedMeshCount=SourceCutTriangleCount=0;destructibles=null;towerFixtures=null;',1)

s=s.replace('private R24TowerFixtures towerFixtures;','private R24TowerFixtures towerFixtures;\n        private R24SourceCuts sourceCuts;private string sourceCutTowerHash,sourceCutArchitectureHash;')
s=s.replace('public string towerFixturesFile="";','public string towerFixturesFile="";\n            public string sourceCutsFile="",sourceCutsSha256="",sourceCutsBaseMapSha256="";')
s=s.replace('var data=JsonUtility.FromJson<R24TowerManifest>(System.Text.Encoding.UTF8.GetString(TakeResponse()));','var towerBytes=TakeResponse();sourceCutTowerHash=R24SourceCuts.Hash(towerBytes);var data=JsonUtility.FromJson<R24TowerManifest>(System.Text.Encoding.UTF8.GetString(towerBytes));')
s=s.replace('try{architecture=new R24ArchitectureMaterials(System.Text.Encoding.UTF8.GetString(TakeResponse()),MapId,manifest.chunks);}','try{var architectureBytes=TakeResponse();sourceCutArchitectureHash=R24SourceCuts.Hash(architectureBytes);architecture=new R24ArchitectureMaterials(System.Text.Encoding.UTF8.GetString(architectureBytes),MapId,manifest.chunks);}')
needle='            if(sourceArt.Failure!=null){Fail(new InvalidDataException(sourceArt.Failure));yield break;}'
s=s.replace(needle,needle+'''
            if(!String.IsNullOrEmpty(manifest.sourceCutsFile))
            {
                if(manifest.version!=1||MapId!="archive:classic-bigbox-vr"||manifest.sourceCutsFile!="classic-source-cuts-v003.bytes"||towerFixtures==null||destructibles==null){Fail(new InvalidDataException("Source cut manifest binding"));yield break;}
                yield return Fetch(manifest.sourceCutsFile);
                try
                {
                    var cutBytes=TakeResponse();if(R24SourceCuts.Hash(cutBytes)!=manifest.sourceCutsSha256)throw new InvalidDataException("Source cut payload checksum");
                    var keys=new string[manifest.materials.Length];for(int i=0;i<keys.Length;i++)keys[i]=manifest.materials[i].name;
                    sourceCuts=new R24SourceCuts(cutBytes,MapId,manifest.sourceCutsBaseMapSha256,sourceCutTowerHash,sourceCutArchitectureHash,sourceArt.SourceBindingSha256,keys,destructibles.Count,manifest.chunks);
                    var old=destructibles.Definitions;var owners=manifest.chunkPanelIds;if(owners==null){owners=new int[manifest.chunks];for(int i=0;i<owners.Length;i++)owners[i]=-1;}
                    destructibles=new R23ArchiveDestructibles(sourceCuts.Definitions(old),owners,manifest.chunks);
                }
                catch(Exception e){Fail(e);yield break;}
            }''',1)
s=s.replace('if(architecture!=null)architecture.VerifyGeometry(geometryReader.BaseStream);','if(architecture!=null)architecture.VerifyGeometry(geometryReader.BaseStream);\n                if(sourceCuts!=null)sourceCuts.VerifyGeometry(geometryReader.BaseStream);',1)
needle='                    if(patch==null)BuildOwnerPart(chunk,material,collisionFlags,visible,positions,normals,uv,colors,indices,indices,sourcePositions,sourceNormals,-1,false);'
replacement='''                    bool cuts=sourceCuts!=null&&sourceCuts.VerifyChunk(chunk,r.BaseStream,vertexStart,indexStart,r.BaseStream.Position,material,collisionFlags,visible,nv,ni);
                    int[] cutStatic=cuts?sourceCuts.KeepStatic(chunk,indices,patch==null?null:patch.staticTriangleRanges):null;
                    if(patch==null)BuildOwnerPart(chunk,material,collisionFlags,visible,positions,normals,uv,colors,indices,cutStatic??indices,sourcePositions,sourceNormals,-1,false);''';assert needle in s;s=s.replace(needle,replacement,1)
s=s.replace('indices,partitions[0],sourcePositions','indices,cutStatic??partitions[0],sourcePositions',1)
needle='''                    }
        }
        void BuildOwnerPart'''
repl='''                    }
                    if(cuts)
                    {
                        var original=new float[nv*12];for(int v=0;v<nv;v++){int at=v*12;original[at]=positions[v].x;original[at+1]=positions[v].y;original[at+2]=positions[v].z;original[at+3]=normals[v].x;original[at+4]=normals[v].y;original[at+5]=normals[v].z;original[at+6]=uv[v].x;original[at+7]=uv[v].y;original[at+8]=colors[v].r;original[at+9]=colors[v].g;original[at+10]=colors[v].b;original[at+11]=colors[v].a;}
                        foreach(var part in sourceCuts.Assemble(chunk,original,indices))BuildSourceCutPart(chunk,material,collisionFlags,visible,indices,part);
                    }
        }
        void BuildOwnerPart''';assert needle in s;s=s.replace(needle,repl,1)
needle='        void BuildMeshPart(int chunk,'
method='''        void BuildSourceCutPart(int chunk,int material,int flags,bool visible,int[] original,R24SourceCutMesh part)
        {
            int count=part.Vertices.Length/12;var p=new Vector3[count];var n=new Vector3[count];var u=new Vector2[count];var c=new Color[count];var indices=new int[count];
            for(int i=0;i<count;i++){int at=i*12;p[i]=new Vector3(part.Vertices[at],part.Vertices[at+1],part.Vertices[at+2]);n[i]=new Vector3(part.Vertices[at+3],part.Vertices[at+4],part.Vertices[at+5]);u[i]=new Vector2(part.Vertices[at+6],part.Vertices[at+7]);c[i]=new Color(part.Vertices[at+8],part.Vertices[at+9],part.Vertices[at+10],part.Vertices[at+11]);indices[i]=i;}
            int[] render=indices;var draws=new List<ArtDraw>();R24SourceArtPart[] details;
            var rotation=SourceCutRotation(part);
            if(visible&&sourceArt!=null&&sourceArt.Plan!=null&&sourceArt.Plan.SelectBarycentric(chunk,original,part.SourceTriangles,part.Barycentric,out render,out details))foreach(var art in details)
            {var mesh=R24SourceArtMaterials.BuildDetail(art,p,n,c,Quaternion.Inverse(rotation));owned.Add(mesh);draws.Add(new ArtDraw{Mesh=mesh,Material=sourceArt.ForKind(art.Kind)});}
            var kinds=new byte[render.Length/3];for(int i=0;i<kinds.Length;i++)kinds[i]=part.Kinds[render[i*3]/3];
            BuildMeshPart(chunk,material,flags,visible,p,n,u,c,indices,null,null,part.Owner,kinds,render,draws.ToArray(),null,part);
        }
        static Quaternion SourceCutRotation(R24SourceCutMesh part){var q=R24SourceCuts.Rotation(part);return new Quaternion(q[0],q[1],q[2],q[3]);}
''';assert needle in s;s=s.replace(needle,method+needle,1)
s=s.replace('R24ArchitectureMaterials.Fragment[] fragments=null)\n        {','R24ArchitectureMaterials.Fragment[] fragments=null,R24SourceCutMesh sourceCut=null)\n        {',1)
needle='                    var go = new GameObject(mesh.name); go.layer = gameObject.layer; go.transform.SetParent(geometryRoot.transform, false);'
s=s.replace(needle,needle+'\n                    if(sourceCut!=null){go.transform.localPosition=Vec(sourceCut.Origin);go.transform.localRotation=SourceCutRotation(sourceCut);}',1)
needle='                        renderer.shadowCastingMode = manifest.chunkCastShadows'
s=s.replace(needle,'''                        if(sourceCut!=null){var block=new MaterialPropertyBlock();block.SetVector("_SourceObjectOffset",Vec(sourceCut.Origin));block.SetFloat("_SourceCutFrame",sourceCut.Precision?1:0);if(sourceCut.Precision)block.SetMatrix("_SourceCutLocalToMap",Matrix4x4.TRS(go.transform.localPosition,go.transform.localRotation,Vector3.one));renderer.SetPropertyBlock(block);}
'''+needle,1)
s=s.replace('var collider = collisionNode.AddComponent<MeshCollider>();collider.convex=convex; collider.sharedMesh = colliderMesh;','var collider = collisionNode.AddComponent<MeshCollider>();collider.convex=convex;if(sourceCut!=null)collider.cookingOptions=MeshColliderCookingOptions.CookForFasterSimulation|MeshColliderCookingOptions.UseFastMidphase;collider.sharedMesh = colliderMesh;',1)
s=s.replace('if(towerFixtures!=null)towerFixtures.Complete();','if(towerFixtures!=null)towerFixtures.Complete();\n            if(sourceCuts!=null)sourceCuts.Complete();',1)
s=s.replace('destructibles=null;towerFixtures=null;clearGlass=null;architecture=null;','destructibles=null;towerFixtures=null;clearGlass=null;architecture=null;sourceCuts=null;sourceCutTowerHash=sourceCutArchitectureHash=null;',1)
write('R23PlayableMap.cs',s)
s=read('SourceArt/R24SourceArtMaterials.cs');s=s.replace('public R24SourceArtPlan Plan {get;private set;}','public string SourceBindingSha256 {get;private set;}public R24SourceArtPlan Plan {get;private set;}',1)
s=s.replace('Plan=new R24SourceArtPlan(JsonUtility.FromJson<R24SourceArtData>(request.downloadHandler.text),mapId,chunks);','SourceBindingSha256=Ashfall.Integration.Playable.World.Destructibles.R24SourceCuts.Hash(request.downloadHandler.data);Plan=new R24SourceArtPlan(JsonUtility.FromJson<R24SourceArtData>(request.downloadHandler.text),mapId,chunks);',1)
s=s.replace('BuildDetail(R24SourceArtPart part,Vector3[] positions,Vector3[] normals,Color[] colors)','BuildDetail(R24SourceArtPart part,Vector3[] positions,Vector3[] normals,Color[] colors,Quaternion? sourceNormalToLocal=null)',1)
s=s.replace('c[i]=colors[at];c[i].a=1;','if(part.SourceNormals!=null&&sourceNormalToLocal.HasValue)n[i]=sourceNormalToLocal.Value*n[i];c[i]=colors[at];c[i].a=1;',1);write('SourceArt/R24SourceArtMaterials.cs',s)
s=read('SourceArt/R24SourceArtPlan.cs');needle='  static string HashSpan(Stream stream,long start,long length)'
method='''  // New cut meshes retain raw-face ordinals and weights; never infer a style
  // from remapped indices or paint the whole surviving source triangle.
  public bool SelectBarycentric(int chunk,int[] original,int[] sourceTriangles,double[] barycentric,out int[] keepIndices,out R24SourceArtPart[] details)
  {
   keepIndices=new int[sourceTriangles.Length*3];for(int i=0;i<keepIndices.Length;i++)keepIndices[i]=i;details=new R24SourceArtPart[0];R24SourceArtChunk patch;if(!chunks.TryGetValue(chunk,out patch))return false;
   if(original==null||original.Length!=patch.indices||barycentric.Length!=sourceTriangles.Length*9)throw new InvalidDataException("Source art cut layout");
   var wanted=new Dictionary<int,Face>();foreach(var part in patch.parts)for(int i=0;i<part.triangles.Length;i++){var uv=new float[9];Array.Copy(part.projected,i*9,uv,0,9);float[] normal=null;if(part.normals!=null){normal=new float[9];Array.Copy(part.normals,i*9,normal,0,9);}wanted.Add(part.triangles[i],new Face{Kind=part.kind,UV=uv,Normals=normal});}
   var keep=new List<int>();var groups=new Dictionary<string,Group>();
   for(int f=0;f<sourceTriangles.Length;f++)
   {
    Face face;if(!wanted.TryGetValue(sourceTriangles[f],out face)){keep.Add(f*3);keep.Add(f*3+1);keep.Add(f*3+2);continue;}Group g;if(!groups.TryGetValue(face.Kind,out g)){g=new Group();groups.Add(face.Kind,g);}
    for(int corner=0;corner<3;corner++){g.Indices.Add(f*3+corner);for(int axis=0;axis<3;axis++){double value=0,normal=0;for(int k=0;k<3;k++){double w=barycentric[f*9+corner*3+k];value+=face.UV[k*3+axis]*w;if(face.Normals!=null)normal+=face.Normals[k*3+axis]*w;}g.UV.Add((float)value);if(face.Normals!=null)g.Normals.Add((float)normal);}}
   }
   keepIndices=keep.ToArray();var result=new List<R24SourceArtPart>();foreach(var pair in groups){var g=pair.Value;if(g.Normals.Count!=0&&g.Normals.Count!=g.UV.Count)throw new InvalidDataException("Source art mixed cut normal provenance");result.Add(new R24SourceArtPart(pair.Key,g.Indices.ToArray(),g.UV.ToArray(),g.Normals.Count==0?null:g.Normals.ToArray()));}details=result.ToArray();return details.Length>0;
  }
''';assert needle in s;s=s.replace(needle,method+needle,1);write('SourceArt/R24SourceArtPlan.cs',s)
s=read('Resources/R24Architecture.shader');s=s.replace('  _VertexSrgb("Original vertex colour sRGB",Float)=0','  _VertexSrgb("Original vertex colour sRGB",Float)=0\n  _SourceObjectOffset("Cut local source offset",Vector)=(0,0,0,0) _SourceCutFrame("Cut oriented source frame",Float)=0',1)
s=s.replace('float4 _SourceTint;float _Roughness,_VertexSrgb;','float4 _SourceTint;float _Roughness,_VertexSrgb;\n   float4 _SourceObjectOffset;float _SourceCutFrame;float4x4 _SourceCutLocalToMap;',1)
s=s.replace('o.sourcePosition=v.positionOS.xyz*float3(1,1,-1);','float3 sourcePosition=v.positionOS.xyz+_SourceObjectOffset.xyz;if(_SourceCutFrame>.5)sourcePosition=mul(_SourceCutLocalToMap,v.positionOS).xyz;\n    o.sourcePosition=sourcePosition*float3(1,1,-1);',1);write('Resources/R24Architecture.shader',s)
(HERE/'baseline.json').write_text(json.dumps(pins,indent=2)+'\n')
print('Staged map/SourceArt/shader candidates only; live Assets untouched')
