from pathlib import Path
import ast,hashlib,json,math,struct,uuid,shutil,io,re,wave,xml.etree.ElementTree as ET
import numpy as np
from PIL import Image,ImageDraw
from source import ROOT,read,verify,INDEX,CONTENT_ID
HERE=Path(__file__).resolve().parent;OUT=ROOT/'unity/Assets/StreamingAssets/PlayableR23/Consumables/Supplies'
FAMILIES={'pistol':(16,'477bd4'),'smg':(20,'a77332'),'rifle':(32,'9d9c2c'),'shells':(6,'7f3cc4'),'sniper':(5,'a83f35'),'builds':(6,'3f4851')}
def rgb(value):return [int(value[i:i+2],16)/255 for i in [0,2,4]]
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def rasterize(path):
 parser=next(n for n in ast.parse((ROOT/'unity/Tools/PlayableR23/UI/SourceFidelity/prepare_icons.py').read_text()).body if isinstance(n,ast.FunctionDef)and n.name=='paths');ns={'re':re};exec(compile(ast.Module(body=[parser],type_ignores=[]),'source_svg_paths','exec'),ns)
 root=ET.parse(path).getroot();width,height=map(float,root.attrib['viewBox'].split()[2:]);factor=4;canvas=Image.new('RGBA',(int(width*factor),int(height*factor)))
 for e in root:
  assert e.tag.split('}')[-1]=='path';tx=ty=0
  if e.get('transform'):
   match=re.fullmatch(r'translate\(([-.\d]+) ([-.\d]+)\)',e.attrib['transform']);assert match;tx,ty=map(float,match.groups())
  layer=Image.new('RGBA',canvas.size);draw=ImageDraw.Draw(layer);alpha=round(float(e.get('opacity',1))*255)
  def color(value):return tuple(int(value[i:i+2],16)for i in [1,3,5])+(alpha,)
  for points,closed in ns['paths'](e.attrib['d']):
   points=[((x+tx)*factor,(y+ty)*factor)for x,y in points]
   if e.get('fill'):draw.polygon(points,fill=color(e.get('fill')))
   if e.get('stroke'):draw.line(points,fill=color(e.get('stroke')),width=max(1,round(float(e.get('stroke-width',1))*factor)))
  canvas=Image.alpha_composite(canvas,layer)
 output=io.BytesIO();canvas.resize((240,160),Image.Resampling.LANCZOS).save(output,format='PNG');return output.getvalue()
def main():
 OUT.mkdir(parents=True,exist_ok=True);ns={'np':np,'math':math,'TAU':math.tau,'parts':[],'materials':[]};tree=ast.parse((HERE.parent/'prepare.py').read_text())
 exec(compile(ast.Module(body=[x for x in tree.body if isinstance(x,ast.FunctionDef)],type_ignores=[]),'existing_source_item_recipes','exec'),ns)
 rows=[]
 def output(family,glyph=''):
  groups={}
  for name,v,ix,mi,cap in ns['parts']:groups.setdefault(mi,[]).append((v,ix))
  with(OUT/(family+'.meshbin')).open('wb')as f:
   f.write(struct.pack('<ii',0x52323343,len(groups)))
   for mi,parts in groups.items():
    vertices=[];indices=[];offset=0
    for v,ix in parts:vertices.append(v);indices.append(ix+offset);offset+=len(v)
    v=np.concatenate(vertices);ix=np.concatenate(indices);label=('SourceSupplyFinish'+str(mi)).encode();f.write(struct.pack('<i',len(label))+label+struct.pack('<iiii',len(v),len(ix),mi,0)+v.tobytes()+ix.tobytes())
  (OUT/(family+'.json')).write_text(json.dumps(dict(materials=ns['materials'],anchor=[0,0,0],heldOffset=[0,0,0],glyph=glyph,glyphPosition=[.021,.002,-.067],glyphSize=[240*.00038,160*.00038]),indent=2)+'\n')
  rows.append(dict(family=family,amount=1 if family=='backpack'else FAMILIES[family][0],triangles=sum(len(ix)//3 for _,v,ix,mi,cap in ns['parts']),surfaces=len(groups),glyph=glyph))
 for family,(amount,tint)in FAMILIES.items():
  ns['parts']=[];ns['materials']=[];color=ns['mat'](rgb(tint),.35,.58);dark=ns['mat'](rgb('1c2529'),.25,.7);pale=ns['mat'](rgb('e4e8df'),.15,.55)
  box=ns['rounded'];box('ColoredSupplyBody',(.22,.15,.13),(0,0,0),color,.009);box('DarkLid',(.224,.025,.136),(0,.077,0),dark,.005)
  for x in [-.086,.086]:box('CornerBand',(.012,.15,.135),(x,0,0),dark,.002)
  for i in range(3):box('SupplyMark',(.027,.009,.003),(-.057,-.035+i*.022,.067),pale,.001)
  if family=='builds':
   for i in range(3):box('BuildPanel',(.11,.018,.13),(.02,.015+i*.026,.01),pale,.003)
  glyph=''
  if family!='builds':glyph=family+'-glyph.png';(OUT/glyph).write_bytes(rasterize(read('assets/ui/supply_088_'+family+'.svg')))
  output(family,glyph)
 # Genuine field_item_models._backpack: six front webbing rows, carry loop,
 # padded straps, buckles and zipper pulls. No substitute bag primitive.
 ns['parts']=[];ns['materials']=[];fabric=ns['mat']([.60,.13,.085],0,.9);side=ns['mat']([.36,.08,.055],0,.93);web=ns['mat']([.10,.13,.13],0,.95);seam=ns['mat']([.83,.35,.16],0,.82);buckle=ns['mat']([.36,.4,.39],.45,.46);box=ns['rounded']
 box('PaddedBody',(.263,.33,.128),(0,0,0),fabric,.025);box('BackPadding',(.23,.29,.026),(0,.002,-.075),web,.012);box('FrontPanel',(.222,.235,.030),(0,.026,.074),side,.018);box('FrontPocket',(.223,.084,.044),(0,-.117,.079),fabric,.014)
 for i in range(6):box('WebbingRow'+str(i),(.180,.012,.004),(0,.108-i*.032,.092),web,.002)
 for s in [-1,1]:
  box('Piping',(.004,.210,.004),(s*.098,.024,.091),seam,.0015);box('SidePocket',(.026,.12,.075),(s*.137,-.048,0),side,.009);box('PaddedShoulderStrap',(.035,.33,.022),(s*.073,.035,-.102),fabric,.010,rotate_z=-s*.09);box('StrapBuckle',(.040,.032,.026),(s*.084,-.102,-.107),buckle,.004);box('ZipperPull',(.006,.020,.006),(s*.092,.138,.086),buckle,.002)
 ns['tube']('CarryLoop',[[-.042,.157,0],[-.042,.184,0],[-.024,.198,0],[.024,.198,0],[.042,.184,0],[.042,.157,0]],.006,6,web)
 for i in range(2):box('LowerPocketWebbing'+str(i),(.180,.012,.004),(0,-.100-i*.029,.104),web,.002)
 output('backpack')
 routing=json.loads(read('assets/audio/reference0992/manifest.json').read_text());audio=[]
 for key in ['ammo_pickup','build_pickup']:
  src=read(routing['generic'][key][0].removeprefix('res://'));dst=OUT/(key+'.wav');shutil.copyfile(src,dst)
  with wave.open(str(src),'rb')as w:audio.append(dict(action=key,source=str(src),file=dst.name,sha256=sha(src),rate=w.getframerate(),frames=w.getnframes(),channels=w.getnchannels(),pcmSha256=hashlib.sha256(w.readframes(w.getnframes())).hexdigest()))
 inputs=['scripts/supply_catalog_088.gd','scripts/ammo_pickups.gd','scripts/item_catalog_service_088.gd','scripts/field_item_models.gd','scripts/field_consumables.gd','scripts/banana_peel_086.gd']+['assets/ui/supply_088_'+x+'.svg'for x in FAMILIES if x!='builds']
 (OUT/'source-r5.json').write_text(json.dumps(dict(target='Published frozen R24 R6 (R5 item recipes byte-identical)',contentId=CONTENT_ID,inputs=[verify(p)for p in inputs],models=rows,audio=audio,engineRun=False),indent=2)+'\n')
 for p in [OUT]+list(OUT.iterdir()):
  if p.suffix=='.meta':continue
  meta=Path(str(p)+'.meta')
  if not meta.exists():meta.write_text('fileFormatVersion: 2\nguid: '+uuid.uuid5(uuid.NAMESPACE_URL,'ashfall-r5-supplies:'+p.name).hex+'\n'+('folderAsset: yes\n'if p.is_dir()else''))
 print(rows)
if __name__=='__main__':main()
