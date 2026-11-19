#if UNITY_6000_0_OR_NEWER
using System;using System.Collections.Generic;using UnityEngine;using Ashfall.Integration.Playable.Networking;
namespace Ashfall.Integration.Playable.UI
{
    // One anchored panel and one 64-particle burst per local winner. No camera effects.
    public sealed class R26MatchEndCelebration:MonoBehaviour
    {
        R23PlayableLoop root;GameObject panel;TextMesh countdown;AudioSource voice;AudioClip victory,shortCue;Font font;Material ink,flat,confettiMaterial;
        Mesh confetti;Vector3[] confettiVertices;Vector3[] confettiOrigins,confettiVelocities;float burstAt;
        R26MatchEndResults shown;long shownEpoch=-1;double localEnds;float shownAt;int lastSecond=-1;
        public bool Visible{get{return panel!=null;}}public R26MatchEndResults Results{get{return shown;}}
        public void Initialize(R23PlayableLoop owner){root=owner;font=Resources.Load<Font>("R23InventoryUI");font.RequestCharactersInTexture("ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789 s",72,FontStyle.Normal);voice=gameObject.AddComponent<AudioSource>();voice.playOnAwake=false;voice.spatialBlend=0;voice.priority=64;shortCue=MakeCue();victory=Resources.Load<AudioClip>("R26MatchVictory");if(victory==null)Debug.LogWarning("R26 victory cue unavailable: R26MatchVictory resource missing");}
        void LateUpdate()
        {
            if(root==null||!root.Ready||root.Lobby==null||root.Lobby.Visible||root.ActualMatchRules==null){Clear();return;}
            var lobby=root.LanSession==null?null:root.LanSession.Lobby;R26MatchEndResults result=null;long epoch=root.MatchEpoch;double now=Time.unscaledTimeAsDouble;
            if(lobby!=null){epoch=lobby.Epoch;now=lobby.EstimatedHostTime;var selection=lobby.SelectedMap;if(selection!=null&&selection.Results)result=lobby.MatchEndResults;}
            else if(!root.MatchActive){if(shown==null||shownEpoch!=epoch){var world=root.ReadWorldScoreState();var rows=new List<R26MatchEndRow>();var teams=new Dictionary<int,int>();foreach(var p in world.Players)rows.Add(new R26MatchEndRow(p.Id,p.Name,p.Team,p.Kills??0,p.Deaths??0,p.Score??0,0,0));foreach(var t in world.Rows)if(t.Team>=0)teams[t.Team]=t.Score??0;localEnds=now+R26MatchEndResults.Duration;result=R26MatchEndResults.Rank(localEnds,world.Teams>0,rows,teams);}else result=shown;}
            if(result==null){Clear();return;}
            if(shown==null||shown.EndsAt!=result.EndsAt||shownEpoch!=epoch){Clear();shown=result;shownEpoch=epoch;Show(result.ForActor(lobby==null?"player":lobby.OwnActor));}
            if(panel==null)return;StepConfetti();voice.volume=R23MenuAudioEdges.Gain(root.UI.Settings.SfxVolume);
            int seconds=Math.Max(0,(int)Math.Ceiling(result.EndsAt-now));if(seconds!=lastSecond){lastSecond=seconds;countdown.text="WARMUP IN "+seconds+" s";}
            if(lobby==null&&now>=localEnds){Clear();root.ReturnToMapLobby();}
        }
        void Show(R26MatchEndRow row)
        {
            if(row==null)return;shownAt=Time.unscaledTime;lastSecond=-1;
            root.Gameplay.HolsterCurrent();root.Consumables.Stow();if(root.Explosives!=null&&root.Explosives.FlashSmoke!=null)root.Explosives.FlashSmoke.Release();root.UI.SetPlaying();
            font=Resources.Load<Font>("R23InventoryUI");var shader=Resources.Load<Shader>("R23VrUi");
            flat=new Material(shader);flat.renderQueue=4110;flat.color=Hex(R25Theme.Panel);
            ink=new Material(shader);ink.SetFloat("_Font",1);ink.renderQueue=4111;ink.mainTexture=font.material.mainTexture;Font.textureRebuilt+=RefreshFont;
            panel=new GameObject("R26 match results / fixed world anchor");var view=root.Rig.Head.transform;Vector3 forward=Vector3.ProjectOnPlane(view.forward,Vector3.up).normalized;if(forward.sqrMagnitude<.5f)forward=Vector3.forward;
            panel.transform.SetPositionAndRotation(view.position+forward*1.8f,Quaternion.LookRotation(forward,Vector3.up));
            var backing=GameObject.CreatePrimitive(PrimitiveType.Quad);backing.layer=5;backing.name="Results backing";Destroy(backing.GetComponent<Collider>());backing.transform.SetParent(panel.transform,false);backing.transform.localScale=new Vector3(1.25f,.88f,1);backing.GetComponent<Renderer>().sharedMaterial=flat;
            string placement=R26MatchEndResults.Placement(row.Rank).ToUpperInvariant();string title=shown.Teams?(row.Outcome>0?"VICTORY":row.Outcome==0?"DRAW":"DEFEAT"):placement+" PLACE";
            Label(title,.27f,.09f,row.Outcome>0?Hex(R25Theme.EmberHot):Hex(R25Theme.Text));
            Label(shown.Teams?placement+" IN ROOM":"MATCH COMPLETE",.13f,.036f,Hex(R25Theme.TextDim));
            Label("KILLS  "+row.Kills,-.025f,.055f,Hex(R25Theme.Text));Label("DEATHS  "+row.Deaths,-.14f,.055f,Hex(R25Theme.Text));countdown=Label("WARMUP IN 10 s",-.32f,.032f,Hex(R25Theme.TextDim));
            ink.mainTexture=font.material.mainTexture;voice.volume=R23MenuAudioEdges.Gain(root.UI.Settings.SfxVolume);voice.clip=row.Outcome>0&&victory!=null?victory:shortCue;if(voice.volume>0){voice.Play();if(R25NetBot.Requested)Debug.LogFormat("R26 MUSIC results clip={0} length={1:F3} channels={2} rate={3} load={4} playing={5} outcome={6} gain={7:F3}",voice.clip.name,voice.clip.length,voice.clip.channels,voice.clip.frequency,voice.clip.loadState,voice.isPlaying,row.Outcome,voice.volume);}if(row.Outcome>0)Burst();
            Debug.Log("R26 MATCHEND show epoch="+shownEpoch+" duration=10 actor="+row.Actor+" rank="+row.Rank+" kills="+row.Kills+" deaths="+row.Deaths+" outcome="+row.Outcome+" ends="+shown.EndsAt.ToString("F3",System.Globalization.CultureInfo.InvariantCulture));
            foreach(var p in shown.Rows)Debug.Log("R26 MATCHEND row actor="+p.Actor+" rank="+p.Rank+" kills="+p.Kills+" deaths="+p.Deaths+" outcome="+p.Outcome);
        }
        TextMesh Label(string value,float y,float height,Color color){var go=new GameObject("Result text");go.layer=5;go.transform.SetParent(panel.transform,false);go.transform.localPosition=new Vector3(0,y,-.008f);var text=go.AddComponent<TextMesh>();text.font=font;text.fontSize=72;text.characterSize=height/7;text.anchor=TextAnchor.MiddleCenter;text.alignment=TextAlignment.Center;text.color=color;text.text=value;go.GetComponent<Renderer>().sharedMaterial=ink;return text;}
        void RefreshFont(Font changed){if(changed==font&&ink!=null)ink.mainTexture=font.material.mainTexture;}
        void Burst()
        {
            // The project deliberately excludes Unity's particle module. A single dynamic mesh
            // supplies 48 ballistic confetti particles, 96 triangles and one draw call.
            const int count=48;var go=new GameObject("Winner confetti / 48 mesh particles");go.layer=5;go.transform.SetParent(panel.transform,false);
            confetti=new Mesh();confetti.name="R26 bounded confetti";confetti.MarkDynamic();confettiVertices=new Vector3[count*4];confettiOrigins=new Vector3[count];confettiVelocities=new Vector3[count];var colors=new Color[count*4];var indices=new int[count*6];var random=new System.Random(26);
            for(int i=0;i<count;i++){confettiOrigins[i]=new Vector3((float)random.NextDouble()*1.4f-.7f,.3f+(float)random.NextDouble()*.35f,-.2f);confettiVelocities[i]=new Vector3((float)random.NextDouble()*.6f-.3f,.35f+(float)random.NextDouble()*.65f,0);for(int j=0;j<4;j++)colors[i*4+j]=Hex(i%2==0?R25Theme.EmberHot:R25Theme.Teal);int v=i*4,k=i*6;indices[k]=v;indices[k+1]=v+1;indices[k+2]=v+2;indices[k+3]=v;indices[k+4]=v+2;indices[k+5]=v+3;}
            confetti.vertices=confettiVertices;confetti.colors=colors;confetti.triangles=indices;go.AddComponent<MeshFilter>().sharedMesh=confetti;var renderer=go.AddComponent<MeshRenderer>();confettiMaterial=new Material(flat);confettiMaterial.color=Color.white;confettiMaterial.renderQueue=4112;renderer.sharedMaterial=confettiMaterial;renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;renderer.receiveShadows=false;burstAt=Time.unscaledTime;StepConfetti();
        }
        void StepConfetti(){if(confetti==null)return;float t=Time.unscaledTime-burstAt;if(t>2.8f){confetti.Clear();return;}for(int i=0;i<confettiOrigins.Length;i++){Vector3 at=confettiOrigins[i]+confettiVelocities[i]*t+Vector3.down*(.45f*t*t);float angle=i+t*(i%2==0?4:-4);Vector3 x=new Vector3(Mathf.Cos(angle),Mathf.Sin(angle),0)*.011f,y=new Vector3(-x.y,x.x,0)*.6f;int v=i*4;confettiVertices[v]=at-x-y;confettiVertices[v+1]=at+x-y;confettiVertices[v+2]=at+x+y;confettiVertices[v+3]=at-x+y;}confetti.vertices=confettiVertices;confetti.RecalculateBounds();}

        static Color Hex(string value){Color color;ColorUtility.TryParseHtmlString("#"+value,out color);return color;}
        // Short two-note cue for draws, defeats, and a missing victory asset.
        static AudioClip MakeCue(){const int rate=22050;var samples=new float[rate];for(int i=0;i<samples.Length;i++){float t=i/(float)rate,local=t% .5f;float hz=t<.5f?392:523.25f;float envelope=Mathf.Min(1,local/.015f)*Mathf.Exp(-local*8);samples[i]=.16f*envelope*Mathf.Sin(2*Mathf.PI*hz*t);}var clip=AudioClip.Create("R26 results short cue",samples.Length,1,rate,false);clip.SetData(samples,0);return clip;}
        void Clear(){if(panel!=null){Debug.Log("R26 MATCHEND hide elapsed="+(Time.unscaledTime-shownAt).ToString("F3",System.Globalization.CultureInfo.InvariantCulture));Destroy(panel);}panel=null;countdown=null;shown=null;Font.textureRebuilt-=RefreshFont;if(ink!=null)Destroy(ink);if(flat!=null)Destroy(flat);if(confettiMaterial!=null)Destroy(confettiMaterial);ink=flat=confettiMaterial=null;if(confetti!=null)Destroy(confetti);confetti=null;if(voice!=null)voice.Stop();}
        void OnDisable(){Clear();}void OnDestroy(){Clear();if(shortCue!=null)Destroy(shortCue);if(voice!=null)Destroy(voice);}
    }
}
#endif
