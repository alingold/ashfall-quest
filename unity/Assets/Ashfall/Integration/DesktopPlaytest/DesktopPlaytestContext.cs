#if UNITY_6000_0_OR_NEWER
using System;
using System.Collections.Generic;
using UnityEngine;
namespace Ashfall.Integration.DesktopPlaytest
{
    public interface IDesktopPlaytestModule { void Initialize(DesktopPlaytestContext context); }
    // Local synthetic desktop sandbox only; no network or production asset authority.
    public sealed class DesktopPlaytestContext
    {
        public Vector3 Spawn=new Vector3(0,1.1f,-8);
        public Transform PlayerRoot;
        public CharacterController Controller;
        public Camera View;
        public bool Paused;
        public string MovementStatus="Movement initializing",CombatStatus="Combat disabled",ConstructionStatus="Construction disabled";
        public int Hits,RemainingTargets;
        public readonly List<Collider> Targets=new List<Collider>();
        public bool Retired {get;private set;}
        public event Action ResetRequested;
        public void RequestReset(){if(Retired)return;var reset=ResetRequested;if(reset!=null)reset();}
        public void Retire(){Retired=true;Paused=true;ResetRequested=null;}
        public void RequestQuit(){Cursor.lockState=CursorLockMode.None;Cursor.visible=true;Application.Quit();}
    }
}
#endif
