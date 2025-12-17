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

uniform vec3 sunDirection;
uniform vec3 fogColor;

uniform float u_minLight;
uniform float u_maxLight;

//uniform float u_seaLevel;

in float isWater;
in float isFoliage;

out vec4 FragColor;

vec4 lerpvec4(vec4 a, vec4 b, float t){
    return ( a*t + b*(1-t));
}

vec3 lerpvec3(vec3 a, vec3 b, float t){
    return ( a*t + b*(1-t));
}

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

void main()
{
    vec4 texColor = texture(albedoTexture, texCoord); 
    if(texColor.a < 0.1) discard;

    float daylightRaw = clamp(dot(sunDirection, vec3(0.0, 1.0, 0.0)), 0.0, 1.0);

    float daytime = (sunDirection.y < 0.0)
        ? 0.447
        : sqrt(clamp(daylightRaw, 0.2, 1.0));

    float warmFactor = max(0.0, daytime - 0.447);

    // Explicit night flag
    bool isNight = (warmFactor <= 0.0);

    vec3 shadowFactor = vec3(u_minLight);

    float sunLightAmount = smoothstep(0.0, 0.2, sunDirection.y);

    if (sunLightAmount > 0.0 && !isNight && dot(sunDirection, vNormal) >= 0.0)
    {
        float bias = 0.00005;
        float fade = 0.1;

        vec4 lightSpacePos = lightProjMat * lightViewMat * worldPos;
        vec3 projCoord = (lightSpacePos.xyz / lightSpacePos.w) * 0.5 + 0.5;

        float shadow = 0.0;

        if (projCoord.x >= 0.0 && projCoord.x <= 1.0 &&
            projCoord.y >= 0.0 && projCoord.y <= 1.0)
        {
            float edgeFadeX = smoothstep(0.0, fade, projCoord.x) *
                              smoothstep(1.0, 1.0 - fade, projCoord.x);

            float edgeFadeY = smoothstep(0.0, fade, projCoord.y) *
                              smoothstep(1.0, 1.0 - fade, projCoord.y);

            float current = projCoord.z;

            int pcfSampleSize = 1;
            vec2 texelSize = 1.0 / textureSize(shadowMap, 0);

            for (int x = -pcfSampleSize; x <= pcfSampleSize; ++x)
            {
                for (int y = -pcfSampleSize; y <= pcfSampleSize; ++y)
                {
                    // Spread sampling using randomness and texel size
                    vec2 offset = vec2(x, y) / max(1.0, float(abs(x) + abs(y)));
                    float pcfDepth = texture(shadowMap, projCoord.xy + offset * texelSize * rand(worldPos.xz)).r;

                    shadow += (current - bias > pcfDepth) ? 1.0 : 0.0;
                }
            }

            float kernel = float((pcfSampleSize * 2 + 1) * (pcfSampleSize * 2 + 1));
            shadow /= (kernel / (edgeFadeX * edgeFadeY));
        }

        shadowFactor = vec3((1.0 - shadow) * sqrt(sunLightAmount));
    }

    shadowFactor = clamp(shadowFactor, u_minLight, u_maxLight);

    float SSS = 0.0;
    if (!isNight)
    {
        float surf = clamp(isWater + isFoliage, 0.0, 1.0);
        float facing = clamp(dot(vNormal, sunDirection), 0.1, 1.0);
        SSS = surf * facing;
    }

    // Sun-facing fog tint only during daytime
    float sunAlignment = 0.0;
    if (!isNight)
    {
        sunAlignment = smoothstep(0.0, 0.3, dot(normalize(worldPos.xyz - cameraPos), sunDirection));
    }

    // fake subsurface scattering
    vec4 tintedColor = texColor + vec4(0.3, 0.2, 0.1, 0.0) * (SSS * sunAlignment * warmFactor);

    vec3 warmTint = vec3(1.1, 0.6, 0.3);
    vec4 litColor = tintedColor * vec4(1.0 + SSS * warmFactor * sunAlignment * warmTint, 1.0);

    vec3 lightLevel = vec3(vertexBrightness / 16.0);
    litColor *= vec4(((1.0 + daytime) * lightLevel) * shadowFactor, 1.0);

    // Fog
    float distToCamera = distance(cameraPos, worldPos.xyz);
    float fogginess = clamp(1.0 / exp((distToCamera * distToCamera) / (100000.0 * daytime)), 0.0, 1.0);
        
    // Warm fog only during daytime
    vec4 fogWarm = vec4(sunAlignment, sunAlignment * 0.5, sunAlignment * 0.333, 0.0) * warmFactor;

    vec4 finalFogColor = vec4(fogColor, 1.0) + fogWarm;

    FragColor = mix(litColor, finalFogColor, 1 - fogginess);
}