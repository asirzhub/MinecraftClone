using Minecraft_Clone.Graphics;
using OpenTK.Mathematics;
using System.Collections.Concurrent;
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

        public ConcurrentDictionary<Vector3i, BlockType> pendingBlocks;

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

        public bool hasMeshes => solidMesh != null && liquidMesh != null;

        // if a chunk is updated, it must be marked dirty (for a re-mesh)
        public bool TryMarkDirty()
        {
            if(state == ChunkState.DIRTY) return true;
            if (LegalTransitions[ChunkState.DIRTY].Contains(state))
            {
                SetState(ChunkState.DIRTY);
                return true;
            }
            return false;
        }

        public void AddPendingBlock(Vector3i pos, BlockType newType)
        {
            if (pendingBlocks == null)
                pendingBlocks = new();

            if (pendingBlocks.TryGetValue(pos, out var oldType))
            {
                if (oldType.Equals(newType))
                    return;
            }

            else
                pendingBlocks.TryAdd(pos, newType);
        }

        public async Task ProcessBlocks()
        {
            if (pendingBlocks == null || pendingBlocks.Count == 0)
                return;

            await Task.Run(async () =>
            {
                foreach (var kvp in pendingBlocks.ToArray())
                {
                    await SetBlockAsync(kvp.Key, kvp.Value);
                    pendingBlocks.TryRemove(kvp.Key, out _);
                }
            });
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

        // sets the block at a given local coordinate. this should only be used on creation, everything else on async method
        public void SetBlock(int x, int y, int z, BlockType type)
        {
            if (IsEmpty && type == BlockType.AIR) return;

            if (IsEmpty)
            {
                IsEmpty = false;
                blocks = new byte[SIZE * SIZE * SIZE];
            }
            blocks[(y * SIZE + z) * SIZE + x] = (byte)type;

            TryMarkDirty();
        }

        public void SetBlock(Vector3i pos, BlockType type)
        {
            if (IsEmpty && type == BlockType.AIR) return;

            if (IsEmpty)
            {
                IsEmpty = false;
                blocks = new byte[SIZE * SIZE * SIZE];
            }

            blocks[(pos.Y * SIZE + pos.Z) * SIZE + pos.X] = (byte)type;

            TryMarkDirty();
        }

        public Task<Vector3i[]> SetBlockAsync(Vector3i pos, BlockType type)
        {
            if (IsEmpty && type == BlockType.AIR) return Task.FromResult(new Vector3i[0]);

            if (IsEmpty)
            {
                IsEmpty = false;
                blocks = new byte[SIZE * SIZE * SIZE];
            }

            blocks[(pos.Y * SIZE + pos.Z) * SIZE + pos.X] = (byte)type;

            TryMarkDirty();
            return Task.FromResult(NeighborDirtyAlert(pos));
        }

        public Vector3i[] NeighborDirtyAlert(Vector3i localCoord)
        {
            List<Vector3i> result = new();

            if (localCoord.X == 0) result.Add(new Vector3i(-1, 0, 0));
            if (localCoord.Y == 0) result.Add(new Vector3i(0, -1, 0));
            if (localCoord.Z == 0) result.Add(new Vector3i(0, 0, -1));
            if (localCoord.X == 31) result.Add(new Vector3i(1, 0, 0));
            if (localCoord.Y == 31) result.Add(new Vector3i(0, 1, 0));
            if (localCoord.Z == 31) result.Add(new Vector3i(0, 0, 1));

            return result.ToArray();
        }

        // clean up meshes when removing from memory
        public void DisposeMeshes()
        {
            solidMesh?.Dispose();
            liquidMesh?.Dispose();
        }
    }
}