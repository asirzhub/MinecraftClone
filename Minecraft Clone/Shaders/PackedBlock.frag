#version 330 core

in vec2 texCoord;
in vec3 vNormal;
in vec4 worldPos;
in float vertexBrightness;

uniform sampler2D albedoTexture;
uniform sampler2D shadowMap;

uniform mat4 lightProjMat;
uniform mat4 lightViewMat;

uniform vec3 cameraPos;

uniform vec3 u_sunColor;
uniform vec3 u_nightLight;
uniform vec3 u_sunDirection;
uniform vec3 u_fogColor;

uniform float u_minLight;
uniform float u_maxLight;

uniform vec3 u_horizonColor;
uniform vec3 u_zenithColor;

uniform float u_fogStartDistance;
uniform float u_fogEndDistance;

//uniform float u_seaLevel;

in float isWater;
in float isFoliage;

out vec4 FragColor;

float lerp(float a, float b, float t){
    return ( a*t + b*(1-t));
}

float rand(vec2 co) { return fract(sin(dot(co, vec2(12.9898, 78.233))) * 43758.5453); }

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

float shadowAtQuantPos(vec4 qpos, float bias, float fade)
{
    vec4 lightSpacePos = lightProjMat * lightViewMat * qpos;
    vec3 projCoord = (lightSpacePos.xyz / lightSpacePos.w) * 0.5 + 0.5;

    // outside the shadow map = treat as lit
    if (projCoord.x < 0.0 || projCoord.x > 1.0 ||
        projCoord.y < 0.0 || projCoord.y > 1.0)
        return 0.0;

    float edgeFadeX = smoothstep(0.0, fade, projCoord.x) *
                      smoothstep(1.0, 1.0 - fade, projCoord.x);
    float edgeFadeY = smoothstep(0.0, fade, projCoord.y) *
                      smoothstep(1.0, 1.0 - fade, projCoord.y);

    float currentDepth = projCoord.z;
    float mapDepth     = texture(shadowMap, projCoord.xy).r;

    // 1.0 = shadowed, 0.0 = lit
    return (currentDepth - bias > mapDepth) ? (edgeFadeX * edgeFadeY) : 0.0;
}


void main()
{
    const float oneTexel = 1.0/16.0;

    vec4 texColor = texture(albedoTexture, texCoord); 
    if(texColor.a < 0.1) discard;

    float shadowAmount = 0.0;
    float daylight = sqrt(smoothstep(-0.2, 0.2, clamp(u_sunDirection.y + 0.2, 0.0, 1.0)));

    if (dot(u_sunDirection, vNormal) >= 0.0)
    {
        float bias = 0.00001;
        float fade = 0.1;

        vec4 quantPos = floor(worldPos*16.0)/16.0;

        vec4 lightSpacePos = lightProjMat * lightViewMat * quantPos;
        vec3 projCoord = (lightSpacePos.xyz / lightSpacePos.w) * 0.5 + 0.5;

        // 6 axis-adjacent offsets in the grid
        vec4 dx = vec4(oneTexel/2.0, 0.0,     0.0,     0.0);
        vec4 dy = vec4(0.0,     oneTexel/2.0, 0.0,     0.0);
        vec4 dz = vec4(0.0,     0.0,     oneTexel/2.0, 0.0);

        shadowAmount = 
            shadowAtQuantPos(quantPos, bias, fade) * 0.5 +
            shadowAtQuantPos(quantPos + dx, bias, fade) * 0.083 +
            shadowAtQuantPos(quantPos - dx, bias, fade) * 0.083 +
            shadowAtQuantPos(quantPos + dy, bias, fade) * 0.083 +
            shadowAtQuantPos(quantPos - dy, bias, fade) * 0.083 +
            shadowAtQuantPos(quantPos + dz, bias, fade) * 0.083 +
            shadowAtQuantPos(quantPos - dz, bias, fade) * 0.083;
    }

    // Fog
    vec3 fragDirection = worldPos.xyz - cameraPos;
    float fogginess = smoothstep(u_fogStartDistance, u_fogEndDistance, length(fragDirection));

    vec4 finalFogColor = vec4(u_fogColor, 1.0);
    
    // combination of horizon or zenith light (fixed), sunlight * (dynamic), fake SSS, clamped by vertex ambient occlusion
    float isTopFace = clamp(vNormal.y, 0.0, 1.0);

    float SSS = clamp(isFoliage + isWater, 0.0, 1.0) * smoothstep(0.0, 2.0, dot(normalize(fragDirection), u_sunDirection));

    vec3 faceLight = vertexBrightness*vertexBrightness/(16.0*16.0) * (
    (1 - isTopFace) * sqrt(daylight) * u_horizonColor 
    + isTopFace * sqrt(daylight) * u_zenithColor 
    + (clamp(dot(vNormal, u_sunDirection), 0, 1.0) * pow(daylight, 2.0) * (1 - shadowAmount) + SSS) * u_sunColor
    );

    FragColor = mix(texColor * vec4(faceLight, 1.0), finalFogColor, fogginess);
}