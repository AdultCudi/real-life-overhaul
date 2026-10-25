using System.Collections.Generic;
using RealLifeOverhaul.NewBuildings;
using UnityEngine;
using UnityEngine.AI;

namespace RealLifeOverhaul.Uptown
{
    /// <summary>
    /// The fillers between and behind Uptown's buildings (FrontageRules.Plan): the game's own buildings copied for their looks
    /// alone, no controller, so they are no building of the city's and nobody enters them, as the game's backdrop is; each
    /// cut into the walking meshes as a district building is, so nobody walks through its walls. Made once a load after the
    /// district's buildings stand, hidden with them when the district closes, gone when the city goes or the next load begins.
    /// COST: nothing per frame; a copy and a carving box each, once a load, behind the loading screen.
    /// </summary>
    internal static partial class LifeGame
    {
        private const string FillerName = "RealLife Uptown filler";

        private static readonly List<CopyLook> Fillers = new List<CopyLook>(256);

        /// <summary>The fillers standing at this load.</summary>
        public static int FillerCount => Fillers.Count;

        /// <summary>Stands every filler of the plan, its root on the district's ground, cut into the walking meshes; returns how many stood.</summary>
        public static int StandFillers(IList<FrontFill> fills)
        {
            IList<DesignEntry> designs = CopyGame.Catalogue;
            for (int i = 0; fills != null && i < fills.Count; i++)
            {
                FrontFill f = fills[i];
                if (f.Design < 0 || f.Design >= designs.Count)
                {
                    continue;
                }
                DesignEntry d = designs[f.Design];
                NewBuildingsRules.Place(d, f.Lot, out float x, out float z, out float yaw);
                CopyLook look = BuildingCopies.StandLook(f.Design, new Vector3(x, UptownLots.GroundY - d.DoorY, z), yaw, FillerName);
                Transform root = BuildingCopies.RootOf(look);
                if (root == null)
                {
                    continue;
                }
                CutFiller(root, d);
                Fillers.Add(look);
            }
            return Fillers.Count;
        }

        // A carving box over the filler's footprint, as Cut gives a district building, so the walking meshes go round its walls.
        private static void CutFiller(Transform root, DesignEntry d)
        {
            NavMeshObstacle cut = root.gameObject.AddComponent<NavMeshObstacle>();
            Vector3 scale = root.lossyScale;
            float sx = Mathf.Max(Mathf.Abs(scale.x), 1e-4f), sy = Mathf.Max(Mathf.Abs(scale.y), 1e-4f), sz = Mathf.Max(Mathf.Abs(scale.z), 1e-4f);
            float width = Mathf.Max(0.5f, d.MaxX - d.MinX - 2f * CutInset), depth = Mathf.Max(0.5f, d.MaxZ - d.MinZ - 2f * CutInset), height = Mathf.Max(2f, d.Height);
            cut.shape = NavMeshObstacleShape.Box;
            cut.center = new Vector3((d.MinX + d.MaxX) * 0.5f / sx, height * 0.5f / sy, (d.MinZ + d.MaxZ) * 0.5f / sz);
            cut.size = new Vector3(width / sx, height / sy, depth / sz);
            cut.carveOnlyStationary = true;
            cut.carving = true;
        }

        /// <summary>The fillers shown or hidden with the district's buildings.</summary>
        public static void ShowFillers(bool shown)
        {
            for (int i = 0; i < Fillers.Count; i++)
            {
                Transform root = BuildingCopies.RootOf(Fillers[i]);
                if (root != null && root.gameObject.activeSelf != shown)
                {
                    root.gameObject.SetActive(shown);
                }
            }
        }

        // The city goes or a new load begins: the fillers are taken away.
        private static void DropFillers()
        {
            for (int i = 0; i < Fillers.Count; i++)
            {
                BuildingCopies.RemoveLook(Fillers[i]);
            }
            Fillers.Clear();
        }
    }
}
