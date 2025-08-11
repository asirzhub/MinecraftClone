using Minecraft_Clone.Graphics;
using Minecraft_Clone.World.Chunks;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;

namespace Minecraft_Clone
{
    class Game : GameWindow
    {
        Camera camera;

        // world data
        int seed = 46;
        float noiseScale = 0.01f;

        //chunks work in this order
        public ChunkManager chunkManager;
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
            skyRender = new SkyRender((0.4f, 1f, -1f));
        }

        // first frame activities
        float[] xs = new float[100];
        float[] ys = new float[100];
        protected override void OnLoad()
        {
            base.OnLoad();
            GL.ClearColor(0.0f, 0.0f, 1.0f, 1.0f);


            // some clean-up stuff
            CursorState = CursorState.Grabbed;

            //VSync = VSyncMode.On;
            GL.Enable(EnableCap.DepthTest);
            GL.Enable(EnableCap.CullFace);
            GL.Enable(EnableCap.Blend);
            GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);

            chunkManager = new ChunkManager();

            skyRender.InitializeSky();
        }

        // render stuff for each frame 
        protected override async void OnRenderFrame(FrameEventArgs args)
        {
            base.OnRenderFrame(args);
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

            // Render sky first
            skyRender.SetSunDirection(Vector3.Transform(skyRender.sunDirection, new Quaternion((float)args.Time / 15f, 0f, 0f)));
            skyRender.RenderSky(camera);

            //await chunkManager.UpdateAsync(camera, timeElapsed, skyRender.sunDirection.Normalized());

            chunkManager.Update(camera, timeElapsed, skyRender.sunDirection.Normalized());

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