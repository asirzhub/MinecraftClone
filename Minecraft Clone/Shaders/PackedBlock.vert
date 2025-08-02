#version 330 core

// vertex data
layout(location = 0) in uint inPosNorBright;
layout(location = 1) in vec2 inTex;

uniform mat4 model;
uniform mat4 view;
uniform mat4 projection;

out vec2 texCoord;
out vec3 vNormal;
out vec3 vColor;

vec3 DecodePos(uint p) {
    int x = int((p >> 0) & 0x1Fu);
    int y = int((p >> 5) & 0x1Fu);
    int z = int((p >> 10) & 0x1Fu);
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
    texCoord   = inTex;

    vColor = DecodeColor(inPosNorBright);
    vNormal = DecodeNormal(inPosNorBright);
    gl_Position = vec4(DecodePos(inPosNorBright), 1.0) * model * view * projection;
}
