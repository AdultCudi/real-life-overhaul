using System.Globalization;
using Extensions;
using Helpers;
using UI.Notification;
using UnityEngine;
using UnityEngine.AI;
using Random = UnityEngine.Random;

namespace RealLifeOverhaul
{
    /// <summary>Which cart a stall is.</summary>
    public enum StallKind { Coffee, HotDogs }

    /// <summary>
    /// A coffee or hot-dog cart on the pavement with a real queue walking up, served at the counter and walking off, every place
    /// tested for room. The player can buy a coffee at the counter through the game's own calls; nothing is stored, and with
    /// player interactions off it is scenery.
    /// </summary>
    public sealed class StallActivity : CityActivity
    {
        public static int Price => Prices.At(4);

        private const float Energy = 5f;            // of the game's hundred
        // Where the two stand is fixed by the hand-over clips: the cup goes down 0.78 m ahead of the vendor and is taken 0.62 m
        // ahead of the customer, 0.10 m to the customer's side of the middle. Against both carts as modelled (counter 0.80 m
        // deep) both stand clear of the counter's edge. PropLibrary.SizeOf's 1.07 m for coffee_cart is its awning, overhead.
        private const float VendorBack = 0.68f;     // metres from the cart's middle to where the vendor stands
        private const float CustomerOut = 0.72f;    // metres from the cart's middle to the customer at the counter
        private const float QueueStep = 0.85f;      // metres between two people in the queue
        private const float CartHalfLength = 0.75f; // metres: the cart's body along the pavement
        private const float CartHalfDepth = 0.40f;  // metres: its counter across it
        private const int QueueMost = 3;
        private const int PeopleMost = 4;           // vendor, queue and whoever is walking off, at once
        private const float BuyWithin = 2.3f;   // metres from the cart within which the player can buy
        private const float BuyGap = 8f;            // seconds between two coffees

        // The moments of a sale, in seconds from its start where the clips have no mark of their own.
        private const int CupDown = 1, Reach = 2, CupTaken = 3, WalkOff = 4, CupUp = 5;
        private const float ReachAt = 2.0f;         // seconds: the customer has paid and reaches for the cup
        private const float ReachLength = 2.4f;   // seconds

        private readonly StallKind _kind;
        private readonly SolidFootprint _solid = new SolidFootprint();
        private readonly Vector3[] _places = new Vector3[QueueMost];
        private readonly CityPerson[] _queue = new CityPerson[QueueMost];
        private readonly bool[] _settled = new bool[QueueMost];
        private readonly float[] _due = new float[QueueMost];   // when whoever is walking to a place should have got there
        private const float WalkPatience = 55f;   // seconds
        private Vector3 _cart;
        private Vector3 _vendor;
        private Vector3 _shade;
        private Vector3 _counterSide;   // out of the cart towards the customers
        private float _heading;
        private int _placeCount;
        private bool _hasShade;
        private float _nextWipe;
        private float _nextArrival;
        private float _vendorBusyUntil;
        private float _saleEnds;
        private float _nextSale;
        private bool _prompted;
        private Transform _cup;         // the cup on the counter between the vendor's hand and the customer's
        private bool _buyerHasCup;

        public StallActivity(StallKind kind)
        {
            _kind = kind;
        }

        /// <summary>True while the player stands at the counter on foot and a coffee can be bought: the director then listens for the key.</summary>
        public bool PlayerAtCounter { get; private set; }

        public override bool Find(CitySite site)
        {
            if (!CitySpots.Stand(site.Anchor, 1.1f, out _cart) || !CitySpots.Along(_cart, out Vector3 along))
            {
                return false;
            }
            Vector3 across = Vector3.Cross(Vector3.up, along);
            for (int side = 0; side < 2; side++)
            {
                Vector3 outwards = side == 0 ? across : -across;
                Vector3 right = Vector3.Cross(Vector3.up, outwards);   // the cart's right once its counter faces outwards
                // The four corners of the cart's footprint, the vendor behind and the customer in front.
                if (!CitySpots.OnPavement(_cart + along * 0.85f + outwards * 0.5f, out _) || !CitySpots.OnPavement(_cart - along * 0.85f + outwards * 0.5f, out _)
                    || !CitySpots.OnPavement(_cart + along * 0.85f - outwards * 0.5f, out _) || !CitySpots.OnPavement(_cart - along * 0.85f - outwards * 0.5f, out _)
                    || !CitySpots.Stand(_cart - outwards * VendorBack + right * 0.08f, 0.28f, out _vendor)
                    || !CitySpots.Stand(_cart + outwards * CustomerOut - right * 0.10f, 0.28f, out _places[0]))
                {
                    continue;
                }
                // The queue runs straight out from the counter if there is pavement for it, else along the cart.
                _placeCount = 1;
                for (int i = 1; i < QueueMost; i++)
                {
                    if (!CitySpots.Stand(_places[0] + outwards * (QueueStep * i), 0.3f, out _places[_placeCount])
                        && !CitySpots.Stand(_places[0] + outwards * 0.35f + right * (QueueStep * i), 0.3f, out _places[_placeCount]))
                    {
                        break;
                    }
                    _placeCount++;
                }
                _counterSide = outwards;
                _hasShade = _kind == StallKind.HotDogs && CitySpots.OnPavement(_cart - right * 1.15f - outwards * 0.55f, out _shade);
                // The cart's +Z is its counter side.
                _heading = Quaternion.LookRotation(outwards).eulerAngles.y;
                Centre = _cart;
                return true;
            }
            return false;
        }

        public override bool Begin(CitySite site)
        {
            Start(site);
            for (int i = 0; i < QueueMost; i++)
            {
                _queue[i] = null;
                _settled[i] = false;
            }
            _cup = null;
            if (!Put(_kind == StallKind.Coffee ? "coffee_cart" : "vendor_cart", _cart, _heading) || Add(CityLook.Vendor, _vendor, _cart) == null)
            {
                End();
                return false;
            }
            // Solid to everyone on foot, as the bus stops are (Core/SolidFootprint): walkers steer round it, and it cuts the mesh
            // only while the player is on foot nearby (Tick). Its depth leaves the vendor's and customer's places on the mesh.
            _solid.Fit(Dressing[0], CartHalfLength, CartHalfDepth, Mathf.Min(VendorBack, CustomerOut));
            if (_hasShade)
            {
                Put("vendor_umbrella", _shade, _heading);
            }
            if (!Cast[0].Work("counter_wipe", Props, 0f) || !ClipLibrary.Has("serve_counter") || !ClipLibrary.Has("counter_reach") || !ClipLibrary.Has("pay_tap") || !ClipLibrary.Has("queue_wait"))
            {
                site.Refusal = CityRefusal.NoProp;
                End();
                return false;
            }
            if (!Join(site))
            {
                End();
                return false;
            }
            _vendorBusyUntil = 0f;
            _saleEnds = 0f;
            _nextWipe = In(6f, 10f);
            _nextArrival = In(5f, 11f);
            _nextSale = 0f;
            _prompted = false;
            PlayerAtCounter = false;
            return true;
        }

        public override bool Tick(float now)
        {
            PlayerAtCounter = false;
            TickDepartures();
            if (Cast.Count == 0 || Cast[0].Lost)
            {
                Over = true;   // no vendor, no stall
            }
            if (Over)
            {
                return CitySpots.InView(Centre);
            }
            CityPerson vendor = Cast[0];

            // Solid only while the player is on foot near it, nearer to become so than to stay so.
            PlayerController walker = PlayerHelper.PlayerController;
            float solidWithin = _solid.IsSolid ? SolidFootprint.Keep : SolidFootprint.Within;
            _solid.Solid(walker != null && !PlayerHelper.IsUsingVehicle && CitySpots.Flat(walker.transform.position - _cart) < solidWithin * solidWithin, now);
            if (_solid.VerifyDue && _solid.Verify(now, _vendor, _places[0], out bool cut, out bool kept, out bool way) && Meta.Dev.Verbose)
            {
                CityLifeDirector.Logger?.Info("City life check (cart solid): the " + (_kind == StallKind.Coffee ? "coffee" : "hot-dog") + " cart: walker radius "
                                              + _solid.Radius.ToString("F2", CultureInfo.InvariantCulture)
                                              + " m; cut out of the mesh: the cart's middle " + (cut ? "yes" : "no") + "; still walkable: the vendor's place and the first customer's place "
                                              + (kept ? "yes" : "no") + ", a way on foot from one to the other " + (way ? "yes" : "no") + ": "
                                              + (cut && kept && way ? "PASS" : !kept || !way ? "FAIL (the cut is taken back: the cart is steered round, not solid)"
                                                 : "FAIL (the cart can be walked through)") + ".");
            }

            // The queue: forget whoever was knocked down, close up, and settle whoever has arrived.
            for (int i = 0; i < _placeCount; i++)
            {
                if (_queue[i] != null && _queue[i].Lost)
                {
                    _queue[i] = null;
                }
            }
            bool selling = now < _saleEnds;
            if (!selling && _cup != null)
            {
                // A cup nobody took, its sale come apart: off the counter, or nothing could be sold again.
                Dressing.Remove(_cup);
                Props.Return(_cup);
                _cup = null;
            }
            for (int i = selling ? 2 : 1; i < _placeCount; i++)
            {
                if (_queue[i] != null && _queue[i - 1] == null)
                {
                    _queue[i - 1] = _queue[i];
                    _queue[i] = null;
                    _settled[i - 1] = false;
                    _due[i - 1] = now + WalkPatience;
                    _queue[i - 1].WalkTo(_places[i - 1]);
                }
            }
            for (int i = 0; i < _placeCount; i++)
            {
                CityPerson waiting = _queue[i];
                if (waiting != null && !_settled[i] && waiting.Near(_places[i], 0.6f) && (waiting.Arrived || (waiting.Near(_places[i], 0.35f) && waiting.Speed < 0.1f)))
                {
                    _settled[i] = true;
                    waiting.Stand();
                    waiting.Face(i == 0 ? _cart : _places[i - 1]);
                    waiting.Work("queue_wait", Props, 0.4f, ClipEnd.Return, true, Random.Range(0f, 8f));
                }
                else if (waiting != null && !_settled[i] && now >= _due[i])
                {
                    // Never got to their place (the way was blocked): they walk off and the place is free again.
                    _queue[i] = null;
                    Depart(waiting);
                }
                else if (waiting != null && !_settled[i] && waiting.Arrived)
                {
                    waiting.WalkTo(_places[i]);   // stopped short: on again
                }
            }

            if (!selling && now >= _vendorBusyUntil && _queue[0] != null && _settled[0] && _cup == null)
            {
                CityPerson buyer = _queue[0];
                _buyerHasCup = false;
                if (vendor.Work("serve_counter", Props, 0.3f, ClipEnd.Return, false)
                    && ClipLibrary.TryGet("serve_counter", out CityClip serve) && ClipLibrary.TryGet("counter_reach", out CityClip reach))
                {
                    CityLifeDirector.After(serve.Mark("grip", 0.28f), this, CupUp, buyer);
                    buyer.Idle(0.3f);
                    buyer.WorkOver("pay_tap", Props, 0.3f, ClipEnd.Return, BodyMask.Upper);
                    CityLifeDirector.After(serve.Mark("release", 1.1f), this, CupDown, buyer);
                    CityLifeDirector.After(ReachAt, this, Reach, buyer);
                    CityLifeDirector.After(ReachAt + reach.Mark("grip", 0.9f), this, CupTaken, buyer);
                    CityLifeDirector.After(ReachAt + ReachLength, this, WalkOff, buyer);
                    _saleEnds = now + ReachAt + ReachLength + 0.2f;
                    _vendorBusyUntil = now + serve.Length + 0.3f;
                    _nextWipe = _vendorBusyUntil + Random.Range(1f, 4f);
                }
            }
            else if (now >= _vendorBusyUntil && now >= _nextWipe)
            {
                // Between customers: wipes the counter a while, stands a while.
                bool wiping = vendor.Playing == "counter_wipe";
                if (wiping)
                {
                    vendor.Idle(0.5f);
                }
                else
                {
                    vendor.Work("counter_wipe", Props, 0.5f);
                }
                _nextWipe = wiping ? In(4f, 8f) : In(6f, 12f);
            }

            // Somebody else comes along if the end of the queue is free and somebody in the pool is too.
            if (now >= _nextArrival)
            {
                _nextArrival = In(9f, 22f);
                if (Cast.Count < PeopleMost)
                {
                    Join(null);
                }
            }

            PlayerController player = PlayerHelper.PlayerController;
            if (_kind == StallKind.Coffee && Settings.CityInteractions && player != null && !PlayerHelper.IsUsingVehicle && now >= _nextSale && !selling)
            {
                Vector3 offset = player.transform.position - _cart;
                offset.y = 0f;
                if (offset.sqrMagnitude < BuyWithin * BuyWithin && Vector3.Dot(offset, _counterSide) > 0.3f)
                {
                    PlayerAtCounter = true;
                    if (!_prompted)
                    {
                        _prompted = true;
                        // Once per stall; the text names the interaction key as it is bound now.
                        Notifications.Show(NotificationType.Info, "reallife_city_coffee_prompt",
                            new System.Collections.Generic.Dictionary<string, string>(2) { { "key", Keys.Label(KeyAction.Interact) }, { "price", ((float)Price).ToCurrencyFormat() } }, 6f);
                    }
                }
            }
            return true;
        }

        public override void Moment(int what, CityPerson who, float now)
        {
            if (Cast.Count == 0)
            {
                return;
            }
            CityPerson vendor = Cast[0];
            switch (what)
            {
                case CupUp:
                    // The vendor's hand has reached the worktop: a cup is in it from here.
                    if (!vendor.Lost && vendor.Playing == "serve_counter")
                    {
                        Transform fresh = Props.Take("coffee_cup");
                        if (fresh != null && !vendor.Take(fresh, "serve_counter", Props))
                        {
                            Props.Return(fresh);
                        }
                    }
                    break;
                case CupDown:
                    // The vendor lets go: the cup stands on the counter where the hand put it.
                    if (!vendor.Lost)
                    {
                        _cup = vendor.Release(true);
                        if (_cup != null)
                        {
                            _cup.SetPositionAndRotation(_cup.position, Quaternion.Euler(0f, _cup.eulerAngles.y, 0f));
                            Dressing.Add(_cup);
                        }
                    }
                    break;
                case Reach:
                    if (who != null && !who.Lost && who == _queue[0])
                    {
                        who.StopOver(0.2f);
                        who.Work("counter_reach", Props, 0.25f, ClipEnd.Hold, false);
                    }
                    break;
                case CupTaken:
                    if (_cup != null && who != null && !who.Lost && who == _queue[0] && who.Take(_cup, "counter_reach", Props))
                    {
                        Dressing.Remove(_cup);
                        _cup = null;
                        _buyerHasCup = true;
                    }
                    break;
                case WalkOff:
                    if (who != null && who == _queue[0])
                    {
                        _queue[0] = null;
                        _settled[0] = false;
                        if (!who.Lost)
                        {
                            // The cup stays in that hand alone, and the game walks them away.
                            if (_buyerHasCup)
                            {
                                who.WorkOver("cup_hold", Props, 0.3f, ClipEnd.Return, BodyMask.RightArm);
                            }
                            Depart(who);
                        }
                    }
                    if (_cup != null)
                    {
                        // Nobody took it (the customer was knocked down, or the hand could not): off the counter.
                        Dressing.Remove(_cup);
                        Props.Return(_cup);
                        _cup = null;
                    }
                    break;
            }
        }

        /// <summary>The player pressed the key at the counter.</summary>
        public void Buy(float now)
        {
            if (!PlayerAtCounter || Cast.Count == 0 || Cast[0].Lost)
            {
                return;
            }
            PlayerAtCounter = false;
            _nextSale = now + BuyGap;
            var save = SaveGameManager.Current;
            if (save == null || save.Money < Price)
            {
                Notifications.Show(NotificationType.Warning, "reallife_city_coffee_broke", null, 5f);
                return;
            }
            try
            {
                GameManager.ChangeMoneySafe(-Price, new TransactionInfo("reallife_transaction_coffee"), null, null, force: true);
                EnergyHelper.GenerateEnergy(Energy);
            }
            catch (System.Exception e)
            {
                // The game's money or energy call failed: no coffee, and the street carries on.
                CityLifeDirector.Logger?.Error(e);
                return;
            }
            Notifications.Show(NotificationType.Success, "reallife_city_coffee_bought", null, 5f);
            SocialHooks.BoughtCoffee();   // a regular at the cart gets noticed (Scripts/Social)
            if (now >= _vendorBusyUntil && Cast[0].Work("serve", Props, 0.25f))
            {
                _vendorBusyUntil = now + 2.6f;
                _nextWipe = now + 2.6f + Random.Range(3f, 6f);
            }
        }

        public override void End()
        {
            PlayerAtCounter = false;
            _solid.Clear();   // nothing left cutting the mesh when the cart object is next put out
            _cup = null;   // on the counter it was dressing and goes back with it
            for (int i = 0; i < QueueMost; i++)
            {
                _queue[i] = null;
            }
            base.End();
        }

        // One more customer walks up to the end of the queue; false if it is full, nobody is free, or there is no way in.
        private bool Join(CitySite site)
        {
            int place = -1;
            for (int i = 0; i < _placeCount; i++)
            {
                if (_queue[i] == null)
                {
                    place = i;
                    break;
                }
            }
            if (place < 0)
            {
                return false;
            }
            for (int i = place + 1; i < _placeCount; i++)
            {
                if (_queue[i] != null)
                {
                    return false;   // somebody is still closing up from behind: wait for them
                }
            }
            CityPerson customer = Arrive(site, CityLook.Civilian, _places[place]);
            if (customer == null)
            {
                return false;
            }
            _queue[place] = customer;
            _settled[place] = false;
            _due[place] = Time.time + WalkPatience;
            return true;
        }
    }
}
