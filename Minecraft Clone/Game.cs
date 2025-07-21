using Minecraft_Clone.Graphics;
using Minecraft_Clone.Rendering;
using Minecraft_Clone.World;
using OpenTK.Compute.OpenCL;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;
using System.Diagnostics;
using static Minecraft_Clone.Graphics.VBO;
using static Minecraft_Clone.Graphics.VertexUtils;

namespace Minecraft_Clone
{
    class Game : GameWindow
    {
        //paths
        string vertexPath = "";
        string fragmentPath = "";

        // Render Pipeline
        VAO vao;
        IBO ibo;
        Shader blockShader;
        Texture blockTexture;

        int skyVAO;
        int skyVBO;
        Shader skyShader;

        Camera camera;

        // window-centered variables
        private float time;
        private int width;
        private int height; 
        private double frameTimeAccumulator = 0.0;
        private int frameCount = 0;

        // temporary world data
        Chunk chunk = new Chunk(0, 0, 0);
        float noiseScale = 0.1f;

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
        }

        protected override void OnLoad()
        {
            base.OnLoad();
            GL.ClearColor(0.0f, 0.0f, 1.0f, 1.0f);

            //for (int i = 0; i < chunk.blocks.Length; i++)
            //{
            //    chunk.blocks[i] = new Block(BlockType.DIRT);
            //}

            PerlinNoise noise = new PerlinNoise();
            Random rnd = new Random();

            for(int x = 0; x < Chunk.CHUNKSIZE; x++)
            {
                for (int z = 0; z < Chunk.CHUNKSIZE; z++)
                {
                    float height = noise.Noise(x*noiseScale, z*noiseScale) * Chunk.CHUNKSIZE;
                    for (int y = 0; y < Chunk.CHUNKSIZE; y++)
                    {
                        if (y < height - 4)
                        {
                            chunk.SetBlock(x, y, z, BlockType.STONE);
                        }
                        else if (y < height-1)
                        {
                            chunk.SetBlock(x, y, z, BlockType.DIRT);
                        }
                        else if (y < height)
                        {
                            chunk.SetBlock(x, y, z, BlockType.GRASS);
                        }
                        else chunk.SetBlock(x, y, z, BlockType.AIR);
                    }
                }
            }

            blockShader = new Shader("../../../Shaders/default.vert", "../../../Shaders/default.frag");
            blockShader.Bind();
            blockTexture = new Texture("textures.png");
            blockTexture.Bind();

            // some clean-up stuff
            time = 0f;
            camera = new Camera(width, height, -2f * Vector3.UnitX);
            CursorState = CursorState.Grabbed;

            VSync = VSyncMode.On;
            GL.Enable(EnableCap.DepthTest);

            ChunkMesher.GenerateMesh(chunk, out var verts, out List<uint> indices);
            float[] vData = FlattenVertices(verts);

            blockShader.Bind();
            blockTexture.Bind();   
            
            var result = UploadMesh(vData, indices); // need a magic function...
            vao = result.vao;
            ibo = result.ibo;

            vao.Bind();
            ibo.Bind();

            InitSky();
        }

        public void InitSky()
        {
            skyVAO = GL.GenVertexArray();
            skyVBO = GL.GenBuffer();
            skyShader = new Shader("../../../Shaders/sky.vert", "../../../Shaders/sky.frag");

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

        public void RenderSky()
        {
            skyShader.Bind();
            GL.Disable(EnableCap.DepthTest);

            // Compute inverse view-projection matrix
            Matrix4 inverseVP = Matrix4.Invert(camera.GetProjectionMatrix() * camera.GetViewMatrix());
            int loc = GL.GetUniformLocation(skyShader.ID, "inverseVP");
            GL.UniformMatrix4(loc, true, ref inverseVP);


            Matrix4 view = camera.GetViewMatrix();
            Vector3 forward = new Vector3(-view.M13, -view.M23, -view.M33); // or from your own camera.forward
            float cameraViewY = forward.Y; // y-component of camera's look direction

            loc = GL.GetUniformLocation(skyShader.ID, "cameraViewY");
            GL.Uniform1(loc, cameraViewY);


            // Draw fullscreen triangle
            GL.BindVertexArray(skyVAO);
            GL.DrawArrays(PrimitiveType.Triangles, 0, 3);

            GL.Enable(EnableCap.DepthTest); // Re-enable for world rendering
            skyShader.UnBind();
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
            RenderSky();

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
        }
        
    }

    

    class Program
    {
        static void Main(string[] args)
        {
            var game = new Game(800, 450, "game");
            game.Run();
        }
    }
}