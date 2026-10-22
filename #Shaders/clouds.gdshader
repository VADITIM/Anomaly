shader_type canvas_item;
render_mode blend_mix, unshaded;

// Drifting cloud cover for the islands, in two passes from the same field:
//  - sea pass: a bank of clouds far below the islands, lit from above by the binary star with dark undersides,
//    stepped into a few flat tones so it reads as pixel art;
//  - shadow pass: the same clouds high above, cast as soft dark patches sweeping across the ground and actors.
// Pixel-snapped to the world grid like the atmosphere and void shaders.
// The field is kept low-frequency on purpose: its finest features span several art pixels, so the stepped
// tones and alpha bands come out as solid clumps instead of lone pixels.

uniform vec2 world_size = vec2(1920.0, 1408.0);
uniform bool shadow_pass = false;
uniform float pixel_size = 2.0;
uniform float scale = 0.008;
uniform vec2 wind = vec2(5.0, 1.6);
uniform float coverage = 0.68;
uniform vec4 highlight_color : source_color = vec4(0.8, 0.72, 0.9, 1.0);
uniform vec4 lit_color : source_color = vec4(0.62, 0.52, 0.78, 1.0);
uniform vec4 mid_color : source_color = vec4(0.34, 0.24, 0.5, 1.0);
uniform vec4 shade_color : source_color = vec4(0.12, 0.06, 0.22, 1.0);
uniform vec2 light_direction = vec2(-0.6, -0.8);
uniform float opacity = 0.8;
uniform vec4 shadow_color : source_color = vec4(0.04, 0.02, 0.1, 1.0);
uniform float shadow_coverage = 0.62;
uniform float shadow_strength = 0.24;

// Each octave is rotated so lattice lines never stack into straight edges.
const mat2 OCTAVE = mat2(vec2(1.62, 1.22), vec2(-1.22, 1.62));
// Width of the soft edge between clear sky and full cloud, in field units.
const float EDGE = 0.22;

// Sine-free hash: stable on every GPU, no banding at larger coordinates.
float hash(vec2 p) {
	vec3 p3 = fract(vec3(p.xyx) * 0.1031);
	p3 += dot(p3, p3.yzx + 33.33);
	return fract((p3.x + p3.y) * p3.z);
}

vec2 gradient(vec2 i) {
	float a = hash(i) * 6.2831853;
	return vec2(cos(a), sin(a));
}

// Gradient noise with quintic easing, roughly in [-0.7, 0.7]; free of the blocky value-noise grid.
float noise(vec2 p) {
	vec2 i = floor(p);
	vec2 f = fract(p);
	vec2 u = f * f * f * (f * (f * 6.0 - 15.0) + 10.0);
	float a = dot(gradient(i), f);
	float b = dot(gradient(i + vec2(1.0, 0.0)), f - vec2(1.0, 0.0));
	float c = dot(gradient(i + vec2(0.0, 1.0)), f - vec2(0.0, 1.0));
	float d = dot(gradient(i + vec2(1.0, 1.0)), f - vec2(1.0, 1.0));
	return mix(mix(a, b, u.x), mix(c, d, u.x), u.y);
}

float fbm(vec2 p, int octaves) {
	float value = 0.0;
	float amplitude = 0.5;
	for (int i = 0; i < octaves; i++) {
		value += amplitude * noise(p);
		p = OCTAVE * p + vec2(5.3, 1.9);
		amplitude *= 0.5;
	}
	return value;
}

// Summed absolute noise: rounded lumps pinched by creases, the puffy cauliflower edge of a cumulus.
float billow(vec2 p, int octaves) {
	float value = 0.0;
	float amplitude = 0.5;
	for (int i = 0; i < octaves; i++) {
		value += amplitude * abs(noise(p));
		p = OCTAVE * p + vec2(-3.7, 8.1);
		amplitude *= 0.5;
	}
	return value;
}

// Raw cloud field at a world position, centred around 0.5. Broad masses decide where clouds are and leave clear
// sky between them; billows round off their edges into puffs. A light warp keeps the masses from looking gridded
// without twisting them into random swirls. Shadows take fewer puffs so they read as soft blobs.
float field(vec2 world, float t, float puff_weight) {
	vec2 p = (world - wind * t) * scale;
	vec2 warp = vec2(fbm(p * 0.35 + vec2(t * 0.01, 0.0), 2), fbm(p * 0.35 + vec2(4.2, -t * 0.008), 2));
	vec2 q = p + warp * 0.8;
	float mass = fbm(q * 0.55, 3) + 0.5;
	float puffs = billow(q * 1.6 + vec2(t * 0.012, 0.0), 3) * 2.2;
	return mass + (puffs - 0.45) * puff_weight;
}

void fragment() {
	vec2 world = floor(UV * world_size / pixel_size) * pixel_size;
	float t = TIME;

	if (shadow_pass) {
		float cloud = smoothstep(1.0 - shadow_coverage, 1.0 - shadow_coverage + EDGE, field(world * 1.7 + vec2(310.0, -170.0), t * 1.6, 0.2));
		// Quantised to two steps so the shadow edge stays crisp at pixel scale.
		float shade = floor(cloud * 2.0 + 0.35) / 2.0;
		COLOR = vec4(shadow_color.rgb, shade * shadow_strength);
	} else {
		// Thin the bank out towards the rect's border so clouds shrink away there instead of being cut off.
		vec2 inside = min(UV, 1.0 - UV) * world_size;
		float fade = smoothstep(0.0, 260.0, min(inside.x, inside.y));
		float low = 1.0 - coverage;
		float f = field(world, t, 0.5) * fade;
		float cloud = smoothstep(low, low + EDGE, f);
		// Thin wisps fade in stepped bands rather than a smooth gradient.
		float alpha = floor(cloud * 3.0 + 0.4) / 3.0;
		if (alpha <= 0.0) {
			COLOR = vec4(0.0);
		} else {
			// Treat the field as height: lobes sloping up towards the star are lit, the far side of each puff and
			// the cloud's underside fall into shade, and thicker cores glow brighter than thin edges.
			vec2 towards = normalize(light_direction);
			float far = field(world + towards * 26.0, t, 0.5) * fade;
			float near = smoothstep(low, low + EDGE, field(world + towards * 7.0, t, 0.5) * fade);
			float form = (f - far) * 3.0;
			float rim = (cloud - near) * 0.9;
			float thickness = (f - low - EDGE) * 0.9;
			float light = 0.5 + form + rim + thickness;
			int band = int(clamp(floor(light * 4.0), 0.0, 3.0));
			vec3 color = band == 0 ? shade_color.rgb : band == 1 ? mid_color.rgb : band == 2 ? lit_color.rgb : highlight_color.rgb;
			COLOR = vec4(color, alpha * opacity);
		}
	}
}
