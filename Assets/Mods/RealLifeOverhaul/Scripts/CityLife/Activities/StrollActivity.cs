using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>What somebody out walking has with them.</summary>
    public enum StrollKind
    {
        Pushchair,   // pushed in front: push_cart's arms over the game's walk
        Umbrella,    // an open umbrella in the right hand, in the rain
        Suitcase     // a cabin case in the right hand on the mod's whole-body walk, kept in step with the ground
    }

    /// <summary>
    /// Somebody walking a loop of pavement with a pushchair, an umbrella in the rain or a cabin case. With the first two the
    /// walk is the game's and the arms a masked clip; the case's whole-body walk, made for 1.3 m/s, plays only while they really
    /// move, kept in step once a tick (<see cref="CityPerson.KeepStep"/>). One path per stretch.
    /// </summary>
    public sealed class StrollActivity : CityActivity
    {
        private const int MaxStops = 4;
        private const float Patience = 70f;      // seconds a stretch may take before the next is tried
        private const float Moving = 0.45f;     // metres a second: faster than this the traveller is walking
        private const float Stopped = 0.2f;     // metres a second: slower than this they are held up, after the grace below
        private const float SetOffGrace = 2f;   // seconds after a new route before a slow agent counts as held up
        private const string CaseWalk = "carry_suitcase_walk";
        private const float HandleAhead = 0.88f; // metres from the feet to the pushchair's middle: its handle is then where push_cart has the hands

        private readonly StrollKind _kind;
        private readonly Vector3[] _loop = new Vector3[MaxStops];
        private Vector3 _start;
        private int _stops;
        private int _next;
        private float _giveUp;
        private float _setOff;

        public StrollActivity(StrollKind kind)
        {
            _kind = kind;
        }

        public override bool Find(CitySite site)
        {
            if (_kind == StrollKind.Umbrella && !site.Raining && !site.Test)
            {
                site.Refusal = CityRefusal.NotRaining;
                return false;
            }
            if (_kind == StrollKind.Suitcase && !ClipLibrary.Has(CaseWalk))
            {
                site.Refusal = CityRefusal.NoProp;
                return false;
            }
            float room = _kind == StrollKind.Pushchair ? 0.6f : 0.45f;
            if (!CitySpots.Stand(site.Anchor, room, out _start))
            {
                return false;
            }
            if (_kind == StrollKind.Pushchair && CitySpots.Along(_start, out Vector3 along)
                && !CitySpots.Stand(_start + along * HandleAhead, 0.4f, out _) && !CitySpots.Stand(_start - along * HandleAhead, 0.4f, out _))
            {
                return false;   // no room for the pushchair either way along the pavement
            }
            _stops = 0;
            Vector3 from = _start;
            for (int i = 0; i < MaxStops; i++)
            {
                if (!CitySpots.PavementRing(from, 16f, 30f, false, out Vector3 stop))
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
            CityPerson walker = Add(CityLook.Civilian, _start, _loop[0]);
            if (walker == null)
            {
                return false;
            }
            bool ready;
            if (_kind == StrollKind.Pushchair)
            {
                Transform chair = Props.Take("pushchair");
                ready = chair != null && walker.Bring(chair, null, new Vector3(0f, 0f, HandleAhead), Quaternion.identity)
                        && walker.WorkOver("push_cart", Props, 0f, ClipEnd.Return, BodyMask.Arms, false);
                if (!ready && chair != null && chair.parent != walker.Character.transform)
                {
                    Props.Return(chair);
                }
            }
            else if (_kind == StrollKind.Suitcase)
            {
                ready = walker.Carry(CaseWalk, Props);   // its walk plays while they are going
            }
            else
            {
                ready = walker.WorkOver("umbrella_hold", Props, 0f, ClipEnd.Return, BodyMask.RightArm);
            }
            if (!ready)
            {
                site.Refusal = CityRefusal.NoProp;
                End();
                return false;
            }
            _next = 0;
            WalkOn(Time.time);
            return true;
        }

        public override bool Tick(float now)
        {
            CityPerson walker = Cast[0];
            if (!walker.Lost)
            {
                Centre = walker.Position;
                if (walker.Arrived || now >= _giveUp)
                {
                    WalkOn(now);
                }
                else if (_kind == StrollKind.Suitcase)
                {
                    KeepStep(walker, now);
                }
            }
            return Lasting();
        }

        private void WalkOn(float now)
        {
            Cast[0].WalkTo(_loop[_next]);
            _next = (_next + 1) % _stops;
            _giveUp = now + Patience;
            _setOff = now;
            if (_kind == StrollKind.Suitcase)
            {
                Cast[0].Work(CaseWalk, Props, 0.3f);   // WalkTo ended any clip: this is the walk from its first step
            }
        }

        // The traveller's walk follows the ground: its speed set from theirs, and the game's walk while they are held up.
        private void KeepStep(CityPerson walker, float now)
        {
            float speed = walker.Speed;
            if (walker.Playing == null)
            {
                if (speed > Moving)
                {
                    walker.Work(CaseWalk, Props, 0.3f);
                }
            }
            else if (speed < Stopped && now - _setOff > SetOffGrace)
            {
                walker.Idle(0.3f);
            }
            else if (ClipLibrary.TryGet(CaseWalk, out CityClip walk))
            {
                walker.KeepStep(walk.Speed);
            }
        }
    }
}
