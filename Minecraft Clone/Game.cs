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
        VAO vao;
        IBO ibo;
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

            List<float> vDataList = new List<float>();
            List<uint> iDataList = new List<uint>();

            Console.WriteLine("Going to convert world chunks into vertices");

            world.GenerateWorldAbout((0, -1, 0), (8, 3, 8), -10, 3);

            const int floatsPerVertex = 3 + 2 + 3; // your stride
            uint baseVertex = (uint)(vDataList.Count / floatsPerVertex);
            foreach (var chunk in world.chunks)
            {
                ChunkMesher.GenerateMesh(chunk.Value, out var verts, out List<uint> indices);
                var vertexList = FlattenVertices(verts);
                // Append vertex data
                vDataList.AddRange(vertexList);

                // Offset indices by how many verts we've already got
                for (int i = 0; i < indices.Count; i++)
                    iDataList.Add(indices[i] + baseVertex);

                // Advance baseVertex for the next chunk
                baseVertex += (uint)verts.Count;
            }

            Console.WriteLine("Converted world chunks to vertices");

            // Optional: Convert to array if needed
            float[] finalVertexData = vDataList.ToArray();

            blockShader.Bind();
            blockTexture.Bind();   
            
            var result = UploadMesh(finalVertexData, iDataList); // need a magic function...
            vao = result.vao;
            ibo = result.ibo;

            vao.Bind();
            ibo.Bind();

            skyRender.InitSky();
        }

        
        protected override void OnRenderFrame(FrameEventArgs args)
        {
            base.OnRenderFrame(args);
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
            time += (float)args.Time;

            // placeholder values?
            Matrix4 model = Matrix4.Identity;
            Matrix4 view = camera.GetViewMatrix();
            Matrix4 projection = camera.GetProjectionMatrix();

            // draw the sky
            skyRender.RenderSky(camera);

            // draw the chunk

            blockShader.Bind();
            vao.Bind();
            ibo.Bind();

            int modelLocation = GL.GetUniformLocation(blockShader.ID, "model");
            int viewLocation = GL.GetUniformLocation(blockShader.ID, "view");
            int projectionLocation = GL.GetUniformLocation(blockShader.ID, "projection");

            GL.UniformMatrix4(modelLocation, true, ref model);
            GL.UniformMatrix4(viewLocation, true, ref view);
            GL.UniformMatrix4(projectionLocation, true, ref projection);

            GL.DrawElements(
                PrimitiveType.Triangles,
                ibo.length,
                DrawElementsType.UnsignedInt,
                0
            );

            SwapBuffers();

            frameTimeAccumulator += args.Time;
            frameCount++;

            if (frameTimeAccumulator >= 0.25)
            {
                Title = $"game - FPS: {frameCount*4}";
                frameTimeAccumulator = 0.0;
                frameCount = 0;
            }

            // press escape to close this window or release mouse
            if (KeyboardState.IsKeyPressed(Keys.Escape))
            {
                if(CursorState == CursorState.Grabbed)
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
                CursorState = CursorState.Normal;
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
        }

        protected override void OnUnload()
        {
            base.OnUnload();

            vao.Delete();
            ibo.Delete();
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