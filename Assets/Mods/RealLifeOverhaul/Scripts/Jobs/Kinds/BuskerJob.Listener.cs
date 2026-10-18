using System.Collections.Generic;
using BigAmbitions.Characters;
using Characters.EmojiSystem;
using Entities;
using Helpers;
using RealLifeOverhaul.GameUi;
using Streets.Pedestrians;
using UnityEngine;
using UnityEngine.AI;
using Random = UnityEngine.Random;

namespace RealLifeOverhaul
{
    /// <summary>Busking: a passer-by who stops to listen, drops something in the case and may cheer.</summary>
    public sealed partial class BuskerJob
    {
        private const float ListenWithin = 7f;      // metres
        private const string CheerClip = "cheer_once";   // a listener's punch of the fist after a song that went well
        private readonly PedestrianTakeover _listener = new PedestrianTakeover();
        private float _listenUntil;
        private bool _cheerDue;               // the song went well and the listener holding has not cheered it yet
        private float _cheerEnds;             // when the listener's cheer is over; 0 otherwise
        private float _nextListener;

        private void Listener(float now)
        {
            if (_listener.Held)
            {
                if (_listener.PutAway)
                {
                    Unlook();
                    StopCheer(_listener.Character);
                    _listener.Forget();   // the game took them back meanwhile
                    return;
                }
                if (_cheerEnds > 0f && now < _cheerEnds)
                {
                    return;   // the cheer plays out first
                }
                if (now < _listenUntil && _playing)
                {
                    return;
                }
                // Something in the case, and on their way (after a cheer, if the song earned one).
                ThirdPersonCharacter body = _listener.Character;
                if (_cheerDue && body != null && Cheer(body, now))
                {
                    return;
                }
                StopCheer(body);
                if (body != null)
                {
                    body.LookTarget = Vector3.zero;
                    Ctx.Cast.Emoji(body, CharacterEmojiName.CommonGreenHappy, 1.8f);
                    Vector3 from = body.transform.position;
                    if (!StreetSpots.AwayFrom(from, Ctx.PlayerPosition, 30f, false, out Vector3 to))
                    {
                        to = from;
                    }
                    _listener.HandBack(to);
                    if (_playing)
                    {
                        _tips += Mathf.Max(1, Mathf.RoundToInt(Random.Range(2.75f, 8.25f) * Ctx.Rate));
                    }
                }
                else
                {
                    _listener.Forget();
                }
                _nextListener = now + Random.Range(9f, 18f);
                return;
            }
            if (!_playing || _step != Step.Song || now < _nextListener)
            {
                return;
            }
            _nextListener = now + 3f;
            CityManager city = InstanceBehavior<CityManager>.Instance;
            List<Pedestrian> walkers = city != null && city.pedestrianSpawner != null ? city.pedestrianSpawner.activePedestrians : null;
            Vector3 here = Ctx.Player.transform.position;
            for (int i = 0; walkers != null && i < walkers.Count; i++)
            {
                Pedestrian walker = walkers[i];
                if (walker == null || !walker.isActiveAndEnabled || walker.tpc == null || !walker.tpc.visible || RagdollSystem.Holds(walker.tpc))
                {
                    continue;
                }
                Vector3 offset = walker.tpc.transform.position - here;
                // In front of the busker, a few steps off: not behind them, not on the case.
                if (offset.sqrMagnitude > ListenWithin * ListenWithin || offset.sqrMagnitude < 4f || Vector3.Dot(offset, _facing) < 1.2f
                    || offset.y > 1.5f || offset.y < -1.5f)
                {
                    continue;
                }
                if (!_listener.Take(walker))
                {
                    continue;
                }
                NavMeshAgent agent = walker.tpc.navmeshAgent;
                if (agent.enabled && agent.isOnNavMesh)
                {
                    agent.ResetPath();
                }
                agent.enabled = false;   // the game turns a character to its look target only while it is not steered
                walker.tpc.LookTarget = here;
                _listenUntil = now + Random.Range(6f, 10f);
                return;
            }
        }

        // A punch of the fist on the listener, standing with its steering off; false, with nothing changed, without the clip.
        private bool Cheer(ThirdPersonCharacter body, float now)
        {
            _cheerDue = false;
            ActivityAnimator player = ActivityAnimators.Working && body.animator != null ? ActivityAnimators.For(body.animator) : null;
            if (player == null || !player.Usable || !ClipLibrary.TryGet(CheerClip, out CityClip cheer) || !player.Play(cheer.Clip, 0.25f, 1f, ClipEnd.Return))
            {
                if (Meta.Dev.Verbose)
                {
                    Ctx.Logger?.Info("Jobs: the busker's listener did not cheer ('" + CheerClip + "' could not be played); they go on as before.");
                }
                return false;
            }
            _cheerEnds = now + cheer.Length;
            ClipLibrary.Played(CheerClip, "a listener at the busker");
            return true;
        }

        // The cheer is over or cut short: the game's own animation again at once, as the pedestrian is pooled. The body may be gone.
        private void StopCheer(ThirdPersonCharacter body)
        {
            _cheerDue = false;
            if (_cheerEnds <= 0f)
            {
                return;
            }
            _cheerEnds = 0f;
            if (body != null && body.animator != null)
            {
                ActivityAnimators.Forget(body.animator);
            }
        }

        // A pooled pedestrian keeps whatever look target it was left with: cleared before it is forgotten.
        private void Unlook()
        {
            ThirdPersonCharacter body = _listener.Character;
            if (body != null)
            {
                body.LookTarget = Vector3.zero;
            }
        }

        private void ReleaseListener()
        {
            if (!_listener.Held)
            {
                return;
            }
            ThirdPersonCharacter body = _listener.Character;
            StopCheer(body);
            if (body == null || _listener.PutAway)
            {
                Unlook();
                _listener.Forget();
                return;
            }
            body.LookTarget = Vector3.zero;
            Vector3 from = body.transform.position;
            if (!StreetSpots.AwayFrom(from, Ctx.PlayerPosition, 30f, false, out Vector3 to))
            {
                to = from;
            }
            _listener.HandBack(to);
        }
    }
}
