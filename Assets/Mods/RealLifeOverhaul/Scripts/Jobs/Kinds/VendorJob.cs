using System;
using System.Globalization;
using System.Text;
using Helpers;
using RealLifeOverhaul.GameUi;
using UI.Elements;
using UnityEngine;
using Random = UnityEngine.Random;

namespace RealLifeOverhaul
{
    /// <summary>
    /// A shift at a pavement drinks cart, as a gig or a rota shift: the solid cart (<see cref="SolidFootprint"/>) is put down
    /// on the player's pavement and taken by walking behind it (<see cref="JobStand"/>). Customers queue and order on the panel
    /// over the cart; the right drink quickly earns a tip, the wrong one costs patience, and three walk-offs end the shift.
    /// Calling out (<see cref="Hawk"/>) brings more; the rest can be left to the game's clock (<see cref="ShiftSkip"/>).
    /// </summary>
    public sealed partial class VendorJob : Job, ISkippableShift
    {
        private enum Guest { None, Coming, Queued, Stepping, Ordering, Served, Leaving }

        // The clips fix the spots (CityAssets/README.md, "A counter"): set down 0.78 m ahead, taken 0.62 m ahead, on a 0.80 m counter.
        private const float VendorBack = 0.68f;      // metres from the cart's middle to where the vendor stands
        private const float CustomerOut = 0.72f;     // ...and to the customer at the counter
        private const float CupOut = 0.10f;          // ...and to the cup on the counter between them
        private const float QueueOut = 2.1f;         // ...and to whoever waits behind them
        private const float CupRest = 0.10f;         // seconds the cup stands on the counter before the customer's hand closes on it
        private const string ReachClip = "counter_reach";
        private const float CounterHeight = 0.975f;  // metres
        private const float CartHalfLength = 0.75f;  // the cart's body, along the pavement
        private const float CartHalfDepth = 0.40f;   // ...and its counter, across it
        private const float PostReach = 0.9f;        // metres: the zone behind the cart that takes the player
        private const float PanelUp = 2.05f;         // metres up the cart the game's panel stands over
        public const float Patience = 11f;           // seconds a customer waits for their drink
        public const float WrongCost = 3f;           // ...less this for each wrong one
        public const float QuickTip = 3.5f;          // seconds: served within this, the full tip
        private const float FairTip = 6.5f;          // seconds: within this, a small one
        private const int MissesAllowed = 3;
        public const float AbandonAfter = 25f;       // seconds away from the cart before the shift is given up
        private const float Debounce = 0.7f;         // seconds after an order ends in which the other buttons do nothing (a key meant for a drink)
        private const string CartProp = "vendor_cart";
        private const string CupProp = "coffee_cup";
        private const string IdleClip = "counter_wipe";

        private static readonly string[] CupGrips = { "counter_reach", "cup_hold" };
        private static readonly string[] Left = { "reallife_job_vendor_left_1", "reallife_job_vendor_left_2" };
        private static readonly string[] ItemKeys = { "reallife_job_vendor_item_1", "reallife_job_vendor_item_2", "reallife_job_vendor_item_3" };
        private const string ButtonHawk = "reallife_job_vendor_button_hawk";
        private const string ButtonSkip = "reallife_job_vendor_button_skip";
        private const string ButtonAway = "reallife_job_vendor_button_away";

        private readonly JobDef _def = JobCatalogue.Find(JobCatalogue.Vendor);
        private readonly int _minutesEach = ShiftRates.Minutes(JobCatalogue.Find(JobCatalogue.Vendor));   // game minutes the clock takes a customer (a shift of twelve is four game hours)
        private readonly StreetActor[] _who = new StreetActor[JobCast.Size];
        private readonly Guest[] _state = new Guest[JobCast.Size];
        private readonly float[] _until = new float[JobCast.Size];
        private readonly float[] _walkAt = new float[JobCast.Size];
        private readonly int[] _lease = new int[JobCast.Size];      // the cast's lease when they set off: another lease is not the cart's
        private readonly float[] _best = new float[JobCast.Size];   // metres: the nearest a walking customer has been to their place
        private readonly float[] _bestAt = new float[JobCast.Size]; // ...and when (CartQueueRules.Walking)
        private readonly HeldProp _cupHeld = new HeldProp();
        private readonly WorldPanel _panel = new WorldPanel { Name = "drinks cart" };
        private readonly SolidFootprint _solid = new SolidFootprint();
        private readonly Action[] _onDrink = new Action[JobKeys.Choices];
        private readonly string[] _items = new string[JobKeys.Choices];
        private readonly string[] _orders = new string[JobKeys.Choices];
        private readonly string[] _seconds = new string[(int)Patience + 2];
        private readonly StringBuilder _text = new StringBuilder(160);
        private Action _onHawk;
        private Action _onSkip;
        private Action _onAway;

        private Transform _cart;
        private Transform _cup;
        private Vector3 _cartAt;
        private Vector3 _post;
        private Vector3 _counter;
        private Vector3 _queue;
        private Vector3 _outwards;
        private int _customers;       // how many the shift is (calling out can add to it)
        private int _came;            // how many have set off for the cart
        private int _served;          // by hand and by the clock
        private int _hand;            // ...of which by hand
        private int _lost;            // came and went unserved, with or without blame
        private int _misses;          // ...of which walked off kept waiting
        private int _cantReach;       // customers in a row who could not get to the cart
        private int _routesRefused;   // ring points nobody was sent from: the way cut, or a detour
        private int _tips;
        private int _clockPay;        // paid to the clock's customers, for the takings line
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
        private bool _sentBack;       // the difficult customer has had their say
        private int _line = -1;
        private string _postLabel;
        private string _title;
        private string _waitWords;
        private string _lastWords;
        private string _toComeWords;
        private string _patienceWords;

        public override JobDef Def => _def;

        public override bool PassingTime => _skip != null && _skip.Working;

        public override bool OwnsInteractKey => Ctx.Stand.Standing;

        public override bool QuitIsFailure => _came > 0 && _served * 2 < _came;

        private int LeftToServe => Mathf.Max(0, _customers - _served - _lost);

        private float PayEach => (Ctx.Perk(CareerPerk.OwnCart) ? CareerCatalogue.CartTakings - CareerCatalogue.CartStock : Def.UnitPay) * Ctx.Rate;

        protected override bool OnBegin(float now)
        {
            if (Ctx.Driving || PlayerHelper.IsUsingVehicle)
            {
                Refusal = "reallife_job_on_foot";
                return false;
            }
            _customers = ShiftSize();
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
            if (!PutPitch(now))
            {
                Refusal = "reallife_job_vendor_no_room";
                return false;
            }

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
                Ctx.Logger?.Info("Jobs: vendor cart put down " + JobSpots.Flat(_cartAt, Ctx.PlayerPosition).ToString("F1", CultureInfo.InvariantCulture) + " m from the player; the vendor's place is "
                                 + JobSpots.Flat(_post, Ctx.PlayerPosition).ToString("F1", CultureInfo.InvariantCulture) + " m away (the zone that takes the player is "
                                 + PostReach.ToString("F1", CultureInfo.InvariantCulture)
                                 + " m round it); " + _customers + " customers; shown in the game's own interface: job panel "
                                 + (card != null ? "copied" : "NOT copied (the panel over the cart is all there is)")
                                 + ", waiting for the solid check before the place is offered.");
            }
            return true;
        }

        public override void Tick(float now)
        {
            if (!PitchReady(now))
            {
                return;
            }
            if (_skip != null)
            {
                if (_skip.Open && _skip.Shown)
                {
                    Card(now);   // the clock is working, or its offer is up with the game paused
                    return;
                }
                _skip.Close();   // over, or dropped by the game's panel: it settles nothing more
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
                    // Away from the cart: customers wait and lose patience; too long away gives the shift up.
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

        private void Conclude(bool badly)
        {
            Settle();
            Finish(badly && _served * 2 < Mathf.Max(1, _came) ? JobEnd.Failed : JobEnd.Done);
        }

        // The customers served by hand are paid once at the end; the clock's were paid as each run ended.
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
            if (_paid || _hand <= 0)
            {
                return false;
            }
            slip = new PaySlip
            {
                Units = _hand,
                // A wage per customer, rounded on the total; with their own cart, the takings less the drinks' cost.
                Base = Mathf.RoundToInt(_hand * PayEach),
                Tips = Mathf.RoundToInt(_tips * Ctx.TipFactor),
                Bonus = _misses == 0 && _served >= _customers ? Mathf.RoundToInt(_hand * Def.UnitPay * Ctx.Rate * 0.1f) : 0   // nobody kept waiting
            };
            return true;
        }

        // The cart runs the clock itself: what a run still going has served, at the clock's share.
        protected override bool ClockOwed(out PaySlip slip)
        {
            slip = default;
            if (_ending || _skip == null || _skipDone <= 0)
            {
                return false;
            }
            slip = new PaySlip { Units = _skipDone, Base = Mathf.RoundToInt(_skipDone * PayEach * ShiftRates.Share) };
            return true;
        }

        public override void End(JobEnd why)
        {
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
            ClearCounter();
            ReleaseGuests();
            if (_cart != null)
            {
                _solid.Clear();   // nothing is left cutting the mesh when the cart object is next put out
                Ctx.Props.Return(_cart);
                _cart = null;
            }
            Settle();   // also after a fault (last, so nothing above depends on it)
        }
    }
}
