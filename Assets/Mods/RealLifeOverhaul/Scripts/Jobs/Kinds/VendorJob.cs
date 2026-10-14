using System;
using System.Collections.Generic;
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
    /// <summary>
    /// A shift at a pavement drinks cart, worked as an employee's rota shift or taken as a gig. The cart is set
    /// up on the stretch of pavement the player is standing on and is solid (<see cref="SolidFootprint"/>).
    ///
    /// TAKING THE PLACE. Nothing is pressed: the player walks behind the cart and the stand
    /// (<see cref="JobStand"/>) takes them the last step, turns them to face the customers, keeps them there
    /// and puts the vendor's working pose on them. The first time, they are put in the vendor's uniform
    /// (<see cref="JobUniform"/>), which the director takes off again however the shift ends.
    ///
    /// BY HAND. Customers come along the pavement one after another, walk up to the counter (a second waits
    /// their turn behind the first) and ask for one of three drinks. The game's panel over a place
    /// (<see cref="WorldPanel"/>) stands over the cart with the order, the customer's patience and a button for
    /// each drink on the game's own keys. The right drink, quickly, earns a tip; the wrong one costs the
    /// customer's patience; a customer kept waiting too long walks off, and three of those end the shift.
    ///
    /// CALLING OUT. A button of the same panel: the vendor calls to the game's pedestrians near the cart, and
    /// each reacts by chance (see <see cref="Hawk"/>): tempted (a green face, some stop to look, and a
    /// customer more comes to the cart, soon), annoyed (a red face and a gesture), or not at all.
    ///
    /// BY THE CLOCK. Another button hands the rest of the shift to the game's clock, as the game's own job does
    /// it (<see cref="ShiftSkip"/>): <see cref="ShiftRates.Minutes"/> game minutes a customer, at
    /// <see cref="ShiftRates.Share"/> of the pay a customer, no tips and no bonus.
    ///
    /// THE FIGURES are on the HUD for the whole shift in a copy of the game's job panel
    /// (<see cref="ShiftCard"/>): served of the shift's total, walk-offs of the three allowed, takings and tips,
    /// time on shift and about how long is left. Each line is written only when one of its figures changes.
    ///
    /// The customers are the job's own two characters (JobCast), who arrive and leave on foot and stand only
    /// at places that were tested when the cart was put down. Serving is JobClip.Serve on the player; the cup
    /// stands on the counter and is in the customer's hand, at the grip of the clip it was made for, as they
    /// take it. Everything here runs on the director's tick, five times a second.
    /// </summary>
    public sealed class VendorJob : Job, ISkippableShift
    {
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
        private const float CartHalfLength = 0.75f;  // the cart's body, along the pavement
        private const float CartHalfDepth = 0.40f;   // ...and its counter, across it
        private const float PostReach = 0.9f;        // the zone behind the cart that takes the player
        private const float PanelUp = 2.05f;         // metres up the cart the game's panel stands over
        public const float Patience = 11f;           // seconds a customer waits for their drink
        public const float WrongCost = 3f;           // ...less this for each wrong one
        public const float QuickTip = 3.5f;          // served within this: the full tip
        private const float FairTip = 6.5f;          // within this: a small one
        private const int MissesAllowed = 3;
        public const float AbandonAfter = 25f;       // seconds away from the cart before the shift is given up
        private const float Debounce = 0.7f;         // seconds after an order ends in which the panel's other buttons do nothing (a key meant for a drink)
        private const string CartProp = "vendor_cart";
        private const string CupProp = "coffee_cup";
        private const string IdleClip = "counter_wipe";

        // Calling out. Every figure of its balance is here.
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
        private const int AnnoyedKept = 12;

        private static readonly string[] CupGrips = { "counter_reach", "cup_hold" };
        private static readonly string[] Thanks = { "reallife_job_vendor_thanks_1", "reallife_job_vendor_thanks_2", "reallife_job_vendor_thanks_3" };
        private static readonly string[] Wrong = { "reallife_job_vendor_wrong_1", "reallife_job_vendor_wrong_2" };
        private static readonly string[] Left = { "reallife_job_vendor_left_1", "reallife_job_vendor_left_2" };
        private static readonly string[] ItemKeys = { "reallife_job_vendor_item_1", "reallife_job_vendor_item_2", "reallife_job_vendor_item_3" };
        private const string ButtonHawk = "reallife_job_vendor_button_hawk";
        private const string ButtonSkip = "reallife_job_vendor_button_skip";
        private const string ButtonAway = "reallife_job_vendor_button_away";

        private readonly JobDef _def = JobCatalogue.Find(JobCatalogue.Vendor);
        private readonly int _minutesEach = ShiftRates.Minutes(JobCatalogue.Find(JobCatalogue.Vendor));   // game minutes the clock takes for one customer (ShiftRates: a shift of twelve is four game hours)
        private readonly StreetActor[] _who = new StreetActor[JobCast.Size];
        private readonly Guest[] _state = new Guest[JobCast.Size];
        private readonly float[] _until = new float[JobCast.Size];
        private readonly float[] _walkAt = new float[JobCast.Size];
        private readonly HeldProp _cupHeld = new HeldProp();
        private readonly WorldPanel _panel = new WorldPanel();
        private readonly SolidFootprint _solid = new SolidFootprint();
        private readonly PedestrianTakeover[] _lookers = { new PedestrianTakeover(), new PedestrianTakeover() };
        private readonly float[] _lookUntil = new float[LookersMost];
        private readonly int[] _annoyed = new int[AnnoyedKept];
        private readonly Action[] _onDrink = new Action[JobKeys.Choices];
        private readonly string[] _items = new string[JobKeys.Choices];
        private readonly string[] _orders = new string[JobKeys.Choices];
        private readonly string[] _seconds = new string[(int)Patience + 2];
        private readonly StringBuilder _text = new StringBuilder(160);
        private Action _onHawk;
        private Action _onSkip;
        private Action _onAway;

        // A drink served with the mod's clips, a moment at a time: the customer reaches, the cup is on the counter as the
        // vendor's hand lets go of it, and it is the customer's as theirs closes on it. 0: none of that is pending.
        private const int BeatReach = 1, BeatCupDown = 2, BeatCupTaken = 3;
        private int _beat;
        private StreetActor _beatWho;
        private int _beatLease;
        private float _cupDownAt;
        private float _cupTakenAt;

        private Transform _cart;
        private Transform _cup;
        private Vector3 _cartAt;
        private Vector3 _post;
        private Vector3 _counter;
        private Vector3 _queue;
        private Vector3 _outwards;
        private Vector3 _along;
        private int _customers;       // how many the shift is (calling out can add to it)
        private int _came;            // how many have set off for the cart
        private int _served;          // by hand and by the clock
        private int _hand;            // ...of which by hand
        private int _lost;            // came and went unserved, with or without blame
        private int _misses;          // ...of which walked off kept waiting
        private int _tips;
        private int _clockPay;        // what the clock's customers have been paid, for the takings line
        private int _front = -1;      // who is at the counter
        private int _wants;
        private float _orderedAt;
        private float _orderEnds;
        private float _orderOver = -10f;
        private float _nextArrival;
        private float _serveUntil;
        private float _awaySince = -1f;
        private float _startMinutes;
        private bool _open;           // the player has taken their place at least once
        private bool _paid;
        private bool _ending;
        private int _line = -1;
        private string _postLabel;
        private string _title;
        private string _waitWords;
        private string _lastWords;
        private string _toComeWords;
        private string _patienceWords;

        // The clock doing the work.
        private ShiftSkip _skip;
        private int _skipLeft;        // customers the clock had to serve when it started
        private int _skipDone;        // ...and how many of them it has, so far

        // Calling out.
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

        // What is on screen, so that a text is made only when what it says has changed.
        private int _cardFigures = int.MinValue;
        private int _cardMoney = int.MinValue;
        private int _cardTime = int.MinValue;
        private int _cardState = int.MinValue;
        private int _panelState = int.MinValue;
        private int _panelUp;         // 0 not up, 1 up, 2 refused by another panel

        public override JobDef Def => _def;

        public override bool PassingTime => _skip != null && _skip.Working;

        /// <summary>While the panel over the cart is up its first button is on the game's interact key: the mod's own use of that key stands aside.</summary>
        public override bool OwnsInteractKey => Ctx != null && Ctx.Stand.Standing;

        public override bool QuitIsFailure => _came > 0 && _served * 2 < _came;

        private int LeftToServe => Mathf.Max(0, _customers - _served - _lost);

        private bool _sentBack;   // the difficult customer has had their say

        private float PayEach => (Ctx.Perk(CareerPerk.OwnCart) ? CareerCatalogue.CartTakings - CareerCatalogue.CartStock : Def.UnitPay) * Ctx.Rate;

        protected override bool OnBegin(float now)
        {
            if (Ctx.Driving || PlayerHelper.IsUsingVehicle)
            {
                Refusal = "reallife_job_on_foot";
                return false;
            }
            _customers = ShiftSize();   // the offer's number of customers
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
            // Solid to everyone on foot from the start: the player is on foot beside it for as long as it stands
            // (a shift is given up after twenty-five seconds away), so the mesh is cut once and closed once.
            _solid.Fit(_cart, CartHalfLength, CartHalfDepth, Mathf.Min(VendorBack, CustomerOut));
            _solid.Solid(true, now);

            for (int i = 0; i < JobKeys.Choices; i++)
            {
                int drink = i;
                _items[i] = JobText.T(ItemKeys[i]);
                _orders[i] = JobText.T("reallife_job_vendor_panel_order", "item", _items[i]);
                _onDrink[i] = () => Choose(drink, Time.time);
            }
            _onHawk = () => Hawk(Time.time);
            _onSkip = OfferSkip;
            _onAway = StepAway;
            for (int i = 0; i < _seconds.Length; i++)
            {
                _seconds[i] = JobText.T("reallife_job_vendor_panel_seconds", "seconds", i.ToString());
            }
            _postLabel = JobText.T("reallife_job_vendor_marker_post");
            _waitWords = JobText.T("reallife_job_vendor_obj_wait");
            _lastWords = JobText.T("reallife_job_vendor_obj_last");
            _toComeWords = JobText.T("reallife_job_vendor_panel_to_come");
            _patienceWords = JobText.T("reallife_job_vendor_panel_patience");
            _title = JobText.T(Def.NameKey);
            _startMinutes = TimeHelper.NowInMinutes();
            Ctx.Stand.Place(_post, _outwards, PostReach, IdleClip);
            Mark(_post, _postLabel);
            ShiftCard card = Ctx.Card;
            if (card != null)
            {
                card.Title(Ctx.Shift != null ? JobText.T(Ctx.Shift.Rank.TitleKey) : _title);
                card.Line(0, Ctx.Shift != null
                    ? JobText.T("reallife_job_card_employer", "employer", JobText.T(Ctx.Shift.Employer.NameKey), "time", CareerClock.Span(CareerState.RotaHour))
                    : JobText.T("reallife_job_card_gig"), WorldPanel.White());
            }
            Card(now);
            if (Meta.Dev.Verbose)
            {
                Ctx.Logger?.Info("Jobs: vendor cart put down " + JobSpots.Flat(_cartAt, Ctx.PlayerPosition).ToString("F1") + " m from the player; the vendor's place is "
                                 + JobSpots.Flat(_post, Ctx.PlayerPosition).ToString("F1") + " m away (the zone that takes the player is " + PostReach.ToString("F1")
                                 + " m round it); " + _customers + " customers; shown in the game's own interface: job panel "
                                 + (card != null ? "copied" : "NOT copied (the panel over the cart is all there is)") + ", the game's guider points at the place.");
            }
            return true;
        }

        // Along the pavement, counter towards the side with room: the cart's four corners, the vendor behind, the customer
        // in front and the one waiting behind them all on pavement with room, and a way on foot from the counter round to
        // the vendor's place, or the cart is not put here.
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
                        && CitySpots.Stand(cart + outwards * QueueOut, 0.35f, out _queue)
                        && CitySpots.Reachable(_counter, _post))
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
            if (_solid.VerifyDue && _solid.Verify(now, _post, _counter, out bool cut, out bool kept, out bool way))
            {
                Ctx.Logger?.Info("Jobs check (cart solid): walker radius " + _solid.Radius.ToString("F2") + " m, box " + (CartHalfLength * 2f).ToString("F2") + " x "
                                 + (_solid.HalfDepth * 2f).ToString("F2") + " m; cut out of the mesh: the cart's middle " + (cut ? "yes" : "no")
                                 + "; still walkable: the vendor's place and the customer's place " + (kept ? "yes" : "no") + ", a way on foot from one to the other "
                                 + (way ? "yes" : "no") + ": " + (cut && kept && way ? "PASS" : !kept || !way ? "FAIL (the cut is taken back: the cart is steered round, not solid)"
                                     : "FAIL (the cart can be walked through)") + ".");
            }
            if (_skip != null)
            {
                if (_skip.Open && _skip.Shown)
                {
                    Card(now);   // the clock is doing the work (or the offer is up, with the game paused)
                    return;
                }
                _skip.Close();   // over, or dropped by the game's panel: either way it settles nothing more
                _skip = null;
                _panelState = int.MinValue;
                _cardState = int.MinValue;
            }

            bool onFoot = !Ctx.Driving && !PlayerHelper.IsUsingVehicle;
            bool standing = Ctx.Stand.Tick(Ctx.Player, onFoot, now);
            if (standing)
            {
                if (!_open && Ctx.Stand.Ready)
                {
                    _open = true;
                    _nextArrival = now + 2f;
                    Ctx.Uniform.Wear(Ctx.Player, CityLook.Vendor);
                    Ctx.Say("reallife_job_vendor_open", 6f);
                }
                if (HasMarker)
                {
                    Unmark();
                }
                _awaySince = -1f;
            }
            else
            {
                HidePanel();
                if (!HasMarker)
                {
                    Mark(_post, _postLabel);
                }
                if (_open)
                {
                    // Away from the cart: customers wait (and run out of patience); too long away and the shift is given up.
                    if (_awaySince < 0f)
                    {
                        _awaySince = now;
                    }
                    else if (now - _awaySince > AbandonAfter)
                    {
                        Ctx.Say("reallife_job_vendor_abandoned", 8f);
                        Conclude(true);
                        return;
                    }
                }
            }
            if (_hailUntil >= 0f && now >= _hailUntil)
            {
                _hailUntil = -1f;
                Ctx.Animations.Stop(Ctx.Player.Character);   // the calling arm comes down
            }
            Lookers(now);
            if (_open)
            {
                for (int i = 0; i < JobCast.Size; i++)
                {
                    Attend(i, now);
                }
                if (Over)
                {
                    return;
                }
                Arrivals(now);
            }
            Card(now);
            if (standing && Ctx.Stand.Ready)
            {
                Panel(now);
            }
            if (_open && LeftToServe <= 0 && _front < 0 && _state[0] == Guest.None && _state[1] == Guest.None)
            {
                Ctx.Say("reallife_job_vendor_done", 7f);
                Conclude(false);
            }
        }

        // Somebody new comes along the pavement while there is a free place and customers left in the shift.
        private void Arrivals(float now)
        {
            if (now < _nextArrival || _came - _served - _lost >= LeftToServe)
            {
                return;
            }
            int slot = _state[0] == Guest.None ? 0 : _state[1] == Guest.None ? 1 : -1;
            if (slot < 0)
            {
                return;
            }
            if (_soon > 0)
            {
                _soon--;
                _nextArrival = now + Random.Range(1.2f, 2.4f);   // somebody was tempted: the next one is not long behind
            }
            else
            {
                _nextArrival = now + Random.Range(2.5f, 6f) / Ctx.Offer.CrowdFactor;   // a quiet street or a rush, as the offer said
            }
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
                Gone(i, true);
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
                        Gone(i, true);   // never got to the front: gives up
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
                    }
                    break;
                case Guest.Ordering:
                    if (now >= _orderEnds)
                    {
                        // Kept waiting: they go, and say so.
                        _misses++;
                        _lost++;
                        Ctx.Animations.Play(person.Character, JobClip.Annoyed);
                        Ctx.Cast.Emoji(person, CharacterEmojiName.CommonRedAngry, 2.5f);
                        Ctx.Say(JobText.Pick(Left, ref _line), 5f);
                        if (Meta.Dev.Verbose)
                        {
                            Ctx.Logger?.Info("Jobs: vendor: a customer walked off unserved after " + Patience.ToString("F0") + " s (walk-off " + _misses + " of " + MissesAllowed + ").");
                        }
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
                        Gone(i, false);
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
                Gone(i, true);   // never got here (something in the way): no blame, the shift is one customer shorter
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
                _orderOver = now;
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
                Ctx.Director.StartCoroutine(Guard.Routine(WalkAfter(person, person.Lease, after)));
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

        /// <param name="unserved">They never had their drink and are not coming back: the shift is one customer shorter.</param>
        private void Gone(int i, bool unserved)
        {
            if (unserved)
            {
                _lost++;
            }
            if (_front == i)
            {
                _front = -1;
                _orderOver = Time.time;
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

        /// <summary>One of the panel's three drink buttons (the game ran it on its key or on a click): 0, 1 or 2.</summary>
        public override void Choose(int choice, float now)
        {
            if (Ctx == null || _front < 0 || _state[_front] != Guest.Ordering || !Ctx.Stand.Ready || now < _serveUntil || _skip != null)
            {
                return;
            }
            StreetActor person = _who[_front];
            Ctx.Stand.Busy(2.2f, now);   // the cloth is put down; the working pose comes back when the drink has been served
            float reach = Mathf.Max(0.9f, Ctx.Animations.Play(Ctx.Player.Character, JobClip.Serve));
            _serveUntil = now + Mathf.Min(reach, 2.2f);
            // The one difficult customer of the shift, if the offer drew one, sends back the first right drink they are handed.
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
                    Ctx.Logger?.Info("Jobs: vendor event: a difficult customer sends the drink back at customer " + (_served + 1) + " (the offer drew " + Ctx.Offer.RedoAt + "); it is made again, and the patience drops by " + WrongCost.ToString("F0") + " s.");
                    return;
                }
                Ctx.Say(JobText.Pick(Wrong, ref _line), 4f);
                if (Meta.Dev.Verbose)
                {
                    Ctx.Logger?.Info("Jobs: vendor: wrong drink by hand (" + ItemKeys[choice] + " for " + ItemKeys[_wants] + "): " + WrongCost.ToString("F0")
                                     + " s off the customer's patience, " + Mathf.Max(0f, _orderEnds - now).ToString("F1") + " s left.");
                }
                return;
            }
            float took = now - _orderedAt;
            int tip = 0;
            _served++;
            _hand++;
            UnitDone(_hand, UnitPay());   // the cart's big tipper, if the offer drew one
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
                Ctx.Logger?.Info("Jobs: vendor: customer served by hand (" + ItemKeys[_wants] + ") in " + took.ToString("F1") + " s, tip " + tip + " ("
                                 + (took <= QuickTip ? "quick" : took <= FairTip ? "fair" : "slow") + "); served " + _served + " of " + _customers + ", walk-offs " + _misses + ".");
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
            _orderOver = now;
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

        // ---------------------------------------------------------------- the game's panel over the cart

        // The order and its buttons while somebody is ordering, else what the vendor can do meanwhile. A text or a
        // button is made only when what it says has changed; the patience is one of sixteen texts made at the start.
        private void Panel(float now)
        {
            bool ordering = _front >= 0 && _state[_front] == Guest.Ordering;
            bool hawk = !_hawkBanned && now >= _hawkReadyAt;
            int left = LeftToServe;
            int seconds = ordering ? Mathf.Clamp(Mathf.CeilToInt(_orderEnds - now), 0, _seconds.Length - 1) : 0;
            int state = (ordering ? 1 + _wants : 0) | (seconds << 3) | ((hawk ? 1 : 0) << 8) | ((_hawkBanned ? 1 : 0) << 9) | (left << 10);
            if (state != _panelState)
            {
                _panelState = state;
                if (ordering)
                {
                    _panel.Lines(_orders[_wants], _patienceWords, _seconds[seconds], seconds <= 2 ? WorldPanel.Red() : seconds <= 4 ? WorldPanel.Yellow() : WorldPanel.White());
                    for (int i = 0; i < JobKeys.Choices; i++)
                    {
                        _panel.Button(i, ItemKeys[i], _onDrink[i], true, true);
                    }
                }
                else
                {
                    _panel.Lines(left <= 2 ? _lastWords : _waitWords, _toComeWords, left < _seconds.Length ? Count(left) : left.ToString(), WorldPanel.White());
                    _panel.Button(0, null, null, false, false);
                    _panel.Button(1, ButtonAway, _onAway, true, false);
                    _panel.Button(2, ButtonSkip, _onSkip, left > 0, true);
                }
                _panel.Button(3, _hawkBanned ? null : ButtonHawk, _onHawk, hawk, false);
            }
            // Over the cart; over the player's own head while the cart is behind the camera.
            Vector3 over = _cartAt + Vector3.up * PanelUp;
            Camera camera = GameManager.GetMainCamera();
            if (camera != null && Vector3.Dot(over - camera.transform.position, camera.transform.forward) < 0.5f)
            {
                over = Ctx.Player.transform.position + Vector3.up * 2.1f;
            }
            _panel.Position = over;
            int up = _panel.Show() ? 1 : 2;
            if (up != _panelUp)
            {
                _panelUp = up;
                if (Meta.Dev.Verbose)
                {
                    Ctx.Logger?.Info("Jobs check (cart panel): " + (up == 1 ? "the game's own panel is up over the cart: PASS"
                        : "FAIL (the game's panel is showing another place; it is tried again five times a second while the player stands at the cart)") + ".");
                }
            }
        }

        private readonly string[] _counts = new string[32];

        // A small number as a text, made once each.
        private string Count(int number)
        {
            if (number < 0 || number >= _counts.Length)
            {
                return number.ToString();
            }
            return _counts[number] ?? (_counts[number] = number.ToString());
        }

        private void HidePanel()
        {
            if (_panelUp != 0)
            {
                _panelUp = 0;
                _panelState = int.MinValue;
                _panel.Hide();
            }
        }

        private void StepAway()
        {
            if (Ctx == null || Time.time - _orderOver < Debounce)
            {
                return;
            }
            Ctx.Stand.StepAway();
            HidePanel();
        }

        // ---------------------------------------------------------------- the figures on the HUD

        // Five lines of the game's job panel, each written only when one of its figures changes.
        private void Card(float now)
        {
            ShiftCard card = Ctx.Card;
            if (card == null)
            {
                return;
            }
            bool clock = _skip != null && _skip.Working;
            bool ordering = !clock && _front >= 0 && _state[_front] == Guest.Ordering;
            bool hawked = _hawkWords != null && now < _hawkWordsUntil;
            int state = clock ? 1 : !_open ? 2 : !Ctx.Stand.Standing ? 3 : hawked ? 4 : ordering ? 5 + _wants : LeftToServe <= 2 ? 9 : 10;
            if (state != _cardState)
            {
                _cardState = state;
                string words = clock ? JobText.T("reallife_job_vendor_card_clock") : !_open ? JobText.T("reallife_job_vendor_obj_post")
                    : !Ctx.Stand.Standing ? JobText.T("reallife_job_vendor_obj_back") : hawked ? _hawkWords : ordering ? _orders[_wants] : LeftToServe <= 2 ? _lastWords : _waitWords;
                card.Line(1, words, state == 3 ? WorldPanel.Yellow() : WorldPanel.White());
            }
            int served = _served + (clock ? _skipDone : 0);
            int figures = served | (_customers << 8) | (_misses << 16);
            if (figures != _cardFigures)
            {
                _cardFigures = figures;
                _text.Length = 0;
                _text.Append(JobText.T("reallife_job_vendor_card_served", "served", served.ToString(), "count", _customers.ToString())).Append(", ")
                    .Append(JobText.T("reallife_job_vendor_card_walkoffs", "misses", _misses.ToString(), "most", MissesAllowed.ToString()));
                card.Line(2, _text.ToString(), _misses >= MissesAllowed - 1 ? WorldPanel.Yellow() : WorldPanel.White());
            }
            int takings = Mathf.RoundToInt(_hand * PayEach) + _clockPay;
            int tips = Mathf.RoundToInt(_tips * Ctx.TipFactor);
            int money = takings | (tips << 16);
            if (money != _cardMoney)
            {
                _cardMoney = money;
                card.Line(3, JobText.T("reallife_job_vendor_card_money", "takings", JobText.Money(takings), "tips", JobText.Money(tips)), WorldPanel.White());
            }
            // The time on shift in steps of five game minutes (five seconds of play), and about how long is left at the clock's pace.
            int on = Mathf.Max(0, Mathf.FloorToInt((TimeHelper.NowInMinutes() - _startMinutes) / 5f)) * 5;
            int left = Mathf.Max(0, LeftToServe - (clock ? _skipDone : 0)) * _minutesEach;
            int time = on | (left << 12);
            if (time != _cardTime)
            {
                _cardTime = time;
                card.Line(4, JobText.T("reallife_job_card_time", "on", Duration(on), "left", Duration(left)), WorldPanel.White());
            }
        }

        private static string Duration(int minutes)
        {
            return JobText.T("reallife_job_duration", "hours", (minutes / 60).ToString(), "minutes", (minutes % 60).ToString());
        }

        // ---------------------------------------------------------------- the rest of the shift by the clock

        private void OfferSkip()
        {
            if (Ctx == null || _skip != null || !Ctx.Stand.Ready || LeftToServe <= 0 || Time.time - _orderOver < Debounce)
            {
                return;
            }
            _skip = ShiftSkip.Offer(Ctx.Director, this);
            if (_skip != null)
            {
                HidePanel();   // the game's activity panel has the keys while it is up
            }
        }

        string ISkippableShift.SkipTitle => Ctx != null && Ctx.Shift != null
            ? JobText.T("reallife_career_shift_title", "title", JobText.T(Ctx.Shift.Rank.TitleKey), "employer", JobText.T(Ctx.Shift.Employer.NameKey)) : _title;

        int ISkippableShift.SkipMinutes => LeftToServe * _minutesEach;

        LabelInfo[] ISkippableShift.SkipLabels()
        {
            int left = LeftToServe;
            int minutes = left * _minutesEach;
            Color32 white = WorldPanel.White();
            var time = new LabelInfo("reallife_job_skip_info_time", new { count = left, hours = minutes / 60, minutes = minutes % 60 }, white);
            var pay = new LabelInfo("reallife_job_skip_info_pay", new
            {
                share = Mathf.RoundToInt(ShiftRates.Share * 100f), amount = JobText.Money(Mathf.RoundToInt(left * PayEach * ShiftRates.Share))
            }, white);
            return Ctx.Shift != null
                ? new[] { time, pay, new LabelInfo("reallife_job_skip_info_wage", null, white) }
                : new[] { time, pay };
        }

        void ISkippableShift.SkipStarted()
        {
            // The street is cleared: whoever was on the way or at the counter goes back among those still to come.
            _cupHeld.Take();
            _beat = 0;
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
            _front = -1;
            _came = _served + _lost;
            ReleaseLookers();
            _skipLeft = LeftToServe;
            _skipDone = 0;
            if (Meta.Dev.Verbose)
            {
                Ctx.Logger?.Info("Jobs: vendor: the rest of the shift is left to the clock: " + _skipLeft + " customers, " + (_skipLeft * _minutesEach) + " game minutes, at "
                                 + Mathf.RoundToInt(ShiftRates.Share * 100f) + "% of " + PayEach.ToString("F2") + " a customer; the game's activity panel shows its progress.");
            }
        }

        void ISkippableShift.SkipWorked(int minutes)
        {
            _skipDone = Mathf.Min(_skipLeft, minutes / _minutesEach);
        }

        void ISkippableShift.SkipEnded(int minutes, bool whole)
        {
            if (Ctx == null || _ending)
            {
                return;
            }
            int count = whole ? _skipLeft : Mathf.Min(_skipLeft, minutes / _minutesEach);
            _skipDone = 0;
            _skipLeft = 0;
            _nextArrival = Time.time + 2f;
            _panelState = int.MinValue;
            float energyBefore = SaveGameManager.Current != null ? SaveGameManager.Current.Energy : 0f;
            int pay = 0;
            if (count > 0)
            {
                pay = Mathf.RoundToInt(count * PayEach * ShiftRates.Share);
                _served += count;
                _came = _served + _lost;
                _clockPay += pay;
                Ctx.Pay(this, new PaySlip { Units = count, Base = pay }, "reallife_job_vendor_slip");
            }
            float energyAfter = SaveGameManager.Current != null ? SaveGameManager.Current.Energy : 0f;
            Ctx.Logger?.Info("Jobs: vendor: shift skipped by the clock: " + (minutes / 60f).ToString("F1") + " game hours (" + minutes + " min), " + count + " customers at "
                             + Mathf.RoundToInt(ShiftRates.Share * 100f) + "%: pay " + pay + ", no tips, no bonus; energy " + energyBefore.ToString("F1") + " -> " + energyAfter.ToString("F1")
                             + "; " + (whole ? "to the end of the shift" : "stopped early: " + LeftToServe + " customers are left to serve by hand") + ".");
        }

        // ---------------------------------------------------------------- calling out

        /// <summary>
        /// The vendor calls out to the street. Each of the game's pedestrians within <see cref="HawkWithin"/> metres
        /// of the cart (six at most) reacts by chance: tempted, annoyed, or not at all.
        ///
        /// Tempted: <see cref="HawkTempt"/>, times the hour (0.5 at night, 0.8 early, 1.2 at lunch, 1.3 from four to
        /// nine), times how busy the pavement is (0.8 to 1.2), times the player's customer service skill (1 to 1.5),
        /// times the queue (0.6 with one customer at the cart, 0.3 with two), less <see cref="HawkTemptStep"/> of
        /// itself for each recent call. Annoyed: <see cref="HawkAnnoy"/> plus <see cref="HawkAnnoyStep"/> for each
        /// recent call. A call is "recent" for <see cref="HawkHeatFade"/> seconds.
        ///
        /// A tempted passer-by shows the game's green face, up to two of them stop and look at the cart for a few
        /// seconds, and for each one a customer more is added to the shift (<see cref="HawkExtraMost"/> at most, and
        /// never past the day's amount of a gig) who comes along soon. An annoyed one shows the red face and a
        /// gesture, and is not asked again this shift. With three or more recent calls and police presence on, an
        /// officer may warn the vendor; the second warning ends calling out for the shift.
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
                _hailUntil = now + 1.6f;   // an arm up and a wave over the working pose: it comes down on the tick
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
                        .Append(" (roll ").Append(roll.ToString("F2")).Append(')');
                }
            }
            float heatBefore = _heat;
            _heat += 1f;
            int near = looked + cross + passed;
            _hawkWords = near == 0 ? JobText.T("reallife_job_vendor_hawk_none")
                : JobText.T("reallife_job_vendor_hawk_result", "looked", looked.ToString(), "annoyed", cross.ToString()).Replace("{ignored}", passed.ToString());
            _hawkWordsUntil = now + 7f;
            _cardState = int.MinValue;

            // Too much of it, with officers about: a warning, and the second ends it for the shift.
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
                Ctx.Logger?.Info("Jobs: vendor: called out. Chances: tempted " + tempt.ToString("F2") + " (base " + HawkTempt.ToString("F2") + " x hour " + hourFactor.ToString("F1")
                                 + " at " + hour + "h x busy " + busyFactor.ToString("F2") + " with " + about + " about x skill " + skillFactor.ToString("F2") + " x queue "
                                 + queueFactor.ToString("F1") + " with " + waiting + " at the cart, recent calls " + heatBefore.ToString("F1") + "), annoyed " + annoy.ToString("F2")
                                 + ". Within " + HawkWithin.ToString("F0") + " m: " + near + (near > 0 ? " (" + _text + ")" : string.Empty) + ". Customers gained: " + gained
                                 + " (the shift is now " + _customers + ")" + (warned ? "; an officer's warning " + _hawkWarnings + " of 2" + (_hawkBanned ? ": no more calling out this shift" : string.Empty) : string.Empty)
                                 + "; next call in " + HawkCooldown.ToString("F0") + " s.");
            }
        }

        // One of the two who may stop and look at the cart: taken out of the game's hands where they stand, turned to
        // the cart by the game's own look target, and handed back walking a few seconds later (as the busker's listener is).
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
                agent.enabled = false;   // the game turns a character to its look target only while it is not being steered
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

        // On the tick: whoever has looked long enough walks on.
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
                body.LookTarget = Vector3.zero;   // a pedestrian of the game's keeps whatever look target it was left with
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

        // ---------------------------------------------------------------- the end

        private void Conclude(bool badly)
        {
            Settle();
            Finish(badly && _served * 2 < Mathf.Max(1, _came) ? JobEnd.Failed : JobEnd.Done);
        }

        // The customers served by hand, paid once at the end (the clock's were paid as each run of it ended).
        private void Settle()
        {
            if (!Owed(out PaySlip slip))
            {
                return;
            }
            _paid = true;
            Ctx.Pay(this, slip, "reallife_job_vendor_slip");
        }

        protected override bool Owed(out PaySlip slip)
        {
            slip = default;
            if (_paid || _hand <= 0 || Ctx == null)
            {
                return false;
            }
            slip = new PaySlip
            {
                Units = _hand,
                // A wage by the customer, rounded on the total; with a cart of their own, the takings less what the drinks cost.
                Base = Mathf.RoundToInt(_hand * PayEach),
                Tips = Mathf.RoundToInt(_tips * Ctx.TipFactor),
                Bonus = _misses == 0 && _served >= _customers ? Mathf.RoundToInt(_hand * Def.UnitPay * Ctx.Rate * 0.1f) : 0   // nobody kept waiting
            };
            return true;
        }

        // The cart runs the clock itself: the customers a run that is still going has served, at the clock's share, as that run pays them when it ends.
        protected override bool ClockOwed(out PaySlip slip)
        {
            slip = default;
            if (Ctx == null || _ending || _skip == null || _skipDone <= 0)
            {
                return false;
            }
            slip = new PaySlip { Units = _skipDone, Base = Mathf.RoundToInt(_skipDone * PayEach * ShiftRates.Share) };
            return true;
        }

        public override void End(JobEnd why)
        {
            if (Ctx == null)
            {
                return;
            }
            _ending = true;
            if (_skip != null)
            {
                _skip.Close();   // the game's activity panel goes; nothing more is settled through it
                _skip = null;
            }
            _panelUp = 1;
            HidePanel();
            if (_hailUntil >= 0f && Ctx.Player != null)
            {
                _hailUntil = -1f;
                Ctx.Animations.Stop(Ctx.Player.Character);
            }
            ReleaseLookers();
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
                _solid.Clear();   // nothing is left cutting the mesh when the same cart object is next put out
                Ctx.Props.Return(_cart);
                _cart = null;
            }
            Settle();   // also after a fault: the customers served before it are paid (last of all, so nothing above depends on it)
        }
    }
}
