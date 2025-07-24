#version 330 core
uniform float fovY, aspectRatio;
uniform vec3 sunDir;                // assumed normalized
uniform vec3 cameraForward, cameraRight, cameraUp;

in vec2 screenUV;
out vec4 FragColor;

void main() {
    // --- generate ray direction ---
    float s = tan(radians(fovY) * 0.5);
    vec2 ndc = screenUV * 2.0 - 1.0;
    vec3 rd = normalize(ndc.x * s * aspectRatio * cameraRight
                      + ndc.y * s * cameraUp
                      + cameraForward);

    // --- sun parameters ---
    float h = clamp(sunDir.y, -1.0, 1.0);
    float c = max(dot(rd, sunDir), 0.0);
    const float SUN_SIZE = 0.004;
    float cosR = cos(SUN_SIZE);
    float sunDisc = smoothstep(cosR, cosR + 0.0005, c) * h;
    vec3 sunGlow = vec3(1,0.8,0.6) * pow(c, 300.0);

    // --- height-based blends ---
    float up = clamp(rd.y, 0.0, 1.0);
    float bz = pow(up, 0.7);           // zenith boost (day & night)

    // --- day sky ---
    vec3 dayH = vec3(0.8,0.9,1.0),
         dayZ = vec3(0.4,0.6,1.0);
    vec3 sky = mix(dayH, dayZ, bz);

    // --- sunset glow ---
    vec3 glowCol = mix(vec3(1,0.4,0.1), vec3(1,0.1,0.6), 0.5 + 0.5*h);
    float glowB  = smoothstep(0.2, -0.1, h) * (1.0 - up);
    sky = mix(sky, glowCol, glowB);

    // --- night sky ---
    vec3 nightH = vec3(0.05,0.1,0.2),
         nightZ = vec3(0.02,0.05,0.1);
    vec3 night = mix(nightH, nightZ, bz);
    float nb = smoothstep(-0.05, -0.3, h);
    sky = mix(sky, night, nb);

    // --- composite ---
    vec3 col = sky + sunGlow + sunDisc * vec3(1.5,1.2,1.0);
    FragColor = vec4(col, 1.0);
}
