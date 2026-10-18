using System;
using BigAmbitions.Items;
using Characters.EmojiSystem;
using Helpers;
using RealLifeOverhaul.GameUi;
using UnityEngine;
using Vehicles.DeliveryDriverJob;
using Random = UnityEngine.Random;

namespace RealLifeOverhaul
{
    /// <summary>Courier: a parcel changing hands at the door and carried indoors by the recipient.</summary>
    public sealed partial class CourierJob
    {
        private const string HoldClip = "carry_box_hold";    // the arms alone over the game's walk and idle: nothing of the legs is ours, nothing can slide
        private const string CarryClip = "carry_box_walk";   // the whole body walking, where a bundle has no arms-only clip: kept in step with the ground
        private const float HoldLead = 0.3f;                 // seconds before the end of taking a parcel at which the arms settle into carrying it

        // The clips a parcel is held with, best first; they share the box's grip.
        private static readonly string[] ParcelGrips = { "job_receive_box", "carry_box_hold", "carry_box_walk" };

        private StreetActor _goingIn;       // the last recipient, carrying the parcel indoors
        private float _goingAt;
        private float _goingUntil;
        private bool _goingWalks;
        private Vector3 _goingTo;
        private readonly HeldProp _handed = new HeldProp();   // the parcel in the last recipient's arms
        private Transform _onStep;          // ...or on the step in front of them
        private ActivityAnimator _carrying; // the recipient's clip player carrying the box...

        private bool _carryingOver;         // ...on the masked layer (arms only), not as a whole-body walk

        private float _carryMade;           // m/s the whole-body walk was made for
        private StreetActor _passTo;        // whoever is reaching for the parcel: theirs at the clip's grip...
        private float _passHoldAt = -1f;    // ...and their arms settle into carrying it at this time

        // The parcel leaves the player's hands and is seen to arrive: in the recipient's arms if the bundle has the clips to
        // hold and walk with one, otherwise on the step between them.
        private void PassParcel(StreetActor to)
        {
            DropParcel();
            if ((ClipLibrary.Has(HoldClip) || ClipLibrary.Has(CarryClip)) && _handed.Give(to.Character, Ctx.Props, ParcelGrips))
            {
                return;
            }
            Vector3 between = Vector3.Lerp(to.Position, Ctx.Player.transform.position, 0.4f);
            if (StreetSpots.Ground(between, out float ground) && Mathf.Abs(ground - to.Position.y) < 0.4f)
            {
                between.y = ground;
                _onStep = Ctx.Props.Stand("parcel_m", between, to.Position - between);
            }
        }

        /// <summary>The two moments of a parcel being taken: the hands close on it, and then the arms settle into carrying it.</summary>
        public override void Moment(float now)
        {
            StreetActor to = _passTo;
            if (to != null)
            {
                _passTo = null;
                if (to.IsValid && to.InUse)
                {
                    PassParcel(to);
                    if (_handed.Held && _passHoldAt >= 0f)
                    {
                        Ctx.At(_passHoldAt);
                        return;
                    }
                }
                _passHoldAt = -1f;
                return;
            }
            if (_passHoldAt >= 0f)
            {
                _passHoldAt = -1f;
                StreetActor holder = _goingIn;
                if (holder != null && holder.IsValid && _handed.Held && _carrying == null)
                {
                    Carry(holder, false);
                }
            }
        }

        // A box in someone's arms: the arms-only clip over the game's own animation; where a bundle has only the whole-body walk,
        // that, once they walk, kept in step with the ground by SeeIn.
        private void Carry(StreetActor who, bool walking)
        {
            ActivityAnimator player = ActivityAnimators.Working && who.Character != null ? ActivityAnimators.For(who.Character.animator) : null;
            if (player == null || !player.Usable)
            {
                return;
            }
            if (ClipLibrary.Has(HoldClip) && ClipLibrary.TryGet(HoldClip, out CityClip hold) && hold.Loop)
            {
                if (player.PlayOver(hold.Clip, BodyMask.Arms, 0.3f))
                {
                    _carrying = player;
                    _carryingOver = true;
                }
            }
            else if (walking && ClipLibrary.Has(CarryClip) && ClipLibrary.TryGet(CarryClip, out CityClip carry) && carry.Loop && player.Play(carry.Clip, 0.3f))
            {
                _carrying = player;
                _carryingOver = false;
                _carryMade = carry.Speed;
            }
        }

        // Whatever showed the last parcel goes back to the props: it went indoors with whoever took it.
        private void DropParcel()
        {
            _passTo = null;
            _passHoldAt = -1f;
            if (_carrying != null)
            {
                if (_carryingOver)
                {
                    _carrying.StopOver(0.2f);
                }
                else
                {
                    _carrying.Stop(0.2f);
                }
                _carrying = null;
            }
            _handed.Take();
            if (_onStep != null)
            {
                Ctx.Props.Return(_onStep);
                _onStep = null;
            }
        }

        // The last recipient walks the parcel to their door and is gone.
        private void SeeIn(float now)
        {
            if (_goingIn == null)
            {
                return;
            }
            if (!_goingIn.IsValid)
            {
                DropParcel();
                _goingIn = null;
                return;
            }
            _handed.Show(_goingIn.Visible);
            if (!_goingWalks)
            {
                if (now < _goingAt)
                {
                    return;
                }
                _goingWalks = true;
                if (JobSpots.Flat(_goingIn.Position, _goingTo) > 0.8f && _goingIn.MoveTo(_goingTo, Pace.Walk))
                {
                    // Walking with a box: the arms that carry it go on over the game's walk.
                    if (_handed.Held && _carrying == null)
                    {
                        Carry(_goingIn, true);
                    }
                    return;
                }
                _goingUntil = now + 2.5f;   // already at the door: a moment more, then in
                return;
            }
            if (_carrying != null && !_carryingOver && _carryMade > 0f)
            {
                // A whole-body walk is played at their real speed, so the planted foot does not slide.
                UnityEngine.AI.NavMeshAgent agent = _goingIn.Character.navmeshAgent;
                _carrying.MatchSpeed(agent != null && agent.enabled ? agent.velocity.magnitude : 0f, _carryMade);
            }
            bool there = now - _goingAt > 1.2f && _goingIn.HasArrived && JobSpots.Flat(_goingIn.Position, _goingTo) < 1.2f;
            if (there || now > _goingUntil)
            {
                DropParcel();
                Ctx.Cast.Release(_goingIn);
                _goingIn = null;
            }
        }
    }
}
