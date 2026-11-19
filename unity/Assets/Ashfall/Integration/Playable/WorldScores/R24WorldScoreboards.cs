#if UNITY_6000_0_OR_NEWER
using System;using System.Collections;using System.Collections.Generic;
using Ashfall.Integration.Playable.World;using UnityEngine;using UnityEngine.Networking;using UnityEngine.Rendering;using UnityEngine.Rendering.Universal;
namespace Ashfall.Integration.Playable.WorldScores {
 // One source-size canvas and one manual camera render per second, shared by all
 // authored faces. No colliders, rule mutation or per-face render targets.
 public sealed class R24WorldScoreboards:MonoBehaviour {
  [Serializable]sealed class Manifest{public int version=0;public Map[] maps=null;}
  [Serializable]sealed class Map{public string mapId="",sourceSha256="";public Face[] faces=null;}
  [Serializable]sealed class Face{public string id="";public float[] vertices=null,uv=null;public int[] indices=null;}
  const int CanvasLayer=30;R23PlayableMap map;Func<R24WorldScoreState> read;Func<bool> show;Manifest data;string bound;Transform faceRoot,canvas;Camera canvasCamera;RenderTexture texture;Material display,flat,letters;Font font;Mesh rectangles;readonly List<Mesh> faces=new List<Mesh>();readonly List<TextMesh> texts=new List<TextMesh>();readonly List<Vector3> vertices=new List<Vector3>();readonly List<Color> colors=new List<Color>();readonly List<int> indices=new List<int>();int textCount;float next;bool disposed;
  public bool Ready{get{return data!=null;}}public string Failure{get;private set;}public int FaceCount{get{return faces.Count;}}public int CanvasUpdates{get;private set;}public bool Visible{get{return faceRoot!=null&&faceRoot.gameObject.activeSelf;}}public R24WorldScoreState LastState{get;private set;}public RenderTexture SharedCanvas{get{return texture;}}public string BoundMap{get{return bound;}}
  public void Initialize(R23PlayableMap actualMap,Func<R24WorldScoreState> actualState,Func<bool> worldVisible){if(map!=null||actualMap==null||actualState==null||worldVisible==null)throw new ArgumentException();map=actualMap;read=actualState;show=worldVisible;StartCoroutine(Load());}
  IEnumerator Load(){string path=Application.streamingAssetsPath+"/PlayableR23/WorldScores/boards.json";if(!path.Contains("://"))path=new Uri(path).AbsoluteUri;using(var req=UnityWebRequest.Get(path)){yield return req.SendWebRequest();if(req.result!=UnityWebRequest.Result.Success){Failure=req.error;yield break;}try{data=JsonUtility.FromJson<Manifest>(req.downloadHandler.text);if(data==null||data.version!=1||data.maps==null)throw new InvalidOperationException("Source score face manifest");CreateCanvas();}catch(Exception e){Failure=e.Message;data=null;}}}
  void CreateCanvas(){
   var shader=Resources.Load<Shader>("R24WorldScore");var ui=Resources.Load<Shader>("R23VrUi");font=Resources.Load<Font>("R23InventoryUI");if(shader==null||ui==null||font==null)throw new InvalidOperationException("Scoreboard canvas resources missing");
   texture=new RenderTexture(1024,1280,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Default);texture.name="Shared R6 world score canvas";texture.useMipMap=false;texture.filterMode=FilterMode.Bilinear;texture.Create();display=new Material(shader);display.mainTexture=texture;flat=new Material(ui);flat.renderQueue=4101;letters=new Material(ui);letters.SetFloat("_Font",1);letters.renderQueue=4102;
   canvas=new GameObject("Isolated source1024x1280 score canvas").transform;canvas.SetParent(transform,false);canvas.position=new Vector3(0,-20000,0);canvas.gameObject.layer=CanvasLayer;
   var go=new GameObject("Source board rectangles");go.layer=CanvasLayer;go.transform.SetParent(canvas,false);rectangles=new Mesh();rectangles.MarkDynamic();go.AddComponent<MeshFilter>().sharedMesh=rectangles;go.AddComponent<MeshRenderer>().sharedMaterial=flat;
   var cameraObject=new GameObject("Scoreboard1Hz raster camera");cameraObject.transform.SetParent(canvas,false);canvasCamera=cameraObject.AddComponent<Camera>();canvasCamera.enabled=false;canvasCamera.GetUniversalAdditionalCameraData().allowXRRendering=false;canvasCamera.orthographic=true;canvasCamera.orthographicSize=640;canvasCamera.aspect=.8f;canvasCamera.nearClipPlane=.1f;canvasCamera.farClipPlane=50;canvasCamera.cullingMask=1<<CanvasLayer;canvasCamera.clearFlags=CameraClearFlags.SolidColor;canvasCamera.backgroundColor=Hex("032237");canvasCamera.allowHDR=false;canvasCamera.allowMSAA=false;canvasCamera.targetTexture=texture;cameraObject.transform.localPosition=new Vector3(512,-640,-20);
  }
  void Update(){try{Present();}catch(Exception e){Failure=e.Message;ClearFaces();enabled=false;Debug.LogError("World scoreboard presentation failed: "+e);}}
  void Present(){if(!Ready||disposed)return;if(!map.IsLoaded){if(faceRoot!=null)faceRoot.gameObject.SetActive(false);return;}if(bound!=map.MapId)BindMap();bool visible=show()&&FaceCount>0;if(faceRoot!=null)faceRoot.gameObject.SetActive(visible);if(!visible||Time.unscaledTime<next)return;next=Time.unscaledTime+1;Refresh();}
  void BindMap(){ClearFaces();bound=map.MapId;if(R26WorldScoreboardFrames.TryBind(map,display,faces,out faceRoot)){next=0;return;}Map selected=null;foreach(var m in data.maps)if(m.mapId==bound){selected=m;break;}if(selected==null)return;if(selected.sourceSha256!=map.SourceSha256){Failure="Scoreboard source differs from loaded map";return;}faceRoot=new GameObject("Actual source world score faces").transform;faceRoot.SetParent(map.transform,false);
   foreach(var row in selected.faces){var mesh=new Mesh();mesh.name=row.id;if(row.vertices==null||row.uv==null||row.indices==null||row.vertices.Length%3!=0||row.uv.Length*3!=row.vertices.Length*2||row.indices.Length%3!=0)throw new InvalidOperationException("Invalid flat source score face "+row.id);var v=new Vector3[row.vertices.Length/3];var uv=new Vector2[v.Length];for(int i=0;i<v.Length;i++){v[i]=new Vector3(row.vertices[i*3],row.vertices[i*3+1],row.vertices[i*3+2]);uv[i]=new Vector2(row.uv[i*2],1-row.uv[i*2+1]);}mesh.vertices=v;mesh.uv=uv;mesh.uv2=new Vector2[v.Length];mesh.triangles=row.indices;mesh.RecalculateBounds();faces.Add(mesh);var go=new GameObject(row.id);go.transform.SetParent(faceRoot,false);go.AddComponent<MeshFilter>().sharedMesh=mesh;var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterial=display;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;}next=0;
  }
  public void Refresh(){if(!Ready||canvasCamera==null)return;LastState=read();if(LastState==null)throw new InvalidOperationException("Actual scoreboard state required");Draw(LastState);letters.mainTexture=font.material.mainTexture;canvasCamera.Render();CanvasUpdates++;}
  void Rect(float x,float y,float w,float h,Color color){int start=vertices.Count;vertices.Add(new Vector3(x,-y,0));vertices.Add(new Vector3(x,-y-h,0));vertices.Add(new Vector3(x+w,-y-h,0));vertices.Add(new Vector3(x+w,-y,0));for(int i=0;i<4;i++)colors.Add(color);indices.AddRange(new[]{start,start+2,start+1,start,start+3,start+2});}
  void Line(float x,float y,float w,float h,string color){Rect(x,y,w,h,Hex(color));}
  void Text(string value,float x,float y,int size,string color,TextAnchor anchor=TextAnchor.MiddleLeft,float maxWidth=950,bool bold=false){TextMesh t;if(textCount==texts.Count){var go=new GameObject("Score text");go.layer=CanvasLayer;go.transform.SetParent(canvas,false);t=go.AddComponent<TextMesh>();t.font=font;t.richText=false;t.characterSize=10;go.GetComponent<MeshRenderer>().sharedMaterial=letters;texts.Add(t);}else t=texts[textCount];textCount++;t.gameObject.SetActive(true);t.fontSize=size;t.fontStyle=bold?FontStyle.Bold:FontStyle.Normal;t.text=value;t.color=Hex(color);t.anchor=anchor;t.alignment=anchor==TextAnchor.MiddleCenter?TextAlignment.Center:anchor==TextAnchor.MiddleRight?TextAlignment.Right:TextAlignment.Left;t.transform.localPosition=new Vector3(x,-y,-.01f);t.transform.localScale=Vector3.one;font.RequestCharactersInTexture(value,size,t.fontStyle);float width=t.GetComponent<MeshRenderer>().localBounds.size.x;if(width>maxWidth&&width>0)t.transform.localScale=Vector3.one*(maxWidth/width);}
  void Draw(R24WorldScoreState state){
   vertices.Clear();colors.Clear();indices.Clear();textCount=0;
   Line(0,0,1024,1280,"032237");
   var players=(R24WorldScoreRow[])state.Players.Clone();
   Array.Sort(players,delegate(R24WorldScoreRow a,R24WorldScoreRow b){int kills=Nullable.Compare(b.Kills,a.Kills);if(kills!=0)return kills;int name=StringComparer.OrdinalIgnoreCase.Compare(a.Name,b.Name);return name!=0?name:StringComparer.Ordinal.Compare(a.Id,b.Id);});
   const float top=24,minimumRow=48,available=1232;
   int count=Math.Min(players.Length,(int)(available/minimumRow));float height=Mathf.Min(76,Mathf.Floor(available/Mathf.Max(1,count)));
   int size=Mathf.RoundToInt(height*.69f);float y=top;
   for(int i=0;i<count;i++,y+=height){
    var player=players[i];if(i%2==0)Line(24,y,976,height,"072b40");
    Text(String.IsNullOrEmpty(player.Name)?"Player":player.Name,48,y+height*.5f,size,"eaf5fa",TextAnchor.MiddleLeft,765);
    Text(R24WorldScoreState.Number(player.Kills),976,y+height*.5f,size,"43c6ee",TextAnchor.MiddleRight,144,true);
   }
   display.SetFloat("_ContentHeight",Mathf.Clamp((y+24)/1280f,.28f,1));
   for(int i=textCount;i<texts.Count;i++)texts[i].gameObject.SetActive(false);
   rectangles.Clear();rectangles.SetVertices(vertices);rectangles.SetColors(colors);rectangles.SetTriangles(indices,0);rectangles.RecalculateBounds();
  }
  static Color Hex(string hex){Color c;ColorUtility.TryParseHtmlString("#"+hex,out c);return c;}
  void ClearFaces(){if(faceRoot!=null)Destroy(faceRoot.gameObject);faceRoot=null;foreach(var m in faces)Destroy(m);faces.Clear();}
  void OnDestroy(){disposed=true;StopAllCoroutines();ClearFaces();if(canvas!=null)Destroy(canvas.gameObject);if(texture!=null){texture.Release();Destroy(texture);}foreach(var m in new[]{display,flat,letters})if(m!=null)Destroy(m);if(rectangles!=null)Destroy(rectangles);}
 }
}
#endif
