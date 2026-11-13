using System;using Ashfall.Integration.Playable.Calibration;
static class ManualHeightChecks
{
 static int n;static void Check(bool b,string why){n++;if(!b)throw new Exception(why);}static bool Near(float a,float b){return Math.Abs(a-b)<.00002f;}
 static void Main(){
 foreach(float height in new[]{.1f,.25f,.65f,.8f,1.15f,1.55f,1.9f,2.4f,4f})foreach(bool focused in new[]{true,false})foreach(bool paused in new[]{true,false})
 {
  var s=new R23CalibrationState();s.RequestRecalibration();Check(s.Sample(height,true,focused,paused,.0001f),"explicit current tracked seated/standing captures FIRST sample without focus/menu/stability gate");
  var receipt=s.Propose(height,height+.37f,.37f);s.Accept(receipt);Check(s.Calibrated&&!s.Sample(height,true,true,false,.01f)&&Near(height+receipt.BaseOriginY,R23CalibrationState.TargetEye),"exact source eye normalization/nonaccumulating origin/receipt acceptance");
  if(height>.2f)Check(Near(height-.1f+s.DesiredOriginY(height-.1f),R23CalibrationState.TargetEye-.1f),"centimetre physical crouch remains centimetre world crouch");
  if(height<3.9f)Check(Near(height+.1f+s.DesiredOriginY(height+.1f),R23CalibrationState.TargetEye),"source upper baseline correction retained");
  s.RequestRecalibration();float lower=Math.Max(.1f,height-.07f);Check(s.Sample(lower,true,false,true,.0001f),"explicit changed posture recaptures immediately");var next=s.Propose(lower,lower+receipt.BaseOriginY,receipt.BaseOriginY);Check(Near(lower+next.BaseOriginY,R23CalibrationState.TargetEye),"repeated Set Height never accumulates origin lift");
 }
 foreach(float bad in new[]{0,.09f,4.01f,float.NaN,float.PositiveInfinity}){var s=new R23CalibrationState();s.RequestRecalibration();Check(!s.Sample(bad,true,true,false,.01f)&&!s.Calibrated,"invalid source height not fabricated");bool refused=false;try{s.Propose(bad,bad,0);}catch(InvalidOperationException){refused=true;}Check(refused,"proposal requires a valid actual manual sample");}
 var loss=new R23CalibrationState();loss.RequestRecalibration();Check(!loss.Sample(1.15f,false,true,false,.01f)&&loss.Status=="Waiting for tracked height","untracked finite cached pose cannot calibrate");Check(loss.Sample(.65f,true,false,true,.01f),"first recovered actual tracking sample completes pending manual request");
 var invalid=new R23CalibrationState();invalid.RequestRecalibration();invalid.Sample(0,true,true,false,.01f);Check(invalid.Status!="Waiting for tracked height","valid tracking with invalid floor height is diagnosed separately");
 var boot=new R23CalibrationState();for(int i=0;i<4;i++)Check(!boot.Sample(.65f,true,true,false,.05f),"source boot still waits .25s");Check(boot.Sample(.65f,true,true,false,.05f),"source boot quarter-second unchanged");
 var moving=new R23CalibrationState();moving.Sample(.65f,true,true,false,.1f);moving.Sample(.65f,true,true,false,.1f);Check(!moving.Sample(.70f,true,true,false,.1f),"boot movement resets source stability");Check(!moving.Sample(.70f,true,false,false,.1f)&&Near(moving.Stability,0),"boot focus gate unchanged");
 Check(!R23CalibrationState.ValidNativePose(false,0,1.2f,0,0,0,0,1),"native finite pose must actually be tracked");Check(!R23CalibrationState.ValidNativePose(true,0,1.2f,0,0,0,0,0),"zero rotation is not full6DoF");Check(!R23CalibrationState.ValidNativePose(true,float.NaN,1.2f,0,0,0,0,1),"native nonfinite position refused");Check(R23CalibrationState.ValidNativePose(true,.4f,.65f,-.2f,0,0,0,1),"native Quest stage pose accepted without PC focus/display flags");
 Check(Near(R23CalibrationState.SafeRecoveryEye(.65f,R23CalibrationState.TargetEye,.08f),.73f),"native roof clearance still protects lift");Console.WriteLine("PASS "+n+" explicit seated-or-standing source calibration/tracking/boot/metric/clearance cases; native hardware unrun");}
}
