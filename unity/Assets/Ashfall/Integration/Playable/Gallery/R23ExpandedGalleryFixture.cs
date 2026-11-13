#if UNITY_6000_0_OR_NEWER && UNITY_EDITOR
using System;
using System.Collections;
namespace Ashfall.Integration.Playable.Gallery
{
    // Optional root-admitted native asset/presentation check. No tracked pose,
    // input frame, carried inventory, damage or gameplay authority is modified.
    public static class R23ExpandedGalleryFixture
    {
        public static IEnumerator Run(R23LobbyWeaponGallery gallery,Action<string> report=null,Func<string,IEnumerator> capture=null)
        {return RunProfiles(gallery,new[]{"mk18","akm","spr","magnum","cx4","px4","m1014"},report,capture);}
        // Additive native entry: the established seven-profile gate stays unchanged.
        public static IEnumerator RunSmg(R23LobbyWeaponGallery gallery,Action<string> report=null,Func<string,IEnumerator> capture=null)
        {return RunProfiles(gallery,new[]{"ump","p90","mp5","mp9"},report,capture);}
        public static IEnumerator RunCatalog5(R23LobbyWeaponGallery gallery,Action<string> report=null,Func<string,IEnumerator> capture=null)
        {return RunProfiles(gallery,new[]{"1911","tec9","rfb","fal","s85"},report,capture);}
        public static IEnumerator RunAll(R23LobbyWeaponGallery gallery,Action<string> report=null,Func<string,IEnumerator> capture=null)
        {return RunProfiles(gallery,gallery.AvailableWeaponIds,report,capture);}
        static IEnumerator RunProfiles(R23LobbyWeaponGallery gallery,string[] ids,Action<string> report,Func<string,IEnumerator> capture)
        {
            bool wasActive=gallery.Active;string previous=gallery.ViewedWeaponId;
            var ui=UnityEngine.Object.FindAnyObjectByType<Ashfall.Integration.Playable.UI.R23VrUI>();
            string previousCaption=ui==null?null:ui.CurrentViewedWeaponId;
            try
            {
                gallery.Hide();
                foreach(string id in ids)
                {
                    // Native captures render the VR world board, not Godot's desktop
                    // CanvasLayer. Keep its real browse caption in sync with the model.
                    if(previousCaption!=null&&!ui.BrowseWeapon(id))throw new InvalidOperationException("Source gallery UI has no profile "+id);
                    if(!gallery.Show(id))throw new InvalidOperationException("Source gallery rejected "+id);
                    double start=UnityEngine.Time.realtimeSinceStartupAsDouble;
                    while(!gallery.IsWeaponReady(id)&&string.IsNullOrEmpty(gallery.WeaponFailure(id))&&UnityEngine.Time.realtimeSinceStartupAsDouble-start<90)yield return null;
                    if(!gallery.IsWeaponReady(id)||!string.IsNullOrEmpty(gallery.WeaponFailure(id)))throw new InvalidOperationException("Source gallery load "+id+": "+gallery.WeaponFailure(id));
                    if(gallery.VisiblePreviewCount!=1||gallery.VisiblePreviewId!=id||gallery.ViewState.Live||gallery.ViewedWeaponId!=id)throw new InvalidOperationException("Source gallery visible/live identity mismatch "+id);
                    if(report!=null)report("SourceGalleryAsset "+id+": actual decoded single matching preview, no live private or carried grant");
                    if(capture!=null&&(id=="cx4"||id=="px4"||id=="m1014"||id=="ump"||id=="p90"||id=="mp5"||id=="mp9"||id=="1911"||id=="tec9"||id=="rfb"||id=="fal"||id=="s85"))yield return capture("source-gallery-"+id);
                }
                gallery.Hide();if(gallery.VisiblePreviewCount!=0||gallery.Active)throw new InvalidOperationException("Source gallery hide cleanup");
            }
            finally{if(previousCaption!=null)ui.BrowseWeapon(previousCaption);if(wasActive&&previous!=null)gallery.Show(previous);else gallery.Hide();}
        }
    }
}
#endif
