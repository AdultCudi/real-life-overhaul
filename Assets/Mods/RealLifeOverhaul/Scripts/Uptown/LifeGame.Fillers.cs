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
        private static readonly List<LookPiece> FillerPieces = new List<LookPiece>(256);   // each filler as the census sees it, in step with Fillers

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
                string name = "filler " + FillerPieces.Count.ToString(System.Globalization.CultureInfo.InvariantCulture);
                FillerPieces.Add(new LookPiece { Id = name, Design = d, Ground = f.Lot, Filler = true, Back = f.Back });
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

        /// <summary>The fillers standing, as the census sees them, with the root each stands on, into the caller's lists (not cleared).</summary>
        public static void FillersInto(List<LookPiece> pieces, List<Transform> roots)
        {
            for (int i = 0; i < Fillers.Count; i++)
            {
                Transform root = BuildingCopies.RootOf(Fillers[i]);
                if (root != null && i < FillerPieces.Count)
                {
                    pieces.Add(FillerPieces[i]);
                    roots.Add(root);
                }
            }
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

        // The fillers standing on the ground of some lots hidden (their building was knocked down): a filler stands on its lot's
        // ground, so one whose root is inside a lot's rectangle is that lot's. Uptown's grid lies along the world's axes.
        private static int HideFillersOn(List<BuildingsLot> lots)
        {
            int hidden = 0;
            for (int i = 0; lots != null && i < Fillers.Count; i++)
            {
                Transform root = BuildingCopies.RootOf(Fillers[i]);
                if (root == null || !root.gameObject.activeSelf)
                {
                    continue;
                }
                Vector3 at = root.position;
                for (int k = 0; k < lots.Count; k++)
                {
                    if (OnLot(lots[k], at.x, at.z))
                    {
                        root.gameObject.SetActive(false);
                        hidden++;
                        break;
                    }
                }
            }
            return hidden;
        }

        // True for a point on a lot's ground: its width along its street, its depth back from it.
        private static bool OnLot(BuildingsLot lot, float x, float z)
        {
            bool alongX = Mathf.Abs(Mathf.DeltaAngle(lot.Facing, 0f)) < 45f || Mathf.Abs(Mathf.DeltaAngle(lot.Facing, 180f)) < 45f;
            float halfX = (alongX ? lot.Width : lot.Depth) * 0.5f, halfZ = (alongX ? lot.Depth : lot.Width) * 0.5f;
            return Mathf.Abs(x - lot.X) <= halfX && Mathf.Abs(z - lot.Z) <= halfZ;
        }

        // The city goes or a new load begins: the fillers are taken away.
        private static void DropFillers()
        {
            for (int i = 0; i < Fillers.Count; i++)
            {
                BuildingCopies.RemoveLook(Fillers[i]);
            }
            Fillers.Clear();
            FillerPieces.Clear();
        }
    }
}
