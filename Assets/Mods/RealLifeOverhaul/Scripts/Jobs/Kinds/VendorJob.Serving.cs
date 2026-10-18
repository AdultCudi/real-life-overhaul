using System.Collections.Generic;
using System.Globalization;
using Characters.EmojiSystem;
using Helpers;
using RealLifeOverhaul.GameUi;
using Streets.Pedestrians;
using UI.Elements;
using UnityEngine;
using Random = UnityEngine.Random;

namespace RealLifeOverhaul
{
    /// <summary>The drinks cart: a drink served, and the cup changing hands at the clips' moments.</summary>
    public sealed partial class VendorJob
    {
        private static readonly string[] Thanks = { "reallife_job_vendor_thanks_1", "reallife_job_vendor_thanks_2", "reallife_job_vendor_thanks_3" };
        private static readonly string[] Wrong = { "reallife_job_vendor_wrong_1", "reallife_job_vendor_wrong_2" };

        // A drink served with the mod's clips, a moment at a time: the reach, the cup put down, the cup taken. 0: nothing pending.
        private const int BeatReach = 1, BeatCupDown = 2, BeatCupTaken = 3;
        private int _beat;
        private StreetActor _beatWho;
        private int _beatLease;
        private float _cupDownAt;
        private float _cupTakenAt;

        public override void Choose(int choice, float now)
        {
            if (_front < 0 || _state[_front] != Guest.Ordering || !Ctx.Stand.Ready || now < _serveUntil || _skip != null)
            {
                return;
            }
            StreetActor person = _who[_front];
            Ctx.Stand.Busy(2.2f, now);   // the cloth is put down; the working pose returns once served
            float reach = Mathf.Max(0.9f, Ctx.Animations.Play(Ctx.Player.Character, JobClip.Serve));
            _serveUntil = now + Mathf.Min(reach, 2.2f);
            // The shift's one difficult customer, if the offer drew one, sends back the first right drink.
            bool sendBack = choice == _wants && !_sentBack && Ctx.Offer.RedoAt > 0 && _served + 1 >= Ctx.Offer.RedoAt;
            if (choice != _wants || sendBack)
            {
                _orderEnds -= WrongCost;
                Ctx.Animations.Play(person.Character, JobClip.Refuse);
                Ctx.Cast.Emoji(person, CharacterEmojiName.CommonYellowNeutral, 1.6f);
                if (sendBack)
                {
                    _sentBack = true;
                    _wants = (_wants + 1 + Random.Range(0, JobKeys.Choices - 1)) % JobKeys.Choices;   // they now want another
                    Ctx.Say("reallife_gig_redo_cart", 6f);
                    Ctx.Logger?.Info("Jobs: vendor event: a difficult customer sends the drink back at customer " + (_served + 1) + " (the offer drew " + Ctx.Offer.RedoAt
                                     + "); it is made again, and the patience drops by " + WrongCost.ToString("F0") + " s.");
                    return;
                }
                Ctx.Say(JobText.Pick(Wrong, ref _line), 4f);
                if (Meta.Dev.Verbose)
                {
                    Ctx.Logger?.Info("Jobs: vendor: wrong drink by hand (" + ItemKeys[choice] + " for " + ItemKeys[_wants] + "): " + WrongCost.ToString("F0")
                                     + " s off the customer's patience, " + Mathf.Max(0f, _orderEnds - now).ToString("F1", CultureInfo.InvariantCulture) + " s left.");
                }
                return;
            }
            float took = now - _orderedAt;
            int tip = 0;
            _served++;
            _hand++;
            UnitDone(_hand, UnitPay());   // the big tipper, if the offer drew one
            if (took <= QuickTip)
            {
                tip = Mathf.RoundToInt(Random.Range(5.5f, 9.5f) * Ctx.Rate);
            }
            else if (took <= FairTip && Random.value < 0.6f)
            {
                tip = Mathf.RoundToInt(2.75f * Ctx.Rate);
            }
            _tips += tip;
            if (Meta.Dev.Verbose)
            {
                Ctx.Logger?.Info("Jobs: vendor: customer served by hand (" + ItemKeys[_wants] + ") in " + took.ToString("F1", CultureInfo.InvariantCulture) + " s, tip " + tip + " ("
                                 + (took <= QuickTip ? "quick" : took <= FairTip ? "fair" : "slow") + "); served " + _served + " of " + _customers + ", walk-offs " + _misses + ".");
            }
            // The cup goes on the counter between them, and they take it.
            float take;
            float release = Ctx.Animations.Moment(JobClip.Serve, "release");
            if (release >= 0f && ActivityAnimators.Working && ClipLibrary.Has(ReachClip) && ClipLibrary.TryGet(ReachClip, out CityClip reaching))
            {
                // Both clips are the mod's: each thing happens at its clip's moment, the customer's hand closing just after the vendor's lets go.
                float grip = reaching.Mark("grip", reaching.Length * 0.4f);
                float reachAt = now + Mathf.Max(0f, release + CupRest - grip);
                _cupDownAt = now + release;
                _cupTakenAt = reachAt + grip;
                _beatWho = person;
                _beatLease = person.Lease;
                _beat = BeatReach;
                Ctx.At(reachAt);
                take = _cupTakenAt - now + (reaching.Length - grip);
            }
            else
            {
                // The game's gestures, or only one of the clips: the cup is there at once and theirs a moment later.
                _cup = Ctx.Props.Take(CupProp);
                if (_cup != null)
                {
                    _cup.SetPositionAndRotation(_cartAt + _outwards * CupOut + Vector3.up * CounterHeight, Quaternion.identity);
                }
                take = 0f;
                if (ClipLibrary.Has(ReachClip) && ClipLibrary.TryGet(ReachClip, out CityClip grab) && ActivityAnimators.Working)
                {
                    ActivityAnimator hand = ActivityAnimators.For(person.Character.animator);
                    if (hand != null && hand.Usable && hand.Play(grab.Clip, 0.2f))
                    {
                        take = grab.Length;   // made for this counter: the hand goes to the cup and comes back with it
                    }
                }
                if (take <= 0f)
                {
                    take = Ctx.Animations.Play(person.Character, JobClip.Receive);   // one or the other: a gesture over the reach would take its arm
                }
                take = Mathf.Clamp(take, 1.4f, 2.6f);
            }
            Ctx.Cast.Emoji(person, took <= FairTip ? CharacterEmojiName.CommonGreenHappy : CharacterEmojiName.CommonYellowNeutral, 2f);
            if (_served % 3 == 1)
            {
                Ctx.Say(JobText.Pick(Thanks, ref _line), 4f);
            }
            _state[_front] = Guest.Served;
            _until[_front] = now + Mathf.Clamp(take, 1.4f, 3.4f);
            _orderOver = now;
        }

        /// <summary>The next moment of a drink changing hands (the Beat constants).</summary>
        public override void Moment(float now)
        {
            int beat = _beat;
            _beat = 0;
            StreetActor person = _beatWho;
            if (beat == 0 || _front < 0 || _who[_front] != person || _state[_front] != Guest.Served
                || person == null || !person.IsValid || !person.InUse || person.Lease != _beatLease)
            {
                return;   // they have gone; whatever stood on the counter went with them
            }
            switch (beat)
            {
                case BeatReach:
                    if (ClipLibrary.TryGet(ReachClip, out CityClip reaching))
                    {
                        ActivityAnimator hand = ActivityAnimators.For(person.Character.animator);
                        if (hand != null && hand.Usable)
                        {
                            hand.Play(reaching.Clip, 0.2f);   // made for this counter: the hand goes to the cup and comes back with it
                        }
                    }
                    _beat = BeatCupDown;
                    Ctx.At(_cupDownAt);
                    break;

                case BeatCupDown:
                    if (_cup == null)
                    {
                        _cup = Ctx.Props.Take(CupProp);
                    }
                    if (_cup != null)
                    {
                        _cup.SetPositionAndRotation(_cartAt + _outwards * CupOut + Vector3.up * CounterHeight, Quaternion.identity);
                    }
                    _beat = BeatCupTaken;
                    Ctx.At(_cupTakenAt);
                    break;

                case BeatCupTaken:
                    if (_cup != null)
                    {
                        Ctx.Props.Return(_cup);
                        _cup = null;
                    }
                    _cupHeld.Give(person.Character, Ctx.Props, CupGrips);
                    break;
            }
        }
    }
}
