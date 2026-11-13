shader_type spatial;
render_mode unshaded,cull_disabled,fog_disabled,depth_draw_never;
uniform vec3 eye_world;
varying vec3 world;
void vertex(){world=(MODEL_MATRIX*vec4(VERTEX,1.)).xyz;}
void fragment(){
 float axis=world.x+world.z;
 float a=abs(fract((axis+world.y)*.055-TIME*.035)-.5);
 float b=abs(fract((axis-world.y)*.055+TIME*.035)-.5);
 float footprint=max(fwidth(a),fwidth(b));
 float hatch=(1.-smoothstep(.035,.035+footprint*1.5,min(a,b)));
 vec3 red=mix(vec3(.40,.003,.015),vec3(1.,.025,.055),hatch);
 ALBEDO=red;EMISSION=red*(.55+hatch*.5);
 ALPHA=.11+hatch*.16;
}
