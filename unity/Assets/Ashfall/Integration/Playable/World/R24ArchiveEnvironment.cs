#if UNITY_6000_0_OR_NEWER
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Ashfall.Integration.Playable.MapStyles;
namespace Ashfall.Integration.Playable.World
{
    // Actual archive_map1003 inputs. FILMIC tone mapping is not claimed by
    // this lighting/background binding and remains a measured renderer gap.
    public sealed class R24ArchiveEnvironment:IDisposable
    {
        sealed class View {public Camera Camera;public CameraClearFlags Clear;public Color Background;}
        readonly List<View> views=new List<View>();readonly GameObject lightObject;readonly Light light;
        R25StyleProfile profile;Material styleSky;FogMode oldFogMode;Color oldFogColor;float oldFogDensity;
        Material oldSky;Light oldSun;bool oldSunEnabled,oldFog,active;AmbientMode oldMode;Color oldAmbient;float oldIntensity;
        public R24ArchiveEnvironment(Transform root)
        {lightObject=new GameObject("R24 source archive sun");lightObject.transform.SetParent(root,false);lightObject.transform.localRotation=Quaternion.Euler(53,28,0);light=lightObject.AddComponent<Light>();light.type=LightType.Directional;light.color=Color.white;light.intensity=1;light.shadows=LightShadows.None;light.enabled=false;}
        static Color Hex(string value){Color c;ColorUtility.TryParseHtmlString("#"+value,out c);return c;}
        void Configure(Camera camera){camera.clearFlags=styleSky!=null?CameraClearFlags.Skybox:CameraClearFlags.SolidColor;camera.backgroundColor=profile==null?Hex("9db6c6"):C(profile.ClearColor);}
        static Color C(float[] v){return new Color(v[0],v[1],v[2],1);}
        public void SetProfile(R25StyleProfile value){profile=value;}
        void ApplyProfile()
        {
            if(!Expansion.R25Features.MapStyles)profile=null;
            R25StyleGlobals.Apply(profile);
            if(profile==null)return;
            light.color=C(profile.SunColor);light.intensity=profile.SunIntensity;
            lightObject.transform.localRotation=Quaternion.Euler(profile.SunEuler[0],profile.SunEuler[1],profile.SunEuler[2]);
            RenderSettings.ambientLight=C(profile.AmbientColor)*profile.AmbientIntensity;
            // GraphicsSettings keeps exponential fog, but URP unused-variant stripping is enabled.
            // Until build retention is established, only our manual world fog is enabled.
            RenderSettings.fog=false;RenderSettings.fogMode=FogMode.Exponential;
            RenderSettings.fogColor=C(profile.FogColor);RenderSettings.fogDensity=profile.FogDensity;
            if(profile.SkyZenith==null||profile.SkyHorizon==null||profile.SkyNadir==null)return;
            var shader=Resources.Load<Shader>("R25StyleSky");
            if(shader==null||!shader.isSupported)return;
            styleSky=new Material(shader);styleSky.name="Archive style gradient sky";
            styleSky.SetColor("_Zenith",C(profile.SkyZenith));styleSky.SetColor("_Horizon",C(profile.SkyHorizon));styleSky.SetColor("_Nadir",C(profile.SkyNadir));
            styleSky.SetVector("_ToSun",-light.transform.forward);styleSky.SetColor("_SunColor",C(profile.SunColor));styleSky.SetFloat("_SunGlow",profile.SunGlow);
            RenderSettings.skybox=styleSky;
        }
        public void BindCamera(Camera camera)
        {if(camera==null)return;foreach(var view in views)if(view.Camera==camera)return;views.Add(new View{Camera=camera,Clear=camera.clearFlags,Background=camera.backgroundColor});if(active)Configure(camera);}
        public void SetActive(bool value)
        {
            if(active==value)return;
            if(value)
            {
                oldSky=RenderSettings.skybox;oldSun=RenderSettings.sun;oldSunEnabled=oldSun!=null&&oldSun.enabled;oldMode=RenderSettings.ambientMode;oldAmbient=RenderSettings.ambientLight;oldIntensity=RenderSettings.ambientIntensity;oldFog=RenderSettings.fog;oldFogMode=RenderSettings.fogMode;oldFogColor=RenderSettings.fogColor;oldFogDensity=RenderSettings.fogDensity;
                if(oldSun!=null&&oldSun!=light)oldSun.enabled=false;
                light.color=Color.white;light.intensity=1;lightObject.transform.localRotation=Quaternion.Euler(53,28,0);RenderSettings.skybox=null;RenderSettings.sun=light;RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=Hex("c5d5e2")*.8f;RenderSettings.ambientIntensity=1;RenderSettings.fog=false;light.enabled=true;ApplyProfile();
                foreach(var view in views)if(view.Camera!=null){view.Clear=view.Camera.clearFlags;view.Background=view.Camera.backgroundColor;Configure(view.Camera);}active=true;
            }
            else
            {
                light.enabled=false;RenderSettings.skybox=oldSky;RenderSettings.sun=oldSun;if(oldSun!=null)oldSun.enabled=oldSunEnabled;RenderSettings.ambientMode=oldMode;RenderSettings.ambientLight=oldAmbient;RenderSettings.ambientIntensity=oldIntensity;RenderSettings.fog=oldFog;RenderSettings.fogMode=oldFogMode;RenderSettings.fogColor=oldFogColor;RenderSettings.fogDensity=oldFogDensity;R25StyleGlobals.Apply(null);if(styleSky!=null){UnityEngine.Object.Destroy(styleSky);styleSky=null;}
                foreach(var view in views)if(view.Camera!=null){view.Camera.clearFlags=view.Clear;view.Camera.backgroundColor=view.Background;}active=false;
            }
        }
        public void Dispose(){SetActive(false);if(lightObject!=null)UnityEngine.Object.Destroy(lightObject);views.Clear();}
    }
}
#endif
