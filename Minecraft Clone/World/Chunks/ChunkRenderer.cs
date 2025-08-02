using Minecraft_Clone.Graphics;
using System.Runtime.InteropServices;
using OpenTK.Mathematics;
using OpenTK.Graphics.OpenGL4;

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

        public ChunkRenderer()
        {
            blockShader = new Shader("PackedBlock.vert", "PackedBlock.frag");
            blockTexture = new Texture("textures.png");
            waterShader = new Shader("default.vert", "default.frag");
            waterTexture = new Texture("textures.png");
        }

        public void RenderChunk(MeshData mesh, Camera camera, Vector3i index)
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
