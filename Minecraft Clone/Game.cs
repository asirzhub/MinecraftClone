using Minecraft_Clone.Graphics;
using Minecraft_Clone.World;
using Minecraft_Clone.World.Chunks;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;
using System;
using static Minecraft_Clone.Graphics.VBO;

namespace Minecraft_Clone
{
    public struct Ray () {
        public Vector3 origin;
        public Vector3 direction;
        public bool hit;
        public Vector3 hitLoc;
    };


    class Game : GameWindow
    {
        public static bool RaycastSolidBlock(
        ChunkManager cm,
        Vector3 origin,
        Vector3 dir,
        float maxDist,
        int maxSteps,
        out Vector3i hitBlock,
        out Vector3i lastBlockBeforeHit
        )
        {
            hitBlock = default;

            // Start cell
            int x = (int)MathF.Floor(origin.X);
            int y = (int)MathF.Floor(origin.Y);
            int z = (int)MathF.Floor(origin.Z);

            int stepX = dir.X > 0f ? 1 : (dir.X < 0f ? -1 : 0);
            int stepY = dir.Y > 0f ? 1 : (dir.Y < 0f ? -1 : 0);
            int stepZ = dir.Z > 0f ? 1 : (dir.Z < 0f ? -1 : 0);

            float tDeltaX = stepX == 0 ? float.PositiveInfinity : MathF.Abs(1f / dir.X);
            float tDeltaY = stepY == 0 ? float.PositiveInfinity : MathF.Abs(1f / dir.Y);
            float tDeltaZ = stepZ == 0 ? float.PositiveInfinity : MathF.Abs(1f / dir.Z);

            // Distance to first boundary
            float nextVoxelBoundaryX = stepX > 0 ? (x + 1) : x;
            float nextVoxelBoundaryY = stepY > 0 ? (y + 1) : y;
            float nextVoxelBoundaryZ = stepZ > 0 ? (z + 1) : z;

            // infinities are protection against edge case where pointing exactly along an axis
            float tMaxX = stepX == 0 ? float.PositiveInfinity : (nextVoxelBoundaryX - origin.X) / dir.X;
            float tMaxY = stepY == 0 ? float.PositiveInfinity : (nextVoxelBoundaryY - origin.Y) / dir.Y;
            float tMaxZ = stepZ == 0 ? float.PositiveInfinity : (nextVoxelBoundaryZ - origin.Z) / dir.Z;

            // no negatives
            if (tMaxX < 0f) tMaxX = 0f;
            if (tMaxY < 0f) tMaxY = 0f;
            if (tMaxZ < 0f) tMaxZ = 0f;

            float t = 0f;

            Vector3i lastMove = (0, 0, 0);

            for (int i = 0; i < maxSteps && t <= maxDist; i++)
            {
                // Check current cell
                if (cm.TryGetBlockAtWorldPosition(new Vector3i(x, y, z), out var b) && b.IsSolid)
                {
                    hitBlock = new Vector3i(x, y, z);
                    lastBlockBeforeHit = hitBlock - lastMove;
                    return true;
                }

                // Step to next cell
                if (tMaxX <= tMaxY && tMaxX <= tMaxZ)
                {
                    x += stepX;
                    t = tMaxX;
                    tMaxX += tDeltaX;
                    lastMove = (stepX, 0, 0);
                }
                else if (tMaxY <= tMaxZ)
                {
                    y += stepY;
                    t = tMaxY;
                    tMaxY += tDeltaY;
                    lastMove = (0, stepY, 0);
                }
                else
                {
                    z += stepZ;
                    t = tMaxZ;
                    tMaxZ += tDeltaZ;
                    lastMove = (0, 0, stepZ);
                }

            }

            lastBlockBeforeHit = lastMove;
            return false;
        }


        AerialCameraRig aerialCamera;
        public bool camOrbiting = true;

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

        Ray mouseRay;

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
            skyRender = new SkyRender((-1f, 1f, 0f));
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

            SwapBuffers();

            // track fps
            frameTimeAccumulator += args.Time;
            shortFrameCount++;
            totalFrameCount++;

            if (frameTimeAccumulator >= 0.5)
            {
                Title = $"game - FPS: {shortFrameCount * 2} | " +
                    $"Position: {aerialCamera.CameraPosition()} | " +
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

            timeElapsed += (float)args.Time;

            // press escape to close this window or release mouse
            if (KeyboardState.IsKeyPressed(Keys.Escape)) Close();

            camOrbiting = MouseState.IsButtonDown(MouseButton.Right);

            if (camOrbiting)
            {
                CursorState = CursorState.Grabbed;
                aerialCamera.Update(input, mouse, args);
                aerialCamera.firstMove = false;
            }
            else
            {
                CursorState = CursorState.Normal;
                aerialCamera.firstMove = true;
            }

            if (KeyboardState.IsKeyPressed(Keys.Period)) chunkManager.radius++;            

            if (KeyboardState.IsKeyPressed(Keys.Comma)) chunkManager.radius--;

            Vector3 focusPoint = new(aerialCamera.focusPoint);
            float targetFocusHeight = chunkManager.worldGenerator.GetNoiseAt(World.NoiseLayer.HEIGHT, (int)focusPoint.X, (int)focusPoint.Z);
            aerialCamera.focusPoint.Y = Lerp(aerialCamera.focusPoint.Y, targetFocusHeight, aerialCamera.smoothing);

            if (MouseState.IsButtonPressed(MouseButton.Left))
            {
                var origin = aerialCamera.CameraPosition();   // more stable than focusPoint

                float aspect = aerialCamera.screenWidth / aerialCamera.screenHeight;

                float tanHalfFovY = MathF.Tan(MathHelper.DegreesToRadians(aerialCamera.fovY * 0.5f));
                float tanHalfFovX = tanHalfFovY * aspect;

                Vector2 ndc = mouse.Position;
                ndc.X = (ndc.X / aerialCamera.screenWidth) * 2f - 1f;
                ndc.Y = 1f - (ndc.Y / aerialCamera.screenHeight) * 2f;

                Vector3 dir =
                    aerialCamera.forward +
                    aerialCamera.right * (ndc.X * tanHalfFovX) +
                    aerialCamera.up * (ndc.Y * tanHalfFovY);

                dir = Vector3.Normalize(dir);

                if (RaycastSolidBlock(chunkManager, origin, dir, maxDist: 256f, maxSteps: 256, out var hit, out var place))
                    chunkManager.TrySetBlockAtWorldPosition(hit, BlockType.AIR);
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