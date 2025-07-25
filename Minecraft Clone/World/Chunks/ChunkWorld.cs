using OpenTK.Mathematics;

namespace Minecraft_Clone.World.Chunks
{
    public class ChunkWorld
    {
        // store chunks in a 3d dictionary, with vector3 (integers) perfect for our case
        public Dictionary<Vector3i, Chunk> chunks = new();


        public int seaLevel;
        public int dirtThickness;
        public int sandFalloff;

        // <summary>
        /// Create a world using a seed and noise Scale
        /// </summary>
        public ChunkWorld()
        {
        }

        // <summary>
        /// Instance a chunk at the designated location, and add it to the world's chunks.
        /// </summary>
        public Chunk AddChunk(Vector3i pos, BlockType fillType = BlockType.AIR)
        {
            Chunk chunk = new Chunk();
            chunk.FillWithBlock(fillType);
            chunks.Add(pos, chunk);
            return chunk;
        }

        public void AddChunk(Vector3i pos, Chunk chunk) {
            chunks.Add(pos, chunk);
        }

        public bool HasChunk(Vector3i pos) 
        {
            if (chunks.TryGetValue(pos, out var chunk)) return true;
            else return false;
        }

        public Vector3i GetChunkIndex(Chunk chunk)
        {
            foreach (var kvp in chunks)
                if (kvp.Value == chunk) return kvp.Key;

            Console.WriteLine("[ChunkWorld] Tried getting a chunk index that is not registered!!!");
            return (0,0,0);
        }

        public Chunk GetChunkAtIndex(Vector3i index, out bool found)
        {
            found = false;
            foreach (var kvp in chunks)
                if (kvp.Key == index)
                {
                    found = true;
                    return kvp.Value;
                }
            
            Console.WriteLine($"[ChunkWorld] Tried getting a chunk at {index} that is not registered!!!");
            return new Chunk(); // find a way to NOT do this
        }
    }
}