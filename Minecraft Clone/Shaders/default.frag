#version 330 core

in vec2 texCoord;
in vec3 normal;
in float brightness;

uniform sampler2D texture0;

void main()
{
    vec4 texColor = texture(texture0, texCoord);
    
    // Optional: discard fully transparent pixels
    if (texColor.a < 0.1)
        discard;

    gl_FragColor = texColor * brightness;
}
