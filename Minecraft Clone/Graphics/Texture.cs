using OpenTK.Graphics.OpenGL4;
using StbImageSharp;

namespace Minecraft_Clone.Graphics
{
    public class Texture
    {
        public int ID;
        string pathPrefix = "../../../Textures/";

        public void Bind() {
            GL.BindTexture(TextureTarget.Texture2D, ID);
            GL.GenerateMipmap(GenerateMipmapTarget.Texture2D); 
        }
        public void UnBind() => GL.BindTexture(TextureTarget.Texture2D, 0);
        public void Delete() => GL.DeleteTexture(ID);

        // <summary>
        /// Bind, import a texture, upload it.
        /// </summary>
        public Texture(string path)
        {
            ID = GL.GenTexture();
            Bind();

            // texture params
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.Repeat);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.Repeat);

            // pixel-perfect
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);

            // stb reads pictures upside down so fix that
            StbImage.stbi_set_flip_vertically_on_load(1);

            // Load the image.
            ImageResult image = ImageResult.FromStream(File.OpenRead(pathPrefix + path), ColorComponents.RedGreenBlueAlpha);

            // upload the image
            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba, image.Width, image.Height, 0, PixelFormat.Rgba, PixelType.UnsignedByte, image.Data);
        }
    }
}