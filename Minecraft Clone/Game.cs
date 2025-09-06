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
        public bool focused = true;

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
            camera = new Camera(width, height, -1f * Vector3.UnitX + 64 * Vector3.UnitY);
            this.width = width;
            this.height = height;
            skyRender = new SkyRender((0.4f, 1f, -1f));
        }

        protected override void OnLoad()
        {
            base.OnLoad();
            GL.ClearColor(0.0f, 0.0f, 1.0f, 1.0f);

            CursorState = CursorState.Grabbed;

            VSync = VSyncMode.Adaptive;
            GL.Enable(EnableCap.DepthTest);
            GL.Enable(EnableCap.CullFace);
            GL.Enable(EnableCap.Blend);
            GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
            GL.Enable(EnableCap.Multisample);

            chunkManager = new ChunkManager();

            skyRender.InitializeSky();
        }

        // render stuff for each frame 
        protected override void OnRenderFrame(FrameEventArgs args)
        {
            base.OnRenderFrame(args);
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

            // Render sky first
            skyRender.SetSunDirection(Vector3.Transform(skyRender.sunDirection, new Quaternion((float)args.Time / 30f, 0f, 0f)));
            skyRender.RenderSky(camera);

            chunkManager.Update(camera, (float)args.Time, timeElapsed, skyRender.sunDirection.Normalized());

            SwapBuffers();

            // track fps
            frameTimeAccumulator += args.Time;
            frameCount++;

            if (frameTimeAccumulator >= 0.5)
            {
                Title = $"game - FPS: {frameCount * 2} | " +
                    $"Position: {camera.position} | " +
                    $"Chunk: {chunkManager.currentChunkIndex} | " +
                    $"Chunk Tasks: {chunkManager.taskCount}/{chunkManager.maxChunkTasks} | " +
                    $"Render Calls: {chunkManager.totalRenderCalls}";
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

            if(focused)
                camera.Update(input, mouse, args);

            timeElapsed += (float)args.Time;

            // press escape to close this window or release mouse
            if (KeyboardState.IsKeyPressed(Keys.Escape))
            {
                if (CursorState == CursorState.Grabbed)
                {
                    CursorState = CursorState.Normal;
                    focused = false;
                    camera.firstMove = false;
                }
                else
                    Close();
            }
            if (MouseState.IsButtonPressed(MouseButton.Left))
            {
                CursorState = CursorState.Grabbed;
                focused = true;
                camera.firstMove = true;
            }

            if (KeyboardState.IsKeyPressed(Keys.Period))
            {
                chunkManager.radius++;
            }

            if (KeyboardState.IsKeyPressed(Keys.Comma))
            {
                chunkManager.radius--;
            }
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
            Game G = new Game(1280, 720, "game");
            G.Run();
        }
    }
}