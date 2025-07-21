using OpenTK.Mathematics;
using OpenTK.Graphics.OpenGL4;

namespace Minecraft_Clone.Graphics
{
    public class SkyRender
    {
        public int skyVAO;
        public int skyVBO;
        Shader skyShader;

        public SkyRender()
        {
            skyVAO = GL.GenVertexArray();
            skyVBO = GL.GenBuffer();
            skyShader = new Shader("../../../Shaders/sky.vert", "../../../Shaders/sky.frag");
        }

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

            loc = GL.GetUniformLocation(skyShader.ID, "cameraViewY"); // pass in the camera's y direction component 
            GL.Uniform1(loc, cameraViewY);

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
            skyShader.Delete(); // Assuming this method exists
        }
    }
}
