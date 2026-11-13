shader_type spatial;
// R23: additive rim glow. No discard and no depth writes, so the overlay pass stays cheap on tiled mobile GPUs.
render_mode unshaded, shadows_disabled, blend_add, depth_draw_never, cull_back, fog_disabled;
uniform vec4 rarity_color:source_color;
void fragment(){
	float facing=abs(dot(normalize(NORMAL),normalize(VIEW)));
	float rim=1.0-smoothstep(.12,.34,facing);
	ALBEDO=rarity_color.rgb*rim*2.2;
}