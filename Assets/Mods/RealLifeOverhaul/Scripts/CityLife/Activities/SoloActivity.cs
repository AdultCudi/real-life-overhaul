using BigAmbitions.Characters;
using UnityEngine;
using Random = UnityEngine.Random;

namespace RealLifeOverhaul
{
    /// <summary>Where somebody working alone stands.</summary>
    public enum SoloPlace
    {
        Open,        // a clear piece of pavement
        Shopfront,   // facing a wall, at a measured distance from it
        BackToWall   // the same wall behind them, looking out at the street
    }

    /// <summary>A prop on the ground beside somebody, in their frame: metres to their right and ahead, and its heading.</summary>
    public readonly struct SoloProp
    {
        public readonly string Id;
        public readonly float Right, Ahead, Heading;

        public SoloProp(string id, float right, float ahead, float heading)
        {
            Id = id;
            Right = right;
            Ahead = ahead;
            Heading = heading;
        }
    }

    /// <summary>What one person alone does: where they stand, what they wear, the clip they work to and how they vary it; one per catalogue entry.</summary>
    public sealed class SoloSpec
    {
        public CityLook Look = CityLook.Civilian;
        public SoloPlace Place = SoloPlace.Open;
        public float Wall = 0.5f;          // metres between the feet and the wall, for the two wall placements
        public float Room = 0.6f;          // metres of clear radius round them
        public float Ahead;                // metres of clear pavement they need in front (where the broom goes)

        public string Work;                // the looping clip; null leaves them to the game's own idle
        public string Rest;                // a second looping clip with the same tool, alternated with Work
        public float WorkMin = 10f, WorkMax = 18f;   // seconds
        public float RestMin = 4f, RestMax = 7f;   // seconds

        public string Carry;               // a clip whose tool is in the other hand from the start
        public string Greet;               // a one-off clip, played when someone on foot comes by in front
        public string Now;                 // a one-off clip, played every so often (a photo, a sip)
        public float NowMin = 9f, NowMax = 16f;   // seconds
        public bool HoldNow;               // between one-offs the last pose of Now is held (a carton on the palm), not the game's idle
        public string Ring;                // with a game pose that is a phone call: it ends now and then, and this one-off answers the next

        public bool UsesGamePose;          // instead of Work: one of the game's own looping poses
        public PermanentAnimationType GamePose;
        public string GameProp;

        public SoloProp[] Props;
    }

    /// <summary>One person, one spot, a loop with variation (a sweeper, a window cleaner, a leafleter); all that differs is in the <see cref="SoloSpec"/>.</summary>
    public sealed class SoloActivity : CityActivity
    {
        private enum Doing { Working, Resting, Once }

        private readonly SoloSpec _spec;
        private readonly Vector3[] _propSpots;
        private readonly float[] _propHeadings;
        private Vector3 _stand;
        private Vector3 _face;
        private CityPerson _person;
        private Doing _doing;
        private float _until;
        private float _nextOnce;
        private float _nextGreet;
        private bool _rang;                 // the one-off playing is Ring: its phone goes when the game's pose takes over

        public SoloActivity(SoloSpec spec)
        {
            _spec = spec;
            int props = spec.Props != null ? spec.Props.Length : 0;
            _propSpots = new Vector3[props];
            _propHeadings = new float[props];
        }

        public override bool Find(CitySite site)
        {
            Vector3 forward;
            if (_spec.Place == SoloPlace.Open)
            {
                if (!CitySpots.Stand(site.Anchor, _spec.Room, out _stand))
                {
                    return false;
                }
                // Along the pavement where that can be told, either way, with room for the work in front.
                if (!CitySpots.Along(_stand, out forward))
                {
                    forward = Quaternion.Euler(0f, Random.value * 360f, 0f) * Vector3.forward;
                }
                if (Random.value < 0.5f)
                {
                    forward = -forward;
                }
                if (!RoomAhead(forward))
                {
                    forward = -forward;
                    if (!RoomAhead(forward))
                    {
                        return false;
                    }
                }
            }
            else
            {
                if (!CitySpots.Shopfront(site.Anchor, _spec.Wall, _spec.Room, out _stand, out Vector3 outward))
                {
                    return false;
                }
                forward = _spec.Place == SoloPlace.Shopfront ? -outward : outward;
            }
            _face = _stand + forward;
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            float heading = Quaternion.LookRotation(forward).eulerAngles.y;
            for (int i = 0; i < _propSpots.Length; i++)
            {
                SoloProp prop = _spec.Props[i];
                if (!CitySpots.OnPavement(_stand + right * prop.Right + forward * prop.Ahead, out _propSpots[i]))
                {
                    return false;
                }
                _propHeadings[i] = heading + prop.Heading;
            }
            Centre = _stand;
            return true;
        }

        public override bool Begin(CitySite site)
        {
            Start(site);
            _person = Add(_spec.Look, _stand, _face);
            if (_person == null)
            {
                return false;
            }
            for (int i = 0; i < _propSpots.Length; i++)
            {
                if (!Put(_spec.Props[i].Id, _propSpots[i], _propHeadings[i]))
                {
                    End();
                    return false;
                }
            }
            // The tools first: an activity whose tool cannot be had does not happen.
            if ((_spec.Carry != null && !_person.Carry(_spec.Carry, Props)) || !Settle(0f))
            {
                End();
                return false;
            }
            _nextOnce = In(_spec.NowMin * 0.4f, _spec.NowMax);
            _nextGreet = 0f;
            return true;
        }

        public override bool Tick(float now)
        {
            if (!Lasting())
            {
                return false;
            }
            if (_person.Lost || Over)
            {
                return true;   // carrying on as they are until nobody is looking
            }
            switch (_doing)
            {
                case Doing.Once:
                    if (now >= _until)
                    {
                        if (_spec.HoldNow && !_rang)
                        {
                            _doing = Doing.Working;   // the one-off's last pose is held: that is how they stand until the next
                        }
                        else
                        {
                            Settle(0.35f);
                        }
                    }
                    break;

                case Doing.Resting:
                    if (now >= _until)
                    {
                        // The phone again: out of the pocket and to the ear, then the game's own call.
                        if (_spec.Ring != null && _person.Work(_spec.Ring, Props, 0.25f, ClipEnd.Hold) && ClipLibrary.TryGet(_spec.Ring, out CityClip ring))
                        {
                            _rang = true;
                            _doing = Doing.Once;
                            _until = now + ring.Length;
                        }
                        else
                        {
                            Settle(0.45f);
                        }
                    }
                    break;

                default:
                    if (_spec.Greet != null && now >= _nextGreet && SomeoneAhead(_person, 4.2f, 0.6f))
                    {
                        Once(_spec.Greet, now);
                        _nextGreet = now + Random.Range(5f, 9f);
                    }
                    else if (_spec.Now != null && now >= _nextOnce)
                    {
                        Once(_spec.Now, now);
                        _nextOnce = now + Random.Range(_spec.NowMin, _spec.NowMax);
                    }
                    else if (_spec.Rest != null && now >= _until && _person.Work(_spec.Rest, Props, 0.45f))
                    {
                        _doing = Doing.Resting;
                        _until = now + Random.Range(_spec.RestMin, _spec.RestMax);
                    }
                    else if (_spec.Ring != null && _spec.UsesGamePose && now >= _until)
                    {
                        // The call is over: the game's pose and its phone go, and they stand a while before it rings again.
                        _person.ClearPose();
                        _person.Idle(0.35f);
                        _doing = Doing.Resting;
                        _until = now + Random.Range(_spec.RestMin, _spec.RestMax);
                    }
                    break;
            }
            return true;
        }

        public override void End()
        {
            _person = null;
            _rang = false;
            base.End();
        }

        // Back to the main loop, the game's pose or its idle; false only if the main loop cannot be played.
        private bool Settle(float fade)
        {
            _doing = Doing.Working;
            _until = In(_spec.WorkMin, _spec.WorkMax);
            if (_spec.UsesGamePose)
            {
                if (_rang)
                {
                    _rang = false;
                    _person.PutAway(Props);   // the clip's phone: the game's pose brings its own
                }
                return _person.Pose(_spec.GamePose, _spec.GameProp);
            }
            if (_spec.HoldNow && _spec.Now != null)
            {
                // What they hold is held as the one-off holds it from the start: its first bite or sip, then its last pose.
                if (!_person.Work(_spec.Now, Props, fade, ClipEnd.Hold) || !ClipLibrary.TryGet(_spec.Now, out CityClip held))
                {
                    return false;
                }
                _doing = Doing.Once;
                _until = In(held.Length, held.Length);
                return true;
            }
            if (_spec.Work == null)
            {
                _person.Idle(fade);
                return true;
            }
            return _person.Work(_spec.Work, Props, fade);
        }

        private void Once(string clip, float now)
        {
            // Held on its last pose, the main loop's own, until the next tick fades the loop back in: the game's idle in between
            // would drop the arms, tool and all.
            if (_person.Work(clip, Props, 0.3f, ClipEnd.Hold) && ClipLibrary.TryGet(clip, out CityClip found))
            {
                _doing = Doing.Once;
                _until = now + found.Length;
            }
        }

        private bool RoomAhead(Vector3 forward)
        {
            if (_spec.Ahead <= 0f)
            {
                return true;
            }
            return CitySpots.Stand(_stand + forward * (_spec.Ahead * 0.5f), 0.3f, out _) && CitySpots.Stand(_stand + forward * _spec.Ahead, 0.3f, out _);
        }
    }
}
