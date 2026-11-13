using System;

namespace Ashfall.Integration.Playable.Calibration
{
    public enum R23CalibrationPending { None, Boot, Restore, Manual }

    public sealed class R23CalibrationReceipt
    {
        public const int CurrentVersion=1;
        public readonly int Version;
        public readonly float TrackedStandingEye, TargetHeadTop, BaseOriginY;
        public float TargetEye { get { return TargetHeadTop-R23CalibrationState.HeadTopToEye; } }
        public R23CalibrationReceipt(int version,float trackedStandingEye,float targetHeadTop,float baseOriginY)
        {
            if(version!=CurrentVersion||!R23CalibrationState.IsStandingHeight(trackedStandingEye)||
                !R23CalibrationState.Finite(targetHeadTop)||Math.Abs(targetHeadTop-R23CalibrationState.TargetHeadTop)>.0001f||
                !R23CalibrationState.Finite(baseOriginY)||Math.Abs(baseOriginY)>3||
                Math.Abs(baseOriginY-(R23CalibrationState.TargetEye-trackedStandingEye))>.002f)
                throw new ArgumentException("Invalid or incompatible standing-height receipt");
            Version=version;TrackedStandingEye=trackedStandingEye;TargetHeadTop=targetHeadTop;BaseOriginY=baseOriginY;
        }
    }

    // Pure source-derived measurement/receipt policy; native clearance is owned by the bridge.
    public sealed class R23CalibrationState
    {
        public const float TargetHeadTop=1.8288f,HeadTopToEye=.120f,TargetEye=TargetHeadTop-HeadTopToEye;
        public const float StableSeconds=.25f,StableBand=.025f;
        public const float StandingMinimum=1.25f,StandingMaximum=2.2f;
        float stableReference=-1,stableTime;
        readonly R23CalibrationReceipt saved;
        public R23CalibrationReceipt Receipt { get;private set; }
        public R23CalibrationPending Pending { get;private set; }
        public bool Calibrated { get { return Receipt!=null; } }
        public string Status { get;private set; }
        public float Stability { get { return Math.Min(1,stableTime/StableSeconds); } }
        public R23CalibrationState(R23CalibrationReceipt restored=null)
        { saved=restored;Pending=restored==null?R23CalibrationPending.Boot:R23CalibrationPending.Restore;Status="Waiting for tracked height"; }
        public static bool Finite(float value) { return !float.IsNaN(value)&&!float.IsInfinity(value); }
        public static bool ValidHeight(float value) { return Finite(value)&&value>=.1f&&value<=4; }
        public static bool IsStandingHeight(float value) { return Finite(value)&&value>=StandingMinimum&&value<=StandingMaximum; }
        public void RequestRecalibration() { Pending=R23CalibrationPending.Manual;ResetStability();Status="Stand upright and hold still"; }
        public void ResetStability() { stableReference=-1;stableTime=0; }
        public bool Sample(float raw,bool tracked,bool focused,bool paused,float seconds)
        {
            if(!Finite(seconds)||seconds<=0||seconds>.1f)throw new ArgumentOutOfRangeException("seconds");
            if(!tracked||!ValidHeight(raw)){ResetStability();Status="Waiting for tracked height";return false;}
            if(!focused||paused){ResetStability();Status=paused?"Calibration suspended":"Waiting for XR focus";return false;}
            if(Pending==R23CalibrationPending.None){Status="Calibrated";return false;}
            if(Pending!=R23CalibrationPending.Restore&&!IsStandingHeight(raw))
            {ResetStability();Status="Stand upright to calibrate";return false;}
            // A saved/accepted standing baseline cannot be replaced by a transient crouch.
            // Manual recalibration can update upright readings, but waits below the known stance.
            var known=Receipt??saved;
            if(Pending==R23CalibrationPending.Manual&&known!=null&&raw<known.TrackedStandingEye-.12f)
            {ResetStability();Status="Stand upright to recalibrate";return false;}
            if(stableReference<0||Math.Abs(raw-stableReference)>StableBand){stableReference=raw;stableTime=0;}
            stableTime+=seconds;Status="Hold still / calibrating";
            return stableTime+.000001f>=StableSeconds;
        }
        public R23CalibrationReceipt Propose(float raw,float rootEye,float originY)
        {
            if(Pending==R23CalibrationPending.None||stableTime+.000001f<StableSeconds)throw new InvalidOperationException("Stable measurement required");
            if(Pending==R23CalibrationPending.Restore)return saved;
            if(!IsStandingHeight(raw)||!Finite(rootEye)||!Finite(originY))throw new ArgumentException("Invalid measurement");
            return new R23CalibrationReceipt(R23CalibrationReceipt.CurrentVersion,raw,TargetHeadTop,originY+TargetEye-rootEye);
        }
        public void Accept(R23CalibrationReceipt receipt)
        { if(receipt==null)throw new ArgumentNullException("receipt");Receipt=receipt;Pending=R23CalibrationPending.None;ResetStability();Status="Calibrated"; }
        public float DesiredOriginY(float raw)
        {
            if(!Calibrated||!ValidHeight(raw))throw new InvalidOperationException("Valid tracked calibration required");
            return Receipt.BaseOriginY-Math.Max(0,raw-Receipt.TrackedStandingEye);
        }
        public static float SafeRecoveryEye(float previousEye,float desiredEye,float allowedRise)
        {
            if(!Finite(previousEye)||!Finite(desiredEye)||!Finite(allowedRise)||allowedRise<0)throw new ArgumentOutOfRangeException("allowedRise");
            return desiredEye<=previousEye?desiredEye:Math.Min(desiredEye,previousEye+allowedRise);
        }
    }
}
