using System;
using System.Collections;
using System.Collections.Generic;
using BAModAPI;
using BigAmbitions.DayNightCycle;
using Helpers;
using UI;
using UI.Load;
using UI.MiniMenu;
using UI.Notification;
using UI.Overlays;
using UI.Smartphone;
using RealLifeOverhaul.GameUi;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// Travel and lodging: the one component that runs it. It reads and writes TravelState with
    /// the save, gives the hotels and the agencies their tick (once a second, and only while
    /// the player is out of doors), shows the key prompt at a hotel or agency door, takes the
    /// interaction key there, and keeps the player safe while the travel panel is up.
    ///
    /// Cost when nothing is going on: a few comparisons a frame, a distance to each of eight
    /// hotels and the city's travel agencies once a second, a handful of tests on the hour.
    ///
    /// The watchdog (four times a second while the panel is up) is what makes a stay or a trip
    /// unable to trap anyone: whatever else happens (the hospital, a car, a building, a death,
    /// a menu, a fault in this code), the panel goes, the navigation block is lifted, the
    /// game's energy regeneration is put back and the player is in the street where they stood.
    /// </summary>
    public sealed class TravelDirector : MonoBehaviour
    {
        private const NavigationBlocker Block = NavigationBlocker.PaidActivity;   // one of the game's own, used by nothing that can run at the same time
        private const float SkipPatience = 240f;                                  // real seconds a time skip may take before it is given up on

        public static IModLogger Logger;
        public static TravelDirector Instance { get; private set; }

        private bool _loaded;
        private bool _failed;
        private bool _secluded;
        private int _doorKind;              // what the door panel says: 1 reception, 2 reception and the room, 3 the agent
        private HotelSite _doorSite;
        private int _doorDay = -1;
        private bool _doorRoom;
        private int _lastHour = -1;
        private int _lastDay = -1;
        private float _nextSecond;
        private float _nextQuarter;
        private float _nextHook;
        private HotelSite _marked;
        private float _markUntil;
        private bool _skipping;

        public Hotels Hotels { get; private set; }
        public HotelStay Stay { get; private set; }
        public Holidays Holidays { get; private set; }

        /// <summary>True while the game's panel at a hotel or agency door is up, and in the frame its button ran: the mod's own door key (World/NearbyInteraction) stands aside.</summary>
        public static bool TakesInteractKey => Instance != null && Instance._door != null && Instance._door.TakesKey;

        /// <summary>The list's entries all come here; a fault in one never reaches the game's UI code.</summary>
        public Action<int> Pressed { get; private set; }

        /// <summary>The one list window every travel screen shares (reception, the room, a trip): a copy of the game's list of subway stations.</summary>
        public readonly DeskList Window = new DeskList("RealLifeOverhaul Travel", 1100f, 150f);

        private DoorPanel _door;
        private GuideMark _mark;
        private Action _onReception;
        private Action _onRoom;
        private Action _onAgent;
        private Func<string> _doorWhat;

        /// <summary>True while one of the travel screens is on the list window.</summary>
        public bool ScreenOpen => Window.IsOpen;

        private void Awake()
        {
            Instance = this;
            GameUi.WorldPanel.Logger = Logger;
            GameUi.TravelList.Logger = Logger;
            GameUi.GuideMark.Logger = Logger;
            _door = new DoorPanel(Logger, "Travel check (door panel)");
            _mark = new GuideMark();
            _onReception = _door.Press(ReceptionClicked);
            _onRoom = _door.Press(RoomClicked);
            _onAgent = _door.Press(AgentClicked);
            _doorWhat = DoorWhat;
            TravelState.Reset();
            TravelEffects.Register(Logger);   // before ModState reads the save, so a modifier stashed in it finds its type
            Hotels = new Hotels(transform, Logger);
            Stay = new HotelStay(this);
            Holidays = new Holidays(this);
            Pressed = OnPressed;
        }

        private void OnDestroy()
        {
            SaveHook.Drop(_onSave);
            try
            {
                if (_skipping)
                {
                    HotelStay.EndSleepEffects();
                }
                Seclude(false);
                Hotels.Dispose();
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
            _door?.Hide("the director went with the city");
            _mark?.Hide();
            Window.Destroy(e => Logger?.Error(e));
            TravelBridges.Reset();
            TravelState.Reset();
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void Update()
        {
            if (_failed)
            {
                return;
            }
            try
            {
                Step();
            }
            catch (Exception e)
            {
                // Once: say so, put the player back in the street, and stay off for the session.
                _failed = true;
                Logger?.Error(e);
                Logger?.Warn("Travel: switched off for this session after the error above. Any room or ticket is kept in the save.");
                try
                {
                    ToStreet();
                    HidePrompt("travel was switched off after an error");
                    _mark.Hide();
                }
                catch (Exception again)
                {
                    Logger?.Error(again);
                }
            }
        }

        private void Step()
        {
            var gm = InstanceBehavior<GameManager>.Instance;
            if (gm == null || gm.playerController == null || SaveGameManager.Current == null || LoadScene.isLoading || !ModState.Loaded)
            {
                return;
            }
            float now = Time.unscaledTime;
            if (!_loaded)
            {
                if (!Hotels.Resolve())
                {
                    return;   // the city's buildings are not in yet
                }
                Load();
            }
            if (now >= _nextHook)
            {
                // The game clears its event hub on every load, so the save hook is kept attached (as LifeEvents keeps its own).
                _nextHook = now + 2f;
                SaveHook.Keep(_onSave ?? (_onSave = OnSaveGame));   // the mod's one relay in the game's save event (Core/SaveHook): nothing is allocated while that event has not changed
            }

            if (TimeHelper.CurrentHour != _lastHour)
            {
                _lastHour = TimeHelper.CurrentHour;
                if (TimeHelper.CurrentDay != _lastDay)
                {
                    _lastDay = TimeHelper.CurrentDay;
                    Holidays.DayTick(_lastDay);
                }
                Stay.HourTick();
                Holidays.HourTick();
                TiredCheck();
            }

            if (now >= _nextQuarter)
            {
                _nextQuarter = now + 0.25f;
                Watch();
                Holidays.TickPending();
            }

            bool street = !BuildingManager.IsInsideBuilding && !_secluded;
            if (now >= _nextSecond)
            {
                _nextSecond = now + 1f;
                if (street)
                {
                    bool onFoot = !PlayerHelper.IsUsingVehicle && gm.selectedVehicle == null;
                    Vector3 player = gm.playerController.transform.position;
                    Hotels.Tick(player, onFoot && Settings.Hotels, TimeHelper.CurrentHour);
                    if (onFoot && Settings.Holidays)
                    {
                        Holidays.Tick(player);
                    }
                    else
                    {
                        Holidays.Away();
                    }
                }
                else
                {
                    Hotels.Away();
                    Holidays.Away();
                }
                UpdatePrompt(gm);
                if (_marked != null)
                {
                    if (now > _markUntil || !street || CitySpots.Flat(_marked.Desk - gm.playerController.transform.position) < 12f * 12f)
                    {
                        Unmark();
                    }
                    else
                    {
                        _mark.Show(_marked.Desk, _marked.Name);   // the game's own destination marker; two compares when nothing changed
                    }
                }
            }
        }

        // ---------------------------------------------------------------- save and load

        private void Load()
        {
            _loaded = true;
            TravelEffects.Register(Logger);
            Dictionary<string, string> data = SaveGameManager.Current.modData;
            string saved = null;
            if (data != null)
            {
                data.TryGetValue(TravelState.SaveKey, out saved);
            }
            TravelState.Read(saved);
            _lastHour = TimeHelper.CurrentHour;
            _lastDay = TimeHelper.CurrentDay;
            if (TravelState.StreakDay == 0)
            {
                TravelState.StreakDay = _lastDay;   // a save from before travel: the count begins today
            }
            Holidays.FindAgencies();
            TravelAgentDialog.Host = Holidays;
            TravelPhone.Setup();
            Logger?.Info("Travel: state loaded. room=" + (TravelState.HotelId ?? "none") + " ticket=" + (TravelState.TicketId ?? "none") + " away=" + TravelState.Away
                         + " agencies=" + Holidays.AgencyCount + " hotels=" + Hotels.Sites.Count + ".");
            if (TravelState.Away || TravelState.InRoom)
            {
                StartCoroutine(ResumeSoon());
            }
        }

        // A save made up in a room or on a trip: the panel comes back once the game has settled; if it cannot, the player is simply in the street.
        private IEnumerator ResumeSoon()
        {
            float waited = 0f;
            while (waited < 20f && (waited < 1.5f || !LifeEvents.IsSafeForEvent() || BuildingManager.IsInsideBuilding))
            {
                waited += Time.unscaledDeltaTime;
                yield return null;
            }
            try
            {
                if (TravelState.Away)
                {
                    if (Settings.Holidays)
                    {
                        Holidays.Resume();
                    }
                    else
                    {
                        Holidays.Abort();
                    }
                }
                else if (TravelState.InRoom)
                {
                    if (Settings.Hotels)
                    {
                        Stay.Resume();
                    }
                    else
                    {
                        TravelState.InRoom = false;
                    }
                }
            }
            catch (Exception e)
            {
                Fault(e);
                ToStreet();
            }
        }

        private System.Action _onSave;   // made once: Core/SaveHook compares it, it is never built again

        private void OnSaveGame()
        {
            try
            {
                GameInstance save = SaveGameManager.Current;
                if (!_loaded || save == null)
                {
                    return;
                }
                if (save.modData == null)
                {
                    save.modData = new Dictionary<string, string>();
                }
                save.modData[TravelState.SaveKey] = TravelState.Write();
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        // ---------------------------------------------------------------- the panel and the player

        /// <summary>Whether the panel may come up over the player now, and if not, why.</summary>
        public bool CanSeclude(out TestRefusal why)
        {
            why = TestRefusal.None;
            if (!_loaded || _failed || SaveGameManager.Current == null || PlayerHelper.PlayerController == null)
            {
                why = TestRefusal.NotLoaded;
            }
            else if (BuildingManager.IsInsideBuilding)
            {
                why = TestRefusal.Indoors;
            }
            else if (PlayerHelper.IsUsingVehicle || JobDirector.OnShift || !LifeEvents.IsSafeForEvent() || _skipping)
            {
                why = TestRefusal.SceneActive;
            }
            return why == TestRefusal.None;
        }

        /// <summary>Holds the player where they stand (one of the game's own navigation blocks) while the panel is up, or lets them go.</summary>
        public void Seclude(bool on)
        {
            if (on == _secluded)
            {
                return;
            }
            _secluded = on;
            PlayerController player = PlayerHelper.PlayerController;
            if (player == null)
            {
                return;
            }
            if (on)
            {
                player.ResetNavigation();
                player.SetNavigationBlocker(Block);
                HidePrompt("the player is held in a room or on a trip");
                Unmark();
            }
            else
            {
                player.UnsetNavigationBlocker(Block);
            }
        }

        // Four times a second: anything that should not be true while the panel is up ends it.
        private void Watch()
        {
            if (!_secluded)
            {
                if (Window.IsOpen && !TravelState.InRoom && !TravelState.Away && !Holidays.ShowingCancel && !Stay.AtReception)   // (reception is used from the street, in no room)
                {
                    Window.Close();
                }
                return;
            }
            bool open = Window.IsOpen;
            bool wrong = BuildingManager.IsInsideBuilding || PlayerHelper.IsUsingVehicle || EnergyHelper.goingToHospital || FuneralHelper.PlayerDead
                         || (TravelState.InRoom && !Settings.Hotels) || (TravelState.Away && !Settings.Holidays)
                         || (!open && !_skipping && !Stay.Busy && !Holidays.Busy && !Stay.PlainSleep && !Holidays.Auto);
            if (wrong)
            {
                Logger?.Info("Travel: the watchdog ended a stay or trip (indoors=" + BuildingManager.IsInsideBuilding + " vehicle=" + PlayerHelper.IsUsingVehicle
                             + " hospital=" + EnergyHelper.goingToHospital + " list=" + open + ").");
                ToStreet();
                return;
            }
            // One of the game's own menus over the room's list: the room is left (kept), as the transit list is when a menu opens.
            if (open && TravelState.InRoom && !Stay.Busy && (FullMenu.IsOpen || CityMap.IsOpen || MiniMenu.IsOpen || HudConfirm.isOpen))
            {
                Window.Close();
            }
        }

        /// <summary>The way out of everything: no panel, no block, the game's energy as it was, the player in the street. The room and any unused ticket are kept.</summary>
        public void ToStreet()
        {
            bool skipping = _skipping;
            Stay.ForceOut();
            if (TravelState.Away)
            {
                Holidays.Abort();
            }
            if (skipping)
            {
                var uis = InstanceBehavior<UIs>.Instance;
                if (uis != null && uis.timeMachine != null && uis.timeMachine.isRunning && !EnergyHelper.goingToHospital)
                {
                    uis.timeMachine.StopTimeMachine(0f);
                }
            }
            HotelStay.EndSleepEffects();
            Seclude(false);
            Holidays.ClearCancel();
            Window.Close();   // (what the screen's closing does is already done: the stay and the trip are over)
        }

        private void OnPressed(int id)
        {
            if (_failed || _skipping && id != 19)   // while time is skipped only "wake now" is listened to
            {
                return;
            }
            try
            {
                if (id >= 100)
                {
                    Holidays.Press(id);
                }
                else
                {
                    Stay.Press(id);
                }
            }
            catch (Exception e)
            {
                Fault(e);
                ToStreet();
            }
        }

        public Coroutine Run(IEnumerator routine)
        {
            return StartCoroutine(Guarded(routine));
        }

        // A routine of the stay or the trip: a fault in it ends in the street, never in a frozen panel.
        private IEnumerator Guarded(IEnumerator routine)
        {
            while (true)
            {
                object step;
                try
                {
                    if (!routine.MoveNext())
                    {
                        yield break;
                    }
                    step = routine.Current;
                }
                catch (Exception e)
                {
                    Fault(e);
                    ToStreet();
                    yield break;
                }
                yield return step;
            }
        }

        /// <summary>
        /// Skips to a time with the game's own time machine (everything in the game runs meanwhile)
        /// and returns when it has stopped, however it stopped.
        /// </summary>
        /// <param name="constant">Run the whole distance in about a second (a day of a trip), not at the machine's own pace (a night's sleep).</param>
        public IEnumerator Skip(Timestamp goal, string infoKey, bool constant = false)
        {
            var uis = InstanceBehavior<UIs>.Instance;
            if (uis == null || uis.timeMachine == null || uis.timeMachine.isRunning || goal.GetMinutesFromNow() <= 0f)
            {
                yield break;
            }
            _skipping = true;
            uis.timeMachine.StartTimeMachine(goal, false, infoKey, true, constant);
            float waited = 0f;
            yield return null;
            while (uis != null && uis.timeMachine != null && uis.timeMachine.isRunning && waited < SkipPatience)
            {
                waited += Time.unscaledDeltaTime;
                yield return null;
            }
            if (uis != null && uis.timeMachine != null && uis.timeMachine.isRunning)
            {
                Logger?.Warn("Travel: a time skip did not end within " + SkipPatience + " s and was stopped.");
                uis.timeMachine.StopTimeMachine(0f);
            }
            yield return new WaitForSecondsRealtime(0.3f);   // the machine puts the HUD back a moment after it stops
            _skipping = false;
        }

        // ---------------------------------------------------------------- at the door

        private const string ButtonReception = "reallife_travel_button_reception";
        private const string ButtonRoom = "reallife_travel_button_room";
        private const string ButtonAgent = "reallife_travel_button_agent";
        private const float DoorReachUp = 2.2f;     // metres over the door the game's panel stands

        // Once a second: the game's panel over a hotel's door or an agency's, with what is done there on the game's own keys.
        private void UpdatePrompt(GameManager gm)
        {
            int kind = 0;
            HotelSite site = null;
            if (!_secluded && !JobDirector.TakesInteractKey && !ThirdPersonCamera.IsUiOpen() && !gm.playerController.NavigationDisabled
                && !BuildingManager.IsInsideBuilding && DialogController.current == null)
            {
                site = Hotels.InReach;
                if (site != null && Settings.Hotels)
                {
                    kind = TravelState.HasRoom && TravelState.HotelId == site.Slot.Id ? 2 : 1;
                }
                else if (Holidays.AgencyInReach != null && Settings.Holidays)
                {
                    kind = 3;
                    site = null;
                }
            }
            if (kind == 0)
            {
                HidePrompt(Window.IsOpen ? "a list is open" : "the player walked away or another screen is open");
                return;
            }
            int day = TimeHelper.CurrentDay;
            bool room = TravelState.HasRoom;
            if (kind != _doorKind || !ReferenceEquals(site, _doorSite) || day != _doorDay || room != _doorRoom)
            {
                _doorKind = kind;
                _doorSite = site;
                _doorDay = day;
                _doorRoom = room;
                WriteDoor(site, kind);
            }
            Vector3 at = gm.playerController.transform.position;
            Vector3 place = kind == 3 ? Holidays.AgencyDoor : site.Desk;
            _door.Show(place, at, DoorReachUp, _doorWhat);
        }

        // For the log line, built only when the game accepts or refuses the panel.
        private string DoorWhat()
        {
            return _doorKind == 3 ? "at a travel agency's door" : "at " + (_doorSite != null ? _doorSite.Name : "a hotel") + " (" + (_doorKind == 2 ? "a room is booked here" : _doorRoom ? "a room is booked elsewhere" : "no room booked") + ")";
        }

        // The panel's words and buttons: reception on the interact key (not while a room is held), the room on the secondary one.
        private void WriteDoor(HotelSite site, int kind)
        {
            WorldPanel panel = _door.Panel;
            if (kind == 3)
            {
                panel.Lines(TravelText.T("reallife_travel_door_agency"), TravelText.T("reallife_travel_door_agency_line"), null, WorldPanel.White());
                panel.Buttons(ButtonAgent, _onAgent, true, null, null, false);
                return;
            }
            string left;
            if (kind == 2)
            {
                left = TravelText.T("reallife_travel_door_yours", "room", TravelText.T(TravelCatalogue.Room(TravelState.RoomId).NameKey));
            }
            else if (TravelState.HasRoom)
            {
                left = TravelText.T("reallife_travel_door_elsewhere", "hotel", TravelText.T(TravelCatalogue.Hotel(TravelState.HotelId).NameKey));
            }
            else
            {
                RoomType[] rooms = TravelCatalogue.Rooms(site.Slot.Tier);
                int from = int.MaxValue;
                for (int i = 0; i < rooms.Length; i++)
                {
                    from = Mathf.Min(from, HotelStay.Tonight(site.Slot, rooms[i]));
                }
                left = TravelText.T("reallife_travel_door_from", "rate", TravelText.Money(from));
            }
            panel.Lines(site.Name, left, null, WorldPanel.White());
            panel.Button(0, ButtonReception, _onReception, !TravelState.HasRoom, true);
            panel.Button(1, kind == 2 ? ButtonRoom : null, kind == 2 ? _onRoom : null, kind == 2, false);
            panel.Button(2, null, null, false, false);
            panel.Button(3, null, null, false, false);
        }

        private void HidePrompt(string why)
        {
            if (_door != null && _door.Up)
            {
                _doorKind = 0;
                _doorSite = null;
                _door.Hide(why);
            }
        }

        // The panel's three buttons, on the game's keys or a click. Each makes sure the player is still free to be served.
        private bool Free(out GameManager gm)
        {
            gm = InstanceBehavior<GameManager>.Instance;
            return gm != null && gm.playerController != null && gm.selectedVehicle == null && !PlayerHelper.IsUsingVehicle && !gm.playerController.NavigationDisabled
                   && !ThirdPersonCamera.IsUiOpen() && DialogController.current == null;
        }

        private void ReceptionClicked()
        {
            try
            {
                HotelSite site = Hotels.InReach;
                if (!Free(out GameManager gm) || site == null)
                {
                    return;
                }
                HidePrompt("reception was chosen");
                gm.playerController.ResetNavigation();
                Stay.OpenDesk(site);
            }
            catch (Exception e)
            {
                Fault(e);
            }
        }

        private void RoomClicked()
        {
            try
            {
                if (!Free(out GameManager gm) || Hotels.InReach == null)
                {
                    return;
                }
                HidePrompt("the room was chosen");
                gm.playerController.ResetNavigation();
                if (!Stay.EnterRoom(out TestRefusal why) && why != TestRefusal.None && why != TestRefusal.NothingFits)
                {
                    TravelText.Toast(NotificationType.Info, TravelText.T("reallife_travel_room_not_now"), 5f);
                }
            }
            catch (Exception e)
            {
                Fault(e);
            }
        }

        private void AgentClicked()
        {
            try
            {
                if (!Free(out GameManager gm) || Holidays.AgencyInReach == null)
                {
                    return;
                }
                BuildingRegistration agency = Holidays.AgencyInReach;
                HidePrompt("the agent was chosen");
                gm.playerController.ResetNavigation();
                Holidays.OpenAgent(agency);
            }
            catch (Exception e)
            {
                Fault(e);
            }
        }

        // ---------------------------------------------------------------- finding a bed

        public void Mark(HotelSite site, float seconds)
        {
            _marked = site;
            _markUntil = Time.unscaledTime + seconds;
        }

        private void Unmark()
        {
            if (_marked != null)
            {
                _marked = null;
                _mark.Hide();
            }
        }

        /// <summary>Marks the player's own hotel, or the nearest, and returns the sentence that says which (for the Trips page).</summary>
        public string PointToHotel()
        {
            PlayerController player = PlayerHelper.PlayerController;
            if (player == null || !Settings.Hotels)
            {
                return TravelText.T("reallife_travel_trips_no_hotel");
            }
            float distance;
            HotelSite site = TravelState.HasRoom ? Hotels.ById(TravelState.HotelId) : null;
            if (site != null)
            {
                distance = Mathf.Sqrt(CitySpots.Flat(site.Desk - player.transform.position));
            }
            else
            {
                site = Hotels.Nearest(player.transform.position, null, out distance);
            }
            if (site == null)
            {
                return TravelText.T("reallife_travel_trips_no_hotel");
            }
            Mark(site, 600f);
            RoomType room = TravelCatalogue.Rooms(site.Slot.Tier)[0];
            return TravelText.T("reallife_travel_trips_hotel", "hotel", site.Name, "metres", TravelText.Number(Mathf.RoundToInt(distance)),
                "rate", TravelText.Money(HotelStay.Tonight(site.Slot, room)), "room", TravelText.T(room.NameKey));
        }

        /// <summary>
        /// For other features: the player has nowhere to sleep (locked out, evicted, too far from
        /// home). Shows where the nearest bed is and what it costs, and marks it. One line to call:
        /// <c>TravelDirector.SuggestHotel("reallife_travel_suggest_locked_out");</c> The key's text takes
        /// {hotel}, {metres}, {rate} and {room}. Does nothing if hotels are off or the player has a room.
        /// </summary>
        public static bool SuggestHotel(string key)
        {
            TravelDirector director = Instance;
            PlayerController player = PlayerHelper.PlayerController;
            if (director == null || !director._loaded || director._failed || player == null || !Settings.Hotels || TravelState.HasRoom)
            {
                return false;
            }
            HotelSite site = director.Hotels.Nearest(player.transform.position, null, out float distance);
            if (site == null)
            {
                return false;
            }
            RoomType room = TravelCatalogue.Rooms(site.Slot.Tier)[0];
            director.Mark(site, 600f);
            TravelText.Toast(NotificationType.Info, TravelText.T(key, "hotel", site.Name, "metres", TravelText.Number(Mathf.RoundToInt(distance)),
                "rate", TravelText.Money(HotelStay.Tonight(site.Slot, room)), "room", TravelText.T(room.NameKey)), 12f);
            return true;
        }

        // On the hour: late, worn out and without a room, the player is told where the nearest bed is. Once a day each.
        private void TiredCheck()
        {
            if (!Settings.Hotels || TravelState.HasRoom || TravelState.Away || _secluded || BuildingManager.IsInsideBuilding)
            {
                return;
            }
            GameInstance save = SaveGameManager.Current;
            int hour = TimeHelper.CurrentHour;
            int day = TravelEffects.StayDay();
            bool late = hour >= 20 || hour < 5;
            if (late && save.Energy < 25f && TravelState.TiredDay != day)
            {
                if (SuggestHotel(PlayerHelper.IsUsingVehicle ? "reallife_travel_suggest_tired_driving" : "reallife_travel_suggest_tired"))
                {
                    TravelState.TiredDay = day;
                }
            }
            else if (late && save.Energy < 45f && TravelState.HintDay != day && TravelState.TiredDay != day && !HasBed())
            {
                if (SuggestHotel("reallife_travel_suggest_no_home"))
                {
                    TravelState.HintDay = day;
                }
            }
        }

        // The home feature knows best; without it, a rented apartment counts as a bed.
        private static bool HasBed()
        {
            bool? known = TravelBridges.Home.HasBed;
            if (known.HasValue)
            {
                return known.Value;
            }
            List<BuildingRegistration> all = SaveGameManager.Current.BuildingRegistrations;
            for (int i = 0; all != null && i < all.Count; i++)
            {
                if (all[i] != null && all[i].RentedByPlayer && all[i].GetBuildingType() == "ba:buildingtype_residential")
                {
                    return true;
                }
            }
            return false;
        }

        // ---------------------------------------------------------------- logging

        public void Log(string line)
        {
            Logger?.Info(line);
        }

        public void Fault(Exception e)
        {
            Logger?.Error(e);
        }

        // ---------------------------------------------------------------- the test buttons

        private bool TestReady(string test, bool featureOn)
        {
            TestRefusal why = !_loaded || _failed ? TestRefusal.NotLoaded : !featureOn ? TestRefusal.FeatureOff : TestRefusal.None;
            if (why == TestRefusal.None)
            {
                CanSeclude(out why);
            }
            if (why != TestRefusal.None)
            {
                TestRefusals.Tell(Logger, test, why);
                return false;
            }
            return true;
        }

        private void Refuse(string test, string key, string detail)
        {
            Logger?.Info(test + " test: not started (" + detail + ").");
            Notifications.Show(NotificationType.Warning, key, null, 8f);
        }

        /// <summary>Takes the cheapest room of the nearest hotel of a tier for one night (at its real price) and goes up to it.</summary>
        public void TestCheckIn(HotelTier tier)
        {
            const string test = "Hotel check-in";
            if (!TestReady(test, Settings.Hotels))
            {
                return;
            }
            if (TravelState.HasRoom)
            {
                Refuse(test, "reallife_travel_test_has_room", "a room is already booked at " + TravelState.HotelId);
                return;
            }
            HotelSite site = Hotels.Nearest(PlayerHelper.PlayerController.transform.position, tier, out float distance);
            if (site == null)
            {
                Refuse(test, "reallife_travel_test_no_hotel", "no hotel of tier " + tier + " was designated in this city");
                return;
            }
            RoomType[] rooms = TravelCatalogue.Rooms(site.Slot.Tier);
            RoomType room = HotelStay.FreeTonight(site.Slot, rooms[0]) > 0 ? rooms[0] : rooms[1];
            if (!Stay.Book(site, room, 1, false, out string failKey))
            {
                Refuse(test, failKey == "reallife_travel_desk_full" ? "reallife_travel_test_full" : "reallife_travel_test_no_money",
                    site.Slot.Id + ", " + room.Id + ", " + HotelStay.Tonight(site.Slot, room) + " a night, " + failKey);
                return;
            }
            Logger?.Info(test + " test: " + room.Id + " at " + site.Slot.Id + ", " + Mathf.RoundToInt(distance) + " m away.");
            Stay.EnterRoom(out _);
        }

        public void TestOpenRoom()
        {
            const string test = "Stay panel";
            if (!TestReady(test, Settings.Hotels))
            {
                return;
            }
            if (!TravelState.HasRoom)
            {
                Refuse(test, "reallife_travel_test_no_room", "no room is booked");
                return;
            }
            if (!Stay.EnterRoom(out TestRefusal why) && why != TestRefusal.None)
            {
                TestRefusals.Tell(Logger, test, why);
            }
        }

        /// <summary>Gives a ticket for a trip (free, economy, alone) and sets off at once; any ticket already held is replaced.</summary>
        public void TestTrip(string tripId)
        {
            const string test = "Trip";
            if (!TestReady(test, Settings.Holidays))
            {
                return;
            }
            Trip trip = TravelCatalogue.TripById(tripId);
            if (trip == null || TravelState.Away || TravelState.Package)
            {
                Refuse(test, "reallife_travel_test_away", "a trip is already under way");
                return;
            }
            if (trip.Kind == TripKind.Real && TravelState.HasRoom && Hotels.ById(TravelCatalogue.HamptonsSlot) != null)
            {
                Refuse(test, "reallife_travel_test_has_room", "the Hamptons weekend needs the room slot, and a room is booked at " + TravelState.HotelId);
                return;
            }
            TravelState.ClearTicket();
            TravelState.TicketId = trip.Id;
            TravelState.TicketDay = TimeHelper.CurrentDay;
            if (!Holidays.Depart(out string why, true))
            {
                TravelState.ClearTicket();
                Logger?.Info(test + " test: not started (" + why + ").");
                TravelText.Toast(NotificationType.Warning, why ?? TravelText.T("reallife_travel_depart_busy"), 8f);
            }
        }

        public void TestReturn()
        {
            const string test = "Return from trip";
            if (!_loaded || _failed)
            {
                TestRefusals.Tell(Logger, test, TestRefusal.NotLoaded);
            }
            else if (!Holidays.ReturnNow())
            {
                Refuse(test, "reallife_travel_test_not_away", "no card trip is under way (the Hamptons weekend ends at checkout)");
            }
        }

        /// <summary>Ends everything: out of the room or the trip, no room, no ticket, no marker. Stamps and past trips are kept.</summary>
        public void TestClear()
        {
            if (!_loaded)
            {
                TestRefusals.Tell(Logger, "Travel clear", TestRefusal.NotLoaded);
                return;
            }
            ToStreet();
            TravelState.ClearRoom();
            TravelState.ClearTicket();
            TravelState.ClearAway();
            TravelState.BurntOut = false;
            ModState.RemoveModifier(TravelEffects.Burnout);
            Unmark();
            Logger?.Info("Travel clear test: no room, no ticket, nobody away.");
            Notifications.Show(NotificationType.Info, "reallife_travel_test_cleared", null, 6f);
        }
    }
}
