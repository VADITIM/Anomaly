shader_type canvas_item;

uniform vec2 world_size = vec2(1920.0, 1408.0);
uniform vec4 deep_color : source_color = vec4(0.012, 0.0, 0.035, 1.0);
uniform vec4 mid_color : source_color = vec4(0.2, 0.04, 0.3, 1.0);
uniform vec4 glow_color : source_color = vec4(0.85, 0.22, 0.72, 1.0);
uniform vec4 star_color : source_color = vec4(0.75, 0.9, 1.0, 1.0);

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
	for (int i = 0; i < 5; i++) {
		value += amplitude * noise(p);
		p = p * 2.03 + vec2(1.7, 9.2);
		amplitude *= 0.5;
	}
	return value;
}

void fragment() {
	vec2 world = floor(UV * world_size / 2.0) * 2.0;
	float t = TIME;
	float swirl = fbm(world * 0.006 + vec2(t * 0.018, -t * 0.012));
	float veins = fbm(world * 0.014 + vec2(-t * 0.03, t * 0.02) + swirl * 2.0);
	vec3 color = mix(deep_color.rgb, mid_color.rgb, smoothstep(0.28, 0.78, swirl));
	float vein = smoothstep(0.62, 0.74, veins) * (1.0 - smoothstep(0.74, 0.9, veins));
	color += glow_color.rgb * vein * 0.55;
	color += glow_color.rgb * pow(smoothstep(0.55, 0.95, swirl * veins * 1.6), 3.0) * 0.4;

	// A slow far layer under everything: dim, drifting the other way, so the abyss reads as deep rather than flat.
	vec2 far = world * 0.5 + vec2(-t * 1.5, t * 0.6);
	vec2 far_cell = floor(far / 3.0);
	float far_star = step(0.996, hash(far_cell + 11.0));
	color += star_color.rgb * far_star * 0.25;
	float breath = 0.5 + 0.5 * sin(t * 0.25);
	color += mid_color.rgb * smoothstep(0.55, 0.85, fbm(world * 0.0025 - vec2(t * 0.006, 0.0))) * 0.35 * breath;

	vec2 star_cell = floor(world / 3.0);
	float star = step(0.993, hash(star_cell));
	float twinkle = 0.5 + 0.5 * sin(t * (1.5 + hash(star_cell + 3.1) * 3.0) + hash(star_cell) * 30.0);
	color += star_color.rgb * star * twinkle * 0.8;

	vec2 drift_cell = floor((world + vec2(0.0, t * 9.0)) / 5.0);
	float mote = step(0.998, hash(drift_cell + 7.0));
	color += glow_color.rgb * mote * 0.6;

	COLOR = vec4(color, 1.0);
}
