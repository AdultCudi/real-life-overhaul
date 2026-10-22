using Helpers;
using UnityEngine;
using UnityEngine.AI;
using Random = UnityEngine.Random;

namespace RealLifeOverhaul
{
    /// <summary>
    /// A door at night: somebody on it with folded arms and a queue of two or three along the wall waiting to be let in,
    /// each in a measured place.
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
}
