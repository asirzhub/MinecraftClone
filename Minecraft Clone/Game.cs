using Minecraft_Clone.Graphics;
using Minecraft_Clone.Rendering;
using Minecraft_Clone.World;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;
using static Minecraft_Clone.Graphics.VertexUtils;
using ErrorCode = OpenTK.Graphics.OpenGL4.ErrorCode;

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

        Camera camera;

        // window-specific variables
        private int width;
        private int height; 
        private double frameTimeAccumulator = 0.0;
        private int frameCount = 0;

        // world data
        int seed = 69420;
        float noiseScale = 0.02f;
        ChunkWorld world;
        private Task generationTask;
        private bool rebuildWorld = true;

        // Game Constructor not much to say
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

        // first frame activities
        protected override void OnLoad()
        {
            base.OnLoad();
            GL.ClearColor(0.0f, 0.0f, 1.0f, 1.0f);

            blockShader = new Shader("default.vert", "default.frag");
            blockShader.Bind();
            blockTexture = new Texture("textures.png");
            blockTexture.Bind();

            // some clean-up stuff
            camera = new Camera(width, height, -20f * Vector3.UnitX);
            CursorState = CursorState.Grabbed;

            VSync = VSyncMode.On;
            GL.Enable(EnableCap.DepthTest);

            var cts = new CancellationTokenSource();

            var progress = new Progress<float>(p =>
                Console.Title = $"Generating world… {p:P0}"
            );

            generationTask = world.GenerateWorldAsync(
                origin: new Vector3i(0, 0, 0),
                size: new Vector3i(12, 3, 12),
                seaLevel: 0,
                dirtThickness: 3,
                progress: progress,
                token: cts.Token
            ); // start the world gen, it'll be rendered later

            skyRender.InitSky();

            // allow water and stuff to be transparent
            GL.Enable(EnableCap.Blend);
            GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);

        }

        // render stuff for each frame 
        protected override void OnRenderFrame(FrameEventArgs args)
        {
            base.OnRenderFrame(args);
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

            Matrix4 model = Matrix4.Identity;
            Matrix4 view = camera.GetViewMatrix();
            Matrix4 projection = camera.GetProjectionMatrix();

            // Render sky first
            skyRender.RenderSky(camera);

            if(rebuildWorld && generationTask.IsCompleted)
            {
                Console.WriteLine("Uploading data to GPU, will freeze for a second.");
                rebuildWorld = false;
                BuildAndUploadAllChunks();
            }


            // Draw SOLID blocks
            if (solidIbo != null)
            {
                GL.DepthMask(true);             // enable depth write
                GL.Disable(EnableCap.Blend);    // disable blending

                blockShader.Bind();
                blockTexture.Bind();

                blockShader.SetMatrix4("model", model);
                blockShader.SetMatrix4("view", view);
                blockShader.SetMatrix4("projection", projection);

                solidVao.Bind();
                solidIbo.Bind();


                GL.DrawElements(
                    PrimitiveType.Triangles,
                    solidIbo.length,
                    DrawElementsType.UnsignedInt,
                    0
                );

                var error = GL.GetError();
                if (error != ErrorCode.NoError)
                    Console.Write($"Solid OpenGL Error: {error}");
            }

            // draw WATER blocks (transparent pass)
            if (waterIbo != null)
            {
                waterVao.Bind();
                waterIbo.Bind();

                GL.DepthMask(false);            // water wont write to depth buffer
                GL.Enable(EnableCap.Blend);     // enable alpha blending
                GL.BlendFunc(
                    BlendingFactor.SrcAlpha,
                    BlendingFactor.OneMinusSrcAlpha
                );

                blockShader.Bind();
                blockTexture.Bind();

                blockShader.SetMatrix4("model", model);
                blockShader.SetMatrix4("view", view);
                blockShader.SetMatrix4("projection", projection);

                GL.DrawElements(
                    PrimitiveType.Triangles,
                    waterIbo.length,
                    DrawElementsType.UnsignedInt,
                    0
                );
                var error = GL.GetError();
                if (error != ErrorCode.NoError)
                    Console.Write($"Water OpenGL Error: {error}");

                GL.Disable(EnableCap.Blend);
                GL.DepthMask(true);
            }
            
            SwapBuffers();

            // track fps
            frameTimeAccumulator += args.Time;
            frameCount++;

            if (frameTimeAccumulator >= 0.25)
            {
                Title = $"game - FPS: {frameCount * 4}" ;
                frameTimeAccumulator = 0.0;
                frameCount = 0;
            }
        }

        // TODO: async this
        void BuildAndUploadAllChunks()
        {
            Console.WriteLine("Sending CPU Buffer to GPU");

            var vDataList = new List<float>();
            var iDataList = new List<uint>();
            var waterVDataList = new List<float>();
            var waterIDataList = new List<uint>();

            uint baseVertex = 0, waterBaseVertex = 0;
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

            Console.WriteLine("Completed CPU Buffer to GPU");
        }

        // simplify vao/vbo/ibo into one function for a mesh
        public static (VAO vao, IBO ibo) UploadMesh(float[] vData, List<uint> indices)
        {
            VAO vao = new VAO();
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

        // logic, non-rendering frame-by-frame stuff
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

            if(solidVao != null)
                solidVao.Delete();
            if(solidIbo!=null)
                solidIbo.Delete();

            if (waterVao != null)
                waterVao.Delete();
            if (waterIbo != null)
                waterIbo.Delete();
            
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