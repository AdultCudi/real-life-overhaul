using System;
using System.Collections.Generic;
using BAModAPI;
using Helpers;
using Localizor;
using Parking.UndergroundParking;
using UI.Notification;
using UI.Overlays;
using UnityEngine;
using Random = UnityEngine.Random;

namespace RealLifeOverhaul
{
    /// <summary>
    /// Decides when and which crime happens near the player (dealt like cards by CrimeDeck) and runs the one scene at a
    /// time, never while an accident or a pursuit has the street. It also acts out thefts the game books in the player's
    /// own shop, hands the hourly roll to OffscreenCrimes and answers 911. Idle it costs two time comparisons a frame; with
    /// a scene, the marker each frame and a tick four times a second. Three faults switch it off for the session.
    /// </summary>
    public class CrimeDirector : MonoBehaviour
    {
        private static readonly FeatureGuard FrameGuard = Guard.For("crime", "CrimeDirector", typeof(CrimeDirector));

        internal const int CashLeast = 60;   // dollars at the start's prices: a crime against the player needs this on them
        private const float CheckInterval = 20f;      // seconds
        private const float TickInterval = 0.25f;     // seconds
        private const float WatchInterval = 1f;       // seconds
        private const float MinDistance = 35f;        // metres
        private const float MaxDistance = 80f;        // metres
        private const float ReportRange = 160f;       // metres: a call about something further away is a call about nothing
        private const int MaxFaults = 3;
        private const int PlayerCrimeRestDays = 2;    // in-game days between crimes against the player
        private const float ShopTheftMinimum = 0.5f;  // dollars: a smaller rise in stolen value is rounding, not a theft

        public static IModLogger Logger;
        public static CrimeDirector Instance { get; private set; }

        public static bool IsActive => Instance != null && Instance._scene != null;

        /// <summary>True while an assault, a mugging or a snatch is going on in the street, and where (Wiring/StandingLinks).</summary>
        public static bool Victim(out Vector3 where)
        {
            ICrimeScene scene = Instance != null ? Instance._scene : null;
            where = scene != null ? scene.Focus : Vector3.zero;
            return scene != null && scene.Active && scene.ShowsMarker && !scene.Indoors && (scene.Id == "assault" || scene.Id == "mugging" || scene.Id == "snatch");
        }

        /// <summary>The patrol car the scenes share: the one that collects an arrested suspect, or chases a car.</summary>
        internal static ResponseUnit Patrol => Instance != null ? Instance._patrol : null;

        /// <summary>A traffic car a scene has taken over.</summary>
        internal static ResponseUnit Suspect => Instance != null ? Instance._suspect : null;

        private readonly HashSet<string> _seen = new HashSet<string>();
        private readonly List<string> _unstageable = new List<string>(3);
        private readonly Dictionary<string, string> _markerLabels = new Dictionary<string, string>();
        private Dictionary<string, ICrimeScene> _scenes;
        private ResponseUnit _patrol;
        private ResponseUnit _suspect;
        private OffscreenCrimes _offscreen;

        private ICrimeScene _scene;
        private string _markerLabel;
        private readonly RealLifeOverhaul.GameUi.GuideMark _mark = new RealLifeOverhaul.GameUi.GuideMark("street crime");
        private string _lastId;
        private float _nextCheck;
        private float _nextTick;
        private float _nextWatch;
        private int _lastHour = -1;
        private int _faults;
        private int _lastPlayerCrimeDay = -100;

        // The player's own shop, while they are inside it.
        private BuildingRegistration _watched;
        private float _watchedCost;
        private float _pendingTheft;
        private float _pendingAt;

        private int _wastedDay = -1;
        private int _wastedCalls;

        private void Awake()
        {
            Instance = this;
            Crime.Logger = Logger;
            _mark.Keep(this);
            _nextCheck = Time.time + CheckInterval;
            _patrol = new ResponseUnit(gameObject);
            _suspect = new ResponseUnit(gameObject);
            _scenes = CrimeScenes.Build();
            _offscreen = new OffscreenCrimes();
            gameObject.AddComponent<CuffPose>();
        }

        private void OnDestroy()
        {
            EndScene();
            ReleaseCars();
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private static readonly int HotUpdate = HotSpots.Slot("Crime: CrimeDirector.Update");
        private void Update()
        {
            using var hotSpot = HotSpots.Time(HotUpdate);
            if (FrameGuard.Off)
            {
                enabled = false;
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
            float now = Time.time;
            if (_scene != null)
            {
                try
                {
                    RunScene(now);
                }
                catch (Exception e)
                {
                    Fault(e);
                }
                return;
            }

            if (now >= _nextWatch)
            {
                _nextWatch = now + WatchInterval;
                try
                {
                    Watch(now);
                }
                catch (Exception e)
                {
                    Fault(e);
                }
            }
            if (now < _nextCheck)
            {
                return;
            }
            _nextCheck = now + CheckInterval;
            try
            {
                if (Settings.StreetCrime && CanStage() && Random.value < Settings.CrimeFrequency * 0.02f * PlaceShare() * Events.Weather.CrimeFactor * CityWatch.CrimeScale)
                {
                    Deal(now);
                }
            }
            catch (Exception e)
            {
                Fault(e);
            }
        }

        // The scene is ended first, so a fault cannot repeat every frame; a few of them and it is off for the session.
        private void Fault(Exception e)
        {
            Logger?.Error(e);
            EndScene();
            if (Guard.StandsDown(e, "crime in the street"))
            {
                enabled = false;   // a game that lacks something it names would fail at every look
            }
            else if (++_faults >= MaxFaults)
            {
                Logger?.Warn("Crime: switched off for this session after repeated errors (see above).");
                enabled = false;
            }
        }

        private void RunScene(float now)
        {
            ICrimeScene scene = _scene;
            PlayerController player = PlayerHelper.PlayerController;
            bool placeGone = scene.Indoors ? !BuildingManager.IsInsideBuilding : !TrafficTools.Street;
            if (!scene.Active || player == null || placeGone || !Settings.StreetCrime || !ModState.Loaded)
            {
                EndScene();
                return;
            }

            if (scene.ShowsMarker)
            {
                _mark.Show(scene.Focus, _markerLabel);
            }
            else
            {
                _mark.Hide();
            }

            Vector3 at = player.transform.position;
            // The mod's enter keys: F, or E while the game offers no interaction of its own.
            if (scene.Interactive && (Keys.Down(KeyAction.Interact) || (Keys.Down(KeyAction.InteractAlt) && !OverlayUI.IsVisible))
                && !ThirdPersonCamera.IsUiOpen())
            {
                scene.Interact(now, at);
            }

            if (now < _nextTick)
            {
                return;
            }
            _nextTick = now + TickInterval;
            scene.Tick(now, at);
            if (!scene.Active)
            {
                EndScene();
            }
        }

        private void EndScene()
        {
            ICrimeScene scene = _scene;
            _scene = null;
            _nextCheck = Time.time + CheckInterval * 3f;
            try
            {
                if (scene != null)
                {
                    scene.End();
                    _mark.Hide();
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        /// <summary>Ends whatever scene is running, at once and without a message, for an incident that takes precedence.</summary>
        public static void EndActive()
        {
            if (Instance != null && Instance._scene != null)
            {
                Instance.EndScene();
            }
        }

        /// <summary>Lets go of both shared cars, whatever they were doing. Safe at any time.</summary>
        internal static void ReleaseCars()
        {
            CrimeDirector director = Instance;
            if (director == null)
            {
                return;
            }
            bool traffic = TrafficTools.Ready;
            director._patrol?.Release(traffic, remove: false, quiet: true);
            director._suspect?.Release(traffic, remove: false, quiet: true);
        }

        private static bool CanStage()
        {
            return ModState.Loaded && ActorPool.Instance != null && PlayerHelper.PlayerController != null
                   && !TrafficAccidents.IsActive && !PoliceAttention.IsBusy && LifeEvents.IsSafeForEvent()
                   && !Wiring.Scenes.AnyBut(Wiring.Scene.Crime);   // a fire, a street event or a rally has the street
        }

        private static CrimePlace CurrentPlace()
        {
            if (BuildingManager.IsInsideBuilding)
            {
                return ShopFloor.IsOwnShop ? CrimePlace.OwnShop : ShopFloor.IsOtherShop ? CrimePlace.OtherShop : CrimePlace.None;
            }
            return TrafficTools.Street && !UndergroundParkingManager.IsInsideParking ? CrimePlace.Street : CrimePlace.None;
        }

        // Shops are small and the player is in them briefly: a scene there is rarer.
        private static float PlaceShare()
        {
            return BuildingManager.IsInsideBuilding ? 0.5f : 1f;
        }

        private CrimeNeed CurrentNeeds(bool forced)
        {
            CrimeNeed have = CrimeNeed.None;
            have |= Crime.IsNight || forced ? CrimeNeed.Night : CrimeNeed.None;
            have |= !PlayerHelper.IsUsingVehicle ? CrimeNeed.OnFoot : CrimeNeed.None;
            have |= Settings.Police ? CrimeNeed.PatrolCars : CrimeNeed.None;
            have |= TrafficTools.Ready ? CrimeNeed.Traffic : CrimeNeed.None;
            have |= Settings.Ragdolls ? CrimeNeed.Ragdolls : CrimeNeed.None;
            have |= Crime.Cash >= Prices.At(CashLeast) ? CrimeNeed.Cash : CrimeNeed.None;
            if (forced || (Settings.CrimeAgainstPlayer && TimeHelper.CurrentDay - _lastPlayerCrimeDay >= PlayerCrimeRestDays))
            {
                have |= CrimeNeed.AgainstPlayer;
            }
            return have;
        }

        private void Deal(float now)
        {
            CrimePlace place = CurrentPlace();
            if (place == CrimePlace.None)
            {
                return;
            }
            CrimeNeed have = CurrentNeeds(false);
            int free = ActorPool.Instance.Free;
            // A kind that cannot be staged just here (no parked car, no traffic at a light) goes back in the deck.
            _unstageable.Clear();
            for (int attempt = 0; attempt < 3; attempt++)
            {
                CrimeEntry entry = CrimeDeck.Draw(CrimeCatalogue.All, place, have, free, _seen, _lastId, Crime.Random01);
                if (entry == null)
                {
                    break;
                }
                if (Begin(entry, place, now, false, 0f))
                {
                    _lastId = entry.Id;
                    break;
                }
                _unstageable.Add(entry.Id);   // still seen for the other attempts, so a different kind is drawn
            }
            for (int i = 0; i < _unstageable.Count; i++)
            {
                _seen.Remove(_unstageable[i]);
            }
            _unstageable.Clear();
        }

        private bool Begin(CrimeEntry entry, CrimePlace place, float now, bool forced, float amount)
        {
            using var piece = SessionWatch.Piece("crime: a scene starting");
            if (!_scenes.TryGetValue(entry.Id, out ICrimeScene scene))
            {
                return false;
            }
            Vector3 player = PlayerHelper.PlayerController.transform.position;
            var setup = new CrimeSetup
            {
                Player = player, Spot = player, Place = place, Night = Crime.IsNight, Forced = forced, Amount = amount
            };
            // A crime against the player happens where they are; any other street crime on a pavement some way off.
            if (place == CrimePlace.Street && entry.Family != CrimeFamily.Player
                && !StreetSpots.PavementAround(player, MinDistance, MaxDistance, true, out setup.Spot))
            {
                return false;
            }
            if (!scene.Begin(entry, setup, now))
            {
                return false;
            }
            _scene = scene;
            _nextTick = now + TickInterval;
            if (!_markerLabels.TryGetValue(entry.Marker, out _markerLabel))
            {
                _markerLabel = entry.Marker.GetLocalization();
                _markerLabels[entry.Marker] = _markerLabel;
            }
            if (entry.Family == CrimeFamily.Player)
            {
                _lastPlayerCrimeDay = TimeHelper.CurrentDay;
            }
            News.Report(NewsKind.Crime, setup.Spot, entry.Id);
            Logger?.Info($"Crime: {entry.Id}{(forced ? " (test)" : string.Empty)}, {Vector3.Distance(player, setup.Spot):F0} m away.");
            return true;
        }

        /// <summary>Test button: a crime of one family at once, whatever the hour or the options; says so if none can be staged.</summary>
        public void StageNow(int family)
        {
            try
            {
                CrimePlace place = CurrentPlace();
                bool staged = false;
                // What stops any of them, by name; a place this family cannot play keeps its own sentence below.
                TestRefusal why = !ModState.Loaded || ActorPool.Instance == null || PlayerHelper.PlayerController == null ? TestRefusal.NotLoaded
                    : PoliceAttention.IsBusy ? TestRefusal.PoliceBusy
                    : place == CrimePlace.None && !BuildingManager.IsInsideBuilding ? TestRefusal.TrafficOff
                    : place == CrimePlace.None && family != (int)CrimeFamily.Shop ? TestRefusal.Indoors
                    : TestRefusal.None;
                if (why != TestRefusal.None)
                {
                    TestRefusals.Tell(Logger, "Crime (" + (CrimeFamily)family + ")", why);
                    return;
                }
                if (place != CrimePlace.None)
                {
                    EndScene();
                    CrimeNeed have = CurrentNeeds(true);
                    CrimeEntry[] all = CrimeCatalogue.All;
                    int start = Random.Range(0, all.Length);
                    float now = Time.time;
                    for (int i = 0; i < all.Length && !staged; i++)
                    {
                        CrimeEntry entry = all[(start + i) % all.Length];
                        if ((int)entry.Family != family || entry.Id == _lastId || !entry.Fits(place, have, ActorPool.Instance.Free))
                        {
                            continue;
                        }
                        staged = Begin(entry, place, now, true, 20f);
                        if (staged)
                        {
                            _lastId = entry.Id;
                            // A test says what it staged: several scenes show nothing for the first few seconds.
                            var data = new Dictionary<string, string>(2)
                            {
                                { "name", entry.Id },
                                { "metres", Mathf.RoundToInt(Vector3.Distance(PlayerHelper.PlayerController.transform.position, _scene.Focus)).ToString() }
                            };
                            Notifications.Show(NotificationType.Info, "reallife_crime_test_staged", data, 7f);
                        }
                    }
                    if (!staged)
                    {
                        _lastId = null;   // the only one that fits may be the one just shown
                    }
                }
                if (!staged)
                {
                    if (family == (int)CrimeFamily.Shop)
                    {
                        Logger?.Info("Crime test: not started (no shop crime fits where the player stands).");
                        Notifications.Show(NotificationType.Warning, "reallife_crime_none_shop", null, 7f);
                    }
                    else
                    {
                        TestRefusals.Tell(Logger, "Crime (" + (CrimeFamily)family + ")", TestRefusal.NothingFits, 0,
                            "place " + place + ", characters free " + ActorPool.Instance.Free);
                    }
                }
            }
            catch (Exception e)
            {
                Fault(e);
            }
        }

        // Once a second: the hour's roll, and thefts the game books in the shop the player is standing in.
        private void Watch(float now)
        {
            if (!ModState.Loaded || SaveGameManager.Current == null)
            {
                return;
            }
            int hour = TimeHelper.CurrentHour;
            if (hour != _lastHour)
            {
                bool first = _lastHour < 0;
                _lastHour = hour;
                if (!first && Settings.StreetCrime)
                {
                    _offscreen.OnNewHour(hour);
                }
            }

            BuildingRegistration shop = Settings.StreetCrime ? ShopFloor.OwnRegistration : null;
            if (shop != _watched)
            {
                _watched = shop;
                _watchedCost = shop != null ? shop.stolenItemsCost : 0f;
                _pendingTheft = 0f;
                return;
            }
            if (shop == null)
            {
                return;
            }
            float cost = shop.stolenItemsCost;
            if (cost < _watchedCost)
            {
                _pendingTheft = 0f;   // the day's books closed at midnight, or a thief was stopped
            }
            else if (cost > _watchedCost + ShopTheftMinimum)
            {
                _pendingTheft = cost - _watchedCost;
                _pendingAt = now + Random.Range(3f, 25f);
            }
            _watchedCost = cost;
            if (_pendingTheft > 0f && now >= _pendingAt && CanStage())
            {
                float amount = _pendingTheft;
                _pendingTheft = 0f;
                Begin(CrimeCatalogue.Find("shop_theft_own"), CrimePlace.OwnShop, now, false, amount);
            }
        }

        /// <summary>Test button for the crimes out of sight: each press stages the next kind.</summary>
        public void TestOffscreen()
        {
            try
            {
                if (ModState.Loaded && SaveGameManager.Current != null)
                {
                    _offscreen.TestNext();
                }
                else
                {
                    TestRefusals.Tell(Logger, "Off-screen crime", TestRefusal.NotLoaded);
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        /// <summary>What a 911 call made now would be about; None if nothing is going on within reach.</summary>
        internal static ReportKind Reportable()
        {
            CrimeDirector director = Instance;
            ICrimeScene scene = director != null ? director._scene : null;
            PlayerController player = PlayerHelper.PlayerController;
            if (scene != null && scene.Active && scene.Entry != null && scene.Entry.Report != ReportKind.None && player != null
                && (scene.Indoors || (scene.Focus - player.transform.position).sqrMagnitude < ReportRange * ReportRange))
            {
                return scene.Entry.Report;
            }
            return TrafficAccidents.IsActive ? ReportKind.Accident : ReportKind.None;
        }

        /// <summary>The player reports the crime in progress. True if there is one and the call does something.</summary>
        internal static bool ReportScene()
        {
            CrimeDirector director = Instance;
            try
            {
                if (director == null || director._scene == null)
                {
                    return false;
                }
                ReportKind kind = Reportable();
                return kind != ReportKind.None && kind != ReportKind.Accident && director._scene.Report(Time.time);
            }
            catch (Exception e)
            {
                director.Fault(e);
                return false;
            }
        }

        /// <summary>A call with nothing to report: a warning for the first two in a day, then a fine. Returns the fine (0 for a warning).</summary>
        internal static int WastedCall()
        {
            CrimeDirector director = Instance;
            if (director == null)
            {
                return 0;
            }
            int day = TimeHelper.CurrentDay;
            if (day != director._wastedDay)
            {
                director._wastedDay = day;
                director._wastedCalls = 0;
            }
            if (++director._wastedCalls < 3)
            {
                return 0;
            }
            Crime.Fine(CrimeLaw.WastedCallFine, "reallife_crime_wasted_fine");
            Law.Arrests.WastedCall(director._wastedCalls, CrimeLaw.WastedCallFine);   // from the fifth in a day, a few hours in a cell too
            return CrimeLaw.WastedCallFine;
        }
    }
}
