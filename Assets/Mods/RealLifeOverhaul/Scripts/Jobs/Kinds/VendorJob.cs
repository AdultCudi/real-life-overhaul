using Characters.EmojiSystem;
using Helpers;
using UnityEngine;
using UnityEngine.AI;
using Random = UnityEngine.Random;

namespace RealLifeOverhaul
{
    /// <summary>
    /// A shift at a pavement drinks cart. The cart is set up on the stretch of pavement the
    /// player is standing on; they take their place behind it. Customers come along the
    /// pavement one after another, walk up to the counter (a second waits their turn behind the
    /// first), and ask for one of three drinks. The player picks it with the matching key. The
    /// right drink, quickly, earns a tip; the wrong one costs the customer's patience; a
    /// customer kept waiting too long walks off, and three of those end the shift. Paid per
    /// customer served, like an hourly wage, plus the tips.
    ///
    /// The customers are the job's own two characters (JobCast), who arrive and leave on foot
    /// and stand only at places that were tested when the cart was put down. The cart is the
    /// modelled <c>vendor_cart</c>, or the living city's coffee cart until that is in the
    /// bundle; with neither there is no cart and the job is not offered. Serving is JobClip.Serve
    /// on the player, who is held still for the moment it takes; the cup stands on the counter
    /// and is in the customer's hand, at the grip of the clip it was made for, as they take it.
    /// </summary>
    public sealed class VendorJob : Job
    {
        private enum Step { ToPost, Open }
        private enum Guest { None, Coming, Queued, Stepping, Ordering, Served, Leaving }

        // Where the two stand and where the cup goes down are fixed by the clips (CityAssets/README.md, "A counter"): the
        // vendor's hand sets it down 0.78 m ahead of them and the customer's closes on it 0.62 m ahead of them, which on
        // a counter 0.80 m deep is one spot, 0.10 m to the customer's side of the cart's middle.
        private const float VendorBack = 0.68f;      // metres from the cart's middle to where the vendor stands
        private const float CustomerOut = 0.72f;     // ...and to the customer at the counter
        private const float CupOut = 0.10f;          // ...and to the cup on the counter between them
        private const float CupRest = 0.10f;         // seconds the cup stands on the counter before the customer's hand closes on it
        private const string ReachClip = "counter_reach";
        private const float QueueOut = 2.1f;         // ...and to whoever waits behind them
        private const float CounterHeight = 0.975f;
        private const float PostReach = 0.9f;
        private const float AwayFromPost = 2.6f;     // further than this from the post and nobody is being served
        private const float Patience = 11f;          // seconds a customer waits for their drink
        private const float WrongCost = 3f;          // ...less this for each wrong one
        private const float QuickTip = 3.5f;         // served within this: the full tip
        private const float FairTip = 6.5f;          // within this: a small one
        private const int MissesAllowed = 3;
        private const float AbandonAfter = 25f;      // seconds away from the cart before the shift is given up
        private const string CartProp = "vendor_cart";
        private const string CupProp = "coffee_cup";

        private static readonly string[] CupGrips = { "counter_reach", "cup_hold" };
        private static readonly string[] Thanks = { "reallife_job_vendor_thanks_1", "reallife_job_vendor_thanks_2", "reallife_job_vendor_thanks_3" };
        private static readonly string[] Wrong = { "reallife_job_vendor_wrong_1", "reallife_job_vendor_wrong_2" };
        private static readonly string[] Left = { "reallife_job_vendor_left_1", "reallife_job_vendor_left_2" };
        private static readonly string[] ItemKeys = { "reallife_job_vendor_item_1", "reallife_job_vendor_item_2", "reallife_job_vendor_item_3" };

        private readonly JobDef _def = JobCatalogue.Find(JobCatalogue.Vendor);
        private readonly StreetActor[] _who = new StreetActor[JobCast.Size];
        private readonly Guest[] _state = new Guest[JobCast.Size];
        private readonly float[] _until = new float[JobCast.Size];
        private readonly float[] _walkAt = new float[JobCast.Size];
        private readonly HeldProp _cupHeld = new HeldProp();
        // A drink served with the mod's clips, a moment at a time: the customer reaches, the cup is on the counter as the
        // vendor's hand lets go of it, and it is the customer's as theirs closes on it. 0: none of that is pending.
        private const int BeatReach = 1, BeatCupDown = 2, BeatCupTaken = 3;
        private int _beat;
        private StreetActor _beatWho;
        private int _beatLease;
        private float _cupDownAt;
        private float _cupTakenAt;
        private readonly string[] _items = new string[JobKeys.Choices];
        private readonly string[] _orders = new string[JobKeys.Choices];

        private Step _step;
        private Transform _cart;
        private Transform _cup;
        private Vector3 _cartAt;
        private Vector3 _post;
        private Vector3 _counter;
        private Vector3 _queue;
        private Vector3 _outwards;
        private Vector3 _along;
        private int _customers;       // how many the shift is
        private int _came;
        private int _served;
        private int _misses;
        private int _tips;
        private int _front = -1;      // who is at the counter
        private int _wants;
        private float _orderedAt;
        private float _orderEnds;
        private float _nextArrival;
        private float _awaySince = -1f;
        private bool _paid;
        private int _shown = -1;
        private int _line = -1;
        private string _postLabel;

        public override JobDef Def => _def;

        public override bool QuitIsFailure => _came > 0 && _served * 2 < _came;

        protected override bool OnBegin(float now)
        {
            if (Ctx.Driving || PlayerHelper.IsUsingVehicle)
            {
                Refusal = "reallife_job_on_foot";
                return false;
            }
            _customers = Ctx.FullShift ? Def.ShiftUnits : Mathf.Min(Def.ShiftUnits, JobState.LeftToday(Def));
            if (_customers <= 0)
            {
                Refusal = "reallife_job_no_daily";
                return false;
            }
            if (!PropLibrary.Ready || !JobProps.Has(CartProp))
            {
                Refusal = "reallife_job_no_here";
                Ctx.Logger?.Info("Jobs: no vendor shift: the prop bundle has no cart.");
                return false;
            }
            if (!FindPitch(Ctx.PlayerPosition))
            {
                Refusal = "reallife_job_vendor_no_room";
                return false;
            }
            _cart = Ctx.Props.Stand(CartProp, _cartAt, _outwards);
            if (_cart == null)
            {
                Refusal = "reallife_job_no_here";
                return false;
            }
            // Solid to people on foot, as the living city's cart is: an obstacle agents steer round, not one that cuts the mesh.
            if (!_cart.TryGetComponent(out NavMeshObstacle solid))
            {
                solid = _cart.gameObject.AddComponent<NavMeshObstacle>();
                solid.shape = NavMeshObstacleShape.Box;
                solid.center = new Vector3(0f, 1f, 0f);
                solid.size = new Vector3(1.6f, 2f, 0.9f);
                solid.carving = false;
            }
            for (int i = 0; i < JobKeys.Choices; i++)
            {
                _items[i] = JobText.T(ItemKeys[i]);
                _orders[i] = JobText.T("reallife_job_vendor_obj_order", "item", _items[i]);
            }
            _postLabel = JobText.T("reallife_job_vendor_marker_post");
            _step = Step.ToPost;
            Mark(_post, _postLabel);
            Show(JobText.T("reallife_job_vendor_obj_post"));
            Status = null;
            return true;
        }

        // Along the pavement, counter towards the side with room: the cart's four corners, the vendor behind, the customer
        // in front and the one waiting behind them all on pavement with room, or the cart is not put here.
        private bool FindPitch(Vector3 near)
        {
            if (!CitySpots.Pavement(near, 8f, out Vector3 start))
            {
                return false;
            }
            for (int attempt = 0; attempt < 6; attempt++)
            {
                Vector3 place = attempt == 0 ? start : start + Quaternion.Euler(0f, attempt * 60f, 0f) * Vector3.forward * 3.5f;
                if (!CitySpots.Stand(place, 1.1f, out Vector3 cart) || !CitySpots.Along(cart, out Vector3 along) || JobSpots.Flat(cart, near) < 1.6f)
                {
                    continue;
                }
                Vector3 across = Vector3.Cross(Vector3.up, along);
                for (int side = 0; side < 2; side++)
                {
                    Vector3 outwards = side == 0 ? across : -across;
                    if (CitySpots.OnPavement(cart + along * 0.85f + outwards * 0.5f, out _) && CitySpots.OnPavement(cart - along * 0.85f + outwards * 0.5f, out _)
                        && CitySpots.OnPavement(cart + along * 0.85f - outwards * 0.5f, out _) && CitySpots.OnPavement(cart - along * 0.85f - outwards * 0.5f, out _)
                        && CitySpots.Stand(cart - outwards * VendorBack, 0.35f, out _post)
                        && CitySpots.Stand(cart + outwards * CustomerOut, 0.35f, out _counter)
                        && CitySpots.Stand(cart + outwards * QueueOut, 0.35f, out _queue))
                    {
                        _cartAt = cart;
                        _outwards = outwards;
                        _along = along;
                        return true;
                    }
                }
            }
            return false;
        }

        // ---------------------------------------------------------------- the tick

        public override void Tick(float now)
        {
            float fromPost = JobSpots.Flat(Ctx.Player.transform.position, _post);
            bool atPost = !Ctx.Driving && !PlayerHelper.IsUsingVehicle && fromPost < (_step == Step.ToPost ? PostReach : AwayFromPost);
            if (_step == Step.ToPost)
            {
                if (atPost)
                {
                    _step = Step.Open;
                    Unmark();
                    _nextArrival = now + 2f;
                    Ctx.TurnPlayer(_cartAt + _outwards * 3f);
                    Ctx.Say("reallife_job_vendor_open", 6f);
                    Display(now);
                }
                return;
            }

            // Away from the cart: customers wait (and run out of patience); too long away and the shift is given up.
            if (!atPost)
            {
                if (_awaySince < 0f)
                {
                    _awaySince = now;
                    Mark(_post, _postLabel);
                    _shown = -1;
                    Choices = null;
                    ClockSeconds = -1;
                    Show(JobText.T("reallife_job_vendor_obj_back"));
                }
                else if (now - _awaySince > AbandonAfter)
                {
                    Ctx.Say("reallife_job_vendor_abandoned", 8f);
                    Conclude(true);
                    return;
                }
            }
            else if (_awaySince >= 0f)
            {
                _awaySince = -1f;
                Unmark();
                _shown = -1;
            }

            for (int i = 0; i < JobCast.Size; i++)
            {
                Attend(i, now);
            }
            Arrivals(now);
            if (_awaySince < 0f)
            {
                Display(now);
            }
            if (_came >= _customers && _front < 0 && _state[0] == Guest.None && _state[1] == Guest.None)
            {
                Ctx.Say("reallife_job_vendor_done", 7f);
                Conclude(false);
            }
        }

        // Somebody new comes along the pavement while there is a free place and customers left in the shift.
        private void Arrivals(float now)
        {
            if (now < _nextArrival || _came >= _customers)
            {
                return;
            }
            int slot = _state[0] == Guest.None ? 0 : _state[1] == Guest.None ? 1 : -1;
            if (slot < 0)
            {
                return;
            }
            _nextArrival = now + Random.Range(2.5f, 6f);
            // Only ever put down out of the player's sight: round the corner or further up the street, or not this time.
            if (!CitySpots.PavementRing(_cartAt, 11f, 20f, true, out Vector3 from) && !CitySpots.PavementRing(_cartAt, 20f, 34f, true, out from))
            {
                return;
            }
            StreetActor person = Ctx.Cast.Put(from, false);
            if (person == null)
            {
                return;
            }
            // To the counter if nobody is there or on their way to it, else to the place behind.
            bool counterFree = _front < 0;
            if (!person.MoveTo(counterFree ? _counter : _queue, Pace.Walk))
            {
                Ctx.Cast.Release(person);
                return;
            }
            _who[slot] = person;
            _state[slot] = counterFree ? Guest.Stepping : Guest.Coming;
            _until[slot] = now + 40f;
            if (counterFree)
            {
                _front = slot;
            }
            _came++;
        }

        // One customer's part, a step at a time.
        private void Attend(int i, float now)
        {
            StreetActor person = _who[i];
            if (_state[i] == Guest.None)
            {
                return;
            }
            if (person == null || !person.IsValid)
            {
                Gone(i);
                return;
            }
            switch (_state[i])
            {
                case Guest.Coming:
                    if (Reached(person, _queue, now, i))
                    {
                        person.StandAt(_queue, _cartAt);
                        _state[i] = Guest.Queued;
                        _until[i] = now + 75f;
                    }
                    break;
                case Guest.Queued:
                    if (_front < 0 && person.MoveTo(_counter, Pace.Walk))
                    {
                        _front = i;
                        _state[i] = Guest.Stepping;
                        _until[i] = now + 15f;
                    }
                    else if (now >= _until[i])
                    {
                        Gone(i);   // never got to the front: gives up
                    }
                    break;
                case Guest.Stepping:
                    if (Reached(person, _counter, now, i))
                    {
                        person.StandAt(_counter, _cartAt);
                        _state[i] = Guest.Ordering;
                        _wants = Random.Range(0, JobKeys.Choices);
                        _orderedAt = now;
                        _orderEnds = now + Patience;
                        _shown = -1;
                    }
                    break;
                case Guest.Ordering:
                    if (now >= _orderEnds)
                    {
                        // Kept waiting: they go, and say so.
                        _misses++;
                        Ctx.Animations.Play(person.Character, JobClip.Annoyed);
                        Ctx.Cast.Emoji(person, CharacterEmojiName.CommonRedAngry, 2.5f);
                        Ctx.Say(JobText.Pick(Left, ref _line), 5f);
                        Leave(i, now, 1.4f);
                        if (_misses >= MissesAllowed)
                        {
                            Ctx.Say("reallife_job_vendor_sacked", 8f);
                            Conclude(true);
                        }
                    }
                    break;
                case Guest.Served:
                    if (now >= _until[i])
                    {
                        Leave(i, now, 0f);
                    }
                    else if (_beat == 0 && _cup != null && now >= _until[i] - 1.0f)
                    {
                        // The cup leaves the counter for their hand as the hand closes on it.
                        Ctx.Props.Return(_cup);
                        _cup = null;
                        _cupHeld.Give(person.Character, Ctx.Props, CupGrips);
                    }
                    break;
                case Guest.Leaving:
                    if (now < _walkAt[i] + 1.5f)
                    {
                        break;   // a last moment at the counter, then a few steps before "arrived" means anything
                    }
                    if (person.HasArrived || now >= _until[i] || JobSpots.Flat(person.Position, _cartAt) > 26f)
                    {
                        Gone(i);
                    }
                    break;
            }
        }

        private bool Reached(StreetActor person, Vector3 place, float now, int i)
        {
            if (person.HasArrived && JobSpots.Flat(person.Position, place) < 0.9f)
            {
                return true;
            }
            if (now >= _until[i])
            {
                Gone(i);   // never got here (something in the way): no blame, the shift is one customer shorter
            }
            return false;
        }

        // Away along the pavement; taken off the street once well away.
        private void Leave(int i, float now, float after)
        {
            StreetActor person = _who[i];
            if (_front == i)
            {
                _front = -1;
                _shown = -1;
            }
            _cupHeld.Take();
            _beat = 0;
            if (_cup != null)
            {
                Ctx.Props.Return(_cup);
                _cup = null;
            }
            _state[i] = Guest.Leaving;
            _walkAt[i] = now + after;
            _until[i] = now + 30f + after;
            if (after <= 0f)
            {
                Walk(person);
            }
            else if (Ctx.Director != null && Ctx.Director.isActiveAndEnabled)
            {
                Ctx.Director.StartCoroutine(WalkAfter(person, person.Lease, after));
            }
        }

        private void Walk(StreetActor person)
        {
            // If there is nowhere to walk to they stay where they are, and are taken off the street by their own
            // slot a moment later (the Leaving step): never released from here, where the slot would not know.
            if (StreetSpots.AwayFrom(person.Position, _cartAt, 22f, false, out Vector3 to))
            {
                person.MoveTo(to, Pace.Walk);
            }
        }

        private System.Collections.IEnumerator WalkAfter(StreetActor person, int lease, float seconds)
        {
            yield return new WaitForSeconds(seconds);
            if (person != null && person.IsValid && person.InUse && person.Lease == lease && Ctx != null)
            {
                Walk(person);
            }
        }

        private void Gone(int i)
        {
            if (_front == i)
            {
                _front = -1;
                _shown = -1;
                _cupHeld.Take();
                _beat = 0;
                if (_cup != null)
                {
                    Ctx.Props.Return(_cup);
                    _cup = null;
                }
            }
            Ctx.Cast.Release(_who[i]);
            _who[i] = null;
            _state[i] = Guest.None;
        }

        // ---------------------------------------------------------------- serving

        public override void Choose(int choice, float now)
        {
            if (_front < 0 || _state[_front] != Guest.Ordering || _awaySince >= 0f || Ctx.Self.Holding)
            {
                return;
            }
            StreetActor person = _who[_front];
            ChoiceLit = choice;
            Ctx.TurnPlayer(person.Position);
            float reach = Mathf.Max(0.9f, Ctx.Animations.Play(Ctx.Player.Character, JobClip.Serve));
            Ctx.Self.Hold(Mathf.Min(reach, 2.2f));   // on the spot until the serving clip (the whole body) has stood them up again
            if (choice != _wants)
            {
                _orderEnds -= WrongCost;
                Ctx.Animations.Play(person.Character, JobClip.Refuse);
                Ctx.Cast.Emoji(person, CharacterEmojiName.CommonYellowNeutral, 1.6f);
                Ctx.Say(JobText.Pick(Wrong, ref _line), 4f);
                return;
            }
            float took = now - _orderedAt;
            _served++;
            if (took <= QuickTip)
            {
                _tips += Mathf.RoundToInt(Random.Range(2f, 3.5f) * Ctx.Rate);
            }
            else if (took <= FairTip && Random.value < 0.6f)
            {
                _tips += Mathf.RoundToInt(1f * Ctx.Rate);
            }
            // The cup on the counter between them; they reach for it and it is theirs.
            float take;
            float release = Ctx.Animations.Moment(JobClip.Serve, "release");
            if (release >= 0f && ActivityAnimators.Working && ClipLibrary.Has(ReachClip) && ClipLibrary.TryGet(ReachClip, out CityClip reaching))
            {
                // Both clips are the mod's: each thing happens at the moment its clip says, to the frame. The customer
                // starts reaching so that their hand closes just after the vendor's has let go.
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
                // The game's gestures (or half the clips): the cup is there at once and theirs a moment later, as before.
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
                        take = grab.Length;   // made for exactly this counter: the hand goes to the cup and comes back with it
                    }
                }
                if (take <= 0f)
                {
                    take = Ctx.Animations.Play(person.Character, JobClip.Receive);   // (one or the other: a gesture over the reach would take its arm)
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
            _shown = -1;
        }

        /// <summary>The next moment of a drink changing hands (see the Beat constants).</summary>
        public override void Moment(float now)
        {
            int beat = _beat;
            _beat = 0;
            StreetActor person = _beatWho;
            if (beat == 0 || _front < 0 || _who[_front] != person || _state[_front] != Guest.Served
                || person == null || !person.IsValid || !person.InUse || person.Lease != _beatLease)
            {
                return;   // they have gone meanwhile; whatever stood on the counter went with them (Leave, Gone)
            }
            switch (beat)
            {
                case BeatReach:
                    if (ClipLibrary.TryGet(ReachClip, out CityClip reaching))
                    {
                        ActivityAnimator hand = ActivityAnimators.For(person.Character.animator);
                        if (hand != null && hand.Usable)
                        {
                            hand.Play(reaching.Clip, 0.2f);   // made for exactly this counter: the hand goes to the cup and comes back with it
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

        // What the panel says and offers: rebuilt only when the customer at the counter, or what they want, changes.
        private void Display(float now)
        {
            bool ordering = _front >= 0 && _state[_front] == Guest.Ordering;
            ClockSeconds = ordering ? Mathf.CeilToInt(_orderEnds - now) : -1;
            int show = ordering ? 1 + _wants : 0;
            if (show == _shown)
            {
                return;
            }
            _shown = show;
            if (ordering)
            {
                Show(_orders[_wants]);
                Choices = _items;
                ChoiceLit = -1;
            }
            else
            {
                Choices = null;
                Status = JobText.T("reallife_job_vendor_status", "served", _served.ToString(), "count", _customers.ToString());
                Show(JobText.T(_came >= _customers ? "reallife_job_vendor_obj_last" : "reallife_job_vendor_obj_wait"));
            }
        }

        // ---------------------------------------------------------------- the end

        private void Conclude(bool badly)
        {
            Settle();
            Finish(badly && _served * 2 < Mathf.Max(1, _came) ? JobEnd.Failed : JobEnd.Done);
        }

        private void Settle()
        {
            if (_paid || _served <= 0 || Ctx == null)
            {
                return;
            }
            _paid = true;
            var slip = new PaySlip
            {
                Units = _served,
                // A wage by the customer, rounded on the total; with a cart of their own, the takings less what the drinks cost.
                Base = Mathf.RoundToInt(_served * (Ctx.Perk(CareerPerk.OwnCart) ? CareerCatalogue.CartTakings - CareerCatalogue.CartStock : Def.UnitPay) * Ctx.Rate),
                Tips = Mathf.RoundToInt(_tips * Ctx.TipFactor),
                Bonus = _misses == 0 && _served >= _customers ? Mathf.RoundToInt(_served * Def.UnitPay * Ctx.Rate * 0.1f) : 0   // nobody kept waiting
            };
            Ctx.Pay(this, slip, "reallife_job_vendor_slip");
        }

        public override void End(JobEnd why)
        {
            if (Ctx == null)
            {
                return;
            }
            Choices = null;
            Prompt = null;
            _cupHeld.Take();
            if (_cup != null)
            {
                Ctx.Props.Return(_cup);
                _cup = null;
            }
            for (int i = 0; i < JobCast.Size; i++)
            {
                Ctx.Cast.Release(_who[i]);
                _who[i] = null;
                _state[i] = Guest.None;
            }
            if (_cart != null)
            {
                Ctx.Props.Return(_cart);
                _cart = null;
            }
            if (why != JobEnd.Fault)
            {
                Settle();
            }
        }
    }
}
