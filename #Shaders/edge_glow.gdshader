shader_type canvas_item;
render_mode blend_add, unshaded;

// Faint screen-edge glow for full meters: Corruption lights the top edge red, a ready heal lights the top-left
// corner green. Where both are lit the two colours blend along the edge instead of stacking. Meant to be barely there.

uniform float rage : hint_range(0.0, 1.0) = 0.0;
uniform float heal : hint_range(0.0, 1.0) = 0.0;
uniform float aspect = 1.7778;
uniform vec4 rage_color : source_color = vec4(1.0, 0.12, 0.18, 1.0);
uniform vec4 heal_color : source_color = vec4(0.35, 1.0, 0.5, 1.0);
uniform float intensity = 0.16;

float hash(vec2 p) {
	return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453);
}

float noise(vec2 p) {
	vec2 i = floor(p);
	vec2 f = fract(p);
	f = f * f * (3.0 - 2.0 * f);
	return mix(mix(hash(i), hash(i + vec2(1.0, 0.0)), f.x), mix(hash(i + vec2(0.0, 1.0)), hash(i + vec2(1.0, 1.0)), f.x), f.y);
}

// Glow hanging from the top edge whose lower boundary rolls like liquid lapping over a rim, with thin runs
// that slide down and fade: the overflow.
float overflow(vec2 uv, float depth, float seed) {
	float x = uv.x * aspect;
	float t = TIME;
	float lip = depth * (0.75 + 0.5 * noise(vec2(x * 5.0 + t * 0.35, seed)) + 0.25 * noise(vec2(x * 13.0 - t * 0.6, seed + 4.0)));
	float body = exp(-uv.y / max(lip, 0.001));
	float lane = noise(vec2(x * 22.0, seed + 9.0));
	float run = smoothstep(0.72, 0.95, lane) * smoothstep(0.35, 0.0, abs(fract(x * 22.0) - 0.5));
	float travel = fract(t * 0.12 + hash(vec2(floor(x * 22.0), seed)) * 7.0);
	float head = travel * depth * 4.0;
	float drip = run * smoothstep(head + depth * 0.4, head, uv.y) * smoothstep(0.0, head, uv.y) * (1.0 - travel);
	return body + drip * 0.45;
}

void fragment() {
	vec2 uv = UV;
	float breath = 0.85 + 0.15 * sin(TIME * 1.3);

	float red = rage * overflow(uv, 0.045, 1.0) * breath;

	vec2 corner = vec2(uv.x * aspect, uv.y);
	float wobble = 0.85 + 0.3 * noise(vec2(atan(corner.y, corner.x) * 4.0, TIME * 0.4));
	float green = heal * exp(-length(corner) / (0.24 * wobble));
	// The corner bleeds a little way along the top edge so it meets the red band rather than sitting beside it.
	green = max(green, heal * overflow(uv, 0.035, 5.0) * exp(-uv.x * aspect / 0.45));
	green *= 0.9 + 0.1 * sin(TIME * 1.1 + 1.7);

	float total = red + green;
	vec3 color = (rage_color.rgb * red + heal_color.rgb * green) / max(total, 0.0001);
	float strength = clamp(max(red, green) + min(red, green) * 0.35, 0.0, 1.0) * intensity;
	COLOR = vec4(color * strength, 1.0);
}
