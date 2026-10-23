using System.Collections.Generic;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// A building's solid shell as the game itself has it: the box colliders of its parts (the ground floor, the upper
    /// floors), read straight from the components into the building's own axes, the street at its door at height zero. No
    /// physics query is asked, so a building the camera has faded (its parts then sit on the Default layer) reads the same,
    /// and a door marker that stands out on the pavement does not hide which wall it is in. Cost: nothing per frame; a few
    /// component reads when a site is set out or chosen.
    /// </summary>
    internal static class BuildingShell
    {
        private const int MostBoxes = 24;            // boxes read from one building: its parts carry one or two each
        private const float SolidHeight = 1f;        // metres: a box lower than this is a plane or a plinth, not a wall

        private static readonly ShellBox[] Boxes = new ShellBox[MostBoxes];
        private static readonly List<BoxCollider> Colliders = new List<BoxCollider>(8);
        private static readonly Vector3[] Corners = new Vector3[8];

        /// <summary>
        /// Reads a building into <paramref name="shell"/>: its axes are its main part's (origin at its door's street level,
        /// turned by that part's yaw). False with nothing read if it has no solid box.
        /// </summary>
        public static bool Read(CityBuildingController building, WrapShell shell, out Vector3 origin, out Quaternion turn)
        {
            origin = default;
            turn = Quaternion.identity;
            shell.Valid = false;
            if (building == null || building.cityBuildingParts == null)
            {
                return false;
            }
            Transform axes = null;
            for (int i = 0; i < building.cityBuildingParts.Count && axes == null; i++)
            {
                ViewBlockingEntityPart part = building.cityBuildingParts[i];
                if (part != null && Solid(part.transform))
                {
                    axes = part.transform;
                }
            }
            if (axes == null)
            {
                return false;
            }
            Vector3 street = CivicCity.HasDoor(building) ? building.entranceDoors[0].doorTransform.position : axes.position;
            origin = new Vector3(axes.position.x, street.y, axes.position.z);
            turn = Quaternion.Euler(0f, axes.eulerAngles.y, 0f);
            Quaternion back = Quaternion.Inverse(turn);
            int count = 0;
            for (int i = 0; i < building.cityBuildingParts.Count && count < MostBoxes; i++)
            {
                ViewBlockingEntityPart part = building.cityBuildingParts[i];
                if (part == null)
                {
                    continue;
                }
                Colliders.Clear();
                part.GetComponents(Colliders);
                for (int c = 0; c < Colliders.Count && count < MostBoxes; c++)
                {
                    BoxCollider box = Colliders[c];
                    if (box == null || box.isTrigger)
                    {
                        continue;
                    }
                    Boxes[count++] = BoxOf(box, origin, back);
                }
            }
            Colliders.Clear();
            WrapRules.Measure(Boxes, count, shell);
            return shell.Valid;
        }

        // A part with a solid box a storey high gives the building its axes.
        private static bool Solid(Transform part)
        {
            Colliders.Clear();
            part.GetComponents(Colliders);
            bool solid = false;
            for (int c = 0; c < Colliders.Count && !solid; c++)
            {
                solid = Colliders[c] != null && !Colliders[c].isTrigger && Colliders[c].size.y * part.lossyScale.y >= SolidHeight;
            }
            Colliders.Clear();
            return solid;
        }

        // The eight corners of a collider's box taken into the building's axes, and the box round them.
        private static ShellBox BoxOf(BoxCollider box, Vector3 origin, Quaternion back)
        {
            Transform t = box.transform;
            Vector3 c = box.center, h = box.size * 0.5f;
            int k = 0;
            for (int x = -1; x <= 1; x += 2)
            {
                for (int y = -1; y <= 1; y += 2)
                {
                    for (int z = -1; z <= 1; z += 2)
                    {
                        Corners[k++] = back * (t.TransformPoint(c + new Vector3(h.x * x, h.y * y, h.z * z)) - origin);
                    }
                }
            }
            Vector3 min = Corners[0], max = Corners[0];
            for (int i = 1; i < 8; i++)
            {
                min = Vector3.Min(min, Corners[i]);
                max = Vector3.Max(max, Corners[i]);
            }
            return new ShellBox(min.x, max.x, min.y, max.y, min.z, max.z);
        }

        /// <summary>A point of the building's axes in the world.</summary>
        public static Vector3 World(Vector3 origin, Quaternion turn, float x, float y, float z) => origin + turn * new Vector3(x, y, z);

        /// <summary>A world point in the building's axes.</summary>
        public static Vector3 Local(Vector3 origin, Quaternion turn, Vector3 world) => Quaternion.Inverse(turn) * (world - origin);

        /// <summary>A direction of the building's axes in the world.</summary>
        public static Vector3 Direction(Quaternion turn, float x, float z) => turn * new Vector3(x, 0f, z);
    }
}
