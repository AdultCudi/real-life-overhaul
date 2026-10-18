using System.Globalization;
using Characters.EmojiSystem;
using Helpers;
using Streets.Pedestrians;
using UnityEngine;
using UnityEngine.AI;
using Random = UnityEngine.Random;

namespace RealLifeOverhaul
{
    /// <summary>Flyers: a flyer offered, taken or waved away, and the passer-by let go.</summary>
    public sealed partial class FlyerJob
    {
        // Who the panel is about; set only on a change.
        private void Point(BaseHuman person, Pedestrian walker)
        {
            if (ReferenceEquals(person, _near))
            {
                return;
            }
            _near = person;
            _nearWalker = walker;
        }

        // The panel's Offer a flyer: to whoever the panel stands over, else whoever is nearest a little further out.
        private void Offer(float now)
        {
            if (_with != null || Ctx.Driving || PlayerHelper.IsUsingVehicle)
            {
                return;   // a flyer is changing hands already
            }
            BaseHuman person = _near;
            Pedestrian walker = _nearWalker;
            // Someone who walked out of reach as the key went down is stopped all the same.
            if (person == null || !Eligible(person))
            {
                Find(Reach + Grace, out person, out walker);
            }
            Point(null, null);
            if (person == null || (walker != null && !_held.Take(walker)))
            {
                return;   // nobody in reach after all; the panel is made again on the next tick
            }
            Remember(person.GetInstanceID(), now);
            _with = person;
            _withWalker = walker != null;
            _turned = false;
            Vector3 player = Ctx.Player.transform.position;
            var body = person as ThirdPersonCharacter;

            if (_withWalker)
            {
                // They stop where they are and turn to the player.
                NavMeshAgent agent = body.navmeshAgent;
                if (agent.enabled && agent.isOnNavMesh)
                {
                    agent.ResetPath();
                }
                agent.enabled = false;   // the game turns a character to its look target only while it is not steered
                body.LookTarget = player;
            }
            else if (person.isSittingOn == null && (body == null || body.visible) && Ctx.Director != null && Ctx.Director.isActiveAndEnabled)
            {
                // Someone standing about stays where they are, the game's throughout: they only turn, and turn back after.
                _turned = true;
                _turnedFrom = person.transform.rotation;
                Ctx.Director.StartCoroutine(Guard.Routine(Turn(person.transform, Facing(person.transform.position, player), 0.25f)));
            }
            Ctx.TurnPlayer(person.transform.position);

            _heldTakes = Random.value < (TakeChance + TakePerLevel * (Ctx.Level - 1)) * Ctx.Offer.CrowdFactor;   // a quiet street or a rush, as the offer drew
            _heldUntil = now + Beat;
            Ctx.Self.Hold(Beat - 0.2f);   // on the spot while the arm is out; let go again on the director's tick
            _offered.Give(Ctx.Player.Character, Ctx.Props, OfferGrips, FlyerProp);
            float reach = Ctx.Animations.Play(Ctx.Player.Character, JobClip.HandOver);
            // With the mod's clip the flyer leaves the hand as the clip opens it, to the frame.
            float release = Ctx.Animations.Moment(JobClip.HandOver, "release");
            if (release >= 0f)
            {
                _armAgainAt = now + Mathf.Max(release + 0.1f, reach - HoldAgain);   // the gesture took the arm the stack is in
            }
            _receivePlayed = false;
            if (_heldTakes)
            {
                _receiveDue = true;       // a moment later, as the flyer reaches them
                if (release >= 0f)
                {
                    _receiveAt = now + release;
                    float grip = Ctx.Animations.Moment(JobClip.Receive, "grip");
                    if (grip >= 0f && release - grip < 0.15f)
                    {
                        // Their hand closes (0.50 s into reaching) as the player's opens (0.55 s): both start together.
                        _receivePlayed = true;
                        Ctx.Animations.Play(person, JobClip.Receive);
                    }
                    Ctx.At(_receiveAt);
                }
                else
                {
                    _receiveAt = now + Mathf.Clamp(reach * 0.45f, 0.45f, 0.75f);
                }
            }
            else
            {
                _receiveDue = false;
                Ctx.Animations.Play(person, JobClip.Refuse);
                _refusals++;
                if (Random.value < 0.35f)
                {
                    Ctx.Cast.Emoji(person, Random.value < 0.3f ? CharacterEmojiName.CommonRedAngry : CharacterEmojiName.CommonYellowNeutral, 1.6f);
                }
                if (_refusals % 3 == 1)
                {
                    Ctx.Say(JobText.Pick(Refused, ref _line), 4f);
                }
            }
        }

        private void Holding(float now)
        {
            // The game took them back meanwhile (the view moved on, a pool was emptied).
            if (_withWalker ? _held.PutAway || !_held.Held : !_with.gameObject.activeInHierarchy)
            {
                Drop();
                return;
            }
            if (_receiveDue && now >= _receiveAt)
            {
                Pass();
            }
            if (now < _heldUntil)
            {
                return;
            }
            bool busy = false;
            if (_heldTakes)
            {
                _taken++;
                busy = Busy();
                if (busy)
                {
                    _busy++;
                }
                if (_taken % 4 == 1)
                {
                    Ctx.Say(JobText.Pick(Took, ref _line), 4f);
                }
            }
            if (Meta.Dev.Verbose)
            {
                Ctx.Logger?.Info("Jobs: flyers: a flyer was " + (_heldTakes ? "taken" + (busy ? " on a busy street" : string.Empty) : "waved away") + "; taken " + _taken + " of " + _bundle
                                 + ", waved away " + _refusals + ", " + Mathf.Max(0, Mathf.CeilToInt(_endsAt - now)) + " s of the bundle's time left.");
            }
            _offerOver = now;
            LetGo();
        }

        /// <summary>The frame the offering hand opens: the flyer is the taker's.</summary>
        public override void Moment(float now)
        {
            if (_receiveDue && _with != null && _with.gameObject.activeInHierarchy && (!_withWalker || (_held.Held && !_held.PutAway)))
            {
                Pass();
            }
        }

        private void Pass()
        {
            _receiveDue = false;
            _offered.Take();                                              // out of the player's hand...
            _given.Give(_with, Ctx.Props, TakerGrips, FlyerProp);         // ...and into theirs until they go
            if (!_receivePlayed)
            {
                Ctx.Animations.Play(_with, JobClip.Receive);
            }
            Ctx.Cast.Emoji(_with, CharacterEmojiName.CommonGreenHappy, 1.6f);
        }

        // Turned on the spot over a moment; they may be put away by the game meanwhile.
        private static System.Collections.IEnumerator Turn(Transform who, Quaternion to, float seconds)
        {
            Quaternion from = who != null ? who.rotation : to;
            for (float t = 0f; t < seconds && who != null && who.gameObject.activeInHierarchy; t += Time.deltaTime)
            {
                who.rotation = Quaternion.Slerp(from, to, t / seconds);
                yield return null;
            }
            if (who != null && who.gameObject.activeInHierarchy)
            {
                who.rotation = to;
            }
        }

        private static Quaternion Facing(Vector3 from, Vector3 towards)
        {
            Vector3 flat = towards - from;
            flat.y = 0f;
            return flat.sqrMagnitude > 0.01f ? Quaternion.LookRotation(flat.normalized) : Quaternion.identity;
        }

        // Nothing more is done to them: whoever it was is the game's again, or gone.
        private void Drop()
        {
            _offered.Take();
            _given.Take();
            if (_held.Held)
            {
                if (_held.Character != null)
                {
                    _held.Character.LookTarget = Vector3.zero;   // a pooled pedestrian keeps what it was left with
                }
                _held.Forget();
            }
            _with = null;
            _withWalker = false;
            _turned = false;
        }

        // A walker goes on their way away from the player; someone standing is turned back as they were.
        private void LetGo()
        {
            BaseHuman person = _with;
            bool walker = _withWalker;
            bool turned = _turned;
            _offered.Take();   // a flyer that was waved away goes back on the stack
            _given.Take();     // nothing of ours goes with them
            _with = null;
            _withWalker = false;
            _turned = false;
            if (walker)
            {
                if (!_held.Held)
                {
                    return;
                }
                ThirdPersonCharacter body = _held.Character;
                if (body == null || _held.PutAway)
                {
                    if (body != null)
                    {
                        body.LookTarget = Vector3.zero;
                    }
                    _held.Forget();
                    return;
                }
                body.LookTarget = Vector3.zero;
                Vector3 from = body.transform.position;
                if (!StreetSpots.AwayFrom(from, Ctx != null ? Ctx.PlayerPosition : from, 30f, false, out Vector3 to))
                {
                    to = from;
                }
                _held.HandBack(to);
            }
            else if (turned && person != null && person.gameObject.activeInHierarchy)
            {
                if (Ctx != null && Ctx.Director != null && Ctx.Director.isActiveAndEnabled)
                {
                    Ctx.Director.StartCoroutine(Guard.Routine(Turn(person.transform, _turnedFrom, 0.3f)));
                }
                else
                {
                    person.transform.rotation = _turnedFrom;
                }
            }
        }
    }
}
