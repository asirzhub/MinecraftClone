#version 330 core

uniform sampler2D texture0;

in vec2 screenUV;
out vec4 FragColor;

void main()
{
    
    FragColor = vec4(texture(texture0, screenUV).rrr*6.0, 1.0);
}
