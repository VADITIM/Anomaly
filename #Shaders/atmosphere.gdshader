shader_type canvas_item;
render_mode blend_mix, unshaded;

// World-space haze over the islands: low drifting fog banks plus faint diagonal light shafts from the binary star.
// Pixel-snapped to the 1px world grid so it reads as part of the pixel art rather than a filter on top.

uniform vec2 world_size = vec2(1920.0, 1408.0);
uniform vec4 fog_color : source_color = vec4(0.34, 0.32, 0.52, 1.0);
uniform vec4 shaft_color : source_color = vec4(1.0, 0.92, 0.78, 1.0);
uniform float fog_density = 0.24;
uniform float shaft_strength = 0.085;
uniform vec2 shaft_direction = vec2(1.0, 0.42);

float hash(vec2 p) {
	return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453);
}

float noise(vec2 p) {
	vec2 i = floor(p);
	vec2 f = fract(p);
	f = f * f * (3.0 - 2.0 * f);
	return mix(mix(hash(i), hash(i + vec2(1.0, 0.0)), f.x), mix(hash(i + vec2(0.0, 1.0)), hash(i + vec2(1.0, 1.0)), f.x), f.y);
}

float fbm(vec2 p) {
	float value = 0.0;
	float amplitude = 0.5;
	for (int i = 0; i < 4; i++) {
		value += amplitude * noise(p);
		p = p * 2.07 + vec2(3.1, 7.7);
		amplitude *= 0.5;
	}
	return value;
}

void fragment() {
	vec2 world = floor(UV * world_size);
	float t = TIME;

	float banks = fbm(world * 0.0045 + vec2(t * 0.011, t * 0.004));
	float wisps = fbm(world * 0.012 + vec2(-t * 0.02, t * 0.008) + banks * 1.5);
	float fog = smoothstep(0.42, 0.9, banks * 0.75 + wisps * 0.45);

	vec2 axis = normalize(shaft_direction);
	float along = dot(world, axis);
	float across = dot(world, vec2(-axis.y, axis.x));
	float wobble = fbm(vec2(along * 0.006, t * 0.04)) * 5.0;
	float bands = pow(0.5 + 0.5 * sin(along * 0.03 + wobble), 8.0);
	float reach = smoothstep(0.25, 0.75, fbm(vec2(across * 0.0025, along * 0.001 + t * 0.015)));
	float shafts = bands * reach * (0.75 + 0.25 * sin(t * 0.35 + along * 0.004));

	float fog_alpha = fog * fog_density;
	float shaft_alpha = shafts * shaft_strength;
	float alpha = fog_alpha + shaft_alpha;
	vec3 color = mix(fog_color.rgb, shaft_color.rgb, shaft_alpha / max(alpha, 0.0001));
	COLOR = vec4(color, clamp(alpha, 0.0, 0.5));
}
