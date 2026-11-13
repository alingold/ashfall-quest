#if UNITY_EDITOR && UNITY_6000_0_OR_NEWER
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace Ashfall.Editor.Playable
{
    // One XR-disabled scene run, with independent results for the corrected
    // reload arbitration and source ghost lifecycle. No already-passing FFA replay.
    public sealed class R24GhostReloadSmoke
    {
        [Serializable] sealed class Result
        {
            public bool success,practice,ghost,headsetVerified=false;
            public string practiceJson="",ghostJson="",practiceError="",ghostError="";
        }
        public IEnumerator Run()
        {
            var result=new Result();
            var practice=new R24LocalMatchSmoke(true,true){AutoFinish=false};
            var first=Drive(practice.Run(),error=>result.practiceError=error);
            while(first.MoveNext())yield return first.Current;
            result.practice=practice.Passed&&result.practiceError.Length==0;result.practiceJson=practice.Json??"";
            var ghost=new R24GhostSmoke{AutoFinish=false};
            var second=Drive(ghost.Run(),error=>result.ghostError=error);
            while(second.MoveNext())yield return second.Current;
            result.ghost=ghost.Passed&&result.ghostError.Length==0;result.ghostJson=ghost.Json??"";
            result.success=result.practice&&result.ghost;
            R23SmokeControl.Finish(result.success,JsonUtility.ToJson(result,true));
        }
        static IEnumerator Drive(IEnumerator routine,Action<string> failed)
        {
            var stack=new Stack<IEnumerator>();stack.Push(routine);
            try
            {
                while(stack.Count>0)
                {
                    bool moved=false;object value=null;
                    try{moved=stack.Peek().MoveNext();if(moved)value=stack.Peek().Current;}
                    catch(Exception error){failed(error.ToString());Debug.LogException(error);yield break;}
                    if(!moved){var done=stack.Pop()as IDisposable;if(done!=null)done.Dispose();continue;}
                    var nested=value as IEnumerator;if(nested!=null)stack.Push(nested);else yield return value;
                }
            }
            finally{while(stack.Count>0){var done=stack.Pop()as IDisposable;if(done!=null)done.Dispose();}}
        }
    }
}
#endif
