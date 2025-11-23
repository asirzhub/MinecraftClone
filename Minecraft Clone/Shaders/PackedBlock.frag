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

out vec4 FragColor;

vec4 lerpvec4(vec4 a, vec4 b, float t){
    return ( a*t + b*(1-t));
}

float lerp(float a, float b, float t){
    return ( a*t + b*(1-t));
}

float rand(vec2 co) { return fract(sin(dot(co, vec2(12.9898, 78.233))) * 43758.5453); }

void main()
{
    vec4 texColor = texture(albedoTexture, texCoord);
    if(texColor.a < 0.1) discard;

    vec4 finalColor = vec4(1.0); // calculation is as follows: lerp( albedo * lightLevel * shadowFactor,  fogColor,  t = fogAttenuation);
    vec3 shadowFactor = vec3(0.0);
    vec3 lightLevel = vec3(vertexBrightness/16.0);
    float dist = distance(cameraPos, worldPos.xyz);

    // depending on if the sun can light this face or not, use shadowmap or static face lighting
    float sunLightAmount = dot(vNormal, sunDirection);
    if(sunLightAmount < 0)
    {
        shadowFactor = vec3(0.0);
    }
    else{        
        
        float bias = 0.00005;

        float fade = 0.1;

        vec4 lightSpacePos = lightProjMat * lightViewMat * worldPos; 
        vec3 projCoord = (lightSpacePos.xyz/lightSpacePos.w) * 0.5 + 0.5;
        
        float shadow = 0.0;

        if(projCoord.x >= 0.0 && projCoord.x <= 1.0 && projCoord.y >= 0.0 && projCoord.y <= 1.0)
        {            
            // distance to edge (for fading out the shadow at the shadowmap boundary)
            float edgeFadeX = smoothstep(0.0, fade, projCoord.x) * smoothstep(1.0, 1.0 - fade, projCoord.x);
            float edgeFadeY = smoothstep(0.0, fade, projCoord.y) * smoothstep(1.0, 1.0 - fade, projCoord.y);

            float current = projCoord.z;

            int pcfSampleSize = 2;

            vec2 texelSize = 1.0 / textureSize(shadowMap, 0);

            for(int x = -pcfSampleSize; x <= pcfSampleSize; ++x)
            {
                for(int y = -pcfSampleSize; y <= pcfSampleSize; ++y)
                {
                    float pcfDepth = texture(shadowMap, projCoord.xy + vec2(x,y)/(abs(x) + abs(y)) * texelSize * rand(worldPos.xz)).r; 
                    shadow += current - bias > pcfDepth ? 1.0 : 0.0;        
                }    
            }

            shadow /= (((pcfSampleSize*2)+1) * ((pcfSampleSize*2)+1))/(edgeFadeX * edgeFadeY); // ((r*2) + 1)^2
        }
        
        shadowFactor = vec3((1.0 - shadow) * sqrt(sunLightAmount));
    }
    float daytime = 0;

    if(sunDirection.y + 0.2 < 0){
        daytime = 0.2 - 0.1 * sunDirection.y; // bounce back as if the moon lights things up
    }
    if(sunDirection.y + 0.2 >= 0)
    {
        daytime = clamp(dot(sunDirection, vec3(0, 1, 0)), 0.2, 1);
    }

    shadowFactor = clamp(shadowFactor, u_minLight, u_maxLight);

    float fogginess = clamp(1.0/exp((dist * dist)/500000.0), 0.0, 1.0);

    FragColor = lerpvec4(texColor * vec4(lightLevel, 1.0) * vec4(shadowFactor, 1.0), vec4(fogColor.xyz, 1.0), fogginess);
}