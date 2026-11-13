#if UNITY_EDITOR && UNITY_6000_0_OR_NEWER
using System;
using System.Collections;
using System.Collections.Generic;
using Ashfall.Integration.Playable;
using UnityEngine;
namespace Ashfall.Editor.Playable
{
    // One finite native pass over the actual newly integrated UI and melee
    // owners. No reflection, replacement floor, invented actor or fake damage.
    public sealed class R24R6GameplaySmoke
    {
        [Serializable]sealed class Result
        {public bool success,uiPassed,knifePassed,dt11Passed,awpPassed,m60Passed,dualPassed,nativeExecuted,headsetVerified=false;public string error="",uiError="",knifeError="",dt11Error="",awpError="",m60Error="",dualError="",uiJson="",startedUtc="",finishedUtc="";public List<string> knife=new List<string>(),dt11=new List<string>(),awp=new List<string>(),m60=new List<string>(),dual=new List<string>();}
        public IEnumerator Run()
        {
            var result=new Result{startedUtc=DateTime.UtcNow.ToString("o")};R23PlayableLoop loop=null;float end=Time.realtimeSinceStartup+120;
            while(Time.realtimeSinceStartup<end){loop=UnityEngine.Object.FindAnyObjectByType<R23PlayableLoop>();if(loop!=null&&(loop.Ready||loop.Failure!=null))break;yield return null;}
            if(loop==null||!loop.Ready){result.error=loop==null?"Actual playable root missing":loop.Failure??"Root timeout";Finish(result);yield break;}
            if(UnityEngine.XR.XRSettings.isDeviceActive){result.error="XR-off native gameplay fixture required";Finish(result);yield break;}
            result.nativeExecuted=true;
            var ui=new R24R6UiSmoke{AutoFinish=false};string uiError=null;
            var work=Drive(ui.Run(),delegate(Exception e){uiError=e.ToString();});while(work.MoveNext())yield return work.Current;
            result.uiJson=ui.Json??"";result.uiPassed=ui.Passed&&uiError==null;
            result.uiError=uiError??(ui.Passed?"":"Actual UI group failure; see uiJson for the individual result.");result.error=result.uiError;
            string knifeError=null;
            work=Drive(R24KnifeContactFixture.Run(loop,delegate(string note){result.knife.Add(note);Debug.Log("R6 KNIFE "+note);},delegate(string suffix){R23SmokeControl.Capture(loop.Rig.Head,suffix);}),delegate(Exception e){knifeError=e.ToString();});
            while(work.MoveNext())yield return work.Current;
            result.knifePassed=knifeError==null;result.knifeError=knifeError??"";if(knifeError!=null)result.error+=(result.error.Length>0?"\n":"")+knifeError;
            string dt11Error=null;
            work=Drive(R24Dt11GameplayFixture.Run(loop,delegate(string note){result.dt11.Add(note);Debug.Log("R6 DT11 "+note);},delegate(string suffix){R23SmokeControl.Capture(loop.Rig.Head,suffix);}),delegate(Exception e){dt11Error=e.ToString();});
            while(work.MoveNext())yield return work.Current;
            result.dt11Passed=dt11Error==null;result.dt11Error=dt11Error??"";if(dt11Error!=null)result.error+=(result.error.Length>0?"\n":"")+dt11Error;
            string awpError=null;
            work=Drive(R24AwpGameplayFixture.Run(loop,delegate(string note){result.awp.Add(note);Debug.Log("R6 AWP "+note);},delegate(string suffix){R23SmokeControl.Capture(loop.Rig.Head,suffix);}),delegate(Exception e){awpError=e.ToString();});
            while(work.MoveNext())yield return work.Current;
            result.awpPassed=awpError==null;result.awpError=awpError??"";if(awpError!=null)result.error+=(result.error.Length>0?"\n":"")+awpError;
            string m60Error=null;
            work=Drive(R24M60GameplayFixture.Run(loop,delegate(string note){result.m60.Add(note);Debug.Log("R6 M60 "+note);},delegate(string suffix){R23SmokeControl.Capture(loop.Rig.Head,suffix);}),delegate(Exception e){m60Error=e.ToString();});
            while(work.MoveNext())yield return work.Current;
            result.m60Passed=m60Error==null;result.m60Error=m60Error??"";if(m60Error!=null)result.error+=(result.error.Length>0?"\n":"")+m60Error;
            string dualError=null;
            work=Drive(R24DualGameplayFixture.Run(loop,delegate(string note){result.dual.Add(note);Debug.Log("R6 DUAL "+note);},delegate(string suffix){R23SmokeControl.Capture(loop.Rig.Head,suffix);}),delegate(Exception e){dualError=e.ToString();});
            while(work.MoveNext())yield return work.Current;
            result.dualPassed=dualError==null;result.dualError=dualError??"";if(dualError!=null)result.error+=(result.error.Length>0?"\n":"")+dualError;
            result.success=result.uiPassed&&result.knifePassed&&result.dt11Passed&&result.awpPassed&&result.m60Passed&&result.dualPassed&&result.error.Length==0;Finish(result);
        }
        static IEnumerator Drive(IEnumerator routine,Action<Exception> failed)
        {
            var stack=new Stack<IEnumerator>();stack.Push(routine);
            try
            {
                while(stack.Count>0)
                {
                    bool moved=false;object current=null;try{moved=stack.Peek().MoveNext();if(moved)current=stack.Peek().Current;}catch(Exception e){failed(e);break;}
                    if(!moved){var done=stack.Pop()as IDisposable;if(done!=null)done.Dispose();continue;}
                    var nested=current as IEnumerator;if(nested!=null)stack.Push(nested);else yield return current;
                }
            }
            finally{while(stack.Count>0){var done=stack.Pop()as IDisposable;if(done!=null)done.Dispose();}}
        }
        static void Finish(Result result){result.finishedUtc=DateTime.UtcNow.ToString("o");R23SmokeControl.Finish(result.success,JsonUtility.ToJson(result,true));}
    }
}
#endif
