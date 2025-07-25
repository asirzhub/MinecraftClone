using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.GraphicsLibraryFramework;

namespace Minecraft_Clone
{
    public class Camera
    {
        // camera properties
        private float speed = 5f;
        public float screenwidth;
        public float screenheight;
        private float sensitivity = 10f;

        public Vector3 position;

        public Vector3 right = Vector3.UnitX;
        public Vector3 up = Vector3.UnitY; // we define Y as going up, not Z. but you can.
        public Vector3 front = -Vector3.UnitZ;
        public float fovY = 60;

        private float pitch;
        private float yaw;

        private bool firstMove = true;
        public Vector2 lastPos;

        // <summary>
        /// Create a camera with a specific width/height (for aspect ratio) and location in worldspace
        /// </summary>
        public Camera(float width, float height, Vector3 position)
        {
            screenwidth = width;
            screenheight = height;
            this.position = position;
        }

        public Matrix4 GetViewMatrix() => Matrix4.LookAt(position, position + front, up);
        public Matrix4 GetProjectionMatrix() => Matrix4.CreatePerspectiveFieldOfView(MathHelper.DegreesToRadians(fovY), screenwidth / screenheight, 0.01f, 2000f);

        private void UpdateVectors()
        { // copied straight out of the tutorial lol

            if (pitch > 85f) pitch = 85f;
            if (pitch < -85f) pitch = -85f;

            front.X = MathF.Cos(MathHelper.DegreesToRadians(pitch)) * MathF.Cos(MathHelper.DegreesToRadians(yaw));
            front.Y = MathF.Sin(MathHelper.DegreesToRadians(pitch));
            front.Z = MathF.Cos(MathHelper.DegreesToRadians(pitch)) * MathF.Sin(MathHelper.DegreesToRadians(yaw));

            front = Vector3.Normalize(front);


            right = Vector3.Normalize(Vector3.Cross(front, Vector3.UnitY));
            up = Vector3.Normalize(Vector3.Cross(right, front));
        }

        public void InputController(KeyboardState keyboard, MouseState mouse, FrameEventArgs e)
        {
            if (keyboard.IsKeyDown(Keys.W)) { position += front * speed * (float)e.Time; }
            if (keyboard.IsKeyDown(Keys.A)) { position -= right * speed * (float)e.Time; }
            if (keyboard.IsKeyDown(Keys.S)) { position -= front * speed * (float)e.Time; }
            if (keyboard.IsKeyDown(Keys.D)) { position += right * speed * (float)e.Time; }
            if (keyboard.IsKeyDown(Keys.Space)) { position.Y += speed * (float)e.Time; }
            if (keyboard.IsKeyDown(Keys.LeftControl)) { position.Y -= speed * (float)e.Time; }
            if (keyboard.IsKeyDown(Keys.LeftShift)) { speed = 15f; } else { speed = 5f; }

            if (firstMove)
            {
                lastPos = new Vector2(mouse.X, mouse.Y);
                firstMove = false;
            }
            else
            {
                var deltaX = mouse.X - lastPos.X;
                var deltaY = mouse.Y - lastPos.Y;
                lastPos = new Vector2(mouse.X, mouse.Y);

                yaw += deltaX * sensitivity * (float)e.Time;
                pitch -= deltaY * sensitivity * (float)e.Time;
            }

            UpdateVectors();
        }

        public void Update(KeyboardState keyboard, MouseState mouse, FrameEventArgs e)
        {
            InputController(keyboard, mouse, e);
        }

        public void UpdateResolution(float width, float height)
        {
            this.screenwidth = width;
            this.screenheight = height;
        }

        public float AspectRatio()
        {
            return screenwidth / screenheight;
        }
    }
}
