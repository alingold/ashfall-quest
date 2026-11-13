shader_type spatial;
render_mode unshaded, blend_mix, depth_draw_never, depth_test_disabled, cull_back, fog_disabled;

// Original grayscale ghost rendering. No colored light, emissive tint or shadow.
uniform vec4 source_tint : source_color = vec4(0.65, 0.65, 0.65, 1.0);
uniform sampler2D source_albedo : source_color, filter_linear_mipmap, repeat_enable;
uniform bool has_albedo = false;
uniform bool vertex_albedo = false;
uniform vec3 source_uv_scale = vec3(1.0);
uniform vec3 source_uv_offset = vec3(0.0);
uniform float ghost_opacity : hint_range(0.0, 1.0) = 0.42;

void fragment() {
	vec4 sample_color = source_tint;
	if (has_albedo) sample_color *= texture(source_albedo, UV * source_uv_scale.xy + source_uv_offset.xy);
	if (vertex_albedo) sample_color *= COLOR;
	float luminance = dot(sample_color.rgb, vec3(0.2126, 0.7152, 0.0722));
	float facing = abs(dot(normalize(NORMAL), normalize(VIEW)));
	float rim = pow(1.0 - facing, 2.0);
	ALBEDO = vec3(clamp(0.17 + luminance * 0.69 + rim * 0.14, 0.0, 1.0));
	ALPHA = ghost_opacity * sample_color.a;
}
