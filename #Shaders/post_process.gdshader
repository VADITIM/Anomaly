shader_type canvas_item;

uniform sampler2D screen_texture : hint_screen_texture, filter_linear_mipmap;
uniform float aberration = 0.0;
uniform float vignette_strength = 0.5;
uniform float aetheriac = 0.0;
uniform float flash = 0.0;
uniform vec4 flash_color : source_color = vec4(1.0);
uniform float saturation = 1.08;
uniform float contrast = 1.08;
uniform float desaturate = 0.0;
uniform vec4 shadow_tint : source_color = vec4(0.10, 0.04, 0.22, 1.0);
uniform vec4 highlight_tint : source_color = vec4(1.0, 0.97, 0.92, 1.0);
uniform float brightness = 1.0;
uniform float bloom = 1.0;
uniform float bloom_threshold = 0.52;
uniform float bloom_strength = 0.55;
uniform float grain = 1.0;
uniform float grain_amount = 0.035;

vec3 bright_pass(vec3 color) {
	float luminance = max(color.r, max(color.g, color.b));
	return color * smoothstep(bloom_threshold, 1.0, luminance);
}

void fragment() {
	vec2 uv = SCREEN_UV;
	vec2 offset = uv - 0.5;
	float shift = aberration * 0.008 + aetheriac * 0.0025;
	vec3 color;
	color.r = texture(screen_texture, uv + offset * shift).r;
	color.g = texture(screen_texture, uv).g;
	color.b = texture(screen_texture, uv - offset * shift).b;

	// Bloom from the screen mip chain: two blurred levels of only the bright parts, added back before grading.
	if (bloom > 0.0) {
		vec3 glow = bright_pass(textureLod(screen_texture, uv, 2.5).rgb) * 0.6
			+ bright_pass(textureLod(screen_texture, uv, 4.5).rgb) * 0.4;
		color += glow * bloom_strength * bloom;
	}

	float luminance = dot(color, vec3(0.299, 0.587, 0.114));
	color = mix(vec3(luminance), color, saturation * (1.0 - desaturate));
	color = (color - 0.5) * contrast + 0.5;
	color = mix(color * mix(vec3(1.0), shadow_tint.rgb * 3.0, 0.4), color * highlight_tint.rgb, smoothstep(0.15, 0.85, luminance));

	vec3 crimson = vec3(luminance * 1.35 + 0.05, luminance * 0.22, luminance * 0.3);
	color = mix(color, crimson, aetheriac * 0.5);

	float vignette = smoothstep(0.85, 0.2, length(offset * vec2(1.0, 0.9)));
	color *= mix(1.0 - vignette_strength - aetheriac * 0.25, 1.0, vignette);
	float pulse = 0.65 + 0.35 * sin(TIME * 7.0);
	color += vec3(0.45, 0.0, 0.06) * aetheriac * (1.0 - vignette) * pulse;

	color *= brightness;
	color = pow(max(color, vec3(0.0)), vec3(1.0 / mix(1.0, brightness, 0.5)));

	// Grain is luminance-weighted toward the midtones so blacks stay black and highlights stay clean.
	if (grain > 0.0) {
		vec2 cell = floor(FRAGCOORD.xy * 0.5);
		float n = fract(sin(dot(cell + fract(TIME * 13.7) * 97.0, vec2(12.9898, 78.233))) * 43758.5453) - 0.5;
		float weight = 1.0 - abs(luminance * 2.0 - 1.0);
		color += n * grain_amount * grain * (0.35 + weight);
	}

	color = mix(color, flash_color.rgb, clamp(flash, 0.0, 1.0));
	COLOR = vec4(clamp(color, 0.0, 1.0), 1.0);
}
