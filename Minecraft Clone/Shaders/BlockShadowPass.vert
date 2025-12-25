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

vec3 DecodePos(uint p) {
    float x = ((p >> 0) & 0x7Fu);
    float y = ((p >> 7) & 0x7Fu);
    float z = ((p >> 14) & 0x7Fu);

    return vec3(x, y, z);
}

vec3 DecodeNormal(uint p) {
    uint n = (p >> 21) & 0x7u;
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
    vec4 position = vec4(DecodePos(inPosNorBright), 1.0);

    gl_Position = projection * view * model * position;
}