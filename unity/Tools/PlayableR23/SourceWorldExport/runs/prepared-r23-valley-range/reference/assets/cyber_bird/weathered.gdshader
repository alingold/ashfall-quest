shader_type spatial;
render_mode specular_schlick_ggx;
uniform vec4 base_color : source_color = vec4(0.1,0.15,0.2,1.0);
uniform float metal = 0.3;
uniform float rough = 0.6;
uniform vec4 bounce_color : source_color = vec4(0.0);
uniform float floor_finish = 0.0;
varying vec3 world_vertex;
void vertex() { world_vertex = (MODEL_MATRIX * vec4(VERTEX,1.0)).xyz; }
void fragment() {
    // Analytic seams and rain wear, no bitmap textures, alpha or screen copies.
    vec3 p = world_vertex;
    float grit = sin(p.x*3.0+p.z*0.7)*sin(p.z*4.0+p.y*0.2)*0.5+0.5;
    float seams = step(0.975,fract(p.y*0.25)) * 0.10;
    float streak = sin(p.x*19.0+p.z*17.0)*sin(p.y*0.2)*0.028;
    ALBEDO = base_color.rgb * (0.90+grit*0.16-seams+streak);
    if (floor_finish > 0.5) {
        vec2 tile = abs(fract(p.xz*0.4)-0.5);
        float grout = smoothstep(0.47,0.49,max(tile.x,tile.y));
        ALBEDO *= 1.0-grout*0.3;
    }
    METALLIC = metal;
    ROUGHNESS = clamp(rough+grit*0.09,0.12,1.0);
    EMISSION = bounce_color.rgb;
}
