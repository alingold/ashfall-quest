"""Rasterize genuine small source SVGs with existing CPU path parser."""
from pathlib import Path
import ast,io,re,xml.etree.ElementTree as ET
from PIL import Image,ImageDraw
ROOT=Path(__file__).resolve().parents[4];SOURCE=Path('C:/Users/aling/Documents/Codex/2026-11-03/ashfall-r23/source/assets/ui')
parser_file=ROOT/'unity/Tools/PlayableR23/UI/SourceFidelity/prepare_icons.py'
parser=next(n for n in ast.parse(parser_file.read_text()).body if isinstance(n,ast.FunctionDef)and n.name=='paths')
exec(compile(ast.Module(body=[parser],type_ignores=[]),str(parser_file),'exec'))
def rasterize(name,source=None):
    svg=ET.parse((SOURCE if source is None else Path(source))/(name+'.svg')).getroot();width,height=map(float,svg.attrib['viewBox'].split()[2:]);factor=1024/width
    image=Image.new('RGBA',(1024,round(height*factor)));draw=ImageDraw.Draw(image)
    def color(value,opacity):
        if value=='none':return None
        if value in ('white','black'):rgb=(255,255,255)if value=='white'else(0,0,0)
        else:rgb=tuple(int(value[j:j+2],16)for j in (1,3,5))
        return rgb+(round(opacity*255),)
    def visit(el,parent):
        style=dict(parent);style.update(el.attrib);tag=el.tag.split('}')[-1];opacity=float(style.get('opacity',1))
        fill=color(style.get('fill','black'),opacity);stroke=color(style.get('stroke','none'),opacity);thick=max(1,round(float(style.get('stroke-width',1))*factor))
        if tag=='path':
            for points,closed in paths(el.attrib['d']):
                points=[(x*factor,y*factor)for x,y in points]
                if fill and len(points)>=3:draw.polygon(points,fill=fill)
                if stroke and len(points)>1:
                    draw.line(points,fill=stroke,width=thick,joint='curve')
                    if style.get('stroke-linecap')=='round':
                        for x,y in points:draw.ellipse((x-thick/2,y-thick/2,x+thick/2,y+thick/2),fill=stroke)
        elif tag=='rect':
            x=float(style.get('x',0))*factor;y=float(style.get('y',0))*factor;w=float(style['width'])*factor;h=float(style['height'])*factor;radius=float(style.get('rx',0))*factor
            draw.rounded_rectangle((x,y,x+w,y+h),radius=radius,fill=fill,outline=stroke,width=thick)
        elif tag=='ellipse':
            x=float(style['cx'])*factor;y=float(style['cy'])*factor;rx=float(style['rx'])*factor;ry=float(style['ry'])*factor;draw.ellipse((x-rx,y-ry,x+rx,y+ry),fill=fill,outline=stroke,width=thick)
        elif tag not in ('svg','g'):raise ValueError(tag)
        for child in el:visit(child,style)
    visit(svg,{});out=io.BytesIO();image.resize((256,round(height*256/width)),Image.Resampling.LANCZOS).save(out,format='PNG');return out.getvalue()
