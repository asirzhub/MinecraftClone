using Minecraft_Clone.Graphics;
using OpenTK.Mathematics;
using static Minecraft_Clone.Graphics.CubeMesh;
using static Minecraft_Clone.Graphics.VBO;

namespace Minecraft_Clone.World.Chunks
{
    public enum ChunkState
    {
        BIRTH,
        GENERATING,
        GENERATED,
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
        public ChunkState GetState() { return state; }

        public Block[] blocks = new Block[SIZE * SIZE * SIZE];

        public MeshData solidMesh = new();
        public MeshData liquidMesh = new();

        // practice chunk safety: enforce state transitions
        // you can't transition into GENERATING - only happens at birth
        // {result state , beginning state(s) allowed}
        public static readonly Dictionary<ChunkState, List<ChunkState>> LegalTransitions = new()
        {
            {ChunkState.GENERATING, new(){ ChunkState.BIRTH } },
            { ChunkState.GENERATED, new() { ChunkState.GENERATING} },
            { ChunkState.MESHING, new(){ ChunkState.DIRTY, ChunkState.GENERATED, ChunkState.MESHING } },
            { ChunkState.MESHED, new() { ChunkState.MESHING} },
            { ChunkState.VISIBLE, new(){ ChunkState.MESHED, ChunkState.INVISIBLE} },
            { ChunkState.INVISIBLE, new(){ChunkState.MESHED, ChunkState.VISIBLE } },
            { ChunkState.DIRTY, new(){ ChunkState.VISIBLE} }
        };
        public void SetState(ChunkState NewState)
        {
            if (!LegalTransitions[NewState].Contains(state))
                throw new Exception($"nuh uh, can't go from {state} -> {NewState}!");
            else
                state = NewState;
        }

        public bool IsRenderable => (state != ChunkState.GENERATING && state != ChunkState.MESHING); // can't generate an ungenned-unmeshed chunk!

        // default new chunk state is generating state... maybe later add "READING" to read from disk cache
        public Chunk(ChunkState state = ChunkState.BIRTH) { this.state = state; }

        public Block GetBlock(int x, int y, int z) 
            => blocks[(y * SIZE + z) * SIZE + x];

        public void SetBlock(int x, int y, int z, BlockType type) 
            => blocks[(y * SIZE + z) * SIZE + x] = new Block(type);

        public void FillWithBlock(BlockType blockType)
        {
            for (int x = 0; x < SIZE; x++)
            {
                for (int y = 0; y < SIZE; y++)
                {
                    for (int z = 0; z < SIZE;  z++)
                    {
                        SetBlock(x, y, z, blockType);
                    }
                }
            }
            Console.WriteLine("filled " + blockType.ToString() +" into chunk");
        }

        public void DisposeMeshes()
        {
            solidMesh.Dispose();
            liquidMesh.Dispose();
        }
    }
}
