using Helpers;
using UnityEngine;
using UnityEngine.AI;
using Random = UnityEngine.Random;

namespace RealLifeOverhaul
{
    /// <summary>
    /// Two people at a chess table on a wide piece of pavement: the table and its two stools are
    /// one prop the mod stands there, so the seat height the clip sits at (0.45 m) and the reach
    /// to the board are known. Each plays the same loop from a different moment, so one thinks
    /// while the other moves. Two are seated or neither is, each facing the board's centre from
    /// their stool and kept so once a second (CityPerson.AtTable).
    /// </summary>
    public sealed class ChessActivity : CityActivity
    {
        private const float StoolOut = 0.62f;   // a stool's centre from the table's (chess_sit)

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
            // The stools are the table's own +Z and -Z; the players sit along the pavement, out of the way of people passing.
            if (!CitySpots.OnPavement(_table + along * StoolOut, out _first) || !CitySpots.OnPavement(_table - along * StoolOut, out _second)
                || !CitySpots.OnPavement(_table + along * 1.2f, out _) || !CitySpots.OnPavement(_table - along * 1.2f, out _)
                || !CitySpots.ChessClear(_table, along))
            {
                return false;
            }
            _first = _table + along * StoolOut;     // exactly on the stools, not where the mesh rounded them to
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

    /// <summary>
    /// Somebody leaning on a parked car with folded arms, waiting: a driver between fares. The
    /// car is one the game has parked; the place is on the pavement side of it, and the person's
    /// back is the clip's 0.14 m from its side, measured, or they are not put there.
    /// </summary>
    public sealed class CarLeanActivity : CityActivity
    {
        private const float Gap = 0.14f;        // the car's side behind the feet (lean_wait)
        private const float GapSlack = 0.07f;

        private Vector3 _stand, _face;

        public override bool Find(CitySite site)
        {
            if (!CrimeCars.FindParked(site.Anchor, 14f, out Vector3 centre, out Vector3 forward, out float halfWidth))
            {
                site.Refusal = CityRefusal.NoParkedCar;
                return false;
            }
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            for (int side = -1; side <= 1; side += 2)
            {
                Vector3 out_ = right * side;
                for (int k = -1; k <= 1; k++)
                {
                    // By the front door, the middle, the back door.
                    Vector3 wanted = centre + out_ * (halfWidth + Gap) + forward * (k * 0.7f);
                    if (!CitySpots.Pavement(wanted, 0.3f, out Vector3 spot))
                    {
                        continue;
                    }
                    // Measured to the bodywork itself at hip height, not to a box round the car.
                    if (!Physics.Raycast(spot + Vector3.up * 0.9f, -out_, out RaycastHit body, 0.6f, 1 << LayerHelper.ParkedVehiclesLayerIndex, QueryTriggerInteraction.Ignore)
                        || Mathf.Abs(body.distance - Gap) > GapSlack || Vector3.Dot(body.normal, out_) < 0.85f || !CitySpots.Stand(spot + out_ * 0.45f, 0.3f, out _))
                    {
                        continue;   // not the measured distance from real bodywork, or no clear pavement in front of them
                    }
                    _stand = spot;
                    _face = spot + out_;
                    Centre = spot;
                    return true;
                }
            }
            site.Refusal = CityRefusal.NoParkedCar;
            return false;
        }

        public override bool Begin(CitySite site)
        {
            Start(site);
            CityPerson driver = Add(CityLook.Civilian, _stand, _face);
            if (driver == null)
            {
                return false;
            }
            if (!driver.Work("lean_wait", Props, 0f))
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

    /// <summary>
    /// A door at night: somebody on it with folded arms, back to the wall, and a short queue
    /// along the wall waiting to be let in. Everybody has a measured place; with room for fewer
    /// than two in the queue there is no queue here.
    /// </summary>
    public sealed class DoorQueueActivity : CityActivity
    {
        private const int QueueMost = 3;

        private readonly Vector3[] _queue = new Vector3[QueueMost];
        private Vector3 _door, _out, _along;
        private int _count;

        public override bool Find(CitySite site)
        {
            if (!CitySpots.Shopfront(site.Anchor, 0.55f, 0.4f, out _door, out _out))
            {
                return false;
            }
            Vector3 along = Vector3.Cross(Vector3.up, _out);
            for (int side = 0; side < 2; side++)
            {
                _along = side == 0 ? along : -along;
                _count = 0;
                for (int i = 0; i < QueueMost; i++)
                {
                    if (!CitySpots.Stand(_door + _along * (1.35f + 0.85f * i) + _out * 0.2f, 0.3f, out _queue[_count]))
                    {
                        break;
                    }
                    _count++;
                }
                if (_count >= 2)
                {
                    Centre = _door;
                    return true;
                }
            }
            return false;
        }

        public override bool Begin(CitySite site)
        {
            Start(site);
            CityPerson door = Add(CityLook.Doorman, _door, _door + _out + _along * 0.6f);
            if (door == null || !door.Work("arms_crossed", Props, 0f))
            {
                End();
                return false;
            }
            for (int i = 0; i < _count; i++)
            {
                CityPerson waiting = Add(CityLook.Civilian, _queue[i], i == 0 ? _door : _queue[i - 1]);
                if (waiting == null)
                {
                    break;   // the pool ran out: a shorter queue
                }
                waiting.Work("queue_wait", Props, 0f, ClipEnd.Return, true, i * 3.1f);
            }
            if (Cast.Count < 3)
            {
                site.Refusal = CityRefusal.NoPeople;
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

    /// <summary>
    /// A gardener at one of the game's own street trees: watering its foot from a can, or raking
    /// round it. The tree is found (the game keeps a list of them), and the gardener's place is
    /// a tested piece of pavement a step from the trunk, facing it.
    /// </summary>
    public sealed class TreeCareActivity : CityActivity
    {
        private const float Reach = 16f;
        private const float FromTrunk = 1.15f;

        private Vector3 _stand, _tree;

        public override bool Find(CitySite site)
        {
            var trees = TreeController.AllTrees;
            TreeController nearest = null;
            float best = Reach * Reach;
            for (int i = 0; i < trees.Count; i++)
            {
                TreeController tree = trees[i];
                if (tree == null || !tree.gameObject.activeInHierarchy)
                {
                    continue;
                }
                float sqr = CitySpots.Flat(tree.transform.position - site.Anchor);
                if (sqr < best)
                {
                    best = sqr;
                    nearest = tree;
                }
            }
            if (nearest == null)
            {
                site.Refusal = CityRefusal.NoTree;
                return false;
            }
            _tree = nearest.transform.position;
            float start = Random.value * 360f;
            for (int i = 0; i < 8; i++)
            {
                Vector3 from = Quaternion.Euler(0f, start + i * 45f, 0f) * Vector3.forward;
                if (CitySpots.Stand(_tree + from * FromTrunk, 0.4f, out _stand) && Mathf.Abs(Mathf.Sqrt(CitySpots.Flat(_stand - _tree)) - FromTrunk) < 0.2f)
                {
                    Centre = _stand;
                    return true;
                }
            }
            return false;
        }

        public override bool Begin(CitySite site)
        {
            Start(site);
            CityPerson gardener = Add(CityLook.Gardener, _stand, _tree);
            if (gardener == null)
            {
                return false;
            }
            if (!gardener.Work(Random.value < 0.5f ? "water_plants" : "rake_leaves", Props, 0f))
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
