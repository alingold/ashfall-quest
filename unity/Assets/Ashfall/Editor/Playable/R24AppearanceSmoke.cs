#if UNITY_EDITOR && UNITY_6000_0_OR_NEWER
using System;
using System.Collections;
using System.IO;
using Ashfall.Integration.Playable;
using UnityEngine;
namespace Ashfall.Editor.Playable
{
    public sealed class R24AppearanceSmoke
    {
        [Serializable] sealed class Failure { public bool success=false,nativeExecuted=true,headsetVerified=false;public string error=""; }
        public IEnumerator Start()
        {
            R23PlayableLoop loop=null;float end=Time.realtimeSinceStartup+120;
            while(Time.realtimeSinceStartup<end){loop=UnityEngine.Object.FindAnyObjectByType<R23PlayableLoop>();if(loop!=null&&(loop.Ready||loop.Failure!=null))break;yield return null;}
            if(loop==null||!loop.Ready){R23SmokeControl.Finish(false,JsonUtility.ToJson(new Failure{error=loop==null?"Actual playable root missing":loop.Failure??"Root timeout"},true));yield break;}
            string output=Path.ChangeExtension(R23PlayableBuild.Argument("-ashfallVRSmoke"),null)+"-images";
            IEnumerator work=R24MatchedAppearanceCapture.Run(loop,output);Exception error=null;
            try
            {
                while(true){bool moved=false;object current=null;try{moved=work.MoveNext();if(moved)current=work.Current;}catch(Exception e){error=e;break;}if(!moved)break;yield return current;}
            }
            finally{try{var disposable=work as IDisposable;if(disposable!=null)disposable.Dispose();}catch(Exception e){if(error==null)error=e;}}
            string receipt=Path.Combine(output,"matched-appearance.json");
            R23SmokeControl.Finish(error==null&&File.Exists(receipt),File.Exists(receipt)?File.ReadAllText(receipt):JsonUtility.ToJson(new Failure{error=error==null?"Capture receipt missing":error.ToString()},true));
        }
    }
}
#endif
