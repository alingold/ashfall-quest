#if UNITY_6000_0_OR_NEWER
using System;
using Ashfall.Core;
using Ashfall.Modules.Modes;
using Ashfall.Integration.Playable.MatchZone;
using Ashfall.Integration.Playable.Networking;
using UnityEngine;

namespace Ashfall.Integration.Playable.Input
{
    public sealed partial class R24TacticalMap
    {
        void Rect(float x,float y,float w,float h,Color color)
        {
            int n=vertices.Count;vertices.Add(new Vector3(x,-y,0));vertices.Add(new Vector3(x,-y-h,0));vertices.Add(new Vector3(x+w,-y-h,0));vertices.Add(new Vector3(x+w,-y,0));
            for(int i=0;i<4;i++)colors.Add(color);indices.AddRange(new[]{n,n+2,n+1,n,n+3,n+2});
        }
        void Line(Vector2 a,Vector2 b,float width,Color color)
        {
            var d=b-a;if(d.sqrMagnitude<.000001f)return;var side=new Vector2(-d.y,d.x).normalized*width*.5f;int n=vertices.Count;
            foreach(var p in new[]{a+side,a-side,b-side,b+side})vertices.Add(new Vector3(p.x,-p.y,-.002f));for(int i=0;i<4;i++)colors.Add(color);indices.AddRange(new[]{n,n+2,n+1,n,n+3,n+2});
        }
        void Circle(Vector2 p,float radius,Color color)
        {
            int n=vertices.Count;vertices.Add(new Vector3(p.x,-p.y,-.003f));colors.Add(color);
            for(int i=0;i<=24;i++){float a=i*Mathf.PI/12;vertices.Add(new Vector3(p.x+Mathf.Cos(a)*radius,-p.y-Mathf.Sin(a)*radius,-.003f));colors.Add(color);if(i>0)indices.AddRange(new[]{n,n+i,n+i+1});}
        }
        void Triangle(Vector2 a,Vector2 b,Vector2 c,Color color)
        {int n=vertices.Count;foreach(var p in new[]{a,b,c}){vertices.Add(new Vector3(p.x,-p.y,-.004f));colors.Add(color);}indices.AddRange(new[]{n,n+1,n+2});}
        void Text(string value,float x,float y,int size,Color tint,float maximum=950)
        {
            TextMesh t;if(textCount==texts.Count){var go=new GameObject("Actual tactical label");go.layer=5;go.transform.SetParent(canvas,false);t=go.AddComponent<TextMesh>();t.font=font;t.richText=false;t.characterSize=10;t.anchor=TextAnchor.MiddleLeft;go.GetComponent<MeshRenderer>().sharedMaterial=letters;texts.Add(t);}else t=texts[textCount];textCount++;
            t.gameObject.SetActive(true);t.fontSize=size;t.text=value;t.color=tint;t.transform.localPosition=new Vector3(x,-y,-.006f);t.transform.localScale=Vector3.one;font.RequestCharactersInTexture(value,size,FontStyle.Normal);float width=t.GetComponent<MeshRenderer>().localBounds.size.x;if(width>maximum)t.transform.localScale=Vector3.one*(maximum/width);
        }
        void Image(Mesh mesh,float x,float y,float w,float h,Color tint)
        {
            mesh.Clear();mesh.vertices=new[]{new Vector3(x,-y,-.001f),new Vector3(x,-y-h,-.001f),new Vector3(x+w,-y-h,-.001f),new Vector3(x+w,-y,-.001f)};mesh.uv=new[]{Vector2.up,Vector2.zero,Vector2.right,Vector2.one};mesh.colors=new[]{tint,tint,tint,tint};mesh.triangles=new[]{0,2,1,0,3,2};mesh.RecalculateBounds();
        }
        Vector2 Pixel(Vector3 world){MapPoint p=projection.Pixel(F(world));return new Vector2((float)p.X,(float)p.Y);}
        void Zone(R24ZoneSquare square,Color color,float width)
        {
            float x=310+(float)((square.X-info.CatalogMinX)/(info.CatalogMaxX-info.CatalogMinX)*650),y=100+(float)((square.Z-info.CatalogMinZ)/(info.CatalogMaxZ-info.CatalogMinZ)*650),w=(float)(square.Size/(info.CatalogMaxX-info.CatalogMinX)*650),h=(float)(square.Size/(info.CatalogMaxZ-info.CatalogMinZ)*650);
            var a=new Vector2(x,y);var b=a+new Vector2(w,0);var c=a+new Vector2(w,h);var d=a+new Vector2(0,h);Line(a,b,width,color);Line(b,c,width,color);Line(c,d,width,color);Line(d,a,width,color);
        }
        void Marker(string title,Vector3 position,Color color,bool local)
        {
            Vector2 p=Pixel(position);Circle(p,8,color);Text(title,p.x+12,p.y-8,18,color,160);
            if(local){PlayerPixel=p;var h=TacticalProjection.Heading(F(root.Map.transform.InverseTransformDirection(root.Rig.Head.transform.forward)));PlayerHeading=new Vector2((float)h.X,(float)h.Y);var side=new Vector2(-PlayerHeading.y,PlayerHeading.x);Triangle(p+PlayerHeading*17,p-PlayerHeading*3+side*6,p-PlayerHeading*3-side*6,color);}
        }
        public void Refresh(){if(Opened){Follow();Draw();}}
        void Draw()
        {
            vertices.Clear();colors.Clear();indices.Clear();textCount=0;
            // Source 1400x850 panel, exact map rectangle310/100/650/650.
            Rect(0,0,1400,850,new Color(.02f,.055f,.075f,.96f));Rect(310,100,650,650,Hex("0c2530"));
            for(int i=1;i<10;i++){float f=i*65;Line(new Vector2(310+f,100),new Vector2(310+f,750),1,Hex("23434d"));Line(new Vector2(310,100+f),new Vector2(960,100+f),1,Hex("23434d"));}
            Line(new Vector2(310,100),new Vector2(960,100),2,Hex("337286"));Line(new Vector2(960,100),new Vector2(960,750),2,Hex("337286"));Line(new Vector2(960,750),new Vector2(310,750),2,Hex("337286"));Line(new Vector2(310,750),new Vector2(310,100),2,Hex("337286"));
            // Background behind the image must render first; grid/markers after.
            DrawBackground();
            Image(imageMesh,310,100,650,650,imageMaterial.mainTexture==null?Color.clear:new Color(.7f,.8f,.85f));Image(previewMesh,1000,245,355,235,previewMaterial.mainTexture==null?Color.clear:Color.white);
            Text(root.Map.Title,38,54,27,Color.white,925);
            var lobby=root.LanSession==null?null:root.LanSession.Lobby;bool session=lobby!=null&&(lobby.State==R24LanLobbyState.Hosting||lobby.State==R24LanLobbyState.Connected);
            Text(session?"PLAYERS":"YOU",38,119,23,Color.white,245);var own=Hex("ffce3c");string ownName="YOU";
            if(session){int row=0;foreach(var member in lobby.Members){bool local=member.Actor==lobby.OwnActor;Text(member.Name+(local?" (YOU)":""),30,166+row++*68,17,Color.white,245);if(local){ownName=member.Name;continue;}R24LanPoseSnapshot snapshot;if(!lobby.TryReadPresence(member.Actor,out snapshot)||!snapshot.Pose.Tracked||snapshot.Pose.Scene!="map:"+root.Map.ManifestSha256||Time.realtimeSinceStartupAsDouble-snapshot.ReceivedAt>1)continue;bool allied=root.ActualMatchRules!=null&&root.ActualMatchRules.Teams>0&&Allied(lobby,member.Team);Marker(member.Name,V(snapshot.Pose.Root.Position),Hex(allied?"41deed":"ff756e"),false);}}
            Marker(ownName,root.Rig.transform.position,own,true);
            if(root.MatchActive&&root.MatchZoneOwner!=null&&root.MatchZoneOwner.State!=null){Zone(root.MatchZoneOwner.State.Current(root.MatchClock),Hex("ff273e"),4);Zone(root.MatchZoneOwner.State.Next,Color.white,3);}
            var mark=root.TeamMarks==null?null:root.TeamMarks.Current;pingView.gameObject.SetActive(mark!=null);
            if(mark!=null){Vector2 at=Pixel(mark.Position);Line(PlayerPixel,at,5,new Color(.005f,.01f,.015f,.8f));Line(PlayerPixel,at,2.5f,Hex(R23TeamMarksState.ColorHex(mark.Index)));float size=mark.Index==4?60:12;pingMaterial.mainTexture=pingIcons[mark.Index];Image(pingMesh,at.x-size*.5f,at.y-size*.5f,size,size,Color.white);}
            drawing.Clear();drawing.SetVertices(vertices);drawing.SetColors(colors);drawing.SetTriangles(indices,0);drawing.RecalculateBounds();for(int i=textCount;i<texts.Count;i++)texts[i].gameObject.SetActive(false);letters.mainTexture=font.material.mainTexture;Draws++;
        }
        Mesh background;Material backgroundMaterial;
        void DrawBackground()
        {
            if(background==null){backgroundMaterial=new Material(flat);backgroundMaterial.renderQueue=4110;background=Part("Source panel background",backgroundMaterial,out _);}
            background.Clear();background.SetVertices(vertices.GetRange(0,8));background.SetColors(colors.GetRange(0,8));background.SetTriangles(indices.GetRange(0,12),0);background.RecalculateBounds();
            // Remove the two background quads from the later grid mesh so the
            // source image is not covered by an opaque late overlay.
            vertices.RemoveRange(0,8);colors.RemoveRange(0,8);indices.RemoveRange(0,12);for(int i=0;i<indices.Count;i++)indices[i]-=8;
        }
        static bool Allied(R24LanLobby lobby,int team){foreach(var member in lobby.Members)if(member.Actor==lobby.OwnActor)return member.Team==team;return false;}
        static Color Hex(string s){Color c;ColorUtility.TryParseHtmlString("#"+s,out c);return c;}
    }
}
#endif
