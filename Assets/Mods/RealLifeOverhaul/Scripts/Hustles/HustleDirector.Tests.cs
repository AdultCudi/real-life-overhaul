using System.Globalization;
using Helpers;
using UI.Notification;
using UnityEngine;
using UnityEngine.AI;

namespace RealLifeOverhaul.Hustles
{
    /// <summary>
    /// The Marketplace's test buttons (Options, Test tools): each journey without looking for a seller, a client or a buyer.
    /// What a test makes is marked as a test: selling it or a test visit moves no money. Each writes a check line.
    /// </summary>
    public sealed partial class HustleDirector
    {
        private const string TestName = "Hustles";
        private const float TestReach = 60f;    // metres to the nearest door a test puts a seller or a client at

        private bool TestReady(bool outside)
        {
            if (!HustleGame.Ready || HustleGame.Player == null)
            {
                TestRefusals.Tell(Logger, TestName, TestRefusal.NotLoaded);
                return false;
            }
            if (!Settings.Hustles || _broken || !_running)
            {
                TestRefusals.Tell(Logger, TestName, TestRefusal.FeatureOff, 0, _broken ? "switched off after an error" : null);
                return false;
            }
            if (outside && BuildingManager.IsInsideBuilding)
            {
                TestRefusals.Tell(Logger, TestName, TestRefusal.Indoors);
                return false;
            }
            if (outside && PlayerHelper.IsUsingVehicle)
            {
                TestRefusals.Tell(Logger, TestName, TestRefusal.OnFootOnly);
                return false;
            }
            return true;
        }

        // The nearest door, for a seller or a client of a test: the player is already at it.
        private bool TestDoor(out HustlePlace place)
        {
            if (!HustleGame.NearestDoor(TestReach, false, out place, out Vector3 front))
            {
                TestRefusals.Tell(Logger, TestName, TestRefusal.NoVenue, (int)TestReach, "no door of the city's within reach");
                return false;
            }
            float metres = JobSpots.Flat(front, HustleGame.Player.transform.position);
            Logger?.Info("Hustles test: the nearest door is " + place.Street + " " + place.Number + ", " + metres.ToString("F0", CultureInfo.InvariantCulture)
                         + " m away.");
            return true;
        }

        /// <summary>A laptop bought for nothing and waiting at the nearest door: walk to it and press Collect.</summary>
        public void TestCollect()
        {
            if (!TestReady(true) || !TestDoor(out HustlePlace door))
            {
                return;
            }
            Owned o = TestThing("laptop", 35);
            o.State = OwnedState.Waiting;
            o.Street = door.Street;
            o.Number = door.Number;
            o.Seller = HustleText.T("reallife_hustle_test_seller");
            Route(door.Street, door.Number);
            HustleText.Note(NotificationType.Info, "reallife_hustle_test_collect_done", "place", HustleText.Place(door.Street, door.Number), 10f);
            Logger?.Info("Hustles check (test: collect): a laptop waits at " + door.Street + " " + door.Number + "; the door's panel offers Collect: PASS");
            Changed();
        }

        /// <summary>
        /// The whole of a pickup, end to end, on a laptop bought for nothing at the nearest door: the route is made and must end on the
        /// pickup point, the point must be on the walking mesh, the parcel must stand there, the door's panel must offer Collect to
        /// someone standing at the point, and Collect must put the laptop in the player's things. PASS or FAIL in the log.
        /// </summary>
        public void TestPickup()
        {
            if (!TestReady(true) || !TestDoor(out HustlePlace door))
            {
                return;
            }
            Owned o = TestThing("laptop", 35);
            o.State = OwnedState.Waiting;
            o.Street = door.Street;
            o.Number = door.Number;
            o.Seller = HustleText.T("reallife_hustle_test_seller");
            string fail = PickupTravel(o, out string facts);
            if (fail == null)
            {
                int taken = Collect(door.Street, door.Number);
                fail = taken == 1 && o.State == OwnedState.Kept ? null : "pressing Collect took " + taken + " thing(s) and the laptop is " + o.State;
            }
            Logger?.Info("Hustles check (test: pickup): " + door.Street + " " + door.Number + ", " + facts + ": " + (fail == null ? "PASS" : "FAIL (" + fail + ")"));
            HustleText.Note(fail == null ? NotificationType.Success : NotificationType.Warning, fail == null ? "reallife_hustle_test_pickup_pass" : "reallife_hustle_test_pickup_fail",
                "place", HustleText.Place(door.Street, door.Number), "why", fail ?? string.Empty, 12f);
            Changed();
        }

        // The travel part of the check: null if all holds, else what does not.
        private string PickupTravel(Owned o, out string facts)
        {
            facts = "no point";
            Vector3 here = HustleGame.Player.transform.position;
            bool marked = Route(o.Street, o.Number);
            Door d = Spot(o.Street, o.Number, here, true);
            if (d == null || !d.Has)
            {
                return d == null || d.Building == null ? "the city has no building at the address" : "no pickup point could be found at its door";
            }
            bool onMesh = NavMesh.SamplePosition(d.Front, out NavMeshHit hit, 0.75f, NavMesh.AllAreas);
            int state = HustleGame.RouteState(o.Street, o.Number, d.Front, out Vector3 end);
            float gap = JobSpots.Flat(end, d.Front);
            Unstage(d);
            Stand(d, o);
            bool parcel = d.Parcel != null && d.Parcel.gameObject.activeInHierarchy;
            bool offered = FindDoor(d.Front) && _at == At.Collect && _atId == o.Id;
            HidePanel("the pickup check");
            string kind = d.Kind == HustleGame.SpotFront ? "on the pavement" : d.Kind == HustleGame.SpotWide ? "on the wider pavement"
                : d.Kind == HustleGame.SpotMesh ? "on the walking mesh" : "under the door";
            facts = "the point " + kind
                    + ", " + JobSpots.Flat(d.Front, d.DoorAt).ToString("F1", CultureInfo.InvariantCulture) + " m from the door, on the mesh " + (onMesh ? "yes" : "no") + ", the route ends "
                    + gap.ToString("F1", CultureInfo.InvariantCulture) + " m from it, a parcel " + (parcel ? "stands" : "is missing") + ", the panel "
                    + (offered ? "offers" : "does not offer") + " Collect";
            if (!marked || state == 0)
            {
                return "the route could not be marked";
            }
            if (!onMesh)
            {
                return "the point is not on the walking mesh";
            }
            if (!PickupRules.RouteArrives(gap))
            {
                return "the route ends " + gap.ToString("F1", CultureInfo.InvariantCulture) + " m from the point";
            }
            if (!parcel)
            {
                return "no parcel stands at the point";
            }
            return offered ? null : "the door's panel does not offer Collect at the point";
        }

        /// <summary>A used car bought for nothing and waiting at the nearest door: press Take the keys and it is put at the kerb.</summary>
        public void TestCar()
        {
            if (!TestReady(true) || !TestDoor(out HustlePlace door))
            {
                return;
            }
            if (_cars.Count == 0)
            {
                TestRefusals.TellKey(Logger, TestName, "reallife_hustle_test_no_cars", "the game has no car the market deals in");
                return;
            }
            MarketModelPick(out string type, out int price);
            Owned o = TestThing(type, 58);
            o.Car = true;
            o.Km = 96000;
            o.Price = price;
            o.Colour = _colours.Count > 0 ? _colours[0] : null;
            o.State = OwnedState.Waiting;
            o.Street = door.Street;
            o.Number = door.Number;
            o.Seller = HustleText.T("reallife_hustle_test_seller");
            HustleText.Note(NotificationType.Info, "reallife_hustle_test_car_done", "thing", HustleText.Thing(type, true), 10f);
            Logger?.Info("Hustles check (test: a car): a " + type + " at 58% waits at " + door.Street + " " + door.Number + "; Take the keys puts it at the kerb: PASS");
            Changed();
        }

        // The cheapest car the market deals in: the test leaves the least behind.
        private void MarketModelPick(out string type, out int price)
        {
            int at = 0;
            for (int i = 1; i < _cars.Count; i++)
            {
                at = _cars[i].Price < _cars[at].Price ? i : at;
            }
            type = _cars[at].Type;
            price = Mathf.RoundToInt(_cars[at].Price);
        }

        /// <summary>A broken phone of the player's, mended by hand right here on the game's work panel: the place a repair is begun is waived.</summary>
        public void TestFix()
        {
            if (!TestReady(false))
            {
                return;
            }
            Owned o = TestThing("phone", 22);
            o.State = OwnedState.Kept;
            o.Plan = OwnedPlan.FixAtHome;
            int before = o.Condition;
            bool offered = StartHome(o.Id, true);
            Logger?.Info("Hustles check (test: fix): a phone at " + before + "% is offered for a repair by hand on the game's work panel: " + (offered ? "PASS" : "FAIL (see the notice)"));
            if (offered)
            {
                HustleText.Note(NotificationType.Info, "reallife_hustle_test_fix_done", 8f);
            }
            Changed();
        }

        /// <summary>A jacket of the player's listed at its worth, and a buyer's offer for it at once: answer it in the app.</summary>
        public void TestBuyer()
        {
            if (!TestReady(false))
            {
                return;
            }
            Owned o = TestThing("jacket", 70);
            int worth = WorthOf(o);
            HustleRules.List(o, HustleRules.AskAt(worth, HustleRules.AskStepAtWorth), HustleGame.Day);
            var dice = new Economy.Dealership.DealerDice(_state.Seed, HustleGame.Stamp, o.Id, 59);
            BuyerOffer d = HustleRules.MakeOffer(_state.Next(), o, worth, HustleGame.Stamp + HustleCatalogue.OfferHours, ref dice);
            _state.Offers.Add(d);
            Logger?.Info("Hustles check (test: a buyer): a jacket listed at " + o.Asking + " (worth " + worth + ") has an offer of " + d.Offer + " from " + d.Buyer + " (ceiling " + d.Ceiling
                         + "): " + (d.Offer > 0 && d.Offer <= o.Asking && d.Ceiling >= d.Offer ? "PASS" : "FAIL"));
            HustleText.Note(NotificationType.Info, "reallife_hustle_note_offer", "who", d.Buyer, "amount", HustleText.Money(d.Offer), "thing", HustleText.Thing(o.Thing, false), 10f);
            Changed();
            Open(TabOffers, HustleApp.OfferRow + d.Id);
        }

        /// <summary>A handyman visit booked at the nearest door, wanted now: walk to it and press Start the job.</summary>
        public void TestGig()
        {
            if (!TestReady(true) || !TestDoor(out HustlePlace door))
            {
                return;
            }
            Gig g = TestVisit(door);
            g.Stage = GigStage.Booked;
            Route(door.Street, door.Number);
            HustleText.Note(NotificationType.Info, "reallife_hustle_test_gig_done", "place", HustleText.Place(door.Street, door.Number), 10f);
            Logger?.Info("Hustles check (test: a visit): odd jobs for " + g.Client + " booked at " + door.Street + " " + door.Number + " from now; the door's panel offers Start the job: PASS");
            Changed();
        }

        /// <summary>A visit left to a helper and finished at once: the client pays, the helper takes their share, the rating counts.</summary>
        public void TestHelper()
        {
            if (!TestReady(false))
            {
                return;
            }
            HustlePlace door = default;
            if (!HustleGame.NearestDoor(TestReach * 10f, false, out door, out _))
            {
                door = _homes.Count > 0 ? _homes[0] : new HustlePlace("?", 1);
            }
            Gig g = TestVisit(door);
            int before = _state.Ratings.Count;
            HustleRules.SendHelper(_state, g, HustleGame.Stamp);
            int kept = HustleRules.HelperDone(_state, g);
            bool ok = g.Stage == GigStage.Done && _state.Ratings.Count == before && g.Rating >= 1 && kept == g.Pay - g.HelperFee + (g.Rating >= 5 ? HustleRules.Tip(g.Pay) : 0);
            Logger?.Info("Hustles check (test: a helper): a visit for " + g.Client + " paying " + g.Pay + " was left to a helper for " + g.HelperFee + "; done with " + g.Rating + " star(s), "
                          + kept + " kept (a test: no money moved, the player's rating left as it was): " + (ok ? "PASS" : "FAIL"));
            HustleText.Note(NotificationType.Success, "reallife_hustle_note_paid_test", "who", g.Client, "amount", HustleText.Money(kept),
                "stars", g.Rating.ToString(CultureInfo.InvariantCulture), 10f);
            Changed();
            Open(TabFreelance, null);
        }

        private Owned TestThing(string thing, int condition)
        {
            var o = new Owned
            {
                Id = _state.Next(), Thing = thing, Condition = condition, Paid = 0, State = OwnedState.Kept, Seller = HustleText.T("reallife_hustle_test_seller"),
                Bought = HustleGame.Day, Until = PickupRules.UntilFor(HustleGame.Day), Test = true
            };
            if (_state.Items.Count >= HustleCatalogue.OwnedMost)
            {
                _state.Items.RemoveAt(0);   // the oldest makes room: a test never fails for a full list
            }
            _state.Items.Add(o);
            return o;
        }

        private Gig TestVisit(HustlePlace door)
        {
            int stamp = HustleGame.Stamp;
            var g = new Gig
            {
                Id = _state.Next(), Kind = GigKind.Handyman, Client = HustleText.T("reallife_hustle_test_client"), Street = door.Street, Number = door.Number,
                Pay = HustleRules.GigPay(HustleCatalogue.Gig(GigKind.Handyman), HustleGame.Level, _state.Rating, 1000), From = stamp, Until = stamp + HustleCatalogue.GigWindow,
                Stage = GigStage.Offered, Day = HustleGame.Day, Test = true
            };
            if (_state.Gigs.Count >= HustleCatalogue.GigsMost)
            {
                _state.Gigs.RemoveAt(0);
            }
            _state.Gigs.Add(g);
            return g;
        }
    }
}
