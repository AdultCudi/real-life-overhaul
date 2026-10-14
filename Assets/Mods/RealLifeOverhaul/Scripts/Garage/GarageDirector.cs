using System;
using System.Collections.Generic;
using System.Text;
using BAModAPI;
using Helpers;
using Localizor;
using RealLifeOverhaul.GameUi;
using UI.Load;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The Garage: the custom shop for cars (<see cref="GarageShop"/>), the builds on the player's cars
    /// (<see cref="GarageCar"/>, <see cref="GarageTuning"/>), and bicycles and motorbikes
    /// (<see cref="TwoWheelers"/>). This is the one component that ticks: per frame it compares what the
    /// player is riding; four times a second it looks whether the game's panel is wanted at a dock, a rack or
    /// a stand, and whether an open list may stay open; once a second it puts builds on cars the game has
    /// spawned since, lights underglow after
    /// dark and looks after the two-wheelers. Everything it shows is the game's own interface (the game's
    /// panel at a repair bay, at a dock and at a stand, the game's list window, its colour picker, its
    /// destination marker): it reads no key and draws nothing. Anything unexpected is logged once, everything
    /// is put back and the Garage stays off for the session.
    /// </summary>
    public sealed class GarageDirector : MonoBehaviour
    {
        private static readonly FeatureGuard FrameGuard = Guard.For("garage", "GarageDirector", typeof(GarageDirector));   // its Update, LateUpdate and FixedUpdate (Core/Guard)

        public static IModLogger Logger;

        public static GarageDirector Instance { get; private set; }

        private GarageShop _shop;
        private TwoWheelers _bikes;
        private float _nextQuick, _nextSlow;
        private bool _failed;
        private int _exhaustFineDay = -1;
        private float _nextPoliceLook;
        private bool _wet;
        private VehicleController _shopWanted;   // the test button was pressed on the options page: the shop opens when the menus are closed

        public GarageShop Shop => _shop;

        public TwoWheelers Bikes => _bikes;

        /// <summary>
        /// True while the game's panel at a dock, a rack or a stand has its button on the game's interact key, and in
        /// the frame that button ran: the mod's own interact key does nothing else then (World/NearbyInteraction).
        /// </summary>
        public static bool TakesInteractKey => Instance != null && !Instance._failed && Instance._bikes.TakesInteractKey;

        /// <summary>False once the Garage has switched itself off for the session: its shop and lists are then not offered.</summary>
        public static bool Working => Instance != null && !Instance._failed;

        private void Awake()
        {
            Instance = this;
            GarageState.Reset();
            _shop = new GarageShop(transform);
            _bikes = new TwoWheelers(transform);
            ScenePrewarm.Add("garage: the city's scooter stands being found for the bike-share docks", _bikes.Places.PrewarmStands);
            GarageBay.Logger = Logger;
            WorldPanel.Logger = Logger;
            TravelList.Logger = Logger;
            GuideMark.Logger = Logger;
            gameObject.AddComponent<GarageTuning>();
        }

        private static readonly int HotUpdate = HotSpots.Slot("Garage: GarageDirector.Update");
        private void Update()
        {
            using var hotSpot = HotSpots.Time(HotUpdate);
            if (FrameGuard.Off)
            {
                enabled = false;   // (its feature was switched off after its faults: Core/Guard)
                return;
            }
            try
            {
                OnUpdate();
            }
            catch (System.Exception e)
            {
                Guard.Fail(FrameGuard, e, this);
            }
        }

        private void OnUpdate()
        {
            if (_failed)
            {
                return;
            }
            var gm = InstanceBehavior<GameManager>.Instance;
            if (gm == null)
            {
                return;
            }
            try
            {
                _bikes.Frame(gm);
                float now = Time.unscaledTime;
                if (now >= _nextQuick)
                {
                    _nextQuick = now + 0.25f;
                    if (GarageState.Loaded && !LoadScene.isLoading)
                    {
                        GarageBay.Tick(gm);
                        if (_shopWanted != null && !GarageList.MenuUp)
                        {
                            VehicleController wanted = _shopWanted;
                            _shopWanted = null;
                            if (!_shop.IsOpen && !_shop.Open(wanted))
                            {
                                TestRefusals.Tell(Logger, "Custom shop", TestRefusal.NothingFits, 0, "the shop could not be opened on " + wanted.name);
                            }
                        }
                        _shop.Tick(gm);
                        _bikes.Quick(gm);
                        GarageSummary.Tick(gm);
                    }
                }
                if (now >= _nextSlow)
                {
                    _nextSlow = now + 1f;
                    Slow(gm);
                }
            }
            catch (Exception e)
            {
                _failed = true;
                Logger?.Warn("Garage: switched off after something unexpected; cars are back as the game made them. " + e);
                PutEverythingBack(false);
            }
        }

        private void Slow(GameManager gm)
        {
            GameInstance save = SaveGameManager.Current;
            if (save == null || gm.playerController == null || LoadScene.isLoading)
            {
                return;
            }
            if (!GarageState.Loaded)
            {
                if (!ModState.Loaded)
                {
                    return;   // LifeEvents makes the save's modData ready first
                }
                GarageState.Load(save.modData);
                Logger?.Info("Garage: ready. " + GarageState.Builds.Count + " car build(s), " + GarageState.Bikes.Count + " two-wheeler(s) in this save"
                             + (GarageState.Helmet ? ", a helmet." : "."));
            }
            // The game clears its event hub on every load, so the save hook is kept attached.
            SaveHook.Keep(_onSave ?? (_onSave = OnSaveGame));   // the mod's one relay in the game's save event (Core/SaveHook): nothing is allocated while that event has not changed

            bool on = Settings.Garage;
            bool looks = on && Settings.GarageCosmetics;
            bool handling = on && Settings.GarageTuning;
            _wet = GarageBridges.Wet();
            bool dark = looks && Settings.GarageUnderglow && gm.timeOfDayController != null && gm.timeOfDayController.ShouldOutdoorLightsBeOn();
            int stamp = GarageState.Version * 8 + (looks ? 1 : 0) + (handling ? 2 : 0) + (_wet ? 4 : 0);

            List<VehicleController> all = VehicleHelper.AllPlayerVehicles;
            for (int i = 0; i < all.Count; i++)
            {
                VehicleController vehicle = all[i];
                if (!(vehicle is CarController) || vehicle.vehicleInstance == null || !GoingPublic.MineVehicle(vehicle) || (_shop.IsOpen && _shop.Car == vehicle))
                {
                    continue;
                }
                GarageBuild build = on ? GarageState.Of(vehicle.vehicleInstance.id) : null;
                GarageCar car = GarageCar.Of(vehicle, build != null);
                if (car == null)
                {
                    continue;
                }
                if (car.Stamp != stamp)
                {
                    car.Stamp = stamp;
                    if (build != null)
                    {
                        car.Show(build, looks, handling, _wet);
                    }
                    else
                    {
                        car.Clear();
                    }
                }
                if (car.HasGlow)
                {
                    car.LightGlow(dark);
                }
            }

            LoudExhaust(gm);
            _bikes.Slow(gm);
        }

        // A straight-through exhaust is not street legal: driven hard, it gets noticed now and then, once a day at most.
        private void LoudExhaust(GameManager gm)
        {
            if (!Settings.Garage || !Settings.GarageTuning || !Settings.GaragePolice || !Settings.Police || _exhaustFineDay == TimeHelper.CurrentDay
                || Time.time < _nextPoliceLook || !(gm.selectedVehicle is CarController car) || car.vehicleInstance == null || car.CurrentSpeed < 10f)   // m/s
            {
                return;
            }
            if (GarageBridges.LoudnessOf(car.vehicleInstance.id) < 2)
            {
                return;
            }
            _nextPoliceLook = Time.time + 30f;
            if (UnityEngine.Random.value < 0.08f)
            {
                _exhaustFineDay = TimeHelper.CurrentDay;
                Notice("exhaust", BikeCatalogue.ExhaustFine, "reallife_garage_fine_exhaust");
            }
        }

        /// <summary>
        /// The police have noticed something about the player's vehicle. The Crime feature's bridge deals with
        /// it if it is there; otherwise it is a fine through the mod's own, with a message that says what for.
        /// </summary>
        public static void Notice(string what, int fine, string messageKey)
        {
            bool handled = false;
            try
            {
                handled = GarageBridges.PoliceNotice != null && GarageBridges.PoliceNotice(what, fine);
            }
            catch (Exception e)
            {
                Logger?.Warn("Garage: the police bridge failed. " + e.Message);
            }
            if (!handled)
            {
                Crime.Fine(fine, messageKey);
            }
            Logger?.Info("Garage: noticed by the police (" + what + "): " + (handled ? "handed to the Crime feature." : "fined " + fine + "."));
        }

        private System.Action _onSave;   // made once: Core/SaveHook compares it, it is never built again

        private void OnSaveGame()
        {
            try
            {
                GameInstance save = SaveGameManager.Current;
                if (!GarageState.Loaded || save == null)
                {
                    return;
                }
                if (save.modData == null)
                {
                    save.modData = new Dictionary<string, string>();
                }
                _bikes.RememberPositions();
                GarageState.Save(save.modData, save.VehicleInstances);
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        private void OnDestroy()
        {
            SaveHook.Drop(_onSave);
            PutEverythingBack(true);
            GarageList.Destroy();
            if (Instance == this)
            {
                Instance = null;
            }
        }

        // Every car and scooter as the game made it. On the way out of the city, and if the Garage has to switch itself off.
        private void PutEverythingBack(bool leaving)
        {
            try
            {
                _shop.Dispose();
                GarageSummary.Close();
                if (leaving)
                {
                    GarageBay.Remove();
                    _bikes.Dispose();
                }
                else
                {
                    _bikes.HideUi();
                    GarageBay.CloseList("the Garage switched itself off");   // (the bay's panel stays: car ownership's service is still ordered there)
                }
                List<VehicleController> all = VehicleHelper.AllPlayerVehicles;
                for (int i = 0; i < all.Count; i++)
                {
                    if (all[i] == null)
                    {
                        continue;
                    }
                    GarageCar car = all[i].GetComponent<GarageCar>();
                    if (car != null)
                    {
                        car.Clear();
                        Destroy(car);
                    }
                    BikeMount mount = all[i].GetComponent<BikeMount>();
                    if (mount != null)
                    {
                        mount.Detach();
                    }
                }
            }
            catch (Exception e)
            {
                Logger?.Warn("Garage: not everything could be put back. " + e);
            }
        }

        // ---------------------------------------------------------------- the test buttons

        // The car the player is in, or the nearest one of theirs within 20 m.
        private static VehicleController TestCar(GameManager gm, out TestRefusal why, out string detail)
        {
            why = TestRefusal.None;
            detail = null;
            if (!ModState.Loaded || !GarageState.Loaded || SaveGameManager.Current == null || gm == null || gm.playerController == null)
            {
                why = TestRefusal.NotLoaded;
                return null;
            }
            if (!Settings.Garage)
            {
                why = TestRefusal.FeatureOff;
                return null;
            }
            VehicleController best = gm.selectedVehicle as CarController;
            if (best == null)
            {
                float bestSqr = 20f * 20f;
                Vector3 from = gm.playerController.transform.position;
                List<VehicleController> all = VehicleHelper.AllPlayerVehicles;
                for (int i = 0; i < all.Count; i++)
                {
                    if (all[i] is CarController && GoingPublic.MineVehicle(all[i]))
                    {
                        float sqr = (all[i].transform.position - from).sqrMagnitude;
                        if (sqr < bestSqr)
                        {
                            bestSqr = sqr;
                            best = all[i];
                        }
                    }
                }
            }
            if (best == null)
            {
                why = gm.selectedVehicle != null ? TestRefusal.NotACar : TestRefusal.NoCarNear;
                return null;
            }
            string refusal = GarageShop.Refusal(best);
            if (refusal != null)
            {
                why = refusal == "reallife_garage_refused_not_yours" ? TestRefusal.NotYours : TestRefusal.NotACar;
                detail = best.name;
                return null;
            }
            return best;
        }

        /// <summary>Opens the custom shop on the car the player is in, or the nearest of their cars, wherever it stands.</summary>
        public void TestShop()
        {
            var gm = InstanceBehavior<GameManager>.Instance;
            VehicleController car = TestCar(gm, out TestRefusal why, out string detail);
            if (car != null && (_shop.IsOpen || _failed))
            {
                why = TestRefusal.SceneActive;
                car = null;
            }
            if (car == null)
            {
                TestRefusals.Tell(Logger, "Custom shop", why, 20, detail);
                return;
            }
            if (GarageList.MenuUp)
            {
                // Pressed on the options page: the shop's list stands on the HUD, which is not shown under a menu.
                _shopWanted = car;
                Notifications.Show(NotificationType.Info, "reallife_garage_summary_waits", null, 6f);
                return;
            }
            if (!_shop.Open(car))
            {
                TestRefusals.Tell(Logger, "Custom shop", TestRefusal.NothingFits, 0, "the shop could not be opened on " + car.name);
            }
        }

        /// <summary>A full build picked at random, free, on the current or nearest car.</summary>
        public void TestRandomBuild()
        {
            var gm = InstanceBehavior<GameManager>.Instance;
            VehicleController car = TestCar(gm, out TestRefusal why, out string detail);
            if (car != null && _shop.IsOpen)
            {
                why = TestRefusal.SceneActive;
                car = null;
            }
            if (car == null)
            {
                TestRefusals.Tell(Logger, "Random build", why, 20, detail);
                return;
            }
            GarageBuild build = GarageShop.RandomBuild(car);
            GarageState.Set(car.vehicleInstance.id, build);
            Logger?.Info("Garage: random build on " + car.name + ": [" + build.Write() + "].");
            Notifications.Show(NotificationType.Success, "reallife_garage_test_built", null, 6f);
            _nextSlow = 0f;   // put on the car on the next frame
        }

        /// <summary>The current or nearest car back to stock: its build is forgotten.</summary>
        public void TestStock()
        {
            var gm = InstanceBehavior<GameManager>.Instance;
            VehicleController car = TestCar(gm, out TestRefusal why, out string detail);
            if (car != null && _shop.IsOpen)
            {
                why = TestRefusal.SceneActive;
                car = null;
            }
            if (car == null)
            {
                TestRefusals.Tell(Logger, "Restore stock", why, 20, detail);
                return;
            }
            GarageState.Set(car.vehicleInstance.id, null);
            Logger?.Info("Garage: " + car.name + " is stock again.");
            Notifications.Show(NotificationType.Success, "reallife_garage_test_stock", null, 6f);
            _nextSlow = 0f;
        }

        public void TestBike(BikeKind kind)
        {
            if (_failed || !GarageState.Loaded)
            {
                TestRefusals.Tell(Logger, "Two-wheeler (" + kind + ")", TestRefusal.NotLoaded);
                return;
            }
            _bikes.Test(kind);
        }
    }

    /// <summary>
    /// "My vehicles": every car of the player's with what the custom shop has done to it and what that adds
    /// to its worth, and every bicycle and motorbike with where it stands. Shown in the game's list window
    /// (<see cref="GarageList"/>; from the workshop's list in a repair bay, or the options button): a car is an
    /// entry to read, a two-wheeler has two, one whose button marks it and one whose button has it brought.
    /// <see cref="Lines"/> gives the same lines to any other feature that has a garage page of its own to put
    /// them on.
    /// </summary>
    public static class GarageSummary
    {
        private const int Rows = 8;

        private static readonly object Owner = new object();
        private static readonly List<string> Titles = new List<string>(Rows);
        private static readonly List<string> Details = new List<string>(Rows);
        private static readonly List<string> BikeIds = new List<string>(Rows);   // beside Titles: the two-wheeler of a row, null for a car
        private static readonly Action<int> OnPick = Picked;
        private static readonly Action OnClosed = Closed;
        private static string _closing;
        private static bool _wanted;     // asked for from the options page: opened when the menus are closed

        public static bool IsOpen => GarageList.OpenFor(Owner);

        public static void Toggle()
        {
            if (IsOpen || _wanted)
            {
                Close();
            }
            else
            {
                Open();
            }
        }

        public static void Close()
        {
            _wanted = false;
            if (IsOpen)
            {
                GarageList.Close();
            }
        }

        /// <summary>Four times a second: an open list is closed when another screen comes up, and one asked for under a menu is opened when the menu has gone.</summary>
        public static void Tick(GameManager gm)
        {
            if (_wanted && !GarageList.MenuUp)
            {
                Open();
            }
            else if (IsOpen && GarageList.MenuUp)
            {
                _closing = "another screen came up";
                GarageList.Close();
            }
        }

        public static void Open()
        {
            _wanted = false;
            if (!GarageState.Loaded)
            {
                TestRefusals.Tell(GarageDirector.Logger, "My vehicles", TestRefusal.NotLoaded);
                return;
            }
            if (GarageList.MenuUp)
            {
                // Asked for on the options page: the game's list stands on the HUD, which is not shown under a menu.
                _wanted = true;
                Notifications.Show(NotificationType.Info, "reallife_garage_summary_waits", null, 6f);
                return;
            }
            float began = Time.realtimeSinceStartup;
            TravelList list = GarageList.Take(Owner, false);
            if (list == null)
            {
                return;   // (the game's window could not be copied: one "Game UI:" line says why)
            }
            Lines(Titles, Details, BikeIds);
            var gm = InstanceBehavior<GameManager>.Instance;
            Vector3 player = gm != null && gm.playerController != null ? gm.playerController.transform.position : Vector3.zero;
            int bikes = 0;
            for (int i = 0; i < BikeIds.Count; i++)
            {
                bikes += BikeIds[i] != null ? 1 : 0;
            }
            list.Begin(GarageText.T("reallife_garage_my_vehicles"), Titles.Count == 0 ? GarageText.T("reallife_garage_summary_none")
                : GarageText.T("reallife_garage_summary_info", "cars", (Titles.Count - bikes).ToString(), "bikes", bikes.ToString()));
            for (int i = 0; i < Titles.Count; i++)
            {
                string id = i < BikeIds.Count ? BikeIds[i] : null;
                BikeRecord bike = id != null ? GarageState.Bike(id) : null;
                // An entry's id: twice the row, plus one for "bring it here".
                list.Add(i * 2, Titles[i], Details[i], "reallife_garage_summary_show", bike != null);
                if (bike != null)
                {
                    // A two-wheeler can be marked, and stood in front of the player (free from close by, a small fee from further off).
                    int fee = TwoWheelers.BringFee(TwoWheelers.Where(bike), player);
                    list.Add(i * 2 + 1, fee > 0 ? GarageText.T("reallife_garage_summary_bring_fee", "price", GarageText.Money(fee)) : GarageText.T("reallife_garage_summary_bring"),
                        GarageText.T("reallife_garage_summary_bring_detail", "name", GarageText.T(BikeCatalogue.Of(bike.Kind).Name)), "reallife_garage_list_bring", true);
                }
            }
            _closing = null;
            list.Open(OnPick, OnClosed);
            if (Meta.Dev.Verbose)
            {
                GarageDirector.Logger?.Info("Garage check (my vehicles): the game's list is open with " + (Titles.Count - bikes) + " car(s) with a build and " + bikes + " two-wheeler(s); "
                                            + ((Time.realtimeSinceStartup - began) * 1000f).ToString("F1") + " ms: PASS.");
            }
        }

        private static void Picked(int id)
        {
            int row = id / 2;
            string bike = row >= 0 && row < BikeIds.Count ? BikeIds[row] : null;
            if (bike == null)
            {
                return;
            }
            bool bring = (id & 1) == 1;
            if (Meta.Dev.Verbose)
            {
                GarageDirector.Logger?.Info("Garage check (choice): '" + (bring ? "Bring it here" : "Show me") + "' for two-wheeler " + bike + ", from 'My vehicles'.");
            }
            _closing = bring ? "a two-wheeler is brought" : "a two-wheeler is shown";
            if (bring)
            {
                Bring(bike);
            }
            else
            {
                Show(bike);
            }
        }

        private static void Closed()
        {
            if (Meta.Dev.Verbose)
            {
                GarageDirector.Logger?.Info("Garage check (my vehicles): closed (" + (_closing ?? "cancelled by the player, or another list of the Garage was opened") + ").");
            }
            _closing = null;
        }

        /// <summary>
        /// One title and one line of detail per vehicle, cars with a build first, then two-wheelers. For a
        /// Transport feature's garage page. Builds strings: call it when the page opens, not per frame.
        /// </summary>
        public static void Lines(List<string> titles, List<string> details)
        {
            Lines(titles, details, null);
        }

        private static void Lines(List<string> titles, List<string> details, List<string> bikeIds)
        {
            titles.Clear();
            details.Clear();
            bikeIds?.Clear();
            GameInstance save = SaveGameManager.Current;
            if (save == null || save.VehicleInstances == null || !GarageState.Loaded)
            {
                return;
            }
            var text = new StringBuilder(160);
            foreach (VehicleInstance vehicle in save.VehicleInstances)
            {
                GarageBuild build = vehicle != null ? GarageState.Of(vehicle.id) : null;
                if (build == null)
                {
                    continue;
                }
                text.Length = 0;
                text.Append(GarageText.T(GarageCatalogue.FinishName(build.Finish)));
                text.Append(build.Paint >= 0 ? " #" + build.Paint.ToString("X6") : " " + GarageText.T("reallife_garage_summary_factory"));
                if (build.Lamps != LampTint.Stock)
                {
                    text.Append(", ").Append(GarageText.T(GarageCatalogue.LampName(build.Lamps)));
                }
                if (build.Glow >= 0)
                {
                    text.Append(", ").Append(GarageText.T("reallife_garage_summary_glow"));
                }
                if (build.Spoiler)
                {
                    text.Append(", ").Append(GarageText.T("reallife_garage_body_spoiler"));
                }
                if (build.Rack)
                {
                    text.Append(", ").Append(GarageText.T("reallife_garage_body_rack"));
                }
                if (build.Bar)
                {
                    text.Append(", ").Append(GarageText.T("reallife_garage_body_bar"));
                }
                for (int i = 0; i < build.Parts.Length; i++)
                {
                    PartDef part = GarageCatalogue.Part(build.Parts[i]);
                    if (part != null)
                    {
                        text.Append(", ").Append(GarageText.T(part.Name));
                    }
                }
                titles.Add(GarageText.T("reallife_garage_summary_car", "car", vehicle.vehicleTypeName.GetLocalization(),
                    "value", GarageText.Money(GarageBridges.ValueAdded(vehicle.id))));
                details.Add(text.ToString());
                bikeIds?.Add(null);
            }
            var gm = InstanceBehavior<GameManager>.Instance;
            Vector3 player = gm != null && gm.playerController != null ? gm.playerController.transform.position : Vector3.zero;
            foreach (KeyValuePair<string, BikeRecord> pair in GarageState.Bikes)
            {
                BikeRecord bike = pair.Value;
                BikeDef def = BikeCatalogue.Of(bike.Kind);
                titles.Add(GarageText.T(bike.Owned ? "reallife_garage_summary_bike" : "reallife_garage_summary_hired", "name", GarageText.T(def.Name),
                    "value", GarageText.Money(def.Cost * GarageCatalogue.Resale)));
                // Where it stands now (where it was last left, if the game does not have it in the world), by address and district.
                Vector3 at = TwoWheelers.Where(bike);
                string metres = Mathf.RoundToInt(Vector3.Distance(player, at)).ToString();
                string place = BikePlaces.Whereabouts(at);
                details.Add(place.Length > 0
                    ? GarageText.T("reallife_garage_summary_place", "place", place, "metres", metres)
                    : GarageText.T("reallife_garage_summary_where", "metres", metres));
                bikeIds?.Add(bike.Id);
            }
        }

        // The list goes first: what is brought is stood in front of the player.
        // The panel goes first: the marker is not drawn under it, and what is brought is stood in front of the player.
        private static void Show(string id)
        {
            Close();
            try
            {
                GarageDirector.Instance?.Bikes.ShowMe(id);
            }
            catch (Exception e)
            {
                GarageDirector.Logger?.Warn("Two-wheelers: \"Show me\" did not work. " + e);
            }
        }

        private static void Bring(string id)
        {
            Close();
            try
            {
                GarageDirector.Instance?.Bikes.BringHere(id);
            }
            catch (Exception e)
            {
                GarageDirector.Logger?.Warn("Two-wheelers: \"Bring it here\" did not work. " + e);
            }
        }
    }
}
