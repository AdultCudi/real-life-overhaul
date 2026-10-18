using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Characters.EmojiSystem;
using Helpers;
using RealLifeOverhaul.GameUi;
using Streets.Pedestrians;
using UI.Elements;
using UnityEngine;
using UnityEngine.AI;
using Random = UnityEngine.Random;

namespace RealLifeOverhaul
{
    /// <summary>The drinks cart: calling out to the street, and the passers-by who stop to look.</summary>
    public sealed partial class VendorJob
    {
        // Calling out: every figure of its balance.
        public const float HawkCooldown = 10f;       // seconds between two calls
        public const float HawkWithin = 14f;         // metres from the cart at which a passer-by hears it
        public const int HawkReactMost = 6;          // passers-by who react to one call
        public const int HawkExtraMost = 4;          // customers a shift can gain by calling out, over its own number
        public const float HawkTempt = 0.32f;        // a passer-by's chance of being tempted, before what follows
        public const float HawkAnnoy = 0.10f;        // ...and of being annoyed
        public const float HawkAnnoyStep = 0.13f;    // added to that for each recent call
        public const float HawkTemptStep = 0.20f;    // share of the tempted chance lost for each recent call
        public const float HawkHeatFade = 25f;       // seconds for one recent call to be forgotten
        public const float HawkWarnFrom = 3f;        // recent calls from which an officer may speak up
        private const int LookersMost = 2;
        private const int AnnoyedKept = 12;         // passers-by remembered as annoyed, not asked again

        private readonly PedestrianTakeover[] _lookers = { new PedestrianTakeover(), new PedestrianTakeover() };
        private readonly float[] _lookUntil = new float[LookersMost];
        private readonly int[] _annoyed = new int[AnnoyedKept];

        private float _hawkReadyAt;
        private float _heat;
        private float _heatAt;
        private float _hailUntil = -1f;
        private int _extra;
        private int _soon;            // customers who come sooner because somebody was tempted
        private int _annoyedAt;
        private int _hawkWarnings;
        private bool _hawkBanned;
        private string _hawkWords;
        private float _hawkWordsUntil;

        /// <summary>
        /// Calls out to the street: each pedestrian within <see cref="HawkWithin"/> m (six at most) is tempted, annoyed or
        /// unmoved by chance. Tempting scales with the hour, the crowd, the customer service skill and the queue, and fades with
        /// recent calls; a tempted one may stop to look and brings a customer more (<see cref="HawkExtraMost"/> at most).
        /// With police about, many recent calls may bring an officer's warning; the second ends calling out for the shift.
        /// </summary>
        private void Hawk(float now)
        {
            if (Ctx == null || _skip != null || _hawkBanned || now < _hawkReadyAt || !Ctx.Stand.Ready)
            {
                return;
            }
            _hawkReadyAt = now + HawkCooldown;
            _heat = Mathf.Max(0f, _heat - (now - _heatAt) / HawkHeatFade);
            _heatAt = now;
            if (now >= _serveUntil && Ctx.Animations.Play(Ctx.Player.Character, JobClip.Hail) > 0f)
            {
                _hailUntil = now + 1.6f;   // an arm up and a wave over the working pose; it comes down on the tick
            }

            CityManager city = InstanceBehavior<CityManager>.Instance;
            List<Pedestrian> walkers = city != null && city.pedestrianSpawner != null ? city.pedestrianSpawner.activePedestrians : null;
            int about = 0;
            for (int i = 0; walkers != null && i < walkers.Count && about < 6; i++)
            {
                if (walkers[i] != null && (walkers[i].transform.position - _cartAt).sqrMagnitude < 30f * 30f)
                {
                    about++;
                }
            }
            int waiting = 0;
            for (int i = 0; i < JobCast.Size; i++)
            {
                waiting += _state[i] != Guest.None && _state[i] != Guest.Leaving ? 1 : 0;
            }
            int hour = TimeHelper.CurrentHour;
            float hourFactor = hour >= 23 || hour < 6 ? 0.5f : hour >= 16 && hour < 21 ? 1.3f : hour >= 11 && hour < 14 ? 1.2f : hour < 9 ? 0.8f : 1f;
            float busyFactor = 0.8f + 0.4f * (about / 6f);
            Entities.EmployeeInstance self = PlayerHelper.GetPlayerEmployeeInstance();
            float skill = self != null ? Mathf.Clamp(self.GetSkillValue("ba:skill_customerservice"), 0f, 100f) : 0f;
            float skillFactor = 1f + skill / 200f;
            float queueFactor = waiting <= 0 ? 1f : waiting == 1 ? 0.6f : 0.3f;
            float annoy = Mathf.Min(0.7f, HawkAnnoy + HawkAnnoyStep * _heat);
            float tempt = Mathf.Clamp(HawkTempt * hourFactor * busyFactor * skillFactor * queueFactor * Mathf.Max(0.3f, 1f - HawkTemptStep * _heat), 0.03f, 0.75f);
            tempt = Mathf.Min(tempt, 1f - annoy);

            bool log = Meta.Dev.Verbose;
            if (log)
            {
                _text.Length = 0;
            }
            int looked = 0, cross = 0, passed = 0, gained = 0;
            for (int i = 0; walkers != null && i < walkers.Count && looked + cross + passed < HawkReactMost; i++)
            {
                Pedestrian walker = walkers[i];
                if (walker == null || !walker.isActiveAndEnabled || walker.tpc == null || !walker.tpc.visible || RagdollSystem.Holds(walker.tpc))
                {
                    continue;
                }
                Vector3 offset = walker.tpc.transform.position - _cartAt;
                if (offset.sqrMagnitude > HawkWithin * HawkWithin || offset.y > 1.5f || offset.y < -1.5f || Holds(walker) || WasAnnoyed(walker))
                {
                    continue;
                }
                float roll = Random.value;
                string outcome;
                if (roll < annoy)
                {
                    cross++;
                    _annoyed[_annoyedAt++ % AnnoyedKept] = walker.GetInstanceID();
                    Ctx.Cast.Emoji(walker.tpc, CharacterEmojiName.CommonRedAngry, 2.5f);
                    Ctx.Animations.Play(walker.tpc, JobClip.Annoyed);
                    outcome = "annoyed";
                }
                else if (roll < annoy + tempt)
                {
                    looked++;
                    Ctx.Cast.Emoji(walker.tpc, CharacterEmojiName.CommonGreenHappy, 2.5f);
                    bool stops = Look(walker, now);
                    bool room = _extra < HawkExtraMost && (Ctx.FullShift || _customers < JobState.LeftToday(Def));
                    if (room)
                    {
                        _extra++;
                        _customers++;
                        gained++;
                    }
                    _soon++;
                    _nextArrival = Mathf.Min(_nextArrival, now + Random.Range(1.2f, 2.4f));
                    outcome = "tempted" + (stops ? ", stops to look" : string.Empty) + (room ? ", a customer more" : ", the shift has all the extra customers it can take");
                }
                else
                {
                    passed++;
                    outcome = "takes no notice";
                }
                if (log)
                {
                    _text.Append(_text.Length > 0 ? "; " : string.Empty).Append(Mathf.Sqrt(offset.x * offset.x + offset.z * offset.z).ToString("F0")).Append(" m: ").Append(outcome)
                        .Append(" (roll ").Append(roll.ToString("F2", CultureInfo.InvariantCulture)).Append(')');
                }
            }
            float heatBefore = _heat;
            _heat += 1f;
            int near = looked + cross + passed;
            _hawkWords = near == 0 ? JobText.T("reallife_job_vendor_hawk_none")
                : JobText.T("reallife_job_vendor_hawk_result", "looked", looked.ToString(), "annoyed", cross.ToString()).Replace("{ignored}", passed.ToString());
            _hawkWordsUntil = now + 7f;
            _cardState = int.MinValue;

            // Too much of it with officers about: a warning, and the second ends it for the shift.
            bool warned = false;
            if (_heat >= HawkWarnFrom && Settings.PolicePresence > 0 && Random.value < 0.12f + 0.03f * Settings.PolicePresence)
            {
                warned = true;
                _hawkWarnings++;
                _hawkBanned = _hawkWarnings >= 2;
                Ctx.Say(_hawkBanned ? "reallife_job_vendor_hawk_banned" : "reallife_job_vendor_hawk_warned", 8f);
            }
            if (log)
            {
                Ctx.Logger?.Info("Jobs: vendor: called out. Chances: tempted " + tempt.ToString("F2", CultureInfo.InvariantCulture) + " (base "
                                 + HawkTempt.ToString("F2", CultureInfo.InvariantCulture) + " x hour " + hourFactor.ToString("F1", CultureInfo.InvariantCulture)
                                 + " at " + hour + "h x busy " + busyFactor.ToString("F2", CultureInfo.InvariantCulture) + " with " + about + " about x skill "
                                 + skillFactor.ToString("F2", CultureInfo.InvariantCulture) + " x queue "
                                 + queueFactor.ToString("F1", CultureInfo.InvariantCulture) + " with " + waiting + " at the cart or on the way, recent calls "
                                 + heatBefore.ToString("F1", CultureInfo.InvariantCulture) + "), annoyed " + annoy.ToString("F2", CultureInfo.InvariantCulture)
                                 + ". Within " + HawkWithin.ToString("F0") + " m: " + near + (near > 0 ? " (" + _text + ")" : string.Empty) + ". Customers gained: " + gained
                                 + " (the shift is now " + _customers + ")" + (warned ? "; an officer's warning " + _hawkWarnings + " of 2" + (_hawkBanned ? ": no more calling out this shift" : string.Empty) : string.Empty)
                                 + "; next call in " + HawkCooldown.ToString("F0") + " s.");
            }
        }

        // One of the two who may stop and look: taken from the game where they stand, turned to the cart by its look target,
        // and handed back walking a few seconds later (as the busker's listener is).
        private bool Look(Pedestrian walker, float now)
        {
            for (int i = 0; i < LookersMost; i++)
            {
                if (_lookers[i].Held || !_lookers[i].Take(walker))
                {
                    continue;
                }
                NavMeshAgent agent = walker.tpc.navmeshAgent;
                if (agent.enabled && agent.isOnNavMesh)
                {
                    agent.ResetPath();
                }
                agent.enabled = false;   // the game turns a character to its look target only while it is not steered
                walker.tpc.LookTarget = _cartAt;
                _lookUntil[i] = now + Random.Range(4f, 7f);
                return true;
            }
            return false;
        }

        private bool Holds(Pedestrian walker)
        {
            for (int i = 0; i < LookersMost; i++)
            {
                if (_lookers[i].Held && ReferenceEquals(_lookers[i].Character, walker.tpc))
                {
                    return true;
                }
            }
            return false;
        }

        private bool WasAnnoyed(Pedestrian walker)
        {
            int id = walker.GetInstanceID();
            for (int i = 0; i < AnnoyedKept; i++)
            {
                if (_annoyed[i] == id)
                {
                    return true;
                }
            }
            return false;
        }

        // Whoever has looked long enough walks on.
        private void Lookers(float now)
        {
            for (int i = 0; i < LookersMost; i++)
            {
                if (_lookers[i].Held && (_lookers[i].PutAway || now >= _lookUntil[i]))
                {
                    Release(_lookers[i]);
                }
            }
        }

        private void ReleaseLookers()
        {
            for (int i = 0; i < LookersMost; i++)
            {
                if (_lookers[i].Held)
                {
                    Release(_lookers[i]);
                }
            }
        }

        private void Release(PedestrianTakeover looker)
        {
            ThirdPersonCharacter body = looker.Character;
            if (body != null)
            {
                body.LookTarget = Vector3.zero;   // a pedestrian keeps whatever look target it was left with
            }
            if (body == null || looker.PutAway)
            {
                looker.Forget();   // the game took them back meanwhile
                return;
            }
            Vector3 from = body.transform.position;
            if (!StreetSpots.AwayFrom(from, _cartAt, 30f, false, out Vector3 to))
            {
                to = from;
            }
            looker.HandBack(to);
        }
    }
}
