using System.Collections.Generic;
using UnityEngine;

namespace RealLifeOverhaul.Uptown
{
    /// <summary>
    /// The edge of a block's ground laid as its middle. The game's block ground (SM_120-70 GroundPlane, Build 3682) is a
    /// ring of the pavements' own slabs, 4.5 m wide on its east and south sides and 8.6 m and 8.2 m on its west and north,
    /// round its middle of grass or asphalt, laid inside the streets' own pavements. In Manhattan the game's buildings stand
    /// over it from the pavement's back edge, edge to edge, so it never shows; between the district's buildings, on its open
    /// lots and round its corners it showed as a second pavement up to 8.6 m wide, so every building seemed to stand in the
    /// pavement. Each block ground of the band, the copies and the backdrop's own over it, draws its ring with its middle's
    /// material, so the pavement ends where the buildings' fronts stand; the backdrop's get their own back on every way out
    /// (DropArt). COST: at the load, one look at each block ground's materials; nothing per frame.
    /// </summary>
    public static partial class GroundGame
    {
        private const string PavementMaterial = "M_RoadSideCurb";   // the slabs of the streets' pavements, and of a block ground's ring

        private static readonly List<Renderer> KeptPlanes = new List<Renderer>(64);         // the backdrop's own block grounds, which stay
        private static readonly List<Renderer> Repave = new List<Renderer>(32);             // those of them over the band laid otherwise
        private static readonly List<Material[]> RepaveWith = new List<Material[]>(32);     // ... and their own materials, to give back

        /// <summary>Block grounds whose ring of slabs is laid as their middle at this load: the copies and the backdrop's own over the band.</summary>
        public static int Unpaved { get; private set; }

        // A block ground's ring laid with its middle's material; with keep, its own materials kept to be given back. False
        // when it has no ring (the backdrop's 160 by 70 m grounds are old asphalt alone).
        private static bool Unpave(Renderer ground, bool keep)
        {
            if (ground == null)
            {
                return false;
            }
            Material[] own = ground.sharedMaterials;
            int middle = -1;
            bool ring = false;
            for (int i = 0; i < own.Length; i++)
            {
                bool slabs = own[i] != null && own[i].name == PavementMaterial;
                ring |= slabs;
                middle = middle < 0 && own[i] != null && !slabs ? i : middle;
            }
            if (!ring || middle < 0)
            {
                return false;
            }
            var laid = new Material[own.Length];
            for (int i = 0; i < own.Length; i++)
            {
                laid[i] = own[i] != null && own[i].name == PavementMaterial ? own[middle] : own[i];
            }
            ground.sharedMaterials = laid;
            if (keep)
            {
                Repave.Add(ground);
                RepaveWith.Add(own);
            }
            Unpaved++;
            return true;
        }

        // The backdrop's own block grounds whose middle is on the band laid as the copies are (the first block's, and those the
        // backdrop keeps further out); those round the band stay as the game has them.
        private static void UnpaveKept()
        {
            for (int i = 0; i < KeptPlanes.Count; i++)
            {
                Renderer plane = KeptPlanes[i];
                if (plane == null || !plane.name.StartsWith(GroundPlane, System.StringComparison.Ordinal))
                {
                    continue;
                }
                Vector3 middle = plane.bounds.center;
                if (LayoutRules.OnGround(middle.x, middle.z))
                {
                    Unpave(plane, true);
                }
            }
        }

        // The backdrop's block grounds given their own materials back.
        private static void Repaved()
        {
            for (int i = 0; i < Repave.Count; i++)
            {
                if (Repave[i] != null)
                {
                    Repave[i].sharedMaterials = RepaveWith[i];
                }
            }
            Repave.Clear();
            RepaveWith.Clear();
            KeptPlanes.Clear();
            Unpaved = 0;
        }
    }
}
