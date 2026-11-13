from pathlib import Path
import struct
class Resource:
 def take(self,n):
  assert 0<=n<=len(self.b)-self.at
  v=self.b[self.at:self.at+n]; self.at+=n; return bytes(v)
 def num(self,t='I'): return struct.unpack('<'+t,self.take(struct.calcsize(t)))[0]
 def string(self):
  n=self.num(); assert n<16384
  b=self.take(n); assert not n or b[-1:]==b'\0'
  return b.rstrip(b'\0').decode()
 def key(self):
  n=self.num()
  return self.take(n&0x7fffffff).rstrip(b'\0').decode() if n>>31 else self.strings[n]
 def value(self,depth=0):
  assert depth<64; tag=self.num()
  if tag==1: return None
  if tag in (2,3,23,40,4,41): return self.num({2:'i',3:'i',23:'i',40:'q',4:'f',41:'d'}[tag])
  if tag in (5,44): return self.string()
  dims={10:2,11:4,12:3,13:4,14:4,15:6,16:9,17:12,18:6,20:4,45:2,46:4,47:3,50:4,51:4,52:16}
  if tag in dims: return [self.num('i' if tag in (45,46,47,51) else 'f' if tag==20 else self.real) for _ in range(dims[tag])]
  if tag==22:
   n=self.num()
   return {'nodepath':[self.key()for _ in range((n&65535)+((n>>16)&32767))]}
  if tag==24:
   kind=self.num(); assert kind in (0,2,3)
   return ('reference',kind,self.num()) if kind else None
  if tag in (26,30):
   n=self.num()&0x7fffffff; assert n<2000000
   if tag==30: return [self.value(depth+1) for _ in range(n)]
   d={}
   for _ in range(n):
    k=self.value(depth+1); assert k not in d; d[k]=self.value(depth+1)
   return d
  if tag==34: return [self.string() for _ in range(self.num())]
  sizes={31:1,32:4,33:4,35:3*struct.calcsize(self.real),36:16,37:2*struct.calcsize(self.real),48:8,49:8,53:4*struct.calcsize(self.real)}
  assert tag in sizes, (tag,self.at)
  n=self.num(); assert n<20000000
  b=self.take(n*sizes[tag])
  if tag==31: self.take((-n)%4)
  return b
 def __init__(self,b):
  self.provenance={}
  assert b[:4]==b'RSRC'; self.b=b; self.at=4
  assert self.num()==0; self.real64=self.num(); major=self.num(); minor=self.num(); version=self.num()
  assert major==4 and version==4
  self.kind=self.string(); self.num('Q'); flags=self.num(); self.num('Q'); self.real='d' if flags&4 else 'f'
  if flags&8: self.string()
  assert not any(self.num() for _ in range(11))
  self.strings=[self.string() for _ in range(self.num())]
  self.external=[]
  for _ in range(self.num()): self.external.append((self.string(),self.string(),self.num('Q') if flags&2 else None))
  table=[(self.string(),self.num('Q')) for _ in range(self.num())]; self.resources=[]
  assert all(self.at<=off<len(self.b) for _,off in table)
  for i,(name,off) in enumerate(table):
   self.at=off; typ=self.string(); props={}
   for _ in range(self.num()):
    k=self.key(); assert k not in props; props[k]=self.value()
   assert self.at<=(table[i+1][1] if i+1<len(table) else len(self.b))
   self.resources.append(dict(name=name,type=typ,props=props))
  self.header=dict(engine=[major,minor],format=version,real=self.real,resource_count=len(table))
 def nodes(self):
  b=next(r['props']['_bundled'] for r in self.resources if r['type']=='PackedScene')
  ints=list(struct.unpack('<'+'i'*(len(b['nodes'])//4),b['nodes'])); at=0; nodes=[]
  for i in range(b['node_count']):
   parent,owner,typ,name,instance,count=ints[at:at+6]; at+=6; props={}
   for _ in range(count):
    key,v=ints[at:at+2]; at+=2; props[b['names'][key]]=b['variants'][v]
   groups=ints[at]; at+=1+groups
   nodes.append(dict(index=i,parent=parent,name=b['names'][name&0x3fffffff],props=props))
  assert at==len(ints); return nodes
