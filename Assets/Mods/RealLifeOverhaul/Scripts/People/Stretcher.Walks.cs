using System.Globalization;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>The crew's few steps beside the stretcher, walked by it with no route asked.</summary>
    public sealed partial class Stretcher : MonoBehaviour
    {
        private const int Walkers = 2;

        private readonly ActorRole[] _walker = new ActorRole[Walkers];

        private readonly Vector3[] _walkBy = new Vector3[Walkers * 3];   // by way of one place or two (if any), then to

        private readonly int[] _walkPlaces = new int[Walkers];

        private readonly int[] _walkNext = new int[Walkers];

        private readonly Vector3[] _walkFace = new Vector3[Walkers];

        private readonly Vector3[] _walkAt = new Vector3[Walkers];

        private readonly float[] _walkSpeed = new float[Walkers];

        /// <summary>
        /// Walks one of the crew straight to a place beside it (round a corner of the bed and the casualty where asked) and
        /// stands them facing a point; no route is asked, so they cannot stop short. False if it is not out or they cannot.
        /// </summary>
        public bool Walk(ActorRole who, bool hasFirst, Vector3 first, bool byWay, Vector3 way, Vector3 to, Vector3 thenFace)
        {
            if (_failed || !Out || who == null || !who.Present)
            {
                return false;
            }
            int slot = -1;
            for (int i = 0; i < Walkers; i++)
            {
                if (_walker[i] == who || (slot < 0 && _walker[i] == null))
                {
                    slot = i;
                    if (_walker[i] == who)
                    {
                        break;
                    }
                }
            }
            if (slot < 0 || !who.Convey())
            {
                return false;
            }
            _walker[slot] = who;
            int places = 0;
            if (hasFirst)
            {
                _walkBy[slot * 3 + places++] = OnGround(first);
            }
            if (byWay)
            {
                _walkBy[slot * 3 + places++] = OnGround(way);
            }
            _walkBy[slot * 3 + places++] = OnGround(to);
            _walkPlaces[slot] = places;
            _walkNext[slot] = 0;
            _walkFace[slot] = thenFace;
            _walkAt[slot] = who.Position;
            _walkSpeed[slot] = 0f;
            enabled = true;
            return true;
        }

        public bool Walking(ActorRole who)
        {
            for (int i = 0; i < Walkers; i++)
            {
                if (_walker[i] != null && _walker[i] == who)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>Whoever is being walked is put at their place at once: only for when nobody can see them.</summary>
        public void HurryWalks()
        {
            for (int i = 0; i < Walkers; i++)
            {
                if (_walker[i] != null)
                {
                    _walkAt[i] = _walkBy[i * 3 + _walkPlaces[i] - 1];
                    _walkNext[i] = _walkPlaces[i];
                    Arrive(i);
                }
            }
        }

        private static Vector3 OnGround(Vector3 place)
        {
            if (StreetSpots.Ground(place, out float ground))
            {
                place.y = ground;
            }
            return place;
        }

        private bool AnyWalking()
        {
            for (int i = 0; i < Walkers; i++)
            {
                if (_walker[i] != null)
                {
                    return true;
                }
            }
            return false;
        }

        private void StepWalks(float dt)
        {
            for (int i = 0; i < Walkers; i++)
            {
                ActorRole who = _walker[i];
                if (who == null)
                {
                    continue;
                }
                ThirdPersonCharacter character = who.Conveyed ? who.Character : null;
                if (character == null)
                {
                    _walker[i] = null;   // gone, or given an order by whoever owns them: no longer ours to walk
                    continue;
                }
                bool last = _walkNext[i] == _walkPlaces[i] - 1;
                Vector3 to = _walkBy[i * 3 + _walkNext[i]];
                Vector3 run = Flat(to - _walkAt[i]);
                float left = run.magnitude;
                float pace = last ? Mathf.Min(SceneClips.WalkPace, Mathf.Max(0.3f, left * 3f)) : SceneClips.WalkPace;
                _walkSpeed[i] = Mathf.MoveTowards(_walkSpeed[i], pace, Quicken * dt);
                float step = _walkSpeed[i] * dt;
                if (left <= step + 0.004f)
                {
                    _walkAt[i] = to;
                    _walkNext[i]++;
                    if (last)
                    {
                        Arrive(i);
                        continue;
                    }
                    who.ConveyTo(to, character.transform.rotation, left > 0.001f ? run / left * _walkSpeed[i] : Vector3.zero);   // round the place they go by without breaking step
                    continue;
                }
                Vector3 way = run / left;
                Vector3 at = _walkAt[i] + way * step;
                at.y = Mathf.Lerp(_walkAt[i].y, to.y, step / left);
                _walkAt[i] = at;
                who.ConveyTo(at, Quaternion.RotateTowards(character.transform.rotation, Quaternion.LookRotation(way, Vector3.up), GuideTurn * dt), way * _walkSpeed[i]);
            }
        }

        private void Arrive(int slot)
        {
            ActorRole who = _walker[slot];
            _walker[slot] = null;
            if (who == null || !who.Conveyed)
            {
                return;
            }
            ThirdPersonCharacter character = who.Character;
            if (character != null)
            {
                who.ConveyTo(_walkAt[slot], character.transform.rotation, Vector3.zero);
            }
            who.Stand(_walkFace[slot]);
        }

        private void EndWalks()
        {
            for (int i = 0; i < Walkers; i++)
            {
                ActorRole who = _walker[i];
                _walker[i] = null;
                if (who != null && who.Conveyed)
                {
                    who.Stand(_walkFace[i]);
                }
            }
        }
    }
}
