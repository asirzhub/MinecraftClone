using Minecraft_Clone.Graphics;
using Minecraft_Clone.World.Chunks;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;
using System;

namespace Minecraft_Clone
{
    class Game : GameWindow
    {
        AerialCameraRig aerialCamera;
        public bool focused = true;

        public ChunkManager chunkManager;
        SkyRender skyRender;

        // window-specific variables
        private int width;
        private int height;
        private double frameTimeAccumulator = 0.0;
        private int shortFrameCount = 0;
        private int totalFrameCount = 0;
        public float timeElapsed = 0;

        float timeMult = 0.03f;

        MeshData selectionBox;

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
            aerialCamera = new AerialCameraRig(width, height, (0f,0f,0f));
            this.width = width;
            this.height = height;
            skyRender = new SkyRender((1.0f, 1f, -1f));

            selectionBox = new();
            foreach (var face in CubeMesh.PackedFaceVertices) {
                foreach (var v in face.Value) {
                    selectionBox.AddVertex(v);
                }
            }
        }

        protected override void OnLoad()
        {
            base.OnLoad();
            GL.ClearColor(0.0f, 0.0f, 1.0f, 1.0f);

            CursorState = CursorState.Grabbed;

            //VSync = VSyncMode.On; // only needed when i dont want my laptop to turn into a jet engine at the library
            GL.Enable(EnableCap.DepthTest);
            GL.Enable(EnableCap.Blend);
            GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
            GL.Enable(EnableCap.Multisample);

            chunkManager = new ChunkManager(aerialCamera);

            skyRender.InitializeSky();
        }

        // render stuff for each frame 
        protected override void OnRenderFrame(FrameEventArgs args)
        {
            base.OnRenderFrame(args);

            skyRender.SetSunDirection(Vector3.Transform(skyRender.sunDirection, new Quaternion((float)args.Time * timeMult, 0f, 0f)));
            
            chunkManager.Update(aerialCamera, (float)args.Time, timeElapsed, totalFrameCount, skyRender.sunDirection.Normalized(), skyRender);

            Vector3 focusPoint = new(aerialCamera.focusPoint);
            float targetFocusHeight = chunkManager.worldGenerator.GetNoiseAt(World.NoiseLayer.HEIGHT, (int)focusPoint.X, (int)focusPoint.Z);
            aerialCamera.focusPoint.Y = Lerp(aerialCamera.focusPoint.Y, targetFocusHeight, aerialCamera.smoothing);

            //selectionBox.Upload();

            ////with everything prepped, we can now render
            //Matrix4 model = Matrix4.CreateTranslation(chunkManager.currentChunkIndex * (Chunk.SIZE));
            //Matrix4 view = aerialCamera.GetViewMatrix();
            //Matrix4 projection = aerialCamera.GetProjectionMatrix();
            //Shader bleh = chunkManager.renderer.blockShader;

            //bleh.SetMatrix4("model", model);
            //bleh.SetMatrix4("view", view);
            //bleh.SetMatrix4("projection", projection);

            //selectionBox.vao.Bind();
            //selectionBox.vbo.Bind();
            //selectionBox.ibo.Bind();

            //GL.DrawElements(
            //PrimitiveType.Triangles,
            //selectionBox.ibo.length,
            //DrawElementsType.UnsignedInt,
            //0
            //);

            SwapBuffers();

            // track fps
            frameTimeAccumulator += args.Time;
            shortFrameCount++;
            totalFrameCount++;

            if (frameTimeAccumulator >= 0.5)
            {
                Title = $"game - FPS: {shortFrameCount * 2} | " +
                    $"Position: {aerialCamera.Position()} | " +
                    $"Chunk: {chunkManager.currentChunkIndex} | " +
                    $"Chunk Tasks: {chunkManager.taskCount}/{chunkManager.maxChunkTasks} | " +
                    $"Render Distance: {chunkManager.radius}";
                frameTimeAccumulator = 0.0;
                shortFrameCount = 0;
            }
        }

        protected override void OnFramebufferResize(FramebufferResizeEventArgs e)
        {
            base.OnFramebufferResize(e);

            width = e.Width;
            height = e.Height;

            GL.Viewport(0, 0, width, height);
            aerialCamera.UpdateResolution(width, height);
        }

        // logic, non-rendering frame-by-frame stuff
        protected override void OnUpdateFrame(FrameEventArgs args)
        {
            MouseState mouse = MouseState;
            KeyboardState input = KeyboardState;

            base.OnUpdateFrame(args);

            if(focused)
                aerialCamera.Update(input, mouse, args);

            timeElapsed += (float)args.Time;

            // press escape to close this window or release mouse
            if (KeyboardState.IsKeyPressed(Keys.Escape))
            {
                if (CursorState == CursorState.Grabbed)
                {
                    CursorState = CursorState.Normal;
                    focused = false;
                    aerialCamera.firstMove = false;
                }
                else
                    Close();
            }
            if (MouseState.IsButtonPressed(MouseButton.Left))
            {
                CursorState = CursorState.Grabbed;
                focused = true;
                aerialCamera.firstMove = true;
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

        static float Lerp(float x, float y, float t)
        {
            return y * t + x * (1 - t);
        }
    }

    class Program
    {
        static void Main(string[] args)
        {
            Game G = new(1280, 720, "game");
            G.Run();
        }
    }
}