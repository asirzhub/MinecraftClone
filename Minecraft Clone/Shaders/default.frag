#version 330 core

in vec2 texCoord;
in vec3 vNormal;
in float brightness;

uniform sampler2D texture0;
uniform float u_brightnessAdjust;

vec3 sunColor;

out vec4 FragColor;

void main()
{
    vec4 texColor = texture(texture0, texCoord);

    // Optional: discard fully transparent pixels
    if (texColor.a < 0.1)
        discard;

    FragColor = texColor * (brightness + clamp(u_brightnessAdjust, -0.8, 1));
}
