using System;
namespace Ashfall.Integration.Playable.Networking {
 public sealed class R24LanBallisticProfile {
  public readonly string Id;public readonly float Rate;public readonly int Pellets;readonly float[] body,head;
  R24LanBallisticProfile(string id,float rate,int pellets,float[] body,float[] head){Id=id;Rate=rate;Pellets=pellets;this.body=body;this.head=head;}
  public float Damage(int stars,bool headshot,float distance){if(stars<1||stars>5||distance<0||distance>180)return 0;float fall=Pellets>1?1-.75f*Math.Max(0,Math.Min(1,(distance-8)/17)):1;return (headshot?head:body)[stars-1]/Pellets*fall;}
  // Frozen R6 one_star_050 SHA fd665c932c001ce9005693bbe14cf0183cd7674a185bff500fde95d06e7138eb; rarity SHA fdf8727803a6b278b1acddeb7c5ae1faed2f89e15a42151d46a3792207054428.
  static readonly System.Collections.Generic.Dictionary<string,R24LanBallisticProfile> cached=new System.Collections.Generic.Dictionary<string,R24LanBallisticProfile>(StringComparer.Ordinal);
  public static bool TryRead(string id,out R24LanBallisticProfile result){result=null;if(id==null)return false;if(cached.TryGetValue(id,out result))return true;if(!Create(id,out result))return false;cached.Add(id,result);return true;}
  static bool Create(string id,out R24LanBallisticProfile result){switch(id){
   case "cx4":result=new R24LanBallisticProfile(id,6f,1,new float[]{19f,19f,19f,19f,19f},new float[]{23f,23f,23f,23f,23f});return true;
   case "dt11":result=new R24LanBallisticProfile(id,5f,8,new float[]{96f,96f,96f,96f,96f},new float[]{96f,96f,96f,96f,96f});return true;
   case "px4":result=new R24LanBallisticProfile(id,4.76190476f,1,new float[]{30f,31f,33f,34f,36f},new float[]{45f,50f,56f,62f,68f});return true;
   case "mk18":result=new R24LanBallisticProfile(id,12.5f,1,new float[]{22f,22f,22f,22f,22f},new float[]{33f,33f,33f,33f,33f});return true;
   case "awp":result=new R24LanBallisticProfile(id,1.05263158f,1,new float[]{70f,78f,87f,95f,104f},new float[]{117f,132f,148f,164f,180f});return true;
   case "ump":result=new R24LanBallisticProfile(id,14f,1,new float[]{19f,19f,19f,19f,19f},new float[]{23f,23f,23f,23f,23f});return true;
   case "magnum":result=new R24LanBallisticProfile(id,3f,1,new float[]{63f,67f,71f,75f,79f},new float[]{81f,93f,106f,119f,134f});return true;
   case "m60":result=new R24LanBallisticProfile(id,7.2f,1,new float[]{27f,27f,27f,27f,27f},new float[]{33f,33f,33f,33f,33f});return true;
   case "m1014":result=new R24LanBallisticProfile(id,3.43977527f,8,new float[]{77f,77f,77f,77f,77f},new float[]{77f,77f,77f,77f,77f});return true;
   case "p90":result=new R24LanBallisticProfile(id,15f,1,new float[]{20f,20f,20f,20f,20f},new float[]{22f,22f,22f,22f,22f});return true;
   case "uzis":result=new R24LanBallisticProfile(id,13f,1,new float[]{14f,14f,14f,14f,14f},new float[]{14f,14f,14f,14f,14f});return true;
   case "matadors":result=new R24LanBallisticProfile(id,3.125f,8,new float[]{20f,24f,28f,32f,36f},new float[]{20f,24f,28f,32f,36f});return true;
   case "spr":result=new R24LanBallisticProfile(id,3.125f,1,new float[]{55f,59f,63f,67f,71f},new float[]{92f,106f,121f,137f,154f});return true;
   case "mp5":result=new R24LanBallisticProfile(id,13f,1,new float[]{22f,22f,22f,22f,22f},new float[]{33f,33f,33f,33f,33f});return true;
   case "mp9":result=new R24LanBallisticProfile(id,12.5f,1,new float[]{21f,21f,21f,21f,21f},new float[]{26f,26f,26f,26f,26f});return true;
   case "1911":result=new R24LanBallisticProfile(id,5f,1,new float[]{44f,45f,47f,49f,51f},new float[]{61f,64f,68f,71f,75f});return true;
   case "tec9":result=new R24LanBallisticProfile(id,14.4f,1,new float[]{25f,26f,27f,28f,29f},new float[]{30f,32f,35f,37f,40f});return true;
   case "akm":result=new R24LanBallisticProfile(id,11f,1,new float[]{20f,20f,20f,20f,20f},new float[]{30f,30f,30f,30f,30f});return true;
   case "rfb":result=new R24LanBallisticProfile(id,9.5f,1,new float[]{19f,19f,19f,19f,19f},new float[]{23f,23f,23f,23f,23f});return true;
   case "fal":result=new R24LanBallisticProfile(id,5f,1,new float[]{33f,33f,33f,33f,33f},new float[]{44f,44f,44f,44f,44f});return true;
   case "s85":result=new R24LanBallisticProfile(id,1.4f,1,new float[]{65f,69f,73f,77f,81f},new float[]{105f,119f,133f,147f,161f});return true;
   default:result=null;return false;}}
 }
}
