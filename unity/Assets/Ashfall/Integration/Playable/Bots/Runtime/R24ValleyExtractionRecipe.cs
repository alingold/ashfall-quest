using System;
using System.Collections.Generic;
using UnityEngine;

namespace Ashfall.Integration.Playable.Bots
{
    // Source world.gd boxes/hull remain in Godot coordinates until assembly.
    internal static class R24ValleyExtractionRecipe
    {
        internal enum Attachment {Body,LeftRotor,RightRotor}
        internal sealed class Box {internal Attachment Parent;internal string Material;internal Vector3 Position,Size,Rotation;}
        internal const string MapManifestSha256="dabe9b9ea8e82ad90f56e989180a28c9741f2e942ef646513e7fe66368032fb6";
        internal static readonly string[] MaterialKeys={"white","glass","trim","orange","steel","black","yellow","cyan","red"};
        internal static readonly string[] MaterialNames={"resource_01118","resource_01427","resource_00200","resource_01373","resource_00269","resource_00274","resource_00151","resource_00156","resource_00470"};
        internal static readonly Vector3[] HullRings={new Vector3(.55f,.40f,-2.7f),new Vector3(1.30f,.75f,-1.1f),new Vector3(1.25f,.70f,1.1f),new Vector3(.50f,.40f,2.6f)};
        internal static List<Box> Build()
        {
            var boxes=new List<Box>(48);var body=Attachment.Body;
            Action<Attachment,Vector3,Vector3,string,Vector3> add=(parent,at,size,material,rotation)=>boxes.Add(new Box{Parent=parent,Position=at,Size=size,Material=material,Rotation=rotation});
            add(body,new Vector3(0,.55f,-1.7f),new Vector3(1.8f,.75f,1.6f),"glass",new Vector3(-.16f,0,0));
            add(body,new Vector3(0,.96f,-1.7f),new Vector3(.07f,.08f,1.6f),"trim",Vector3.zero);
            add(body,new Vector3(0,.60f,-2.51f),new Vector3(.07f,.7f,.04f),"trim",Vector3.zero);
            foreach(float side in new[]{-1f,1f})
            {
                add(body,new Vector3(side*1.25f,0,.1f),new Vector3(.10f,1.5f,2.2f),"orange",Vector3.zero);
                add(body,new Vector3(side*1.32f,.38f,-.1f),new Vector3(.035f,.55f,1.15f),"glass",Vector3.zero);
                add(body,new Vector3(side*1.35f,-.12f,.7f),new Vector3(.06f,.10f,.35f),"white",Vector3.zero);
                add(body,new Vector3(side*1f,-1.2f,0),new Vector3(.18f,.16f,4.1f),"steel",Vector3.zero);
                foreach(float z in new[]{-1.2f,1.2f})add(body,new Vector3(side,-.85f,z),new Vector3(.13f,.7f,.13f),"trim",Vector3.zero);
                add(body,new Vector3(side*2.25f,.5f,.2f),new Vector3(2.6f,.24f,1.15f),"white",new Vector3(0,0,side*-.08f));
                add(body,new Vector3(side*3.4f,.7f,.2f),new Vector3(.65f,.75f,1.7f),"orange",Vector3.zero);
                add(body,new Vector3(side*3.4f,1.28f,.2f),new Vector3(.15f,.6f,.15f),"trim",Vector3.zero);
                for(int j=0;j<5;j++)add(body,new Vector3(side*3.4f,.48f+j*.11f,1.08f),new Vector3(.5f,.06f,.035f),"black",Vector3.zero);
                var rotor=side<0?Attachment.LeftRotor:Attachment.RightRotor;
                for(int blade=0;blade<3;blade++)
                {
                    float angle=blade*(float)Math.PI*2/3;var rotation=new Vector3(0,angle,0);
                    add(rotor,new Vector3((float)Math.Cos(angle)*1.05f,0,-(float)Math.Sin(angle)*1.05f),new Vector3(2.1f,.045f,.16f),"black",rotation);
                    add(rotor,new Vector3((float)Math.Cos(angle)*1.95f,.005f,-(float)Math.Sin(angle)*1.95f),new Vector3(.22f,.05f,.17f),"yellow",rotation);
                }
                add(body,new Vector3(side,-.4f,-2.3f),new Vector3(.25f,.14f,.12f),"cyan",Vector3.zero);
            }
            add(body,new Vector3(0,.55f,2.4f),new Vector3(.18f,1.8f,1.2f),"orange",new Vector3(-.3f,0,0));
            add(body,new Vector3(0,.22f,2.8f),new Vector3(3.2f,.12f,.8f),"white",Vector3.zero);
            add(body,new Vector3(0,1.43f,2.2f),new Vector3(.17f,.17f,.25f),"red",Vector3.zero);
            return boxes;
        }
    }
}
