#if UNITY_6000_0_OR_NEWER
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
namespace Ashfall.Integration.Playable.Input
{
    // Render-only source ping_wheel_086 / ping_sprite_r19 / ping_column_r23.
    // Real-world anchors and bounded owned geometry; never writes tracked poses.
    internal sealed class R23TeamMarksView:IDisposable
    {
        readonly List<UnityEngine.Object> owned=new List<UnityEngine.Object>();
        readonly Material[] sectors=new Material[8];readonly Texture2D[] icons=new Texture2D[8];
        readonly Transform head,wheel,mark,pointer,icon,column;readonly Material markMaterial,columnMaterial;
        readonly TextMesh header;readonly Font font;readonly Shader ui;readonly Mesh columnMesh;
        Vector3 anchor;int markIndex=-1,dotCount=-1;float markY;const int DotIndices=240;
        public bool WheelVisible {get{return wheel.gameObject.activeSelf;}}
        public bool MarkVisible {get{return mark.gameObject.activeSelf&&icon.gameObject.activeSelf;}}
        public Vector3 WheelPosition {get{return wheel.position;}}
        public Vector3 IconPosition {get{return icon.position;}}
        public float IconWidth {get{return icon.localScale.x;}}
        public int DotCount {get{return Math.Max(0,dotCount);}}
        public R23TeamMarksView(Transform view)
        {
            head=view;ui=Shader.Find("Ashfall/R23GunFeedback");
            if(ui==null)throw new InvalidOperationException("Retained R23GunFeedback shader required");
            font=Resources.Load<Font>("R23GodotUI");if(font==null)throw new InvalidOperationException("Actual R23 Godot UI font required");
            wheel=new GameObject("Source weapon B/Y local mark wheel").transform;wheel.localScale=Vector3.one*1.2f;
            mark=new GameObject("Source single-current local team mark").transform;
            try
            {
                for(int i=0;i<8;i++)
                {
                    icons[i]=Keep(new Texture2D(2,2,TextureFormat.RGBA32,false));
                    if(!icons[i].LoadImage(R23TeamMarksAssets.Icon(i),false))throw new InvalidOperationException("Source ping PNG decode failed");
                    icons[i].name="Original source ping "+i;icons[i].filterMode=FilterMode.Bilinear;icons[i].wrapMode=TextureWrapMode.Clamp;
                    sectors[i]=Material(Hex("16313a"),120);Part("Source sector "+i,Wedge(i),sectors[i],wheel);
                    float a=(90-i*45)*Mathf.Deg2Rad;
                    var glyph=Part("Original ping SVG "+i,Quad(.096f,.096f),Material(Color.white,124,icons[i]),wheel);
                    glyph.localPosition=new Vector3(Mathf.Cos(a)*.225f,Mathf.Sin(a)*.225f,-.022f);
                    float divider=(112.5f-i*45)*Mathf.Deg2Rad;
                    Bar("Source divider",new Vector3(Mathf.Cos(divider)*.221f,Mathf.Sin(divider)*.221f,-.009f),new Vector2(.006f,.238f),Hex("91a6b3"),divider*Mathf.Rad2Deg-90,121);
                }
                Part("Cancel disc",Disc(.102f,-.011f),Material(Hex("82aec7"),122),wheel);
                Bar("Cancel X A",new Vector3(0,0,-.016f),new Vector2(.01155f,.0777f),Hex("173443"),45,123);
                Bar("Cancel X B",new Vector3(0,0,-.016f),new Vector2(.01155f,.0777f),Hex("173443"),-45,123);
                pointer=Part("Source pointer sphere",Sphere(.009f,.018f,1),Material(new Color(1,.85f,.2f),125),wheel);
                var text=new GameObject("Source mark title");text.transform.SetParent(wheel,false);text.transform.localPosition=new Vector3(0,.39f,-.02f);
                header=text.AddComponent<TextMesh>();header.font=font;header.fontSize=30;header.characterSize=1;header.richText=false;header.anchor=TextAnchor.MiddleCenter;header.alignment=TextAlignment.Center;
                var textMaterial=Material(Color.white,126,font.material.mainTexture);textMaterial.SetFloat("_Font",1);text.GetComponent<MeshRenderer>().sharedMaterial=textMaterial;
                markMaterial=Material(Color.white,120);icon=Part("Source angular billboard ping",Quad(1,1),markMaterial,mark);
                var shader=Shader.Find("Universal Render Pipeline/Unlit");if(shader==null)throw new InvalidOperationException("Retained URP Unlit shader required");
                columnMaterial=Keep(new Material(shader));columnMaterial.name="Source depth-tested unshaded dots";columnMaterial.SetFloat("_Cull",0);
                columnMesh=Sphere(.5f,.49f,512);column=Part("Source R23 dotted sky column",columnMesh,columnMaterial,mark);
                HideWheel();ClearMark();
            }
            catch{Dispose();throw;}
        }
        T Keep<T>(T item) where T:UnityEngine.Object {owned.Add(item);return item;}
        Material Material(Color tint,int priority,Texture texture=null)
        {var m=Keep(new Material(ui));m.name="Source mark overlay "+priority;m.SetColor("_Color",tint);m.SetFloat("_Cull",0);m.SetFloat("_Depth",8);m.renderQueue=4000+priority;if(texture!=null)m.SetTexture("_MainTex",texture);return m;}
        Transform Part(string name,Mesh mesh,Material material,Transform parent)
        {
            var go=new GameObject(name);go.layer=5;go.transform.SetParent(parent,false);go.AddComponent<MeshFilter>().sharedMesh=mesh;
            var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;return go.transform;
        }
        void Bar(string name,Vector3 at,Vector2 size,Color color,float angle,int priority)
        {var bar=Part(name,Quad(size.x,size.y),Material(color,priority),wheel);bar.localPosition=at;bar.localRotation=Quaternion.AngleAxis(angle,Vector3.forward);}
        public void Open(Ray ray,Vector3 locomotionAnchor)
        {
            anchor=locomotionAnchor;wheel.position=ray.origin+ray.direction.normalized*.85f;
            var forward=wheel.position-head.position;if(forward.sqrMagnitude<.0001f)forward=head.forward;
            var up=Vector3.up;if(Mathf.Abs(Vector3.Dot(forward.normalized,up))>.95f){up=-head.forward;if(Vector3.Cross(forward,up).sqrMagnitude<.0001f)up=Vector3.forward;if(Vector3.Cross(forward,up).sqrMagnitude<.0001f)up=Vector3.right;}
            wheel.rotation=Quaternion.LookRotation(forward,up);wheel.gameObject.SetActive(true);
        }
        public void Follow(Vector3 locomotionAnchor){wheel.position+=locomotionAnchor-anchor;anchor=locomotionAnchor;}
        public int Point(Ray ray)
        {
            pointer.gameObject.SetActive(false);var o=wheel.InverseTransformPoint(ray.origin);var d=wheel.InverseTransformVector(ray.direction.normalized);
            if(Mathf.Abs(d.z)<=.0001f)return -1;float distance=-o.z/d.z;if(!R23TeamMarksState.Finite(distance)||distance<=0)return -1;
            var hit=o+d*distance;var xy=new Vector2(hit.x,hit.y);float radius=xy.magnitude;var display=Vector2.ClampMagnitude(xy,.34f);
            pointer.localPosition=new Vector3(display.x,display.y,-.012f);pointer.gameObject.SetActive(true);
            if(radius<.102f)return -1;
            // weapon_wheel.point picks the nearest center; exact ties keep the
            // earlier sector, distinct from the stick's floor-at-boundary rule.
            int best=-1;float nearest=float.PositiveInfinity;
            for(int i=0;i<8;i++){float difference=Mathf.Abs(Mathf.DeltaAngle(Mathf.Atan2(xy.y,xy.x)*Mathf.Rad2Deg,90-i*45));if(difference<nearest){nearest=difference;best=i;}}
            return best;
        }
        public void Highlight(int index,int pointed)
        {for(int i=0;i<8;i++)sectors[i].SetColor("_Color",Hex(i==pointed?"b16a20":"16313a"));header.text=R23TeamMarksState.Title(index);FitHeader();}
        public void FitHeader()
        {
            if(string.IsNullOrEmpty(header.text))return;font.RequestCharactersInTexture(header.text,30,FontStyle.Normal);
            header.GetComponent<MeshRenderer>().sharedMaterial.SetTexture("_MainTex",font.material.mainTexture);
            CharacterInfo cap;float height=font.GetCharacterInfo('H',out cap,30,FontStyle.Normal)?cap.glyphHeight*.001f:.022f;
            var bounds=header.GetComponent<MeshRenderer>().localBounds.size;if(bounds.x>.00001f&&bounds.y>.00001f)header.transform.localScale=Vector3.one*Mathf.Min(height/bounds.y,.9f/bounds.x);
        }
        public void HideWheel(){if(wheel!=null)wheel.gameObject.SetActive(false);}
        public void Place(Vector3 point,Vector3 normal,int index)
        {
            markIndex=index;markY=point.y;bool paint=R23TeamMarksState.Paint(index);
            mark.position=point+normal.normalized*(paint?.004f:.035f);mark.rotation=Quaternion.identity;mark.localScale=Vector3.one;
            icon.localPosition=paint?Vector3.zero:Vector3.up*.5f;markMaterial.SetTexture("_MainTex",icons[index]);
            columnMaterial.SetColor("_BaseColor",Hex(R23TeamMarksState.ColorHex(index)));dotCount=-1;mark.gameObject.SetActive(true);
            UpdateMark(false);
        }
        public void UpdateMark(bool tacticalMap)
        {
            if(markIndex<0)return;var center=icon.position;float depth=Vector3.Dot(center-head.position,head.forward);
            bool show=!tacticalMap&&depth>.001f;icon.gameObject.SetActive(show);
            if(show){icon.rotation=head.rotation;icon.localScale=Vector3.one*(R23TeamMarksState.AngularWidth(markIndex)*depth*.5f);}
            // Paint marks still billboard after ping_sprite_r19 overrides the
            // initial surface basis, but never acquire a sky column.
            int count=show&&!R23TeamMarksState.Paint(markIndex)?R23TeamMarksState.ColumnCount(depth,markY,markIndex):0;
            column.gameObject.SetActive(count>0);
            if(count>0)
            {
                float width=R23TeamMarksState.AngularWidth(markIndex)*depth*.5f;
                column.localPosition=Vector3.up*(.5f+width*1.25f);column.localScale=Vector3.one*width;
                if(count!=dotCount)
                {
                    columnMesh.SetSubMesh(0,new SubMeshDescriptor(0,count*DotIndices,MeshTopology.Triangles),MeshUpdateFlags.DontRecalculateBounds);
                    columnMesh.bounds=new Bounds(new Vector3(0,(count-1)*1.37f*.5f,0),new Vector3(1,(count-1)*1.37f+.49f,1));
                }
            }
            dotCount=count;
        }
        public void ClearMark(){markIndex=-1;dotCount=0;if(mark!=null)mark.gameObject.SetActive(false);}
        public Vector3 SectorPoint(int index,float radius){float a=(90-index*45)*Mathf.Deg2Rad;return wheel.TransformPoint(new Vector3(Mathf.Cos(a)*radius,Mathf.Sin(a)*radius,0));}
        Mesh Mesh(Vector3[] vertices,int[] triangles,Vector2[] uv=null)
        {var m=Keep(new Mesh());m.name="Owned source team-mark geometry";m.vertices=vertices;m.triangles=triangles;if(uv!=null)m.uv=uv;m.RecalculateBounds();return m;}
        Mesh Quad(float w,float h)
        {return Mesh(new[]{new Vector3(-w/2,-h/2,0),new Vector3(w/2,-h/2,0),new Vector3(w/2,h/2,0),new Vector3(-w/2,h/2,0)},new[]{0,1,2,0,2,3},new[]{Vector2.zero,Vector2.right,Vector2.one,Vector2.up});}
        Mesh Disc(float radius,float z)
        {var v=new List<Vector3>();var t=new List<int>();for(int i=0;i<32;i++){float a=i*Mathf.PI/16,b=(i+1)*Mathf.PI/16;int n=v.Count;v.Add(new Vector3(0,0,z));v.Add(new Vector3(Mathf.Cos(a)*radius,Mathf.Sin(a)*radius,z));v.Add(new Vector3(Mathf.Cos(b)*radius,Mathf.Sin(b)*radius,z));t.Add(n);t.Add(n+1);t.Add(n+2);}return Mesh(v.ToArray(),t.ToArray());}
        Mesh Wedge(int index)
        {var v=new List<Vector3>();var t=new List<int>();for(int step=0;step<8;step++){float a=(112.5f-index*45-step*45f/8)*Mathf.Deg2Rad,b=(112.5f-index*45-(step+1)*45f/8)*Mathf.Deg2Rad;int n=v.Count;foreach(var p in new[]{new Vector2(a,.102f),new Vector2(a,.34f),new Vector2(b,.34f),new Vector2(b,.102f)})v.Add(new Vector3(Mathf.Cos(p.x)*p.y,Mathf.Sin(p.x)*p.y,0));foreach(int k in new[]{0,1,2,0,2,3})t.Add(n+k);}return Mesh(v.ToArray(),t.ToArray());}
        Mesh Sphere(float radius,float height,int copies)
        {
            // SphereMesh radial_segments=8, rings=4 (five latitude strips).
            const int nv=54,ni=DotIndices;var v=new Vector3[copies*nv];var t=new int[copies*ni];
            for(int dot=0;dot<copies;dot++)
            {
                for(int y=0;y<=5;y++)for(int x=0;x<=8;x++){float latitude=Mathf.PI*y/5,angle=Mathf.PI*2*x/8;v[dot*nv+y*9+x]=new Vector3(Mathf.Sin(latitude)*Mathf.Cos(angle)*radius,Mathf.Cos(latitude)*height*.5f+dot*1.37f,Mathf.Sin(latitude)*Mathf.Sin(angle)*radius);}
                int p=dot*ni;for(int y=0;y<5;y++)for(int x=0;x<8;x++){int n=dot*nv+y*9+x;foreach(int k in new[]{0,9,1,1,9,10})t[p++]=n+k;}
            }
            return Mesh(v,t);
        }
        static Color Hex(string s){Color c;ColorUtility.TryParseHtmlString("#"+s,out c);return c;}
        public void Dispose(){if(wheel!=null)UnityEngine.Object.Destroy(wheel.gameObject);if(mark!=null)UnityEngine.Object.Destroy(mark.gameObject);foreach(var item in owned)if(item!=null)UnityEngine.Object.Destroy(item);owned.Clear();}
    }
}
#endif
