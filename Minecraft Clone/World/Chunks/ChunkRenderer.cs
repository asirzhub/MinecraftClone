using Minecraft_Clone.Graphics;
using OpenTK.Mathematics;
using OpenTK.Graphics.OpenGL4;
using System.Collections.Concurrent;

namespace Minecraft_Clone.World.Chunks
{
    // renderer handles rendering side of things for chunks
    public class ChunkRenderer
    {
        public Texture blockTexture;
        public Shader blockShader;

        // some parameters
        public float waterOffset = 0.15f; // water surface offset from top edge of blocks
        public float waterWaveAmplitude = 0.1f; // how much water (and foliage...) deviates from origin
        public float waterWaveScale = 0.1f; // world-size scale of sine waves
        public float waterWaveSpeed = 0.5f; // speed at which oscillations travel

        List<ChunkState> lightingPassVisibleStates = new List<ChunkState>() { ChunkState.VISIBLE, ChunkState.MESHING, ChunkState.MESHED, ChunkState.DIRTY };
        List<ChunkState> shadowMapPassVisibleStates = new List<ChunkState>() { ChunkState.INVISIBLE, ChunkState.VISIBLE, ChunkState.MESHING, ChunkState.MESHED, ChunkState.DIRTY };

        public ChunkRenderer()
        {
            blockShader = new Shader("PackedBlock.vert", "PackedBlock.frag");
            blockTexture = new Texture("textures.png");
        }

        public void Bind()
        {
            blockShader.Bind();
            blockTexture.Bind();
        }

        public void RenderLightingPass(Camera camera, float time, ConcurrentDictionary<Vector3i, Chunk> chunks, SkyRender skyRender)
        {
            List<Vector3i> visibleIndexes = new List<Vector3i>();

            // render all chunks non-transparent mesh
            foreach(var kvp in chunks)
            {
                var chunk = kvp.Value;
                var idx = kvp.Key;
                if(lightingPassVisibleStates.Contains(chunk.GetState()))
                {
                    RenderChunkLighting(chunk.solidMesh, camera, idx, time, skyRender);
                    visibleIndexes.Add(idx);
                }
            }

            // render water with no depth mask, after all solids were rendered
            GL.DepthMask(false);
            foreach (var idx in visibleIndexes)
            {
                RenderChunkLighting(chunks[idx].liquidMesh, camera, idx, time, skyRender);
            }
            GL.DepthMask(true);
        }

        public bool RenderChunkLighting(MeshData mesh, Camera camera, Vector3i index, float time, SkyRender sky)
        {
            // exit if there's no mesh data
            if (mesh == null || mesh.Vertices.Count == 0) return false;

            mesh.Upload();

            var sunDirection = sky.sunDirection;

            //with everything prepped, we can now render
            Matrix4 model = Matrix4.CreateTranslation(index*(Chunk.SIZE));
            Matrix4 view = camera.GetViewMatrix();
            Matrix4 projection = camera.GetProjectionMatrix();

            blockShader.SetMatrix4("model", model);
            blockShader.SetMatrix4("view", view);
            blockShader.SetMatrix4("projection", projection);
            blockShader.SetVector3("cameraPos", camera.position);
            blockShader.SetFloat("u_waterOffset", waterOffset);
            blockShader.SetFloat("u_waveAmplitude", waterWaveAmplitude);
            blockShader.SetFloat("u_waveScale", waterWaveScale);
            blockShader.SetFloat("u_time", time);
            blockShader.SetFloat("u_waveSpeed", waterWaveSpeed);
            blockShader.SetVector3("sunDirection", sunDirection);
            blockShader.SetVector3("ambientColor", new(1.0f, 1.1f, 1.3f)); 
            blockShader.SetVector3("sunsetColor", new(0.1f, 0.0f, 0.0f)); 
            blockShader.SetVector3("fogColor", sky.finalH);

            mesh.vao.Bind();
            mesh.vbo.Bind();
            mesh.ibo.Bind();

            GL.DrawElements(
            PrimitiveType.Triangles,
            mesh.ibo.length,
            DrawElementsType.UnsignedInt,
            0
            );

            return true;
        }
    }
}
