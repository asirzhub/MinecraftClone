using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Minecraft_Clone.Graphics.VBO;
using OpenTK.Mathematics;

namespace Minecraft_Clone.World
{
    public enum BlockType
    {
        AIR,
        WATER,
        STONE,
        DIRT,
        GRASS,
        LOG,
        LEAVES,
        SAND,
        SNOW,
        TALLGRASS,
        DIAMONDBLOCK,
        GOLDBLOCK,
        IRONBLOCK,
        EMERALDBLOCK
    }

    // <summary>
    /// BlockTypeData identifies different properties of blocks (texture, transparency, solidness, etc)
    /// </summary>
    public class BlockTypeData
    {
        public bool IsSolid;
        public bool IsTransparent;

        public Vector2[] FaceUVs = new Vector2[6]; // 

        // for blocks with identical face textures on every side
        public BlockTypeData(bool solid, bool transparent, Vector2 uniformUV) 
            : this(solid, transparent, new[] { uniformUV, uniformUV, uniformUV, uniformUV, uniformUV, uniformUV }) { }

        // for blocks with unique face textures on every side (log, grass, etc)
        public BlockTypeData(bool solid, bool transparent, Vector2[] faceUVs)
        {
            IsSolid = solid;
            IsTransparent = transparent;
            if (faceUVs.Length != 6) throw new ArgumentException("erm... what the scallop? (did not provide correct # of face UVs!)");
            FaceUVs = faceUVs;
        }
    }

    // <summary>
    /// BlockRegistry links the BlockType Enums (which blocks store) with their correct properties
    /// </summary>
    public static class BlockRegistry
    {
        public static readonly Dictionary<BlockType, BlockTypeData> Types = new()
        {
            // args: solid, transparent, texture tile coordinate
            { BlockType.AIR, new BlockTypeData(false, true, new Vector2(0,0)) },
            { BlockType.WATER, new BlockTypeData(false, true, new Vector2(1, 0))},
            { BlockType.STONE, new BlockTypeData(true, false, new Vector2(2,0)) },
            { BlockType.DIRT, new BlockTypeData(true, false, new Vector2(3,0)) },
            { BlockType.GRASS, new BlockTypeData(true, false, [(4,1), (4, 1) , (4, 1) , (4, 1) , (4, 0), (3, 0)]) },
            { BlockType.LOG, new BlockTypeData(true, false, [(5,1), (5, 1) , (5, 1) , (5, 1) , (5, 0), (5, 0)])},
            { BlockType.LEAVES, new BlockTypeData(true, false, new Vector2(6,0)) },
            { BlockType.SAND, new BlockTypeData(true, false, new Vector2(7,0)) },
            { BlockType.SNOW, new BlockTypeData(true, false, new Vector2(7,1)) },
            { BlockType.DIAMONDBLOCK, new BlockTypeData(true, false, new Vector2(0,2)) },
            { BlockType.GOLDBLOCK, new BlockTypeData(true, false, new Vector2(1,2)) },
            { BlockType.IRONBLOCK, new BlockTypeData(true, false, new Vector2(2,2)) },
            { BlockType.EMERALDBLOCK, new BlockTypeData(true, false, new Vector2(3,2)) },

        };
    }
}
