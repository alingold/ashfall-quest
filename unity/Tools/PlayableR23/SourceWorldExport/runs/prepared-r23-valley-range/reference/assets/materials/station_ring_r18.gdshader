shader_type spatial;
render_mode unshaded,cull_disabled,depth_draw_never;
varying float recharge;
varying float distance_to_eye;
void vertex(){
 recharge=INSTANCE_CUSTOM.r;
 mat4 billboard=mat4(INV_VIEW_MATRIX[0],INV_VIEW_MATRIX[1],INV_VIEW_MATRIX[2],MODEL_MATRIX[3]);
 MODELVIEW_MATRIX=VIEW_MATRIX*billboard;
 distance_to_eye=length(MODELVIEW_MATRIX[3].xyz);
}
void fragment(){
 vec2 p=UV*2.-1.;float radius=length(p);float aa=max(fwidth(radius),.002);
 float band=smoothstep(.76-aa,.76+aa,radius)*(1.-smoothstep(.84-aa,.84+aa,radius));
 if(band<.01)discard;
 float angle=fract(atan(p.x,-p.y)/6.2831853+1.);
 float available=1.-recharge;
 vec3 color=recharge<.001?vec3(.16,1.,.42):mix(vec3(.07,.13,.11),vec3(1.,.65,.08),step(angle,available));
 ALBEDO=color;EMISSION=color*.65;ALPHA=band*.92*(1.-smoothstep(55.,85.,distance_to_eye));
}
