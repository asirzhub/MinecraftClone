using Minecraft_Clone.Graphics;
using Minecraft_Clone.Rendering;
using Minecraft_Clone.World;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;
using static Minecraft_Clone.Graphics.VertexUtils;

namespace Minecraft_Clone
{
    class Game : GameWindow
    {
        // Render Pipeline
        VAO solidVao;
        IBO solidIbo;
        VAO waterVao;
        IBO waterIbo;
        Shader blockShader;
        Texture blockTexture;
        SkyRender skyRender;

        int seed = 69420;
        float noiseScale = 0.02f;

        Camera camera;

        // window-centered variables
        private float time;
        private int width;
        private int height; 
        private double frameTimeAccumulator = 0.0;
        private int frameCount = 0;

        // world data
        ChunkWorld world;

        public Game(int width, int height, string title) : base(GameWindowSettings.Default, new NativeWindowSettings()
        {
            ClientSize = (width, height),
            Title = title,
            API = ContextAPI.OpenGL,
            APIVersion = new Version(3, 3),
            DepthBits = 24
        })
        {
            this.width = width;
            this.height = height;
            skyRender = new SkyRender();
            world = new ChunkWorld(seed, noiseScale);
        }

        protected override void OnLoad()
        {
            base.OnLoad();
            GL.ClearColor(0.0f, 0.0f, 1.0f, 1.0f);

            blockShader = new Shader("../../../Shaders/default.vert", "../../../Shaders/default.frag");
            blockShader.Bind();
            blockTexture = new Texture("textures.png");
            blockTexture.Bind();

            // some clean-up stuff
            time = 0f;
            camera = new Camera(width, height, -20f * Vector3.UnitX);
            CursorState = CursorState.Grabbed;

            VSync = VSyncMode.On;
            GL.Enable(EnableCap.DepthTest);

            const int floatsPerVertex = 3 + 2 + 3; // stride
            List<float> vDataList = new List<float>();
            List<uint> iDataList = new List<uint>();
            uint baseVertex = (uint)(vDataList.Count / floatsPerVertex);

            List<float> waterVDataList = new List<float>();
            List<uint> waterIDataList = new List<uint>();
            uint waterBaseVertex = 0;

            // actually generate the world
            world.GenerateWorldAbout((0, 1, 0), (8, 3, 8), 0, 3); // world.Chunks is now populated with terrain

            Console.WriteLine("Going to convert world chunks into vertices");

            foreach (var chunk in world.chunks)
            {
                ChunkMesher.GenerateMesh(
                    chunk.Value,
                    world,
                    out var verts,
                    out var indices,
                    out var waterVerts,
                    out var waterIndices
                );

                // SOLID
                var vertexList = FlattenVertices(verts);
                vDataList.AddRange(vertexList);
                for (int i = 0; i < indices.Count; i++)
                    iDataList.Add(indices[i] + baseVertex);
                baseVertex += (uint)verts.Count;

                // WATER
                var waterVertexList = FlattenVertices(waterVerts);
                waterVDataList.AddRange(waterVertexList);
                for (int i = 0; i < waterIndices.Count; i++)
                    waterIDataList.Add(waterIndices[i] + waterBaseVertex);
                waterBaseVertex += (uint)waterVerts.Count;
            }

            Console.WriteLine("Converted world chunks to vertices");

            // Optional: Convert to array if needed
            float[] finalVertexData = vDataList.ToArray();
            float[] finalWaterVertexData = waterVDataList.ToArray();

            blockShader.Bind();
            blockTexture.Bind();

            // Solid
            var result = UploadMesh(finalVertexData, iDataList);
            solidVao = result.vao;
            solidIbo = result.ibo;

            // Water
            var waterResult = UploadMesh(finalWaterVertexData, waterIDataList);
            waterVao = waterResult.vao;
            waterIbo = waterResult.ibo;

            solidVao.Bind();
            solidIbo.Bind();

            skyRender.InitSky();

            // allow water and stuff to be transparent
            GL.Enable(EnableCap.Blend);
            GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);

        }


        protected override void OnRenderFrame(FrameEventArgs args)
        {
            base.OnRenderFrame(args);
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
            time += (float)args.Time;

            Matrix4 model = Matrix4.Identity;
            Matrix4 view = camera.GetViewMatrix();
            Matrix4 projection = camera.GetProjectionMatrix();

            // Render sky first (infinite background)
            skyRender.RenderSky(camera);

            // Draw SOLID blocks
            blockShader.Bind();
            solidVao.Bind();
            solidIbo.Bind();

            int modelLocation = GL.GetUniformLocation(blockShader.ID, "model");
            int viewLocation = GL.GetUniformLocation(blockShader.ID, "view");
            int projectionLocation = GL.GetUniformLocation(blockShader.ID, "projection");

            GL.UniformMatrix4(modelLocation, true, ref model);
            GL.UniformMatrix4(viewLocation, true, ref view);
            GL.UniformMatrix4(projectionLocation, true, ref projection);

            GL.DepthMask(true);             // enable depth write
            GL.Disable(EnableCap.Blend);    // disable blending
            GL.DrawElements(
                PrimitiveType.Triangles,
                solidIbo.length,
                DrawElementsType.UnsignedInt,
                0
            );

            // draw WATER blocks (transparent pass)
            waterVao.Bind();
            waterIbo.Bind();

            GL.DepthMask(false);            // water wont write to depth buffer
            GL.Enable(EnableCap.Blend);     // enable alpha blending
            GL.BlendFunc(
                BlendingFactor.SrcAlpha,
                BlendingFactor.OneMinusSrcAlpha
            );

            GL.UniformMatrix4(modelLocation, true, ref model); // reuse same view/proj
            GL.UniformMatrix4(viewLocation, true, ref view);
            GL.UniformMatrix4(projectionLocation, true, ref projection);

            GL.DrawElements(
                PrimitiveType.Triangles,
                waterIbo.length,
                DrawElementsType.UnsignedInt,
                0
            );

            GL.Disable(EnableCap.Blend);
            GL.DepthMask(true);

            SwapBuffers();

            // track fps
            frameTimeAccumulator += args.Time;
            frameCount++;

            if (frameTimeAccumulator >= 0.25)
            {
                Title = $"game - FPS: {frameCount * 4}";
                frameTimeAccumulator = 0.0;
                frameCount = 0;
            }
        }


        public static (VAO vao, IBO ibo) UploadMesh(float[] vData, List<uint> indices)
        {
            // Create and bind VAO
            VAO vao = new VAO();

            // Create and bind VBO
            VBO vbo = new VBO(vData);

            // Link vertex attributes to VAO
            int stride = (3 + 2 + 3) * sizeof(float); // 8 floats per vertex

            vao.LinkToVAO(0, 3, vbo, stride, 0);                       // position
            vao.LinkToVAO(1, 2, vbo, stride, 3 * sizeof(float));       // texcoord
            vao.LinkToVAO(2, 3, vbo, stride, 5 * sizeof(float));       // normal

            // Create IBO
            IBO ibo = new IBO(indices);

            // Return both to bind during draw
            return (vao, ibo);
        }

        protected override void OnFramebufferResize(FramebufferResizeEventArgs e)
        {
            base.OnFramebufferResize(e);
            blockShader.Bind();

            width = e.Width;
            height = e.Height;

            GL.Viewport(0, 0, width, height);
            camera.UpdateResolution(width, height);
        }

        // called every frame. All updating happens here
        protected override void OnUpdateFrame(FrameEventArgs args)
        {
            MouseState mouse = MouseState;
            KeyboardState input = KeyboardState;

            base.OnUpdateFrame(args);
            camera.Update(input, mouse, args);

            // press escape to close this window or release mouse
            if (KeyboardState.IsKeyPressed(Keys.Escape))
            {
                if (CursorState == CursorState.Grabbed)
                {
                    CursorState = CursorState.Normal;
                }
                else
                {
                    Close();
                }
            }
            if (MouseState.IsButtonPressed(MouseButton.Left))
            {
                CursorState = CursorState.Grabbed;
            }
        }

        protected override void OnUnload()
        {
            base.OnUnload();

            solidVao.Delete();
            solidIbo.Delete();
            blockShader.Dispose();
            blockTexture.Delete();
            skyRender.Dispose();
        }
        
    }

    class Program
    {
        static void Main(string[] args)
        {
            var game = new Game(1280, 720, "game");
            game.Run();
        }
    }
}