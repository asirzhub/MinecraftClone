using Minecraft_Clone.Graphics;
using System.Runtime.InteropServices;
using OpenTK.Mathematics;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Windowing.Common;

namespace Minecraft_Clone.World.Chunks
{
    public class ChunkRenderer
    {
        public int blockVBO;
        public int blockVAO;
        public int blockIBO;
        public Texture blockTexture;
        public Shader blockShader;

        public int waterVBO;
        public int waterVAO;
        public int waterIBO;
        public Texture waterTexture;
        public Shader waterShader;

        public float waterOffset = 0.15f;
        public float waterWaveAmplitude = 0.05f;
        public float waterWaveScale = 0.1f;
        public float waterWaveSpeed = 0.3f;

        public ChunkRenderer()
        {
            blockShader = new Shader("PackedBlock.vert", "PackedBlock.frag");
            blockTexture = new Texture("textures.png");
            waterShader = new Shader("default.vert", "default.frag");
            waterTexture = new Texture("textures.png");
        }

        public void RenderChunk(MeshData mesh, Camera camera, Vector3i index, float time, Vector3 sunDirection)
        {
            blockShader.Bind();
            blockTexture.Bind();

            mesh.Upload();

            //with everything prepped, we can now render
            Matrix4 model = Matrix4.CreateTranslation(index*(Chunk.SIZE));
            Matrix4 view = camera.GetViewMatrix();
            Matrix4 projection = camera.GetProjectionMatrix();

            blockShader.SetMatrix4("model", model);
            blockShader.SetMatrix4("view", view);
            blockShader.SetMatrix4("projection", projection);

            blockShader.SetFloat("u_waterOffset", waterOffset);
            blockShader.SetFloat("u_waveAmplitude", waterWaveAmplitude);
            blockShader.SetFloat("u_waveScale", waterWaveScale);
            blockShader.SetFloat("u_time", time);
            blockShader.SetFloat("u_waveSpeed", waterWaveSpeed);
            blockShader.SetVector3("sunDirection", sunDirection);
            blockShader.SetVector3("ambientColor", new(1.1f, 1.2f, 1.3f));
            blockShader.SetVector3("sunsetColor", new(0.7f, 0.2f, 0.3f));

            mesh.vao.Bind();
            mesh.vbo.Bind();
            mesh.ibo.Bind();

            GL.DrawElements(
            PrimitiveType.Triangles,
            mesh.ibo.length,
            DrawElementsType.UnsignedInt,
            0
            );

        }

    }
}
