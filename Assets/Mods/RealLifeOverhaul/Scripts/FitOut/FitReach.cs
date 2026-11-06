using Extensions;
using UnityEngine;
using UnityEngine.AI;

namespace RealLifeOverhaul.FitOut
{
    /// <summary>
    /// The player's way on the navigation mesh to a desk, asked as the game asks before it walks the player to something
    /// clicked (a whole path, the player's filter). Cost: a few path searches in one room, as a floor stands or a desk is
    /// clicked; nothing per frame.
    /// </summary>
    internal static class FitReach
    {
        private const float FromRadius = 1.2f;   // metres the start (a door, the player) may be moved to meet the walkable floor

        private static NavMeshPath _path;        // one native path, made once and used again

        private static NavMeshQueryFilter Filter()
        {
            NavMeshQueryFilter filter = PlayerController.navMeshQueryFilter;
            if (filter.areaMask == 0)
            {
                filter = NavMeshHelper.NpcNavMeshFilter;   // no player yet: the walkers' own, the same floor
            }
            return filter;
        }

        /// <summary>True if the player can walk from <paramref name="from"/> to a place near <paramref name="place"/>, which is <paramref name="at"/> on the walkable floor.</summary>
        public static bool Walkable(Vector3 from, Vector3 place, float within, out Vector3 at)
        {
            at = place;
            NavMeshQueryFilter filter = Filter();
            if (!NavMesh.SamplePosition(place, out NavMeshHit end, within, filter) || !NavMesh.SamplePosition(from, out NavMeshHit start, FromRadius, filter))
            {
                return false;
            }
            if (_path == null)
            {
                _path = new NavMeshPath();
            }
            if (!NavMesh.CalculatePath(start.position, end.position, filter, _path) || _path.status != NavMeshPathStatus.PathComplete)
            {
                return false;
            }
            int corners = _path.GetCornersNonAlloc(Corners);
            if (corners <= 0)
            {
                return false;
            }
            Vector3 last = Corners[Mathf.Min(corners, Corners.Length) - 1];
            if (corners <= Corners.Length && !DeskReach.Ends(last.x, last.z, end.position.x, end.position.z))
            {
                return false;
            }
            at = end.position;
            return true;
        }

        private static readonly Vector3[] Corners = new Vector3[64];

        // Metres walked from one place on the floor to another, or -1 if there is no whole way.
        private static float Walked(Vector3 from, Vector3 to)
        {
            if (!NavMesh.CalculatePath(from, to, Filter(), _path) || _path.status != NavMeshPathStatus.PathComplete)
            {
                return -1f;
            }
            int corners = Mathf.Min(_path.GetCornersNonAlloc(Corners), Corners.Length);
            float metres = 0f;
            for (int i = 1; i < corners; i++)
            {
                metres += Vector3.Distance(Corners[i - 1], Corners[i]);
            }
            return metres;
        }

        /// <summary>The first place by the chair (<see cref="DeskReach.Spot"/>) the player can reach from <paramref name="from"/>; false if none.</summary>
        public static bool StandBy(Transform seat, Transform desk, Vector3 from, float floorY, out Vector3 stand)
        {
            stand = Vector3.zero;
            if (seat == null)
            {
                return false;
            }
            Vector3 s = seat.position, middle = desk != null ? desk.TransformPoint(GameFurniture.LocalBounds(desk).center) : s;
            Vector3 face = seat.forward;
            bool seatOnFloor = NavMesh.SamplePosition(new Vector3(s.x, floorY, s.z), out NavMeshHit seatFloor, DeskReach.SeatRadius, Filter());
            for (int n = 0; DeskReach.Spot(n, s.x, s.z, middle.x, middle.z, face.x, face.z, out float x, out float z); n++)
            {
                if (Walkable(from, new Vector3(x, floorY, z), DeskReach.SampleRadius, out stand) && (!seatOnFloor || DeskReach.Beside(Walked(stand, seatFloor.position))))
                {
                    return true;
                }
            }
            stand = Vector3.zero;
            return false;
        }
    }
}
