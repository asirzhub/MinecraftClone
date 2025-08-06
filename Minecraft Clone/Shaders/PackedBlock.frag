#version 330 core

in vec2 texCoord;
in vec3 vNormal;
in float brightness;
uniform sampler2D texture0;

out vec4 FragColor;

void main()
{
    vec4 texColor = texture(texture0, texCoord);

    if (texColor.a < 0.1)
        discard;

    FragColor = texColor * vec4(vec3(brightness/7.5), 1) ;
}
