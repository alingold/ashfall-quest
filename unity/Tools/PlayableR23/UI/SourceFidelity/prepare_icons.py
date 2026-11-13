"""Render the actual small R23 SVG icon subset with Pillow, outside Assets. No engine/GPU."""
from pathlib import Path
import re
import xml.etree.ElementTree as ET
from PIL import Image,ImageDraw

HERE=Path(__file__).resolve().parent
ROOT=HERE.parents[4]
SOURCE=ROOT/'quest-r24/source/assets/ui'
OUT=HERE/'StagedResources/R23SourceUI'
OUT.mkdir(parents=True,exist_ok=True)
SCALE=4

def paths(data):
    tokens=re.findall(r'[A-Za-z]|[-+]?(?:\d*\.\d+|\d+)(?:[eE][-+]?\d+)?',data)
    result=[];points=[];at=(0.,0.);start=at;i=0;command=None
    while i<len(tokens):
        if tokens[i].isalpha():command=tokens[i];i+=1
        op=command.upper();relative=command.islower()
        if op=='Z':
            points.append(start);result.append((points,True));points=[];at=start;command=None;continue
        n={'M':2,'L':2,'H':1,'V':1,'Q':4}[op]
        values=list(map(float,tokens[i:i+n]));i+=n
        def xy(x,y):return (x+at[0],y+at[1]) if relative else (x,y)
        if op in ('M','L'):
            next_at=xy(*values)
            if op=='M':
                if points:result.append((points,False))
                points=[next_at];start=next_at;command='l' if relative else 'L'
            else:points.append(next_at)
        elif op=='H':next_at=(values[0]+at[0] if relative else values[0],at[1]);points.append(next_at)
        elif op=='V':next_at=(at[0],values[0]+at[1] if relative else values[0]);points.append(next_at)
        else:
            control=xy(*values[:2]);next_at=xy(*values[2:]);begin=at
            for step in range(1,25):
                t=step/24.;points.append(((1-t)**2*begin[0]+2*(1-t)*t*control[0]+t*t*next_at[0],(1-t)**2*begin[1]+2*(1-t)*t*control[1]+t*t*next_at[1]))
        at=next_at
    if points:result.append((points,False))
    return result

def render(name):
    svg=ET.parse(SOURCE/(name+'.svg')).getroot();width,height=map(float,svg.attrib['viewBox'].split()[2:])
    factor=256/width*SCALE;image=Image.new('RGBA',(256*SCALE,round(height*factor)));draw=ImageDraw.Draw(image)
    def color(value,opacity):
        if value=='none':return None
        if value=='white':rgb=(255,255,255)
        elif value=='black':rgb=(0,0,0)
        else:rgb=tuple(int(value[j:j+2],16) for j in (1,3,5))
        return rgb+(round(opacity*255),)
    def visit(element,inherited):
        style=dict(inherited);style.update(element.attrib);tag=element.tag.split('}')[-1];opacity=float(style.get('opacity',1));fill=color(style.get('fill','black'),opacity);stroke=color(style.get('stroke','none'),opacity);thickness=max(1,round(float(style.get('stroke-width',1))*factor))
        if tag=='path':
            for points,closed in paths(element.attrib['d']):
                scaled=[(x*factor,y*factor) for x,y in points]
                if fill and len(scaled)>=3:draw.polygon(scaled,fill=fill)
                if stroke and len(scaled)>1:
                    draw.line(scaled,fill=stroke,width=thickness,joint='curve')
                    if style.get('stroke-linecap')=='round':
                        for x,y in scaled:draw.ellipse((x-thickness/2,y-thickness/2,x+thickness/2,y+thickness/2),fill=stroke)
        elif tag=='circle':
            x=float(style['cx'])*factor;y=float(style['cy'])*factor;r=float(style['r'])*factor
            draw.ellipse((x-r,y-r,x+r,y+r),fill=fill,outline=stroke,width=thickness)
        elif tag not in ('svg','g'):raise ValueError('Unsupported source SVG element '+tag)
        for child in element:visit(child,style)
    visit(svg,{})
    image.resize((256,round(height*256/width)),Image.Resampling.LANCZOS).save(OUT/(name+'.png'))
    print(name)
for key in [f'settings_{i}' for i in range(4)]+[f'pause0993_{i}' for i in range(4)]+['sprint_r4']:
    render(key)
