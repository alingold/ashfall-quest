#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
namespace Ashfall.Editor.Playable
{
    // Called only by an authorized build, never by an import/domain reload.
    public static class R25BrandingSetup
    {
        const string Root="Assets/Ashfall/Branding/";
        static Texture2D Texture(string name,bool sprite)
        {
            string path=Root+name+".png";
            var importer=AssetImporter.GetAtPath(path) as TextureImporter;
            if(importer==null){Debug.LogWarning("ASHFALL branding missing: "+path);return null;}
            bool dirty=importer.textureType!=(sprite?TextureImporterType.Sprite:TextureImporterType.Default)||importer.textureCompression!=TextureImporterCompression.Uncompressed||!importer.isReadable||importer.mipmapEnabled||!importer.alphaIsTransparency||(sprite&&importer.spriteImportMode!=SpriteImportMode.Single);
            importer.textureType=sprite?TextureImporterType.Sprite:TextureImporterType.Default;
            if(sprite)importer.spriteImportMode=SpriteImportMode.Single;
            importer.textureCompression=TextureImporterCompression.Uncompressed;importer.isReadable=true;importer.mipmapEnabled=false;importer.alphaIsTransparency=true;
            foreach(string platform in new[]{"Android","Standalone"})
            {
                var setting=importer.GetPlatformTextureSettings(platform);
                if(setting.overridden){importer.ClearPlatformTextureSettings(platform);dirty=true;}
            }
            if(dirty){importer.SaveAndReimport();Debug.Log("ASHFALL branding imported uncompressed: "+path);}
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
        static void Legacy(NamedBuildTarget target,Texture2D texture)
        {
            if(texture==null)return;
            var sizes=PlayerSettings.GetIconSizes(target,IconKind.Any);var icons=new Texture2D[sizes.Length];
            for(int i=0;i<icons.Length;i++)icons[i]=texture;
            PlayerSettings.SetIcons(target,icons,IconKind.Any);
            Debug.Log("ASHFALL branding legacy icons: "+target.TargetName+" slots="+icons.Length);
        }
        public static void Apply()
        {
            try
            {
                var icon=Texture("app-icon-1024",false);var foreground=Texture("android-adaptive-foreground-432",false);var background=Texture("android-adaptive-background-432",false);
                Legacy(NamedBuildTarget.Standalone,icon);
                foreach(var kind in PlayerSettings.GetSupportedIconKinds(NamedBuildTarget.Android))
                {
                    var slots=PlayerSettings.GetPlatformIcons(NamedBuildTarget.Android,kind);
                    foreach(var slot in slots)
                    {
                        if(slot.maxLayerCount>=2){if(background!=null&&foreground!=null)slot.SetTextures(new[]{background,foreground});}
                        else if(icon!=null)slot.SetTexture(icon);
                    }
                    PlayerSettings.SetPlatformIcons(NamedBuildTarget.Android,kind,slots);
                    Debug.Log("ASHFALL branding Android "+kind+" slots="+slots.Length);
                }
                var splash=Texture("splash-logo",true);
                PlayerSettings.SplashScreen.backgroundColor=new Color(11/255f,12/255f,15/255f);
                PlayerSettings.SplashScreen.show=true;
                if(splash!=null)PlayerSettings.SplashScreen.logos=new[]{PlayerSettings.SplashScreenLogo.Create(2,AssetDatabase.LoadAssetAtPath<Sprite>(Root+"splash-logo.png"))};
                // 6000.6 exposes no public SplashScreen licence entitlement query.
                // Preserve an enabled Unity logo rather than guessing entitlement.
                if(PlayerSettings.SplashScreen.showUnityLogo)Debug.LogWarning("ASHFALL branding: Unity logo retained; public SplashScreen API does not report licence permission to disable it.");
                else Debug.Log("ASHFALL branding: Unity logo already disabled by permitted project settings.");
                AssetDatabase.SaveAssets();Debug.Log("ASHFALL branding applied (icons and charcoal splash).");
            }
            catch(Exception error){Debug.LogWarning("ASHFALL branding skipped safely: "+error.Message);}
        }
    }
}
#endif
