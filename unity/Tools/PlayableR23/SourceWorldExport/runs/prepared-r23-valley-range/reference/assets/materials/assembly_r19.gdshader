shader_type spatial;
render_mode cull_disabled;
uniform int style=0;
uniform bool two_sided_art=false;
uniform sampler2D art:source_color,filter_linear_mipmap_anisotropic,repeat_disable;
void fragment(){
 vec3 p=vec3(UV.x,1.-UV.y,UV2.x);
 float fade=1.-smoothstep(.006,.03,max(fwidth(p.x),fwidth(p.z)));
 if(style==0){ALBEDO=texture(art,two_sided_art?vec2(FRONT_FACING?UV.x:1.-UV.x,UV.y):vec2(1.-UV.x,UV.y)).rgb;ROUGHNESS=.83;}
 else if(style==1){
  // Wheel profile axis x is the axle; circular sidewall lives in yz.
  float r=length((p.yz-vec2(.5))*2.);
  float hub=1.-smoothstep(.43,.48,r);
  float tread=.5+.5*sin(atan(p.y-.5,p.z-.5)*32.);
  ALBEDO=mix(vec3(.025,.031,.035)*(1.+.12*tread*fade),vec3(.34,.39,.42),hub);
  float bore=1.-smoothstep(.12,.18,r);ALBEDO=mix(ALBEDO,vec3(.07,.09,.10),bore);
  METALLIC=hub*.75;ROUGHNESS=mix(.93,.42,hub);
 }else{
  float seam=1.-smoothstep(.025,.06,abs(fract(p.x*12.)-.5));
  vec3 base=style==2?vec3(.24,.27,.28):vec3(.12,.16,.18);
  ALBEDO=base*(1.-.22*seam*fade);
  float edge=step(.94,p.x)+step(p.x,.06);
  float reflector=step(.8,p.z)*step(p.z,.94)*edge;
  ALBEDO=mix(ALBEDO,vec3(.86,.42,.055),clamp(reflector,0.,1.));METALLIC=.65;ROUGHNESS=.64;
 }
}
