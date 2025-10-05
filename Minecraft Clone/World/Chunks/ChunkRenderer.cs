using Minecraft_Clone.Graphics;
using OpenTK.Mathematics;
using OpenTK.Graphics.OpenGL4;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace Minecraft_Clone.World.Chunks
{
    // renderer handles rendering side of things for chunks
    public class ChunkRenderer
    {
        public Texture blockTexture;
        public Shader blockShader;
        public int blockTextureUnit => blockTexture.ID;

        // some parameters
        public float waterOffset = 0.15f; // water surface offset from top edge of blocks
        public float waterWaveAmplitude = 0.1f; // how much water (and foliage...) deviates from origin
        public float waterWaveScale = 0.1f; // world-size scale of sine waves
        public float waterWaveSpeed = 0.5f; // speed at which oscillations travel

        List<ChunkState> lightingPassVisibleStates = new List<ChunkState>() { ChunkState.VISIBLE, ChunkState.MESHING, ChunkState.MESHED, ChunkState.DIRTY };
        List<ChunkState> shadowMapPassVisibleStates = new List<ChunkState>() { ChunkState.INVISIBLE, ChunkState.VISIBLE, ChunkState.MESHING, ChunkState.MESHED, ChunkState.DIRTY };

        public Shader shadowMapShader;
        FBOShadowMap shadowMapFBO;
        int shadowMapResolution = 1024;
        int shadowMapUnit = 10;

        public ChunkRenderer()
        {
            blockShader = new Shader("PackedBlock.vert", "PackedBlock.frag");
            blockTexture = new Texture("textures.png");

            shadowMapFBO = new(shadowMapResolution, shadowMapResolution);
            shadowMapShader = new Shader("ShadowMap.vert", "EmptyFragment.frag");
        }

        public void RenderLightingPass(Camera camera, float time, ConcurrentDictionary<Vector3i, Chunk> chunks, SkyRender skyRender)
        {
            //stopWatch.Restart();
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
            GL.Viewport(0, 0, (int)camera.screenwidth, (int)camera.screenheight);
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
            skyRender.RenderSky(camera); // i didnt want to put this in chunk manager but seems the only way to do it after shadow pass and before chunk lighting pass

            blockShader.Bind();
            blockTexture.Bind();

            GL.ActiveTexture(TextureUnit.Texture0 + shadowMapUnit);
            GL.BindTexture(TextureTarget.Texture2D, shadowMapFBO.depthTexture);

            List<Vector3i> visibleIndexes = new List<Vector3i>();

            // render all chunks non-transparent mesh
            foreach (var kvp in chunks)
            {
                var chunk = kvp.Value;
                var idx = kvp.Key;
                if (lightingPassVisibleStates.Contains(chunk.GetState()))
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
            //Console.WriteLine($"Lighting pass took: {stopWatch.ElapsedMilliseconds} ms");
        }

        bool RenderChunkLighting(MeshData mesh, Camera camera, Vector3i index, float time, SkyRender skyRender)
        {
            // exit if there's no mesh data
            if (mesh == null || mesh.Vertices.Count == 0) return false;

            mesh.Upload();

            var sunDirection = skyRender.sunDirection;

            //with everything prepped, we can now render
            Matrix4 model = Matrix4.CreateTranslation(index * (Chunk.SIZE));
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
            blockShader.SetVector3("sunsetColor", new(0.0f, 0.0f, 0.0f));
            blockShader.SetVector3("fogColor", skyRender.finalH);
            blockShader.SetTexture2DUnit("shadowMap", shadowMapUnit);

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

        Stopwatch stopWatch = new();

        public void RenderShadowMapPass(Camera camera, float time, ConcurrentDictionary<Vector3i, Chunk> chunks, SkyRender skyRender)
        {
            //stopWatch.Restart();
            shadowMapFBO.Bind();
            GL.Viewport(0, 0, shadowMapResolution, shadowMapResolution);
            GL.Clear(ClearBufferMask.DepthBufferBit);
            shadowMapShader.Bind();

            List<Vector3i> visibleIndexes = new List<Vector3i>();

            // render all chunks non-transparent mesh
            foreach(var kvp in chunks)
            {
                var chunk = kvp.Value;
                var idx = kvp.Key;
                if(shadowMapPassVisibleStates.Contains(chunk.GetState()))
                {
                    RenderChunkShadowMap(chunk.solidMesh, camera, idx, time, skyRender);
                    visibleIndexes.Add(idx);
                }
            }

            // render water with no depth mask, after all solids were rendered
            GL.DepthMask(false);
            foreach (var idx in visibleIndexes)
            {
                RenderChunkShadowMap(chunks[idx].liquidMesh, camera, idx, time, skyRender);
            }
            GL.DepthMask(true);

            shadowMapFBO.UnBind();
            //Console.WriteLine($"Shadow pass took: {stopWatch.ElapsedMilliseconds} ms");
        }

        bool RenderChunkShadowMap(MeshData mesh, Camera camera, Vector3i index, float time, SkyRender skyRender)
        {
            // exit if there's no mesh data
            if (mesh == null || mesh.Vertices.Count == 0) return false;

            mesh.Upload();

            var sunDirection = skyRender.sunDirection;

            //with everything prepped, we can now render
            Matrix4 model = Matrix4.CreateTranslation(index*(Chunk.SIZE));

            Matrix4 view = Matrix4.LookAt(camera.position + sunDirection, camera.position, Vector3.UnitY);

            Matrix4 projection = Matrix4.CreateOrthographic(10, 10, 0, 1000f);

            shadowMapShader.SetMatrix4("model", model);
            shadowMapShader.SetMatrix4("view", view);
            shadowMapShader.SetMatrix4("projection", projection);
            shadowMapShader.SetFloat("u_waterOffset", waterOffset);
            shadowMapShader.SetFloat("u_waveAmplitude", waterWaveAmplitude);
            shadowMapShader.SetFloat("u_waveScale", waterWaveScale);
            shadowMapShader.SetFloat("u_time", time);
            shadowMapShader.SetFloat("u_waveSpeed", waterWaveSpeed);
            shadowMapShader.SetVector3("sunDirection", sunDirection);

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
