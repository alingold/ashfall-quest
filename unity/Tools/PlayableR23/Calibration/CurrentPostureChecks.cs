using System;
using Ashfall.Integration.Playable.Calibration;
class CurrentPostureChecks
{
    static void Need(bool value,string message){if(!value)throw new Exception(message);}
    static R23CalibrationReceipt Capture(R23CalibrationState state,float raw,float currentOrigin)
    {
        for(int i=0;i<4;i++)Need(!state.Sample(raw,true,true,false,.05f),"Wait for source stable sample window");
        Need(state.Sample(raw,true,true,false,.05f),"Any valid stable posture can initialize");
        var receipt=state.Propose(raw,raw+currentOrigin,currentOrigin);state.Accept(receipt);return receipt;
    }
    static bool Near(float a,float b){return Math.Abs(a-b)<.00002f;}
    static void Main()
    {
        foreach(float raw in new[]{.1f,.65f,1.15f,1.55f,1.9f,2.4f,4f})
        {
            var state=new R23CalibrationState();var receipt=Capture(state,raw,0);
            Need(Near(raw+receipt.BaseOriginY,R23CalibrationState.TargetEye),"Seated/tall eye normalized to same source target");
            if(raw>.2f)Need(Near(raw-.1f+state.DesiredOriginY(raw-.1f),R23CalibrationState.TargetEye-.1f),"Physical ten-centimetre crouch remains ten centimetres");
            if(raw<3.9f)Need(Near(raw+.1f+state.DesiredOriginY(raw+.1f),R23CalibrationState.TargetEye),"Source above-baseline ceiling correction preserved");
        }
        var standing=new R23CalibrationState();var saved=Capture(standing,1.8f,0);
        var nextBoot=new R23CalibrationState(saved);var sitting=Capture(nextBoot,1.15f,0);
        Need(Near(sitting.TrackedStandingEye,1.15f),"New boot measures current posture instead of restoring old standing baseline");
        nextBoot.RequestRecalibration();var reclined=Capture(nextBoot,.8f,sitting.BaseOriginY);
        Need(Near(reclined.TrackedStandingEye,.8f)&&Near(.8f+reclined.BaseOriginY,R23CalibrationState.TargetEye),"Explicit seated recalibration does not require old stance or accumulate lift");
        Need(!new R23CalibrationState().Sample(.09f,true,true,false,.1f),"Invalid raw pose rejected");
        Need(!new R23CalibrationState().Sample(float.NaN,true,true,false,.1f),"Nonfinite pose rejected");
        var loss=new R23CalibrationState();loss.Sample(1.15f,true,true,false,.1f);loss.Sample(1.15f,true,true,false,.1f);
        Need(!loss.Sample(1.15f,false,true,false,.1f)&&Near(loss.Stability,0),"Tracking loss clears partial normalization");
        Need(Near(R23CalibrationState.SafeRecoveryEye(1.2f,1.7088f,.08f),1.28f),"Native ceiling still limits recovery");
        Console.WriteLine("PASS current-posture source calibration: seated/tall boot, manual seated recapture, metric crouch, upper clamp, tracking loss and ceiling");
    }
}
