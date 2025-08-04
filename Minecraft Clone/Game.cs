using Minecraft_Clone.Graphics;
using Minecraft_Clone.World.Chunks;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;
using System.Runtime.InteropServices;
using static Minecraft_Clone.Graphics.VBO;
using static Minecraft_Clone.Graphics.VertexUtils;
using ErrorCode = OpenTK.Graphics.OpenGL4.ErrorCode;

namespace Minecraft_Clone
{
    class Game : GameWindow
    {
        Camera camera;

        // world data
        int seed = 46;
        float noiseScale = 0.01f;

        //chunks work in this order
        ChunkManager chunkManager;
        SkyRender skyRender;

        // window-specific variables
        private int width;
        private int height;
        private double frameTimeAccumulator = 0.0;
        private int frameCount = 0;
        public float timeElapsed = 0;

        // Game Constructor not much to say
        public Game(int width, int height, string title) : base(GameWindowSettings.Default, new NativeWindowSettings()
        {
            ClientSize = (width, height),
            Title = title,
            API = ContextAPI.OpenGL,
            APIVersion = new Version(3, 3),
            DepthBits = 24,
        })
        {
            camera = new Camera(width, height, -1f * Vector3.UnitX + 4 * Vector3.UnitY);
            this.width = width;
            this.height = height;
            skyRender = new SkyRender((0f, 1f, -1f));
        }

        // first frame activities
        protected override void OnLoad()
        {
            base.OnLoad();
            GL.ClearColor(0.0f, 0.0f, 1.0f, 1.0f);

            //blockShader = new Shader("default.vert", "default.frag");
            //blockShader.Bind();
            //blockTexture = new Texture("textures.png");
            //blockTexture.Bind();

            // some clean-up stuff
            CursorState = CursorState.Grabbed;

            VSync = VSyncMode.On;
            GL.Enable(EnableCap.DepthTest);

            chunkManager = new ChunkManager();
            skyRender.InitializeSky();

            /*
            // allow water and stuff to be transparent
            GL.Enable(EnableCap.Blend);
            GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
            */
        }

        // render stuff for each frame 
        protected override void OnRenderFrame(FrameEventArgs args)
        {
            base.OnRenderFrame(args);
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

            // Render sky first
            skyRender.SetSunDirection(Vector3.Transform(skyRender.sunDirection, new Quaternion((float)args.Time / 5f, 0f, 0f)));
            skyRender.RenderSky(camera);

            chunkManager.Update(camera, timeElapsed);

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

        protected override void OnFramebufferResize(FramebufferResizeEventArgs e)
        {
            base.OnFramebufferResize(e);

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

            timeElapsed += (float)args.Time;

            // press escape to close this window or release mouse
            if (KeyboardState.IsKeyPressed(Keys.Escape))
            {
                if (CursorState == CursorState.Grabbed)
                    CursorState = CursorState.Normal;
                else
                    Close();
            }
            if (MouseState.IsButtonPressed(MouseButton.Left))
                CursorState = CursorState.Grabbed;
        }

        protected override void OnUnload()
        {
            base.OnUnload();
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