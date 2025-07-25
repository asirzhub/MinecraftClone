using OpenTK.Mathematics;

namespace Minecraft_Clone.World.Chunks
{
    // Orchestrates the lifecycle of chunks: updates, mesh creation, and rendering.
    public class ChunkManager
    {
        ChunkLoader loader; // decides which chunks to load or unload from the world, based on player pos
        ChunkWorld world; // stores loaded chunks in memory
        ChunkGenerator[] generators; // generators are only dispatched when a new chunk is made
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

        public void Update(Camera camera)
        {
            currentChunkIndex = ToChunkIndex(camera.position);

            // Loader decides which chunks to load/unload
            var loadList = loader.GetChunksToLoad(currentChunkIndex, camera, radius:1);

            int chunksGeneratedThisFrame = 0;
            int maxPerFrame = 2;

            foreach (var kvp in loadList)
            {
                if (chunksGeneratedThisFrame >= maxPerFrame)
                    break;

                if (kvp.Value && !world.HasChunk(kvp.Key))
                {
                    var chunk = new Chunk();
                    generators[0].GenerateChunk(chunk);
                    world.AddChunk(kvp.Key, chunk);
                    mesher.GenerateMesh(kvp.Key, world);
                    chunksGeneratedThisFrame++;
                }
            }

            Render(camera);
        }

        private void Render(Camera camera)
        {
            foreach (var kvp in mesher.meshes)
            {
                renderer.RenderChunk(kvp.Value, camera);
            }
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
