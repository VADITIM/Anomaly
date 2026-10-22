shader_type canvas_item;

// Walkable Void ground: the abyss of void.gdshader set solid, with torn tiles of every land breaking through it.
// VoidPainter encodes each tile in its modulate: r = style / 8, g = a per-tile seed, a = how much of the tile the Void
// has taken (0 keeps the torn land's tile, 1 is solid Void).
uniform vec4 floor_color : source_color = vec4(0.1, 0.04, 0.17, 1.0);
uniform vec4 floor_light : source_color = vec4(0.26, 0.12, 0.38, 1.0);
uniform vec4 glow_color : source_color = vec4(0.92, 0.26, 0.8, 1.0);
uniform vec4 seam_color : source_color = vec4(0.42, 0.82, 1.0, 1.0);
uniform vec4 star_color : source_color = vec4(0.8, 0.92, 1.0, 1.0);

varying vec2 world;

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

void vertex() {
	world = (MODEL_MATRIX * vec4(VERTEX, 0.0, 1.0)).xy;
}

void fragment() {
	float taken = COLOR.a;
	int style = int(round(COLOR.r * 8.0));
	float seed = COLOR.g;
	vec2 p = floor(world);
	vec2 local = mod(p, 32.0);
	float t = TIME;

	// Glitch: thin bands slip sideways for a moment, more often the more the Void holds the tile.
	float band = floor(p.y / 2.0);
	float tick = floor(t * 7.0 + seed * 5.0);
	float slip = step(1.0 - 0.03 * (0.3 + taken), hash(vec2(band, tick)));
	vec2 uv = UV + vec2(round((hash(vec2(tick, band)) - 0.5) * 10.0) * slip * TEXTURE_PIXEL_SIZE.x, 0.0);
	vec3 texel = texture(TEXTURE, uv).rgb;
	float grain = dot(texel, vec3(0.299, 0.587, 0.114));

	// The torn land keeps its own colours, bruised toward violet. Bright land (snow) is pressed down hardest, or it
	// would glare against the dark Void around it.
	vec3 torn = mix(texel, vec3(grain) * vec3(0.82, 0.64, 1.05), 0.35 + 0.3 * taken);
	torn *= 0.85 - 0.35 * smoothstep(0.5, 0.95, grain);

	// Void ground: dark glass with the abyss turning under it, grained by the source tile so it reads as a surface,
	// not as the pit. Each style is its own tile, so neighbouring patches of Void stay distinct.
	float swirl = fbm(p * 0.02 + vec2(t * 0.02, -t * 0.015));
	vec3 ground = mix(floor_color.rgb, floor_light.rgb, smoothstep(0.3, 0.75, swirl));
	ground *= 0.7 + grain * 0.7;
	float edge = min(min(local.x, local.y), min(31.0 - local.x, 31.0 - local.y));
	if (style == 0) {
		// Plates: every tile a slab with lit seams and a dark bevel inside them.
		float pulse = 0.6 + 0.4 * sin(t * 1.5 + seed * 6.28);
		ground += seam_color.rgb * (1.0 - step(1.0, edge)) * 0.4 * pulse;
		ground *= 1.0 - (step(1.0, edge) - step(2.0, edge)) * 0.35;
	} else if (style == 1) {
		// Starglass: the abyss's stars under a sheen that sweeps across.
		vec2 cell = floor(p / 3.0);
		float twinkle = 0.5 + 0.5 * sin(t * (1.5 + hash(cell + 3.1) * 3.0) + hash(cell) * 30.0);
		ground += star_color.rgb * step(0.985, hash(cell + seed)) * twinkle * 0.7;
		float sheen = fract((p.x + p.y) * 0.008 - t * 0.07);
		ground += floor_light.rgb * smoothstep(0.0, 0.015, sheen) * (1.0 - smoothstep(0.015, 0.04, sheen)) * 0.6;
	} else if (style == 2) {
		// Veined: corrupted veins pulsing under the glass.
		float v = fbm(p * 0.05 + seed * 10.0 + vec2(t * 0.03, 0.0));
		float vein = smoothstep(0.58, 0.63, v) * (1.0 - smoothstep(0.63, 0.7, v));
		ground += glow_color.rgb * vein * (0.5 + 0.25 * sin(t * 2.0 + seed * 9.0));
	} else if (style == 3) {
		// Facets: the floor has set like crystal, each facet catching the light differently.
		vec2 facet = floor((p + vec2(floor(p.y / 8.0) * 4.0, 0.0)) / 8.0);
		ground *= 0.78 + hash(facet + seed * 7.0) * 0.45;
		vec2 inner = mod(p + vec2(floor(p.y / 8.0) * 4.0, 0.0), 8.0);
		ground *= 1.0 - (1.0 - step(1.0, min(inner.x, inner.y))) * 0.3;
	} else {
		// Static: the floor cannot quite decide to exist. Flickering pixels and a slow scan line.
		float flicker = hash(p + floor(t * 10.0) + seed);
		ground += star_color.rgb * step(0.993, flicker) * 0.6;
		float scan = step(0.5, fract(p.y / 4.0 - t * 0.5));
		ground *= 0.88 + scan * 0.12;
	}

	// One slow network of veins runs across every tile, so the Void reads as one body under the patches.
	float web = fbm(p * 0.012 + vec2(t * 0.01, -t * 0.006));
	float network = smoothstep(0.66, 0.7, web) * (1.0 - smoothstep(0.7, 0.76, web));

	// The Void eats the torn tiles pixel by pixel, with a burning edge where it is still eating.
	float bite = noise(p * 0.12 + seed * 31.0) * 0.8 + noise(p * 0.45) * 0.2;
	float voided = step(bite, taken);
	float rim = (1.0 - smoothstep(0.0, 0.03, abs(bite - taken))) * step(0.04, taken) * step(taken, 0.96);
	vec3 color = mix(torn, ground, voided);
	color += glow_color.rgb * rim * 0.55;
	color += glow_color.rgb * network * 0.3 * voided;

	COLOR = vec4(color, 1.0);
}
