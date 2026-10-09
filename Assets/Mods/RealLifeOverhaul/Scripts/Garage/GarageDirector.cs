using System;
using System.Collections.Generic;
using System.Text;
using BAModAPI;
using Helpers;
using Localizor;
using UI.Load;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The Garage: the custom shop for cars (<see cref="GarageShop"/>), the builds on the player's cars
    /// (<see cref="GarageCar"/>, <see cref="GarageTuning"/>), and bicycles and motorbikes
    /// (<see cref="TwoWheelers"/>). This is the one component that ticks: per frame it compares what the
    /// player is riding and, while a prompt is up, tests one key; four times a second it looks for a
    /// prompt; once a second it puts builds on cars the game has spawned since, lights underglow after
    /// dark and looks after the two-wheelers. Anything unexpected is logged once, everything is put back
    /// and the Garage stays off for the session.
    /// </summary>
    public sealed class GarageDirector : MonoBehaviour
    {
        public static IModLogger Logger;

        public static GarageDirector Instance { get; private set; }

        private static object _promptOwner;

        private GarageShop _shop;
        private TwoWheelers _bikes;
        private float _nextQuick, _nextSlow;
        private bool _failed;
        private int _exhaustFineDay = -1;
        private float _nextPoliceLook;
        private bool _wet;

        public GarageShop Shop => _shop;

        public TwoWheelers Bikes => _bikes;

        private void Awake()
        {
            Instance = this;
            GarageState.Reset();
            _shop = new GarageShop(transform);
            _bikes = new TwoWheelers(transform);
            gameObject.AddComponent<GarageTuning>();
        }

        private void Update()
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
                if ((_shop.PromptOn || _bikes.PromptOn) && Keys.Down(KeyAction.Garage) && !Leisure.PhotoMode.Active)   // (photo mode has the same key for its filters)
                {
                    if (_shop.PromptOn)
                    {
                        _shop.Use(gm);
                    }
                    else
                    {
                        _bikes.Use(gm);
                    }
                }
                float now = Time.unscaledTime;
                if (now >= _nextQuick)
                {
                    _nextQuick = now + 0.25f;
                    if (GarageState.Loaded && !LoadScene.isLoading)
                    {
                        _shop.Tick(gm);
                        _bikes.Quick(gm);
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
                if (!(vehicle is CarController) || vehicle.vehicleInstance == null || (_shop.IsOpen && _shop.Car == vehicle))
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

        /// <summary>The one line at the bottom of the screen. Whoever put it up is the only one who can take it down.</summary>
        public static void Prompt(object owner, string text)
        {
            if (text != null)
            {
                _promptOwner = owner;
                GarageUi.Instance.Prompt(text);
            }
            else if (_promptOwner == owner)
            {
                _promptOwner = null;
                if (GarageUi.Exists)
                {
                    GarageUi.Instance.Prompt(null);
                }
            }
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
            if (Instance == this)
            {
                Instance = null;
            }
            _promptOwner = null;
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
                    _bikes.Dispose();
                }
                else
                {
                    _bikes.ClosePanel();
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
                Prompt(_promptOwner, null);
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
                    if (all[i] is CarController)
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
    /// to its worth, and every bicycle and motorbike with where it stands. Shown as a panel of the Garage's
    /// own (from the shop, or the options button); <see cref="Lines"/> gives the same lines to any other
    /// feature that has a garage page of its own to put them on.
    /// </summary>
    public static class GarageSummary
    {
        private const int Rows = 8;

        private static GameObject _panel;
        private static readonly List<string> Titles = new List<string>(Rows);
        private static readonly List<string> Details = new List<string>(Rows);
        private static readonly List<string> BikeIds = new List<string>(Rows);   // beside Titles: the two-wheeler of a row, null for a car

        public static bool IsOpen => _panel != null;

        public static void Toggle()
        {
            if (_panel != null)
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
            if (_panel != null)
            {
                UnityEngine.Object.Destroy(_panel);
                _panel = null;
                GarageUi.GiveCursor();
            }
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
                    "value", GarageText.Money(def.Price * GarageCatalogue.Resale)));
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

        public static void Open()
        {
            Close();
            if (!GarageState.Loaded)
            {
                TestRefusals.Tell(GarageDirector.Logger, "My vehicles", TestRefusal.NotLoaded);
                return;
            }
            GarageUi ui = GarageUi.Instance;
            Lines(Titles, Details, BikeIds);
            var gm = InstanceBehavior<GameManager>.Instance;
            Vector3 player = gm != null && gm.playerController != null ? gm.playerController.transform.position : Vector3.zero;
            RectTransform panel = ui.Box("My Vehicles", ui.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(120f, 20f), new Vector2(900f, 640f), GarageUi.Plate);
            _panel = panel.gameObject;
            GarageUi.TakeCursor();
            ui.TextAt(panel, 20f, 12f, 860f, 32f, 26f, Color.white, GarageText.T("reallife_garage_my_vehicles"));
            if (Titles.Count == 0)
            {
                ui.TextAt(panel, 20f, 64f, 860f, 60f, 18f, GarageUi.Dim, GarageText.T("reallife_garage_summary_none"));
            }
            for (int i = 0; i < Titles.Count && i < Rows; i++)
            {
                float y = 56f + i * 64f;
                ui.At("Row", panel, 20f, y, 860f, 58f, GarageUi.Row).GetComponent<UnityEngine.UI.Image>().raycastTarget = false;
                string id = i < BikeIds.Count ? BikeIds[i] : null;
                BikeRecord bike = id != null ? GarageState.Bike(id) : null;
                float width = bike != null ? 500f : 836f;
                ui.TextAt(panel, 32f, y + 5f, width, 24f, 19f, Color.white, Titles[i]);
                ui.TextAt(panel, 32f, y + 31f, width, 22f, 14f, GarageUi.Dim, Details[i]);
                if (bike != null)
                {
                    // A two-wheeler can be pointed at, and stood in front of the player (free from close by, a small fee from further off).
                    int fee = TwoWheelers.BringFee(TwoWheelers.Where(bike), player);
                    ui.ButtonAt(panel, 544f, y + 9f, 120f, 40f, GarageUi.Chosen, GarageText.T("reallife_garage_summary_show"), () => Show(id), 16f);
                    ui.ButtonAt(panel, 672f, y + 9f, 200f, 40f, GarageUi.Go,
                        fee > 0 ? GarageText.T("reallife_garage_summary_bring_fee", "price", GarageText.Money(fee)) : GarageText.T("reallife_garage_summary_bring"), () => Bring(id), 16f);
                }
            }
            if (Titles.Count > Rows)
            {
                ui.TextAt(panel, 20f, 56f + Rows * 64f, 500f, 22f, 15f, GarageUi.Dim, GarageText.T("reallife_garage_summary_more", "n", (Titles.Count - Rows).ToString()));
            }
            ui.ButtonAt(panel, 680f, 588f, 200f, 40f, GarageUi.Stop, GarageText.T("reallife_garage_close"), Close, 18f);
        }

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
