shader_type spatial;
render_mode unshaded, cull_disabled, depth_draw_never;
uniform sampler2D pattern : source_color, filter_linear_mipmap_anisotropic, repeat_enable;
void fragment(){
 vec4 ink=texture(pattern,UV*2.0);
 float line=max(ink.r,max(ink.g,ink.b))*ink.a;
 ALBEDO=mix(vec3(0.01,0.12,0.17),ink.rgb,0.65);
 EMISSION=ink.rgb*0.32;
 ALPHA=clamp(line*0.38,0.025,0.38);
 ROUGHNESS=0.85;
}
