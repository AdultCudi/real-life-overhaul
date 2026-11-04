using System;

namespace RealLifeOverhaul.Map25
{
    /// <summary>
    /// What a building's drawing is made of, one chunk list each, because each is drawn under its own rule: the blocks always; the crowns
    /// (the setback of a wide tower) only when the blocks have height, so the flat map has no frame inside a frame; the fronts (shop awnings)
    /// only when the blocks stand well up; the panes (windows, a translucent pane a quad) only then too, over the walls; the contact patches
    /// under the blocks only when they have height.
    /// </summary>
    public enum Map25Kind
    {
        Blocks = 0,
        Crowns = 1,
        Fronts = 2,
        Panes = 3,
        Contacts = 4
    }

    /// <summary>The chunks of one kind for the whole city, and where each building's own run of vertices lies in them.</summary>
    public sealed class Map25Layer
    {
        public Map25Chunk[] Chunks = new Map25Chunk[0];
        public int[] ChunkOf = new int[0], First = new int[0], Count = new int[0];
        public int Vertices, Triangles;

        /// <summary>
        /// Whether the colours of a building's vertices are written again when it is tinted (the panes and the contact patches are not: they are
        /// translucent over whatever is under them).
        /// </summary>
        public bool Tints;

        public void Total()
        {
            Vertices = 0;
            Triangles = 0;
            for (int k = 0; k < Chunks.Length; k++)
            {
                Vertices += Chunks[k].Vertices;
                Triangles += Chunks[k].Indices / 3;
            }
        }
    }

    /// <summary>One builder for each kind of drawing of a building: a building is generated into all of them at once.</summary>
    public sealed class Map25Builders
    {
        public const int Kinds = 5;

        public readonly Map25Builder[] All = new Map25Builder[Kinds];

        public Map25Builders(int capacity)
        {
            for (int k = 0; k < Kinds; k++)
            {
                All[k] = new Map25Builder(capacity);
            }
        }

        public Map25Builder Blocks => All[(int)Map25Kind.Blocks];
        public Map25Builder Crowns => All[(int)Map25Kind.Crowns];
        public Map25Builder Fronts => All[(int)Map25Kind.Fronts];
        public Map25Builder Panes => All[(int)Map25Kind.Panes];
        public Map25Builder Contacts => All[(int)Map25Kind.Contacts];

        public void Clear()
        {
            for (int k = 0; k < Kinds; k++)
            {
                All[k].Clear();
            }
        }

        public int Vertices
        {
            get
            {
                int total = 0;
                for (int k = 0; k < Kinds; k++)
                {
                    total += All[k].Vertices;
                }
                return total;
            }
        }
    }

    /// <summary>When each kind is drawn, by how tall the blocks stand (<see cref="Map25View.Rise"/>): a rule, so the flat map is clean and the renderer and the checks agree.</summary>
    public static class Map25Rules
    {
        public const float CrownsFrom = 0.10f;     // a setback shows once the blocks are a tenth as tall as they are
        public const float PanesFrom = 0.25f;      // windows from a quarter
        public const float FrontsFrom = 0.50f;     // awnings from a half: they stand out of the wall, so a flat map would show them as bars

        public static bool Draws(Map25Kind kind, float rise)
        {
            switch (kind)
            {
                case Map25Kind.Blocks:
                    return true;
                case Map25Kind.Crowns:
                    return rise > CrownsFrom;
                case Map25Kind.Panes:
                    return rise > PanesFrom;
                case Map25Kind.Fronts:
                    return rise > FrontsFrom;
                case Map25Kind.Contacts:
                    return rise > CrownsFrom;
                default:
                    throw new ArgumentOutOfRangeException(nameof(kind));
            }
        }
    }
}
