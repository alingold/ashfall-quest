using System;
using System.Text;
using System.Collections.Generic;
namespace Ashfall.Modules.Avatar
{
 public sealed class SourceBodyProfile
 {
  public readonly float EyeToShoulder,BodyRear;public readonly bool ChestPlateFollowsJacket;public readonly string StyleId,SourceSha256;
  SourceBodyProfile(string style,string hash,float eye,float rear,bool follow){StyleId=style;SourceSha256=hash;EyeToShoulder=eye;BodyRear=rear;ChestPlateFollowsJacket=follow;}
  public static SourceBodyProfile Game(byte[] registry,string expectedSha,string style){SkinJson.Check(registry!=null&&registry.Length<=512*1024&&style!=null,"Profile source budget");var b=(byte[])registry.Clone();string hash=ManifestXml.Sha(b);SkinJson.Check(hash==expectedSha,"Profile source identity");string text=new UTF8Encoding(false,true).GetString(b);string[] lines=text.Split('\n');string json=null;bool known=false;foreach(string line in lines)if(line.StartsWith("const STYLE_KEYS := ",StringComparison.Ordinal)){var doc=SkinJson.Read(Encoding.UTF8.GetBytes("{\"styles\":"+line.Substring(20).Trim()+"}"));var styles=doc["styles"] as object[];SkinJson.Check(styles!=null,"Style keys array");foreach(object key in styles)if(Object.Equals(key,style))known=true;}SkinJson.Check(known,"Unknown source style");foreach(string line in lines)if(line.StartsWith("const EXTRA_KITS := ",StringComparison.Ordinal)){SkinJson.Check(json==null,"Duplicate kit registry");json=line.Substring(20).Trim();}SkinJson.Check(json!=null,"Missing kit registry");var kits=SkinJson.Read(Encoding.UTF8.GetBytes(json));object value;if(kits.TryGetValue(style,out value)){var kit=value as Dictionary<string,object>;SkinJson.Check(kit!=null,"Profile kit type");object eye=null,rear=null,follow=null;SkinJson.Check(kit.TryGetValue("eye_to_shoulder",out eye)&&kit.TryGetValue("body_rear",out rear),"Profile fields");bool follows=kit.TryGetValue("chestplate_follow_jacket",out follow)&&Object.Equals(follow,true);SkinJson.Check(eye is double&&rear is double,"Profile numeric type");float e=(float)(double)eye,r=(float)(double)rear;SkinJson.Check(e>0&&e<1&&Math.Abs(r)<1,"Profile range");return new SourceBodyProfile(style,hash,e,r,follows);}return new SourceBodyProfile(style,hash,style=="obsidian"?.18f:.29f,style=="obsidian"?.06f:.12f,false);}
  public static SourceBodyProfile QuestFixed(string style,string pinnedPlayerBodySha){SkinJson.Check(style!=null&&pinnedPlayerBodySha!=null&&pinnedPlayerBodySha.Length==64,"Quest identity");return new SourceBodyProfile(style,pinnedPlayerBodySha,.29f,.12f,false);}
  public void Require(AvatarAssembly assembly){SkinJson.Check(assembly!=null&&assembly.StyleId==StyleId&&assembly.EyeToShoulder==EyeToShoulder&&assembly.BodyRear==BodyRear&&assembly.ChestPlateFollowsJacket==ChestPlateFollowsJacket,"Source body profile mismatch");}
 }
 public static class SourceGripMount
 {
  public static float[] Create(VerifiedSkin skin,bool left){SkinJson.Check(skin!=null,"Mount skin");string suffix=left?"_L":"_R";var names=skin.CopyNames();int wrist=Array.IndexOf(names,"Wrist"+suffix),middle=Array.IndexOf(names,"Middle_Proximal"+suffix);SkinJson.Check(wrist>=0&&middle>=0,"Mount source bones");var world=skin.WorldMatrices(skin.CopyLocalMatrices());var mount=SkinMatrix.Identity();mount[0]=0;mount[5]=0;mount[1]=left?1:-1;mount[4]=left?-1:1;var palm=new float[3];for(int k=0;k<3;k++)palm[k]=(world[wrist][k*4+3]+world[middle][k*4+3])*.5f;for(int r=0;r<3;r++){float rotated=0;for(int c=0;c<3;c++)rotated+=mount[r*4+c]*palm[c];mount[r*4+3]=(r==1?.032f:0)-rotated;}return mount;}
 }
}

