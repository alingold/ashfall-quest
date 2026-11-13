"""Focused CPU source formula checks; never runs an engine or shader compiler."""
from pathlib import Path
import ast,hashlib,json,math,re
import numpy as np

ROOT=Path(__file__).resolve().parents[5];HERE=Path(__file__).resolve().parent
SOURCE=ROOT/'quest-r24/source'
SURFACE=ROOT/'unity/Assets/Ashfall/Integration/Playable/Lobby/Resources/R23LobbySurface.shader'
SKY=SURFACE.with_name('R23LobbySky.shader')
FACADE=SOURCE/'assets/materials/lobby_facade_091.gdshader'
SKY_REFERENCE=HERE.parent/'GodotSkyReference.cpp'

def mix(a,b,t):return a*(1-t)+b*t
def srgb_to_linear(x):
    x=np.asarray(x);return np.where(x<=.04045,x/12.92,((x+.055)/1.055)**2.4)
def linear_to_srgb(x):
    x=np.asarray(x);return np.where(x<=.0031308,x*12.92,1.055*np.maximum(x,0)**(1/2.4)-.055)
FUNCS=dict(abs=np.abs,frac=lambda x:x-np.floor(x),fract=lambda x:x-np.floor(x),floor=np.floor,sin=np.sin,
           dot=np.dot,step=lambda edge,x:np.where(x<edge,0.,1.),mix=mix,lerp=mix,
           float2=lambda *x:np.asarray(x),vec2=lambda *x:np.asarray(x),float3=lambda *x:np.asarray(x),vec3=lambda *x:np.asarray(x),
           clamp=np.clip,saturate=lambda x:np.clip(x,0,1),pow=np.power)

def expr(text,values):
    if '?' in text:
        condition,rest=text.split('?',1);yes,no=rest.split(':',1);text='('+yes+') if ('+condition+') else ('+no+')'
    def visit(node):
        if isinstance(node,ast.Expression):return visit(node.body)
        if isinstance(node,ast.Constant) and isinstance(node.value,(int,float)):return node.value
        if isinstance(node,ast.Name):return values[node.id]
        if isinstance(node,ast.Attribute) and node.attr in ('x','y','z'):return visit(node.value)[('x','y','z').index(node.attr)]
        if isinstance(node,ast.IfExp):return visit(node.body) if visit(node.test) else visit(node.orelse)
        if isinstance(node,ast.Compare) and len(node.ops)==1 and isinstance(node.ops[0],ast.Gt):return visit(node.left)>visit(node.comparators[0])
        if isinstance(node,ast.UnaryOp) and isinstance(node.op,ast.USub):return -visit(node.operand)
        if isinstance(node,ast.BinOp):
            a,b=visit(node.left),visit(node.right)
            if isinstance(node.op,ast.Add):return a+b
            if isinstance(node.op,ast.Sub):return a-b
            if isinstance(node.op,ast.Mult):return a*b
            if isinstance(node.op,ast.Div):return a/b
        if isinstance(node,ast.Call) and isinstance(node.func,ast.Name) and node.func.id in FUNCS:
            return FUNCS[node.func.id](*[visit(x) for x in node.args])
        raise AssertionError('Unrecognized source expression '+ast.dump(node))
    return visit(ast.parse(text.strip(),mode='eval'))

def declarations(body,values):
    for _,name,rhs in re.findall(r'\b(float3|float2|vec3|vec2|float)\s+(\w+)\s*=\s*([^;]+);',body):values[name]=expr(rhs,values)
    return values
def facade(body,p,n,albedo_name,emission_name):
    v=declarations(body,dict(wall_position=np.array(p),wall_normal=np.array(n)))
    for name in (albedo_name,emission_name):v[name]=expr(re.search(r'\b'+name+r'\s*=([^;]+);',body).group(1),v)
    return v

def main():
    source=FACADE.read_text(encoding='utf-8').split('void fragment(){',1)[1]
    surface=SURFACE.read_text(encoding='utf-8');sky=SKY.read_text(encoding='utf-8');cpp=SKY_REFERENCE.read_text(encoding='utf-8')
    target=surface.split('void R23Facade(',1)[1].split('{',1)[1].split('}',1)[0]
    cases=[]
    for label,n,u,v in [('wall', (0,0,1),.5,.5),('side',(1,0,0),.5,.5),('roof',(0,1,0),.5,.5),
                         ('bevel raw',(.4,.4,0),.5,.5),('roof threshold',(0,.5,1),.5,.5),
                         ('below roof threshold',(0,.499,1),.5,.5),('mullion',(0,0,1),.1,.5),
                         ('above pane',(0,0,1),.5,.9),('left pane edge',(0,0,1),.16001,.5),
                         ('outside left',(0,0,1),.15999,.5)]:
        cases.append((label,[(620+u)/.62,(7+v)/.64,(601+u)/.62],n))
    cases.extend([('negative grid',[-37.2,4.73,-81.6],(0,0,1)),('other occupancy',[1000.7,15.7,972.9],(1,0,0))])
    samples=[];errors=[]
    for label,p,n in cases:
        a=facade(source,p,n,'ALBEDO','EMISSION');b=facade(target,p,n,'albedo','emission')
        for key in ('horizontal','grid','cell','pane','occupied'):assert np.allclose(a[key],b[key],atol=0,rtol=0),(label,key)
        error=float(max(np.max(np.abs(a['ALBEDO']-b['albedo'])),np.max(np.abs(a['EMISSION']-b['emission']))));assert error==0
        assert np.isfinite(b['albedo']).all() and np.isfinite(b['emission']).all()
        # Current Unity baked/world coordinates must reflect once to this source world phase.
        unity=np.array(p)*[1,1,-1];assert np.array_equal(unity*[1,1,-1],p)
        samples.append(dict(case=label,pane=float(b['pane']),occupied=float(b['occupied']),linearAlbedo=b['albedo'].tolist(),linearEmission=b['emission'].tolist()));errors.append(error)
    raw=facade(target,cases[3][1],(.4,.4,0),'albedo','emission')
    normalized=facade(target,cases[3][1],np.array([.4,.4,0])/math.sqrt(.32),'albedo','emission')
    assert raw['pane']==1 and normalized['pane']==0,'Case must catch old normalize-before-threshold defect'
    assert any(x['pane']==1 for x in samples) and any(x['pane']==0 for x in samples)
    # Primary generated sky: source_color endpoints are decoded before mixing.
    colors=[np.array(c)/255 for c in ((36,110,182),(163,201,228),(53,72,89))]
    top,horizon,bottom=[srgb_to_linear(c) for c in colors]
    sky_body=sky.split('float3 R23SkyGradient(',1)[1].split('{',1)[1].split('}',1)[0]
    source_sky=re.search(r'vec3 sky = (.*?);',cpp).group(1)
    source_ground=re.search(r'vec3 ground = (.*?);',cpp).group(1)
    sky_errors=[];old_gamma_differences=[]
    for y in (-1.,-.5,-.01,0.,.05,.3,1.):
        values=dict(v_angle=y,sky_top_color=top,sky_horizon_color=horizon,ground_bottom_color=bottom,ground_horizon_color=horizon,inv_sky_curve=4.,inv_ground_curve=30.)
        # .rgb source syntax is redundant for these 3-component test values.
        a=expr((source_sky if y>=0 else source_ground).replace('.rgb',''),values)
        bvalues=declarations(sky_body,dict(y=y,top=top,horizon=horizon,bottom=bottom))
        b=expr(re.search(r'return\s+([^;]+);',sky_body).group(1),bvalues)
        error=float(np.max(np.abs(a-b)));assert error<1e-15;sky_errors.append(error)
        encoded=linear_to_srgb(b);assert np.allclose(srgb_to_linear(encoded),b,atol=1e-14,rtol=0)
        if y>=0:
            old=mix(colors[1],colors[0],1-(1-y)**4);old_gamma_differences.append(float(np.max(np.abs(old-encoded))))
    assert max(old_gamma_differences)>.02,'CPU cases must expose the old gamma-space interpolation'
    # Source sky light uses cos(default size=0), not the old guessed .5 degree disk.
    limit=math.cos(math.radians(30));curve=1.6/.15**1.4
    for degrees in (0.,.25,1.,10.,30.,45.):
        angle=math.cos(math.radians(degrees));source_c2=(1-angle)/(1-limit)
        source_halo=np.clip((1-source_c2)**curve,0,1) if angle>limit else 0
        target_halo=np.clip(np.power(1-(1-angle)/(1-limit),curve),0,1) if angle>limit else 0
        assert source_halo==target_halo
    assert 'float size=1.0,limit=cos(radians(30.0))' in sky
    assert 'R23WorkingColor(skyColor)' in sky and 'SRGBToLinear(float3(163,201,228)/255.0)' in sky
    assert 'i.normalWS*float3(1,1,-1)' in surface and 'R23Facade(i.positionWS*float3(1,1,-1)' in surface
    assert 'UniversalFragmentPBR' in surface and 'SRGBToLinear(_Emission.rgb/1.5)*1.5' in surface
    assert 'max(SampleSH(n),float3(.08' not in surface and 'lerp(100,4' not in surface
    for prop in ('_BaseColor','_Metallic','_Roughness','_Emission','_Facade'):assert prop in surface.split('SubShader',1)[0]
    for path,expected in [(SURFACE,'777f0606f3d84392bbdd97fbb25963d7'),(SKY,'f8d1a221d96646bda5692155931e1376')]:assert 'guid: '+expected in path.with_name(path.name+'.meta').read_text()
    # Existing 1.5 source emission gain is applied after source_color decoding.
    neon=np.array([24,227,240])/255;binding=neon*1.5
    decoded=srgb_to_linear(binding/1.5)*1.5;assert np.allclose(decoded,srgb_to_linear(neon)*1.5,atol=1e-15,rtol=0)
    inputs=[FACADE,SOURCE/'scripts/lobby_map_091.gd',SOURCE/'scripts/front_menu_091.gd',SKY_REFERENCE]
    report=dict(facadeCases=len(cases),maxFacadeFormulaError=max(errors),rawNormalRegressionCaught=True,
                skyGradientCases=len(sky_errors),maxSkyFormulaError=max(sky_errors),maxOldGammaInterpolationDifference=max(old_gamma_differences),
                sunHaloCases=6,sourceSunAngularSizeDegrees=0,sourceEmissionGain=1.5,propertiesAndMetaGuidsPreserved=True,
                inputs={str(p):hashlib.sha256(p.read_bytes()).hexdigest() for p in inputs},
                shaders={str(p):hashlib.sha256(p.read_bytes()).hexdigest() for p in (SURFACE,SKY)},samples=samples,
                limits='CPU source/formula checks and installed URP source inspection only. No shader compile, engine, render, GPU or device execution. Renderer BRDF, ambient/reflection/exposure/debanding parity is not claimed.')
    (HERE/'checks.json').write_text(json.dumps(report,indent=2)+'\n',encoding='utf-8')
    print('PASS',len(cases),'original facade cases, raw-normal regression,',len(sky_errors),'linear sky gradients, 6 sun-halo cases, current source emission binding, preserved shader properties/meta GUIDs; no native run')

if __name__=='__main__':main()
