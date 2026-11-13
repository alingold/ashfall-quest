using System;
using Ashfall.Core;
using Ashfall.Integration.Playable.Match;
using Ashfall.Integration.Playable.MatchZone;
class RecoveryCases
{
 static int checks;static void N(bool b,string m){checks++;if(!b)throw new Exception(m);}
 static void Main()
 {
  N(!R24GhostRecoveryMath.Outside("range",-25)&&R24GhostRecoveryMath.Outside("range",-25.001f),"source Range strict<-25");
  N(!R24GhostRecoveryMath.Outside("valley",-25)&&R24GhostRecoveryMath.Outside("valley",-100),"source Valley<-25");
  N(!R24GhostRecoveryMath.Outside("cyber_bird",-12)&&R24GhostRecoveryMath.Outside("cyber_bird",-12.001f),"source Cyber strict<-12");
  N(!R24GhostRecoveryMath.Outside("archive:alfa-aglae-souza",-1000),"archive _outside not replaced with generic floor");
  N(!R24GhostRecoveryMath.Outside("range",float.NaN),"nonfinite not relocated");
  var baseline=new Float3(0,.2f,-608);var p=R24GhostRecoveryMath.Candidates("range",baseline,new Float3(0,0,-600),null);
  N(p.Length==8,"source exact8arena candidates");
  for(int i=0;i<8;i++)N(p[i].X==-15+(i%4)*10&&Math.Abs(p[i].Y-2.2)<.00001&&p[i].Z==-602+(i/4)*30,"source reflected Range offset "+i);
  p=R24GhostRecoveryMath.Candidates("cyber_bird",baseline,new Float3(0,0,-2400),null);
  for(int i=0;i<8;i++)N(p[i].X==-55+(i%4)*35&&p[i].Y==1&&p[i].Z==-2730+(i/4)*660,"source Cyber original8cities "+i);
  var rect=new R24ZoneSquare(-70,-80,52);p=R24GhostRecoveryMath.Candidates("range",baseline,new Float3(4,3,-600),rect);
  N(p.Length==49,"source actual next.grow(-1)49grid");
  for(int i=0;i<49;i++)N(Math.Abs(p[i].X-(4-69+50*((i%7)+.5)/7))<.00001&&p[i].Y==103&&Math.Abs(p[i].Z-(-600+79-50*((i/7)+.5)/7))<.0001,"source next square single reflection "+i);
  for(int offset=0;offset<8;offset++){var seen=new bool[8];for(int i=0;i<8;i++)seen[R24GhostRecoveryMath.SourceOrder(i,8,offset,1)]=true;foreach(bool found in seen)N(found,"actual source sourceID+randomoffset order covers each once");}
  Console.WriteLine(checks+" exact source ghost thresholds/8candidate/cyber/49next-grid/reflection/order cases PASS");
 }
}
