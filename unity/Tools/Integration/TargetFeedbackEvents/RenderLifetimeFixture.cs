#if UNITY_EDITOR && UNITY_6000_0_OR_NEWER
using System;
using System.Collections;
using System.IO;
using UnityEngine;
using Ashfall.Integration.PersonalSettings.Engine;
namespace Ashfall.Tools.TargetFeedbackEvents
{
 public static class RenderLifetimeFixture
 {
  // Root-only coroutine: use its actual approved authored camera/font/materials and
  // serialized engine lane. No automatic camera, layout, shader or approval is made.
  // External driver must suspend its normal Refresh while this fixture owns time.
  public static IEnumerator Capture(NativeTargetFeedbackView view,Camera authoredCamera,string canonicalTarget,string freshDirectory)
  {
   if(view==null||authoredCamera==null||String.IsNullOrEmpty(canonicalTarget)||String.IsNullOrEmpty(freshDirectory)||Directory.Exists(freshDirectory))throw new ArgumentException("fresh authored capture");
   Directory.CreateDirectory(freshDirectory);
   var previous=authoredCamera.targetTexture;
   var rt=new RenderTexture(640,480,24);var image=new Texture2D(640,480,TextureFormat.RGB24,false);
   try {
    authoredCamera.targetTexture=rt;view.Clear();view.Emit(canonicalTarget,.25f,true);view.Emit(canonicalTarget,2.5f,false);
    float[] steps={0,.64f,.20f,.02f};string[] names={"concurrent","fade","before-expiry","expired"};
    for(int i=0;i<steps.Length;i++) {
     view.Refresh(steps[i]);yield return new WaitForEndOfFrame();
     authoredCamera.Render();var old=RenderTexture.active;
     try{RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,640,480),0,0);image.Apply();File.WriteAllBytes(Path.Combine(freshDirectory,names[i]+".png"),image.EncodeToPNG());}
     finally {RenderTexture.active=old;}
    }
    view.Clear();
   }finally{authoredCamera.targetTexture=previous;UnityEngine.Object.DestroyImmediate(image);UnityEngine.Object.DestroyImmediate(rt);view.Clear();}
  }
 }
}
#endif
