shader_type spatial;
varying vec3 wall_position;
varying vec3 wall_normal;
void vertex(){wall_position=(MODEL_MATRIX*vec4(VERTEX,1.0)).xyz;wall_normal=MODEL_NORMAL_MATRIX*NORMAL;}
void fragment(){
 float horizontal=abs(wall_normal.x)>0.5?wall_position.z:wall_position.x;
 vec2 grid=vec2(horizontal*0.62,wall_position.y*0.64);
 vec2 cell=fract(grid);
 float pane=step(0.16,cell.x)*step(cell.x,0.84)*step(0.20,cell.y)*step(cell.y,0.80)*(1.0-step(0.5,abs(wall_normal.y)));
 float occupied=step(0.3,fract(sin(dot(floor(grid),vec2(12.9898,78.233)))*43758.5453));
 ALBEDO=mix(vec3(0.17,0.22,0.29),vec3(0.025,0.15,0.23),pane);
 EMISSION=vec3(0.015,0.11,0.17)*pane*occupied;
 METALLIC=0.6;ROUGHNESS=0.43;
}
