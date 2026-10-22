using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// A mail carrier with a satchel walks in from out of view, posts a letter at each of two or three measured shopfronts
    /// along the block, and walks away out of view.
    /// </summary>
    public sealed class MailActivity : CityActivity
    {
        private const int MaxStops = 3;
        private const float SlotAhead = 0.52f;   // metres from the feet to the letter box (mail_deliver)
        private const float Patience = 70f;      // seconds a walk to a stop may take

        private readonly Vector3[] _stops = new Vector3[MaxStops];
        private readonly Vector3[] _walls = new Vector3[MaxStops];
        private CityPerson _carrier;
        private int _count;
        private int _at;
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
                Props.Return(satchel);
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
                Depart(_carrier);
                _at++;
                return true;
            }
            if (!_posting)
            {
                if (now >= _giveUp)
                {
                    // Never got there: on to the next stop, or away.
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

        // The satchel is made to hang from the upper chest; a rig without that bone carries it on the chest.
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
