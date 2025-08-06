using OpenTK.Mathematics;
using OpenTK.Graphics.OpenGL4;

namespace Minecraft_Clone.World.Chunks
{
    // Orchestrates the lifecycle of chunks: updates, mesh creation, and rendering.
    public class ChunkManager
    {
        ChunkLoader loader; // decides which chunks to load or unload from the world, based on player pos
        ChunkWorld world; // stores loaded chunks in memory
        public ChunkGenerator[] generators; // generators are only dispatched when a new chunk is made
        ChunkMesher mesher; // based on what the laoder has decided, mesher makes a mesh for each loaded chunk
        ChunkRenderer renderer; // renders the chunks that the mesher has meshed

        private Vector3i currentChunkIndex; // stores the player's current chunk's index

        public ChunkManager()
        {
            loader = new ChunkLoader();
            world = new ChunkWorld();
            generators = new ChunkGenerator[4];

            for(int i = 0; i < 4; i++)
                generators[i] = new ChunkGenerator();

            mesher = new ChunkMesher();
            renderer = new ChunkRenderer();
        }

        public void Update(Camera camera, float time, Vector3 sunDirection)
        {
            currentChunkIndex = ToChunkIndex(camera.position);

            // Loader decides which chunks to load/unload
            var loadList = loader.GetChunksToLoad(currentChunkIndex, camera, radius:2);

            // with the list of chunks to load, check the world to see if a chunk was created prior:
            foreach (var kvp in loadList)
            {
                //Console.WriteLine($"Does the world have the chunk {kvp.Key} with visibility set {kvp.Value}? {world.HasChunk(kvp.Key)}");
                Vector3i thisChunkIndex = kvp.Key;
                bool thisChunkExists = world.HasChunk(thisChunkIndex);
                bool showThisChunk = kvp.Value;
                // if it exists, just render it if its visible
                if (thisChunkExists && showThisChunk) {
                    //Console.WriteLine($"Need to render the chunk at {thisChunkIndex}");
                    mesher.GenerateMesh(thisChunkIndex, world);
                }
                // if it doesnt exist, create it first
                if (!thisChunkExists)
                {
                    //Console.WriteLine($"adding a chunk that was not created before at {thisChunkIndex}");
                    Chunk newChunk = new Chunk();
                    world.AddChunk(thisChunkIndex, newChunk);// after it's added to the world, it can be generated
                    generators[0].GenerateChunk(newChunk, thisChunkIndex, world);
                }
                if (thisChunkExists && !showThisChunk)
                {
                    world.GetChunkAtIndex(thisChunkIndex, out var found).dirty = true;
                    mesher.DisposeMesh(thisChunkIndex);
                }
            }

            Render(camera, time, sunDirection);
        }

        private void Render(Camera camera, float time, Vector3 sunDirection)
        {
            foreach(var kvp in mesher.solidMeshes)
            {
                renderer.RenderChunk(kvp.Value, camera, kvp.Key, time, sunDirection);
            }

            GL.Enable(EnableCap.Blend);
            GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
            GL.DepthMask(false);

            foreach (var kvp in mesher.liquidMeshes)
            {
                renderer.RenderChunk(kvp.Value, camera, kvp.Key, time, sunDirection);
            }

            GL.DepthMask(true);
        }

        private Vector3i ToChunkIndex(Vector3 position)
        {
            return new Vector3i(
                (int)MathF.Floor(position.X / Chunk.SIZE),
                (int)MathF.Floor(position.Y / Chunk.SIZE),
                (int)MathF.Floor(position.Z / Chunk.SIZE)
            );
        }

    }
}
