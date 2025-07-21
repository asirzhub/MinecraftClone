#version 330 core

in vec2 screenUV;
out vec4 FragColor;

uniform float cameraViewY;

void main()
{
    float t = clamp(screenUV.y/2.0 + cameraViewY/2.0 + 0.25, 0.0, 1.0);

    vec3 color;

    if (t < 0.5)
    {
        // Black to white
        color = mix(vec3(0.0), vec3(1.0), t*2.0);
    }
    else
    {
        // White to blue
        color = mix(vec3(1.0), vec3(0.5, 0.7, 1.0), (t - 0.5)*4.0);
    }

    FragColor = vec4(color, 1.0); 
}