using System;
using System.Collections.Generic;
using System.Globalization;
using BAModAPI;
using Entities;
using GleyTrafficSystem;
using Helpers;
using Localizor;
using UI;
using UI.Load;
using UI.Notification;
using UI.Smartphone.Apps.Contacts;
using UnityEngine;
using Random = UnityEngine.Random;

namespace RealLifeOverhaul.Events
{
    /// <summary>
    /// City events: fires and the fire service, weather that matters, and street events.
    ///
    /// ONE LARGE SCENE AT A TIME, across the whole mod. This director runs at most one scene (a
    /// fire or a street event) and starts none while a traffic accident, a crime scene or police
    /// business with the player is on; the others ask <see cref="IsActive"/> before they start
    /// one of theirs. It owns what its scenes share, all made once and reused: the event pool of
    /// eight people and its props, a cordon of its own, the fire effects, the hose, the fire
    /// engine, a patrol car, and two roles for officers from the accident pool.
    ///
    /// WHAT IT COSTS. With nothing on: one comparison a frame, and once a second the weather (two
    /// flags and the hour), the calendar (on the hour) and, every twenty seconds, a roll for a
    /// fire. With a scene on: the marker per frame (no allocation), the scene four times a second,
    /// most of it once a second. People: 8 of its own and at most 2 officers. Vehicles: at most 3
    /// (engine, patrol car, the burning car). Particles: 418 for a fire, 240 for fireworks, never
    /// both. Nothing is saved but one line in modData (the calendar's number, what has been
    /// announced, and a business a fire closed so that it is reopened); a save made with a scene
    /// on holds nothing of it. Any fault clears the street, logs once and switches city events
    /// off for the session.
    /// </summary>
    public sealed class EventDirector : MonoBehaviour
    {
        private const string StateKey = "reallife:events";
        private const string BulletinId = "reallife-bulletin";
        private const float RollInterval = 20f;

        public static IModLogger Logger;
        public static EventDirector Instance { get; private set; }

        /// <summary>True while a fire or a street event is on: accidents, crimes and police scenes should not start.</summary>
        public static bool IsActive => Instance != null && Instance._scene != null;

        /// <summary>True while a street event (never a fire) is on: where it is and the locale key of its name. For the city map (World/MapLinks).</summary>
        public static bool StreetEvent(out Vector3 at, out string nameKey)
        {
            EventDirector director = Instance;
            bool on = director != null && director._scene != null && director._scene == director._street;
            at = on ? director._scene.Focus : default;
            nameKey = on ? director._scene.Label : null;
            return on;
        }

        /// <summary>True while something is burning that a 911 call can bring the engine to.</summary>
        public static bool FireBurning => Instance != null && Instance._scene != null && Instance._scene == Instance._fire;

        internal EventProps Props { get; private set; }
        internal EventPeople People { get; private set; }
        internal SceneCordon Cordon { get; private set; }
        internal FireEffects Effects { get; private set; }
        internal Fireworks Sky { get; private set; }
        internal Hose Hose { get; private set; }
        internal HoseHold Hold { get; private set; }
        internal FireEngine Engine { get; private set; }
        internal ResponseUnit Patrol { get; private set; }
        internal ResponseUnit Wreck { get; private set; }
        internal ActorRole[] Officers { get; } = { new ActorRole(), new ActorRole() };

        private readonly WeatherEffects _weather = new WeatherEffects();
        private FireScene _fire;
        private StreetEventScene _street;
        private IEventScene _scene;
        private string _label;
        private readonly RealLifeOverhaul.GameUi.GuideMark _mark = new RealLifeOverhaul.GameUi.GuideMark();
        private string _prompt;
        private float _markerUntil;
        private float _nextTick, _nextSecond, _nextRoll, _nextFlicker, _nextWarm, _nextHook;
        private bool _failed;
        private bool _stateLoaded;
        private int _lastHour = -1;
        private int _announcedDay, _toldDay, _doneDay;
        private int _stageFailures;
        private Contact _bulletin;
        private Sprite _bulletinIcon;

        // A wreck another feature asked to have set alight (CrashFire): flames only, no engine.
        private VehicleComponent _crashCar;
        private float _crashUntil;

        private void Awake()
        {
            Instance = this;
            Props = new EventProps(transform);
            People = new EventPeople(gameObject, Props);
            Cordon = new SceneCordon(transform);
            Effects = new FireEffects(transform);
            Sky = new Fireworks(transform);
            Hose = new Hose(transform);
            Hold = gameObject.AddComponent<HoseHold>();
            Engine = new FireEngine(gameObject);
            Patrol = new ResponseUnit(gameObject);
            Wreck = new ResponseUnit(gameObject);
            _fire = new FireScene(this);
            _street = new StreetEventScene(this);
            _nextRoll = Time.time + 60f;   // the city settles first
        }

        private void OnDestroy()
        {
            try
            {
                EndScene(false, "the city is unloading");
                StopCrashFire();
                _weather.Restore();
                Weather.Reset();
                Sky.Dispose();
                FireOutfit.Reset();
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
            SaveHook.Drop(_onSave);
            FireDamage.ClosedKey = null;
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
            float now = Time.time;
            try
            {
                if (_scene != null)
                {
                    RunScene(now);
                }
                if (now >= _nextSecond)
                {
                    _nextSecond = now + 1f;
                    Second(now);
                }
            }
            catch (Exception e)
            {
                Fail(e);
            }
        }

        // ---------------------------------------------------------------- a scene that is on

        private void RunScene(float now)
        {
            if (now < _markerUntil)
            {
                _mark.Show(_scene.Focus, _label);
            }
            if (Effects.Burning && now >= _nextFlicker)
            {
                _nextFlicker = now + Random.Range(0.05f, 0.13f);
                Effects.Flicker();
            }
            PlayerController player = PlayerHelper.PlayerController;
            if (_prompt != null && player != null && JobKeys.InteractDown())   // (as every other prompt: E only while the game shows no interaction of its own)
            {
                _scene.Interact(now, player.transform.position);
                _prompt = null;
            }
            if (now < _nextTick)
            {
                return;
            }
            _nextTick = now + 0.25f;
            UIs uis = InstanceBehavior<UIs>.Instance;
            if (player == null || uis == null || !ModState.Loaded || LoadScene.isLoading || BuildingManager.IsInsideBuilding || uis.timeMachine.isRunning || !TrafficTools.Ready)
            {
                EndScene(false, "the street was unloaded (indoors, a time skip or loading)");
                return;
            }
            if (uis.gameSpeed.Paused)
            {
                return;
            }
            Vector3 at = player.transform.position;
            if (CitySpots.Flat(at - _scene.Focus) > _scene.Reach * _scene.Reach)
            {
                EndScene(true, "the player left it behind");
                return;
            }
            if (!_scene.Tick(now, at))
            {
                EndScene(true, "finished");
                return;
            }
            if (_markerUntil > 0f && now >= _markerUntil)
            {
                _markerUntil = 0f;   // hidden once: the marker is shared, and another scene may want it later
                _mark.Hide();
            }
            string prompt = _scene.Prompt(at);
            if (prompt != _prompt)
            {
                _prompt = prompt;
                if (prompt != null)
                {
                    Crime.ShowKey(NotificationType.Info, prompt, KeyAction.Interact, 6f);
                }
            }
        }

        private void Begin(IEventScene scene, float markerSeconds)
        {
            SessionWatch.Mark("events: a fire or street event starting");
            _scene = scene;
            if (ReferenceEquals(scene, _fire))
            {
                Wiring.Paper.Print("reallife_news_story_fire");   // tomorrow's City Ledger (Scripts/Social)
            }
            _label = scene.Label.GetLocalization();
            _prompt = null;
            _markerUntil = Time.time + markerSeconds;
            _nextTick = 0f;
            StopCrashFire();
        }

        private void EndScene(bool trafficRunning, string why)
        {
            if (_scene == null)
            {
                return;
            }
            IEventScene scene = _scene;
            _scene = null;
            _prompt = null;
            try
            {
                scene.End(trafficRunning && TrafficTools.Ready);
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
            SafetyNet();
            _mark.Hide();
            int reopened = Cordon.Recheck();
            Logger?.Info("City events: scene cleared (" + why + ")" + (reopened > 0 ? "; " + reopened + " road point(s) were still closed and are open again." : "."));
        }

        // Whatever a scene's own End missed (it faulted part of the way): everything shared is put away regardless.
        private void SafetyNet()
        {
            try
            {
                Effects.StopAll();
                Sky.StopAll();
                Hose.Take();
                Hold.End();
                People.ReleaseAll();
                for (int i = 0; i < Officers.Length; i++)
                {
                    Officers[i].Exit();
                }
                bool traffic = TrafficTools.Ready;
                if (Engine.Out)
                {
                    Engine.Release(traffic, !traffic);
                }
                if (Patrol.InUse)
                {
                    if (Patrol.Car != null)
                    {
                        SceneObstacles.Remove(Patrol.Car);
                    }
                    Patrol.Release(traffic, !traffic, true);
                }
                if (Wreck.InUse)
                {
                    if (Wreck.Car != null)
                    {
                        SceneObstacles.Remove(Wreck.Car);
                    }
                    Wreck.Release(traffic, true);
                }
                Cordon.Open();
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        /// <summary>Ends whatever fire or street event is on, at once: for a feature that must have the street.</summary>
        public static void EndActive()
        {
            if (Instance != null)
            {
                Instance.EndScene(true, "another feature took the street");
            }
        }

        /// <summary>
        /// The player rang 911. True if there is a fire and the call did something (the engine is sent
        /// at once): the phone then says so. The one-line hook is in Phone/PhoneContacts.
        /// </summary>
        public static bool Report911()
        {
            EventDirector director = Instance;
            try
            {
                return director != null && director._scene != null && director._scene.Report(Time.time);
            }
            catch (Exception e)
            {
                director.Fail(e);
                return false;
            }
        }

        /// <summary>
        /// For the accident director: sets a wreck of its own alight for so many seconds (flames,
        /// black smoke and firelight on its bonnet, dying down by themselves; the accident's own
        /// crews attend, no engine is sent). Does nothing while a fire or a street event is on.
        /// </summary>
        public static void CrashFire(VehicleComponent wreck, float seconds = 50f)
        {
            EventDirector director = Instance;
            if (director == null || director._failed || director._scene != null || wreck == null || !Settings.CityFires || !director.Effects.Ready())
            {
                return;
            }
            director._crashCar = wreck;
            director._crashUntil = Time.time + seconds;
            Vector3 bonnet = wreck.transform.TransformPoint(new Vector3(0f, 1f, wreck.length * 0.33f));
            director.Effects.Flame(0, bonnet, 0.9f);
            director.Effects.Smoke(0, bonnet + Vector3.up * 0.4f, false, 14f);
            director.Effects.Glow(bonnet + Vector3.up * 0.5f, 0.9f);
        }

        private void StopCrashFire()
        {
            if ((object)_crashCar != null)
            {
                _crashCar = null;
                Effects.StopAll();
            }
        }

        // ---------------------------------------------------------------- once a second

        private void Second(float now)
        {
            if (!ModState.Loaded || SaveGameManager.Current == null)
            {
                if (_stateLoaded)
                {
                    // The save is going: what the weather changed is put back, so that it is applied afresh to the next one.
                    _stateLoaded = false;
                    _weather.Restore();
                    Weather.Reset();
                }
                return;
            }
            if (!_stateLoaded)
            {
                LoadState();
            }
            if (now >= _nextHook)
            {
                _nextHook = now + 2f;   // the game clears its event hub on every load
                SaveHook.Keep(_onSave ?? (_onSave = OnSaveGame));   // the mod's one relay in the game's save event (Core/SaveHook): nothing is allocated while that event has not changed
            }
            _weather.Tick(this);

            if ((object)_crashCar != null)
            {
                float left = _crashUntil - now;
                if (_crashCar == null || !_crashCar.gameObject.activeInHierarchy || left <= 0f)
                {
                    StopCrashFire();
                }
                else if (left < 15f)
                {
                    Effects.SetFlame(0, left / 15f);
                    Effects.Glow(_crashCar.transform.position + Vector3.up * 1.5f, left / 15f);
                }
            }

            int hour = TimeHelper.CurrentHour;
            if (hour != _lastHour)
            {
                _lastHour = hour;
                Hourly(hour);
            }

            // The pool is filled a person at a time, and only once something needs it.
            PlayerController player = PlayerHelper.PlayerController;
            bool eventDue = Settings.StreetEvents && Due(hour, out _);
            if (player != null && now >= _nextWarm && (_scene != null || eventDue) && !BuildingManager.IsInsideBuilding)
            {
                _nextWarm = now + 2f;
                People.Warm(player.transform.position);
            }
            if (_scene == null && now >= _nextRoll)
            {
                _nextRoll = now + RollInterval;
                Roll(now, hour, player);
            }
        }

        // An event is on the calendar for this hour and has not been held today.
        private bool Due(int hour, out PlannedEvent plan)
        {
            int day = TimeHelper.CurrentDay;
            plan = EventCalendar.For(day);
            return plan.Kind != EventKind.None && _doneDay != day && hour >= plan.From && hour < plan.To - 1;
        }

        private static bool CanStage()
        {
            return TrafficTools.Ready && !BuildingManager.IsInsideBuilding && LifeEvents.IsSafeForEvent()
                   && !Wiring.Scenes.AnyBut(Wiring.Scene.Event) && !PoliceAttention.IsBusy;   // no other street scene is on (Wiring/Scenes)
        }

        private void Roll(float now, int hour, PlayerController player)
        {
            if (player == null || !CanStage())
            {
                return;
            }
            Vector3 at = player.transform.position;
            if (Settings.StreetEvents && Due(hour, out PlannedEvent plan) && !Weather.TakeCover)
            {
                if (People.Count < CityPeople.MaxPeople || !ClipLibrary.Ready || !PropLibrary.Ready)
                {
                    return;   // its people are still being made, a couple of seconds each
                }
                if (!CitySpots.PavementRing(at, 55f, 110f, true, out Vector3 place))
                {
                    return;   // nowhere out of view just now: looked for again in twenty seconds
                }
                if (_street.Stage(plan.Kind, place, false, plan.To, now))
                {
                    _doneDay = TimeHelper.CurrentDay;
                    _stageFailures = 0;
                    Begin(_street, 50f);
                    Notifications.Show(NotificationType.Info, EventCalendar.Today(plan.Kind), new Dictionary<string, string>(2) { { "from", Clock(plan.From) }, { "to", Clock(plan.To) } }, 9f);
                }
                else if (_street.Costly && ++_stageFailures >= 3)
                {
                    // A try that got as far as closing the road is not cheap, and moves traffic: three of those and it is not held today.
                    _doneDay = TimeHelper.CurrentDay;
                    _stageFailures = 0;
                    Logger?.Info("Street event: " + plan.Kind + " could not be fitted on three streets (" + _street.Refusal + "); it is not held today.");
                }
                return;
            }
            float chance = Settings.FireFrequency * 0.004f * (Weather.Spell == Spell.HeatWave ? 1.4f : Weather.Sky == Events.Sky.Rain || Weather.Sky == Events.Sky.Storm ? 0.6f : 1f);
            if (!Settings.CityFires || Random.value >= chance)
            {
                return;
            }
            float pick = Random.value;
            FireKind kind = pick < 0.4f ? FireKind.Car : pick < 0.72f ? FireKind.Bin : FireKind.Building;
            Vector3 near = at;
            if (kind != FireKind.Car && !CitySpots.PavementRing(at, 30f, 80f, true, out near))
            {
                return;
            }
            if (_fire.Stage(kind, at, near, false, now))
            {
                Begin(_fire, 600f);
            }
            else
            {
                Logger?.Info("Fire: a " + kind + " fire was rolled but not staged (" + _fire.Refusal + ").");
            }
        }

        // ---------------------------------------------------------------- once a game hour

        private void Hourly(int hour)
        {
            FireDamage.Hourly();
            if (!Settings.StreetEvents)
            {
                return;
            }
            int day = TimeHelper.CurrentDay;
            if (hour >= 7 && _toldDay != day)
            {
                _toldDay = day;
                PlannedEvent today = EventCalendar.For(day);
                if (today.Kind != EventKind.None)
                {
                    Tell(EventCalendar.Today(today.Kind), today);
                }
            }
            if (hour >= 18 && _announcedDay != day)
            {
                _announcedDay = day;
                PlannedEvent tomorrow = EventCalendar.For(day + 1);
                if (tomorrow.Kind != EventKind.None)
                {
                    Tell(EventCalendar.Tomorrow(tomorrow.Kind), tomorrow);
                }
            }
        }

        private void Tell(string key, PlannedEvent plan)
        {
            var data = new Dictionary<string, string>(2) { { "from", Clock(plan.From) }, { "to", Clock(plan.To) } };
            Notifications.Show(NotificationType.Info, key, data, 10f);
            Bulletin(key, new Dictionary<string, string>(data));   // the game keeps hold of each dictionary it is given
        }

        private static string Clock(int hour)
        {
            return (hour % 24).ToString("00", CultureInfo.InvariantCulture) + ":00";
        }

        /// <summary>
        /// A line on the phone: the City Bulletin contact carries the forecast, what is on today and
        /// what is on tomorrow (the game keeps a contact's last twenty messages). Nothing if the
        /// phone contacts are switched off.
        /// </summary>
        internal void Bulletin(string key, Dictionary<string, string> data)
        {
            if (!Settings.PhoneContacts)
            {
                return;
            }
            try
            {
                if (_bulletin == null)
                {
                    AddBulletinIcon();
                    _bulletin = Contact.GetContact(BulletinId, ContactCategoryName.General, "reallife_contact_bulletin_desc", null, false, true);
                }
                _bulletin.SendMessage(new TextMessage(key, data), notify: false);
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        // The game looks a contact's icon up by sprite name; a contact without one must not be left to chance.
        private void AddBulletinIcon()
        {
            var references = InstanceBehavior<GlobalReferences>.Instance;
            if (references == null || references.contactIcons == null)
            {
                return;
            }
            foreach (Sprite existing in references.contactIcons)
            {
                if (existing != null && existing.name == BulletinId)
                {
                    return;
                }
            }
            if (_bulletinIcon == null)
            {
                const int size = 64;
                var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
                var pixels = new Color[size * size];
                var colour = new Color(0.16f, 0.42f, 0.72f);
                float radius = size / 2f - 2f;
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        float distance = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(size / 2f, size / 2f));
                        Color pixel = Mathf.Abs(y - size / 2) < 4 && Mathf.Abs(x - size / 2) < 16 || Mathf.Abs(y - size / 2 - 12) < 3 && Mathf.Abs(x - size / 2) < 16
                                      || Mathf.Abs(y - size / 2 + 12) < 3 && Mathf.Abs(x - size / 2) < 16 ? Color.white : colour;
                        pixel.a = Mathf.Clamp01(radius - distance);
                        pixels[y * size + x] = pixel;
                    }
                }
                texture.SetPixels(pixels);
                texture.Apply();
                _bulletinIcon = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
                _bulletinIcon.name = BulletinId;
            }
            Sprite[] old = references.contactIcons;
            var icons = new Sprite[old.Length + 1];
            old.CopyTo(icons, 0);
            icons[old.Length] = _bulletinIcon;
            references.contactIcons = icons;
        }

        // ---------------------------------------------------------------- the save: one line of plain text in modData

        private void LoadState()
        {
            _stateLoaded = true;
            _bulletin = null;
            _announcedDay = _toldDay = _doneDay = 0;
            _weather.LastForecastDay = 0;
            FireDamage.ClosedKey = null;
            FireDamage.ReopenAt = 0f;
            int salt = 0;
            var save = SaveGameManager.Current;
            if (save.modData != null && save.modData.TryGetValue(StateKey, out string text) && !string.IsNullOrEmpty(text))
            {
                string[] parts = text.Split(';');
                if (parts.Length >= 7)
                {
                    int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out salt);
                    int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out _announcedDay);
                    int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out _toldDay);
                    int.TryParse(parts[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out _doneDay);
                    int.TryParse(parts[4], NumberStyles.Integer, CultureInfo.InvariantCulture, out int forecast);
                    _weather.LastForecastDay = forecast;
                    if (parts[5].Length > 0 && float.TryParse(parts[6], NumberStyles.Float, CultureInfo.InvariantCulture, out float reopen))
                    {
                        FireDamage.ClosedKey = parts[5];
                        FireDamage.ReopenAt = reopen;
                    }
                }
            }
            EventCalendar.Salt = salt != 0 ? salt : Random.Range(1, int.MaxValue);
            _lastHour = -1;
            PlannedEvent today = EventCalendar.For(TimeHelper.CurrentDay);
            Logger?.Info($"City events: state loaded; day {TimeHelper.CurrentDay} of a {EventCalendar.DaysPerYear}-day year ({Weather.SeasonOf(TimeHelper.CurrentDay)}), today: "
                         + (today.Kind == EventKind.None ? "nothing on" : today.Kind + " " + today.From + ":00 to " + today.To + ":00")
                         + (FireDamage.ClosedKey != null ? "; a business is closed after a fire and is reopened by the mod." : "."));
        }

        private System.Action _onSave;   // made once: Core/SaveHook compares it, it is never built again

        private void OnSaveGame()
        {
            try
            {
                var save = SaveGameManager.Current;
                if (!_stateLoaded || save == null)
                {
                    return;
                }
                if (save.modData == null)
                {
                    save.modData = new Dictionary<string, string>();
                }
                save.modData[StateKey] = string.Join(";", EventCalendar.Salt.ToString(CultureInfo.InvariantCulture), _announcedDay.ToString(CultureInfo.InvariantCulture),
                    _toldDay.ToString(CultureInfo.InvariantCulture), _doneDay.ToString(CultureInfo.InvariantCulture), _weather.LastForecastDay.ToString(CultureInfo.InvariantCulture),
                    FireDamage.ClosedKey ?? string.Empty, FireDamage.ReopenAt.ToString(CultureInfo.InvariantCulture));
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        private void Fail(Exception error)
        {
            _failed = true;
            Logger?.Error(error);
            Logger?.Warn("City events: switched off for this session after the error above; the street has been cleared and the weather's effects put back.");
            try
            {
                if (_scene != null)
                {
                    EndScene(true, "a fault");
                }
                else
                {
                    SafetyNet();
                }
                StopCrashFire();
                _weather.Restore();
                Weather.Reset();
                // Nothing here runs again this session: a business a fire closed is reopened now rather than never.
                if (FireDamage.ClosedKey != null)
                {
                    FireDamage.ReopenAt = 0f;
                    FireDamage.Hourly();
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        // ---------------------------------------------------------------- test buttons

        private bool TestReady(string test, out PlayerController player)
        {
            player = PlayerHelper.PlayerController;
            TestRefusal why = _failed ? TestRefusal.FeatureOff : TestRefusals.Street();
            if (why == TestRefusal.None && (TrafficAccidents.IsActive || CrimeDirector.IsActive))
            {
                why = TestRefusal.SceneActive;
            }
            if (why == TestRefusal.None && PoliceAttention.IsBusy)
            {
                why = TestRefusal.PoliceBusy;
            }
            if (why == TestRefusal.None && (!ClipLibrary.Ready || !PropLibrary.Ready))
            {
                why = TestRefusal.NotLoaded;
            }
            if (why != TestRefusal.None)
            {
                TestRefusals.Tell(Logger, test, why, 0, _failed ? "city events faulted earlier this session" : null);
                return false;
            }
            EndScene(true, "a test button");
            People.WarmAll(player.transform.position);
            return true;
        }

        private static Vector3 Ahead(PlayerController player, float metres)
        {
            Camera camera = GameManager.GetMainCamera();
            Vector3 ahead = camera != null ? camera.transform.forward : player.transform.forward;
            ahead.y = 0f;
            ahead = ahead.sqrMagnitude > 0.01f ? ahead.normalized : Vector3.forward;
            return player.transform.position + ahead * metres;
        }

        /// <summary>Options-page test: stages a fire of a kind near the player, now.</summary>
        public void TestFire(FireKind kind)
        {
            try
            {
                if (!TestReady("Fire", out PlayerController player))
                {
                    return;
                }
                if (_fire.Stage(kind, player.transform.position, Ahead(player, 14f), true, Time.time))
                {
                    Begin(_fire, 600f);
                    return;
                }
                EndSceneQuietly();
                TestRefusals.Tell(Logger, kind + " fire", _fire.Why, _fire.WhyMetres, _fire.Refusal);
            }
            catch (Exception e)
            {
                Fail(e);
            }
        }

        /// <summary>Options-page test: holds a street event of a kind on the road ahead of the player, now.</summary>
        public void TestEvent(EventKind kind)
        {
            try
            {
                if (!TestReady("Street event", out PlayerController player))
                {
                    return;
                }
                for (int attempt = 0; attempt < 3; attempt++)
                {
                    if (_street.Stage(kind, Ahead(player, 16f + attempt * 12f), true, 24, Time.time))
                    {
                        Begin(_street, 50f);
                        Notifications.Show(NotificationType.Info, EventCalendar.Today(kind), new Dictionary<string, string>(2) { { "from", Clock(TimeHelper.CurrentHour) }, { "to", Clock(TimeHelper.CurrentHour + 3) } }, 8f);
                        return;
                    }
                }
                EndSceneQuietly();
                TestRefusals.Tell(Logger, kind + " street event", _street.Refusal != null && _street.Refusal.StartsWith("no plain", StringComparison.Ordinal) ? TestRefusal.NoRoad : TestRefusal.NothingFits,
                    30, _street.Refusal);
            }
            catch (Exception e)
            {
                Fail(e);
            }
        }

        /// <summary>Options-page test: holds the weather's effects (not its looks) as given until cleared.</summary>
        public void TestWeather(Sky sky, Spell spell)
        {
            if (_failed || !ModState.Loaded)
            {
                TestRefusals.Tell(Logger, "Weather", _failed ? TestRefusal.FeatureOff : TestRefusal.NotLoaded);
                return;
            }
            Weather.Release();
            Weather.Reset();
            Weather.Force(sky, spell);
            _weather.Changed();
            _weather.Tick(this);
            var data = new Dictionary<string, string>(3)
            {
                { "grip", Mathf.RoundToInt(Weather.GripFactor * 100f).ToString() }, { "accidents", Mathf.RoundToInt(Weather.AccidentFactor * 100f).ToString() },
                { "crime", Mathf.RoundToInt(Weather.CrimeFactor * 100f).ToString() }
            };
            Notifications.Show(NotificationType.Info, "reallife_weather_test_on", data, 9f);
        }

        /// <summary>Options-page test: clears any fire or street event and gives the weather back to the game.</summary>
        public void TestClear()
        {
            try
            {
                EndScene(true, "the clear button");
                StopCrashFire();
                Weather.Release();
                Weather.Reset();
                _weather.Changed();   // fair weather for a moment: the game's own is read again within the second
                Notifications.Show(NotificationType.Info, "reallife_events_test_cleared", null, 5f);
            }
            catch (Exception e)
            {
                Fail(e);
            }
        }

        // A stage that refused may have put part of itself out: it is taken back without a word.
        private void EndSceneQuietly()
        {
            SafetyNet();
            Cordon.Recheck();
        }
    }
}
