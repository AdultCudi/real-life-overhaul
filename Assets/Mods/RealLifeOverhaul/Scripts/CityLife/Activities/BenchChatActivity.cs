using UnityEngine;
using UnityEngine.AI;

namespace RealLifeOverhaul
{
    /// <summary>
    /// Two friends talking on a bench the mod stands with its back to a wall: <c>sit_chat_a</c> turned to the other,
    /// <c>sit_chat_b</c> listening. The bench (<c>precinct_bench</c>: seat 0.46 m up, the sitter facing its +Z) fixes the
    /// clips' seat height and the 0.55 m between the two. Both are seated or neither is.
    /// </summary>
    public sealed class BenchChatActivity : CityActivity
    {
        private const string Bench = "precinct_bench";
        private const float FromWall = 0.45f;     // the bench's pivot from the wall: its back (0.275 m behind the pivot) stands clear of it
        private const float SeatAhead = 0.03f;    // metres, the seat's middle ahead of the bench's pivot
        private const float Apart = 0.55f;        // sit_chat_a / sit_chat_b: the other one this far to the side
        private const float HalfLength = 0.90f;   // the bench is 1.80 m long

        private Vector3 _bench, _talker, _listener, _outward;
        private float _heading;

        public override bool Find(CitySite site)
        {
            if (!CitySpots.Shopfront(site.Anchor, FromWall, 0.9f, out _bench, out _outward))
            {
                return false;
            }
            Vector3 right = Vector3.Cross(Vector3.up, _outward);
            Vector3 seat = _bench + _outward * SeatAhead;
            // Pavement under both ends of the bench, under both of them, and in front of their knees.
            if (!CitySpots.OnPavement(_bench + right * HalfLength, out _) || !CitySpots.OnPavement(_bench - right * HalfLength, out _)
                || !CitySpots.OnPavement(seat + right * (Apart * 0.5f), out _) || !CitySpots.OnPavement(seat - right * (Apart * 0.5f), out _)
                || !CitySpots.OnPavement(seat + _outward * 0.7f, out _))
            {
                return false;
            }
            // sit_chat_a is turned to somebody on its LEFT, sit_chat_b to somebody on its RIGHT: the talker sits on the right.
            _talker = seat + right * (Apart * 0.5f);
            _listener = seat - right * (Apart * 0.5f);
            _heading = Quaternion.LookRotation(_outward).eulerAngles.y;
            Centre = _bench;
            return true;
        }

        public override bool Begin(CitySite site)
        {
            Start(site);
            if (!Put(Bench, _bench, _heading) || Add(CityLook.Civilian, _talker, _talker + _outward) == null || Add(CityLook.Civilian, _listener, _listener + _outward) == null)
            {
                End();
                return false;
            }
            // People walking by go round the bench; nothing is cut out of the mesh (the two sit on it), as at the chess table.
            GameObject bench = Dressing[0].gameObject;
            if (!bench.TryGetComponent(out NavMeshObstacle solid))
            {
                solid = bench.AddComponent<NavMeshObstacle>();
                solid.shape = NavMeshObstacleShape.Box;
                solid.center = new Vector3(0f, 0.45f, 0.1f);
                solid.size = new Vector3(1.8f, 0.9f, 0.9f);
                solid.carving = false;
            }
            if (!Cast[0].Work("sit_chat_a", Props, 0f) || !Cast[1].Work("sit_chat_b", Props, 0f, ClipEnd.Return, true, 2f))
            {
                site.Refusal = CityRefusal.NoProp;
                End();
                return false;
            }
            return true;
        }

        public override bool Tick(float now)
        {
            return Lasting();
        }
    }
}
