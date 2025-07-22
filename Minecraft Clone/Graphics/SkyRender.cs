using OpenTK.Mathematics;
using OpenTK.Graphics.OpenGL4;

namespace Minecraft_Clone.Graphics
{
    public class SkyRender
    {
        public int skyVAO;
        public int skyVBO;
        Shader skyShader;

        // <summary>
        /// A Triangle is rendered over the entire screen, coloured to look like the Sky. Stores its own vao/vbo.
        /// </summary>
        public SkyRender()
        {
            skyVAO = GL.GenVertexArray();
            skyVBO = GL.GenBuffer();
            skyShader = new Shader("sky.vert", "sky.frag");
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
        /// Binds the correct shader and renders the sky based on the direction the camera faces. TODO: Optimize/simplify.
        /// </summary>
        public void RenderSky(Camera camera)
        {
            skyShader.Bind();
            GL.Disable(EnableCap.DepthTest);

            // Compute inverse view-projection matrix
            Matrix4 inverseVP = Matrix4.Invert(camera.GetProjectionMatrix() * camera.GetViewMatrix());
            int loc = GL.GetUniformLocation(skyShader.ID, "inverseVP");
            GL.UniformMatrix4(loc, true, ref inverseVP);

            Matrix4 view = camera.GetViewMatrix();
            Vector3 forward = new Vector3(-view.M13, -view.M23, -view.M33);
            float cameraViewY = forward.Y; // y-component of camera's look direction

            skyShader.SetFloat("cameraViewY", cameraViewY);

            // Draw fullscreen triangle
            GL.BindVertexArray(skyVAO);
            GL.DrawArrays(PrimitiveType.Triangles, 0, 3);

            GL.Enable(EnableCap.DepthTest); // Re-enable for world rendering
            skyShader.UnBind();
            GL.BindVertexArray(0);
        }

        public void Dispose()
        {
            GL.DeleteVertexArray(skyVAO);
            GL.DeleteBuffer(skyVBO);
            skyShader.Delete(); 
        }
    }
}
