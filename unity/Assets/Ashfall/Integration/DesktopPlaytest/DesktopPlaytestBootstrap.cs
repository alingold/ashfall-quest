#if UNITY_6000_0_OR_NEWER
using System;
using System.Collections;
using System.IO;
using UnityEngine;
namespace Ashfall.Integration.DesktopPlaytest
{
    // Explicit root-authored scene entry, only for this synthetic desktop milestone.
    public sealed class DesktopPlaytestBootstrap : MonoBehaviour
    {
        public Material[] ShaderRetention=new Material[0];
        public bool EnableConstruction=true;
        public DesktopPlaytestContext Context {get;private set;}
        string failure;
        float elapsed;
        bool smokeWritten;
        bool started,retiring;
        readonly System.Collections.Generic.List<GameObject> ownedModules=new System.Collections.Generic.List<GameObject>();
#if UNITY_EDITOR
        internal string NativeFailureAfter="";
#endif
        public void Awake()
        {
            if(started)return;started=true;
            try
            {
                Context=new DesktopPlaytestContext();
                Add<Arena.DesktopArena>("Arena");Add<Pawn.DesktopPawn>("Pawn");
                Add<Combat.DesktopWeapons>("Combat");
                if(EnableConstruction)Add<Construction.DesktopConstruction>("Construction");
                Add<Hud.DesktopHud>("Hud");
                Context.View.tag="MainCamera";
                Debug.Log("ASHFALL desktop sandbox initialized: movement, rifle, five targets; construction="+EnableConstruction);
            }
            catch(Exception e){failure=e.ToString();CleanupOwned();Debug.LogException(e);Cursor.lockState=CursorLockMode.None;Cursor.visible=true;}
        }
        void Add<T>(string label) where T:MonoBehaviour,IDesktopPlaytestModule
        {
            var child=new GameObject(label);ownedModules.Add(child);child.transform.SetParent(transform,false);child.AddComponent<T>().Initialize(Context);
#if UNITY_EDITOR
            if(NativeFailureAfter==label)throw new InvalidOperationException("Injected desktop startup failure after "+label);
#endif
        }
        void CleanupOwned()
        {
            if(retiring)return;retiring=true;
            if(Context!=null)Context.Retire();
            for(int i=ownedModules.Count-1;i>=0;i--){var child=ownedModules[i];if(!child)continue;child.SetActive(false);Destroy(child);}
            ownedModules.Clear();
        }
        void OnDisable(){CleanupOwned();}
        void OnDestroy(){CleanupOwned();}
        void Update()
        {
            if(smokeWritten||Array.IndexOf(Environment.GetCommandLineArgs(),"-ashfallSmoke")<0)return;
            elapsed+=Time.unscaledDeltaTime;if(elapsed<5)return;smokeWritten=true;
            bool okay=failure==null&&Context!=null&&Context.PlayerRoot&&Context.View&&Context.Controller&&Context.Targets.Count==5;
            var displays=new System.Collections.Generic.List<UnityEngine.XR.XRDisplaySubsystem>();SubsystemManager.GetSubsystems(displays);
            foreach(var display in displays)if(display.running)okay=false;
            string[] args=Environment.GetCommandLineArgs();int at=Array.IndexOf(args,"-ashfallSmokeReceipt");
            try
            {
                if(at>=0&&at+1<args.Length)
                using(var file=new FileStream(args[at+1],FileMode.CreateNew,FileAccess.Write,FileShare.Read))
                using(var writer=new StreamWriter(file))writer.WriteLine("{\"passed\":"+(okay?"true":"false")+",\"targets\":"+(Context==null?0:Context.Targets.Count)+",\"scope\":\"standalone startup only; controls still require play validation\"}");
            }
            catch(Exception e){okay=false;Debug.LogException(e);}
            Debug.Log("ASHFALL_STANDALONE_SMOKE "+(okay?"PASS":"FAIL")+" targets="+(Context==null?0:Context.Targets.Count)+" failure="+failure);
            int screenshot=Array.IndexOf(args,"-ashfallScreenshot");
            if(screenshot>=0&&screenshot+1<args.Length)StartCoroutine(CaptureAndQuit(args[screenshot+1],okay));
            else Application.Quit(okay?0:2);
        }
        IEnumerator CaptureAndQuit(string path,bool okay)
        {
            yield return new WaitForEndOfFrame();
            Texture2D frame=null;
            try
            {
                frame=new Texture2D(Screen.width,Screen.height,TextureFormat.RGB24,false);
                var previous=RenderTexture.active;RenderTexture.active=null;
                try{frame.ReadPixels(new Rect(0,0,Screen.width,Screen.height),0,0,false);frame.Apply(false);}
                finally{RenderTexture.active=previous;}
                byte[] png=ImageConversion.EncodeToPNG(frame);
                using(var file=new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.Read))file.Write(png,0,png.Length);
                Debug.Log("ASHFALL standalone rendered-frame capture: "+path);
            }
            catch(Exception e){okay=false;Debug.LogException(e);}
            finally{if(frame)Destroy(frame);}
            Application.Quit(okay?0:2);
        }
        void OnGUI()
        {
            if(failure==null)return;
            GUI.Box(new Rect(20,20,Mathf.Max(300,Screen.width-40),Mathf.Max(200,Screen.height-40)),"ASHFALL playtest startup failed\n"+failure);
            if(GUI.Button(new Rect(40,Screen.height-80,180,40),"Quit"))Application.Quit();
        }
    }
}
#endif
