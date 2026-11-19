from pathlib import Path
import json
import sys
import numpy as np
import R26ArchiveCandidate as c
from R26AuditArchive import native_body

for slug in c.read(c.TMP/'R26SelectedSlugs.json'):
    folder=c.STAGE/slug
    m=c.read(folder/'map.json')
    scene,_=native_body(folder,m)
    prov=c.read(folder/'provenance.json')
    points=np.array(c.read(folder/'spawn-proof.json')['finalRuntimeValidation']['matchSites'])
    raw=c.read(c.input_files(slug)[0])
    center=np.array(raw['center'])*[1,1,-1]
    radius=raw['extent']*.33
    mid=np.median(points[:,1])
    top=scene.bounds[:,4].max()+2
    def roof(point):
        # Real collision from the sky, with 7 m above the verified gameplay
        # level distinguishing indoor cover from terrain and flat pads.
        return scene.floor(point[0],point[2],top,top-(point[1]+7)) is not None
    probes=np.array([[center[0]+x,mid,center[2]+z] for x in np.linspace(-radius,radius,9) for z in np.linspace(-radius,radius,9)])
    print(slug,sum(roof(x) for x in points),len(points),sum(roof(x) for x in probes),len(probes),flush=True)
