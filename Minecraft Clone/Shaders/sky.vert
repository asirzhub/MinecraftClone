#version 330 core

layout(location = 0) in vec2 aPos;
out vec2 screenUV;
in vec3 viewDir;

void main()
{
    // Convert from [-1, 1] to [0, 1] for UVs
    screenUV = aPos * 0.5 + 0.5;

    gl_Position = vec4(aPos, 0.0, 1.0);
}
