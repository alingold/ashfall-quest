using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
#if UNITY_6000_0_OR_NEWER
using UnityEngine;
using UnityEngine.Rendering;
#endif
namespace Ashfall.Integration.Playable.World
{
 [Serializable]public sealed class R24CyberColor {public float[] colorUnconverted=null;}
 [Serializable]public sealed class R24CyberEnvironmentValues
 {
  public int background_mode=0,ambient_light_source=0,tonemap_mode=0;public R24CyberColor background_color=null,ambient_light_color=null,fog_light_color=null;
  public float ambient_light_energy=0,glow_intensity=0,glow_bloom=0,fog_density=0;public bool glow_enabled=false,fog_enabled=false;
 }
 [Serializable]public sealed class R24CyberDirectional
 {public string name="",shadowMode="";public float[] sourceEulerDegrees=null,color=null;public float energy=0,shadowMaxDistance=0;public bool shadows=false;}
 [Serializable]public sealed class R24CyberStreetLights
 {public int count=0;public float[] color=null,sourceEulerDegrees=null;public float energy=0,range=0,angle=0,attenuation=0;public bool shadows=false;}
 [Serializable]public sealed class R24CyberLightSite {public float[] at=null,color=null;}
 [Serializable]public sealed class R24CyberInteriorLights
 {public int count=0,nearestInteriorCount=0,nearestNeonCount=0;public float energy=0,range=0;public bool shadows=false;public R24CyberLightSite[] interiorSites=null,neonSites=null;}
 [Serializable]public sealed class R24CyberEnvironmentData
 {
  public int version=0;public string mapId="",sourceContentId="";public R24CyberEnvironmentValues sourceEnvironment=null;public R24CyberDirectional directional=null;public R24CyberStreetLights pooledStreetLights=null;public R24CyberInteriorLights pooledInteriorLights=null;
  public void Validate()
  {
   if(version!=1||mapId!="cyber_bird"||sourceContentId!="23951bcc4c358a38a0fd1ad59fa24cef0171f5df6c6c2cc2e38dca5a095f5520"||sourceEnvironment==null||directional==null||pooledStreetLights==null||pooledInteriorLights==null)throw new InvalidDataException("Cyber Bird environment binding");
   if(sourceEnvironment.background_mode!=1||sourceEnvironment.ambient_light_source!=3||pooledStreetLights.count!=4||pooledInteriorLights.count!=6||pooledInteriorLights.nearestInteriorCount!=3||pooledInteriorLights.nearestNeonCount!=3)throw new InvalidDataException("Cyber Bird source pool/background policy");
   R24CyberLightPolicy.Vector(directional.sourceEulerDegrees,3);R24CyberLightPolicy.Vector(directional.color,4);R24CyberLightPolicy.Vector(pooledStreetLights.sourceEulerDegrees,3);R24CyberLightPolicy.Vector(pooledStreetLights.color,4);
   if(sourceEnvironment.background_color==null||sourceEnvironment.ambient_light_color==null||sourceEnvironment.fog_light_color==null)throw new InvalidDataException("Cyber Bird source colors");
   R24CyberLightPolicy.Vector(sourceEnvironment.background_color.colorUnconverted,4);R24CyberLightPolicy.Vector(sourceEnvironment.ambient_light_color.colorUnconverted,4);R24CyberLightPolicy.Vector(sourceEnvironment.fog_light_color.colorUnconverted,4);
   ValidateSites(pooledInteriorLights.interiorSites);ValidateSites(pooledInteriorLights.neonSites);
   foreach(float v in new[]{sourceEnvironment.ambient_light_energy,sourceEnvironment.glow_intensity,sourceEnvironment.glow_bloom,sourceEnvironment.fog_density,directional.energy,directional.shadowMaxDistance,pooledStreetLights.energy,pooledStreetLights.range,pooledStreetLights.angle,pooledStreetLights.attenuation,pooledInteriorLights.energy,pooledInteriorLights.range})if(!R24CyberLightPolicy.Finite(v)||v<0)throw new InvalidDataException("Cyber Bird light value");
   if(pooledStreetLights.angle<=0||pooledStreetLights.angle>=90)throw new InvalidDataException("Godot spotlight angular radius");
  }
  static void ValidateSites(R24CyberLightSite[] sites){if(sites==null||sites.Length<3||sites.Length>4096)throw new InvalidDataException("Cyber Bird bounded site list");foreach(var site in sites){if(site==null)throw new InvalidDataException("Cyber Bird site");R24CyberLightPolicy.Vector(site.at,3);R24CyberLightPolicy.Vector(site.color,4);}}
 }
 public static class R24CyberLightPolicy
 {
  public const float UpdateSeconds=.5f,StreetVisibleDistance=55,InteriorVisibleDistance=65,InteriorEnergy=6.5f,NeonEnergy=3.4f,InteriorRange=18,NeonRange=14;
  public static bool Finite(float v){return !Single.IsNaN(v)&&!Single.IsInfinity(v);}
  public static void Vector(float[] v,int count){if(v==null||v.Length!=count)throw new InvalidDataException("Cyber Bird vector/color");foreach(float n in v)if(!Finite(n))throw new InvalidDataException("Cyber Bird nonfinite vector/color");}
  public static float UnitySpotAngle(float godotRadius){return godotRadius*2;}
  public static bool Advance(ref float clock,float seconds){if(!Finite(seconds)||seconds<0)return false;clock+=seconds;if(clock<UpdateSeconds)return false;clock=0;return true;}
  // Source sites already contain global(0,0,2400). Reflect Z once, no second offset.
  public static void ReflectZ(float[] positions){for(int i=2;i<positions.Length;i+=3)positions[i]=-positions[i];}
  public static void Nearest(float[] positions,float[] matrix,float eyeX,float eyeY,float eyeZ,int[] selected,float[] distances)
  {
   for(int i=0;i<selected.Length;i++){selected[i]=-1;distances[i]=Single.PositiveInfinity;}
   for(int i=0;i<positions.Length/3;i++)
   {
    int at=i*3;float x=positions[at],y=positions[at+1],z=positions[at+2];float dx=matrix[0]*x+matrix[1]*y+matrix[2]*z+matrix[3]-eyeX,dy=matrix[4]*x+matrix[5]*y+matrix[6]*z+matrix[7]-eyeY,dz=matrix[8]*x+matrix[9]*y+matrix[10]*z+matrix[11]-eyeZ,d=dx*dx+dy*dy+dz*dz;
    if(!Finite(d)||d>=distances[distances.Length-1])continue;int slot=distances.Length-1;while(slot>0&&d<distances[slot-1]){selected[slot]=selected[slot-1];distances[slot]=distances[slot-1];slot--;}selected[slot]=i;distances[slot]=d;
   }
  }
  // Unity JsonUtility cannot deserialize jagged float arrays. This narrow
  // numeric reader handles only the actual source street sites, once at load.
  public static float[] ReadStreetSites(string json)
  {
   int at=json.IndexOf("\"pooledStreetLights\"",StringComparison.Ordinal);if(at<0)throw new InvalidDataException("Cyber street descriptors missing");at=json.IndexOf("\"sites\"",at,StringComparison.Ordinal);if(at<0)throw new InvalidDataException("Cyber street sites missing");at=json.IndexOf('[',at);if(at<0)throw new InvalidDataException("Cyber street site array");at++;var values=new List<float>();
   Action whitespace=delegate{while(at<json.Length&&Char.IsWhiteSpace(json[at]))at++;};whitespace();
   while(at<json.Length&&json[at]!=']')
   {
    if(values.Count>=4096*3||json[at++]!='[')throw new InvalidDataException("Cyber street site tuple");for(int k=0;k<3;k++){whitespace();int first=at;while(at<json.Length&&(Char.IsDigit(json[at])||json[at]=='-'||json[at]=='+'||json[at]=='.'||json[at]=='e'||json[at]=='E'))at++;float n;if(first==at||!Single.TryParse(json.Substring(first,at-first),NumberStyles.Float,CultureInfo.InvariantCulture,out n)||!Finite(n))throw new InvalidDataException("Cyber street site number");values.Add(n);whitespace();if(k<2&&json[at++]!=',')throw new InvalidDataException("Cyber street site comma");}if(json[at++]!=']')throw new InvalidDataException("Cyber street site end");whitespace();if(at<json.Length&&json[at]==','){at++;whitespace();}else break;
   }
   if(at>=json.Length||json[at]!=']'||values.Count<12)throw new InvalidDataException("Cyber street list incomplete");return values.ToArray();
  }
  // Source Godot default YXZ Euler, -Z emission, reflected to Unity+Z.
  public static float[] ReflectedEmission(float[] degrees)
  {
   double x=-degrees[0]*Math.PI/180,y=-degrees[1]*Math.PI/180;return new[]{(float)(Math.Sin(y)*Math.Cos(x)),(float)-Math.Sin(x),(float)(Math.Cos(y)*Math.Cos(x))};
  }
 }
#if UNITY_6000_0_OR_NEWER
 public sealed class R24CyberEnvironment:IDisposable
 {
  sealed class View {internal Camera Camera;internal CameraClearFlags Clear;internal Color Background;internal bool Saved;}
  struct PreviousLight {internal Light Light;internal bool Enabled;}
  sealed class SceneState
  {
   readonly Material sky;readonly Light sun;readonly AmbientMode mode;readonly Color ambient,skyColor,equator,ground,fogColor;readonly float ambientIntensity,fogDensity,fogStart,fogEnd;readonly bool fog;readonly FogMode fogMode;readonly SphericalHarmonicsL2 probe;
   internal SceneState(){sky=RenderSettings.skybox;sun=RenderSettings.sun;mode=RenderSettings.ambientMode;ambient=RenderSettings.ambientLight;skyColor=RenderSettings.ambientSkyColor;equator=RenderSettings.ambientEquatorColor;ground=RenderSettings.ambientGroundColor;ambientIntensity=RenderSettings.ambientIntensity;probe=RenderSettings.ambientProbe;fog=RenderSettings.fog;fogMode=RenderSettings.fogMode;fogColor=RenderSettings.fogColor;fogDensity=RenderSettings.fogDensity;fogStart=RenderSettings.fogStartDistance;fogEnd=RenderSettings.fogEndDistance;}
   internal void Restore(){RenderSettings.skybox=sky;RenderSettings.sun=sun;RenderSettings.ambientMode=mode;RenderSettings.ambientLight=ambient;RenderSettings.ambientSkyColor=skyColor;RenderSettings.ambientEquatorColor=equator;RenderSettings.ambientGroundColor=ground;RenderSettings.ambientIntensity=ambientIntensity;RenderSettings.ambientProbe=probe;RenderSettings.fog=fog;RenderSettings.fogMode=fogMode;RenderSettings.fogColor=fogColor;RenderSettings.fogDensity=fogDensity;RenderSettings.fogStartDistance=fogStart;RenderSettings.fogEndDistance=fogEnd;}
  }
  readonly Transform root;readonly GameObject content;readonly R24CyberEnvironmentData data;readonly Light moon;readonly Light[] street=new Light[4],local=new Light[6];readonly float[] streets,interiors,neons;readonly Color[] interiorColors,neonColors;readonly int[] streetIndices=new int[4],interiorIndices=new int[3],neonIndices=new int[3];readonly float[] streetDistances=new float[4],interiorDistances=new float[3],neonDistances=new float[3],matrix=new float[12];readonly List<View> views=new List<View>();readonly List<PreviousLight> previousLights=new List<PreviousLight>();SceneState previous;Camera camera;bool active,disposed;float clock;
  public bool Active {get{return active;}}public Light MoonLight {get{return moon;}}public float SourceShadowMaxDistance {get{return data.directional.shadowMaxDistance;}}public int SourceShadowCascades {get{return 2;}}public int SourceToneMapper {get{return data.sourceEnvironment.tonemap_mode;}}public bool SourceGlowEnabled {get{return data.sourceEnvironment.glow_enabled;}}public float SourceGlowIntensity {get{return data.sourceEnvironment.glow_intensity;}}public float SourceGlowBloom {get{return data.sourceEnvironment.glow_bloom;}}public float SourceStreetDistanceAttenuation {get{return data.pooledStreetLights.attenuation;}}
  public int SelectedStreetSite(int slot){return streetIndices[slot];}public int SelectedInteriorSite(int slot){return interiorIndices[slot];}public int SelectedNeonSite(int slot){return neonIndices[slot];}
  public R24CyberEnvironment(Transform mapRoot,string json)
  {
   if(mapRoot==null)throw new ArgumentNullException("mapRoot");if(String.IsNullOrEmpty(json))throw new ArgumentException("Cyber Bird environment JSON");data=JsonUtility.FromJson<R24CyberEnvironmentData>(json);if(data==null)throw new InvalidDataException("Cyber Bird JSON");data.Validate();streets=R24CyberLightPolicy.ReadStreetSites(json);R24CyberLightPolicy.ReflectZ(streets);interiors=Sites(data.pooledInteriorLights.interiorSites,out interiorColors);neons=Sites(data.pooledInteriorLights.neonSites,out neonColors);root=mapRoot;content=new GameObject("R6 Cyber Bird source environment lights");content.transform.SetParent(root,false);content.SetActive(false);
   moon=Light("City_Moon",LightType.Directional);moon.color=ColorValue(data.directional.color);moon.intensity=data.directional.energy;moon.shadows=data.directional.shadows?LightShadows.Soft:LightShadows.None;moon.transform.localRotation=SourceRotation(data.directional.sourceEulerDegrees);
   for(int i=0;i<4;i++){streetIndices[i]=-1;var l=street[i]=Light("PooledStreetLampR5_"+i,LightType.Spot);l.color=ColorValue(data.pooledStreetLights.color);l.intensity=data.pooledStreetLights.energy;l.range=data.pooledStreetLights.range;l.spotAngle=R24CyberLightPolicy.UnitySpotAngle(data.pooledStreetLights.angle);l.innerSpotAngle=0;l.shadows=data.pooledStreetLights.shadows?LightShadows.Soft:LightShadows.None;l.transform.localRotation=SourceRotation(data.pooledStreetLights.sourceEulerDegrees);}
   for(int i=0;i<6;i++){var l=local[i]=Light("Nearest_Interior_Light_"+i,LightType.Point);l.range=data.pooledInteriorLights.range;l.intensity=data.pooledInteriorLights.energy;l.shadows=data.pooledInteriorLights.shadows?LightShadows.Soft:LightShadows.None;}for(int i=0;i<3;i++)interiorIndices[i]=neonIndices[i]=-1;
  }
  Light Light(string name,LightType type){var go=new GameObject(name);go.transform.SetParent(content.transform,false);var result=go.AddComponent<Light>();result.type=type;result.enabled=false;result.renderMode=LightRenderMode.ForcePixel;return result;}
  static float[] Sites(R24CyberLightSite[] input,out Color[] colors){var result=new float[input.Length*3];colors=new Color[input.Length];for(int i=0;i<input.Length;i++){result[i*3]=input[i].at[0];result[i*3+1]=input[i].at[1];result[i*3+2]=-input[i].at[2];colors[i]=ColorValue(input[i].color);}return result;}
  static Color ColorValue(float[] c){return new Color(c[0],c[1],c[2],c[3]);}
  static Quaternion SourceRotation(float[] e){return Quaternion.AngleAxis(-e[1],Vector3.up)*Quaternion.AngleAxis(-e[0],Vector3.right)*Quaternion.AngleAxis(e[2],Vector3.forward);}
  void Configure(Camera c){c.clearFlags=CameraClearFlags.SolidColor;c.backgroundColor=ColorValue(data.sourceEnvironment.background_color.colorUnconverted);}
  public void BindCamera(Camera value)
  {
   if(disposed)return;camera=value;if(value==null){DisablePools();return;}View found=null;for(int i=0;i<views.Count;i++)if(views[i].Camera==value){found=views[i];break;}if(found==null){found=new View{Camera=value};views.Add(found);}if(active){if(!found.Saved){found.Clear=value.clearFlags;found.Background=value.backgroundColor;found.Saved=true;}Configure(value);Refresh();}
  }
  public void SetActive(bool value)
  {
   if(disposed||active==value)return;if(value&&(root==null||content==null))return;
   if(value)
   {
    previous=new SceneState();previousLights.Clear();foreach(var l in UnityEngine.Object.FindObjectsByType<Light>(FindObjectsInactive.Include))if(l.type==LightType.Directional&&l!=moon){previousLights.Add(new PreviousLight{Light=l,Enabled=l.enabled});l.enabled=false;}
    RenderSettings.skybox=null;RenderSettings.sun=moon;RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=ColorValue(data.sourceEnvironment.ambient_light_color.colorUnconverted)*data.sourceEnvironment.ambient_light_energy;RenderSettings.ambientIntensity=1;RenderSettings.fog=data.sourceEnvironment.fog_enabled;RenderSettings.fogMode=FogMode.Exponential;RenderSettings.fogColor=ColorValue(data.sourceEnvironment.fog_light_color.colorUnconverted);RenderSettings.fogDensity=data.sourceEnvironment.fog_density;
    for(int i=0;i<views.Count;i++){var v=views[i];if(v.Camera==null)continue;v.Clear=v.Camera.clearFlags;v.Background=v.Camera.backgroundColor;v.Saved=true;Configure(v.Camera);}content.SetActive(true);active=true;moon.enabled=true;clock=0;Refresh();
   }
   else
   {
    active=false;clock=0;DisablePools();if(moon!=null)moon.enabled=false;if(content!=null)content.SetActive(false);if(previous!=null){previous.Restore();previous=null;}foreach(var p in previousLights)if(p.Light!=null)p.Light.enabled=p.Enabled;previousLights.Clear();for(int i=0;i<views.Count;i++){var v=views[i];if(v.Camera!=null&&v.Saved){v.Camera.clearFlags=v.Clear;v.Camera.backgroundColor=v.Background;}v.Saved=false;}
   }
  }
  public void Tick(float seconds){if(disposed||!active)return;if(root==null){SetActive(false);return;}if(camera==null){DisablePools();return;}if(R24CyberLightPolicy.Advance(ref clock,seconds))Refresh();}
  public void Tick(){Tick(Time.deltaTime);}
  void Refresh()
  {
   if(!active||root==null||camera==null){DisablePools();return;}var transform=root.localToWorldMatrix;matrix[0]=transform.m00;matrix[1]=transform.m01;matrix[2]=transform.m02;matrix[3]=transform.m03;matrix[4]=transform.m10;matrix[5]=transform.m11;matrix[6]=transform.m12;matrix[7]=transform.m13;matrix[8]=transform.m20;matrix[9]=transform.m21;matrix[10]=transform.m22;matrix[11]=transform.m23;var eye=camera.transform.position;
   R24CyberLightPolicy.Nearest(streets,matrix,eye.x,eye.y,eye.z,streetIndices,streetDistances);R24CyberLightPolicy.Nearest(interiors,matrix,eye.x,eye.y,eye.z,interiorIndices,interiorDistances);R24CyberLightPolicy.Nearest(neons,matrix,eye.x,eye.y,eye.z,neonIndices,neonDistances);
   for(int i=0;i<4;i++){SetPosition(street[i],streets,streetIndices[i]);street[i].enabled=streetIndices[i]>=0&&streetDistances[i]<55*55;}
   for(int i=0;i<3;i++){SetLocal(local[i],interiors,interiorColors,interiorIndices[i],interiorDistances[i],true);SetLocal(local[i+3],neons,neonColors,neonIndices[i],neonDistances[i],false);}
  }
  static void SetPosition(Light l,float[] sites,int index){if(index>=0){int at=index*3;l.transform.localPosition=new Vector3(sites[at],sites[at+1],sites[at+2]);}}
  static void SetLocal(Light l,float[] sites,Color[] colors,int index,float distance,bool interior){SetPosition(l,sites,index);if(index>=0)l.color=colors[index];l.intensity=interior?R24CyberLightPolicy.InteriorEnergy:R24CyberLightPolicy.NeonEnergy;l.range=interior?R24CyberLightPolicy.InteriorRange:R24CyberLightPolicy.NeonRange;l.enabled=index>=0&&distance<65*65;}
  void DisablePools(){for(int i=0;i<4;i++){if(street[i]!=null)street[i].enabled=false;streetIndices[i]=-1;}for(int i=0;i<6;i++)if(local[i]!=null)local[i].enabled=false;for(int i=0;i<3;i++)interiorIndices[i]=neonIndices[i]=-1;}
  public void Suspend(){SetActive(false);}
  public void Dispose(){if(disposed)return;SetActive(false);disposed=true;camera=null;views.Clear();if(content!=null)UnityEngine.Object.Destroy(content);}
 }
#endif
}
