#if UNITY_6000_0_OR_NEWER
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
namespace Ashfall.Integration.Playable.Input
{
    public sealed class R23WeaponWheel:MonoBehaviour
    {
        readonly R23WeaponWheelState state=new R23WeaponWheelState();readonly List<UnityEngine.Object> owned=new List<UnityEngine.Object>();
        readonly Dictionary<string,Texture2D> textures=new Dictionary<string,Texture2D>();readonly Material[] sectors=new Material[8];readonly Transform[] cards=new Transform[R23WeaponWheelRules.MaximumSlots];readonly Transform[] lockedMarks=new Transform[6];
        readonly Dictionary<TextMesh,float> textHeights=new Dictionary<TextMesh,float>();
        Transform head,root,pointer;Vector3 anchor;TextMesh status;Font font;Shader uiShader;Func<string,int,bool> equip;Action backpack,quickToggle;Func<bool> revive;
        public bool Ready {get;private set;}public string Failure {get;private set;}public bool Active {get{return state.Active;}}
        public int HighlightedSector {get{return state.HighlightedSector;}}public string LastRejectedId {get;private set;}
        public event Action<bool> ActiveChanged;public event Action<R23WeaponWheelRequest> RequestAccepted;
        public Vector3 Center {get{return root==null?Vector3.zero:root.position;}}
        public void Initialize(Transform view,Func<string,int,bool> tryEquip,Action openBackpack=null,Action toggleHeldItem=null,Func<bool> tryEquipRevive=null)
        {
            if(root!=null||view==null||tryEquip==null)throw new ArgumentException("One head and an accepted inventory equip callback required");
            head=view;equip=tryEquip;backpack=openBackpack;quickToggle=toggleHeldItem;revive=tryEquipRevive;
            try{uiShader=Resources.Load<Shader>("R23VrUi");font=Resources.Load<Font>("R23InventoryUI");if(uiShader==null||font==null)throw new InvalidOperationException("Source UI font plus explicit rarity-star fallback required");
                root=new GameObject("Source A-held owned weapon wheel").transform;root.gameObject.SetActive(false);root.localScale=Vector3.one*R23WeaponWheelRules.DisplayScale;
                for(int i=0;i<8;i++){var mat=Material(Hex("16313a"));sectors[i]=mat;Shape("Source sector "+i,Wedge(i),mat,root);}
                Shape("Source cancel center",Disc(R23WeaponWheelRules.InnerRadius),Material(Hex("82aec7")),root);
                Bar("Cancel mark A",Vector3.zero,new Vector2(.011f,.0777f),Hex("173443"),45);Bar("Cancel mark B",Vector3.zero,new Vector2(.011f,.0777f),Hex("173443"),-45);
                for(int i=0;i<8;i++){float a=(112.5f-45*i)*Mathf.Deg2Rad,r=(.34f+.102f)*.5f;Bar("Source divider",new Vector3(Mathf.Cos(a)*r,Mathf.Sin(a)*r,-.009f),new Vector2(.006f,.238f),Hex("91a6b3"),a*Mathf.Rad2Deg-90);}
                // Human request: no interior text. Original utility geometry replaces
                // the old explanatory labels; item pictures remain inside the arc.
                var revive=At(3,.225f);Bar("Source revive vertical",revive,new Vector2(.022f,.09f),Hex("a6b3ba"),0);Bar("Source revive horizontal",revive,new Vector2(.09f,.022f),Hex("a6b3ba"),0);
                var bag=At(4,.225f);Bar("Source backpack body",bag,new Vector2(.094f,.09f),Hex("d8e4e9"),0);Bar("Source backpack flap",bag+Vector3.up*.034f,new Vector2(.068f,.018f),Hex("678b9c"),0);
                foreach(float x in new[]{-.033f,.033f})Bar("Source backpack strap",bag+new Vector3(x,.060f,0),new Vector2(.014f,.035f),Hex("d8e4e9"),0);
                foreach(int i in new[]{5,6,7}){int k=(i-5)*2;lockedMarks[k]=Bar("Locked cross A",At(i,.215f),new Vector2(.009f,.070f),Hex("b8c0c7"),45);lockedMarks[k+1]=Bar("Locked cross B",At(i,.215f),new Vector2(.009f,.070f),Hex("b8c0c7"),-45);}
                status=Label("",new Vector3(0,.39f,-.025f),.03024f,Color.white);pointer=new GameObject("Source clamped wheel pointer").transform;pointer.SetParent(root,false);Shape("Pointer",Disc(.009f),Material(new Color(1,.85f,.2f)),pointer);pointer.gameObject.SetActive(false);
                Ready=true;UpdateSlots();UpdateHighlight();
            }catch(Exception error){Failure=error.ToString();Ready=false;if(root!=null)root.gameObject.SetActive(false);}
        }
        public void SetSlots(params R23WeaponWheelSlot[] slots){SetInventorySlots(Math.Max(3,slots==null?0:slots.Length),slots);}
        public void SetInventorySlots(int capacity,params R23WeaponWheelSlot[] slots)
        {
            bool was=Active,changed=state.Capacity!=capacity;
            if(slots==null||capacity<3||capacity>R23WeaponWheelRules.MaximumSlots||slots.Length>capacity)throw new ArgumentException("Live inventory slots exceed source capacity");
            for(int i=0;i<R23WeaponWheelRules.MaximumSlots;i++){var old=state.Slot(i);var next=i<slots.Length?slots[i]:null;if((old==null)!=(next==null)||(old!=null&&next!=null&&(old.Id!=next.Id||old.Label!=next.Label||old.WeaponId!=next.WeaponId||old.CanEquip!=next.CanEquip||old.Count!=next.Count)))changed=true;}
            state.SetSlots(slots,capacity);if(Ready){if(changed)UpdateSlots();SyncVisibility(was);UpdateHighlight();}
        }
        public void Cancel(){bool was=Active;state.Cancel();SyncVisibility(was);}
        // Root combines session focus, aim tracking, alive/game input and menu/backpack
        // availability into valid. This component never pauses gameplay or emits jump.
        public bool Step(float seconds,bool aHeld,bool valid,Ray aim,Vector3 locomotionAnchor,int primaryHand=1)
        {
            if(!Ready)return false;bool was=Active;valid=valid&&Finite(aim.origin)&&Finite(aim.direction)&&aim.direction.sqrMagnitude>.000001f&&Finite(locomotionAnchor)&&head!=null&&isActiveAndEnabled;
            if(was&&valid){root.position+=locomotionAnchor-anchor;anchor=locomotionAnchor;}
            int pointed=was&&valid?Point(aim):-1;var request=state.Step(seconds,aHeld,valid,pointed,primaryHand);
            if(!was&&Active){Open(aim,locomotionAnchor);Point(aim);}
            SyncVisibility(was);UpdateHighlight();if(request==null)return false;
            bool accepted=false;if(request.Action==R23WeaponWheelAction.Equip){accepted=equip(request.Id,request.Hand);LastRejectedId=accepted?null:request.Id;}
            else if(request.Action==R23WeaponWheelAction.Revive){accepted=revive!=null&&revive();LastRejectedId=accepted?null:request.Id;}
            else if(request.Action==R23WeaponWheelAction.Backpack&&backpack!=null){backpack();accepted=true;}
            else if(request.Action==R23WeaponWheelAction.QuickToggle&&quickToggle!=null){quickToggle();accepted=true;}
            if(accepted&&RequestAccepted!=null)RequestAccepted(request);return accepted;
        }
        void Open(Ray aim,Vector3 locomotionAnchor)
        {
            anchor=locomotionAnchor;root.position=aim.origin+aim.direction.normalized*R23WeaponWheelRules.OpenDistance;Vector3 forward=root.position-head.position;if(forward.sqrMagnitude<.0001f)forward=head.forward;
            Vector3 up=Vector3.up;if(Mathf.Abs(Vector3.Dot(forward.normalized,up))>.95f){up=-head.forward;if(Vector3.Cross(forward,up).sqrMagnitude<.0001f)up=Vector3.forward;if(Vector3.Cross(forward,up).sqrMagnitude<.0001f)up=Vector3.right;}
            root.rotation=Quaternion.LookRotation(forward,up);root.localScale=Vector3.one*R23WeaponWheelRules.DisplayScale;
        }
        int Point(Ray ray)
        {
            var origin=root.InverseTransformPoint(ray.origin);var direction=root.InverseTransformVector(ray.direction);pointer.gameObject.SetActive(false);if(Mathf.Abs(direction.z)<=.0001f)return -1;
            float distance=-origin.z/direction.z;if(float.IsNaN(distance)||float.IsInfinity(distance)||distance<=0)return -1;Vector3 hit=origin+direction*distance;if(!Finite(hit))return -1;
            var xy=Vector2.ClampMagnitude(new Vector2(hit.x,hit.y),.34f);pointer.localPosition=new Vector3(xy.x,xy.y,-.012f);pointer.gameObject.SetActive(true);return R23WeaponWheelRules.Sector(hit.x,hit.y);
        }
        void SyncVisibility(bool before){if(root!=null)root.gameObject.SetActive(Active&&Ready&&isActiveAndEnabled);if(before!=Active&&ActiveChanged!=null)ActiveChanged(Active);}
        void UpdateHighlight()
        {
            int selected=state.HighlightedSector;for(int i=0;i<8;i++){int slot=R23WeaponWheelRules.SlotForSector(i);var item=slot>=0?state.Slot(slot):null;string color=slot>=state.Capacity?"747b84":i==3?(i==selected?"b16a20":"202d37"):slot>=0&&item==null?(i==selected?"26323a":"10232d"):i==selected&&(i==4||item!=null&&item.CanEquip)?"b16a20":"16313a";sectors[i].SetColor("_Color",Hex(color));}
            for(int i=5;i<8;i++)for(int k=0;k<2;k++)if(lockedMarks[(i-5)*2+k]!=null)lockedMarks[(i-5)*2+k].gameObject.SetActive(R23WeaponWheelRules.SlotForSector(i)>=state.Capacity);
            int pointed=R23WeaponWheelRules.SlotForSector(selected);var row=pointed>=0?state.Slot(pointed):null;status.text=row!=null?row.Label:selected==4?"BACKPACK":selected==3?"REVIVE PADDLES":"";status.color=row!=null&&R23InventoryPresentation.Tiered(row.WeaponId)?Hex(R23InventoryPresentation.RarityColor(row.Stars)):Color.white;
        }
        void UpdateSlots()
        {
            for(int i=0;i<cards.Length;i++){if(cards[i]!=null){cards[i].gameObject.SetActive(false);foreach(var label in cards[i].GetComponentsInChildren<TextMesh>(true))textHeights.Remove(label);foreach(var filter in cards[i].GetComponentsInChildren<MeshFilter>(true))if(filter.sharedMesh!=null&&owned.Remove(filter.sharedMesh))Destroy(filter.sharedMesh);foreach(var renderer in cards[i].GetComponentsInChildren<Renderer>(true))if(renderer.sharedMaterial!=null&&owned.Remove(renderer.sharedMaterial))Destroy(renderer.sharedMaterial);Destroy(cards[i].gameObject);}var card=new GameObject("Live owned inventory slot "+i).transform;card.gameObject.layer=5;cards[i]=card;card.SetParent(root,false);card.localPosition=At(R23WeaponWheelRules.SectorForSlot(i),.215f);var row=state.Slot(i);if(row==null)continue;
                Texture2D texture;var key=row.WeaponId;if(!textures.TryGetValue(key,out texture)){byte[] bytes=R23WeaponWheelIcons.Read(key);if(bytes!=null){texture=new Texture2D(2,2,TextureFormat.RGBA32,false,false);if(!texture.LoadImage(bytes,true))throw new InvalidOperationException("Source weapon icon "+key);texture.filterMode=FilterMode.Bilinear;texture.wrapMode=TextureWrapMode.Clamp;owned.Add(texture);}textures[key]=texture;}
                if(texture!=null){var shader=Resources.Load<Shader>("R23SourceIcon");if(shader==null)throw new InvalidOperationException("Source icon shader");var mat=new Material(shader);owned.Add(mat);mat.SetTexture("_IconTex",texture);mat.SetColor("_Tint",R23InventoryPresentation.Tiered(key)?Hex(R23InventoryPresentation.RarityColor(row.Stars)):Color.white);mat.renderQueue=4124;float iconWidth=.18432f,iconHeight=iconWidth*texture.height/texture.width;if(key=="soda"||key=="shaker"){float fit=.13824f/Mathf.Max(iconWidth,iconHeight);iconWidth*=fit;iconHeight*=fit;}var icon=Shape("Original weapon icon "+key,Quad(iconWidth,iconHeight),mat,card);icon.localPosition=new Vector3(0,0,-.022f);if(key=="knife"||key=="katana"||key=="excalibur"||key=="cyberblade")icon.localRotation=Quaternion.Euler(0,0,180);}if(row.Count>1)Label("x"+row.Count,new Vector3(.075f,.065f,-.027f),.028f,Color.white,card);
            }
        }
        Material Material(Color color){var material=new Material(uiShader);owned.Add(material);material.SetColor("_Color",color);material.renderQueue=4120;return material;}
        Transform Shape(string label,Mesh mesh,Material material,Transform parent){owned.Add(mesh);var go=new GameObject(label);go.layer=5;go.transform.SetParent(parent,false);go.AddComponent<MeshFilter>().sharedMesh=mesh;var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;return go.transform;}
        TextMesh Label(string text,Vector3 at,float size,Color color,Transform parent=null){var go=new GameObject("Source wheel label");go.layer=5;go.transform.SetParent(parent==null?root:parent,false);go.transform.localPosition=at;var label=go.AddComponent<TextMesh>();label.font=font;label.fontSize=96;label.characterSize=1;label.richText=false;label.anchor=TextAnchor.MiddleCenter;label.alignment=TextAlignment.Center;label.text=text;label.color=color;var mat=Material(Color.white);mat.SetTexture("_MainTex",font.material.mainTexture);mat.SetFloat("_Font",1);mat.renderQueue=4125;go.GetComponent<MeshRenderer>().sharedMaterial=mat;textHeights.Add(label,size);Fit(label,size);return label;}
        void Fit(TextMesh label,float height){if(label==null||string.IsNullOrEmpty(label.text))return;font.RequestCharactersInTexture(label.text,label.fontSize,label.fontStyle);label.GetComponent<MeshRenderer>().sharedMaterial.SetTexture("_MainTex",font.material.mainTexture);var size=label.GetComponent<MeshRenderer>().localBounds.size;if(size.x>.00001f)label.transform.localScale=Vector3.one*Mathf.Min(height/(.72f*label.fontSize*label.characterSize*.1f),.95f/size.x);}
        void LateUpdate(){if(Ready&&Active)foreach(var label in textHeights)Fit(label.Key,label.Value);}
        Transform Bar(string name,Vector3 at,Vector2 size,Color color,float angle){var bar=Shape(name,Quad(size.x,size.y),Material(color),root);bar.localPosition=at+Vector3.back*.016f;bar.localRotation=Quaternion.AngleAxis(angle,Vector3.forward);return bar;}
        static Vector3 At(int sector,float radius){float a=(90-sector*45)*Mathf.Deg2Rad;return new Vector3(Mathf.Cos(a)*radius,Mathf.Sin(a)*radius,-.018f);}
        static Mesh Quad(float width,float height){return Mesh(new[]{new Vector3(-width/2,-height/2,0),new Vector3(width/2,-height/2,0),new Vector3(width/2,height/2,0),new Vector3(-width/2,height/2,0)},new[]{0,1,2,0,2,3},new[]{Vector2.zero,Vector2.right,Vector2.one,Vector2.up});}
        static Mesh Disc(float radius){var v=new List<Vector3>();var t=new List<int>();for(int i=0;i<32;i++){float a=i*Mathf.PI/16,b=(i+1)*Mathf.PI/16;int n=v.Count;v.Add(Vector3.zero);v.Add(new Vector3(Mathf.Cos(a)*radius,Mathf.Sin(a)*radius,0));v.Add(new Vector3(Mathf.Cos(b)*radius,Mathf.Sin(b)*radius,0));t.Add(n);t.Add(n+1);t.Add(n+2);}return Mesh(v.ToArray(),t.ToArray(),null);}
        static Mesh Wedge(int sector){var v=new List<Vector3>();var t=new List<int>();for(int i=0;i<8;i++){float a=(112.5f-sector*45-i*45f/8)*Mathf.Deg2Rad,b=(112.5f-sector*45-(i+1)*45f/8)*Mathf.Deg2Rad;int n=v.Count;foreach(var pair in new[]{new Vector2(a,.102f),new Vector2(a,.34f),new Vector2(b,.34f),new Vector2(b,.102f)})v.Add(new Vector3(Mathf.Cos(pair.x)*pair.y,Mathf.Sin(pair.x)*pair.y,0));foreach(int k in new[]{0,1,2,0,2,3})t.Add(n+k);}return Mesh(v.ToArray(),t.ToArray(),null);}
        static Mesh Mesh(Vector3[] vertices,int[] triangles,Vector2[] uv){var mesh=new Mesh();mesh.vertices=vertices;mesh.triangles=triangles;var colors=new Color[vertices.Length];for(int i=0;i<colors.Length;i++)colors[i]=Color.white;mesh.colors=colors;if(uv!=null)mesh.uv=uv;mesh.RecalculateBounds();return mesh;}
        static bool Finite(Vector3 v){return !(float.IsNaN(v.x)||float.IsInfinity(v.x)||float.IsNaN(v.y)||float.IsInfinity(v.y)||float.IsNaN(v.z)||float.IsInfinity(v.z));}
        static Color Hex(string code){Color value;ColorUtility.TryParseHtmlString("#"+code,out value);return value;}
        public Vector3 FixtureSectorPoint(int sector,float radius=.215f){if(root==null||sector<0||sector>7)throw new ArgumentOutOfRangeException("sector");return root.TransformPoint(At(sector,radius));}
        public bool FixtureVisible {get{return root!=null&&root.gameObject.activeInHierarchy;}}
        public string StatusText {get{return status==null?"":status.text;}}
        public int InteriorTextCount {get{if(root==null)return 0;int count=0;foreach(var text in root.GetComponentsInChildren<TextMesh>(true))if(text!=status)count++;return count;}}
        void OnDisable(){Cancel();}
        void OnDestroy(){state.Cancel();if(root!=null)Destroy(root.gameObject);foreach(var item in owned)if(item!=null)Destroy(item);owned.Clear();textures.Clear();}
    }
}
#endif
