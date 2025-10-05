#version 330 core

// vertex data
layout(location = 0) in uint inPosNorBright;
layout(location = 1) in vec2 inTex;

uniform mat4 model;
uniform mat4 view;
uniform mat4 projection;
uniform vec3 sunDirection;

uniform float u_waterOffset;
uniform float u_waveAmplitude;
uniform float u_waveScale;
uniform float u_time;
uniform float u_waveSpeed;

vec3 DecodePos(uint p) {
    float x = ((p >> 0) & 0x3Fu);
    float y = ((p >> 6) & 0x3Fu);
    float z = ((p >> 12) & 0x3Fu);
    y -= u_waterOffset * ((inPosNorBright >> 22) & 0x1u);

    return vec3(x, y, z);
}

void main()
{
    int isWater = int((inPosNorBright >> 22) & 0x1u);
    int isFoliage = int(((inPosNorBright >> 21) & 0x1u) + ((inPosNorBright >> 23) & 0x1u));

    vec4 position = vec4(DecodePos(inPosNorBright), 1.0);
    vec4 worldPos = position * model;

    if(isWater == 1)
    {
        position.y += u_waveAmplitude * sin(((worldPos.x + worldPos.z - 5 * worldPos.y) * u_waveScale + u_time * u_waveSpeed)*6.28318);
    }
    if(isFoliage >= 1){
        position.x += sin(((worldPos.x + worldPos.z ) + u_time * u_waveSpeed)*6.28318) * u_waveAmplitude * cos(((worldPos.x + worldPos.z - 5 * worldPos.y) * u_waveScale + u_time/2 * u_waveSpeed)*6.28318);
        position.z += u_waveAmplitude * sin(((worldPos.x + worldPos.z + 5 * worldPos.y) * -u_waveScale/2 + u_time * u_waveSpeed)*6.28318);
    }

    vec3 blag = 0.0 * sunDirection;

    gl_Position = (position * model * view * projection);
}
