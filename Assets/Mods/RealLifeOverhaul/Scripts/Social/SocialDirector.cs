using System;
using System.Collections.Generic;
using BAModAPI;
using BigAmbitions.DayNightCycle;
using Helpers;
using NWH.VehiclePhysics2.Damage;
using UI;
using UI.Guiders;
using UI.Load;
using UI.Notification;
using RealLifeOverhaul.GameUi;
using UnityEngine;
using Random = UnityEngine.Random;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The one-line doors other features knock on. Each is safe to call at any time, costs a
    /// null check when the social life is not running, and never throws.
    /// </summary>
    public static class SocialHooks
    {
        /// <summary>The player helped somebody (a 911 call that brought help, a thief stopped, a wallet returned).</summary>
        public static void Helped()
        {
            SocialDirector director = SocialDirector.Instance;
            if (director != null)
            {
                director.OnHelped();
            }
        }

        /// <summary>The player bought a coffee at a cart.</summary>
        public static void BoughtCoffee()
        {
            SocialDirector director = SocialDirector.Instance;
            if (director != null)
            {
                director.OnCoffee();
            }
        }

        /// <summary>The player was taken into custody.</summary>
        public static void PlayerArrested()
        {
            SocialDirector director = SocialDirector.Instance;
            if (director != null)
            {
                director.OnArrested();
            }
        }

        /// <summary>
        /// The player saved a selfie in photo mode, standing at <paramref name="where"/>. On an outing with the friend
        /// beside them, the friend likes it (once an outing): the first name of who did, or null.
        /// </summary>
        public static string Selfie(Vector3 where)
        {
            SocialDirector director = SocialDirector.Instance;
            return director != null ? director.OnSelfie(where) : null;
        }

        /// <summary>The friend on an outing with the player who stands beside <paramref name="where"/> now (the social life's number), or -1.</summary>
        public static int Companion(Vector3 where)
        {
            SocialDirector director = SocialDirector.Instance;
            return director != null ? director.Companion(where) : -1;
        }
    }

    /// <summary>
    /// Runs the social life: loads and saves it, keeps the day's round (moods, neglect,
    /// birthdays, loans), decides when somebody gets in touch, keeps the plan that is on, and
    /// plays the one scene there can be at a time (somebody walking up, an outing, a favour).
    ///
    /// Cost: with nothing on, <see cref="Update"/> is three comparisons a frame; once a second
    /// a handful more (did the hour change, is a plan due, did the player come out of a door).
    /// A scene is ticked five times a second, its panels brought up to date on the same tick, and per frame there are only clock comparisons; the game itself listens
    /// for its key. Text is built when something changes, never per frame. Anything unexpected
    /// is logged once, everything on the street is given back, and the social life stays off
    /// for the session.
    /// </summary>
    public sealed class SocialDirector : MonoBehaviour
    {
        private static readonly FeatureGuard FrameGuard = Guard.For("social", "SocialDirector", typeof(SocialDirector));   // its Update, LateUpdate and FixedUpdate (Core/Guard)

        private const float TickInterval = 0.2f;
        private const float CardSeconds = 11f;
        private const string ButtonAnswer = "reallife_social_button_answer";
        private const float RingSeconds = 22f;
        private const float PendingSeconds = 150f;
        private const float IntroSeconds = 720f;
        private static readonly float[] WealthTiers = { 10000f, 50000f, 250000f, 1000000f, 5000000f };

        private enum Inside { None, Gym, OwnShop, Home, Other }

        public static IModLogger Logger;
        public static SocialDirector Instance { get; private set; }

        /// <summary>What the face-to-face conversation that is open is about; null when there is none.</summary>
        public static SceneTalk Talk;

        /// <summary>True while the interaction key is the social life's: a prompt is up, or the phone is ringing.</summary>
        public static bool TakesInteractKey =>
            Instance != null && (Instance._ringing || Instance._keyFrame == Time.frameCount || (Instance._door != null && Instance._door.TakesKey)
                                 || (Instance._scene != null && Instance._scene.Prompt != null));

        /// <summary>True while somebody is out on the street with the player (not while a plan only shows where to go).</summary>
        public static bool Busy => Instance != null && Instance._scene != null && !Instance._scene.Waiting;

        /// <summary>True while the game's own destination guider points at a scene's place (which is then on the city map already): for World/MapLinks.</summary>
        public static bool Guiding => Instance != null && Instance._mark.Showing;

        // ---- what a scene is handed: refreshed in place on the tick
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
        private SocialScene _selfieScene;   // the outing a selfie was last liked on: one like an outing
        private Rigidbody _carBody;
        private CarController _hookedCar;
        private float _lastCrash = -100f;
        private float _nextSlow;
        private float _nextTick;
        private float _nextDraw;
        private float _lastInsideAt = -100f;
        private float _lastContextAt;
        private Vector3 _lastOnFoot;
        private bool _broken;
        private int _keyFrame = -1;
        private int _lastHour = -1;
        private int _phoneState = -1;
        private Vector3 _guidedAt;
        private readonly GuideMark _mark = new GuideMark();   // the game's destination marker for the scene's place or person
        private DoorPanel _door;                              // the game's own panel over the person, or the player, with one button
        private ShiftCard _plan;                              // a copy of the game's job panel for the plan ("Coffee with Mom, 15:00 at ...")
        private bool _planTried;
        private Action _onPrompt;                             // made once: the panel's button
        private Func<string> _doorWhat;                       // made once: for the log
        private string _planToldLine;

        // ---- things waiting for their moment
        private SocialPerson _pendingMeet;
        private float _pendingFrom, _pendingUntil;
        private SocialPerson _pendingDropBy;
        private bool _dropByAtShop;
        private float _dropByUntil;
        private SocialPerson _introFriend, _introStranger;
        private float _introUntil;
        private bool _ringing;
        private SocialPerson _ringPerson;
        private float _ringUntil;
        private string _ringPrompt;
        private SocialPerson _callWhenFree;
        private float _callUntil;
        private bool _reactArrest;
        private int _reactedCrashDay, _reactedBreakInDay;
        private Inside _inside;
        private bool _wasInside;
        private bool _wasOnShift;
        private int _favourTurn;
        private SocialPerson _thanks;
        private float _thanksAt;
        private float _nextPendingTry;
        private SceneBreak _held;              // what is holding the scene up, if anything: it waits, and is not ticked
        private float _heldFrom;
        private TestRefusal _lastRefusal;
        private float _lastRefusalAt = -100f;

        private void Awake()
        {
            Instance = this;
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

        private static readonly int HotUpdate = HotSpots.Slot("Social: SocialDirector.Update");
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

        // A fault: logged once, whoever was on the street taken back, the social life off for the session.
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

        // ---------------------------------------------------------------- once a second

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

            // The game clears its event hub on every load, so the save hook is kept attached.
            SaveHook.Keep(_onSave ?? (_onSave = OnSaveGame));   // the mod's one relay in the game's save event (Core/SaveHook): nothing is allocated while that event has not changed

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
                News.Compose(day, false);   // the City Ledger prints it (Scripts/Ledger)
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
                SocialLife.SendText(_thanks, SocialLines.Pick(SocialCatalogue.TextThanks), SocialLife.Data(_thanks));
                _thanks = null;
            }

            Doors(real, gm);
            Plan(real);
            Pending(real, gm);
        }

        private System.Action _onSave;   // made once: Core/SaveHook compares it, it is never built again

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

        // ---------------------------------------------------------------- where the player has been

        // Coming out of a gym, their own shop or their home is when somebody from there can be met.
        private void Doors(float real, GameManager gm)
        {
            if (BuildingManager.IsInsideBuilding)
            {
                if (!_wasInside)
                {
                    _wasInside = true;
                    _inside = Classify();
                }
                return;
            }
            bool shift = JobDirector.OnShift;
            if (_wasOnShift && !shift && JobDirector.EarnedToday > 0)
            {
                Pend(SocialOrigin.Work, 0.3f, real, 12f);
            }
            _wasOnShift = shift;
            if (!_wasInside || !TrafficTools.Street)
            {
                return;
            }
            _wasInside = false;
            switch (_inside)
            {
                case Inside.Gym:
                    Pend(SocialOrigin.Gym, 0.35f, real, 4f);
                    break;
                case Inside.Home:
                    Pend(SocialOrigin.Neighbour, 0.15f, real, 4f);
                    break;
                case Inside.OwnShop:
                    SocialPerson friend = Settings.SocialContact && SocialState.LastDropByDay != SocialLife.Day && Random.value < 0.25f ? Visitor() : null;
                    if (friend != null)
                    {
                        _pendingDropBy = friend;
                        _dropByAtShop = true;
                        _dropByUntil = real + PendingSeconds;
                    }
                    else
                    {
                        Pend(SocialOrigin.Customer, 0.2f, real, 4f);
                    }
                    break;
            }
            _inside = Inside.None;
        }

        private static Inside Classify()
        {
            try
            {
                BuildingManager manager = InstanceBehavior<BuildingManager>.Instance;
                BuildingRegistration registration = manager != null ? manager.buildingRegistration : null;
                if (registration != null && GoingPublic.Mine(registration) && registration.HasEstablishedBusiness)
                {
                    return Inside.OwnShop;
                }
                if (registration != null && registration.businessTypeName != null && registration.businessTypeName.IndexOf("gym", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return Inside.Gym;
                }
                if (manager != null && manager.building != null && manager.building.BuildingType == "ba:buildingtype_residential")
                {
                    return Inside.Home;
                }
            }
            catch (Exception)
            {
                // a building the game has no data for is nowhere in particular
            }
            return Inside.Other;
        }

        private void Pend(SocialOrigin origin, float chance, float real, float delay)
        {
            SocialPerson person = SocialState.ByOrigin(origin);
            if (!Settings.SocialMeetings || person == null || person.Met || _pendingMeet != null || SocialLife.Day - SocialState.LastMeetDay < 2 || Random.value > chance)
            {
                return;
            }
            _pendingMeet = person;
            _pendingFrom = real + delay;
            _pendingUntil = real + delay + PendingSeconds;
            Logger?.Info("Social: " + person.Name + " (" + origin + ") will come over if the player stays on foot in the street.");
        }

        internal void OnHelped()
        {
            if (!_broken && SocialState.Loaded && Settings.Social)
            {
                Pend(SocialOrigin.Helped, 0.6f, Time.unscaledTime, 25f);
            }
        }

        internal void OnCoffee()
        {
            if (_broken || !SocialState.Loaded || !Settings.Social)
            {
                return;
            }
            SocialState.CoffeeBuys++;
            if (SocialState.CoffeeBuys >= 2)
            {
                Pend(SocialOrigin.CoffeeCart, 0.5f, Time.unscaledTime, 5f);   // a regular notices another
            }
        }

        internal string OnSelfie(Vector3 where)
        {
            try
            {
                if (_broken || !SocialState.Loaded || !Settings.Social || !(_scene is OutingScene outing) || ReferenceEquals(_selfieScene, _scene)
                    || !outing.Beside(where, SocialRules.SelfieWithin))
                {
                    return null;
                }
                _selfieScene = _scene;
                SocialPerson person = outing.Who;
                SocialLife.Change(person, SocialRules.SelfieFamiliarity, SocialRules.SelfieAffection, 0f);
                Logger?.Info("Social: " + person.Name + " was in a selfie with the player on an outing and liked it.");
                return SocialLife.First(person);
            }
            catch (Exception e)
            {
                Logger?.Error(e);
                return null;
            }
        }

        internal int Companion(Vector3 where)
        {
            return !_broken && SocialState.Loaded && Settings.Social && _scene is OutingScene outing && outing.Beside(where, SocialRules.SelfieWithin) ? outing.Who.Index : -1;
        }

        internal void OnArrested()
        {
            if (_broken || !SocialState.Loaded || !Settings.Social || SocialState.LastArrestDay == SocialLife.Day)
            {
                return;
            }
            SocialState.LastArrestDay = SocialLife.Day;
            for (int i = 0; i < SocialRules.CastSize; i++)
            {
                SocialPerson person = SocialState.Get(i);
                if (person == null || !person.Met)
                {
                    continue;
                }
                // Word gets round. Family worries more than it judges; a supportive friend hardly minds.
                float trust = person.IsFamily ? -2f : person.Has(SocialTrait.Supportive) ? -1f : SocialRules.ArrestTrust;
                SocialLife.Remember(person, MemoryCode.Arrest);
                SocialLife.Change(person, 0f, 0f, trust);
            }
            _reactArrest = true;
        }

        // ---------------------------------------------------------------- the hour and the day

        private void Hourly(float real, int day, int hour, bool skipping)
        {
            News.NoteWeather();
            if (skipping || !Settings.SocialContact || SocialState.MetCount == 0 || !SocialLife.MayText())
            {
                return;
            }
            // Something happened to the player: one of them gets in touch about it before anything else.
            if (_reactArrest)
            {
                _reactArrest = false;
                SocialPerson mom = SocialState.ByOrigin(SocialOrigin.Family);
                SocialPerson who = mom != null && Random.value < 0.5f ? mom : Contactor(hour);
                if (who != null)
                {
                    SocialLife.SendText(who, SocialLines.Pick(who.IsFamily ? SocialCatalogue.TextArrestMom
                        : who.Has(SocialTrait.Supportive) || who.Together ? SocialCatalogue.TextArrestKind : SocialCatalogue.TextArrestStern), SocialLife.Data(who));
                }
                return;
            }
            if (_reactedCrashDay != day && News.Recent(NewsKind.PlayerCrash, 0, out Vector3 where, out _))
            {
                _reactedCrashDay = day;
                SocialPerson who = Contactor(hour);
                if (who != null && who.Stage >= SocialStage.Friend)
                {
                    SocialLife.SendText(who, SocialLines.Pick(SocialCatalogue.TextCrash), SocialLife.Data(who, "street", SocialPlaces.StreetAt(where)));
                    return;
                }
            }
            if (_reactedBreakInDay != day && News.Recent(NewsKind.BreakIn, 0, out _, out string shop))
            {
                _reactedBreakInDay = day;
                SocialPerson who = Contactor(hour);
                if (who != null && who.Stage >= SocialStage.Friend)
                {
                    SocialLife.SendText(who, SocialLines.Pick(SocialCatalogue.TextBreakIn), SocialLife.Data(who, "shop", shop ?? SocialLife.T("reallife_social_your_shop")));
                    return;
                }
            }
            if (Random.value > 0.09f + 0.02f * SocialState.MetCount)
            {
                return;
            }
            SocialPerson person = Contactor(hour);
            if (person == null)
            {
                return;
            }
            bool free = !SocialState.Offer.Active && !SocialState.Plan.Active;
            float roll = Random.value;
            if (free && Settings.SocialOutings && roll < 0.38f)
            {
                Invite(person, real);
            }
            else if (free && roll < 0.52f && person.Trust >= 20f)
            {
                AskFavour(person, real, -1);
            }
            else if (roll < 0.58f && person.Stage >= SocialStage.Friend && Settings.SocialMeetings && day - SocialState.LastMeetDay >= 2 && NextStranger() != null
                     && _introStranger == null)
            {
                SocialPerson stranger = NextStranger();
                if (SocialLife.SendText(person, SocialLines.Pick(SocialCatalogue.TextIntro), SocialLife.Data(person, "other", stranger.Name)))
                {
                    QueueIntroduction(person, stranger);
                }
            }
            else
            {
                string[] pool = person.MovedIn ? SocialCatalogue.TextHome : person.Together ? SocialCatalogue.TextLove
                    : person.Mood <= -1 ? SocialCatalogue.TextBadDay : SocialCatalogue.TextNews;
                SocialLife.SendText(person, SocialLines.Pick(pool), SocialLife.Data(person));
            }
        }

        // Who gets in touch: somebody awake who has not texted today; the closer, the likelier.
        private static SocialPerson Contactor(int hour)
        {
            float total = 0f;
            int day = SocialLife.Day;
            for (int i = 0; i < SocialRules.CastSize; i++)
            {
                SocialPerson p = SocialState.Get(i);
                if (p != null && p.Met && !p.Drifted && !p.Asleep(hour) && p.LastTextDay != day)
                {
                    total += Keenness(p);
                }
            }
            if (total <= 0f)
            {
                return null;
            }
            float roll = Random.value * total;
            for (int i = 0; i < SocialRules.CastSize; i++)
            {
                SocialPerson p = SocialState.Get(i);
                if (p != null && p.Met && !p.Drifted && !p.Asleep(hour) && p.LastTextDay != day)
                {
                    roll -= Keenness(p);
                    if (roll <= 0f)
                    {
                        return p;
                    }
                }
            }
            return null;
        }

        private static float Keenness(SocialPerson p)
        {
            return 1f + p.Closeness / 25f + (p.Together ? 3f : 0f);
        }

        private void Invite(SocialPerson person, float real)
        {
            PlanKind kind = person.Wants;
            OutingDef def = SocialCatalogue.Outing(kind);
            if (person.Stage < def.Needs && !person.IsFamily)
            {
                kind = PlanKind.Coffee;
                def = SocialCatalogue.Outing(kind);
            }
            SocialOffer offer = SocialState.Offer;
            offer.Person = person.Index;
            offer.Kind = (int)kind;
            offer.Amount = 0;
            offer.Expires = SocialLife.Now + 36f * 60f;
            // Somebody close may ring instead; otherwise it is a text, and the answer is given by calling them.
            if (person.Closeness >= 40 && Random.value < 0.3f && StartRing(person, real))
            {
                return;
            }
            if (!SocialLife.SendText(person, def.Invite, SocialLife.Data(person)))
            {
                offer.Clear();
            }
        }

        private void AskFavour(SocialPerson person, float real, int which)
        {
            // What they could ask: a lift of anybody they trust a little, money or a morning's carrying only of a friend.
            bool friend = person.Stage >= SocialStage.Friend;
            bool canCarry = ClipLibrary.Has("carry_box_hold") && PropLibrary.Ready;
            int kind = which >= 0 ? which : Random.Range(0, friend ? 3 : 1);
            if (kind == 2 && !canCarry)
            {
                kind = 0;
            }
            SocialOffer offer = SocialState.Offer;
            offer.Person = person.Index;
            offer.Expires = SocialLife.Now + 6f * 60f;
            offer.Amount = 0;
            string[] text;
            if (kind == 1)
            {
                float money = SaveGameManager.Current != null ? SaveGameManager.Current.Money : 0f;
                offer.Kind = SocialOffer.Loan;
                offer.Amount = Mathf.Clamp(Mathf.RoundToInt(money * 0.02f / 10f) * 10, Prices.At(40), Prices.At(400));
                offer.Expires = SocialLife.Now + 24f * 60f;
                text = SocialCatalogue.TextLoanAsk;
            }
            else if (kind == 2)
            {
                offer.Kind = (int)PlanKind.Moving;
                offer.Expires = SocialLife.Now + 24f * 60f;
                text = SocialCatalogue.TextMovingAsk;
            }
            else
            {
                offer.Kind = (int)PlanKind.Lift;
                text = SocialCatalogue.TextLiftAsk;
            }
            if (Random.value < 0.4f && StartRing(person, real))
            {
                return;
            }
            Dictionary<string, string> data = SocialLife.Data(person, "amount", SocialLife.Money(offer.Amount));
            if (!SocialLife.SendText(person, SocialLines.Pick(text), data, which >= 0))
            {
                offer.Clear();
            }
        }

        /// <summary>The first person of the cast the player has still to meet, or null.</summary>
        public static SocialPerson NextStranger()
        {
            for (int i = 0; i < SocialRules.CastSize; i++)
            {
                SocialPerson p = SocialState.Get(i);
                if (p != null && !p.Met)
                {
                    return p;
                }
            }
            return null;
        }

        /// <summary>A friend will bring somebody along the next time the player is on foot in the street. False if one is already waiting.</summary>
        public bool QueueIntroduction(SocialPerson friend, SocialPerson stranger)
        {
            if (_broken || _introStranger != null || friend == null || stranger == null || stranger.Met || !Settings.SocialMeetings)
            {
                return false;
            }
            _introFriend = friend;
            _introStranger = stranger;
            _introUntil = Time.unscaledTime + IntroSeconds;
            return true;
        }

        // A friend who might be passing: somebody close enough to bother, awake, not seen today.
        private static SocialPerson Visitor()
        {
            SocialPerson best = null;
            int hour = SocialLife.Hour;
            int day = SocialLife.Day;
            int from = Random.Range(0, SocialRules.CastSize);
            for (int i = 0; i < SocialRules.CastSize; i++)
            {
                SocialPerson p = SocialState.Get((from + i) % SocialRules.CastSize);
                if (p != null && p.Met && !p.Drifted && p.Stage >= SocialStage.Friend && !p.Asleep(hour) && p.LastOutingDay != day
                    && p.FreeAt(hour, SocialLife.Weekend(day)) && (best == null || p.Closeness > best.Closeness + 10))
                {
                    best = p;
                }
            }
            return best;
        }

        private void Daily(int day)
        {
            int perYear = SocialPlaces.DaysPerYear;
            int today = (Mathf.Max(1, day) - 1) % perYear + 1;
            int yesterday = (Mathf.Max(2, day) - 2) % perYear + 1;
            SocialState.Day = day;
            SocialPerson mom = SocialState.ByOrigin(SocialOrigin.Family);
            bool reminded = false;
            ISocialLaw law = SocialLinks.Law;
            for (int i = 0; i < SocialRules.CastSize; i++)
            {
                SocialPerson p = SocialState.Get(i);
                if (p == null || !p.Met)
                {
                    continue;
                }
                // How their day is going: mostly fine, now and then rough or grand. A birthday is a good day.
                float roll = Random.value;
                p.Mood = roll < 0.08f ? -2 : roll < 0.25f ? -1 : roll < 0.65f ? 0 : roll < 0.9f ? 1 : 2;
                if (p.Birthday == today)
                {
                    p.Mood = 2;
                }
                if (day - p.WantsDay >= 3)
                {
                    PlanKind wants = p.Wants;
                    float best = 0f;
                    for (int k = 0; k <= (int)PlanKind.Business; k++)
                    {
                        var kind = (PlanKind)k;
                        if (kind == p.Wants || p.Stage < SocialCatalogue.Outing(kind).Needs)
                        {
                            continue;
                        }
                        float score = SocialRules.Liking(p, kind) * Random.Range(0.6f, 1.4f);
                        if (score > best)
                        {
                            best = score;
                            wants = kind;
                        }
                    }
                    p.Wants = wants;
                    p.WantsDay = day;
                }

                if (Settings.SocialRelationships)
                {
                    if (SocialRules.Decay(p, day))
                    {
                        SocialLife.Restage(p);
                    }
                    if (!p.Drifted && !p.IsFamily && p.Stage == SocialStage.Acquaintance && p.Familiarity < SocialRules.DriftFamiliarity && day - p.LastContactDay > 10)
                    {
                        p.Drifted = true;
                        SocialLife.Toast(NotificationType.Info, "reallife_social_drifted", SocialLife.Data(p), 9f);
                        Logger?.Info("Social: " + p.Name + " has drifted away.");
                    }
                    else if (!p.Drifted && p.Stage >= SocialStage.Friend && day - p.LastContactDay == SocialRules.Grace(p) + 2)
                    {
                        SocialLife.SendText(p, SocialLines.Pick(SocialCatalogue.TextDrift), SocialLife.Data(p));
                    }
                    // A birthday that went by without a word.
                    if (p.Birthday == yesterday && day > 1 && p.MetDay < day - 1 && p.Stage >= SocialStage.Friend && p.BirthdayYear != (day - 2) / perYear)
                    {
                        p.BirthdayYear = (day - 2) / perYear;
                        SocialLife.Change(p, 0f, SocialRules.BirthdayMissed, 0f);
                        SocialLife.SendText(p, SocialLines.Pick(SocialCatalogue.TextBirthdayMissed), SocialLife.Data(p));
                    }
                    // What the player borrowed and has not given back.
                    if (p.Debt > 0 && day - p.DebtDay > SocialRules.RepayDays)
                    {
                        p.DebtDay = day;
                        SocialLife.Remember(p, MemoryCode.NotRepaid);
                        SocialLife.Change(p, 0f, -2f, SocialRules.NotRepaidTrust);
                        SocialLife.SendText(p, SocialLines.Pick(SocialCatalogue.TextDebt), SocialLife.Data(p, "amount", SocialLife.Money(p.Debt)), true);
                    }
                    // People who like things done properly think less of somebody with a record, until the record is lived down.
                    if (law != null && law.HasRecord && law.Reputation < 60 && (p.Has(SocialTrait.Punctual) || p.Has(SocialTrait.Careful) || p.Has(SocialTrait.Reliable)))
                    {
                        SocialLife.Change(p, 0f, 0f, -0.3f);
                    }
                }

                // What they borrowed from the player.
                if (p.Loan > 0 && day >= p.LoanDue)
                {
                    bool repays = p.Has(SocialTrait.Reliable) || p.Remembers(MemoryCode.NotRepaid) == false && Random.value < (p.Has(SocialTrait.Flaky) ? 0.55f : 0.9f);
                    if (repays)
                    {
                        Dictionary<string, string> data = SocialLife.Data(p, "amount", SocialLife.Money(p.Loan));
                        SocialLife.Give(p.Loan, "reallife_transaction_social_loan_back");
                        SocialLife.SendText(p, SocialLines.Pick(SocialCatalogue.TextLoanBack), data, true);
                        p.Loan = 0;
                    }
                    else
                    {
                        // An excuse, and a few more days. Somebody flaky may never get round to it: the second time it is written off.
                        Dictionary<string, string> data = SocialLife.Data(p, "amount", SocialLife.Money(p.Loan));
                        SocialLife.SendText(p, SocialLines.Pick(SocialCatalogue.TextLoanExcuse), data, true);
                        if (p.LoanDue < 0)
                        {
                            p.Loan = 0;
                            SocialLife.Remember(p, MemoryCode.NotRepaid);
                        }
                        else
                        {
                            p.LoanDue = -(day + 4);   // negative: the excuse has been made once
                        }
                    }
                }
                else if (p.Loan > 0 && p.LoanDue < 0 && day >= -p.LoanDue)
                {
                    bool repays = !p.Has(SocialTrait.Flaky) || Random.value < 0.5f;
                    Dictionary<string, string> data = SocialLife.Data(p, "amount", SocialLife.Money(p.Loan));
                    if (repays)
                    {
                        SocialLife.Give(p.Loan, "reallife_transaction_social_loan_back");
                        SocialLife.SendText(p, SocialLines.Pick(SocialCatalogue.TextLoanBack), data, true);
                    }
                    else
                    {
                        SocialLife.Remember(p, MemoryCode.NotRepaid);
                        SocialLife.Toast(NotificationType.Warning, "reallife_social_loan_written_off", data, 9f);
                    }
                    p.Loan = 0;
                }

                // Birthdays: theirs today (they say so), or a friend's tomorrow (Mom says so).
                if (Settings.SocialContact && p.Birthday == today && p.BirthdayYear != (day - 1) / perYear)
                {
                    SocialLife.SendText(p, SocialLines.Pick(SocialCatalogue.TextBirthdayOwn), SocialLife.Data(p), true);
                }
                else if (Settings.SocialContact && !reminded && mom != null && p != mom && p.Stage >= SocialStage.Friend && p.Birthday == today % perYear + 1)
                {
                    reminded = true;
                    SocialLife.SendText(mom, SocialLines.Pick(SocialCatalogue.TextBirthdayMom), SocialLife.Data(p));
                }
                if (p.MovedIn)
                {
                    ModState.AddModifier(ModState.Family, 24);
                }
            }

            // Money changes how some people look at you.
            float money = SaveGameManager.Current != null ? SaveGameManager.Current.Money : 0f;
            int tier = 0;
            while (tier < WealthTiers.Length && money >= WealthTiers[tier])
            {
                tier++;
            }
            if (tier > SocialState.WealthTier && Settings.SocialContact)
            {
                SocialPerson who = Contactor(SocialLife.Hour);
                if (who != null && who.Stage >= SocialStage.Friend)
                {
                    bool sour = who.Has(SocialTrait.Envious);
                    if (SocialLife.SendText(who, SocialLines.Pick(sour ? SocialCatalogue.TextRichSour : SocialCatalogue.TextRichKind), SocialLife.Data(who)))
                    {
                        SocialLife.Change(who, 0f, sour ? -1f : 1f, 0f);
                    }
                }
            }
            SocialState.WealthTier = tier;
        }

        // ---------------------------------------------------------------- the plan that is on

        private void Plan(float real)
        {
            SocialPlan plan = SocialState.Plan;
            if (!plan.Active || _scene is OutingScene)
            {
                return;
            }
            SocialPerson person = SocialState.Get(plan.Person);
            if (person == null || !person.Met)
            {
                plan.Clear();
                return;
            }
            if (!Settings.SocialOutings)
            {
                // Switched off with a plan on: it is dropped and said so; nobody is stood up over an option.
                Dictionary<string, string> off = SocialLife.Data(person, "why", SocialLife.T("reallife_social_why_outings_off"));
                off["outing"] = SocialLife.T(SocialCatalogue.Outing(plan.Kind).Name);
                SocialLife.Toast(NotificationType.Warning, "reallife_social_break_dropped", off, 10f);
                Logger?.Info("Social: the plan with " + person.Name + " was dropped (OutingsOff).");
                plan.Clear();
                return;
            }
            float left = plan.Time - SocialLife.Now;
            if (left < -SocialRules.StoodUpMinutes)
            {
                SocialLife.StoodUp();
                return;
            }
            if (!plan.Checked && left < 90f)
            {
                plan.Checked = true;
                if (left > 15f && Random.value < SocialRules.CancelOdds(person, plan.Kind, RainHelper.isRaining))
                {
                    TheyCancel(person, plan);
                    return;
                }
            }
            if (left > SocialRules.EarlyMinutes)
            {
                return;
            }
            if (!plan.Reminded)
            {
                plan.Reminded = true;
                Dictionary<string, string> data = SocialLife.Data(person, "place", plan.Place);
                data["time"] = SocialLife.Clock(plan.Time);
                data["outing"] = SocialLife.T(SocialCatalogue.Outing(plan.Kind).Name);
                SocialLife.Toast(NotificationType.Info, "reallife_social_plan_remind", data, 12f);
            }
            if (_scene != null || !StreetReady() || !Settings.SocialOutings)
            {
                return;
            }
            if (!SocialPlaces.Resolve(plan.Street, plan.Number, out Venue venue)
                && !SocialPlaces.Find(plan.Kind, new Vector3(plan.X, plan.Y, plan.Z), 0f, 250f, out venue))
            {
                // The place is not what it was (sold, shut for good): called off, nobody's fault.
                SocialLife.SendText(person, "reallife_social_plan_lost", SocialLife.Data(person), true);
                plan.Clear();
                return;
            }
            Venue to = default;
            if (plan.ToStreet != null)
            {
                SocialPlaces.Resolve(plan.ToStreet, plan.ToNumber, out to);
            }
            var scene = new OutingScene(person, plan.Kind, venue, plan.Time, plan.Arrives, plan.TheirIdea, to, false);
            RefreshContext(InstanceBehavior<GameManager>.Instance, Time.time);
            if (!Begin(scene))
            {
                SocialLife.SendText(person, "reallife_social_plan_lost", SocialLife.Data(person), true);
                plan.Clear();
            }
        }

        private void TheyCancel(SocialPerson person, SocialPlan plan)
        {
            PlanKind kind = plan.Kind;
            bool rain = RainHelper.isRaining && kind == PlanKind.Walk;
            SocialLife.SendText(person, SocialLines.Pick(rain ? SocialCatalogue.TextCancelRain : SocialCatalogue.TextCancel),
                SocialLife.Data(person, "outing", SocialLife.T(SocialCatalogue.Outing(kind).Name)), true);
            Logger?.Info("Social: " + person.Name + " called off " + kind + ".");
            plan.Clear();
            // They would still like to: the next call to them picks it up again.
            if (kind <= PlanKind.Business && !SocialState.Offer.Active)
            {
                SocialState.Offer.Person = person.Index;
                SocialState.Offer.Kind = (int)kind;
                SocialState.Offer.Amount = 0;
                SocialState.Offer.Expires = SocialLife.Now + 48f * 60f;
            }
        }

        // ---------------------------------------------------------------- people who are on their way to the player

        private void Pending(float real, GameManager gm)
        {
            if (_pendingMeet != null && (real > _pendingUntil || _pendingMeet.Met || !Settings.SocialMeetings))
            {
                _pendingMeet = null;
            }
            if (_pendingDropBy != null && real > _dropByUntil)
            {
                _pendingDropBy = null;
            }
            if (_introStranger != null && (real > _introUntil || _introStranger.Met))
            {
                _introStranger = null;
                _introFriend = null;
            }
            if (_callWhenFree != null && real > _callUntil)
            {
                _callWhenFree = null;
            }
            bool anything = _pendingMeet != null || _pendingDropBy != null || _introStranger != null || _callWhenFree != null;
            if (!anything || _scene != null || _ringing)
            {
                return;
            }
            if (_callWhenFree != null)
            {
                SocialPerson caller = _callWhenFree;
                if (StartRing(caller, real))
                {
                    _callWhenFree = null;
                }
                return;
            }
            if (real < _nextPendingTry || !StreetReady() || CrimeDirector.IsActive || PoliceAttention.IsBusy || TrafficAccidents.IsActive || !LifeEvents.IsSafeForEvent())
            {
                return;
            }
            _nextPendingTry = real + 5f;   // a place that would not do is not asked about again every second
            RefreshContext(gm, Time.time);
            if (!OnFoot)
            {
                return;
            }
            if (_introStranger != null)
            {
                if (Begin(new MeetScene(MeetScene.Kind.Introduction, _introStranger, _introFriend)))
                {
                    _introStranger = null;
                    _introFriend = null;
                }
            }
            else if (_pendingMeet != null && real >= _pendingFrom)
            {
                if (Begin(new MeetScene(MeetScene.Kind.Stranger, _pendingMeet)))
                {
                    _pendingMeet = null;
                }
            }
            else if (_pendingDropBy != null)
            {
                if (Begin(new MeetScene(MeetScene.Kind.DropBy, _pendingDropBy, null, _dropByAtShop)))
                {
                    _pendingDropBy = null;
                }
            }
        }

        // ---------------------------------------------------------------- the phone rings

        private bool CanRing()
        {
            var gm = InstanceBehavior<GameManager>.Instance;
            return !_ringing && _scene == null && gm != null && gm.selectedVehicle == null && !PlayerHelper.IsUsingVehicle && LifeEvents.IsSafeForEvent()
                   && !UiOpen() && !JobDirector.OnShift && !CrimeDirector.IsActive && !PoliceAttention.IsBusy;
        }

        private bool StartRing(SocialPerson person, float real)
        {
            if (person == null || !person.Met || !CanRing())
            {
                return false;
            }
            _ringing = true;
            _ringPerson = person;
            _ringUntil = real + RingSeconds;
            Dictionary<string, string> data = SocialLife.Data(person, "key", Keys.Label(KeyAction.Interact));
            _ringPrompt = SocialLife.Say("reallife_social_ring_prompt", data);
            SocialLife.Toast(NotificationType.Info, "reallife_social_ringing", data, 6f);
            SocialState.LastCallInDay = SocialLife.Day;
            Logger?.Info("Social: " + person.Name + " is ringing the player.");
            return true;
        }

        private void Ring(float real)
        {
            if (_ringPerson == null || real > _ringUntil || !Settings.Social)
            {
                // Not answered: they try a text instead (if the day's texts allow), and what they wanted still stands.
                _ringing = false;
                if (_ringPerson != null && Settings.Social)
                {
                    SocialLife.SendText(_ringPerson, SocialLines.Pick(SocialCatalogue.TextMissedCall), SocialLife.Data(_ringPerson));
                }
                _ringPerson = null;
                return;
            }
        }

        // The panel's button while the phone rings.
        private void AnswerRing()
        {
            if (!_ringing || _ringPerson == null)
            {
                return;
            }
            _keyFrame = Time.frameCount;
            _ringing = false;
            SocialPerson caller = _ringPerson;
            _ringPerson = null;
            SocialPhone.Incoming = caller.Index;
            if (!SocialPhone.Call(caller))
            {
                SocialPhone.Incoming = -1;
            }
        }

        // ---------------------------------------------------------------- the scene that is on

        private bool Begin(SocialScene scene)
        {
            bool begun;
            try
            {
                begun = scene.Start(this, Time.time);
            }
            catch (Exception e)
            {
                Logger?.Error(e);
                begun = false;
            }
            if (!begun)
            {
                // Said in the log once per reason (somebody waiting to come over is tried again every few seconds).
                if (scene.Refusal != _lastRefusal || Time.unscaledTime - _lastRefusalAt > 60f)
                {
                    _lastRefusal = scene.Refusal;
                    _lastRefusalAt = Time.unscaledTime;
                    Logger?.Info("Social: could not be started (" + scene.Refusal + (scene.RefusalDetail != null ? "; " + scene.RefusalDetail : string.Empty) + "): "
                                 + Described(scene) + ".");
                }
                SafeEnd(scene, SceneEnd.Interrupted);
                Cast.ReleaseAll();
                return false;
            }
            _scene = scene;
            _held = SceneBreak.None;
            _nextTick = 0f;
            _lastInsideAt = Time.time;
            return true;
        }

        private void Tick(float now)
        {
            var gm = InstanceBehavior<GameManager>.Instance;
            SceneBreak why = Broken(now, gm);
            if (why != SceneBreak.None && !CanWait(why))
            {
                Stop(why);
                return;
            }
            var premises = InstanceBehavior<BuildingManager>.Instance;
            if (premises != null && (premises.enteringBuilding || premises.exitingBuilding))
            {
                return;   // being taken through a door: nothing is asked of anyone until they are through
            }
            if (why != SceneBreak.None)
            {
                // A door, a time skip, the street not running: the scene waits if it can, and says when it has waited long enough.
                if (_held == SceneBreak.None)
                {
                    if (!_scene.Hold(why, now))
                    {
                        Stop(why);
                        return;
                    }
                    _held = why;
                    _heldFrom = now;
                    Unguide();
                    Logger?.Info("Social: held up (" + why + "), waiting: " + Described(_scene) + ".");
                }
                if (!_scene.Holding(why, now, now - _heldFrom))
                {
                    EndScene(SceneEnd.Left, SceneBreak.GaveUp);
                }
                else if (_scene.Over)
                {
                    EndScene(SceneEnd.Done);
                }
                return;
            }
            if (_held != SceneBreak.None)
            {
                SceneBreak was = _held;
                _held = SceneBreak.None;
                _lastInsideAt = now;   // the street takes a moment to start up again
                RefreshContext(gm, now);
                Logger?.Info("Social: back on the street after " + (now - _heldFrom).ToString("F0") + " s (" + was + "); the scene goes on.");
                _scene.Resume(was, now, now - _heldFrom);
            }
            RefreshContext(gm, now);
            _scene.Tick(now);
            if (_scene == null)
            {
                return;
            }
            Guide();
            if (Cast.AnyOut)
            {
                Cast.Cull();
            }
            if (_scene.Over)
            {
                EndScene(SceneEnd.Done);
            }
        }

        // What, if anything, has taken the player off the street: one answer per condition, the gravest first. The
        // game's own delivery jobs (currentPlayerMission) are not among them: they leave the player free on the street;
        // while one is on only the game's destination guider is left to it (see Guide). The standing objectives of the
        // game ("pay off your loans") are not missions at all.
        private SceneBreak Broken(float now, GameManager gm)
        {
            UIs uis = InstanceBehavior<UIs>.Instance;
            GameInstance save = SaveGameManager.Current;
            if (uis == null || save == null || gm == null || gm.playerController == null)
            {
                return SceneBreak.NoGame;
            }
            if (LoadScene.isLoading)
            {
                return SceneBreak.Loading;
            }
            if (FuneralHelper.PlayerDead)
            {
                return SceneBreak.PlayerDead;
            }
            if (EnergyHelper.goingToHospital)
            {
                return SceneBreak.Hospital;
            }
            if (_scene.SkippingTime)
            {
                _lastInsideAt = now;   // the street takes a moment to start up again after the time machine too
                return SceneBreak.None;
            }
            if (_scene is OutingScene && !Settings.SocialOutings)
            {
                return SceneBreak.OutingsOff;
            }
            if (JobDirector.OnShift)
            {
                return SceneBreak.JobShift;
            }
            if (uis.timeMachine != null && uis.timeMachine.isRunning)
            {
                _lastInsideAt = now;
                return SceneBreak.TimeSkip;
            }
            if (BuildingManager.IsInsideBuilding)
            {
                _lastInsideAt = now;
                return SceneBreak.Indoors;
            }
            if (!TrafficTools.Street && now - _lastInsideAt > 6f)
            {
                return SceneBreak.StreetNotReady;
            }
            return SceneBreak.None;
        }

        private static bool CanWait(SceneBreak why)
        {
            return why == SceneBreak.Indoors || why == SceneBreak.StreetNotReady || why == SceneBreak.TimeSkip;
        }

        // Ended by something that is not the scene's own doing: the scene tells the player why (if they had been told
        // about it at all) and what becomes of the plan, then everything is given back.
        private void Stop(SceneBreak why)
        {
            if (_scene == null)
            {
                return;
            }
            try
            {
                _scene.Cut(why);
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
            EndScene(SceneEnd.Interrupted, why);
        }

        private string Described(SocialScene scene)
        {
            try
            {
                return scene.Describe();
            }
            catch (Exception e)
            {
                Logger?.Error(e);
                return scene.GetType().Name;
            }
        }

        // Where something new may be staged: out of doors, with the street running and nothing else having the player.
        private static bool StreetReady()
        {
            UIs uis = InstanceBehavior<UIs>.Instance;
            GameInstance save = SaveGameManager.Current;
            return uis != null && save != null && !LoadScene.isLoading && !BuildingManager.IsInsideBuilding && TrafficTools.Street
                   && (uis.timeMachine == null || !uis.timeMachine.isRunning) && !JobDirector.OnShift && !FuneralHelper.PlayerDead && !EnergyHelper.goingToHospital;
        }

        private void RefreshContext(GameManager gm, float now)
        {
            if (gm == null || gm.playerController == null)
            {
                return;
            }
            PlayerController player = gm.playerController;
            Body.Bind(player);
            var car = gm.selectedVehicle as CarController;
            if (car != Car)
            {
                Car = car;
                _carBody = car != null ? car.GetComponent<Rigidbody>() : null;   // looked up once per vehicle
                Hook(car);
            }
            OnFoot = gm.selectedVehicle == null && !PlayerHelper.IsUsingVehicle;
            CarVelocity = _carBody != null ? _carBody.velocity : Vector3.zero;
            CarSpeed = CarVelocity.magnitude;
            Vector3 at = player.transform.position;
            float dt = now - _lastContextAt;
            PlayerSpeed = dt > 0.01f && dt < 1f ? JobSpots.Flat(at, _lastOnFoot) / dt : 0f;
            _lastOnFoot = at;
            _lastContextAt = now;
            PlayerPosition = car != null ? car.transform.position : at;
            Vector3 forward = player.transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude > 0.01f)
            {
                PlayerForward = forward.normalized;
            }
        }

        /// <summary>Where the player is right now, for whatever is asked outside a scene (a plan made on the phone).</summary>
        public static Vector3 Where()
        {
            var gm = InstanceBehavior<GameManager>.Instance;
            if (gm == null || gm.playerController == null)
            {
                return Vector3.zero;
            }
            return SocialPlaces.OnStreet(gm.selectedVehicle != null ? gm.selectedVehicle.transform.position : gm.playerController.transform.position);
        }

        /// <summary>Some minutes after a good outing they may send a word of thanks (a text like any other: the cap and the switch hold).</summary>
        public void ThankLater(SocialPerson person)
        {
            if (Random.value < 0.45f)
            {
                _thanks = person;
                _thanksAt = Time.unscaledTime + Random.Range(90f, 240f);
            }
        }

        /// <summary>
        /// What an outing cost and did, as one of the game's notifications (the lines under the title). It stays up for the notification's
        /// own time and is not taken down by the slow tick.
        /// </summary>
        public void ShowCard(string title, string body)
        {
            SocialLife.Toast(NotificationType.Info, "reallife_social_card_toast",
                new Dictionary<string, string>(2) { { "title", title ?? string.Empty }, { "body", body ?? string.Empty } }, CardSeconds);
        }

        // Five times a second while a scene is on or the phone rings: the plan on the copy of the game's job panel and the prompt in the
        // game's panel over the person. Each is told only what changed. The marker is the game's destination marker, moved by Guide().
        private void Draw()
        {
            if (_scene == null && !_ringing)
            {
                HideUi();
                return;
            }
            if (ThirdPersonCamera.IsUiOpen())
            {
                HideUi();
                return;
            }
            if (_scene != null && (_scene.Title != null || _scene.Line != null))
            {
                ShowPlan();
            }
            else
            {
                _plan?.Show(false);
            }
            string prompt = _ringing ? _ringPrompt : _scene != null && _held == SceneBreak.None ? _scene.Prompt : null;
            PlayerController player = PlayerHelper.PlayerController;
            if (prompt == null || player == null || SocialPhone.DialogOpen)
            {
                _door.Hide("nothing to offer here now");
                return;
            }
            WorldPanel panel = _door.Panel;
            panel.Lines(prompt, null, null, WorldPanel.White());
            panel.Button(0, _ringing ? ButtonAnswer : _scene.PromptButton, _onPrompt, true, true);
            panel.Button(1, null, null, false, false);
            panel.Button(2, null, null, false, false);
            panel.Button(3, null, null, false, false);
            Transform over = _ringing ? null : _scene.PromptOver;
            panel.Follow = over;
            panel.FollowLift = 2.1f;
            Vector3 at = player.transform.position;
            _door.Show(at, at, 2.1f, _doorWhat);
        }

        private string DoorWhat()
        {
            return _ringing ? "the phone is ringing" : _scene != null ? _scene.GetType().Name : string.Empty;
        }

        // The panel's button, on the game's key or a click: the phone is answered, or the scene's own key is pressed.
        private void PromptPressed()
        {
            if (_broken || UiOpen())
            {
                return;
            }
            try
            {
                if (_ringing)
                {
                    AnswerRing();
                }
                else if (_scene != null && _held == SceneBreak.None && _scene.Prompt != null)
                {
                    _keyFrame = Time.frameCount;
                    _scene.Interact(Time.time);
                }
            }
            catch (Exception e)
            {
                Fail(e);
            }
        }

        private void ShowPlan()
        {
            if (_plan != null && !_plan.Alive)
            {
                _plan = null;
                _planTried = false;
            }
            if (_plan == null && !_planTried)
            {
                _planTried = true;
                _plan = ShiftCard.Make(Logger);
                if (Meta.Dev.Verbose)
                {
                    Logger?.Info("Social check (plan card): " + (_plan != null ? "a copy of the game's job panel is ready: PASS"
                        : "the game's job panel could not be copied; the plan is sent as a notification each time it changes: FAIL") + ".");
                }
            }
            if (_plan == null)
            {
                if (_scene.Line != null && !ReferenceEquals(_scene.Line, _planToldLine))
                {
                    _planToldLine = _scene.Line;
                    SocialLife.Toast(NotificationType.Info, "reallife_social_text",
                        new Dictionary<string, string>(1) { { "text", (_scene.Title != null ? _scene.Title + ": " : string.Empty) + _scene.Line } }, 8f);
                }
                return;
            }
            Color32 white = WorldPanel.White();
            _plan.Title(_scene.Title);
            _plan.Line(0, _scene.Line, white);
            _plan.Line(1, _scene.Status, white);
            _plan.Show(true);
        }

        private void HideUi()
        {
            _plan?.Show(false);
            _door?.Hide("nothing is going on");
        }

        // The game's own destination marker, the one its delivery jobs use: the place also shows on the city map. Moved only when the
        // scene's place moves by more than a metre (somebody walking to the meeting place moves it as they go). A delivery job of the
        // game's own or a job of the mod's that has the marker keeps it: this is tried again on the next tick.
        private void Guide()
        {
            if (_scene == null)
            {
                return;
            }
            if (!_scene.HasMarker || (SaveGameManager.Current != null && SaveGameManager.Current.currentPlayerMission != null))
            {
                Unguide();
                return;
            }
            Vector3 at = _scene.MarkerAt;
            if (_mark.Showing && (at - _guidedAt).sqrMagnitude <= 1f && ReferenceEquals(_scene.MarkerLabel, _guidedLabel))
            {
                return;
            }
            if (_mark.Show(at, _scene.MarkerLabel))
            {
                _guidedAt = at;
                _guidedLabel = _scene.MarkerLabel;
                if (Meta.Dev.Verbose && !_markSaid)
                {
                    _markSaid = true;
                    Logger?.Info("Social check (marker): the game's destination marker is on " + _scene.MarkerLabel + ": PASS.");
                }
            }
        }

        private string _guidedLabel;
        private bool _markSaid;

        private void Unguide()
        {
            _mark.Hide();
            _guidedLabel = null;
        }

        // A screen of the game's has the keys: its menus, and the face-to-face dialogue.
        private static bool UiOpen()
        {
            return ThirdPersonCamera.IsUiOpen() || SocialPhone.DialogOpen;
        }

        private void EndScene(SceneEnd why, SceneBreak what = SceneBreak.None)
        {
            SocialScene scene = _scene;
            if (scene == null)
            {
                return;
            }
            _scene = null;
            SceneBreak held = _held;
            _held = SceneBreak.None;
            if (why != SceneEnd.Done)
            {
                // The exact reason, and what was going on, before anything is given back.
                Logger?.Info("Social: the scene ended early (" + why + (what != SceneBreak.None ? ": " + what : string.Empty)
                             + (held != SceneBreak.None ? ", while held up by " + held : string.Empty) + "): " + Described(scene)
                             + "; plan " + (SocialState.Plan.Active ? "still on" : "none") + ".");
            }
            try
            {
                // A conversation of ours still open is closed with the scene it belonged to.
                if (Talk != null && SocialPhone.DialogOpen)
                {
                    InstanceBehavior<UIs>.Instance.playerHUD.dialogUI.FinishDialog();
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
            Talk = null;
            HideUi();
            SafeEnd(scene, why);
            Cast.ReleaseAll();   // whoever a scene forgot
            Body.Release();      // the player is their own again, whatever the scene was doing with them
            Unhook();
            Unguide();
            Car = null;
            _carBody = null;
            if (why == SceneEnd.Done && scene is OutingScene outing && outing.Greeted && SocialLinks.OutingDone != null)
            {
                try
                {
                    SocialLinks.OutingDone(outing.Who, outing.Kind);   // an outing gone to its end: whoever keeps Flare may offer it as a post
                }
                catch (Exception e)
                {
                    Logger?.Error(e);
                }
            }
        }

        private static void SafeEnd(SocialScene scene, SceneEnd why)
        {
            try
            {
                scene.End(why);
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        // ---------------------------------------------------------------- crashes with somebody in the car

        private void Hook(CarController car)
        {
            Unhook();
            if (car != null && car.vehicleController != null)
            {
                car.vehicleController.onCollision.AddListener(OnCollision);
                _hookedCar = car;
            }
        }

        private void Unhook()
        {
            if (_hookedCar != null && _hookedCar.vehicleController != null)
            {
                _hookedCar.vehicleController.onCollision.RemoveListener(OnCollision);
            }
            _hookedCar = null;
        }

        private void OnCollision(Collision collision)
        {
            if (_scene == null || _broken || Time.time - _lastCrash < 2.5f)
            {
                return;
            }
            try
            {
                float speed = collision.relativeVelocity.magnitude;
                if (speed < 3f || !DamageHandler.IsCollisionValid(collision))
                {
                    return;
                }
                _lastCrash = Time.time;
                _scene.OnCrash(speed);
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        // ---------------------------------------------------------------- the test buttons

        private bool TestReady(string test, bool street)
        {
            TestRefusal why = !SocialState.Loaded ? TestRefusal.NotLoaded
                : _broken || !Settings.Social ? TestRefusal.FeatureOff
                : street ? TestRefusals.Street()
                : TestRefusal.None;
            if (why == TestRefusal.None && street && _scene != null && !_scene.Waiting)
            {
                why = TestRefusal.SceneActive;
            }
            if (why == TestRefusal.None && street && JobDirector.OnShift)
            {
                why = TestRefusal.OnJob;
            }
            if (why == TestRefusal.None && street && (FuneralHelper.PlayerDead || EnergyHelper.goingToHospital))
            {
                why = TestRefusal.ScreenBusy;
            }
            if (why != TestRefusal.None)
            {
                TestRefusals.Tell(Logger, test, why);
                return false;
            }
            if (street)
            {
                EndScene(SceneEnd.Left, SceneBreak.TestButton);   // a plan that was only showing the way gives the street up to the test
                RefreshContext(InstanceBehavior<GameManager>.Instance, Time.time);
            }
            return true;
        }

        /// <summary>Whom the test buttons act on: the chosen person, or failing that the one known best.</summary>
        private static SocialPerson TestTarget(bool mustBeMet)
        {
            SocialPerson chosen = SocialState.Get(SocialState.TestPerson);
            if (chosen != null && (chosen.Met || !mustBeMet))
            {
                return chosen;
            }
            SocialPerson best = null;
            for (int i = 0; i < SocialRules.CastSize; i++)
            {
                SocialPerson p = SocialState.Get(i);
                if (p != null && p.Met && !p.IsFamily && (best == null || p.Closeness > best.Closeness))
                {
                    best = p;
                }
            }
            return best ?? (mustBeMet ? SocialState.ByOrigin(SocialOrigin.Family) : chosen);
        }

        /// <summary>Test button: the next person the player has not met walks up now.</summary>
        public void TestMeet()
        {
            if (!TestReady("Meet somebody", true))
            {
                return;
            }
            SocialPerson stranger = NextStranger();
            if (stranger == null)
            {
                TestRefusals.Tell(Logger, "Meet somebody", TestRefusal.AllMet);
                return;
            }
            var scene = new MeetScene(MeetScene.Kind.Stranger, stranger);
            if (!Begin(scene))
            {
                TestRefusals.Tell(Logger, "Meet somebody", scene.Refusal, 0, scene.RefusalDetail);
            }
        }

        /// <summary>Test button: a friend who was passing walks up now.</summary>
        public void TestDropBy()
        {
            if (!TestReady("Friend drops by", true))
            {
                return;
            }
            SocialPerson friend = TestTarget(true);
            if (friend == null)
            {
                TestRefusals.Tell(Logger, "Friend drops by", TestRefusal.NobodyKnown);
                return;
            }
            var scene = new MeetScene(MeetScene.Kind.DropBy, friend, null, ShopFloorNear());
            if (!Begin(scene))
            {
                TestRefusals.Tell(Logger, "Friend drops by", scene.Refusal, 0, scene.RefusalDetail);
            }
        }

        private static bool ShopFloorNear()
        {
            return SocialPlaces.Find(PlanKind.Business, Where(), 0f, 25f, out _);
        }

        /// <summary>Test button: somebody the player knows rings now.</summary>
        public void TestCall()
        {
            if (!TestReady("Friend calls", false))
            {
                return;
            }
            SocialPerson friend = TestTarget(true);
            if (friend == null)
            {
                TestRefusals.Tell(Logger, "Friend calls", TestRefusal.NobodyKnown);
                return;
            }
            // The options screen is open while the button is pressed: the phone rings once it has been closed.
            _callWhenFree = friend;
            _callUntil = Time.unscaledTime + 60f;
            SocialLife.Toast(NotificationType.Info, "reallife_social_test_call_soon", SocialLife.Data(friend), 6f);
        }

        /// <summary>Test button: somebody the player knows sends a text now (the cap is not counted against it; the switch still holds).</summary>
        public void TestText()
        {
            if (!TestReady("Friend texts", false))
            {
                return;
            }
            SocialPerson friend = TestTarget(true);
            if (friend == null)
            {
                TestRefusals.Tell(Logger, "Friend texts", TestRefusal.NobodyKnown);
                return;
            }
            if (!Settings.SocialTexts)
            {
                TestRefusals.Tell(Logger, "Friend texts", TestRefusal.FeatureOff, 0, "text messages are switched off");
                return;
            }
            SocialLife.SendText(friend, SocialLines.Pick(friend.Mood <= -1 ? SocialCatalogue.TextBadDay : SocialCatalogue.TextNews), SocialLife.Data(friend), true);
        }

        /// <summary>Test button: somebody asks a favour now: a lift, a loan, help moving, in turn. Call them to answer.</summary>
        public void TestFavour()
        {
            if (!TestReady("Favour asked", false))
            {
                return;
            }
            SocialPerson friend = TestTarget(true);
            if (friend == null)
            {
                TestRefusals.Tell(Logger, "Favour asked", TestRefusal.NobodyKnown);
                return;
            }
            if (SocialState.Plan.Active)
            {
                TestRefusals.Tell(Logger, "Favour asked", TestRefusal.SceneActive, 0, "a plan is already on");
                return;
            }
            AskFavour(friend, Time.unscaledTime + 1000f, _favourTurn++ % 3);   // (never rings from here: the options screen is open)
        }

        /// <summary>Test button: the test buttons act on the next person of the cast.</summary>
        public void TestPerson()
        {
            if (!TestReady("Choose person", false))
            {
                return;
            }
            SocialState.TestPerson = (SocialState.TestPerson + 1) % SocialRules.CastSize;
            TellPerson(SocialState.Get(SocialState.TestPerson));
        }

        private static void TellPerson(SocialPerson person)
        {
            if (person == null)
            {
                return;
            }
            Dictionary<string, string> data = SocialLife.Data(person, "stage", person.Met ? SocialLife.StageName(person) : SocialLife.T(SocialCatalogue.StageNames[0]));
            data["f"] = Mathf.RoundToInt(person.Familiarity).ToString();
            data["a"] = Mathf.RoundToInt(person.Affection).ToString();
            data["t"] = Mathf.RoundToInt(person.Trust).ToString();
            SocialLife.Toast(NotificationType.Info, "reallife_social_test_person_now", data, 9f);
        }

        /// <summary>Test button: the chosen person moves one stage on (met, friend, close friend, dating, partner, living together).</summary>
        public void TestAdvance()
        {
            if (!TestReady("Advance relationship", false))
            {
                return;
            }
            SocialPerson p = TestTarget(false);
            if (p == null)
            {
                TestRefusals.Tell(Logger, "Advance relationship", TestRefusal.NobodyKnown);
                return;
            }
            int day = SocialLife.Day;
            if (!p.Met)
            {
                SocialLife.Meet(p, true);
            }
            else if (p.Stage == SocialStage.Acquaintance)
            {
                p.Drifted = false;
                p.Shared = Mathf.Max(p.Shared, SocialRules.FriendShared);
                Raise(p, SocialRules.FriendFamiliarity + 5f, SocialRules.FriendAffection + 5f, SocialRules.FriendTrust + 5f);
            }
            else if (p.Stage == SocialStage.Friend)
            {
                p.Shared = Mathf.Max(p.Shared, SocialRules.CloseShared);
                p.MetDay = Mathf.Min(p.MetDay, day - SocialRules.CloseDays);
                Raise(p, SocialRules.CloseFamiliarity + 5f, SocialRules.CloseAffection + 5f, SocialRules.CloseTrust + 5f);
            }
            else if (p.Stage == SocialStage.CloseFriend && p.Compatible && SocialState.Partner == null)
            {
                Raise(p, p.Familiarity, SocialRules.DateAffection + 5f, SocialRules.DateTrust + 5f);
                SocialLife.SetStage(p, SocialStage.Dating);
            }
            else if (p.Stage == SocialStage.Dating)
            {
                Raise(p, p.Familiarity, SocialRules.PartnerAffection + 5f, SocialRules.PartnerTrust + 5f);
                SocialLife.SetStage(p, SocialStage.Partner);
            }
            else if (p.Stage == SocialStage.Partner && !p.MovedIn)
            {
                p.MovedIn = true;
                SocialLife.Remember(p, MemoryCode.MovedIn);
            }
            else
            {
                TestRefusals.Tell(Logger, "Advance relationship", TestRefusal.NothingFits, 0, p.Name + " is as close as they can be");
                return;
            }
            p.LastContactDay = day;
            TellPerson(p);
        }

        private static void Raise(SocialPerson p, float familiarity, float affection, float trust)
        {
            p.Familiarity = Mathf.Max(p.Familiarity, familiarity);
            p.Affection = Mathf.Max(p.Affection, affection);
            p.Trust = Mathf.Max(p.Trust, trust);
            SocialLife.Restage(p);
        }

        /// <summary>Test button: an outing of a kind with the chosen person, here and now.</summary>
        public void TestOuting(PlanKind kind)
        {
            const string test = "Outing";
            if (!TestReady(test, true))
            {
                return;
            }
            if (!Settings.SocialOutings)
            {
                TestRefusals.Tell(Logger, test, TestRefusal.FeatureOff);
                return;
            }
            SocialPerson person = TestTarget(true);
            if (person == null)
            {
                TestRefusals.Tell(Logger, test, TestRefusal.NobodyKnown);
                return;
            }
            Vector3 here = Where();
            if ((kind == PlanKind.Drive || kind == PlanKind.Lift) && JobDirector.NearestCar(150f) == null)
            {
                TestRefusals.Tell(Logger, test, TestRefusal.NoCar, 150);
                return;
            }
            if (kind == PlanKind.Moving && (!ClipLibrary.Has("carry_box_hold") || !PropLibrary.Ready))
            {
                TestRefusals.Tell(Logger, test, TestRefusal.NothingFits, 0, "no clip or prop to carry a box with");
                return;
            }
            // A walk needs no business: "here" is the pavement where the player stands (so this one works anywhere out of
            // doors); only with no pavement within 8 m is a business looked for, as for the others.
            Venue venue = default;
            if (!(kind == PlanKind.Walk && SocialPlaces.Here(here, out venue)) && !SocialPlaces.Find(kind, here, 3f, 700f, out venue))
            {
                TestRefusals.Tell(Logger, test, TestRefusal.NoVenue, 700, kind.ToString());
                return;
            }
            Venue to = default;
            if ((kind == PlanKind.Lift && !SocialPlaces.Find(kind, venue.Front, 250f, 1200f, out to, venue.Building))
                || (kind == PlanKind.Moving && !SocialPlaces.Find(kind, venue.Front, 22f, 90f, out to, venue.Building)))
            {
                TestRefusals.Tell(Logger, test, TestRefusal.NoVenue, 1200, "nowhere to go to from " + venue.Name);
                return;
            }
            float nowMinutes = SocialLife.Now;
            var scene = new OutingScene(person, kind, venue, nowMinutes, nowMinutes - 1f, false, to, true);
            if (!Begin(scene))
            {
                TestRefusals.Tell(Logger, test, scene.Refusal, 0, scene.RefusalDetail);
                return;
            }
            Logger?.Info("Social: test outing " + kind + " with " + person.Name + " at " + venue.Name + ", " + JobSpots.Flat(here, venue.Front).ToString("F0") + " m away.");
        }

        /// <summary>
        /// Test button: an outing with Mom, here, now. A walk from the pavement where the player stands (or has pulled up),
        /// so it needs no business near: Mom walks in from out of view with the marker on her, is said hello to, and walks
        /// beside the player. Says why if it cannot (indoors, the street not running, a scene on, a shift, no pavement
        /// within 8 m). Nothing is recorded.
        /// </summary>
        public void TestOutingWithMom()
        {
            const string test = "Outing with Mom";
            if (!TestReady(test, true))
            {
                return;
            }
            if (!Settings.SocialOutings)
            {
                TestRefusals.Tell(Logger, test, TestRefusal.FeatureOff);
                return;
            }
            SocialPerson mom = SocialState.ByOrigin(SocialOrigin.Family);
            if (mom == null || !mom.Met)
            {
                mom = TestTarget(true);
            }
            if (mom == null)
            {
                TestRefusals.Tell(Logger, test, TestRefusal.NobodyKnown);
                return;
            }
            Vector3 here = Where();
            if (!SocialPlaces.Here(here, out Venue venue))
            {
                TestRefusals.Tell(Logger, test, TestRefusal.NoPavement);
                return;
            }
            float nowMinutes = SocialLife.Now;
            var scene = new OutingScene(mom, PlanKind.Walk, venue, nowMinutes, nowMinutes - 1f, false, default, true);
            if (!Begin(scene))
            {
                TestRefusals.Tell(Logger, test, scene.Refusal, 0, scene.RefusalDetail);
                return;
            }
            Logger?.Info("Social: test outing with " + mom.Name + ", here: a walk from the pavement " + JobSpots.Flat(here, venue.Front).ToString("F0") + " m away ("
                         + venue.Name + "); the player is " + (OnFoot ? "on foot" : "in a vehicle") + ".");
        }

        /// <summary>Test button: today's paper is written again from the log (the City Ledger's half of the button prints it and leaves a copy at the door).</summary>
        public void TestPaper()
        {
            if (!SocialState.Loaded || SaveGameManager.Current == null)
            {
                TestRefusals.Tell(Logger, "Print the paper", TestRefusal.NotLoaded);
                return;
            }
            if (!Settings.SocialNewspaper)
            {
                TestRefusals.Tell(Logger, "Print the paper", TestRefusal.FeatureOff);
                return;
            }
            News.Compose(SaveGameManager.Current.Day, true);
            for (int i = 0; i < News.Today.Count; i++)
            {
                Logger?.Info("Ledger: " + SocialLife.T(News.Today[i].Key, News.Today[i].Data));
            }
        }

        /// <summary>Test button: whatever is on is stopped, and the plan (if any) is dropped without blame.</summary>
        public void TestStop()
        {
            if (!SocialState.Loaded)
            {
                TestRefusals.Tell(Logger, "Stop", TestRefusal.NotLoaded);
                return;
            }
            EndScene(SceneEnd.Interrupted, SceneBreak.TestButton);
            _ringing = false;
            HideUi();
            _pendingMeet = null;
            _pendingDropBy = null;
            _introStranger = null;
            _callWhenFree = null;
            SocialState.Plan.Clear();
            SocialState.Offer.Clear();
        }
    }
}
