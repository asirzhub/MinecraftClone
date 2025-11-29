using OpenTK.Mathematics;
using OpenTK.Graphics.OpenGL4;

namespace Minecraft_Clone.Graphics
{
    /// A Triangle is rendered over the entire screen, coloured to look like the Sky. Stores its own vao/vbo.
    /// </summary>
    public class SkyRender(Vector3 sunDirection)
    {
        public int skyVAO = GL.GenVertexArray();
        public int skyVBO = GL.GenBuffer();
        readonly Shader skyShader = new("sky.vert", "sky.frag");

        public Vector3 sunDirection = sunDirection.Normalized();

        Vector3 dayHorizon = new(0.50f, 0.70f, 1.00f);
        Vector3 dayZenith = new(0.30f, 0.50f, 1.00f);
        Vector3 nightHorizon = new(0.05f, 0.10f, 0.20f);
        Vector3 nightZenith = new(0.02f, 0.05f, 0.10f);

        public Vector3 finalH = new();
        public Vector3 finalZ = new();

        // <summary>
        /// Initialize the sky vao and vbo, and binds them.
        /// </summary>
        public void InitializeSky()
        {
            float[] skyVerts = [
                -1f, -1f,
                3f, -1f,
                -1f,  3f
            ];

            GL.BindVertexArray(skyVAO);
            GL.BindBuffer(BufferTarget.ArrayBuffer, skyVBO);
            GL.BufferData(BufferTarget.ArrayBuffer, skyVerts.Length * sizeof(float), skyVerts, BufferUsageHint.StaticDraw);

            GL.EnableVertexAttribArray(0);
            GL.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, 2 * sizeof(float), 0);
        }

        // <summary>
        // Renders the sky for the given camera
        // </summary>
        public void RenderSky(AerialCameraRig camera)
        {
            skyShader.Bind();
            GL.Disable(EnableCap.DepthTest);

            float dayness = MathF.Cos(Vector3.CalculateAngle(Vector3.UnitY, sunDirection));

            float t = 1;

            if (dayness > 0f) t = 1 - MathF.Pow(dayness, 0.5f);

                float sunset = 0f;
            if(t > 0.5f)
                sunset = 2 - 2 * t;
            else if (t <= 0.5f)
                sunset = 2 * t;

            finalH = Vector3.Lerp(dayHorizon, nightHorizon, t);
            finalZ = Vector3.Lerp(dayZenith, nightZenith, t);

            finalH += sunset * new Vector3(0.3f, 0.0f, 0.2f);
            finalZ += sunset * new Vector3(0.2f, 0.0f, 0.1f);

            // Send to GPU
            skyShader.SetVector3("horizonColor", finalH);
            skyShader.SetVector3("zenithColor", finalZ);

            skyShader.SetVector3("cameraRight", camera.right);
            skyShader.SetVector3("cameraUp", camera.up);
            skyShader.SetVector3("cameraForward", camera.forward);
            skyShader.SetVector3("sunDir", sunDirection);
            skyShader.SetFloat("fovY", camera.fovY);
            skyShader.SetFloat("aspectRatio", camera.screenWidth / camera.screenHeight);

            skyShader.SetVector3("sunColor", new Vector3(1.0f, 0.9f, 0.7f));
            skyShader.SetFloat("sunAngularRadiusDeg", 0.57f);
            skyShader.SetFloat("sunEdgeSoftness", 0.0005f);
            skyShader.SetFloat("sunGlowStrength", 1.1f);
            skyShader.SetFloat("sunGlowSharpness", 1000.0f);

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
