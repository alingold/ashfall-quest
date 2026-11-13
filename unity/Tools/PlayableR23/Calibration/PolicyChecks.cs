using System;
using Ashfall.Integration.Playable.Calibration;
class PolicyChecks
{
    static int count;
    static void Check(bool condition,string label) { if(!condition)throw new Exception(label);count++; }
    static bool Near(float a,float b) { return Math.Abs(a-b)<.00001f; }
    static void Stable(R23CalibrationState state,float raw) { for(int i=0;i<4;i++)Check(!state.Sample(raw,true,true,false,.05f),"No early measurement");Check(state.Sample(raw,true,true,false,.05f),"Stable quarter second"); }
    static void Main()
    {
        var boot=new R23CalibrationState();
        Stable(boot,1.6f);var receipt=boot.Propose(1.6f,1.6f,0);
        Check(Near(receipt.TargetEye,1.7088f)&&Near(receipt.BaseOriginY,.1088f),"Head TOP six feet, not eye");
        boot.Accept(receipt);
        Check(!boot.Sample(1.25f,true,true,false,.1f)&&Near(boot.Receipt.TrackedStandingEye,1.6f),"Crouch never recaptures baseline");
        Check(Near(boot.DesiredOriginY(1.5f),.1088f),"Ten centimetre crouch remains ten centimetres");
        Check(Near(boot.DesiredOriginY(1.7f),.0088f),"Above-baseline origin correction source behavior");
        Check(Near(R23CalibrationState.SafeRecoveryEye(1.4f,1.7088f,.08f),1.48f),"Ceiling limits upward recovery");
        Check(Near(R23CalibrationState.SafeRecoveryEye(1.4f,1.2f,0),1.2f),"Lowering remains available");
        Check(Near(R23CalibrationState.SafeRecoveryEye(1.4f,1.7088f,0),1.4f),"Initial overlap prevents upward push");
        var restored=new R23CalibrationState(receipt);Stable(restored,1.0f);
        Check(object.ReferenceEquals(restored.Propose(1.0f,1.0f,0),receipt),"Restore saved standing height while crouching without replacing it");
        restored.Accept(receipt);restored.RequestRecalibration();
        for(int i=0;i<10;i++)Check(!restored.Sample(1.3f,true,true,false,.1f),"Manual transient crouch not persisted");
        Stable(restored,1.65f);var fresh=restored.Propose(1.65f,1.7588f,.1088f);restored.Accept(fresh);
        Check(Near(fresh.TrackedStandingEye,1.65f)&&Near(fresh.BaseOriginY,.0588f),"Manual forces fresh tracked baseline without accumulating old lift");
        var loss=new R23CalibrationState();Check(!loss.Sample(1.6f,true,true,false,.1f),"Partial sample");
        Check(!loss.Sample(1.6f,false,true,false,.1f),"Tracking loss resets stability");
        Stable(loss,1.6f);
        var movement=new R23CalibrationState();movement.Sample(1.6f,true,true,false,.1f);movement.Sample(1.6f,true,true,false,.1f);
        Check(!movement.Sample(1.64f,true,true,false,.1f),"Motion beyond25mm resets stability");
        Check(!movement.Sample(1.64f,true,false,false,.1f)&&Near(movement.Stability,0),"Focus loss resets stability");
        Check(!movement.Sample(1.64f,true,true,true,.1f)&&Near(movement.Stability,0),"Pause suspends calibration");
        Check(!new R23CalibrationState().Sample(.9f,true,true,false,.1f),"First boot rejects deep crouch as standing");
        bool rejected=false;try{new R23CalibrationReceipt(0,1.6f,1.8288f,0);}catch(ArgumentException){rejected=true;}Check(rejected,"Wrong receipt version rejected");
        rejected=false;try{new R23CalibrationReceipt(1,float.NaN,1.8288f,0);}catch(ArgumentException){rejected=true;}Check(rejected,"Corrupt receipt rejected");
        rejected=false;try{new R23CalibrationReceipt(1,1.6f,1.8288f,1.4f);}catch(ArgumentException){rejected=true;}Check(rejected,"Finite but inconsistent lift rejected");
        rejected=false;try{boot.Sample(1.6f,true,true,false,float.NaN);}catch(ArgumentOutOfRangeException){rejected=true;}Check(rejected,"Nonfinite dt rejected before mutation");
        foreach(var field in typeof(R23CalibrationReceipt).GetFields())Check(field.IsInitOnly||field.IsLiteral,"Immutable height receipt");
        Console.WriteLine("PASS "+count+" calibration assertions: stable boot, source top/eye semantics, crouch preservation, restore/manual baseline, ceiling policy, focus/tracking/pause, receipt validation");
    }
}
