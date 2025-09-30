using Minecraft_Clone.Graphics;
using OpenTK.Mathematics;
using static Minecraft_Clone.Graphics.CubeMesh;
using static Minecraft_Clone.Graphics.VBO;

namespace Minecraft_Clone.World.Chunks
{
    // Chunk State Machine states
    public enum ChunkState
    {
        BIRTH,
        GENERATING,
        GENERATED,
        FEATURING,
        FEATURED,
        MESHING,
        MESHED,
        VISIBLE,
        INVISIBLE,
        DIRTY
    };

    public class Chunk
    {
        public const int SIZE = 32; // same size in all coordinates
        private ChunkState state;
        public ChunkState GetState() { return state; } // protect the state from being directly modified
        public bool NeighborReady => !((state == ChunkState.BIRTH) || (state == ChunkState.GENERATING));// can the neighbor query this block for block existence?

        // is the chunk empty?
        public bool IsEmpty { 
            get => isEmpty; 
            set => isEmpty = value; }

        // limits unique blocks in the game to 256 which is fine
        public byte[] blocks;
        private bool isEmpty = true;

        // Chunks store their mesh data
        public MeshData solidMesh;
        public MeshData liquidMesh;

        // if a chunk is updated, it must be marked dirty (for a re-mesh)
        public bool TryMarkDirty()
        {
            if (LegalTransitions[ChunkState.DIRTY].Contains(state))
            {
                SetState(ChunkState.DIRTY);
                return true;
            }
            return false;
        }

        // practice chunk safety: enforce state transitions
        // {result state , beginning state(s) allowed}
        public static readonly Dictionary<ChunkState, List<ChunkState>> LegalTransitions = new()
        {
            { ChunkState.GENERATING, new(){ ChunkState.BIRTH } },
            { ChunkState.GENERATED, new() { ChunkState.GENERATING} },
            { ChunkState.FEATURING, new() { ChunkState.GENERATED} },
            { ChunkState.FEATURED, new() { ChunkState.FEATURING} },
            { ChunkState.MESHING, new(){ ChunkState.DIRTY, ChunkState.FEATURED, ChunkState.MESHING } },
            { ChunkState.MESHED, new() { ChunkState.MESHING} },
            { ChunkState.VISIBLE, new(){ ChunkState.MESHED, ChunkState.INVISIBLE, ChunkState.VISIBLE} },
            { ChunkState.INVISIBLE, new(){ChunkState.MESHED, ChunkState.VISIBLE, ChunkState.INVISIBLE } },
            { ChunkState.DIRTY, new(){ ChunkState.VISIBLE, ChunkState.MESHED} }
        };
        // Safe chunk state management to enforce legal state transitions
        public void SetState(ChunkState NewState)
        {
            if (!LegalTransitions[NewState].Contains(state))
                throw new Exception($"nuh uh, can't go from {state} -> {NewState}!");
            else
                state = NewState;
        }

        // default new chunk state is generating state... maybe later add "READING" to read from disk cache
        public Chunk(ChunkState state = ChunkState.BIRTH) { this.state = state; }

        // returns a block at the gievn local coordinate
        public Block GetBlock(int x, int y, int z)
        {
            if (IsEmpty) return new Block(BlockType.AIR);
            var type = (BlockType)blocks[(y * SIZE + z) * SIZE + x];
            return new Block(type);
        }

        // sets the block at a given local coordinate
        public void SetBlock(int x, int y, int z, BlockType type)
        {
            if (IsEmpty && type == BlockType.AIR) return;

            if (IsEmpty)
            {
                IsEmpty = false;
                blocks = new byte[SIZE * SIZE * SIZE];
            }
            blocks[(y * SIZE + z) * SIZE + x] = (byte)type;
        }

        // clean up meshes when removing from memory
        public void DisposeMeshes()
        {
            solidMesh?.Dispose();
            liquidMesh?.Dispose();
        }
    }
}