shader_type spatial;
render_mode unshaded, cull_disabled, depth_test_disabled, depth_draw_never, fog_disabled;
uniform sampler2D optical_view : source_color, filter_linear;
uniform sampler2D optical_right : source_color, filter_linear;
uniform vec3 view_right=vec3(1.0,0.0,0.0);
uniform vec3 view_up=vec3(0.0,1.0,0.0);
uniform vec3 view_forward=vec3(0.0,0.0,-1.0);
uniform float angular_scale=0.33333333;
uniform int eye_override=-1;
uniform vec2 aim_left=vec2(0.0);
uniform vec2 aim_right=vec2(0.0);
void vertex(){ POSITION=vec4(VERTEX.xy*0.5,1.0,1.0); }
void fragment(){
 // Inverse projection is the current eye's asymmetric XR projection. No finite
 // lens plane, guessed HMD FOV, screen-size percentage or shared-eye disparity.
 vec4 projected=INV_PROJECTION_MATRIX*vec4(SCREEN_UV*2.0-1.0,1.0,1.0);
 // Godot multiview folds view_offset.inverse() into each eye projection.
 // Unprojection is a point in combined-camera space, not an eye-relative ray.
 vec3 ray=normalize((INV_VIEW_MATRIX*vec4(normalize(projected.xyz/projected.w-EYE_OFFSET),0.0)).xyz);
 float denominator=max(0.0001,dot(ray,view_forward));
 vec2 angular=vec2(dot(ray,view_right),dot(ray,view_up))/denominator;
 vec2 p=angular/angular_scale;
 float r=length(p);
 int eye=eye_override<0?VIEW_INDEX:eye_override;
 vec2 uv=vec2(p.x,-p.y)*0.5+0.5;
 vec3 colour=eye==1?texture(optical_right,uv).rgb:texture(optical_view,uv).rgb;
 float edge=1.0-smoothstep(0.96,1.0,r);
 vec2 aim=p-(eye==1?aim_right:aim_left);
 float cross=max(step(abs(aim.y),0.004)*step(abs(aim.x),0.90),step(abs(aim.x),0.004)*step(abs(aim.y),0.90));
 ALBEDO=r>=1.0?vec3(0.0):mix(colour*edge,vec3(0.002),cross);
 // Composite after opaque geometry and other scene transparency. With no
 // depth writes an opaque-pass quad can be overwritten by later world draws.
 ALPHA=1.0;
}