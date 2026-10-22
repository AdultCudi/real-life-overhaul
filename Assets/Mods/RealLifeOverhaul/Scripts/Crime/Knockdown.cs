using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// One of a scene's people knocked off their feet: a ragdoll for a few seconds, then standing where they landed. With
    /// no ragdoll to be had they stay standing and the scene carries on; letting the person go ends the ragdoll too.
    /// </summary>
    internal sealed class Knockdown
    {
        private Ragdoll _ragdoll;
        private int _serial;
        private ActorRole _role;
        private float _upAt;
        private float _upLatest;

        /// <summary>True while they are on the ground and still ours.</summary>
        public bool Down => _ragdoll != null && _ragdoll.Active && _ragdoll.Serial == _serial;

        /// <summary>Throws someone down, pushed away from a point. False if they are still standing.</summary>
        public bool Begin(ActorRole role, Vector3 pushedFrom, float now, float force = 2.6f)
        {
            Clear();
            RagdollSystem ragdolls = RagdollSystem.Instance;
            if (ragdolls == null || !role.Present)
            {
                return false;
            }
            Vector3 feet = role.Position;
            Vector3 away = feet - pushedFrom;
            away.y = 0f;
            away = away.sqrMagnitude > 0.01f ? away.normalized : Vector3.forward;

            role.GoLimp();   // drawn whether in view or not
            ThirdPersonCharacter character = role.Character;
            Ragdoll ragdoll = character != null && ragdolls.CanBegin(character)
                ? ragdolls.Begin(character, away * force + Vector3.up * 1.4f)
                : null;
            if (ragdoll == null)
            {
                role.Stand(pushedFrom);
                return false;
            }
            _ragdoll = ragdoll;
            _serial = ragdoll.Serial;
            _role = role;
            _upAt = now + Random.Range(3.5f, 5.5f);
            _upLatest = now + 11f;
            return true;
        }

        /// <summary>Call while someone is down. True once they are off the ground: standing again, or gone.</summary>
        public bool Tick(float now)
        {
            if (_ragdoll == null)
            {
                return true;
            }
            if (!Down || _ragdoll.Astray)
            {
                // Ended by somebody else (the budget, an error), or it fell out of the world.
                ActorRole lost = _role;
                Clear();
                lost?.Exit();
                return true;
            }
            if (now < _upLatest && (now < _upAt || !_ragdoll.Resting))
            {
                return false;
            }

            // There is no getting-up animation: the ragdoll ends and they stand where they landed.
            Ragdoll ragdoll = _ragdoll;
            ActorRole role = _role;
            _ragdoll = null;
            _role = null;
            if (!StreetSpots.OnRoad(ragdoll.Position, 3f, out Vector3 at))
            {
                at = ragdoll.Origin;
            }
            RagdollSystem.Instance.End(ragdoll, true, at, false);
            if (!role.Rise(at))
            {
                role.Exit();
            }
            return true;
        }

        /// <summary>Ends the ragdoll at once, with the character whole again. Safe at any time.</summary>
        public void Clear()
        {
            if (Down && RagdollSystem.Instance != null)
            {
                RagdollSystem.Instance.End(_ragdoll, false, default, true);
            }
            _ragdoll = null;
            _role = null;
        }
    }
}
