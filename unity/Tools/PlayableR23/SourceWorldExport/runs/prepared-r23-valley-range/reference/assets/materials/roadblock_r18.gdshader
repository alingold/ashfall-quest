shader_type spatial;
render_mode cull_disabled;
// Original restrained concrete detail on exact matched source roadblock geometry.
// Source vertex colors remain the paint/base color; no traffic symbols are invented.
varying vec3 source_position;
void vertex(){source_position=VERTEX;}
float aggregate(vec3 p){return fract(sin(dot(p,vec3(127.1,311.7,74.7)))*43758.5453);}
void fragment(){
 float footprint=max(length(dFdx(source_position)),length(dFdy(source_position)));
 float fade=1.-smoothstep(.012,.045,footprint);
 float grain=aggregate(floor(source_position*150.));
 ALBEDO=COLOR.rgb*(1.+(grain-.5)*.16*fade);
 ROUGHNESS=.96;
}
