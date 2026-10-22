shader_type canvas_item;

uniform float flash = 0.0;
uniform vec4 flash_color : source_color = vec4(1.0, 1.0, 1.0, 1.0);
uniform float outline = 0.0;
uniform vec4 outline_color : source_color = vec4(1.0, 0.15, 0.25, 1.0);
uniform float tint_strength = 0.0;
uniform vec4 tint_color : source_color = vec4(1.0, 1.0, 1.0, 1.0);
// Void corruption, worn by everything that lives in the Void. Where the Void floor is glass over the abyss, a corrupted
// body is being eaten from inside: bruised violet, crawling veins, holes onto the abyss, a body that slips like a bad signal.
uniform float corruption = 0.0;
uniform float corruption_seed = 0.0;
uniform vec4 corruption_dark : source_color = vec4(0.17, 0.05, 0.27, 1.0);
uniform vec4 corruption_glow : source_color = vec4(0.95, 0.3, 0.85, 1.0);
uniform vec4 corruption_fringe : source_color = vec4(0.4, 0.9, 1.0, 1.0);

float hash(vec2 p) {
	return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453);
}

float noise(vec2 p) {
	vec2 i = floor(p);
	vec2 f = fract(p);
	f = f * f * (3.0 - 2.0 * f);
	return mix(mix(hash(i), hash(i + vec2(1.0, 0.0)), f.x), mix(hash(i + vec2(0.0, 1.0)), hash(i + vec2(1.0, 1.0)), f.x), f.y);
}

void fragment() {
	vec4 modulate = COLOR;
	vec2 px = TEXTURE_PIXEL_SIZE;
	vec2 uv = UV;
	vec2 texel_pos = floor(UV / px);
	if (corruption > 0.0) {
		// Now and then a row of the body slips sideways and snaps back.
		float tick = floor(TIME * 9.0 + corruption_seed * 17.0);
		float row = floor(texel_pos.y / 2.0);
		float slip = step(0.94, hash(vec2(row, tick))) * corruption;
		uv.x += round((hash(vec2(tick, row + 7.0)) - 0.5) * 6.0) * px.x * slip;
	}
	vec4 texel = texture(TEXTURE, uv);
	if (outline > 0.0 && texel.a < 0.1) {
		float neighbours = texture(TEXTURE, uv + vec2(px.x, 0.0)).a + texture(TEXTURE, uv - vec2(px.x, 0.0)).a
			+ texture(TEXTURE, uv + vec2(0.0, px.y)).a + texture(TEXTURE, uv - vec2(0.0, px.y)).a;
		COLOR = neighbours > 0.0 ? vec4(outline_color.rgb, outline_color.a * outline * modulate.a) : vec4(0.0);
	} else if (corruption > 0.0 && texel.a < 0.1) {
		// A split fringe, magenta on one side and cyan on the other, flickering.
		float right = texture(TEXTURE, uv + vec2(px.x, 0.0)).a;
		float left = texture(TEXTURE, uv - vec2(px.x, 0.0)).a;
		float flicker = 0.55 + 0.45 * sin(TIME * 13.0 + corruption_seed * 40.0 + texel_pos.y * 0.7);
		vec4 fringe = right > 0.1 ? corruption_glow : left > 0.1 ? corruption_fringe : vec4(0.0);
		COLOR = vec4(fringe.rgb, fringe.a * 0.7 * flicker * corruption * modulate.a);
	} else {
		vec3 rgb = texel.rgb * modulate.rgb;
		rgb = mix(rgb, rgb * tint_color.rgb * 1.4 + tint_color.rgb * 0.08, tint_strength);
		if (corruption > 0.0) {
			// Bruised toward the Void's violet, keeping the sprite's own shading.
			float lum = dot(rgb, vec3(0.299, 0.587, 0.114));
			rgb = mix(rgb, corruption_dark.rgb * (0.6 + lum * 2.2), 0.6 * corruption);
			// Veins drawn in the body's own pixels, so they crawl with it.
			float v = noise(texel_pos * 0.22 + vec2(corruption_seed * 13.0, TIME * 0.9));
			float vein = smoothstep(0.46, 0.5, v) * (1.0 - smoothstep(0.5, 0.56, v));
			rgb += corruption_glow.rgb * vein * 0.8 * corruption;
			// Holes where the body is already gone. The stars behind them are fixed to the screen, not the body,
			// so they read as a window onto the abyss.
			float hollow = smoothstep(0.7, 0.74, noise(texel_pos * 0.12 + vec2(TIME * 0.35, corruption_seed * 5.0)));
			vec2 screen = floor(FRAGCOORD.xy / 2.0);
			float star = step(0.97, hash(screen)) * (0.5 + 0.5 * sin(TIME * 4.0 + hash(screen) * 30.0));
			vec3 abyss = vec3(0.02, 0.0, 0.05) + corruption_fringe.rgb * star;
			rgb = mix(rgb, abyss, hollow * corruption);
		}
		rgb = mix(rgb, flash_color.rgb, flash);
		COLOR = vec4(rgb, texel.a * modulate.a);
	}
}
