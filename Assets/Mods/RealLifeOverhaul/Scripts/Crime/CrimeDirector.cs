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
    /// Decides when a crime happens near the player and which, and looks after the one scene
    /// that is running: there is never more than one, never while a traffic accident or a
    /// police pursuit has the street, never a street scene indoors or an indoor one outside,
    /// and never one the pool cannot cast. Kinds are dealt from CrimeCatalogue like cards
    /// (CrimeDeck), so nothing repeats until everything that fits has been seen.
    /// It also watches the stolen value of the player's own shop while they are in it (once a
    /// second) so that a theft the game books can be acted out, hands the hourly roll to
    /// OffscreenCrimes, and answers the 911 contact.
    /// Idle, this is two time comparisons per frame. With a scene running it places the marker
    /// each frame and ticks the scene four times a second. A scene that throws is ended and
    /// logged; after three faults in a session the whole system switches itself off.
    /// </summary>
    public class CrimeDirector : MonoBehaviour
    {
        private static readonly FeatureGuard FrameGuard = Guard.For("crime", "CrimeDirector", typeof(CrimeDirector));   // its Update, LateUpdate and FixedUpdate (Core/Guard)

        internal const int CashLeast = 60;   // a crime against the player needs this much on them, at the start's prices
        private const float CheckInterval = 20f;
        private const float TickInterval = 0.25f;
        private const float WatchInterval = 1f;
        private const float MinDistance = 35f;
        private const float MaxDistance = 80f;
        private const float ReportRange = 160f;       // a call about something further away than this is a call about nothing
        private const int MaxFaults = 3;
        private const int PlayerCrimeRestDays = 2;    // in-game days between crimes against the player
        private const float ShopTheftMinimum = 0.5f;  // dollars: a smaller rise in stolen value is rounding, not a theft

        public static IModLogger Logger;
        public static CrimeDirector Instance { get; private set; }

        /// <summary>True while a crime scene is running. Only one incident of any kind runs at a time.</summary>
        public static bool IsActive => Instance != null && Instance._scene != null;

        /// <summary>
        /// True while a crime with a victim in the street is going on (an assault, a mugging, a snatch: not one done to the
        /// player), and where (Wiring/StandingLinks: walking away from it in front of others is noticed). A few field reads.
        /// </summary>
        public static bool Victim(out Vector3 where)
        {
            ICrimeScene scene = Instance != null ? Instance._scene : null;
            where = scene != null ? scene.Focus : Vector3.zero;
            return scene != null && scene.Active && scene.ShowsMarker && !scene.Indoors && (scene.Id == "assault" || scene.Id == "mugging" || scene.Id == "snatch");
        }

        /// <summary>The patrol car scenes share: the one that collects an arrested suspect, or chases a car.</summary>
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
        private readonly RealLifeOverhaul.GameUi.GuideMark _mark = new RealLifeOverhaul.GameUi.GuideMark();
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

        // Calls to 911 about nothing.
        private int _wastedDay = -1;
        private int _wastedCalls;

        private void Awake()
        {
            Instance = this;
            Crime.Logger = Logger;
            _nextCheck = Time.time + CheckInterval;
            _patrol = new ResponseUnit(gameObject);
            _suspect = new ResponseUnit(gameObject);
            _scenes = CrimeScenes.Build();
            _offscreen = new OffscreenCrimes();
            gameObject.AddComponent<CuffPose>();   // idle (switched off) until somebody is arrested
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

        // Ended first, so a fault cannot repeat every frame; and after a few of them, off for the session.
        private void Fault(Exception e)
        {
            Logger?.Error(e);
            EndScene();
            if (Guard.StandsDown(e, "crime in the street"))
            {
                enabled = false;   // (a game that lacks something it names would fail at every look)
            }
            else if (++_faults >= MaxFaults)
            {
                Logger?.Warn("Crime: switched off for this session after repeated errors (see above).");
                enabled = false;
            }
        }

        // ---------------------------------------------------------------- the running scene

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
                _mark.Show(scene.Focus, _markerLabel);   // the game's destination marker; another feature that has it keeps it
            }
            else
            {
                _mark.Hide();
            }

            Vector3 at = player.transform.position;
            // The same keys the mod's "enter with a key" uses: F, or E while the game is not offering an interaction of its own.
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

        /// <summary>Ends whatever scene is running, at once and without a message. For an incident that takes precedence.</summary>
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

        // ---------------------------------------------------------------- dealing

        private static bool CanStage()
        {
            return ModState.Loaded && ActorPool.Instance != null && PlayerHelper.PlayerController != null
                   && !TrafficAccidents.IsActive && !PoliceAttention.IsBusy && LifeEvents.IsSafeForEvent()
                   && !Wiring.Scenes.AnyBut(Wiring.Scene.Crime);   // a fire, a street event or a rally has the street (Wiring/Scenes)
        }

        private static CrimePlace CurrentPlace()
        {
            if (BuildingManager.IsInsideBuilding)
            {
                return ShopFloor.IsOwnShop ? CrimePlace.OwnShop : ShopFloor.IsOtherShop ? CrimePlace.OtherShop : CrimePlace.None;
            }
            return TrafficTools.Street && !UndergroundParkingManager.IsInsideParking ? CrimePlace.Street : CrimePlace.None;
        }

        // Shops are small and the player is in them briefly: a scene there is rarer than on the street.
        private static float PlaceShare()
        {
            return BuildingManager.IsInsideBuilding ? 0.5f : 1f;
        }

        private CrimeNeed CurrentNeeds(bool forced)
        {
            CrimeNeed have = CrimeNeed.None;
            if (Crime.IsNight || forced) have |= CrimeNeed.Night;
            if (!PlayerHelper.IsUsingVehicle) have |= CrimeNeed.OnFoot;
            if (Settings.Police) have |= CrimeNeed.PatrolCars;
            if (TrafficTools.Ready) have |= CrimeNeed.Traffic;
            if (Settings.Ragdolls) have |= CrimeNeed.Ragdolls;
            if (Crime.Cash >= Prices.At(CashLeast)) have |= CrimeNeed.Cash;   // (at today's prices)
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
                _unstageable.Add(entry.Id);   // stays "seen" for the other attempts, so a different kind is drawn
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
            // A crime against the player happens where the player is (its scene brings the thief up from a few
            // metres behind); every other street crime is staged on a pavement some way off.
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

        /// <summary>
        /// Options-menu test buttons: stages a crime of one family right away, whatever the time
        /// of day and whether or not crimes against the player are switched on. Whatever is
        /// running gives way. Says so if nothing of that family can be staged here.
        /// </summary>
        public void StageNow(int family)
        {
            try
            {
                CrimePlace place = CurrentPlace();
                bool staged = false;
                // What stops any of them, said by name; a place where this family cannot play keeps its own sentence below.
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
                            // A test says what it staged: several of these scenes show nothing for the first few seconds.
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
                        _lastId = null;   // the only one that fits may be the one just shown: allow it next time
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

        // ---------------------------------------------------------------- once a second, once an hour

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

            // Thefts the game books in the shop the player is standing in.
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
                _pendingTheft = 0f;   // the day's books have closed (or a thief was stopped): nothing of yesterday's is acted out
            }
            else if (cost > _watchedCost + ShopTheftMinimum)
            {
                _pendingTheft = cost - _watchedCost;
                _pendingAt = now + Random.Range(3f, 25f);
            }
            _watchedCost = cost;   // (it also drops: at midnight, and when a thief is stopped)
            if (_pendingTheft > 0f && now >= _pendingAt && CanStage())
            {
                float amount = _pendingTheft;
                _pendingTheft = 0f;
                Begin(CrimeCatalogue.Find("shop_theft_own"), CrimePlace.OwnShop, now, false, amount);
            }
        }

        /// <summary>Options-menu test button for the things that happen out of sight: each press stages the next kind.</summary>
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

        // ---------------------------------------------------------------- 911

        /// <summary>What a 911 call made right now would be about; None if nothing is going on within reach.</summary>
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
                return director != null && director._scene != null && Reportable() != ReportKind.None
                       && Reportable() != ReportKind.Accident && director._scene.Report(Time.time);
            }
            catch (Exception e)
            {
                director.Fault(e);
                return false;
            }
        }

        /// <summary>
        /// A call with nothing to report. The first two in a day get a warning; from the third
        /// there is a fine. Returns the fine charged (0 for a warning).
        /// </summary>
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
            Law.Arrests.WastedCall(director._wastedCalls, CrimeLaw.WastedCallFine);   // from the fifth in a day it is a few hours in a cell as well
            return CrimeLaw.WastedCallFine;
        }
    }
}
