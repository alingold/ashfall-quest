#if UNITY_6000_0_OR_NEWER
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Ashfall.Modules.Presentation.Engine
{
    // Add through coordinator bootstrap, then Bind. No scene, tracking, input
    // package, collider or EventSystem ownership is assumed here.
    public sealed class PresentationAdapter : MonoBehaviour
    {
        public bool DrawDesktop = true;
        public Transform SpatialAnchor;
        private PresentationModel model;
        private HudModel hud;
        private IPresentationStateSource stateSource;
        private IPresentationTransientView transientView;
        public void BindTransientView(IPresentationTransientView view)
        {if(transientView!=null)transientView.Clear();transientView=view;}
        public void BindState(IPresentationStateSource source) { stateSource=source; }
        private Vector2 scroll;
        private int firstRow;
        private GameObject spatialRoot;
        private TextMesh title, status, hudText, clockText;
        private readonly List<TextMesh> labels=new List<TextMesh>();
        private readonly List<Renderer> cards=new List<Renderer>();
        private Material cardMaterial;
        private GUIStyle heading, button, text;
        private readonly Color cyan=new Color(.25f,.91f,.94f);
        private const int SpatialRows=8;
        public void Bind(PresentationModel state,HudModel meters)
        {
            if(state==null || meters==null)throw new ArgumentNullException();
            if(model!=null && model!=state)model.Suspend();
            model=state;hud=meters;
        }
        // Central Input adapter forwards edge-triggered actions, not raw held buttons.
        public void Navigate(int vertical,int horizontal,bool submit,bool back,bool menu)
        {
            if(model==null)return;
            if(menu){if(model.IsOpen)model.Back();else model.Open();return;}
            if(!model.IsOpen)return;
            if(back){model.Back();return;}
            model.Move(vertical);
            if(horizontal!=0)model.Adjust(model.Focus,horizontal);
            if(submit)model.Activate(model.Focus);
        }
        private void OnGUI()
        {
            if(!DrawDesktop || model==null || hud==null)return;
            if(heading==null){heading=new GUIStyle(GUI.skin.label);heading.fontSize=26;heading.normal.textColor=cyan;
                text=new GUIStyle(GUI.skin.label);text.fontSize=18;text.wordWrap=true;
                button=new GUIStyle(GUI.skin.button);button.fontSize=18;button.alignment=TextAnchor.MiddleLeft;button.wordWrap=true;}
            if(!model.IsOpen){
                if(hud.Visible){GUI.color=Color.white;GUI.Label(new Rect(20,Screen.height-100,650,90),HudCaption(),text);
                    GUI.color=hud.ClockDanger?new Color(1,.4f,110f/255):Color.white;
                    GUI.Label(new Rect(20,Screen.height-130,650,30),ClockCaption(),text);GUI.color=Color.white;}return;
            }
            float width=Mathf.Min(680,Screen.width-24),height=Mathf.Min(820,Screen.height-24);
            var area=new Rect((Screen.width-width)/2,(Screen.height-height)/2,width,height);
            GUI.color=new Color(.14f,.17f,.20f);GUI.Box(area,GUIContent.none);GUI.color=Color.white;
            GUILayout.BeginArea(new Rect(area.x+16,area.y+12,width-32,height-24));
            GUILayout.Label(model.Page=="pause"?"ASHFALL":"SETTINGS / "+model.Page.ToUpperInvariant(),heading);
            if(model.Loading)GUILayout.Label("Please wait…",text);
            if(!String.IsNullOrEmpty(model.Error)){GUI.color=new Color(1,.7f,.4f);GUILayout.Label(model.Error,text);GUI.color=Color.white;}
            scroll=GUILayout.BeginScrollView(scroll);
            var rows=model.Rows();
            foreach(var row in rows){
                GUI.enabled=row.Enabled;
                GUI.backgroundColor=row.Id==model.Focus?cyan:new Color(.35f,.39f,.43f);
                GUI.SetNextControlName(row.Id);
                string caption=(row.Id==model.Focus?"> ":"")+row.Label+(row.Value.Length>0?"   "+row.Value:"")+(row.Enabled?"":"  (unavailable)");
                if(GUILayout.Button(caption,button,GUILayout.MinHeight(50))){model.FocusRow(row.Id);model.Activate(row.Id);}
                if(row.Setting!=null && row.Setting.Numeric){
                    float value=float.Parse(row.Value,System.Globalization.CultureInfo.InvariantCulture);
                    float next=GUILayout.HorizontalSlider(value,(float)row.Setting.Min,(float)row.Setting.Max,GUILayout.Height(24));
                    if(!Mathf.Approximately(next,value)){model.FocusRow(row.Id);model.SetNumeric(row.Id,next);}
                }
                GUI.enabled=true;
            }
            GUI.backgroundColor=Color.white;GUILayout.EndScrollView();
            GUILayout.Label("Select to change • Left / right to adjust • Back to save",text);GUILayout.EndArea();
        }
        private string HudCaption()
        {
            return String.Format("HEALTH {0:0}   SHIELD {1:0}   SPRINT {2:0}%{3}",hud.Health*100,hud.Shield*100,hud.Sprint*100,
                hud.IncomingSeconds>0?"   -"+Mathf.RoundToInt(hud.Incoming):"");
        }
        private string ClockCaption() { return hud.ClockCaption; }
        private TextMesh MakeText(string name,Vector3 position,float size)
        {
            var go=new GameObject(name);go.transform.SetParent(spatialRoot.transform,false);go.transform.localPosition=position;
            var mesh=go.AddComponent<TextMesh>();mesh.fontSize=64;mesh.characterSize=size;mesh.anchor=TextAnchor.MiddleLeft;mesh.color=Color.white;
            return mesh;
        }
        private void BuildSpatial()
        {
            spatialRoot=new GameObject("AshfallPresentation");spatialRoot.transform.SetParent(SpatialAnchor,false);
            title=MakeText("Title",new Vector3(-.57f,.60f,0),.055f);title.color=cyan;
            status=MakeText("Status",new Vector3(-.57f,-.58f,0),.026f);
            // Built-in TextMesh and quads; no uGUI package or collision layers needed.
            var shader=Shader.Find("Universal Render Pipeline/Unlit");
            if(shader==null)shader=Shader.Find("Unlit/Color");
            if(shader!=null)cardMaterial=new Material(shader);
            for(int i=0;i<SpatialRows;i++){
                float y=.44f-i*.135f;
                labels.Add(MakeText("Row"+i,new Vector3(-.55f,y,-.002f),.029f));
                var card=GameObject.CreatePrimitive(PrimitiveType.Quad);card.name="Card"+i;
                var collider=card.GetComponent<Collider>();if(collider!=null){collider.enabled=false;Destroy(collider);}
                card.transform.SetParent(spatialRoot.transform,false);card.transform.localPosition=new Vector3(0,y,.004f);card.transform.localScale=new Vector3(1.2f,.12f,1);
                var renderer=card.GetComponent<Renderer>();if(cardMaterial!=null)renderer.sharedMaterial=cardMaterial;cards.Add(renderer);
            }
            hudText=MakeText("HUD",new Vector3(-.55f,-.72f,0),.03f);
            clockText=MakeText("Clock",new Vector3(-.55f,-.77f,0),.03f);
        }
        private void LateUpdate()
        {
            if(model==null || hud==null)return;
            if(stateSource!=null)hud.Update(stateSource.Capture(),model.IsOpen,Time.deltaTime);
            if(transientView!=null)transientView.Refresh(Time.deltaTime);
            model.RefreshAvailability();
            if(SpatialAnchor==null){if(spatialRoot!=null)spatialRoot.SetActive(false);return;}
            if(spatialRoot==null)BuildSpatial();
            spatialRoot.SetActive(model.IsOpen || hud.Visible);
            var rows=model.Rows();int focused=-1;
            for(int i=0;i<rows.Count;i++)if(rows[i].Id==model.Focus)focused=i;
            if(focused>=0){if(focused<firstRow)firstRow=focused;if(focused>=firstRow+SpatialRows)firstRow=focused-SpatialRows+1;}
            firstRow=Mathf.Clamp(firstRow,0,Mathf.Max(0,rows.Count-SpatialRows));
            title.gameObject.SetActive(model.IsOpen);status.gameObject.SetActive(model.IsOpen);
            title.text=model.Page=="pause"?"ASHFALL":"SETTINGS / "+model.Page.ToUpperInvariant();
            status.text=model.Loading?"Please wait…":!String.IsNullOrEmpty(model.Error)?model.Error:String.Format("{0}–{1} / {2}   •   Back to save",firstRow+1,Math.Min(rows.Count,firstRow+SpatialRows),rows.Count);
            for(int i=0;i<SpatialRows;i++){
                bool show=model.IsOpen && i+firstRow<rows.Count;labels[i].gameObject.SetActive(show);cards[i].gameObject.SetActive(show);if(!show)continue;
                var row=rows[firstRow+i];labels[i].text=(model.Focus==row.Id?"> ":"")+row.Label+(row.Value.Length>0?"\n   "+row.Value:"")+(row.Enabled?"":" (unavailable)");
                labels[i].color=row.Enabled?Color.white:Color.gray;
                // Fit text to card width, retaining an explicit minimum rather than clipping.
                labels[i].characterSize=.029f;
                float width=labels[i].GetComponent<Renderer>().bounds.size.x/Mathf.Max(.001f,spatialRoot.transform.lossyScale.x);
                if(width>1.08f)labels[i].characterSize*=1.08f/width;
                var block=new MaterialPropertyBlock();block.SetColor("_BaseColor",row.Id==model.Focus?new Color(.13f,.36f,.46f):new Color(.14f,.16f,.19f));cards[i].SetPropertyBlock(block);
            }
            hudText.gameObject.SetActive(hud.Visible);hudText.text=HudCaption();
            clockText.gameObject.SetActive(hud.Visible);clockText.text=ClockCaption();
            clockText.color=hud.ClockDanger?new Color(1,.4f,110f/255):Color.white;
        }
        // XR pointer provider supplies a world ray plus a press edge. Intersect
        // the panel mathematically: this never changes world hit routing/colliders.
        public bool SubmitRay(Ray ray,bool pressed)
        {
            if(model==null || !model.IsOpen || spatialRoot==null || !spatialRoot.activeInHierarchy)return false;
            var localOrigin=spatialRoot.transform.InverseTransformPoint(ray.origin);
            var localDirection=spatialRoot.transform.InverseTransformVector(ray.direction);
            if(Mathf.Abs(localDirection.z)<.00001f)return false;
            float distance=-localOrigin.z/localDirection.z;if(distance<0)return false;
            var point=localOrigin+localDirection*distance;if(Mathf.Abs(point.x)>.6f)return false;
            if(point.y < -.58f && point.y > -.70f){
                if(pressed){
                    var all=model.Rows();firstRow=Mathf.Clamp(firstRow+(point.x<0?-SpatialRows:SpatialRows),0,Mathf.Max(0,all.Count-SpatialRows));
                    for(int i=firstRow;i<Math.Min(all.Count,firstRow+SpatialRows);i++)if(all[i].Enabled){model.FocusRow(all[i].Id);break;}
                }
                return true;
            }
            int index=Mathf.RoundToInt((.44f-point.y)/.135f);
            if(index<0 || index>=SpatialRows || Mathf.Abs(point.y-(.44f-index*.135f))>.06f)return false;
            var rows=model.Rows();index+=firstRow;if(index>=rows.Count)return false;
            var row=rows[index];if(!row.Enabled)return true;model.FocusRow(row.Id);
            if(pressed){if(row.Setting!=null && row.Setting.Numeric)model.SetNumeric(row.Id,row.Setting.Min+(point.x+.6f)/1.2f*(row.Setting.Max-row.Setting.Min));else model.Activate(row.Id);}
            return true;
        }
        private void OnDisable(){if(transientView!=null)transientView.Clear();if(model!=null)model.Suspend();}
        private void OnDestroy(){if(transientView!=null)transientView.Clear();if(model!=null)model.Suspend();if(spatialRoot!=null)Destroy(spatialRoot);if(cardMaterial!=null)Destroy(cardMaterial);}
    }
}
#endif
