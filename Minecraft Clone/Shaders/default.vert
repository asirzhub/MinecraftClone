#version 330 core

layout(location = 0) in vec3 aPosition;
layout(location = 1) in vec2 aTexCoord;
layout(location = 2) in vec3 aNormal;
layout(location = 3) in float aBrightness;

out vec2 texCoord;
out vec3 vNormal;
out float brightness;

// uniform variables to transform
uniform mat4 model;
uniform mat4 view;
uniform mat4 projection;

void main(void)
{
    texCoord = aTexCoord;
    brightness = aBrightness;

    vNormal = (vec4(aNormal, 1) * model).xyz; // apply the transformation to the vertex normals
    gl_Position = vec4(aPosition, 1.0) * model * view * projection;
}