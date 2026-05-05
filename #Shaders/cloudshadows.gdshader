shader_type canvas_item;

// Use repeat_enable to allow the noise to loop infinitely
uniform sampler2D noise_texture : repeat_enable, filter_linear;
uniform float speed = 0.02;
uniform float density : hint_range(0.0, 1.0) = 0.45;
uniform float cloud_scale = 0.5; // Controls how big the clouds look
uniform vec4 shadow_color : source_color = vec4(0.0, 0.0, 0.0, 0.5);
uniform vec2 world_offset = vec2(0.0); 

void fragment() {
    // 1. Scale and Offset
    // Multiplying UV by cloud_scale makes the noise patterns larger/smaller
    // The world_offset is scaled down so shadows don't fly off screen too fast
    vec2 uv = (SCREEN_UV * cloud_scale) + (world_offset * 0.0002); 
    
    // 2. Continuous Motion
    // Moving in a diagonal direction looks more natural than pure horizontal
    vec2 movement = vec2(TIME * speed, TIME * speed * 0.5);
    vec2 scrolling_uv = uv + movement;
    
    // 3. Sampling
    float noise_value = texture(noise_texture, scrolling_uv).r;
    
    // 4. The Mask Logic
    // Using smoothstep(density, density + 0.1, noise_value)
    // If noise_value > density, it creates a shadow.
    float mask = smoothstep(density, density + 0.1, noise_value);
    
    // 5. Final Color
    // We mix the shadow color based on the mask.
    // Result: Areas with low noise are clear, areas with high noise are shadowy.
    COLOR = vec4(shadow_color.rgb, mask * shadow_color.a);
}