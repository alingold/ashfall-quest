#if UNITY_6000_0_OR_NEWER
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
namespace Ashfall.Integration.Playable.HandsWeapons
{
    // Original ammo_panel + aim_reticle/reticle_geometry + short-lived main.gd effects.
    // All transforms created here are render children; no camera/controller/bore writes.
    internal sealed class R23GunFeedback:IDisposable
    {
        readonly List<UnityEngine.Object> owned=new List<UnityEngine.Object>();readonly Shader shader;readonly Font font;
        readonly Transform panel,face,blue,hinge,flash,reticle;readonly TextMesh loaded,reserve;
        readonly Dictionary<int,Mesh> bracketMeshes=new Dictionary<int,Mesh>();
        readonly Transform[] arms=new Transform[8],impacts=new Transform[8];readonly float[] impactTimes=new float[8];
        readonly Material loadedFont,reserveFont;readonly Mesh quad,cube;int impactCursor;string lastText="";float width=.033f;
        public string AmmoText {get{return lastText;}}public Vector3 PanelPosition {get{return panel.position;}}
        public bool PanelVisible {get{return panel.gameObject.activeInHierarchy;}}
        public bool FlashVisible {get{return flash.gameObject.activeSelf;}}public bool ReticleVisible {get{return reticle.gameObject.activeSelf;}}
        public R23GunFeedback(Transform content,Transform bore)
        {
            shader=Resources.Load<Shader>("R23GunFeedback");font=Resources.Load<Font>("R23GodotUI");if(shader==null||font==null)throw new InvalidOperationException("Source gun feedback shader/font missing");
            quad=Quad();cube=Cube();owned.Add(quad);owned.Add(cube);
            panel=Node("ammo panel",content);panel.gameObject.SetActive(false);
            var dark=Material(Hex("1b252b"),false);face=Part("ammo dark",panel,cube,dark);blue=Part("ammo blue",panel,cube,Material(Hex("008bca"),false));
            hinge=Part("ammo hinge",panel,cube,dark);hinge.gameObject.SetActive(false); // Source creates the stem but hides it.
            loadedFont=FontMaterial();reserveFont=FontMaterial();loaded=Row("loaded",panel,loadedFont,.0123f);reserve=Row("reserve",panel,reserveFont,-.0097f);
            flash=Part("muzzle flash",bore,Rod(),Material(new Color(3,1.95f,.48f),false));owned.Add(flash.GetComponent<MeshFilter>().sharedMesh);flash.gameObject.SetActive(false);
            reticle=Node("world crosshair",content);reticle.gameObject.SetActive(false);var black=Material(Color.black,true);black.renderQueue=3119;var red=Material(new Color(1,.045f,.025f),true);red.renderQueue=3120;
            for(int i=0;i<8;i++)arms[i]=Part("crosshair arm "+i,reticle,quad,i<4?black:red);
            var impactMesh=Sphere();owned.Add(impactMesh);var impactMaterial=Material(Hex("ffe5a0"),false);
            for(int i=0;i<impacts.Length;i++){impacts[i]=Part("impact "+i,content,impactMesh,impactMaterial);impacts[i].gameObject.SetActive(false);}
            // Source scope views include world impacts but exclude gun-mounted art.
            foreach(var child in panel.GetComponentsInChildren<Transform>(true))child.gameObject.layer=18;flash.gameObject.layer=18;
            Font.textureRebuilt+=RefreshFont;
        }
        public void PresentAmmo(Transform sourceModel,float modelScale,int rounds,int pool,bool left,bool visible,float lateralBase=.06f,float depth=.14f)
        {
            panel.gameObject.SetActive(visible);if(!visible)return;if(panel.parent!=sourceModel)panel.SetParent(sourceModel,false);
            string text=rounds+"\n"+R23WeaponFeedbackState.ReserveText(pool);if(lastText!=text){lastText=text;loaded.text=rounds.ToString();reserve.text=R23WeaponFeedbackState.ReserveText(pool);}
            float a=Fit(loaded),b=Fit(reserve);width=Mathf.Max(.033f,Mathf.Max(a,b)+.004f);
            face.localScale=new Vector3(width,.044f,.0025f);blue.localScale=new Vector3(width,.022f,.0026f);blue.localPosition=new Vector3(0,.011f,-.0001f);
            panel.localPosition=new Vector3((lateralBase+width*.5f/modelScale)*(left?1:-1),.05f,depth);panel.localRotation=Quaternion.identity;panel.localScale=Vector3.one/modelScale;
        }
        public void PresentFlash(bool visible,Transform sourceBore=null,float physicalLength=.07f,float physicalRadius=.018f){if(sourceBore!=null&&flash.parent!=sourceBore)flash.SetParent(sourceBore,false);flash.localScale=new Vector3(physicalRadius/.018f,physicalRadius/.018f,physicalLength/.07f);flash.gameObject.SetActive(visible);}
        public void PresentReticle(Camera head,Vector3 point,Vector3 boreDirection,float widthPixels,bool visible,bool magnum=false,bool supported=false,bool bracket=false)
        {
            reticle.gameObject.SetActive(visible);if(!visible)return;
            Vector3 up=Vector3.up;if(Vector3.Cross(head.transform.forward,up).sqrMagnitude<.0001f)up=Vector3.forward;
            reticle.SetPositionAndRotation(point-boreDirection*.015f,Quaternion.LookRotation(head.transform.forward,up));float size=Mathf.Clamp(Vector3.Distance(head.transform.position,point)*.010f,.010f,.60f)*4;reticle.localScale=Vector3.one*size;
            const float unit=1/(1147.17f*.04f);
            arms[4].GetComponent<Renderer>().sharedMaterial.SetColor("_Color",magnum&&!supported?new Color(1,.86f,.08f):new Color(1,.045f,.025f));
            for(int i=0;i<8;i++){
                int n=i%4;float stroke=i<4?5:3;arms[i].localRotation=Quaternion.identity;arms[i].GetComponent<MeshFilter>().sharedMesh=quad;
                if(bracket){
                    float horizontal=n==0||n==3?-1:1,vertical=n<2?1:-1;
                    // Each source corner has two perpendicular14px legs.
                    // A compact procedural corner mesh is supplied separately below.
                    arms[i].GetComponent<MeshFilter>().sharedMesh=BracketMesh(i<4?5:3,horizontal,vertical);
                    arms[i].localPosition=new Vector3(horizontal*(widthPixels-3)*.5f*unit,vertical*(widthPixels-3)*.5f*unit,i<4?0:-.01f/size);arms[i].localScale=Vector3.one;
                }else if(magnum){
                    // Original reticle_geometry: outward < and >, depth15/height32px.
                    float side=n<2?-1:1,vertical=n%2==0?1:-1;var end=new Vector2(-side*15,vertical*16)*unit;
                    arms[i].localPosition=new Vector3(side*(widthPixels-3)*.5f*unit+end.x*.5f,end.y*.5f,i<4?0:-.01f/size);
                    arms[i].localRotation=Quaternion.Euler(0,0,Mathf.Atan2(end.y,end.x)*Mathf.Rad2Deg);arms[i].localScale=new Vector3(end.magnitude,stroke*unit,1);
                }else{
                    bool horizontal=n<2;float sign=n==0||n==2?1:-1,arm=i<4?23:21;float offset=(widthPixels*.5f-21*.5f)*unit;
                    arms[i].localPosition=new Vector3(horizontal?sign*offset:0,horizontal?0:sign*offset,i<4?0:-.01f/size);arms[i].localScale=new Vector3((horizontal?arm:stroke)*unit,(horizontal?stroke:arm)*unit,1);
                }
            }
        }
        Mesh BracketMesh(int stroke,float x,float y){int key=stroke*10+(x<0?0:2)+(y<0?0:1);Mesh mesh;if(bracketMeshes.TryGetValue(key,out mesh))return mesh;const float unit=1/(1147.17f*.04f);var vertices=new List<Vector3>();var indices=new List<int>();foreach(var end in new[]{new Vector2(-x*14,0),new Vector2(0,-y*14)}){var normal=new Vector2(-end.y,end.x).normalized*stroke*.5f;int start=vertices.Count;foreach(var point in new[]{-normal,normal,end+normal,end-normal})vertices.Add(new Vector3(point.x*unit,point.y*unit,0));foreach(int n in new[]{0,1,2,0,2,3})indices.Add(start+n);}mesh=Make(vertices.ToArray(),indices.ToArray(),null);owned.Add(mesh);bracketMeshes.Add(key,mesh);return mesh;}
        public void Impact(Vector3 point,Vector3 normal,float now){int i=impactCursor++%impacts.Length;impacts[i].position=point+normal*.025f;impacts[i].localScale=Vector3.one;impacts[i].gameObject.SetActive(true);impactTimes[i]=now+R23WeaponFeedbackState.ImpactSeconds;}
        public void Tick(float now){for(int i=0;i<impacts.Length;i++)if(now>=impactTimes[i])impacts[i].gameObject.SetActive(false);}
        public void HideHeldPresentation(){panel.gameObject.SetActive(false);flash.gameObject.SetActive(false);reticle.gameObject.SetActive(false);}
        public void Hide(){if(panel!=null)panel.gameObject.SetActive(false);if(flash!=null)flash.gameObject.SetActive(false);if(reticle!=null)reticle.gameObject.SetActive(false);for(int i=0;i<impacts.Length;i++)if(impacts[i]!=null)impacts[i].gameObject.SetActive(false);}
        void RefreshFont(Font value){if(value!=font)return;loadedFont.SetTexture("_MainTex",font.material.mainTexture);reserveFont.SetTexture("_MainTex",font.material.mainTexture);}
        float Fit(TextMesh row){font.RequestCharactersInTexture(row.text,row.fontSize,FontStyle.Bold);RefreshFont(font);var size=row.GetComponent<Renderer>().localBounds.size;if(size.y<=.000001f||size.x<=0)return .029f;float factor=(38*.00055f)/size.y;row.transform.localScale=Vector3.one*factor;return size.x*factor;}
        Material FontMaterial(){var m=Material(Color.white,false);m.SetTexture("_MainTex",font.material.mainTexture);m.SetFloat("_Font",1);m.SetFloat("_Cull",2);m.renderQueue=3025;return m;}
        Material Material(Color color,bool overlay){var m=new Material(shader);owned.Add(m);m.SetColor("_Color",color);m.SetFloat("_Depth",overlay?8:4);m.SetFloat("_Cull",0);m.renderQueue=overlay?3120:3020;return m;}
        static Transform Node(string name,Transform parent){var go=new GameObject("R23 feedback "+name);go.layer=19;go.transform.SetParent(parent,false);return go.transform;}
        static Transform Part(string name,Transform parent,Mesh mesh,Material material){var t=Node(name,parent);t.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;var renderer=t.gameObject.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;return t;}
        TextMesh Row(string name,Transform parent,Material material,float y){var t=Node("ammo "+name,parent);t.localPosition=new Vector3(0,y,-.0038f);var row=t.gameObject.AddComponent<TextMesh>();row.font=font;row.fontStyle=FontStyle.Bold;row.fontSize=96;row.characterSize=1;row.anchor=TextAnchor.MiddleCenter;row.alignment=TextAlignment.Center;row.color=Color.white;row.richText=false;t.GetComponent<MeshRenderer>().sharedMaterial=material;return row;}
        static Mesh Quad(){return Make(new[]{new Vector3(-.5f,-.5f,0),new Vector3(.5f,-.5f,0),new Vector3(.5f,.5f,0),new Vector3(-.5f,.5f,0)},new[]{0,2,1,0,3,2},new[]{Vector2.zero,Vector2.right,Vector2.one,Vector2.up});}
        static Mesh Cube(){var v=new List<Vector3>();var t=new List<int>();foreach(var normal in new[]{Vector3.right,Vector3.left,Vector3.up,Vector3.down,Vector3.forward,Vector3.back}){Vector3 a=Mathf.Abs(normal.y)>.5f?Vector3.right:Vector3.up,b=Vector3.Cross(normal,a);int start=v.Count;foreach(var xy in new[]{new Vector2(-1,-1),new Vector2(1,-1),new Vector2(1,1),new Vector2(-1,1)})v.Add((normal+a*xy.x+b*xy.y)*.5f);foreach(int n in new[]{0,1,2,0,2,3})t.Add(start+n);}return Make(v.ToArray(),t.ToArray(),null);}
        static Mesh Rod(){var v=new List<Vector3>();var t=new List<int>();for(int i=0;i<5;i++){float a=i*Mathf.PI*2/5,b=(i+1)*Mathf.PI*2/5;int start=v.Count;v.Add(new Vector3(Mathf.Cos(a)*.018f,Mathf.Sin(a)*.018f,0));v.Add(new Vector3(Mathf.Cos(b)*.018f,Mathf.Sin(b)*.018f,0));v.Add(new Vector3(Mathf.Cos(b)*.018f,Mathf.Sin(b)*.018f,.07f));v.Add(new Vector3(Mathf.Cos(a)*.018f,Mathf.Sin(a)*.018f,.07f));foreach(int n in new[]{0,2,1,0,3,2})t.Add(start+n);}return Make(v.ToArray(),t.ToArray(),null);}
        static Mesh Sphere(){const int x=8,y=6;var v=new Vector3[(x+1)*(y+1)];var t=new List<int>();for(int i=0;i<=y;i++)for(int j=0;j<=x;j++){float a=i*Mathf.PI/y,b=j*Mathf.PI*2/x;v[i*(x+1)+j]=new Vector3(Mathf.Sin(a)*Mathf.Cos(b),Mathf.Cos(a),Mathf.Sin(a)*Mathf.Sin(b))*.045f;if(i<y&&j<x){int n=i*(x+1)+j;foreach(int k in new[]{n,n+x+1,n+1,n+1,n+x+1,n+x+2})t.Add(k);}}return Make(v,t.ToArray(),null);}
        static Mesh Make(Vector3[] vertices,int[] triangles,Vector2[] uv){var mesh=new Mesh();mesh.name="R23 original feedback geometry";mesh.vertices=vertices;mesh.triangles=triangles;if(uv!=null)mesh.uv=uv;mesh.RecalculateBounds();return mesh;}
        static Color Hex(string text){Color c;ColorUtility.TryParseHtmlString("#"+text,out c);return c;}
        public void Dispose(){Font.textureRebuilt-=RefreshFont;Hide();if(panel!=null)UnityEngine.Object.Destroy(panel.gameObject);if(flash!=null)UnityEngine.Object.Destroy(flash.gameObject);if(reticle!=null)UnityEngine.Object.Destroy(reticle.gameObject);foreach(var impact in impacts)if(impact!=null)UnityEngine.Object.Destroy(impact.gameObject);foreach(var item in owned)UnityEngine.Object.Destroy(item);owned.Clear();}
    }
}
#endif
