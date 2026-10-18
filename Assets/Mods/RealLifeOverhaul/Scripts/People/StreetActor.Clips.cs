using System.Collections.Generic;
using BigAmbitions.Characters;
using Extensions;
using GleyTrafficSystem;
using UnityEngine;
using UnityEngine.AI;

namespace RealLifeOverhaul
{
    /// <summary>The mod's own clips on this character, and being conveyed by whoever moves them frame by frame.</summary>
    public sealed partial class StreetActor
    {
        /// <summary>Plays one of the mod's clips while they stand, until <see cref="ClearPose"/>; false if it cannot be had.</summary>
        public bool Work(string clip, ClipEnd end = ClipEnd.Return, string then = null)
        {
            if (clip == null || Limp || Kneeling)
            {
                return false;
            }
            if (!HasClips())
            {
                return false;
            }
            bool played = _clips.Stand(clip, end, then);
            BagInStep();
            return played;
        }

        public void StowTool()
        {
            if (_clips != null)
            {
                _clips.Stow();
            }
        }

        public void ReleaseClip()
        {
            if (_clips != null)
            {
                _clips.Release();
                BagInStep();
            }
        }

        /// <summary>
        /// How they carry themselves from now on: a walking clip at its own pace (the agent is held to it), optionally a
        /// standing one; null gives back the game's walk. False, walking as the game has them, without the walking clip.
        /// </summary>
        public bool Carriage(string walk, float pace = SceneClips.WalkPace, string idle = null)
        {
            if (walk == null ? _clips == null : Limp || !HasClips())
            {
                return false;
            }
            float before = _clips.Pace;
            bool carried = walk != null && !Limp && _clips.Carry(walk, pace, idle);
            if (!carried)
            {
                _clips.Upright();
            }
            BagInStep();
            float held = _clips.Pace;
            NavMeshAgent agent = Character.navmeshAgent;
            if (held != before && !Limp && agent != null && agent.enabled)
            {
                // Under way already: the pace changes now, not at the next order.
                Character.SetWalkingSpeed(ToGame(held > 0f ? Pace.Walk : _pace));
                if (held > 0f)
                {
                    agent.speed = held;
                }
            }
            return carried;
        }

        public void SyncGait(float now)
        {
            if (_clips == null || _conveyed || (_clips.Pace <= 0f && !_clips.LeadingIn))
            {
                return;   // someone conveyed is told their speed every frame by whoever moves them (ConveyTo)
            }
            NavMeshAgent agent = Character.navmeshAgent;
            _clips.Sync(agent != null && agent.enabled ? agent.velocity.sqrMagnitude : 0f, now);
            BagInStep();
        }

        public void EndClips()
        {
            if (_clips == null)
            {
                return;
            }
            bool paced = _clips.Pace > 0f;
            _clips.End();
            BagInStep();
            NavMeshAgent agent = Character.navmeshAgent;
            if (paced && agent != null && agent.enabled)
            {
                Character.SetWalkingSpeed(ToGame(_pace));
            }
        }

        /// <summary>From now on somebody else puts them where they are every frame (ConveyTo); false for a ragdoll.</summary>
        public bool Convey()
        {
            if (Limp)
            {
                return false;
            }
            MakeMobile();
            NavMeshAgent agent = Character.navmeshAgent;
            if (agent.enabled && agent.isOnNavMesh)
            {
                agent.ResetPath();
                agent.isStopped = false;
                agent.updatePosition = false;
                agent.updateRotation = false;
            }
            _conveyed = true;
            Character.LookTarget = Vector3.zero;
            _pace = Pace.Walk;
            Character.SetWalkingSpeed(ThirdPersonCharacter.WalkingSpeed.Walk);
            Tune(Pace.Walk);
            if (_clips != null)
            {
                _clips.Going(Time.time);
                BagInStep();
            }
            return true;
        }

        private bool _conveyed;   // moved by whoever conveys them, not by their navigation agent

        public void ConveyTo(Vector3 position, Quaternion rotation, Vector3 velocity)
        {
            Character.transform.SetPositionAndRotation(position, rotation);
            NavMeshAgent agent = Character.navmeshAgent;
            if (agent.enabled && agent.isOnNavMesh)
            {
                agent.nextPosition = position;
                agent.velocity = velocity;
            }
            if (_clips != null && (_clips.Pace > 0f || _clips.LeadingIn))
            {
                _clips.Sync(velocity.sqrMagnitude, Time.time);
                BagInStep();
            }
        }

        public bool WalksUnaided
        {
            get
            {
                NavMeshAgent agent = Character.navmeshAgent;
                return agent != null && agent.enabled && agent.isOnNavMesh;
            }
        }

        // No longer conveyed: the agent is warped to where the body is before it moves anything; off the mesh, or when they are
        // to be stood, it is switched off instead, so the body stays exactly where it was put.
        private void Unconvey(bool walksOn)
        {
            if (!_conveyed)
            {
                return;
            }
            _conveyed = false;
            NavMeshAgent agent = Character.navmeshAgent;
            if (agent == null)
            {
                return;
            }
            bool onMesh = agent.enabled && agent.isOnNavMesh;
            if (onMesh)
            {
                agent.velocity = Vector3.zero;
            }
            if (!walksOn || !onMesh || !agent.Warp(Character.transform.position))
            {
                agent.enabled = false;
                _standing = walksOn || _standing;   // MakeMobile switches it on again for whoever is sent somewhere
            }
            agent.updatePosition = true;
            agent.updateRotation = true;
        }

        public void StowBag(Transform onto, Vector3 position, Quaternion rotation)
        {
            if (Kit != null)
            {
                Kit.Stow(onto, position, rotation);
            }
        }

        private bool HasClips()
        {
            if (_clips == null)
            {
                if (!ClipLibrary.Ready)
                {
                    return false;
                }
                _clips = new ActorClips(Character);
                _clips.Show(Character.visible);
                if (!_standing)
                {
                    _clips.Going(Time.time);
                }
            }
            return true;
        }

        // A clip that brings a medic bag of its own holds it at its own grip: the bag of the uniform is not drawn as well.
        private void BagInStep()
        {
            if (Kit != null && _clips != null)
            {
                Kit.InClip(_clips.Tool == BagProp);
            }
        }

        private const string BagProp = "medic_bag";
    }
}
