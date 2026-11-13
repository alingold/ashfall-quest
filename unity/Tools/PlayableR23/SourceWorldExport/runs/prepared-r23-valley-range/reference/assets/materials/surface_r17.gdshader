shader_type spatial;
render_mode cull_disabled;
uniform int style=0;
float hash(vec2 p){return fract(sin(dot(p,vec2(127.1,311.7)))*43758.5453);}
void fragment(){
 vec3 p=vec3(UV.x,UV.y,UV2.x);
 if(style==0){
  vec3 n=abs(normalize(cross(dFdx(p),dFdy(p))));vec2 q=vec2(n.z>n.x?p.x:p.z,p.y);
  float fade=1.-smoothstep(.012,.04,max(fwidth(q.x),fwidth(q.y)));
  float grain=.5+.5*sin(q.y*160.+sin(q.x*35.)*3.+sin(q.y*13.));
  float board=fract(q.x*8.);float seam=1.-smoothstep(.008,.024,min(board,1.-board));
  vec3 base=mix(vec3(.24,.12,.055),vec3(.42,.28,.14),clamp(COLOR.r,0.,1.));
  ALBEDO=base*(.88+.16*grain*fade)*(1.-.35*seam*fade);ROUGHNESS=.94;
 }else{
  vec2 q=vec2(p.x,p.z);float fade=1.-smoothstep(.003,.02,max(fwidth(q.x),fwidth(q.y)));
  float grain=hash(floor(q*512.));vec3 asphalt=vec3(.115,.123,.128)*(.93+.14*grain*fade);
  float bars=step(.45,fract(q.x*12.))*step(.07,q.x)*step(q.x,.93);
  float crosswalk=bars*max(step(.13,q.y)*step(q.y,.21),step(.79,q.y)*step(q.y,.87));
  bars=step(.45,fract(q.y*12.))*step(.07,q.y)*step(q.y,.93);
  crosswalk=max(crosswalk,bars*max(step(.13,q.x)*step(q.x,.21),step(.79,q.x)*step(q.x,.87)));
  ALBEDO=mix(asphalt,vec3(.68,.70,.67),crosswalk*.85);ROUGHNESS=.97;
 }
}
