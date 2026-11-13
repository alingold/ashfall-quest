#if UNITY_6000_0_OR_NEWER
using System;using System.IO;using System.Collections;using System.Collections.Generic;using UnityEngine;using UnityEngine.Networking;
namespace Ashfall.Integration.Playable.Bots
{
 // One decoded bank across all actor owners; shared clips are not actor-owned.
 internal sealed class R24KestrelClipBank:MonoBehaviour
 {
  [Serializable]sealed class Manifest{public int version=0;public Cue[] cues=null;}
  [Serializable]sealed class Cue{public string kind=null,semantic=null;public string[] files=null;public int rate=0;}
  static R24KestrelClipBank shared;int owners;bool disposed;UnityWebRequest pending;readonly Dictionary<string,AudioClip[]> clips=new Dictionary<string,AudioClip[]>();readonly List<AudioClip> resources=new List<AudioClip>();internal bool Ready;internal string Failure;
  internal static R24KestrelClipBank Acquire(){if(shared==null){var go=new GameObject("Shared source KESTREL decoded audio bank");shared=go.AddComponent<R24KestrelClipBank>();shared.owners=1;shared.StartCoroutine(shared.Guard(shared.Load()));}else shared.owners++;return shared;}
  static string Url(string file){string path=Application.streamingAssetsPath+"/PlayableR23/KestrelAudio/"+file;return path.Contains("://")?path:new Uri(path).AbsoluteUri;}
  IEnumerator Guard(IEnumerator work){var stack=new Stack<IEnumerator>();stack.Push(work);try{while(stack.Count>0){object value;try{var top=stack.Peek();if(!top.MoveNext()){var d=stack.Pop()as IDisposable;if(d!=null)d.Dispose();continue;}value=top.Current;}catch(Exception e){Failure=e.ToString();yield break;}var nested=value as IEnumerator;if(nested!=null)stack.Push(nested);else yield return value;}Ready=true;}finally{while(stack.Count>0){var d=stack.Pop()as IDisposable;if(d!=null)d.Dispose();}}}
  IEnumerator Load(){Manifest manifest;using(var request=UnityWebRequest.Get(Url("audio.json"))){pending=request;try{yield return request.SendWebRequest();if(request.result!=UnityWebRequest.Result.Success)throw new IOException("Kestrel cue manifest: "+request.error);manifest=JsonUtility.FromJson<Manifest>(request.downloadHandler.text);}finally{if(pending==request)pending=null;}}
   if(manifest.version!=1||manifest.cues==null||manifest.cues.Length!=4)throw new IOException("Source Kestrel cue format");foreach(var cue in manifest.cues){if(cue.files.Length!=R24KestrelAudioRules.Count(cue.kind)||cue.semantic!=R24KestrelAudioRules.Semantic(cue.kind))throw new IOException("Source Kestrel cue binding "+cue.kind);var decoded=new AudioClip[cue.files.Length];for(int i=0;i<decoded.Length;i++)using(var request=UnityWebRequestMultimedia.GetAudioClip(Url(cue.files[i]),AudioType.WAV)){pending=request;try{var handler=(DownloadHandlerAudioClip)request.downloadHandler;handler.streamAudio=false;handler.compressed=false;yield return request.SendWebRequest();if(request.result!=UnityWebRequest.Result.Success)throw new IOException(cue.files[i]+": "+request.error);var clip=DownloadHandlerAudioClip.GetContent(request);if(clip==null||clip.channels!=1||clip.frequency!=cue.rate){if(clip!=null)Destroy(clip);throw new IOException("Unexpected Kestrel source PCM layout "+cue.files[i]);}clip.name="Shared Kestrel "+cue.files[i];resources.Add(clip);decoded[i]=clip;}finally{if(pending==request)pending=null;}}clips.Add(cue.kind,decoded);}}
  internal AudioClip Read(string kind,int index){AudioClip[] values;return Ready&&clips.TryGetValue(kind,out values)&&index>=0&&index<values.Length?values[index]:null;}
  internal void Release(){if(disposed)return;if(--owners<=0){if(shared==this)shared=null;Clear();Destroy(gameObject);}}
  void Clear(){if(disposed)return;disposed=true;Ready=false;StopAllCoroutines();if(pending!=null){pending.Abort();pending.Dispose();pending=null;}foreach(var clip in resources)if(clip!=null)Destroy(clip);resources.Clear();clips.Clear();}
  void OnDestroy(){if(shared==this)shared=null;Clear();}
 }
}
#endif
