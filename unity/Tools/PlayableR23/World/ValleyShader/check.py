"""CPU comparison of actual original GLSL and published HLSL albedo expressions.
No shader compilation, Unity/Godot process, GPU or render execution.
"""
from pathlib import Path
import ast,hashlib,json,math,re
import numpy as np

ROOT=Path(__file__).resolve().parents[5]
HERE=Path(__file__).resolve().parent
ORIGINAL=Path('C:/Users/aling/Documents/Codex/2026-11-03/ashfall-r23/source/assets/materials/alpine_terrain.gdshader')
EXPORT=ROOT/'unity/Tools/PlayableR23/SourceWorldExport/runs/prepared-r23-valley-range/valley/world.json'
SHADER=ROOT/'unity/Assets/Ashfall/Integration/Playable/World/Resources/R23AlpineTerrain.shader'

def smoothstep(low,high,value):
    t=np.clip((value-low)/(high-low),0,1);return t*t*(3-2*t)
def mix(a,b,t):return a*(1-t)+b*t
def normalize(v):return v/np.linalg.norm(v)
FUNCTIONS=dict(sin=np.sin,smoothstep=smoothstep,mix=mix,lerp=mix,normalize=normalize,
               vec3=lambda *x:np.asarray(x),float3=lambda *x:np.asarray(x))

def expression(text,values):
    """Small safe expression evaluator; only the authored shader math subset."""
    def visit(node):
        if isinstance(node,ast.Expression):return visit(node.body)
        if isinstance(node,ast.Constant) and isinstance(node.value,(int,float)):return node.value
        if isinstance(node,ast.Name):return values[node.id]
        if isinstance(node,ast.Attribute) and node.attr in ('x','y','z'):return visit(node.value)[('x','y','z').index(node.attr)]
        if isinstance(node,ast.UnaryOp) and isinstance(node.op,ast.USub):return -visit(node.operand)
        if isinstance(node,ast.BinOp):
            a,b=visit(node.left),visit(node.right)
            if isinstance(node.op,ast.Add):return a+b
            if isinstance(node.op,ast.Sub):return a-b
            if isinstance(node.op,ast.Mult):return a*b
            if isinstance(node.op,ast.Div):return a/b
        if isinstance(node,ast.Call) and isinstance(node.func,ast.Name) and node.func.id in FUNCTIONS:
            return FUNCTIONS[node.func.id](*[visit(arg) for arg in node.args])
        raise AssertionError('Unexpected shader expression '+ast.dump(node))
    return visit(ast.parse(text.strip(),mode='eval'))

def formula(body,point,normal,snow_height,hlsl=False):
    values=dict(terrain_position=np.asarray(point),terrain_normal=np.asarray(normal),
                terrainNormal=np.asarray(normal),p=np.asarray(point),snow_height=snow_height,_SnowHeight=snow_height)
    for _,name,rhs in re.findall(r'\b(float3|vec3|float)\s+(\w+)\s*=\s*([^;]+);',body):values[name]=expression(rhs,values)
    result=re.search(r'return\s+([^;]+);' if hlsl else r'ALBEDO\s*=\s*([^;]+);',body).group(1)
    return expression(result,values),values

def linear_to_srgb(value):
    x=np.asarray(value);return np.where(x<=.0031308,12.92*x,1.055*np.power(np.maximum(x,0),1/2.4)-.055)
def srgb_to_linear(value):
    x=np.asarray(value);return np.where(x<=.04045,x/12.92,np.power((x+.055)/1.055,2.4))

def main():
    original=ORIGINAL.read_text(encoding='utf-8');hlsl=SHADER.read_text(encoding='utf-8')
    source_body=original.split('void fragment() {',1)[1]
    target_body=hlsl.split('// SOURCE_ALBEDO_BEGIN',1)[1].split('// SOURCE_ALBEDO_END',1)[0].split('{',1)[1]
    export=json.loads(EXPORT.read_text(encoding='utf-8'));rows=[]
    shaders={r['id']:r for r in export['resources'] if r.get('class')=='Shader'}
    source_hash=hashlib.sha256(ORIGINAL.read_bytes()).hexdigest()
    for material in export['resources']:
        if material.get('class')!='ShaderMaterial':continue
        shader=shaders[material['properties']['shader']['resourceRef']]
        assert shader['source']=='res://assets/materials/alpine_terrain.gdshader'
        assert shader['shaderSha256']==source_hash
        value=material['uniforms']['snow_height']
        assert value==material['properties']['shader_parameter/snow_height'] and math.isfinite(value)
        rows.append(dict(sourceMaterial=material['id'],sourceShader=shader['id'],property='_SnowHeight',value=value))
    assert len(rows)==24
    heights=sorted(row['value'] for row in rows);snow=55.0
    cases=[('below',(-80,snow-30,-120),(0,1,0),snow),
           ('lower edge',(0,snow-12,0),(0,1,0),snow),
           ('mid altitude',(0,snow-1,0),(0,1,0),snow),
           ('upper edge',(0,snow+10,0),(0,1,0),snow),
           ('high vertical wall',(37,snow+30,-110),(1,0,0),snow),
           ('high underside',(-47,snow+30,170),(0,-1,0),snow),
           ('slope lower edge',(0,snow+30,0),(math.sqrt(1-.52**2),.52,0),snow),
           ('slope midpoint',(0,snow+30,0),(math.sqrt(1-.71**2),.71,0),snow),
           ('slope upper edge',(0,snow+30,0),(math.sqrt(1-.9**2),.9,0),snow),
           ('normal magnitude',(37,snow,-110),(0,7,0),snow),
           ('minimum actual uniform',(-131,heights[0]+2,73),(.4,.8,.2),heights[0]),
           ('maximum actual uniform',(183,heights[-1]-4,-91),(-.2,.9,.1),heights[-1])]
    errors=[];samples=[]
    for label,p,n,height in cases:
        expected,source_values=formula(source_body,p,n,height)
        actual,target_values=formula(target_body,p,n,height,True)
        error=float(np.max(np.abs(expected-actual)));assert error<1e-12,(label,error)
        for field in ('drift','altitude','slope','accumulation','strata','stone','snow'):
            assert np.allclose(source_values[field],target_values[field],atol=1e-12,rtol=0),field
        assert np.isfinite(actual).all() and (actual>=0).all() and (actual<=.53).all()
        gamma=linear_to_srgb(actual);assert np.allclose(srgb_to_linear(gamma),actual,atol=1e-12,rtol=0)
        samples.append(dict(case=label,snowHeight=height,linearAlbedo=actual.tolist(),gammaWorkingAlbedo=gamma.tolist(),accumulation=float(source_values['accumulation'])))
        errors.append(error)
    assert samples[1]['accumulation']==0 and samples[2]['accumulation']==.45 and samples[3]['accumulation']==.9
    assert samples[4]['accumulation']==.08 and samples[5]['accumulation']==.08
    p=(37,snow,-110);a,_=formula(target_body,p,(0,1,0),snow,True)
    b,_=formula(target_body,(37,snow,110),(0,1,0),snow,True)
    assert np.max(np.abs(a-b))>.01,'Reference position should detect an erroneous second Z reflection'
    up,_=formula(target_body,p,(0,1,0),snow,True);scaled,_=formula(target_body,p,(0,7,0),snow,True)
    assert np.allclose(up,scaled,atol=1e-12,rtol=0)
    assert 'float3 sourcePosition:TEXCOORD1' in hlsl and 'float3 sourceNormal:TEXCOORD2' in hlsl
    assert 'o.sourcePosition=a.sourcePosition;o.sourceNormal=a.sourceNormal;' in hlsl
    assert 'R23SourceAlbedo(i.sourcePosition,i.sourceNormal)' in hlsl and 'i.color' not in hlsl
    assert 'UNITY_COLORSPACE_GAMMA' in hlsl and hlsl.count('LinearToSRGB(')==1
    assert 'Cull Off' in hlsl and 'R23BurleyLight' in hlsl and 'UNITY_VERTEX_OUTPUT_STEREO' in hlsl
    assert 'SPECULAR' not in target_body and 'Roughness' not in hlsl
    report=dict(source=str(ORIGINAL),sourceSha256=source_hash,exportSha256=hashlib.sha256(EXPORT.read_bytes()).hexdigest(),
                shaderSha256=hashlib.sha256(SHADER.read_bytes()).hexdigest(),actualMaterials=len(rows),snowHeightRange=[heights[0],heights[-1]],
                formulaCases=len(cases),maxAlbedoAbsoluteError=max(errors),stageComparisons=True,linearGammaRoundtrips=True,
                sourceCoordinateContract=True,sourceVertexColorIgnored=True,samples=samples,
                limits='CPU authored-expression equivalence, channel/gamma contract and package API inspection only. No HLSL compilation, native engine, GPU, render, headset, Godot-vs-URP lighting/tone parity claim.')
    (HERE/'materials.json').write_text(json.dumps(rows,indent=2)+'\n',encoding='utf-8')
    (HERE/'checks.json').write_text(json.dumps(report,indent=2)+'\n',encoding='utf-8')
    print('PASS',len(cases),'primary GLSL/HLSL formula cases; all intermediate terms equal;',len(rows),'actual snow_height values;',
          'single Gamma encode contract and source-local channels; no shader compile/render')

if __name__=='__main__':main()
