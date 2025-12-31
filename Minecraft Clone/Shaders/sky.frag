#version 330 core

// camera / projection
uniform float fovY;                 // degrees
uniform float aspectRatio;
uniform vec3  cameraForward;
uniform vec3  cameraRight;
uniform vec3  cameraUp;
uniform vec3  cameraPos;

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

float rand(vec2 n) { 
	return fract(sin(dot(n, vec2(12.9898, 4.1414))) * 43758.5453);
}

float noise(vec2 p){
	vec2 ip = floor(p);
	vec2 u = fract(p);
	u = u*u*(3.0-2.0*u);
	
	float res = mix(
		mix(rand(ip),rand(ip+vec2(1.0,0.0)),u.x),
		mix(rand(ip+vec2(0.0,1.0)),rand(ip+vec2(1.0,1.0)),u.x),u.y);
	return res * res;
}

float fbm(vec2 p, int octaves, float lacunarity, float gain) {
    float sum = 0.0;
    float amp = 0.5;
    float freq = 1.0;
    for (int i = 0; i < octaves; i++) {
        sum += amp * noise(p * freq);
        freq *= lacunarity;
        amp *= gain;
    }
    return sum;
}

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

    // sun disc + glow
    float c = max(dot(rd, sunDir), 0.0);
    float h = clamp(sunDir.y, -1.0, 1.0);                 // sun height proxy
    float cosR    = cos(radians(sunAngularRadiusDeg));
    float sunDisc = smoothstep(cosR, cosR + sunEdgeSoftness, c) * max(h, 0.0);
    vec3  sunGlow = sunColor * pow(c, sunGlowSharpness) * sunGlowStrength;
    
    float cloudHeight = 256;

    float t = (cloudHeight - cameraPos.y) / rd.y;
    vec3 hit = rd * t;

    float cloudScale = 120;
    
    float cloudiness = pow(fbm((hit.xz + cameraPos.xz)/cloudScale, 7, 2.6, 0.6), 1.2) * bz;
    
    vec3 sunsetFactor = pow(1.0-abs(rd.y), 2) * clamp(dot(rd, sunDir), 0.0, 1.0) * sunColor;
    vec3 sky = mix(horizonColor + sunsetFactor, zenithColor, bz);
    vec3 col = sky + sunGlow + sunDisc * sunColor;
    FragColor = vec4(col + vec3(cloudiness), 1.0);
}
