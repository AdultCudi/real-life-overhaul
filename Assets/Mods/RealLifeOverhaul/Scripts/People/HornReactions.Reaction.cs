using System;
using System.Collections.Generic;
using BAModAPI;
using BigAmbitions.Characters;
using BigAmbitions.InputSystem;
using Helpers;
using Streets.Pedestrians;
using UI.Notification;
using UnityEngine;
using UnityEngine.AI;
using Random = UnityEngine.Random;

namespace RealLifeOverhaul
{
    /// <summary>One person's reaction: started in a slot, turned and timed, and everything it changed put back.</summary>
    public partial class HornReactions : MonoBehaviour
    {
        // Starts it; false, with nothing changed, if no slot is free or the person cannot do it as they are. seconds: how long
        // a look round lasts, if not the usual moment.
        private bool Begin(ref Heard person, StreetRules.Reaction reaction, Vector3 car, StreetRules.Light light, int rages, float now, float seconds = 0f)
        {
            Slot slot = null;
            for (int i = 0; i < _slots.Length && slot == null; i++)
            {
                if (!_slots[i].InUse)
                {
                    slot = _slots[i];
                }
            }
            if (slot == null)
            {
                return false;
            }

            ThirdPersonCharacter character = person.Walker != null ? person.Walker.tpc : null;
            NavMeshAgent agent = character != null ? character.navmeshAgent : null;
            bool walking = agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh;
            if (!walking && (reaction == StreetRules.Reaction.Hurry || reaction == StreetRules.Reaction.StepBack))
            {
                return false;   // standing about: nobody jogs who is not walking anywhere
            }

            slot.What = reaction;
            slot.Walker = walking ? person.Walker : null;
            slot.Figure = walking ? null : person.Walker != null ? (BaseHuman)person.Walker.tpc : person.Figure;
            slot.Body = walking ? character.transform : slot.Figure != null ? slot.Figure.transform : null;
            slot.Agent = walking ? agent : null;
            slot.Raised = false;
            slot.Stopped = false;
            slot.Turned = false;
            slot.Turning = false;
            slot.TurningBack = false;
            slot.Backing = false;
            slot.Face = car;
            slot.Gestured = null;
            if (slot.Body == null)
            {
                Wipe(slot);
                return false;
            }
            Animator animator = walking ? character.animator : slot.Figure.animator;

            switch (reaction)
            {
                case StreetRules.Reaction.Hurry:
                case StreetRules.Reaction.StepBack:
                    if (agent.isStopped)
                    {
                        Wipe(slot);
                        return false;   // held at a kerb by the game, or turning at their door: not ours to send anywhere
                    }
                    if (reaction == StreetRules.Reaction.StepBack)
                    {
                        if (TurnBack(agent, out slot.Back))
                        {
                            slot.Backing = true;
                            slot.BackBy = now + BackMost;
                        }
                        else
                        {
                            slot.What = StreetRules.Reaction.Hurry;   // no way back to be had: onwards it is
                        }
                    }
                    slot.OwnPace = agent.speed;
                    slot.RaisedPace = StreetRules.HurryPace(slot.OwnPace);
                    agent.speed = slot.RaisedPace;
                    slot.Raised = true;
                    slot.Until = now + StreetRules.HurrySeconds;
                    if (StreetRules.Waves(Random.Range(0, 100)))
                    {
                        Gesture(slot, animator, WaveClip, BodyMask.RightArm);
                    }
                    break;

                case StreetRules.Reaction.Glare:
                case StreetRules.Reaction.Startle:
                    bool glare = reaction == StreetRules.Reaction.Glare;
                    if (walking)
                    {
                        // Stopped for the moment, unless the game is holding them already (then they only turn to look).
                        if (!agent.isStopped)
                        {
                            agent.isStopped = true;
                            slot.Stopped = true;
                        }
                    }
                    else
                    {
                        slot.OwnFacing = slot.Body.rotation;
                        slot.Turned = true;
                    }
                    slot.Turning = true;
                    slot.Until = now + (glare ? StreetRules.GlareFor(rages) : seconds > 0f ? seconds : StreetRules.StartleSeconds);
                    if (glare)
                    {
                        Gesture(slot, animator, StreetRules.Points(light, Random.Range(0, 100)) ? PointClip : AngryClip, BodyMask.Upper);
                        ActorPool pool = ActorPool.Instance;
                        if (pool != null && walking)
                        {
                            pool.ShowEmoji(character, CharacterEmojiName.CommonRedAngry, EmojiFor);
                        }
                        else if (pool != null && slot.Figure.head != null)
                        {
                            pool.ShowEmojiAt(slot.Figure.head, CharacterEmojiName.CommonRedAngry, EmojiFor);
                        }
                    }
                    break;
            }
            slot.InUse = true;
            _active++;
            _nextCare = Mathf.Min(_nextCare, now + CareInterval);
            return true;
        }

        // One step back the way they came, planned into the one path this keeps; the game's walker asks for its own way on by
        // itself afterwards.
        private bool TurnBack(NavMeshAgent agent, out Vector3 back)
        {
            Vector3 going = agent.velocity;
            going.y = 0f;
            back = agent.transform.position;
            if (going.sqrMagnitude < 0.04f)
            {
                return false;
            }
            back -= going.normalized * StepBack;
            return agent.CalculatePath(back, _path) && _path.status != NavMeshPathStatus.PathInvalid && agent.SetPath(_path);
        }

        private static void Backed(Slot slot, float now)
        {
            NavMeshAgent agent = slot.Agent;
            Vector3 off = slot.Body.position - slot.Back;
            if (off.x * off.x + off.z * off.z > BackWithin * BackWithin && now < slot.BackBy)
            {
                return;
            }
            slot.Backing = false;
            if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh && !agent.isStopped)
            {
                agent.isStopped = true;
                slot.Stopped = true;
            }
            slot.Turning = true;
            slot.Until = now + BackWait;
        }

        // An upper-body or one-arm clip of the mod's, once, over the game's animation; without it the turn and the pace say it.
        private static void Gesture(Slot slot, Animator animator, string clipName, BodyMask mask)
        {
            if (animator == null || !ActivityAnimators.Working || !ClipLibrary.Has(clipName) || !ClipLibrary.TryGet(clipName, out CityClip clip))
            {
                return;
            }
            ActivityAnimator player = ActivityAnimators.For(animator);
            if (player != null && player.Usable && !player.Active && player.PlayOver(clip.Clip, mask))
            {
                slot.Gestured = animator;
            }
        }

        private void TickReactions(float now)
        {
            bool care = now >= _nextCare;
            if (care)
            {
                _nextCare = now + CareInterval;
            }
            for (int i = 0; i < _slots.Length; i++)
            {
                Slot slot = _slots[i];
                if (!slot.InUse)
                {
                    continue;
                }
                if (care && Gone(slot))
                {
                    End(slot, early: true);
                    continue;
                }
                if (slot.Backing && slot.Body != null)
                {
                    Backed(slot, now);
                }
                if (slot.Turning)
                {
                    Turn(slot);
                }
                if (now < slot.Until)
                {
                    continue;
                }
                if (slot.Turned && !slot.TurningBack)
                {
                    slot.TurningBack = true;   // a figure that was standing a certain way stands that way again
                    slot.Turning = true;
                    slot.Until = now + TurnBackFor;
                }
                else
                {
                    End(slot, early: false);
                }
            }
        }

        // True if the person is no longer the game's to react with: destroyed, pooled, taken over, thrown down, or handed out
        // again with a pace of its own.
        private static bool Gone(Slot slot)
        {
            if ((object)slot.Walker != null)
            {
                Pedestrian walker = slot.Walker;
                return walker == null || !walker.isActiveAndEnabled || slot.Agent == null || !slot.Agent.isActiveAndEnabled
                       || (slot.Raised && !Mathf.Approximately(slot.Agent.speed, slot.RaisedPace));
            }
            BaseHuman figure = slot.Figure;
            if (figure == null || !figure.gameObject.activeInHierarchy || RagdollSystem.Holds(figure))
            {
                return true;
            }
            // A pedestrian that was standing about and has set off again: the way it stood is nothing to put back.
            if (figure is ThirdPersonCharacter character && character.navmeshAgent != null && character.navmeshAgent.isActiveAndEnabled)
            {
                slot.Turned = false;
                return true;
            }
            return false;
        }

        private static void Turn(Slot slot)
        {
            Transform body = slot.Body;
            if (body == null)
            {
                slot.Turning = false;
                return;
            }
            Quaternion want = slot.OwnFacing;
            if (!slot.TurningBack)
            {
                Vector3 to = slot.Face - body.position;
                to.y = 0f;
                if (to.sqrMagnitude < 0.01f)
                {
                    slot.Turning = false;
                    return;
                }
                want = Quaternion.LookRotation(to, Vector3.up);
            }
            body.rotation = Quaternion.RotateTowards(body.rotation, want, TurnRate * Time.deltaTime);
            if (Quaternion.Angle(body.rotation, want) < FacedWithin)
            {
                slot.Turning = false;
            }
        }

        // The one way out of a reaction: only what this changed is put back, if the person is still there. early: before its
        // time (taken, pooled, switched off).
        private void End(Slot slot, bool early)
        {
            try
            {
                NavMeshAgent agent = slot.Agent;
                if (agent != null)
                {
                    // A pace set by somebody else since is theirs: the pool gives a pedestrian a new one each time it is handed out.
                    if (slot.Raised && Mathf.Approximately(agent.speed, slot.RaisedPace))
                    {
                        agent.speed = slot.OwnPace;
                    }
                    Pedestrian walker = slot.Walker;
                    if (slot.Stopped && walker != null && walker.isActiveAndEnabled && agent.isActiveAndEnabled && agent.isOnNavMesh && agent.isStopped)
                    {
                        agent.isStopped = false;
                    }
                }
                if (slot.Turned && slot.Body != null && (early || slot.TurningBack))
                {
                    slot.Body.rotation = slot.OwnFacing;
                }
                if (early && slot.Gestured != null)
                {
                    ActivityAnimators.Forget(slot.Gestured);   // the body is handed back at once: a ragdoll or the pool is about to have it
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
            Wipe(slot);
            _active = Mathf.Max(0, _active - 1);
        }

        private static void Wipe(Slot slot)
        {
            slot.InUse = false;
            slot.Walker = null;
            slot.Figure = null;
            slot.Body = null;
            slot.Agent = null;
            slot.Gestured = null;
            slot.Raised = false;
            slot.Stopped = false;
            slot.Turned = false;
            slot.Turning = false;
            slot.TurningBack = false;
            slot.Backing = false;
        }

        /// <summary>Everybody who is reacting is the game's again, as it had them, now. Safe at any time.</summary>
        public void ReleaseAll(string why)
        {
            int let = 0;
            for (int i = 0; i < _slots.Length; i++)
            {
                if (_slots[i] != null && _slots[i].InUse)
                {
                    End(_slots[i], early: true);
                    let++;
                }
            }
            _active = 0;
            if (let > 0)
            {
                Logger?.Info("Horn reactions: " + let + " people let go at once (" + why + "); pace, stop and facing put back.");
            }
        }

        /// <summary>A pedestrian about to be taken over by the mod (a car hit them): what a reaction changed is put back first.</summary>
        public static void LetGo(Pedestrian pedestrian)
        {
            HornReactions reactions = Instance;
            if (reactions == null || reactions._active == 0 || pedestrian == null)
            {
                return;
            }
            for (int i = 0; i < reactions._slots.Length; i++)
            {
                Slot slot = reactions._slots[i];
                if (slot.InUse && (slot.Walker == pedestrian || (slot.Figure != null && slot.Figure == pedestrian.tpc)))
                {
                    reactions.End(slot, early: true);
                }
            }
        }

        /// <summary>The same for one of the game's standing figures.</summary>
        public static void LetGo(BaseHuman figure)
        {
            HornReactions reactions = Instance;
            if (reactions == null || reactions._active == 0 || figure == null)
            {
                return;
            }
            for (int i = 0; i < reactions._slots.Length; i++)
            {
                Slot slot = reactions._slots[i];
                if (slot.InUse && slot.Figure == figure)
                {
                    reactions.End(slot, early: true);
                }
            }
        }
    }
}
