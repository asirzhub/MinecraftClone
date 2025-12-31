#version 330 core

in vec2 texCoord;
in vec3 vNormal;
in vec4 worldPos;
in float vertexBrightness;

uniform sampler2D albedoTexture;
uniform sampler2DShadow shadowMap;

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

float shadowAtQuantPos(vec4 qpos, float bias)
{
    vec4 lightSpacePos = lightProjMat * lightViewMat * qpos;
    vec3 projCoord = (lightSpacePos.xyz / lightSpacePos.w) * 0.5 + 0.5;

    // outside the shadow map = treat as lit
    if (projCoord.x < 0.0 || projCoord.x > 1.0 ||
        projCoord.y < 0.0 || projCoord.y > 1.0)
        return 0.0; // 0 shadow

    // hardware compare: returns 1.0 when lit, 0.0 when shadowed (with LEQUAL/LESS depending)
    float visibility = texture(shadowMap, vec3(projCoord.xy, projCoord.z - bias));

    // convert to "shadow amount" (1 = shadowed, 0 = lit)
    return (1.0 - visibility);
}



void main()
{
    const float oneTexel = 1.0/16.0;

    vec4 texColor = texture(albedoTexture, texCoord); 
    if(texColor.a < 0.1) discard;

    float shadowAmount = 0.0;
    float daylight = smoothstep(-0.2, 0.2, clamp(u_sunDirection.y + 0.2, 0.0, 1.0));

    if (dot(u_sunDirection, vNormal) >= 0.0)
    {
        float bias = 0.00002;
        float fade = 0.3;
        vec4 centerOffset = vec4(vec3(oneTexel/2.0), 0.0);
        vec4 quantPos = (floor((worldPos + centerOffset)/oneTexel)-centerOffset)*oneTexel;

        vec4 lightSpacePos = lightProjMat * lightViewMat * quantPos;
        vec3 projCoord = (lightSpacePos.xyz / lightSpacePos.w) * 0.5 + 0.5;

        float edgeFadeX = smoothstep(0.0, fade, projCoord.x) *
                          smoothstep(1.0, 1.0 - fade, projCoord.x);
        float edgeFadeY = smoothstep(0.0, fade, projCoord.y) *
                          smoothstep(1.0, 1.0 - fade, projCoord.y);

        // 6 axis-adjacent offsets in the grid
        vec4 dx = vec4(oneTexel, 0.0,     0.0,     0.0);
        vec4 dy = vec4(0.0,     oneTexel, 0.0,     0.0);
        vec4 dz = vec4(0.0,     0.0,     oneTexel, 0.0);

        shadowAmount = edgeFadeX * edgeFadeY * (
        0.5 * shadowAtQuantPos(quantPos, bias) + 
        0.5 / 6.0 * ( 
        shadowAtQuantPos(quantPos + dx, bias) + 
        shadowAtQuantPos(quantPos - dx, bias) + 
        shadowAtQuantPos(quantPos + dy, bias) + 
        shadowAtQuantPos(quantPos - dy, bias) + 
        shadowAtQuantPos(quantPos + dz, bias) + 
        shadowAtQuantPos(quantPos - dz, bias) ) );
    }

    // Fog
    vec3 fragDirection = worldPos.xyz - cameraPos;
    float fogginess = smoothstep(u_fogStartDistance, u_fogEndDistance, length(fragDirection));

    // combination of horizon or zenith light (fixed), sunlight * (dynamic), fake SSS, clamped by vertex ambient occlusion
    float isTopFace = clamp(vNormal.y, 0.0, 1.0);

    float SSS = clamp(isFoliage + isWater, 0.0, 1.0) * smoothstep(0.0, 2.0, dot(normalize(fragDirection), u_sunDirection));

    vec3 faceLight = vertexBrightness*vertexBrightness/(16.0*16.0) * (
    ((1.2 - isTopFace) * normalize( u_horizonColor )
    + 0.2 + isTopFace * normalize( u_zenithColor )) * 0.8
    + (clamp(dot(vNormal, u_sunDirection) + SSS, 0, 1.0) * sqrt(daylight)) * (1 - shadowAmount) * u_sunColor
    );

    FragColor = mix(texColor * vec4(faceLight, 1.0), vec4(u_fogColor, 1.0), fogginess);
}