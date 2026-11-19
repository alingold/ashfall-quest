"""Render the emitted native triangles and spawn positions in three CPU views."""
from pathlib import Path
import ctypes
import json
import mmap
import os
import struct
import sys

os.environ['OPENBLAS_NUM_THREADS']='1'
import numpy as np
from PIL import Image, ImageDraw, ImageFont
TMP = Path(__file__).resolve().parent
RASTER = ctypes.CDLL(str(TMP/'R26Raster.so')).Raster
RASTER.argtypes = [ctypes.c_int, ctypes.c_int]+[ctypes.c_void_p]*5
RASTER.restype = None
SIZE = 520

def render(folder, destination, window=None, ceiling=None):
    m=json.loads((folder/'map.json').read_text())
    lo,hi=np.array([m['boundsMin'],m['boundsMax']])
    center=(lo+hi)*.5
    width=max(hi[0]-lo[0],hi[2]-lo[2])/2
    if window is not None:
        center[0],center[2],width=window
    center[1]=m.get('gameSpawn',m['spawn'])[1]
    scale=(SIZE-38)/(2*width)
    views=[
        ('Overhead',np.array([[1,0,0],[0,0,1],[0,1,0]])),
        ('Oblique from southwest',np.array([[.86,0,.50],[.27,-.84,-.45],[.42,.53,-.72]])),
        ('Oblique from northeast',np.array([[-.86,0,-.50],[-.27,-.84,.45],[-.42,.53,.72]])),
    ]
    materials=[]
    for row in m['materials']:
        tex=folder/row['color']
        if row['color'] and tex.is_file():
            with Image.open(tex) as f:
                materials.append(np.asarray(f.convert('RGB').resize((1,1)))[0,0].astype(float))
        else:
            materials.append(np.array([183,183,183]))
    depths=[np.full((SIZE,SIZE),-np.inf,np.float32) for _ in views]
    images=[np.full((SIZE,SIZE,3), (33,41,52),np.uint8) for _ in views]
    with (folder/'geometry.bytes').open('rb') as f:
        blob=mmap.mmap(f.fileno(),0,access=mmap.ACCESS_READ)
        magic,n=struct.unpack_from('<ii',blob)
        assert magic==0x52323357 and n==m['chunks'] and m['version']==1
        cursor=8
        for _ in range(n):
            material,collision,visible,nv,ni=struct.unpack_from('<iiiii',blob,cursor);cursor+=20
            v=np.frombuffer(blob,dtype='<f4',count=nv*12,offset=cursor).reshape(nv,12);cursor+=nv*48
            ids=np.frombuffer(blob,dtype='<i4',count=ni,offset=cursor).reshape(-1,3);cursor+=ni*4
            if not visible:
                continue
            if ceiling is not None:
                ids=np.ascontiguousarray(ids[v[ids,1].max(axis=1)<=ceiling])
                if not len(ids):
                    continue
            pos=v[:,:3]-center
            if (np.abs(pos[:,[0,2]])-width*1.65).min()>0:
                continue
            normal=v[ids,3:6].mean(axis=1)
            light=np.clip(.59+.37*np.abs(normal@np.array([-.42,.79,.44])),.58,1)
            col=v[ids,8:11].mean(axis=1)
            color=np.ascontiguousarray(np.clip(col*materials[material]*light[:,None],0,255).astype(np.uint8))
            for (_,rot),d,img in zip(views,depths,images):
                out=np.ascontiguousarray(pos@rot.T,dtype=np.float32)
                out[:,:2]=out[:,:2]*scale+SIZE*.5
                RASTER(SIZE,len(ids),out.ctypes.data,ids.ctypes.data,color.ctypes.data,d.ctypes.data,img.ctypes.data)
        assert cursor==len(blob)
    font=ImageFont.truetype('/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf',17)
    panel=Image.new('RGB',(SIZE*3,SIZE+34),(22,27,34))
    for i,((title,rot),img) in enumerate(zip(views,images)):
        view=Image.fromarray(img); draw=ImageDraw.Draw(view)
        for field,color in [('gameSpawn','#ffda6d'),('warmupSpawn','#9af7df')]:
            if field not in m:continue
            u,v,_=(np.array(m[field])-center)@rot.T
            x,y=u*scale+SIZE*.5,v*scale+SIZE*.5
            draw.ellipse((x-5,y-5,x+5,y+5),fill=color,outline='#252a30',width=2)
        panel.paste(view,(i*SIZE,34))
        ImageDraw.Draw(panel).text((i*SIZE+10,5),title,fill='white',font=font)
    panel.save(destination)

if __name__=='__main__':
    args=[float(x) for x in sys.argv[3:]]
    render(Path(sys.argv[1]),Path(sys.argv[2]),args[:3] or None,args[3] if len(args)>3 else None)
