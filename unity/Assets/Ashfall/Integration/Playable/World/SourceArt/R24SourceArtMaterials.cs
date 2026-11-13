#if UNITY_6000_0_OR_NEWER
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Rendering;
namespace Ashfall.Integration.Playable.World.SourceArt
{
 public sealed class R24SourceArtMaterials:IDisposable
 {
  readonly List<UnityEngine.Object> owned=new List<UnityEngine.Object>();readonly Dictionary<string,Material> materials=new Dictionary<string,Material>();readonly Dictionary<string,Texture2D> textures=new Dictionary<string,Texture2D>();
  public string SourceBindingSha256 {get;private set;}public R24SourceArtPlan Plan {get;private set;}public bool Ready {get;private set;}public string Failure {get;private set;}
  bool legacy; string Url(string file){string p=Application.streamingAssetsPath+"/PlayableR23/"+(legacy&&UI.R25LegacyPack.SourceArtFile(file)?"Legacy/SourceArt/":"SourceArt/")+file;return p.Contains("://")?p:new Uri(p).AbsoluteUri;}
  static bool SupportedSlug(string slug){return slug=="classic-bigbox-vr"||slug=="alfa-aglae-souza"||slug=="bigbabyjesuz-classic-kiki-80"||slug=="classic-bw-187norvis"||slug=="cont-cl-er-ssd-cjcoops"||slug=="cont-cl-ssd-cjcoops"||slug=="evolving-map-bigbox"||slug=="statmaster-classic-soulsmight"||slug=="statmaster-evolving-soulsmight";}
  public IEnumerator Load(string mapId,int chunks)
  {
   legacy=UI.R25LegacyPack.IsMap(mapId);if(Plan!=null)throw new InvalidOperationException("Source art already loaded");string slug=mapId.StartsWith("archive:",StringComparison.Ordinal)?mapId.Substring(8):mapId;if(!SupportedSlug(slug)){Ready=true;yield break;}
   using(var request=UnityWebRequest.Get(Url(slug+".json"))){request.timeout=30;yield return request.SendWebRequest();if(request.result!=UnityWebRequest.Result.Success){Failure=request.error;yield break;}SourceBindingSha256=Ashfall.Integration.Playable.World.Destructibles.R24SourceCuts.Hash(request.downloadHandler.data);Plan=new R24SourceArtPlan(JsonUtility.FromJson<R24SourceArtData>(request.downloadHandler.text),mapId,chunks);}
   foreach(string file in Plan.Textures)using(var request=UnityWebRequest.Get(Url(file))){request.timeout=30;yield return request.SendWebRequest();if(request.result!=UnityWebRequest.Result.Success){Failure=request.error;yield break;}var texture=new Texture2D(2,2,TextureFormat.RGBA32,true,false);owned.Add(texture);texture.name="Actual R19/R21 "+file;texture.wrapMode=TextureWrapMode.Clamp;texture.filterMode=FilterMode.Trilinear;texture.anisoLevel=4;if(!texture.LoadImage(request.downloadHandler.data,true)){Failure="Source art image "+file;yield break;}textures.Add(file,texture);}
   var shader=Resources.Load<Shader>("R24SourceArt");if(shader==null){Failure="R24SourceArt shader missing";yield break;}
   string[] kinds={"container_r10","van_r10","tire_r10","container_end_panels","wood_panels","van","truck","tire","wood","sign","banner"};for(int i=0;i<kinds.Length;i++){var m=new Material(shader);owned.Add(m);m.name="Source matched "+kinds[i];m.SetFloat("_Style",i);if(i>=9)m.SetTexture("_Art",textures[i==10?"northline-portrait.png":"freight.png"]);materials.Add(kinds[i],m);}if(Plan.UsesJunction){var junctionShader=Resources.Load<Shader>("R24SourceJunction");if(junctionShader==null){Failure="Actual source junction shader missing";yield break;}var junction=new Material(junctionShader);owned.Add(junction);junction.name="Actual source R17/R6 detached junction";materials.Add("junction",junction);}if(Plan.UsesClearGlass){var glassShader=Resources.Load<Shader>("R23ArchiveGlass");if(glassShader==null){Failure="Original archive clear glass shader missing";yield break;}var glass=new Material(glassShader);owned.Add(glass);glass.name="Actual source clear_glass_r10 panes";materials.Add("clear_glass",glass);}Ready=true;
  }
  public Material ForKind(string kind){Material m;if(!Ready||!materials.TryGetValue(kind,out m))throw new InvalidDataException("Source art material unavailable: "+kind);return m;}
  public static Mesh BuildDetail(R24SourceArtPart part,Vector3[] positions,Vector3[] normals,Color[] colors,Quaternion? sourceNormalToLocal=null)
  {
   if(part==null||positions==null||normals==null||colors==null||normals.Length!=positions.Length||colors.Length!=positions.Length||part.ProjectedUV3.Length!=part.VertexIndices.Length*3||(part.SourceNormals!=null&&part.SourceNormals.Length!=part.ProjectedUV3.Length))throw new InvalidDataException("Source art detail arrays");int count=part.VertexIndices.Length;var p=new Vector3[count];var n=new Vector3[count];var c=new Color[count];var uv=new Vector3[count];var ix=new int[count];for(int i=0;i<count;i++){int at=part.VertexIndices[i];if(at<0||at>=positions.Length)throw new InvalidDataException("Source art detail reference");p[i]=positions[at];n[i]=part.SourceNormals==null?normals[at]:new Vector3(part.SourceNormals[i*3],part.SourceNormals[i*3+1],part.SourceNormals[i*3+2]);if(part.SourceNormals!=null&&sourceNormalToLocal.HasValue)n[i]=sourceNormalToLocal.Value*n[i];c[i]=colors[at];c[i].a=1;uv[i]=new Vector3(part.ProjectedUV3[i*3],part.ProjectedUV3[i*3+1],part.ProjectedUV3[i*3+2]);ix[i]=i;}var mesh=new Mesh();mesh.name="Actual source art "+part.Kind;mesh.indexFormat=count>65535?IndexFormat.UInt32:IndexFormat.UInt16;mesh.vertices=p;mesh.normals=n;mesh.colors=c;mesh.SetUVs(0,new List<Vector3>(uv));mesh.triangles=ix;mesh.RecalculateBounds();return mesh;
  }
  public void Dispose(){foreach(var o in owned)if(o!=null)UnityEngine.Object.Destroy(o);owned.Clear();textures.Clear();materials.Clear();Plan=null;Ready=false;}
 }
}
#endif
