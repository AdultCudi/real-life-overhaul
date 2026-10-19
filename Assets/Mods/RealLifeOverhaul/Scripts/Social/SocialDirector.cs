using System;
using System.Collections.Generic;
using BAModAPI;
using RealLifeOverhaul.GameUi;
using UI;
using UI.Load;
using UnityEngine;
using Random = UnityEngine.Random;

namespace RealLifeOverhaul
{
    /// <summary>
    /// Runs the social life: its save, the day's round, who gets in touch, the plan, and the one scene at a time. The cost:
    /// idle, three comparisons a frame and a few more once a second; with a scene on, its tick and panels five times a
    /// second. A fault gives everything on the street back and leaves the social life off for the session.
    /// </summary>
    public sealed partial class SocialDirector : MonoBehaviour
    {
        private static readonly FeatureGuard FrameGuard = Guard.For("social", "SocialDirector", typeof(SocialDirector));
        private static readonly int HotUpdate = HotSpots.Slot("Social: SocialDirector.Update");

        private const float TickInterval = 0.2f;   // seconds between a scene's ticks, and between redraws of its panels

        public static IModLogger Logger;
        public static SocialDirector Instance { get; private set; }

        /// <summary>The face-to-face conversation open now, or null.</summary>
        public static SceneTalk Talk;

        /// <summary>True while the interaction key is the social life's: a prompt is up, or the phone is ringing.</summary>
        public static bool TakesInteractKey =>
            Instance != null && (Instance._ringing || Instance._keyFrame == Time.frameCount || (Instance._door != null && Instance._door.TakesKey)
                                 || (Instance._scene != null && Instance._scene.Prompt != null));

        /// <summary>True while somebody is out on the street with the player, not while a plan only shows where to go.</summary>
        public static bool Busy => Instance != null && Instance._scene != null && !Instance._scene.Waiting;

        /// <summary>The game's destination guider points at a scene's place, which is then on the city map already.</summary>
        public static bool Guiding => Instance != null && Instance._mark.Showing;

        // For the scene, brought up to date on its tick.
        public SocialCast Cast { get; private set; }
        public SocialBody Body { get; private set; }
        public CityProps Props { get; private set; }
        public IJobAnimations Animations { get; private set; }
        public Vector3 PlayerPosition { get; private set; }
        public Vector3 PlayerForward { get; private set; } = Vector3.forward;
        public float PlayerSpeed { get; private set; }
        public bool OnFoot { get; private set; }
        public CarController Car { get; private set; }
        public bool Driving => Car != null;
        public float CarSpeed { get; private set; }
        public Vector3 CarVelocity { get; private set; }

        private SocialScene _scene;
        private float _nextSlow;
        private float _nextTick;
        private float _nextDraw;
        private bool _broken;
        private int _keyFrame = -1;   // the frame the panel's button took the interaction key
        private int _lastHour = -1;
        private int _phoneState = -1;
        private readonly GuideMark _mark = new GuideMark("social scenes");   // the game's destination marker for the scene's place or person
        private DoorPanel _door;                                            // the game's own panel over the person, or the player, with one button
        private Action _onPrompt;
        private Func<string> _doorWhat;
        private Action _onSave;
        private SocialPerson _thanks;
        private string _thanksPlace;
        private PlanKind _thanksKind;
        private float _thanksAt;

        private void Awake()
        {
            Instance = this;
            _mark.Keep(this);   // the marker goes with this component, or when the guard stops it (Core/GameUi/GuideMarkWatch)
            _door = new DoorPanel(Logger, "Social check (prompt panel)");
            _onPrompt = _door.Press(PromptPressed);
            _doorWhat = DoorWhat;
            SocialState.Reset();
            News.Reset();
            SocialLife.Logger = Logger;
            Props = new CityProps(transform);
            Cast = new SocialCast(this, Props, Logger);
            Body = new SocialBody(Props, Logger);
            Animations = new ClipJobAnimations();
        }

        private void OnDestroy()
        {
            SaveHook.Drop(_onSave);
            EndScene(SceneEnd.Interrupted, SceneBreak.Shutdown);
            HideUi();
            _mark.Hide();
            _plan?.Destroy();
            _plan = null;
            Unhook();
            Body.Release();
            Cast.Dispose();
            SocialState.Reset();
            News.Reset();
            Talk = null;
            SocialPhone.Incoming = -1;
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void Update()
        {
            using var hotSpot = HotSpots.Time(HotUpdate);
            if (FrameGuard.Off)
            {
                (_scene as OutingScene)?.EndRestEffects();   // a seated outing's recovery is never left on
                enabled = false;   // its feature was switched off after its faults (Core/Guard)
                return;
            }
            try
            {
                OnUpdate();
            }
            catch (Exception e)
            {
                Guard.Fail(FrameGuard, e, this);
            }
        }

        private void OnUpdate()
        {
            float real = Time.unscaledTime;
            if (_broken || (_scene == null && !_ringing && real < _nextSlow))
            {
                return;
            }
            try
            {
                if (real >= _nextSlow)
                {
                    _nextSlow = real + 1f;
                    Slow(real);
                }
                if (_ringing)
                {
                    Ring(real);
                }
                if (_scene != null)
                {
                    if (Time.time >= _nextTick)
                    {
                        _nextTick = Time.time + TickInterval;
                        Tick(Time.time);
                    }
                }
                if (real >= _nextDraw)
                {
                    _nextDraw = real + TickInterval;
                    Draw();
                }
            }
            catch (Exception e)
            {
                Fail(e);
            }
        }

        private void Fail(Exception e)
        {
            Logger?.Error(e);
            Logger?.Warn("Social: switched off for this session after the error above; whoever was on the street was taken back.");
            _broken = true;
            EndScene(SceneEnd.Fault);
            _ringing = false;
            HideUi();
            _mark.Hide();
        }

        private void Slow(float real)
        {
            var gm = InstanceBehavior<GameManager>.Instance;
            GameInstance save = SaveGameManager.Current;
            if (gm == null || gm.playerController == null || save == null || LoadScene.isLoading)
            {
                return;
            }
            if (!SocialState.Loaded)
            {
                if (!ModState.Loaded)
                {
                    return;   // LifeEvents makes the save's modData ready first
                }
                if (save.modData == null)
                {
                    save.modData = new Dictionary<string, string>();
                }
                SocialState.Load(save.modData, save.Day, SocialPlaces.DaysPerYear, SocialPlaces.Districts, Random.Range(1, int.MaxValue));
                News.Load(save.modData);
                SocialLines.Forget();
                if (SocialState.Plan.Active && (SocialState.Get(SocialState.Plan.Person) == null || !SocialState.Get(SocialState.Plan.Person).Met))
                {
                    SocialState.Plan.Clear();
                }
                _lastHour = save.Hour;
                _phoneState = -1;
                Logger?.Info("Social: ready. " + SocialState.MetCount + " of " + SocialRules.CastSize + " people known; plan " + (SocialState.Plan.Active ? "on" : "none")
                             + "; paper of day " + News.PaperDay + ".");
            }

            // The game clears its event hub on every load; Core/SaveHook allocates nothing while the event is unchanged.
            SaveHook.Keep(_onSave ?? (_onSave = OnSaveGame));

            int phone = (Settings.Social ? 1 : 0) | (Settings.SocialNewspaper ? 2 : 0) | (Settings.PhoneContacts ? 4 : 0);
            if (phone != _phoneState)
            {
                _phoneState = phone;
                SocialPhone.Setup(Logger);
            }
            int day = save.Day;
            int hour = save.Hour;
            UIs uis = InstanceBehavior<UIs>.Instance;
            bool skipping = uis != null && uis.timeMachine != null && uis.timeMachine.isRunning;
            if (News.Due(day, hour) && !skipping)
            {
                News.Compose(day, false);
                Logger?.Info("Social: the City Ledger for day " + day + " is out (" + News.Today.Count + " pieces).");
            }
            if (!Settings.Social)
            {
                if (_scene != null)
                {
                    Stop(SceneBreak.SocialOff);
                }
                _ringing = false;
                SocialState.Plan.Clear();    // switched off: nobody is left waiting, and nobody is stood up
                SocialState.Offer.Clear();
                return;
            }
            if (hour != _lastHour)
            {
                _lastHour = hour;
                Hourly(real, day, hour, skipping);
            }
            if (SocialState.Day != day && hour >= 7 && !skipping)
            {
                Daily(day);
            }
            if (skipping)
            {
                return;   // nothing is decided while hours go by in seconds
            }

            SocialOffer offer = SocialState.Offer;
            if (offer.Active && SocialLife.Now > offer.Expires)
            {
                SocialPerson asked = SocialState.Get(offer.Person);
                if (asked != null && (offer.Kind == SocialOffer.Loan || offer.Kind >= (int)PlanKind.Lift))
                {
                    SocialLife.Change(asked, 0f, -1f, 0f);   // a favour asked and never answered
                }
                offer.Clear();
            }

            if (_thanks != null && real >= _thanksAt && _scene == null)
            {
                Dictionary<string, string> data = SocialLife.Data(_thanks);
                data["t_place"] = _thanksPlace ?? string.Empty;   // for RLO Texts' card
                data["t_kind"] = _thanksKind.ToString();
                SocialLife.SendText(_thanks, SocialLines.Pick(SocialCatalogue.TextThanks), data);
                _thanks = null;
            }

            Doors(real);
            Plan();
            Pending(real, gm);
        }

        private void OnSaveGame()
        {
            try
            {
                GameInstance save = SaveGameManager.Current;
                if (SocialState.Loaded && save != null)
                {
                    if (save.modData == null)
                    {
                        save.modData = new Dictionary<string, string>();
                    }
                    SocialState.Save(save.modData);
                    News.Save(save.modData);
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        /// <summary>Where the player is in the street now; indoors, the building's door.</summary>
        public static Vector3 Where()
        {
            var gm = InstanceBehavior<GameManager>.Instance;
            if (gm == null || gm.playerController == null)
            {
                return Vector3.zero;
            }
            return SocialPlaces.OnStreet(gm.selectedVehicle != null ? gm.selectedVehicle.transform.position : gm.playerController.transform.position);
        }

        /// <summary>A few minutes after a good outing they may text their thanks.</summary>
        public void ThankLater(SocialPerson person, string place, PlanKind kind)
        {
            if (Random.value < 0.45f)
            {
                _thanksPlace = place;
                _thanksKind = kind;
                _thanks = person;
                _thanksAt = Time.unscaledTime + Random.Range(90f, 240f);
            }
        }
    }
}
