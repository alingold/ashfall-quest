from pathlib import Path
import hashlib,json,shutil
import numpy as np
ROOT=Path(__file__).resolve().parents[5]
EXPORT=ROOT/'unity/Tools/PlayableR23/SourceWorldExport/runs/prepared-r23-valley-range/valley'
OUT=ROOT/'unity/Assets/StreamingAssets/PlayableR23/Maps/valley'
OUT.mkdir(parents=True,exist_ok=True)
def digest(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def rgb9995(raw):
    v=np.frombuffer(raw,dtype='<u4');e=(v>>27).astype(np.int32)
    return np.stack([(v>>b)&511 for b in [0,9,18]],axis=-1)*np.exp2(e[:,None]-24)
def ambient_samples(rgb,w,h):
    # Integrate every original pixel into a small spherical quadrature. The directions
    # are reflected Unity map-local directions; weights are solidAngle/pi for Lambert.
    rgb=rgb.reshape(h,w,3);rows=[]
    for y in range(32):
        y0=y*h//32;y1=(y+1)*h//32;theta=(np.arange(y0,y1)+.5)*np.pi/h
        solid=np.cos(np.arange(y0,y1)*np.pi/h)-np.cos((np.arange(y0,y1)+1)*np.pi/h)
        for x in range(64):
            x0=x*w//64;x1=(x+1)*w//64;weight=solid.sum()*(x1-x0)*2*np.pi/w
            value=(rgb[y0:y1,x0:x1]*solid[:,None,None]).sum(axis=(0,1))/(solid.sum()*(x1-x0))
            polar=(y0+y1)*.5*np.pi/h;azimuth=(x0+x1)*.5*2*np.pi/w
            direction=[np.sin(polar)*np.sin(azimuth),np.cos(polar),np.sin(polar)*np.cos(azimuth)]
            rows.append({'direction':direction,'radiance':value.tolist(),'weight':float(weight/np.pi)})
    return rows

def prepare():
    world=json.loads((EXPORT/'world.json').read_text());resources={r['id']:r for r in world['resources']}
    env=resources[world['environments'][0]['environment']['resourceRef']]['properties']
    sky=resources[env['sky']['resourceRef']]['properties'];panorama=resources[sky['sky_material']['resourceRef']]['properties'];texture=resources[panorama['panorama']['resourceRef']];image=texture['image']
    assert world['complete'] and image['format']==16 and not image['compressed'] and not image['mipmaps']
    assert image['width']==2048 and image['height']==1024 and image['bytes']==2048*1024*4
    original=EXPORT/image['rawFile'];raw=original.read_bytes();assert digest(original)==image['sha256']
    rgb=rgb9995(raw);assert np.isfinite(rgb).all() and rgb.max()>1 and rgb.min()>=0
    assert env['sky_rotation']['vector3']==[0,0,0] and env['ambient_light_source']==3
    lights=[]
    for source in world['lights']:
        prop=source['properties'];t=np.asarray(source['globalTransform']);assert source['class'] in ['DirectionalLight3D','OmniLight3D']
        lights.append({'name':source['path'].split('/')[-1],'sourceClass':source['class'],'sourcePath':source['path'],'sourceTransform':t.tolist(),'position':[t[9],t[10],-t[11]],'forward':[-t[6],-t[7],t[8]],'up':[t[3],t[4],-t[5]],'color':prop['light_color']['colorUnconverted'],'energy':prop['light_energy'],'indirectEnergy':prop['light_indirect_energy'],'specular':prop['light_specular'],'visible':source['visible'],'shadows':prop['shadow_enabled'],'shadowBias':prop['shadow_bias'],'shadowNormalBias':prop['shadow_normal_bias'],'range':prop.get('omni_range',0),'attenuation':prop.get('omni_attenuation',0),'cullMask':prop['light_cull_mask'],'sourceProperties':prop})
    manifest={'version':1,'mapId':'valley','sourceWorldSha256':digest(EXPORT/'world.json'),'sourceGodot':world['godot'],'sourceEnvironment':env,'sourceSky':sky,'sourcePanorama':panorama,'sourceTexture':texture,'sourceFormat':16,'sourceEncoding':'RGBE9995','skyFile':'sky.rgb9e5','skySha256':image['sha256'],'width':image['width'],'height':image['height'],'skyEnergy':panorama['energy_multiplier'],'backgroundEnergy':env['background_energy_multiplier'],'exposure':env['tonemap_exposure'],'toneMode':env['tonemap_mode'],'ambientColor':env['ambient_light_color']['colorUnconverted'],'ambientEnergy':env['ambient_light_energy'],'ambientSource':env['ambient_light_source'],'ambientSkyContribution':env['ambient_light_sky_contribution'],'fogEnabled':env['fog_enabled'],'fogMode':env['fog_mode'],'fogColor':env['fog_light_color']['colorUnconverted'],'fogEnergy':env['fog_light_energy'],'fogDensity':env['fog_density'],'fogSkyAffect':env['fog_sky_affect'],'ambientSamples':ambient_samples(rgb,image['width'],image['height']),'lights':lights,'limits':'Exact original packed HDR bytes retained format16 is RGBE9995 (no alpha), not RGBAHalf. Unity local-light attenuation/shadow filtering/additional-light limits and scene filmic tone mapper are engine-dependent; diffuse sky uses bounded CPU quadrature and Unity L2 SH, not exact Godot irradiance convolution. Reflection convolution is not ported.'}
    shutil.copyfile(original,OUT/'sky.rgb9e5');(OUT/'environment.json').write_text(json.dumps(manifest,indent=2),encoding='utf-8')
    print('Preserved HDR bytes:',len(raw),image['sha256']);print('RGB ranges:',rgb.min(axis=0).tolist(),rgb.max(axis=0).tolist(),'alpha implicit1');print('lights:',len(lights),'ambient quadrature:',len(manifest['ambientSamples']))
if __name__=='__main__':prepare()
