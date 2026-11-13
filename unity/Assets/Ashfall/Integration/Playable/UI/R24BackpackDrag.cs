#if UNITY_6000_0_OR_NEWER
using System;using System.Collections.Generic;using UnityEngine;
namespace Ashfall.Integration.Playable.UI
{
 public sealed partial class R23VrUI
 {
  const float CatalogTop=.0665f,CatalogBottom=-.6985f,CatalogRowHeight=.155f,CatalogRowPixels=124;
  readonly List<Transform> backpackCatalogSurfaces=new List<Transform>();
  MaterialPropertyBlock backpackClipBlock;
  bool preserveBackpackGestureForScroll;
  bool ReadBackpackPlane(Vector3 origin,Vector3 direction,out Vector2 point)
  {
   point=Vector2.zero;if(Page!=R23UiPage.Loadout||panel==null)return false;
   var o=panel.InverseTransformPoint(origin);var d=panel.InverseTransformVector(direction.normalized);if(Mathf.Abs(d.z)<.00001f)return false;
   float t=-o.z/d.z;if(t<0||t>8)return false;var p=o+d*t;point=new Vector2(p.x,p.y);return Mathf.Abs(p.x)<=.61f&&p.y>=-.80f&&p.y<=.75f;
  }
  bool BackpackCatalogPointVisible(Button button,Vector3 point)
  {
   if(Page!=R23UiPage.Loadout||button.OwnedId==null||!button.OwnedId.StartsWith("catalog/",StringComparison.Ordinal))return true;
   float y=panel.InverseTransformPoint(point).y;return y>=CatalogBottom&&y<=CatalogTop;
  }
  bool ApplyBackpackScrollPixels(float value)
  {
   int maximum=Math.Max(0,(CreativeCards().Count+3)/4-5);value=Mathf.Clamp(value,0,maximum*CatalogRowPixels);
   int next=Mathf.Clamp(Mathf.FloorToInt(value/CatalogRowPixels),0,maximum);catalogScroll=value;
   if(next!=catalogRow){catalogRow=next;preserveBackpackGestureForScroll=true;try{Build();}finally{preserveBackpackGestureForScroll=false;}return true;}
   RefreshBackpackCatalogPositions();return false;
  }
  bool DragBackpack(float delta)
  {return creativeCatalog&&Page==R23UiPage.Loadout&&ApplyBackpackScrollPixels(catalogScroll+Mathf.Clamp(delta,-.4f,.4f)*CatalogRowPixels/CatalogRowHeight);}
  void RefreshBackpackCatalogPositions()
  {
   if(backpackCatalogSurfaces.Count==0)return;
   if(backpackClipBlock==null)backpackClipBlock=new MaterialPropertyBlock();
   float fraction=(catalogScroll/CatalogRowPixels-catalogRow)*CatalogRowHeight;
   for(int i=0;i<backpackCatalogSurfaces.Count;i++){
    var surface=backpackCatalogSurfaces[i];if(surface==null)continue;var p=surface.localPosition;p.y=-.006f-i/4*CatalogRowHeight+fraction;surface.localPosition=p;
    foreach(var renderer in surface.GetComponentsInChildren<Renderer>(true)){
     // Object-to-panel coordinates stay correct through head/actor movement.
     // Only the scrolling catalog receives these per-renderer clip properties.
     var m=panel.worldToLocalMatrix*renderer.transform.localToWorldMatrix;renderer.GetPropertyBlock(backpackClipBlock);
     backpackClipBlock.SetVector("_UiClipY",m.GetRow(1));backpackClipBlock.SetVector("_UiClipBounds",new Vector4(CatalogBottom,CatalogTop,1,0));renderer.SetPropertyBlock(backpackClipBlock);
    }
   }
  }
 }
}
#endif
