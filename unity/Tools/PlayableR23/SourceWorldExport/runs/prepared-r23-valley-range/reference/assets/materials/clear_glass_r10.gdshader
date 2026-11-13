shader_type spatial;
render_mode blend_mix, depth_prepass_alpha, cull_disabled;
void fragment(){
 float edge=pow(1.0-abs(dot(normalize(NORMAL),normalize(VIEW))),3.0);
 ALBEDO=vec3(0.28,0.48,0.56);
 ALPHA=0.07+edge*0.14;
 ROUGHNESS=0.16;
 SPECULAR=0.55;
}
