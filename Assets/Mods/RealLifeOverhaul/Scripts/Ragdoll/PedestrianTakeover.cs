using System;
using Extensions;
using Streets.Pedestrians;
using UnityEngine;
using UnityEngine.AI;

namespace RealLifeOverhaul
{
    /// <summary>
    /// One of the game's pooled pedestrians, borrowed: its walking, standing and range check are switched off the way the
    /// pool does it, so nothing fights the ragdoll. It goes back walking (HandBack), through the pool's own release
    /// (Remove), or is let go when the game already has it (Forget); its agent type is put back on every one of those.
    /// </summary>
    public sealed class PedestrianTakeover
    {
        private Pedestrian _pedestrian;
        private int _ownAgentType;
        private bool _onRoadAgent;   // switched to the agent type that may walk on the road

        /// <summary>True while a pedestrian is in our hands.</summary>
        public bool Held => _pedestrian != null;

        public ThirdPersonCharacter Character => _pedestrian != null ? _pedestrian.tpc : null;

        /// <summary>True if the game put the pedestrian away (or destroyed it) while it was ours.</summary>
        public bool PutAway => (object)_pedestrian != null && (_pedestrian == null || !_pedestrian.gameObject.activeInHierarchy);

        /// <summary>Takes a pedestrian out of the game's control where it stands. False if it cannot be had.</summary>
        public bool Take(Pedestrian pedestrian)
        {
            if (_pedestrian != null || pedestrian == null || !pedestrian.isActiveAndEnabled || pedestrian.tpc == null
                || pedestrian.tpc.navmeshAgent == null)
            {
                return false;
            }
            _pedestrian = pedestrian;
            _ownAgentType = pedestrian.tpc.navmeshAgent.agentTypeID;
            _onRoadAgent = false;
            try
            {
                pedestrian.OnRelease();        // walking and standing off, exactly as when the pool puts it away
                // The pool then switches the object off, which ends the timers its behaviours leave on the character
                // (one puts a phone in its hand a minute later): the same, by hand.
                pedestrian.tpc.StopAllCoroutines();
                pedestrian.enabled = false;    // and no range check: the game will not teleport it while it lies there
                return true;
            }
            catch (Exception e)
            {
                RagdollSystem.Logger?.Error(e);
                Forget();
                return false;
            }
        }

        /// <summary>Stands it still at a point that may be on the road, once the ragdoll has ended there. False if it cannot stand there.</summary>
        public bool StandUp(Vector3 at)
        {
            ThirdPersonCharacter character = Character;
            if (character == null)
            {
                return false;
            }
            NavMeshAgent agent = character.navmeshAgent;
            agent.enabled = false;
            character.transform.position = at;
            // The game's pedestrians can only walk the pavements; the player's agent type covers the road as well.
            agent.agentTypeID = NavMeshHelper.PlayerAgentTypeId;
            _onRoadAgent = true;
            agent.enabled = true;
            bool standing = agent.Warp(at) && agent.isOnNavMesh;
            character.LookTarget = Vector3.zero;
            character.capsuleCollider.enabled = true;   // traffic brakes for someone standing in the road
            return standing;
        }

        /// <summary>Walks to a point, across the road if need be. Only after StandUp.</summary>
        public bool WalkTo(Vector3 point)
        {
            ThirdPersonCharacter character = Character;
            if (character == null)
            {
                return false;
            }
            NavMeshAgent agent = character.navmeshAgent;
            if (!agent.enabled || !agent.isOnNavMesh)
            {
                return false;
            }
            agent.isStopped = false;
            return agent.SetDestination(point);
        }

        public bool Arrived
        {
            get
            {
                ThirdPersonCharacter character = Character;
                if (character == null)
                {
                    return true;
                }
                NavMeshAgent agent = character.navmeshAgent;
                return !agent.enabled || !agent.isOnNavMesh
                       || (!agent.pathPending && (!agent.hasPath || agent.remainingDistance <= agent.stoppingDistance + 0.4f));
            }
        }

        /// <summary>Gives it back as an ordinary walker heading for a point; put away instead if it is not where a pedestrian can walk.</summary>
        public void HandBack(Vector3 walkTo)
        {
            Pedestrian pedestrian = _pedestrian;
            if (pedestrian == null || PutAway)
            {
                Forget();
                return;
            }
            try
            {
                ThirdPersonCharacter character = pedestrian.tpc;
                NavMeshAgent agent = character.navmeshAgent;
                if (!NavMesh.SamplePosition(character.transform.position, out NavMeshHit hit, 4f, NavMeshHelper.NpcNavMeshFilter))
                {
                    Remove();
                    return;
                }
                agent.enabled = false;
                RestoreAgentType(pedestrian);
                character.transform.position = hit.position;
                agent.enabled = true;
                if (!agent.Warp(hit.position) || !agent.isOnNavMesh)
                {
                    Remove();
                    return;
                }
                agent.isStopped = false;
                character.capsuleCollider.enabled = true;
                _pedestrian = null;
                pedestrian.enabled = true;
                // From here it is the game's again: it walks there, stands about, and carries on as it always does.
                pedestrian.pedestrianWalkingAiBehavior.Enable((walkTo, Quaternion.identity));
            }
            catch (Exception e)
            {
                RagdollSystem.Logger?.Error(e);
                _pedestrian = pedestrian;
                Remove();
            }
        }

        /// <summary>Takes it off the street through the pool's own release, so its next use is an ordinary walker.</summary>
        public void Remove()
        {
            Pedestrian pedestrian = _pedestrian;
            bool putAway = PutAway;
            Forget();
            if (pedestrian == null || putAway)
            {
                return;   // destroyed, or already back in the pool: releasing it twice would hand it out twice
            }
            if (!InstanceBehavior<CityManager>.IsInitialized)
            {
                return;   // the city itself is being unloaded, pool and all
            }
            try
            {
                pedestrian.Release();
            }
            catch (Exception e)
            {
                RagdollSystem.Logger?.Error(e);
            }
        }

        /// <summary>Lets go without touching where the pedestrian is: its own scripts are switched back on and that is all.</summary>
        public void Forget()
        {
            Pedestrian pedestrian = _pedestrian;
            _pedestrian = null;
            if (pedestrian == null)
            {
                _onRoadAgent = false;
                return;
            }
            try
            {
                RestoreAgentType(pedestrian);
                pedestrian.enabled = true;
            }
            catch (Exception e)
            {
                RagdollSystem.Logger?.Error(e);
            }
        }

        // Back to its pavements-only agent type; a running agent is switched off for it, as its own type may have no
        // ground where it stands.
        private void RestoreAgentType(Pedestrian pedestrian)
        {
            if (!_onRoadAgent)
            {
                return;
            }
            _onRoadAgent = false;
            NavMeshAgent agent = pedestrian.tpc != null ? pedestrian.tpc.navmeshAgent : null;
            if (agent == null)
            {
                return;
            }
            if (agent.enabled && pedestrian.gameObject.activeInHierarchy)
            {
                agent.enabled = false;
            }
            agent.agentTypeID = _ownAgentType;
        }
    }
}
