#version 330 core

in vec2 texCoord;
in vec3 vNormal;
in vec4 worldPos;
in float brightness;
uniform sampler2D texture0;

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
    vec4 texColor = texture(texture0, texCoord);

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

    vec4 skyLighting = vec4(faceBrightness * vec3(sunsetColor) + daytime * ambientColor, 1);

    float dist = 1.0 - exp(min((-distance(cameraPos, worldPos.xyz)+50)/100, 0));

    vec3 finalFogColor = fogColor + vec3(0.0, 0.0, 1/dist);

    FragColor = clamp(lerpvec4(texColor * vec4(vec3(brightness/15), 1) * skyLighting, vec4(fogColor, 1), 1-dist), 0.0, 1.0);
}
