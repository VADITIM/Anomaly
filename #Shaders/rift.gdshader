shader_type canvas_item;
render_mode blend_mix, unshaded;

// A Fracture drawn as a pixel sprite: a jagged vertical tear whose inside is a window onto the Void, with the same
// swirl, veins and twinkling stars as the bottomless void tiles. Its centre line comes from the C# edge points so the
// sparks it sheds leave from the tear itself. Pixel-snapped to the world grid; the rect is placed by Fracture.cs.

uniform vec2 rect_size = vec2(44.0, 60.0);
uniform float pixel_size = 1.0;
uniform float tear_length = 52.0;
uniform float margin = 4.0;
uniform float center_knots[9];
uniform float width_knots[9];
uniform float seed = 0.0;
uniform bool sealed = false;
uniform float max_width = 8.0;
uniform vec4 deep_color : source_color = vec4(0.012, 0.0, 0.035, 1.0);
uniform vec4 mid_color : source_color = vec4(0.2, 0.04, 0.3, 1.0);
uniform vec4 glow_color : source_color = vec4(0.85, 0.22, 0.72, 1.0);
uniform vec4 star_color : source_color = vec4(0.75, 0.9, 1.0, 1.0);
uniform vec4 rim_color : source_color = vec4(0.95, 0.3, 0.85, 1.0);
uniform vec4 rim_dark_color : source_color = vec4(0.45, 0.08, 0.5, 1.0);
uniform vec4 core_color : source_color = vec4(0.75, 0.95, 1.0, 1.0);
uniform vec4 sealed_color : source_color = vec4(0.35, 0.3, 0.45, 1.0);
uniform vec4 chain_color : source_color = vec4(0.5, 0.42, 0.6, 1.0);

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
		p = p * 2.03 + vec2(1.7, 9.2);
		amplitude *= 0.5;
	}
	return value;
}

// Straight segments between the knots, so the tear zigzags like a crack instead of curving.
float center_at(float v) {
	float k = clamp(v, 0.0, 1.0) * 8.0;
	int i = min(int(floor(k)), 7);
	return mix(center_knots[i], center_knots[i + 1], k - float(i));
}

float width_at(float v) {
	float k = clamp(v, 0.0, 1.0) * 8.0;
	int i = min(int(floor(k)), 7);
	return mix(width_knots[i], width_knots[i + 1], k - float(i));
}

// The inside of the tear: the void tile's look, sampled small and drifting so it reads as depth behind the wound.
vec3 void_inside(vec2 p, float t, float calm) {
	vec2 q = p + vec2(seed * 37.0, seed * 11.0);
	float swirl = fbm(q * 0.05 + vec2(t * 0.15, -t * 0.22) * calm);
	float veins = fbm(q * 0.11 + vec2(-t * 0.2, t * 0.3) * calm + swirl * 2.0);
	vec3 color = mix(deep_color.rgb, mid_color.rgb * 1.3, step(0.45, swirl) * 0.6 + step(0.6, swirl) * 0.4);
	float vein = step(0.58, veins) * (1.0 - step(0.68, veins));
	color += glow_color.rgb * vein * 0.5 * calm;

	vec2 star_cell = floor(q + vec2(0.0, -t * 4.0 * calm));
	float star = step(0.975, hash(star_cell));
	float twinkle = step(0.5, sin(t * (2.0 + hash(star_cell + 3.1) * 4.0) + hash(star_cell) * 30.0));
	color += star_color.rgb * star * twinkle * 0.9;
	return color;
}

void fragment() {
	vec2 cell = floor(UV * rect_size / pixel_size) * pixel_size + pixel_size * 0.5;
	// Origin at the bottom centre of the rect, y up, so the knots match Fracture's edge points.
	vec2 p = vec2(cell.x - rect_size.x * 0.5, rect_size.y - cell.y);
	float t = TIME;
	float v = (p.y - margin) / tear_length;
	float along = clamp(v, 0.0, 1.0);

	float center = center_at(v);
	// The tear breathes in whole-pixel steps.
	float beat = sin(t * 3.0 + seed);
	float pulse = sealed ? 0.0 : step(0.0, beat) + step(0.8, beat);
	float taper = pow(sin(along * 3.14159), 0.85);
	float half_width = sealed ? taper * 2.5 : taper * (max_width * width_at(v) + pulse);
	float off = abs(p.x - center);
	float d = off - half_width;
	bool in_span = v > -0.02 && v < 1.02;

	vec4 rim = sealed ? sealed_color : rim_color;
	vec4 outer = sealed ? vec4(sealed_color.rgb * 0.45, 1.0) : rim_dark_color;
	vec4 color = vec4(0.0);

	if (in_span && d < -2.0) {
		color = vec4(void_inside(p, t, sealed ? 0.15 : 1.0), 1.0);
		if (sealed)
			color.rgb = mix(color.rgb, vec3(0.05, 0.03, 0.08), 0.6);
		// The bright seam down the middle: one unbroken zigzag whose brightness steps with the beat.
		if (!sealed && off < 0.9)
			color = mix(color, core_color, 0.55 + 0.2 * pulse);
	} else if (in_span && d < 0.0) {
		color = rim;
		// Sparkling flecks along the lip, stepped so they pop instead of fading.
		if (!sealed && step(0.93, hash(vec2(floor(p.y), floor(t * 6.0) + seed))) > 0.5)
			color = core_color;
	} else if (in_span && d < 1.0) {
		color = outer;
	} else if (!sealed && in_span && d < 4.0) {
		// A dithered halo of void-light leaking out around the lip.
		float checker = mod(floor(p.x) + floor(p.y), 2.0);
		float band = step(d, 2.5) + checker * step(d, 4.0) * 0.5;
		color = vec4(rim_color.rgb, 0.22 * min(band, 1.0) * (0.75 + 0.25 * sin(t * 3.0)));
	}

	// The tips close to single bright points.
	if (!sealed && in_span && (v < 0.03 || v > 0.97) && off < 1.0)
		color = rim;

	if (sealed) {
		// Chains of hardened Corruption across the tear, one at every second knot.
		for (int i = 2; i < 8; i += 2) {
			float ky = margin + float(i) / 8.0 * tear_length;
			float kx = center_knots[i];
			float dx = p.x - kx;
			if (abs(dx) < 9.0 && abs((p.y - ky) - dx * 0.22) < 1.0) {
				float link = mod(floor((dx + 9.0) / 3.0), 2.0);
				color = vec4(chain_color.rgb * (0.75 + 0.35 * link), 1.0);
			}
		}
	}

	COLOR = color;
}
