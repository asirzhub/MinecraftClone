#version 330 core

in vec2 texCoord;
in vec3 vNormal;
in vec4 worldPos;
in float brightness;

uniform sampler2D albedoTexture;
uniform sampler2D shadowMap;

uniform mat4 lightProjMat;
uniform mat4 lightViewMat;

uniform vec3 cameraPos;

uniform vec3 sunDirection;
uniform vec3 ambientColor;
uniform vec3 sunsetColor;
uniform vec3 fogColor;

out vec4 FragColor;

vec4 lerpvec4(vec4 a, vec4 b, float t){
    return ( a*t + b*(1-t));
}

void main()
{
    float bias = 0.001;

    vec4 texColor = texture(albedoTexture, texCoord);
    

    if (texColor.a < 0.1)
        discard;

    vec3 liftedSun = normalize(sunDirection + vec3(0, 0.2, 0));

    float faceBrightness = 0;
    float daytime = 0;

    if(liftedSun.y <0){
        faceBrightness = clamp(dot(liftedSun, vNormal) + 3 * liftedSun.y, 0, 1);
        daytime = 0.2 - 0.1 * liftedSun.y; // bounce back as if the moon lights things up
    }
    if(liftedSun.y >= 0)
    {
        faceBrightness = clamp(dot(liftedSun, vNormal), 0, 1);
        daytime = clamp(dot(liftedSun, vec3(0, 1, 0)), 0.2, 1);
    }

    vec4 skyLighting = vec4(faceBrightness * vec3(sunsetColor) + daytime * ambientColor, 1) + vec4(fogColor, 1.0); // remove fogColor after, this is here to avoid error

    float dist = exp(min((-distance(cameraPos, worldPos.xyz)+100)/150 - worldPos.y/512.0, 0)) ;
    
    vec4 lightSpacePos = lightProjMat * lightViewMat * worldPos; // 
    vec3 projCoord = (lightSpacePos.xyz/lightSpacePos.w) * 0.5 + 0.5;

    float closest = texture(shadowMap, projCoord.xy).r;
    float current = projCoord.z - 0.0001;

    float diff = closest - current;
    float shadow = diff;
    
    if(diff >= 0) { shadow = 1; }
    if(diff < 0) { shadow = 0.5; }

    FragColor = clamp(lerpvec4(vec4(shadow, shadow, shadow, 1.0) * texColor * vec4(vec3(brightness/15), 1) * skyLighting, vec4(fogColor, 1), dist), 0.0, 1.0);
}
