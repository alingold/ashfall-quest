shader_type spatial;
render_mode cull_disabled;
uniform int style=0;
void fragment(){
 vec3 p=vec3(UV.x,UV.y,UV2.x);vec3 n=normalize(cross(dFdx(p),dFdy(p)));
 float side=step(.65,abs(n.z)),ends=step(.65,abs(n.x));
 if(style==2){
  float tread=.5+.5*sin((p.x+p.z)*90.);float fade=1.-smoothstep(.01,.04,max(fwidth(p.x),fwidth(p.z)));
  ALBEDO=vec3(.035,.041,.045)*(.86+.14*tread*fade);ROUGHNESS=.96;
 }else{
  vec3 paint=style==1?vec3(.21,.32,.39):vec3(.30,.43,.49);
  float window=side*step(.53,p.y)*step(p.y,.89)*max(step(.7,p.x),step(p.x,.29));
  window=max(window,ends*step(.56,p.y)*step(p.y,.90)*step(.12,p.z)*step(p.z,.88));
  float band=side*step(.34,p.y)*step(p.y,.38);
  float seam=side*(1.-smoothstep(.002,.006,min(abs(p.x-.62),abs(p.y-.42))));
  vec3 color=paint*(.88+.12*p.y);color=mix(color,vec3(.08,.12,.15),seam*.8);
  color=mix(color,vec3(.69,.72,.67),band*.85);color=mix(color,vec3(.018,.055,.075),window);
  float bumper=ends*step(p.y,.21);color=mix(color,vec3(.08,.09,.1),bumper);
  float lamp=ends*step(.25,p.y)*step(p.y,.40)*step(.3,abs(p.z-.5));
  color=mix(color,mix(vec3(.57,.018,.012),vec3(.92,.83,.56),step(.5,p.x)),lamp);
  ALBEDO=color;METALLIC=.30;ROUGHNESS=mix(.65,.18,window);
 }
}
