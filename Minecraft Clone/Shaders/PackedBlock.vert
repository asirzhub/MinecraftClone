#version 330 core

// vertex data
layout(location = 0) in uint inPosNorBright;
layout(location = 1) in vec2 inTex;

uniform mat4 model;
uniform mat4 view;
uniform mat4 projection;

uniform float u_waterOffset;
uniform float u_waveAmplitude;
uniform float u_waveScale;
uniform float u_time;
uniform float u_waveSpeed;

out vec2 texCoord;
out vec3 vNormal;
out float brightness;
out int isWater;
out int isFoliage;

vec3 DecodePos(uint p) {
    float x = ((p >> 0) & 0x3Fu);
    float y = ((p >> 6) & 0x3Fu);
    float z = ((p >> 12) & 0x3Fu);
    y -= u_waterOffset * ((inPosNorBright >> 22) & 0x1u);

    return vec3(x, y, z);
}

vec3 DecodeNormal(uint p) {
    uint n = (p >> 18) & 0x7u;
    if (n == 0u) return vec3( 0, 0, 1);
    if (n == 1u) return vec3( 0, 0,-1);
    if (n == 2u) return vec3(-1, 0, 0);
    if (n == 3u) return vec3( 1, 0, 0);
    if (n == 4u) return vec3( 0, 1, 0);
                 return vec3( 0,-1, 0);
}

void main()
{
    texCoord = inTex;

    brightness = uint((inPosNorBright >> 24) & 0xFu);
    vNormal = DecodeNormal(inPosNorBright);
    isWater = int((inPosNorBright >> 22) & 0x1u);
    isFoliage = int(((inPosNorBright >> 21) & 0x1u) + ((inPosNorBright >> 23) & 0x1u));

    vec4 position = vec4(DecodePos(inPosNorBright), 1.0);
    vec4 worldPos = position * model;

    if(isWater == 1)
    {
        position.y += u_waveAmplitude * sin(((worldPos.x + worldPos.z - 5 * worldPos.y) * u_waveScale + u_time * u_waveSpeed)*6.28318);
    }
    if(isFoliage >= 1){
        position.x += u_waveAmplitude * cos(((worldPos.x + worldPos.z - 5 * worldPos.y) * u_waveScale + u_time/2 * u_waveSpeed)*6.28318);
        position.z += u_waveAmplitude * sin(((worldPos.x + worldPos.z + 5 * worldPos.y) * -u_waveScale/2 + u_time * u_waveSpeed)*6.28318);
    }


    gl_Position = (position * model * view * projection);
}
