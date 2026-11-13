shader_type spatial;
render_mode unshaded, cull_disabled, shadows_disabled, depth_draw_opaque, world_vertex_coords;
uniform vec2 zone_origin;
uniform vec4 current_square;
uniform vec4 next_square;
varying vec3 world_position;
// Two millimetres above the same surface avoids depth ties with the weathered material.
void vertex(){world_position=VERTEX;VERTEX+=normalize(NORMAL)*.002;}
float edge_distance(vec2 p,vec4 r){vec2 q=abs(p-(r.xy+r.zw)*.5)-(r.zw-r.xy)*.5;return abs(length(max(q,vec2(0.)))+min(max(q.x,q.y),0.));}
void fragment(){
	vec2 p=world_position.xz-zone_origin;
	float red=edge_distance(p,current_square);
	float white=edge_distance(p,next_square);
	if(red>.60 && white>.1524){discard;}
	vec3 color=red<=.60?vec3(1.,.012,.055):vec3(.92,.98,1.);
	ALBEDO=color;EMISSION=color*(red<=.60?2.8:2.0);
}
