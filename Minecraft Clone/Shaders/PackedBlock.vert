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
out vec3 vColor;
out int isWater;

vec3 DecodePos(uint p) {
    int x = int((p >> 0) & 0x1Fu);
    float y = int((p >> 5) & 0x1Fu);
    int z = int((p >> 10) & 0x1Fu);
    y -= u_waterOffset * int((inPosNorBright >> 19) & 0x1u);

    return vec3(x, y, z);
}

vec3 DecodeNormal(uint p) {
    uint n = (p >> 16) & 0x7u;
    if (n == 0u) return vec3( 0, 0, 1);
    if (n == 1u) return vec3( 0, 0,-1);
    if (n == 2u) return vec3(-1, 0, 0);
    if (n == 3u) return vec3( 1, 0, 0);
    if (n == 4u) return vec3( 0, 1, 0);
                 return vec3( 0,-1, 0);
}

vec3 DecodeColor(uint p) {
    int x = int((p >> 20) & 0xFu);
    int y = int((p >> 24) & 0xFu);
    int z = int((p >> 28) & 0xFu);
    return (vec3(x, y, z)+1)/16;
}

void main()
{
    texCoord = inTex;

    vColor = DecodeColor(inPosNorBright);
    vNormal = DecodeNormal(inPosNorBright);
    isWater = int((inPosNorBright >> 19) & 0x1u);

    vec4 position = vec4(DecodePos(inPosNorBright), 1.0);
    vec4 worldPos = position * model;

    if(isWater == 1)
    {
        position.y += u_waveAmplitude * sin(((worldPos.x + worldPos.z) * u_waveScale + u_time * u_waveSpeed)*6.28318);
    }

    gl_Position = (position * model * view * projection);
}
