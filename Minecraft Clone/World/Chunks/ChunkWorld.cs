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
            MarkNeighborsDirty(pos);
            return chunk;
        }

        public void AddChunk(Vector3i pos, Chunk chunk)
        {
            chunks.Add(pos, chunk);
            MarkNeighborsDirty(pos);
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

            //Console.WriteLine("[ChunkWorld] Tried getting a chunk index that is not registered!!!");
            return (0, 0, 0);
        }

        public Chunk? GetChunkAtIndex(Vector3i index, out bool found)
        {
            found = false;
            foreach (var kvp in chunks)
                if (kvp.Key == index)
                {
                    found = true;
                    return kvp.Value;
                }

            //Console.WriteLine($"[ChunkWorld] Tried getting a chunk at {index} that is not registered!!!.");

            return null;
        }

        public bool TryGetBlockAt(
            Vector3i worldPos,
            out Block block)
        {
            // 1) figure out which chunk that lives in, using true floor division
            int cx = FloorDiv(worldPos.X, Chunk.SIZE);
            int cy = FloorDiv(worldPos.Y, Chunk.SIZE);
            int cz = FloorDiv(worldPos.Z, Chunk.SIZE);
            var chunkIndex = new Vector3i(cx, cy, cz);

            // 2) grab the chunk
            if (!chunks.TryGetValue(chunkIndex, out var chunk))
            {
                block = default!;
                return false;
            }

            // 3) compute the local‐in‐chunk coordinate by subtracting the chunk’s origin
            int lx = worldPos.X - cx * Chunk.SIZE;
            int ly = worldPos.Y - cy * Chunk.SIZE;
            int lz = worldPos.Z - cz * Chunk.SIZE;

            // 4) sanity‐check (should be 0 <= l? < SIZE)
            if (lx < 0 || lx >= Chunk.SIZE
             || ly < 0 || ly >= Chunk.SIZE
             || lz < 0 || lz >= Chunk.SIZE)
            {
                block = default!;
                return false;
            }

            // success
            block = chunk.GetBlock(lx, ly, lz);
            return true;
        }

        /// <summary>
        /// Integer floor‐division helper: always rounds down.
        /// </summary>
        private static int FloorDiv(int a, int b)
            => (int)MathF.Floor(a / (float)b);

        public void MarkNeighborsDirty(Vector3i centerChunkIndex, bool includeSelf = false) 
        {
            List<Vector3i> directions = new(){
                Vector3i.UnitX, Vector3i.UnitY, Vector3i.UnitZ,
                -Vector3i.UnitX, -Vector3i.UnitY, -Vector3i.UnitZ};
            
            if (includeSelf) directions.Add(Vector3i.Zero);

            foreach(var offset in directions)
            {
                if (chunks.TryGetValue(centerChunkIndex + offset, out var chunk))
                {
                    chunk.dirty = true;
                }
            }
        }
    }
}