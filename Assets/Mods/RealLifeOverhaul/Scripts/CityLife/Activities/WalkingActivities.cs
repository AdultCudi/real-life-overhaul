using UnityEngine;
using Random = UnityEngine.Random;

namespace RealLifeOverhaul
{
    /// <summary>What somebody out walking has with them.</summary>
    public enum StrollKind
    {
        Pushchair,   // pushed in front: the arms of push_cart over the game's walk
        Umbrella     // an open umbrella in the right hand: in the rain
    }

    /// <summary>
    /// Somebody walking the pavements with something: a parent with a pushchair, or in the rain
    /// a person under an umbrella. They go round a loop of three or four stretches of pavement at
    /// the game's own walking pace with the game's own walk; only the arms are the mod's (a
    /// masked clip), so nothing slides. Like the jogger, this costs one path per stretch.
    /// </summary>
    public sealed class StrollActivity : CityActivity
    {
        private const int MaxStops = 4;
        private const float Patience = 70f;      // seconds a stretch may take before the next is tried
        private const float HandleAhead = 0.88f; // the pushchair's middle ahead of the feet: its handle is then 0.44 m ahead, as push_cart has the hands

        private readonly StrollKind _kind;
        private readonly Vector3[] _loop = new Vector3[MaxStops];
        private Vector3 _start;
        private int _stops;
        private int _next;
        private float _giveUp;

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
            float room = _kind == StrollKind.Pushchair ? 0.6f : 0.45f;
            if (!CitySpots.Stand(site.Anchor, room, out _start))
            {
                return false;
            }
            if (_kind == StrollKind.Pushchair && CitySpots.Along(_start, out Vector3 along) && !CitySpots.Stand(_start + along * HandleAhead, 0.4f, out _) && !CitySpots.Stand(_start - along * HandleAhead, 0.4f, out _))
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
            }
            return Lasting();
        }

        private void WalkOn(float now)
        {
            Cast[0].WalkTo(_loop[_next]);
            _next = (_next + 1) % _stops;
            _giveUp = now + Patience;
        }
    }

    /// <summary>
    /// The post. A mail carrier with a satchel WALKS IN from out of view and works along the
    /// block: at each of two or three shopfronts they stop square to the wall, take a letter out
    /// of the satchel and post it, and walk on to the next; after the last they walk away out of
    /// view. Each stop is a measured place at a real wall.
    /// </summary>
    public sealed class MailActivity : CityActivity
    {
        private const int MaxStops = 3;
        private const float SlotAhead = 0.52f;   // the letter box ahead of the feet (mail_deliver)

        private readonly Vector3[] _stops = new Vector3[MaxStops];
        private readonly Vector3[] _walls = new Vector3[MaxStops];
        private CityPerson _carrier;
        private int _count;
        private int _at;
        private const float Patience = 70f;   // seconds a walk to a stop may take

        private bool _posting;
        private float _until;
        private float _giveUp;

        public override bool Find(CitySite site)
        {
            _count = 0;
            if (!CitySpots.Shopfront(site.Anchor, SlotAhead, 0.4f, out _stops[0], out _walls[0]))
            {
                return false;
            }
            _count = 1;
            // Further stops along the same building line, either way.
            Vector3 along = Vector3.Cross(Vector3.up, _walls[0]);
            for (int k = 0; k < 4 && _count < MaxStops; k++)
            {
                Vector3 near = _stops[0] + along * ((k % 2 == 0 ? 1f : -1f) * (6f + 5f * (k / 2)));
                if (CitySpots.Shopfront(near, SlotAhead, 0.4f, out Vector3 stop, out Vector3 wall) && Apart(stop))
                {
                    _stops[_count] = stop;
                    _walls[_count] = wall;
                    _count++;
                }
            }
            Centre = _stops[0];
            return _count >= 2;
        }

        public override bool Begin(CitySite site)
        {
            Start(site);
            if (!ClipLibrary.Has("mail_deliver"))
            {
                site.Refusal = CityRefusal.NoProp;
                return false;
            }
            Transform satchel = Props.Take("mail_satchel");
            _carrier = satchel != null ? Arrive(site, CityLook.Postie, _stops[0]) : null;
            if (_carrier == null || !Wear(satchel))
            {
                if (satchel != null)
                {
                    Props.Return(satchel);
                }
                if (site.Refusal == CityRefusal.None)
                {
                    site.Refusal = CityRefusal.NoProp;
                }
                End();
                return false;
            }
            _at = 0;
            _posting = false;
            _giveUp = Time.time + Patience;
            return true;
        }

        public override bool Tick(float now)
        {
            TickDepartures();
            if (_carrier == null || (_at <= _count && _carrier.Lost))
            {
                Over = true;
            }
            if (_at > _count)
            {
                return Cast.Contains(_carrier);   // gone out of view and back in the pool: the round is over
            }
            if (Over)
            {
                return CitySpots.InView(Centre);
            }
            if (_at == _count)
            {
                // The round is done: away.
                Depart(_carrier);
                _at++;
                return true;
            }
            if (!_posting)
            {
                if (now >= _giveUp)
                {
                    // Never got there: on to the next stop (or away).
                    _at++;
                    _giveUp = now + Patience;
                    if (_at < _count)
                    {
                        _carrier.WalkTo(_stops[_at]);
                    }
                }
                else if (_carrier.Arrived && !_carrier.Near(_stops[_at], 0.5f))
                {
                    _carrier.WalkTo(_stops[_at]);   // stopped short: on again
                }
                else if (_carrier.Arrived)
                {
                    _carrier.Stand();
                    _carrier.Face(_stops[_at] - _walls[_at]);
                    _posting = _carrier.Work("mail_deliver", Props, 0.3f);
                    _until = now + (_posting ? 3.4f : 1f);
                    Centre = _stops[_at];
                }
            }
            else if (now >= _until)
            {
                _posting = false;
                _at++;
                _giveUp = now + Patience;
                if (_at < _count)
                {
                    _carrier.WalkTo(_stops[_at]);
                }
            }
            return true;
        }

        public override void End()
        {
            _carrier = null;
            base.End();
        }

        // The satchel is modelled to hang from the upper chest; a rig without that bone carries it on the chest.
        private bool Wear(Transform satchel)
        {
            Animator animator = _carrier.Character.animator;
            if (animator != null && animator.GetBoneTransform(HumanBodyBones.UpperChest) != null)
            {
                return _carrier.Bring(satchel, HumanBodyBones.UpperChest, Vector3.zero, Quaternion.identity);
            }
            return _carrier.Bring(satchel, HumanBodyBones.Chest, new Vector3(0f, -0.09f, 0f), Quaternion.identity);
        }

        private bool Apart(Vector3 stop)
        {
            for (int i = 0; i < _count; i++)
            {
                if (CitySpots.Flat(_stops[i] - stop) < 3.5f * 3.5f)
                {
                    return false;
                }
            }
            return true;
        }
    }
}
