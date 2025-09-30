#version 330 core

// camera / projection
uniform float fovY;                 // degrees
uniform float aspectRatio;
uniform vec3  cameraForward;
uniform vec3  cameraRight;
uniform vec3  cameraUp;

// sun (disc + glow)
uniform vec3  sunDir;               // normalized
uniform vec3  sunColor;             // also tints the disc
uniform float sunAngularRadiusDeg;  // e.g., 0.27
uniform float sunEdgeSoftness;      // e.g., 0.0005 (cos-space)
uniform float sunGlowStrength;      // e.g., 1.0
uniform float sunGlowSharpness;     // e.g., 300.0

// sky colors (already blended on CPU: day/night + sunset)
uniform vec3  horizonColor;
uniform vec3  zenithColor;

in vec2 screenUV;
out vec4 FragColor;

void main()
{
    // view ray
    float s = tan(radians(fovY) * 0.5);
    vec2  ndc = screenUV * 2.0 - 1.0;
    vec3  rd  = normalize(ndc.x * s * aspectRatio * cameraRight
                        + ndc.y * s *               cameraUp
                        +        1.0 *              cameraForward);

    // vertical blend for gradient (fixed curve)
    float up = clamp(rd.y, 0.0, 1.0);
    float bz = pow(up, 0.7);

    vec3 sky = mix(horizonColor, zenithColor, bz);

    // sun disc + glow
    float c = max(dot(rd, sunDir), 0.0);
    float h = clamp(sunDir.y, -1.0, 1.0);                 // sun height proxy
    float cosR    = cos(radians(sunAngularRadiusDeg));
    float sunDisc = smoothstep(cosR, cosR + sunEdgeSoftness, c) * max(h, 0.0);
    vec3  sunGlow = sunColor * pow(c, sunGlowSharpness) * sunGlowStrength;

    vec3 col = sky + sunGlow + sunDisc * sunColor;
    FragColor = vec4(col, 1.0);
}
