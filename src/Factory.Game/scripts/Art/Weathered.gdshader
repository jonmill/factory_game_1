// The game's one surface material for baked buildings. Every part is painted the same way, so the whole
// fleet reads as one family of worked machines:
//   1. a base colour (optionally times a pattern: bricks, planks, stripes), with fine grain and soft mottling;
//   2. panel seams with rivets at their crossings, on panelled surfaces;
//   3. weathering tied to form and gravity: rain streaks down vertical faces, rust blooms low down,
//      dust settled on upward faces and grime splashed up from the ground;
//   4. bright worn metal on chamfered edges (flagged by the mesh's vertex colour, green = 0 on an edge).
// Positions are in world tiles (the bake's north-south stretch is undone), so detail is the same size on
// every building.
shader_type spatial;
render_mode blend_mix, depth_draw_opaque, cull_back, diffuse_burley, specular_schlick_ggx;

uniform vec4 base_color : source_color = vec4(0.5, 0.5, 0.5, 1.0);
uniform sampler2D pattern : source_color, filter_linear_mipmap, repeat_enable, hint_default_white;
uniform float pattern_scale = 1.0;
uniform float metallic = 0.2;
uniform float roughness = 0.6;
uniform vec4 wear_color : source_color = vec4(0.72, 0.7, 0.66, 1.0);
uniform vec4 rust_color : source_color = vec4(0.42, 0.2, 0.09, 1.0);
uniform vec4 dust_color : source_color = vec4(0.5, 0.42, 0.31, 1.0);
uniform float rust = 0.3;
uniform float grime = 0.5;
uniform float streaks = 0.5;
uniform float wear = 0.7;
uniform float panel = 0.0;
uniform float stretch = 1.3054;

varying vec3 wpos;
varying vec3 wnrm;

float hash(vec3 p) {
    p = fract(p * 0.3183099 + vec3(0.71, 0.113, 0.419));
    p *= 17.0;
    return fract(p.x * p.y * p.z * (p.x + p.y + p.z));
}

float noise(vec3 x) {
    vec3 i = floor(x);
    vec3 f = fract(x);
    f = f * f * (3.0 - 2.0 * f);
    return mix(mix(mix(hash(i + vec3(0, 0, 0)), hash(i + vec3(1, 0, 0)), f.x),
                   mix(hash(i + vec3(0, 1, 0)), hash(i + vec3(1, 1, 0)), f.x), f.y),
               mix(mix(hash(i + vec3(0, 0, 1)), hash(i + vec3(1, 0, 1)), f.x),
                   mix(hash(i + vec3(0, 1, 1)), hash(i + vec3(1, 1, 1)), f.x), f.y), f.z);
}

float fbm(vec3 p) {
    float v = 0.0;
    float a = 0.5;
    for (int i = 0; i < 4; i++) {
        v += a * noise(p);
        p *= 2.03;
        a *= 0.5;
    }
    return v;
}

void vertex() {
    vec3 w = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
    wpos = vec3(w.x, w.y, w.z / stretch);
    wnrm = normalize((MODEL_MATRIX * vec4(NORMAL, 0.0)).xyz);
}

// Seam lines on a panel grid in one plane (x = line, y = rivet), rivets in a row just beside each seam.
vec2 seams(vec2 uv, float size) {
    vec2 g = uv / size;
    vec2 e = (0.5 - abs(fract(g) - 0.5)) * size;     // distance to the nearest seam on each axis
    float line = 1.0 - smoothstep(0.004, 0.011, min(e.x, e.y));
    float pitch = size / 5.0;
    vec2 along = (abs(fract(uv / pitch) - 0.5)) * pitch;
    float rx = 1.0 - smoothstep(0.006, 0.011, length(vec2(e.x - 0.022, along.y)));
    float ry = 1.0 - smoothstep(0.006, 0.011, length(vec2(e.y - 0.022, along.x)));
    return vec2(line, max(rx, ry));
}

void fragment() {
    vec3 n = normalize(wnrm);
    vec3 bw = pow(abs(n), vec3(4.0));
    bw /= (bw.x + bw.y + bw.z);
    vec3 p = wpos;

    // 1. Base with pattern, mottling and grain.
    vec3 pat = texture(pattern, p.zy * pattern_scale).rgb * bw.x
             + texture(pattern, p.xz * pattern_scale).rgb * bw.y
             + texture(pattern, p.xy * pattern_scale).rgb * bw.z;
    vec3 col = base_color.rgb * pat;
    float mottle = fbm(p * 3.0);
    float grain = noise(p * 90.0);
    col *= 0.84 + 0.3 * mottle;
    col *= 0.94 + 0.12 * grain;
    float rough = roughness;
    float metal = metallic;

    // 2. Panel seams and rivets.
    if (panel > 0.0) {
        vec2 sx = seams(p.zy, panel);
        vec2 sy = seams(p.xz, panel);
        vec2 sz = seams(p.xy, panel);
        vec2 s = sx * bw.x + sy * bw.y + sz * bw.z;
        col *= 1.0 - 0.45 * s.x;
        col = mix(col, col * 0.6, s.y);
        col = mix(col, wear_color.rgb, s.y * 0.35 * smoothstep(0.3, 0.8, n.y + 0.5));
    }

    // 3. Weathering by form and gravity.
    float side = 1.0 - smoothstep(0.35, 0.7, abs(n.y));
    float streak = noise(vec3(p.x * 34.0 + p.z * 34.0, p.y * 2.2, 0.0)) * noise(vec3(p.x * 9.0 + p.z * 9.0, p.y * 0.8, 3.0));
    col *= 1.0 - side * streaks * smoothstep(0.15, 0.55, streak) * 0.45;

    float low = 1.0 - smoothstep(0.02, 0.42, p.y);
    float rustMask = smoothstep(0.55, 0.75, fbm(p * 2.2 + vec3(5.0)) + low * 0.25) * rust;
    col = mix(col, rust_color.rgb * (0.8 + 0.4 * noise(p * 25.0)), rustMask);
    rough = mix(rough, 0.9, rustMask);
    metal = mix(metal, 0.1, rustMask);

    float dust = smoothstep(0.55, 0.95, n.y) * smoothstep(0.35, 0.75, fbm(p * 5.0 + vec3(11.0))) * grime * 0.55;
    float splash = low * smoothstep(0.3, 0.7, fbm(p * 7.0 + vec3(2.0))) * grime;
    col = mix(col, dust_color.rgb, clamp(dust + splash * 0.7, 0.0, 0.85));
    rough = mix(rough, 0.95, dust + splash);

    // 4. Worn, bright edges on the chamfers, broken up so they read as chips and polish, not outlines.
    float edge = 1.0 - COLOR.g;
    float chip = smoothstep(0.35, 0.6, noise(p * 40.0) * 0.6 + noise(p * 9.0) * 0.6);
    float worn = edge * chip * wear;
    col = mix(col, wear_color.rgb, worn * 0.85);
    metal = mix(metal, 0.85, worn);
    rough = mix(rough, 0.28, worn);

    ALBEDO = col;
    METALLIC = metal;
    ROUGHNESS = clamp(rough, 0.05, 1.0);
}
