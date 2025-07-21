#version 330

out vec4 outputColor;

in vec2 texCoord;
in vec3 vNormal;
uniform sampler2D texture0;

uniform mat4 model;

void main()
{
    vec3 lightDirection = vec3(1.0, 1.0, 1.0);
    outputColor = texture(texture0, texCoord) * (0.8 + 0.2*dot(vNormal, lightDirection));
}