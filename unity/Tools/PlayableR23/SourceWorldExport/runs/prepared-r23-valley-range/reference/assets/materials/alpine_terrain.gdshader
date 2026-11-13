shader_type spatial;
render_mode diffuse_burley, specular_disabled, cull_disabled;
uniform float snow_height = 55.0;
varying vec3 terrain_position;
varying vec3 terrain_normal;

void vertex() {
	terrain_position = VERTEX;
	terrain_normal = NORMAL;
}

void fragment() {
	// Meter-scale accumulation and striation; no subpixel glitter or normal noise.
	vec3 p = terrain_position;
	float drift = sin(p.x * 0.055 + sin(p.z * 0.037)) * 3.0 + sin(p.z * 0.071) * 2.0;
	float altitude = smoothstep(snow_height - 12.0, snow_height + 10.0, p.y + drift);
	float slope = smoothstep(0.52, 0.90, normalize(terrain_normal).y);
	float accumulation = altitude * mix(0.08, 0.90, slope);
	float strata = sin(p.y * 0.22 + sin(p.x * 0.035) * 1.5 + sin(p.z * 0.04));
	vec3 stone = vec3(0.15, 0.175, 0.19) * (0.94 + strata * 0.06);
	vec3 snow = vec3(0.44, 0.49, 0.53);
	ALBEDO = mix(stone, snow, accumulation);
	ROUGHNESS = 1.0;
	SPECULAR = 0.0;
}
