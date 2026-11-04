// Fullscreen impact: outline / invert / blackout+outline+diagonal scratches.
// Assign to a fullscreen ColorRect via ShaderMaterial.

shader_type canvas_item;

uniform sampler2D screen_texture : hint_screen_texture, filter_linear;

// 0 = outline, 1 = invert, 2 = blackout + outline + diagonal white lines.
uniform int style : hint_range(0, 2) = 1;

uniform float threshold : hint_range(0.0, 1.0) = 0.15;
uniform float line_strength : hint_range(0.5, 4.0) = 2.0;
// Rerolled each impact so scratch positions change.
uniform float scratch_seed = 0.0;

float hash(float n) {
	return fract(sin(n) * 43758.5453123);
}

void fragment() {
	vec3 c = texture(screen_texture, SCREEN_UV).rgb;
	vec2 px = 1.0 / vec2(textureSize(screen_texture, 0));
	vec3 l = texture(screen_texture, SCREEN_UV + vec2(-px.x, 0.0)).rgb;
	vec3 r = texture(screen_texture, SCREEN_UV + vec2(px.x, 0.0)).rgb;
	vec3 u = texture(screen_texture, SCREEN_UV + vec2(0.0, -px.y)).rgb;
	vec3 d = texture(screen_texture, SCREEN_UV + vec2(0.0, px.y)).rgb;

	float edge = length(c - l) + length(c - r) + length(c - u) + length(c - d);
	edge = smoothstep(threshold, threshold + 0.05, edge * line_strength);
	vec3 outline_color = vec3(edge);
	vec3 invert_color = 1.0 - c;

	// A few random diagonal slash marks on pure black.
	float scratches = 0.0;
	for (int i = 0; i < 5; i++) {
		float fi = float(i);
		float h0 = hash(scratch_seed + fi * 13.1);
		float h1 = hash(scratch_seed + fi * 31.7 + 4.2);
		float h2 = hash(scratch_seed + fi * 7.9 + 11.0);
		float offset = h0 * 1.6 - 0.3;
		float skew = 0.75 + h1 * 0.7;
		float thickness = 0.003 + h2 * 0.01;
		float band = abs(SCREEN_UV.x * skew + SCREEN_UV.y - offset);
		scratches = max(scratches, 1.0 - smoothstep(0.0, thickness, band));
	}
	// Blackout = white outlines + diagonal scratches on black.
	vec3 blackout_color = vec3(max(edge, scratches));

	float s = float(style);
	vec3 color = mix(outline_color, invert_color, clamp(s, 0.0, 1.0));
	color = mix(color, blackout_color, clamp(s - 1.0, 0.0, 1.0));
	COLOR = vec4(color, 1.0);
}
