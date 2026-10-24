using System;
using System.Collections.Generic;
using System.Globalization;
using RealLifeOverhaul.GameUi;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.Hustles
{
    /// <summary>
    /// The director's part at places: the game's own panel at the door of a seller, a client or a repair shop, and over the
    /// player at a place of theirs to fix or clean a thing; each piece of work on the game's work panel with its clock
    /// (Jobs/ShiftSkip). Once a second the doors in play (a handful) are measured against the player; nothing per frame.
    /// </summary>
    public sealed partial class HustleDirector
    {
        private const float Reach = 3.5f;          // metres from the pavement at a door that count as at it
        private const float Rise = 3f;             // ...and up or down
        private const float PanelUp = 2.2f;        // metres over the door or the player the panel stands
        private const int FrontsKept = 48;         // doors measured once and remembered, while the city lasts
        private const int LooksASecond = 2;        // buildings looked up in the city's list a second: many new doors are spread over seconds
        private const float MeasureWithin = 80f;   // metres from a building the player is to be before its pavement is measured

        private enum At { None, Collect, Gig, Shop, Home }

        private struct FrontSpot
        {
            public string Street;
            public int Number;
            public CityBuildingController Building;   // null: the city has no such building
            public bool Has;                          // the pavement at its door was measured
            public Vector3 Front;
        }

        private readonly List<FrontSpot> _fronts = new List<FrontSpot>(FrontsKept);
        private DoorPanel _door;
        private Action _onFirst, _onSecond;
        private Func<string> _doorWhat;
        private ShiftSkip _skip;
        private HustleWork _work;
        private At _at;
        private int _atId = -1;
        private string _atStreet;
        private int _atNumber;
        private Vector3 _atFront;
        private int _shownFor = int.MinValue;   // what the panel's words were last written for: the place, the thing and what it shows
        private int _hiredFor = -1;        // the visit the jobs' van was hired for

        private void MakePanel()
        {
            _door = new DoorPanel(Logger, "Hustles check (door panel)");
            _onFirst = _door.Press(First);
            _onSecond = _door.Press(Second);
            _doorWhat = () => _at + " " + _atId;
        }

        private bool WorkOpen => _skip != null && _skip.Open;
        private int _looks;

        private void Places()
        {
            _looks = LooksASecond;
            if (_skip != null && !_skip.Open)
            {
                _skip = null;
                _work?.Drop();
                _work = null;
            }
            if (WorkOpen || !HustleGame.Free)
            {
                HidePanel("the player is busy");
                return;
            }
            PlayerController player = HustleGame.Player;
            if (player == null)
            {
                HidePanel("no player");
                return;
            }
            Vector3 here = player.transform.position;
            if (HustleGame.OnFootOutside && FindDoor(here))
            {
                Present(here);
                return;
            }
            if (HustleGame.AtOwnPlace && FindHomeWork())
            {
                Present(here);
                return;
            }
            HidePanel("nothing to do here");
        }

        // The first thing in play at a door the player stands at: something to collect, a visit, a repair shop.
        private bool FindDoor(Vector3 here)
        {
            int stamp = HustleGame.Stamp;
            for (int i = 0; i < _state.Items.Count; i++)
            {
                Owned o = _state.Items[i];
                if (o.State == OwnedState.Waiting && Near(o.Street, o.Number, here))
                {
                    return Aim(At.Collect, o.Id, o.Street, o.Number);
                }
                if (o.State == OwnedState.Kept && o.Plan == OwnedPlan.Shop && Near(o.Street, o.Number, here))
                {
                    return Aim(At.Shop, o.Id, o.Street, o.Number);
                }
            }
            for (int i = 0; i < _state.Gigs.Count; i++)
            {
                Gig g = _state.Gigs[i];
                if (!HustleRules.Open(g, stamp))
                {
                    continue;
                }
                HustlePlace where = HustleRules.Where(g);
                if (Near(where.Street, where.Number, here))
                {
                    return Aim(At.Gig, g.Id, where.Street, where.Number);
                }
            }
            return false;
        }

        private bool FindHomeWork()
        {
            for (int i = 0; i < _state.Items.Count; i++)
            {
                Owned o = _state.Items[i];
                if (o.State == OwnedState.Kept && (o.Plan == OwnedPlan.FixAtHome || o.Plan == OwnedPlan.CleanAtHome))
                {
                    return Aim(At.Home, o.Id, null, 0);
                }
            }
            return false;
        }

        private bool Aim(At at, int id, string street, int number)
        {
            if (at != _at || id != _atId)
            {
                _shownFor = int.MinValue;   // the words are written afresh
            }
            _at = at;
            _atId = id;
            _atStreet = street;
            _atNumber = number;
            return true;
        }

        private bool Near(string street, int number, Vector3 here)
        {
            if (string.IsNullOrEmpty(street) || !FrontOf(street, number, here, false, out Vector3 front))
            {
                return false;
            }
            Vector3 d = front - here;
            if (Mathf.Abs(d.y) > Rise)
            {
                return false;
            }
            d.y = 0f;
            if (d.sqrMagnitude > Reach * Reach)
            {
                return false;
            }
            _atFront = front;
            return true;
        }

        // A door's pavement, measured once and remembered: a building is looked up in the city's list once, a few a second, and
        // its pavement measured once the player is near it (far off, the walking mesh there may not be ready); a miss is tried again.
        private bool FrontOf(string street, int number, Vector3 near, bool now, out Vector3 front)
        {
            front = default;
            int at = -1;
            for (int i = 0; i < _fronts.Count && at < 0; i++)
            {
                at = _fronts[i].Number == number && string.Equals(_fronts[i].Street, street, StringComparison.Ordinal) ? i : -1;
            }
            if (at < 0)
            {
                if (!now && _looks <= 0)
                {
                    return false;
                }
                _looks--;
                if (_fronts.Count >= FrontsKept)
                {
                    _fronts.Clear();
                }
                _fronts.Add(new FrontSpot { Street = street, Number = number, Building = HustleGame.Building(street, number) });
                at = _fronts.Count - 1;
            }
            FrontSpot spot = _fronts[at];
            if (spot.Has)
            {
                front = spot.Front;
                return true;
            }
            if (spot.Building == null || (!now && JobSpots.Flat(spot.Building.transform.position, near) > MeasureWithin) || !JobSpots.Front(spot.Building, out front))
            {
                return false;
            }
            spot.Has = true;
            spot.Front = front;
            _fronts[at] = spot;
            return true;
        }

        /// <summary>The pavement at an address's door, measured now if need be, for the tests and the hired van; false if the city has none.</summary>
        internal bool DoorAt(string street, int number, out Vector3 front)
        {
            PlayerController player = HustleGame.Player;
            return FrontOf(street, number, player != null ? player.transform.position : Vector3.zero, true, out front);
        }

        private void HidePanel(string why)
        {
            _door?.Hide(why);
            _at = At.None;
            _atId = -1;
            _shownFor = int.MinValue;
        }

        private void Present(Vector3 here)
        {
            WorldPanel panel = _door.Panel;
            switch (_at)
            {
                case At.Collect:
                    WriteCollect(panel);
                    break;
                case At.Gig:
                    WriteGig(panel);
                    break;
                case At.Shop:
                    WriteShop(panel);
                    break;
                case At.Home:
                    WriteHome(panel);
                    break;
            }
            _door.Show(_at == At.Home ? here : _atFront, here, PanelUp, _doorWhat);
        }

        private void WriteCollect(WorldPanel panel)
        {
            Owned first = _state.Item(_atId);
            int key = Key(1, _atId, 0);
            if (first == null || _shownFor == key)
            {
                return;
            }
            _shownFor = key;
            int count = 0;
            for (int i = 0; i < _state.Items.Count; i++)
            {
                Owned o = _state.Items[i];
                count += o.State == OwnedState.Waiting && o.Number == first.Number && o.Street == first.Street ? 1 : 0;
            }
            string thing = HustleText.Thing(first.Thing, first.Car);
            panel.Lines(HustleText.T(first.Car ? "reallife_hustle_door_car" : "reallife_hustle_door_collect", "seller", first.Seller, "thing", thing),
                count > 1 ? HustleText.T("reallife_hustle_door_more", "count", (count - 1).ToString(CultureInfo.InvariantCulture)) : null, null, WorldPanel.White());
            panel.Button(0, first.Car ? "reallife_hustle_button_keys" : "reallife_hustle_button_collect", _onFirst, true, true);
            Clear(panel, 1);
        }

        // Returns whether the work can begin here as things stand (a van, when one is needed).
        private bool WriteGig(WorldPanel panel)
        {
            Gig g = _state.GigOf(_atId);
            if (g == null)
            {
                return true;
            }
            GigDef def = HustleCatalogue.Gig(g.Kind);
            bool van = !def.Van || HustleGame.VanNear(_atFront);
            int key = Key(2, _atId, (van ? 8 : 0) + (int)g.Stage);
            if (_shownFor == key)
            {
                return van;
            }
            _shownFor = key;
            string title = HustleText.T(g.Stage == GigStage.Loaded ? "reallife_hustle_door_unload" : "reallife_hustle_door_gig", "job", HustleText.Gig(g.Kind), "client", g.Client);
            string left = !van ? HustleText.T("reallife_hustle_door_needs_van")
                : HustleText.T("reallife_hustle_door_gig_time", "hours", (def.Minutes / 60).ToString(CultureInfo.InvariantCulture),
                    "minutes", (def.Minutes % 60).ToString("00", CultureInfo.InvariantCulture));
            panel.Lines(title, left, HustleText.Money(g.Pay), van ? WorldPanel.Green() : WorldPanel.Yellow());
            panel.Button(0, g.Stage == GigStage.Loaded ? "reallife_hustle_button_unload" : "reallife_hustle_button_start", _onFirst, van, true);
            if (!van)
            {
                panel.Button(1, "reallife_hustle_button_hire_van", _onSecond, true, false);
                Clear(panel, 2);
            }
            else
            {
                Clear(panel, 1);
            }
            return van;
        }

        private void WriteShop(WorldPanel panel)
        {
            Owned o = _state.Item(_atId);
            ThingDef def = o != null ? HustleCatalogue.Thing(o.Thing) : null;
            int key = Key(3, _atId, 0);
            if (def == null || _shownFor == key)
            {
                return;
            }
            _shownFor = key;
            int fee = HustleRules.ShopFee(def, o.Condition, HustleGame.Level);
            panel.Lines(HustleText.T("reallife_hustle_door_shop", "thing", HustleText.Thing(o.Thing, false)), HustleText.T("reallife_hustle_door_shop_wait"),
                HustleText.Money(fee), WorldPanel.White());
            panel.Button(0, "reallife_hustle_button_hand_in", _onFirst, true, true);
            Clear(panel, 1);
        }

        private void WriteHome(WorldPanel panel)
        {
            Owned o = _state.Item(_atId);
            ThingDef def = o != null ? HustleCatalogue.Thing(o.Thing) : null;
            int key = Key(4, _atId, (int)(o != null ? o.Plan : OwnedPlan.None));
            if (def == null || _shownFor == key)
            {
                return;
            }
            _shownFor = key;
            bool fix = o.Plan == OwnedPlan.FixAtHome;
            int cost = fix ? HustleRules.PartsCost(def, HustleGame.Level) : HustleRules.AtPrices(HustleCatalogue.CleanSupplies, HustleGame.Level);
            string thing = HustleText.Thing(o.Thing, false);
            panel.Lines(HustleText.T(fix ? "reallife_hustle_home_fix" : "reallife_hustle_home_clean", "thing", thing),
                fix ? HustleText.T("reallife_hustle_home_fix_chance", "chance", (HustleRules.FixChance(_state.RepairXp, o.Condition) / 10).ToString(CultureInfo.InvariantCulture))
                    : HustleText.Condition(o.Condition),
                HustleText.Money(cost), WorldPanel.White());
            panel.Button(0, fix ? "reallife_hustle_button_fix" : "reallife_hustle_button_clean", _onFirst, true, true);
            Clear(panel, 1);
        }

        // What the panel shows, as one number: a figure is compared each second and no text is made unless it changes.
        private static int Key(int at, int id, int state)
        {
            return (id * 16 + state) * 8 + at;
        }

        private static void Clear(WorldPanel panel, int from)
        {
            for (int s = from; s < WorldPanel.Slots; s++)
            {
                panel.Button(s, null, null, false, false);
            }
        }

        // ---------------------------------------------------------------- the buttons

        private void First()
        {
            if (!_running || WorkOpen || !HustleGame.Free)
            {
                return;
            }
            try
            {
                switch (_at)
                {
                    case At.Collect: Collect(_atStreet, _atNumber); break;
                    case At.Gig: StartGig(_atId, false); break;
                    case At.Shop: StartShop(_atId); break;
                    case At.Home: StartHome(_atId, false); break;
                }
            }
            catch (Exception e)
            {
                Fault("a panel's button", e);
            }
        }

        private void Second()
        {
            if (!_running || WorkOpen || _at != At.Gig)
            {
                return;
            }
            try
            {
                HireVan(_atId);
            }
            catch (Exception e)
            {
                Fault("a panel's button", e);
            }
        }

        /// <summary>Everything waiting at this door is the player's: goods into their things, a car handed over at the kerb. Returns how many.</summary>
        internal int Collect(string street, int number)
        {
            int taken = 0, cars = 0;
            string last = null;
            for (int i = 0; i < _state.Items.Count; i++)
            {
                Owned o = _state.Items[i];
                if (o.State != OwnedState.Waiting || o.Number != number || o.Street != street)
                {
                    continue;
                }
                if (o.Car)
                {
                    if (!HandOver(o))
                    {
                        continue;
                    }
                    cars++;
                }
                o.State = OwnedState.Kept;
                o.Plan = OwnedPlan.None;
                last = HustleText.Thing(o.Thing, o.Car);
                taken++;
            }
            HidePanel("collected");
            if (taken > 0)
            {
                HustleText.Note(NotificationType.Success, cars > 0 ? "reallife_hustle_note_car_collected" : "reallife_hustle_note_collected", "thing", last,
                    "count", taken.ToString(CultureInfo.InvariantCulture));
                Logger?.Info("Hustles check (collected): " + taken + " thing(s) taken from " + street + " " + number + ", " + cars + " car(s) handed over at the kerb: PASS");
                Changed();
            }
            return taken;
        }

        // A car bought from its owner, made at a free stretch of kerb by the player: the keys change hands.
        private bool HandOver(Owned o)
        {
            PlayerController player = HustleGame.Player;
            if (player == null || HustleBridges.KerbSpot == null || !HustleBridges.KerbSpot(player.transform.position, out Vector3 spot, out Quaternion heading))
            {
                HustleText.Note(NotificationType.Warning, "reallife_hustle_note_no_kerb");
                Logger?.Info("Hustles check (car handed over): no free stretch of kerb near " + o.Street + " " + o.Number + "; the car waits there to be collected: FAIL (the player is told)");
                return false;
            }
            o.VehicleId = HustleGame.MakeCar(o, spot, heading);
            if (o.VehicleId == null)
            {
                HustleText.Note(NotificationType.Warning, "reallife_hustle_note_no_kerb");
                Logger?.Warn("Hustles check (car handed over): the game would not make a " + o.Thing + "; the car waits to be collected: FAIL");
                return false;
            }
            Logger?.Info("Hustles check (car handed over): a " + o.Thing + " at " + o.Condition + "% made the game's own way at the kerb, "
                         + Vector3.Distance(spot, player.transform.position).ToString("F0", CultureInfo.InvariantCulture) + " m from the player: PASS");
            return true;
        }

        /// <summary>A visit begun at the client's door on the game's work panel; false (the player told why) if it cannot be now.</summary>
        internal bool StartGig(int id, bool test)
        {
            Gig g = _state.GigOf(id);
            if (g == null || (g.Stage != GigStage.Booked && g.Stage != GigStage.Loaded))
            {
                return false;
            }
            GigDef def = HustleCatalogue.Gig(g.Kind);
            if (!CanWork(test))
            {
                return false;
            }
            if (def.Van && !test && DoorAt(HustleRules.Where(g).Street, HustleRules.Where(g).Number, out Vector3 front) && !HustleGame.VanNear(front))
            {
                HustleText.Note(NotificationType.Warning, "reallife_hustle_door_needs_van");
                return false;
            }
            int stamp = HustleGame.Stamp;
            if (g.Stage == GigStage.Booked)
            {
                g.Late = stamp > g.From;
            }
            string title = HustleText.T("reallife_hustle_work_gig", "job", HustleText.Gig(g.Kind), "client", g.Client);
            var lines = new[]
            {
                HustleText.T("reallife_hustle_work_gig_pay", "amount", HustleText.Money(g.Pay)),
                HustleText.T(g.Late ? "reallife_hustle_work_gig_late" : "reallife_hustle_work_gig_skill", "skill", SkillFor(def).ToString(CultureInfo.InvariantCulture))
            };
            return Offer(new HustleWork(title, def.Minutes, lines, (minutes, whole) => GigEnded(id, minutes, whole)), "a visit for " + g.Client);
        }

        private void GigEnded(int id, int minutes, bool whole)
        {
            Gig g = _state != null ? _state.GigOf(id) : null;
            if (g == null || (g.Stage != GigStage.Booked && g.Stage != GigStage.Loaded))
            {
                return;
            }
            GigDef def = HustleCatalogue.Gig(g.Kind);
            int skill = SkillFor(def);
            int pay = HustleRules.Worked(_state, g, minutes, whole, skill);
            HustleGame.Tire(minutes);
            if (minutes > 0)
            {
                Train(def, minutes);
            }
            if (g.Stage == GigStage.Loaded)
            {
                HustleText.Note(NotificationType.Info, "reallife_hustle_note_loaded", "place", HustleText.Place(g.Street2, g.Number2), 10f);
                HustleGame.Guide(g.Street2, g.Number2);
                Logger?.Info("Hustles check (visit, first stop): the move for " + g.Client + " is loaded; the second address is " + g.Street2 + " " + g.Number2 + ": PASS");
            }
            else
            {
                if (!g.Test)
                {
                    HustleGame.Earned(pay, "reallife_transaction_hustle_client");
                }
                HustleText.Note(NotificationType.Success, g.Test ? "reallife_hustle_note_paid_test" : "reallife_hustle_note_paid", "who", g.Client, "amount", HustleText.Money(pay),
                    "stars", g.Rating.ToString(CultureInfo.InvariantCulture), 12f);
                Logger?.Info("Hustles check (visit): " + def.Kind + " for " + g.Client + " " + (whole ? "done" : "stopped after " + minutes + " min") + ", " + g.Rating + " star(s)"
                             + (g.Late ? " (late)" : string.Empty) + ", paid " + pay + (g.Test ? " (a test: no money moved)" : string.Empty) + "; rating now "
                             + (_state.Rating / 1000f).ToString("0.0", CultureInfo.InvariantCulture) + ": PASS");
                if (_hiredFor == id)
                {
                    _hiredFor = -1;
                    HustleBridges.ReturnVan?.Invoke();
                }
            }
            Changed();
        }

        private void HireVan(int id)
        {
            Gig g = _state.GigOf(id);
            if (g == null || HustleBridges.HireVan == null)
            {
                HustleText.Note(NotificationType.Warning, "reallife_job_no_car");
                return;
            }
            HustlePlace where = HustleRules.Where(g);
            if (!DoorAt(where.Street, where.Number, out Vector3 front))
            {
                return;
            }
            CarController van = HustleBridges.HireVan(front, out string reason);
            if (van == null)
            {
                HustleText.Note(NotificationType.Warning, reason ?? "reallife_job_no_car");
                return;
            }
            _hiredFor = id;
            _shownFor = int.MinValue;
            Logger?.Info("Hustles: the jobs' van was hired for the visit to " + g.Client + ".");
        }

        private void StartShop(int id)
        {
            Owned o = _state.Item(id);
            ThingDef def = o != null ? HustleCatalogue.Thing(o.Thing) : null;
            if (def == null || !CanWork(false))
            {
                return;
            }
            if (!HustleGame.ShopOpen(o.Street, o.Number))
            {
                HustleText.Note(NotificationType.Warning, "reallife_hustle_note_shop_closed");
                return;
            }
            int fee = HustleRules.ShopFee(def, o.Condition, HustleGame.Level);
            if (HustleGame.Cash < fee)
            {
                HustleText.Note(NotificationType.Warning, "reallife_hustle_note_short", "amount", HustleText.Money(fee));
                return;
            }
            string thing = HustleText.Thing(o.Thing, false);
            var lines = new[] { HustleText.T("reallife_hustle_work_shop_fee", "amount", HustleText.Money(fee)), HustleText.T("reallife_hustle_work_shop_after", "percent",
                HustleCatalogue.ShopCondition.ToString(CultureInfo.InvariantCulture)) };
            Offer(new HustleWork(HustleText.T("reallife_hustle_work_shop", "thing", thing), HustleCatalogue.ShopMinutes, lines, (minutes, whole) => ShopEnded(id, whole, fee)),
                "a repair at the shop");
        }

        private void ShopEnded(int id, bool whole, int fee)
        {
            Owned o = _state != null ? _state.Item(id) : null;
            if (o == null)
            {
                return;
            }
            string thing = HustleText.Thing(o.Thing, false);
            if (!whole)
            {
                HustleText.Note(NotificationType.Info, "reallife_hustle_note_shop_stopped", "thing", thing);
                return;
            }
            if (!o.Test && !HustleGame.Pay(fee, "reallife_transaction_hustle_repair"))
            {
                HustleText.Note(NotificationType.Warning, "reallife_hustle_note_short", "amount", HustleText.Money(fee));
                return;
            }
            int before = o.Condition;
            HustleRules.ShopFixed(o);
            o.Spent += o.Test ? 0 : fee;
            o.Plan = OwnedPlan.None;
            HustleText.Note(NotificationType.Success, "reallife_hustle_note_shop_done", "thing", thing, "percent", o.Condition.ToString(CultureInfo.InvariantCulture));
            Logger?.Info("Hustles check (repair shop): the " + o.Thing + " went from " + before + "% to " + o.Condition + "% for " + fee + ": PASS");
            Changed();
        }

        /// <summary>A repair or a clean by the player's own hand on the game's work panel; false (the player told why) if it cannot be now.</summary>
        internal bool StartHome(int id, bool test)
        {
            Owned o = _state.Item(id);
            ThingDef def = o != null ? HustleCatalogue.Thing(o.Thing) : null;
            if (def == null || !CanWork(test))
            {
                return false;
            }
            bool fix = o.Plan == OwnedPlan.FixAtHome;
            if ((fix && !HustleRules.CanFix(o)) || (!fix && !HustleRules.CanClean(o)))
            {
                o.Plan = OwnedPlan.None;
                HustleText.Note(NotificationType.Info, fix ? "reallife_hustle_note_fixed_already" : "reallife_hustle_note_cleaned_already", "thing", HustleText.Thing(o.Thing, false));
                Changed();
                return false;
            }
            int cost = fix ? HustleRules.PartsCost(def, HustleGame.Level) : HustleRules.AtPrices(HustleCatalogue.CleanSupplies, HustleGame.Level);
            if (!test && !o.Test && HustleGame.Cash < cost)
            {
                HustleText.Note(NotificationType.Warning, "reallife_hustle_note_short", "amount", HustleText.Money(cost));
                return false;
            }
            int minutes = fix ? def.FixMinutes : HustleCatalogue.CleanMinutes;
            string thing = HustleText.Thing(o.Thing, false);
            var lines = fix
                ? new[]
                {
                    HustleText.T("reallife_hustle_work_parts", "amount", HustleText.Money(cost)),
                    HustleText.T("reallife_hustle_work_chance", "chance", (HustleRules.FixChance(_state.RepairXp, o.Condition) / 10).ToString(CultureInfo.InvariantCulture))
                }
                : new[] { HustleText.T("reallife_hustle_work_supplies", "amount", HustleText.Money(cost)), HustleText.Condition(o.Condition) };
            return Offer(new HustleWork(HustleText.T(fix ? "reallife_hustle_work_fix" : "reallife_hustle_work_clean", "thing", thing), minutes, lines,
                (worked, whole) => HomeEnded(id, fix, whole, worked, cost)), fix ? "a repair by hand" : "a clean");
        }

        private void HomeEnded(int id, bool fix, bool whole, int minutes, int cost)
        {
            Owned o = _state != null ? _state.Item(id) : null;
            if (o == null)
            {
                return;
            }
            HustleGame.Tire(minutes);
            string thing = HustleText.Thing(o.Thing, false);
            if (!whole)
            {
                HustleText.Note(NotificationType.Info, "reallife_hustle_note_home_stopped", "thing", thing);
                return;
            }
            if (!o.Test && !HustleGame.Pay(cost, fix ? "reallife_transaction_hustle_parts" : "reallife_transaction_hustle_supplies"))
            {
                HustleText.Note(NotificationType.Warning, "reallife_hustle_note_short", "amount", HustleText.Money(cost));
                return;
            }
            o.Spent += o.Test ? 0 : cost;
            int before = o.Condition;
            if (fix)
            {
                bool worked = HustleRules.FixByHand(_state, o, HustleGame.Stamp);
                if (worked || !HustleRules.CanFix(o))
                {
                    o.Plan = OwnedPlan.None;
                }
                HustleText.Note(worked ? NotificationType.Success : NotificationType.Warning, worked ? "reallife_hustle_note_fixed" : "reallife_hustle_note_fix_failed",
                    "thing", thing, "percent", o.Condition.ToString(CultureInfo.InvariantCulture), 10f);
                Logger?.Info("Hustles check (repair by hand): the " + o.Thing + " " + (worked ? "was mended" : "was not mended") + ", " + before + "% to " + o.Condition + "%; experience now "
                             + _state.RepairXp + ": PASS");
            }
            else
            {
                HustleRules.Clean(o);
                o.Plan = OwnedPlan.None;
                HustleText.Note(NotificationType.Success, "reallife_hustle_note_cleaned", "thing", thing, "percent", o.Condition.ToString(CultureInfo.InvariantCulture));
                Logger?.Info("Hustles check (clean): the " + o.Thing + " went from " + before + "% to " + o.Condition + "%: PASS");
            }
            Changed();
        }

        /// <summary>The skill a visit draws on, 0 to 100: one of the game's, or for odd jobs the player's own repair experience.</summary>
        internal int SkillFor(GigDef def)
        {
            return def.Skill != null ? HustleGame.Skill(def.Skill) : _state.RepairXp;
        }

        // A visit trains its skill: the game's own, or for odd jobs a point of repair experience for a whole visit.
        private void Train(GigDef def, int minutes)
        {
            if (def.Skill != null)
            {
                HustleGame.Train(def.Skill, HustleCatalogue.GigSkill * minutes / Mathf.Max(1, def.Minutes));
            }
            else if (minutes >= def.Minutes)
            {
                _state.RepairXp = Mathf.Min(HustleCatalogue.XpMost, _state.RepairXp + 1);
            }
        }

        // Nothing else has the player, a gig is not on, and they have the energy for it (a test waives the energy).
        private bool CanWork(bool test)
        {
            if (HustleBridges.Busy)
            {
                HustleText.Note(NotificationType.Warning, "reallife_hustle_note_on_shift");
                return false;
            }
            if (!test && SaveGameManager.Current != null && SaveGameManager.Current.Energy < 15f)
            {
                HustleText.Note(NotificationType.Warning, "reallife_hustle_note_tired");
                return false;
            }
            return true;
        }

        private bool Offer(HustleWork work, string what)
        {
            HidePanel("the work is offered");
            ShiftSkip skip = ShiftSkip.Offer(this, work);
            if (skip == null)
            {
                work.Drop();
                HustleText.Note(NotificationType.Warning, "reallife_hustle_note_busy");
                Logger?.Info("Hustles: the game's work panel would not take " + what + " (another panel, or the clock is running).");
                return false;
            }
            _skip = skip;
            _work = work;
            if (Verbose)
            {
                Logger?.Info("Hustles check (work panel): " + what + " is on the game's own work panel, " + work.SkipMinutes + " game minutes: PASS");
            }
            return true;
        }

        private void CloseWork()
        {
            if (_skip != null)
            {
                _work?.Drop();   // what was done is not settled: the panel closes with nothing paid or changed
                _skip.Close();
            }
            _skip = null;
            _work = null;
        }
    }
}
