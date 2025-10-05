using OpenTK.Graphics.OpenGL4;

namespace Minecraft_Clone.Graphics
{
    public class FBOShadowMap
    {
        public int ID;
        public int depthTexture;
        public int width;
        public int height;

        public void Bind(){
            GL.BindFramebuffer(FramebufferTarget.Framebuffer , ID);
            GL.BindTexture(TextureTarget.Texture2D , depthTexture);
        }
        public void UnBind() {
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
            GL.BindTexture(TextureTarget.Texture2D, 0);
        }
        public void Dispose() {
            GL.DeleteFramebuffer(ID);
            GL.DeleteTexture(depthTexture);
        }

        public FBOShadowMap(int width, int height)
        {
            this.width = width;
            this.height = height;

            ID = GL.GenFramebuffer();
            Bind();

            // create shadowmap texture
            depthTexture = GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2D, depthTexture);

            // 24-bit buffer with no data yet
            GL.TexImage2D(TextureTarget.Texture2D, 
                0, 
                PixelInternalFormat.DepthComponent24, 
                width, height, 0, 
                PixelFormat.DepthComponent, 
                PixelType.UnsignedByte, 
                0);

            // attach depth texture to this framebuffer, specifically in the depth slot of this framebuffer
            GL.FramebufferTexture2D(FramebufferTarget.Framebuffer,
                FramebufferAttachment.DepthAttachment,
                TextureTarget.Texture2D,
                depthTexture, 0);

            // no drawing or reading colours since this is depth-only
            GL.DrawBuffer(DrawBufferMode.None);
            GL.ReadBuffer(ReadBufferMode.None);

            UnBind();
        }
    }
}
