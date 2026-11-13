shader_type spatial;
render_mode unshaded, cull_disabled, depth_draw_never, fog_disabled;

// Original angular field art, informed by Al's 41.997–50.329 s film views.
// The full rectangular pane remains visible and matches the existing bullet
// test. The inset frame is decoration, not a new protection boundary.
uniform vec4 field_color : source_color = vec4(0.055, 0.94, 0.22, 1.0);

float segment_distance(vec2 point, vec2 start, vec2 end) {
    vec2 direction = end - start;
    float along = clamp(dot(point - start, direction) / dot(direction, direction), 0.0, 1.0);
    return length(point - start - direction * along);
}

void fragment() {
    vec2 p = UV * 2.0 - 1.0;
    // 0.97536/1.2192 = 0.8. Equal coordinate distances give equal line widths
    // across the original measured rectangle, without changing its mesh size.
    vec2 q = abs(p * vec2(0.8, 1.0));
    float frame = segment_distance(q, vec2(0.768, 0.0), vec2(0.768, 0.83));
    frame = min(frame, segment_distance(q, vec2(0.768, 0.83), vec2(0.672, 0.96)));
    frame = min(frame, segment_distance(q, vec2(0.672, 0.96), vec2(0.344, 0.96)));
    frame = min(frame, segment_distance(q, vec2(0.344, 0.96), vec2(0.240, 0.84)));
    frame = min(frame, segment_distance(q, vec2(0.240, 0.84), vec2(0.0, 0.84)));

    float core = 1.0 - smoothstep(0.004, 0.013, frame);
    float halo = (1.0 - smoothstep(0.014, 0.049, frame)) * 0.19;

    // A spare broken inner trace and short inset badge are original geometry
    // choices. The film does not establish their hidden lower/rear contour.
    float trace = segment_distance(q, vec2(0.701, 0.29), vec2(0.701, 0.69));
    trace = min(trace, segment_distance(q, vec2(0.701, 0.69), vec2(0.625, 0.79)));
    trace = min(trace, segment_distance(q, vec2(0.625, 0.79), vec2(0.386, 0.79)));
    float inner = (1.0 - smoothstep(0.001, 0.0035, trace)) * 0.32;
    float badge_half_width = mix(0.135, 0.188, smoothstep(0.719, 0.759, q.y));
    float badge = (1.0 - smoothstep(badge_half_width - 0.003, badge_half_width + 0.003, q.x))
        * smoothstep(0.715, 0.722, q.y) * (1.0 - smoothstep(0.756, 0.762, q.y));

    float rectangle_edge = min(1.0 - abs(p.x), 1.0 - abs(p.y));
    float boundary = (1.0 - smoothstep(0.001, 0.005, rectangle_edge)) * 0.09;
    // Keep a clear centre and a faint full-size rectangular boundary. Decorative
    // angular corners never create an invisible rectangular collision overhang.
    float detail = max(core * 0.79 + halo, max(inner, badge * 0.43));
    ALBEDO = field_color.rgb;
    EMISSION = field_color.rgb * core * 0.35;
    ALPHA = clamp(0.09 + max(detail, boundary), 0.0, 0.93);
}
