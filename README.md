# MinecraftClone
Literally just making a clone of Minecraft. Using C# and OpenTK (A wrapper for OpenGl). This is because I'm still learning C++, otherwise I'd use OpenGL directly. I plan on remaking this engine in C++ in the near future, once I'm confident with C++. 
## Development Roadmap
1. ~~Base-layer absolute minimum engine with chunk generation, camera, and rendering of chunks.~~
2. ~~Improve chunk meshing to massively increase performance~~
3. Dynamic loading and unloading of chunks
4. Improve terrain generation to make it more interesting/realistic
5. Async world generation
6. Async chunk loading
7. ~~Separate solid blocks vs water/transparent render passes~~
8. Vertex-based baked lighting (ambient occlusion)
9. Frustrum culling
10. Surface features (trees, grass, flowers)
11. Occlusion culling?? Is this possible?
12. Shadow mapping
13. Post processing (tonemapping, bloom, SSR)

## Lessons I've learned
### Separate data, logic, and rendering into unique scripts
And use a manager to control the data flow between these three aspects of whatever game feature. For example, re-writing my meshing and dynamically generating chunks had me stuck for a whole week. The biggest mistake that I finally stopped making: The manager does not hold ANY information - It knows nothing in specific about whatever it's managing, it just tells each part what it should do, when it should do it, and carts information from one part to another. Additionally, data has no idea what the logic is or what the rendering is - it's just information, nothing else. Logic is given information by the data, and it does what it's told to, to prepare the data for rendering. But logic doesn't know anything about rendering either. And rendering, all it knows is what vertices to mesh and render - it doesn't know why it's rendering what it is, it just knows what to do once given vertices.
