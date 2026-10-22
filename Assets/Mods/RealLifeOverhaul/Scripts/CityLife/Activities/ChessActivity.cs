using Helpers;
using UnityEngine;
using UnityEngine.AI;
using Random = UnityEngine.Random;

namespace RealLifeOverhaul
{
    /// <summary>
    /// Two people at a chess table the mod stands on a wide piece of pavement, so the clip's seat height (0.45 m) and reach
    /// to the board are known. Both play the same loop from different moments, so one thinks while the other moves. Two are
    /// seated or neither is, each kept facing the board's centre (<see cref="CityPerson.AtTable"/>).
    /// </summary>
    public sealed class ChessActivity : CityActivity
    {
        private const float StoolOut = 0.62f;   // metres from the table's centre to a stool's (chess_sit)

        private Vector3 _table, _first, _second;
        private float _heading;

        public override bool Find(CitySite site)
        {
            if (!CitySpots.Stand(site.Anchor, 1.25f, out _table))
            {
                return false;
            }
            if (!CitySpots.Along(_table, out Vector3 along))
            {
                along = Quaternion.Euler(0f, Random.value * 360f, 0f) * Vector3.forward;
            }
            // The stools are the table's +Z and -Z: the players sit along the pavement, out of the way of people passing.
            if (!CitySpots.OnPavement(_table + along * StoolOut, out _first) || !CitySpots.OnPavement(_table - along * StoolOut, out _second)
                || !CitySpots.OnPavement(_table + along * 1.2f, out _) || !CitySpots.OnPavement(_table - along * 1.2f, out _)
                || !CitySpots.ChessClear(_table, along))
            {
                return false;
            }
            _first = _table + along * StoolOut;     // on the stools exactly, not where the mesh rounded them to
            _second = _table - along * StoolOut;
            _heading = Quaternion.LookRotation(along).eulerAngles.y;
            Centre = _table;
            return true;
        }

        public override bool Begin(CitySite site)
        {
            Start(site);
            if (!Put("chess_table", _table, _heading) || Add(CityLook.Civilian, _first, _table) == null || Add(CityLook.Civilian, _second, _table) == null)
            {
                End();
                return false;
            }
            GameObject table = Dressing[0].gameObject;
            if (!table.TryGetComponent(out NavMeshObstacle solid))
            {
                solid = table.AddComponent<NavMeshObstacle>();
                solid.shape = NavMeshObstacleShape.Box;
                solid.center = new Vector3(0f, 0.5f, 0f);
                solid.size = new Vector3(0.8f, 1f, 0.8f);
                solid.carving = false;
            }
            if (!Cast[0].Work("chess_sit", Props, 0f) || !Cast[1].Work("chess_sit", Props, 0f, ClipEnd.Return, true, 5f))
            {
                site.Refusal = CityRefusal.NoProp;
                End();
                return false;
            }
            Cast[0].AtTable(_table);
            Cast[1].AtTable(_table);
            return true;
        }

        public override bool Tick(float now)
        {
            for (int i = 0; i < Cast.Count; i++)
            {
                if (!Cast[i].Lost)
                {
                    Cast[i].KeepAtTable(CitySpots.InView(Cast[i].Position));
                }
            }
            return Lasting();
        }
    }
}
