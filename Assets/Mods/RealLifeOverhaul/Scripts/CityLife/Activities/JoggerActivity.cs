using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// Somebody running a loop of three or four stretches of pavement at the game's own jogging pace and animation. The only
    /// one of the living city's people who moves, and so the only one that costs a path.
    /// </summary>
    public sealed class JoggerActivity : CityActivity
    {
        private const int MaxStops = 4;
        private const float Patience = 40f;   // seconds a stretch may take before the next is tried

        private readonly Vector3[] _loop = new Vector3[MaxStops];
        private Vector3 _start;
        private int _stops;
        private int _next;
        private float _giveUp;

        public override bool Find(CitySite site)
        {
            if (!CitySpots.Stand(site.Anchor, 0.5f, out _start))
            {
                return false;
            }
            _stops = 0;
            Vector3 from = _start;
            for (int i = 0; i < MaxStops; i++)
            {
                if (!CitySpots.PavementRing(from, 18f, 32f, false, out Vector3 stop))
                {
                    break;
                }
                _loop[_stops++] = stop;
                from = stop;
            }
            Centre = _start;
            return _stops >= 2;
        }

        public override bool Begin(CitySite site)
        {
            Start(site);
            CityPerson runner = Add(CityLook.Civilian, _start, _loop[0]);
            if (runner == null)
            {
                return false;
            }
            _next = 0;
            RunOn(Time.time);
            return true;
        }

        public override bool Tick(float now)
        {
            CityPerson runner = Cast[0];
            if (!runner.Lost)
            {
                Centre = runner.Position;
                if (runner.Arrived || now >= _giveUp)
                {
                    RunOn(now);
                }
            }
            return Lasting();
        }

        private void RunOn(float now)
        {
            Cast[0].WalkTo(_loop[_next], jog: true);
            _next = (_next + 1) % _stops;
            _giveUp = now + Patience;
        }
    }
}
