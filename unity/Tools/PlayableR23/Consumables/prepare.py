"""CPU translation of the actual authored R23 field_item_models.gd soda/shaker recipes."""
from pathlib import Path
import hashlib,json,math,struct
import numpy as np
ROOT=Path(__file__).resolve().parents[4];SRC=ROOT/'quest-r24/source';OUT=ROOT/'unity/Assets/StreamingAssets/PlayableR23/Consumables';OUT.mkdir(parents=True,exist_ok=True)
TAU=math.tau;parts=[];materials=[]
def unit(v):
    v=np.array(v,float);length=np.linalg.norm(v);return v/length if length>1e-12 else v
def mat(color,metal=0,rough=.65):
    materials.append({'color':list(color)+[1],'metallic':metal,'roughness':rough});return len(materials)-1
def add(name,vertices,normals,finish,at=(0,0,0),scale=(1,1,1),rotate=0,cap=False,rotate_z=0):
    v=np.array(vertices,float)*scale;n=np.array(normals,float)/scale
    if rotate_z:
        c,s=math.cos(rotate_z),math.sin(rotate_z);rz=np.array([[c,-s,0],[s,c,0],[0,0,1]]);v=v@rz.T;n=n@rz.T
    c,s=math.cos(rotate),math.sin(rotate);rotation=np.array([[c,0,s],[0,1,0],[-s,0,c]])
    v=v@rotation.T+at;n=n@rotation.T;n/=np.maximum(np.linalg.norm(n,axis=1)[:,None],1e-20)
    v[:,2]*=-1;n[:,2]*=-1
    indices=np.arange(len(v),dtype='<i4').reshape(-1,3)[:,[0,2,1]].ravel()
    assert np.isfinite(v).all() and np.isfinite(n).all()
    parts.append((name,np.c_[v,n].astype('<f4'),indices,finish,cap))
def lathe(name,profile,segments,finish,cap=False):
    v=[];n=[]
    for (r,y),(rr,yy) in zip(profile,profile[1:]):
        slope=unit([yy-y,r-rr])
        for i in range(segments):
            a,b=TAU*i/segments,TAU*(i+1)/segments
            q=np.array([[math.sin(a)*r,y,math.cos(a)*r],[math.sin(b)*r,y,math.cos(b)*r],[math.sin(b)*rr,yy,math.cos(b)*rr],[math.sin(a)*rr,yy,math.cos(a)*rr]])
            for k in [0,2,1,0,3,2]:
                p=q[k];radial=unit([p[0],0,p[2]]);v.append(p);n.append([radial[0]*slope[0],slope[1],radial[2]*slope[0]])
    add(name,v,n,finish,cap=cap)
def torus(name,radius,thickness,at,finish,scale=(1,1,1),cap=False):
    v=[];n=[];r=thickness*.5
    def point(a,b):
        normal=np.array([math.sin(a)*math.cos(b),math.sin(b),math.cos(a)*math.cos(b)])
        return np.array([math.sin(a)*radius,0,math.cos(a)*radius])+normal*r,normal
    for i in range(24):
        for j in range(6):
            q=[point(TAU*i/24,TAU*j/6),point(TAU*(i+1)/24,TAU*j/6),point(TAU*(i+1)/24,TAU*(j+1)/6),point(TAU*i/24,TAU*(j+1)/6)]
            for k in [0,2,1,0,3,2]:v.append(q[k][0]);n.append(q[k][1])
    add(name,v,n,finish,at,scale,cap=cap)
def cylinder(name,radius,height,at,finish,scale=(1,1,1),cap=False):
    v=[];n=[]
    for i in range(24):
        a,b=TAU*i/24,TAU*(i+1)/24
        q=[[math.sin(a)*radius,-height*.5,math.cos(a)*radius],[math.sin(b)*radius,-height*.5,math.cos(b)*radius],[math.sin(b)*radius,height*.5,math.cos(b)*radius],[math.sin(a)*radius,height*.5,math.cos(a)*radius]]
        for k in [0,2,1,0,3,2]:v.append(q[k]);n.append(unit([q[k][0],0,q[k][2]]))
        for sign in [-1,1]:
            ring=[[0,sign*height*.5,0],[math.sin(a)*radius,sign*height*.5,math.cos(a)*radius],[math.sin(b)*radius,sign*height*.5,math.cos(b)*radius]]
            for k in ([0,2,1] if sign>0 else [0,1,2]):v.append(ring[k]);n.append([0,sign,0])
    add(name,v,n,finish,at,scale,cap=cap)
def rounded(name,size,at,finish,bevel=.004,rotate=0,cap=False,rotate_z=0):
    half=np.array(size)*.5;radius=min(max(bevel,.00001),half.min()*.8);inner=half-radius;axes=np.eye(3);v=[];n=[]
    for axis in range(3):
        u,w,normal=axes[(axis+1)%3],axes[(axis+2)%3],axes[axis];us,vs=half[(axis+1)%3],half[(axis+2)%3]
        ug=[-us,-us+radius,us-radius,us];vg=[-vs,-vs+radius,vs-radius,vs]
        for sign in [-1,1]:
            for j in range(3):
                for k in range(3):
                    q=[normal*half[axis]*sign+u*ug[j]+w*vg[k],normal*half[axis]*sign+u*ug[j+1]+w*vg[k],normal*half[axis]*sign+u*ug[j+1]+w*vg[k+1],normal*half[axis]*sign+u*ug[j]+w*vg[k+1]]
                    for index in ([0,2,1,0,3,2] if sign>0 else [0,1,2,0,2,3]):
                        core=np.clip(q[index],-inner,inner);out=unit(q[index]-core);v.append(core+out*radius);n.append(out)
    add(name,v,n,finish,at,rotate=rotate,cap=cap,rotate_z=rotate_z)
def wrapped(name,radii,heights,angles,finish,segments=10):
    v=[];n=[];slope=unit([heights[1]-heights[0],radii[0]-radii[1]])
    for i in range(segments):
        a=angles[0]+(angles[1]-angles[0])*i/segments;b=angles[0]+(angles[1]-angles[0])*(i+1)/segments
        q=[[math.sin(a)*radii[0],heights[0],math.cos(a)*radii[0]],[math.sin(b)*radii[0],heights[0],math.cos(b)*radii[0]],[math.sin(b)*radii[1],heights[1],math.cos(b)*radii[1]],[math.sin(a)*radii[1],heights[1],math.cos(a)*radii[1]]]
        for k in [0,2,1,0,3,2]:
            p=q[k];radial=unit([p[0],0,p[2]]);v.append(p);n.append([radial[0]*slope[0],slope[1],radial[2]*slope[0]])
    add(name,v,n,finish,rotate=-math.pi*.5)
def soda():
    blue=mat([.10,.52,.78],.42,.34);silver=mat([.72,.79,.82],.78,.28);pale=mat([.87,.94,.92],.1,.48);ink=mat([.035,.12,.18],.1,.5)
    lathe('CanBody',[(0,-.067),(.031,-.067),(.035,-.062),(.037,-.054),(.037,.048),(.034,.059),(.031,.064),(.030,.064)],32,blue)
    torus('BottomRolledSeam',.032,.0035,(0,-.064,0),silver);torus('TopRolledSeam',.032,.0035,(0,.065,0),silver)
    lathe('RecessedInterior',[(.029,.063),(.029,.043),(0,.042)],32,ink)
    v=[];n=[]
    for i in range(40):
        a,b=TAU*i/40,TAU*(i+1)/40;oa=np.array([math.sin(a)*.0304,0,math.cos(a)*.0304]);ob=np.array([math.sin(b)*.0304,0,math.cos(b)*.0304]);ia=np.array([math.sin(a)*.007,0,.010+math.cos(a)*.010]);ib=np.array([math.sin(b)*.007,0,.010+math.cos(b)*.010])
        for k in [0,2,1,0,3,2]:v.append([oa,ob,ib,ia][k]);n.append([0,1,0])
        for k in [0,2,1,0,3,2]:
            p=[ia,ib,ib-[0,.0018,0],ia-[0,.0018,0]][k];v.append(p);n.append(unit([-p[0]/(.007**2),0,-(p[2]-.010)/(.010**2)]))
    add('PermanentEndPlate',v,n,silver,(0,.0646,0));torus('LidPressGroove',.026,.0008,(0,.0648,0),silver)
    torus('PermanentPullTab',.008,.0025,(0,.067,-.005),silver,(.65,.30,1.10));cylinder('PermanentRivet',.0028,.0022,(0,.066,-.010),silver)
    cylinder('RemovableOpeningSeal',.0104,.001,(0,.0652,.010),silver,(.71154,1,1),True)
    for side in [-1,1]:wrapped('SideBand'+str(side),(.03745,.03745),(-.042,.042),(side*.60-.28,side*.60+.28),pale)
    wrapped('ShieldMark',(.03750,.03750),(-.0105,.0205),(-.30,.30),pale);wrapped('ShieldInset',(.03765,.03765),(-.0035,.0155),(-.16,.16),blue)
    for i in range(3):
        half=(.018-i*.003)/(.03745*2);wrapped('LabelRule'+str(i),(.03745,.03745),(-.024-i*.004-.0006,-.024-i*.004+.0006),(-half,half),pale,8)
def shaker():
    blue=mat([.10,.44,.60],.15,.42);rubber=mat([.045,.09,.115],0,.86);silver=mat([.58,.69,.72],.62,.36);pale=mat([.68,.88,.89],.1,.52)
    lathe('BottleBody',[(0,-.102),(.030,-.102),(.039,-.093),(.043,-.077),(.047,.070),(.044,.092),(.038,.103),(.030,.103)],28,blue)
    torus('HeelBumper',.036,.006,(0,-.094,0),rubber);torus('NeckCollar',.041,.004,(0,.095,0),silver)
    lathe('OpenMouth',[(.0295,.103),(.0295,.080),(0,.079)],28,rubber)
    cylinder('RibbedLid',.047,.025,(0,.108,0),rubber,cap=True)
    for i in range(16):
        angle=TAU*i/16;rounded('LidRib'+str(i),(.004,.019,.003),(math.sin(angle)*.047,.108,math.cos(angle)*.047),silver,.001,angle,True)
    rounded('FlipSpout',(.026,.012,.038),(0,.127,.008),blue,.005,cap=True)
    def radius(y):return .043+(.047-.043)*max(0,min(1,(y+.077)/.147))
    for i in range(6):
        y=-.057+i*.022;r=radius(y)+.00045;half=(.009 if i%2 else .015)/(r*2);center=math.asin(.014/r)
        wrapped('MeasureTick'+str(i),(radius(y-.0009)+.00045,radius(y+.0009)+.00045),(y-.0009,y+.0009),(center-half,center+half),pale,6)
    for side in [-1,1]:wrapped('GripInset'+str(side),(radius(-.060)+.0008,radius(.040)+.0008),(-.060,.040),(side*.75-.11,side*.75+.11),rubber,8)
    wrapped('ShieldMark',(radius(.0045)+.00045,radius(.0315)+.00045),(.0045,.0315),(-.35,.08),pale,8)
def tube(name,points,radius,sides,finish,taper=False,cap=False):
    points=np.array(points,float);rings=[];v=[];n=[]
    for i,p in enumerate(points):
        tangent=unit(points[min(i+1,len(points)-1)]-points[max(i-1,0)]);side=unit(np.cross(tangent,[0,0,-1]))
        if np.dot(side,side)<.1:side=np.array([1,0,0])
        second=unit(np.cross(tangent,side));r=radius*(.15+.85*math.sin(math.pi*i/(len(points)-1))**.45 if taper else 1)
        rings.append([p+(side*math.cos(TAU*k/sides)+second*math.sin(TAU*k/sides))*r for k in range(sides)])
    for i in range(len(points)-1):
        for k in range(sides):
            nxt=(k+1)%sides;q=[rings[i][k],rings[i][nxt],rings[i+1][nxt],rings[i+1][k]]
            for j in [0,2,1,0,3,2]:v.append(q[j]);n.append(unit(q[j]-points[i if j<2 else i+1]))
    for endpoint in [0,len(points)-1]:
        outward=unit(points[endpoint]-points[1 if endpoint==0 else endpoint-1])
        for k in range(sides):
            a,b=rings[endpoint][k],rings[endpoint][(k+1)%sides]
            if np.dot(np.cross(a-points[endpoint],b-points[endpoint]),outward)>0:a,b=b,a
            for p in [points[endpoint],a,b]:v.append(p);n.append(outward)
    add(name,v,n,finish,cap=cap)
def banana_center(t):return np.array([.050*(2*t-1)**2,-.11+.22*t,0])
def banana():
    flesh=mat([.93,.86,.59],0,.8);yellow=mat([.94,.70,.065],0,.59);seam=mat([.56,.37,.055],0,.8);stem=mat([.22,.14,.055],0,.95)
    centers=[banana_center(i/18) for i in range(19)]
    tube('FruitCore',centers,.021,10,flesh,True)
    tube('PeelShell',centers,.0255,10,yellow,True,True)
    for side in [-1,1]:
        points=[banana_center(i/16)+[side*.017,0,.018*math.sin(math.pi*i/16)] for i in range(17)]
        tube('PeelSeam'+str(side),points,.0008,5,seam,cap=True)
    rounded('Stem',(.013,.030,.014),(.048,.112,0),stem,.003,cap=True,rotate_z=-.25)
    rounded('BlossomTip',(.010,.008,.010),(.048,-.113,0),stem,.003,cap=True)
records=[]
for kind,build in [('soda',soda),('shaker',shaker),('banana',banana)]:
    parts=[];materials=[];build()
    authored_names=[p[0] for p in parts];groups={}
    for name,vertices,indices,mi,cap in parts:groups.setdefault((mi,cap),[]).append((vertices,indices))
    combined=[]
    for (mi,cap),members in groups.items():
        vertices=[];indices=[];offset=0
        for v,i in members:vertices.append(v);indices.append(i+offset);offset+=len(v)
        combined.append(('CapSurface'+str(mi) if cap else 'BodySurface'+str(mi),np.concatenate(vertices),np.concatenate(indices),mi,cap))
    parts=combined
    with (OUT/(kind+'.meshbin')).open('wb') as f:
        f.write(struct.pack('<ii',0x52323343,len(parts)))
        for name,v,indices,mi,cap in parts:
            label=name.encode();f.write(struct.pack('<i',len(label)));f.write(label);f.write(struct.pack('<iiii',len(v),len(indices),mi,int(cap)));f.write(v.tobytes());f.write(indices.tobytes())
    anchor={'soda':[0,.065,-.010],'shaker':[0,.127,-.008],'banana':[.048,.095,0]}[kind]
    (OUT/(kind+'.json')).write_text(json.dumps({'materials':materials,'anchor':anchor,'heldOffset':[0,0,0] if kind=='banana' else [-.0254,-.0381,0]},indent=2))
    records.append({'kind':kind,'parts':authored_names,'surfaces':[p[0] for p in parts],'triangles':sum(len(p[2])//3 for p in parts),'vertices':sum(len(p[1]) for p in parts)})
inputs={}
for name in ['field_item_models.gd','field_consumables.gd','banana_peel_086.gd']:
    p=SRC/'scripts'/name;inputs['scripts/'+name]=hashlib.sha256(p.read_bytes()).hexdigest()
(OUT/'provenance.json').write_text(json.dumps({'baseline':'actual R23 snapshot quest-r24/source','inputs':inputs,'models':records,'conversion':'CPU translation of authored source profiles, wrapped panels, aperture, cap parts, cylinder/torus recipes and rounded-box algorithm; no invented GLB or texture source. Source procedural materials retained. One Z reflection plus winding conversion.'},indent=2))
print(json.dumps(records,indent=2))
