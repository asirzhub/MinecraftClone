using OpenTK.Mathematics;
using OpenTK.Graphics.OpenGL4;

namespace Minecraft_Clone.Graphics
{
    public class SkyRender
    {
        public int skyVAO;
        public int skyVBO;
        Shader skyShader;

        public Vector3 sunDirection;

        // <summary>
        /// A Triangle is rendered over the entire screen, coloured to look like the Sky. Stores its own vao/vbo.
        /// </summary>
        public SkyRender(Vector3 sunDirection)
        {
            skyVAO = GL.GenVertexArray();
            skyVBO = GL.GenBuffer();
            skyShader = new Shader("sky.vert", "sky.frag");

            this.sunDirection = sunDirection.Normalized();
        }

        // <summary>
        /// Initialize the sky vao and vbo, and binds them.
        /// </summary>
        public void InitSky()
        {
            float[] skyVerts = {
                -1f, -1f,
                3f, -1f,
                -1f,  3f
            };

            GL.BindVertexArray(skyVAO);
            GL.BindBuffer(BufferTarget.ArrayBuffer, skyVBO);
            GL.BufferData(BufferTarget.ArrayBuffer, skyVerts.Length * sizeof(float), skyVerts, BufferUsageHint.StaticDraw);

            GL.EnableVertexAttribArray(0);
            GL.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, 2 * sizeof(float), 0);
        }

        // <summary>
        // Renders the sky for the given camera
        // </summary>
        public void RenderSky(Camera camera)
        {
            skyShader.Bind();
            GL.Disable(EnableCap.DepthTest);

            skyShader.SetVector3("cameraRight", camera.right);
            skyShader.SetVector3("cameraUp", camera.up);
            skyShader.SetVector3("cameraForward", camera.front);
            skyShader.SetVector3("sunDir", sunDirection);
            skyShader.SetFloat("fovY", camera.fovY);
            skyShader.SetFloat("aspectRatio", camera.screenwidth / camera.screenheight);

            // Draw fullscreen triangle
            GL.BindVertexArray(skyVAO);
            GL.DrawArrays(PrimitiveType.Triangles, 0, 3);

            GL.Enable(EnableCap.DepthTest); // Re-enable for world rendering
            skyShader.UnBind();
            GL.BindVertexArray(0);
        }

        public void SetSunDirection(Vector3 direction)
        {
            this.sunDirection = direction.Normalized();
        }

        public void Dispose()
        {
            GL.DeleteVertexArray(skyVAO);
            GL.DeleteBuffer(skyVBO);
            skyShader.Delete(); 
        }
    }
}
