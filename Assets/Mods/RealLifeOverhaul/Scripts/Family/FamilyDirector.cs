using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using BAModAPI;
using Helpers;
using UI;
using UI.Load;
using UI.Notification;
using Localizor;
using RealLifeOverhaul.GameUi;
using UnityEngine;

namespace RealLifeOverhaul.Family
{
    /// <summary>
    /// One staged scene with people in it: the partner at home is not one of these (HomeLife keeps
    /// them); a proposal, a wedding and a pushchair walk are. One at a time. The director ticks
    /// it five times a second and calls <see cref="End"/> exactly once on every way out, which
    /// gives back every person and prop and frees the player's body.
    /// </summary>
    internal abstract class FamilyScene
    {
        public bool Done;
        public abstract string Name { get; }
        /// <summary>Stages it where the player stands. False with a reason (a locale key) if it cannot be done here and now; nothing is left out then.</summary>
        public abstract bool Begin(FamilyDirector director, out string refusal);
        public abstract void Tick(float now);
        public abstract void End();
    }

    /// <summary>
    /// Family life: a partner who moves in, an engagement and a wedding, children, relatives and
    /// the hard times. This component is the only one of the feature that runs: a few
    /// comparisons a frame (and the key test while a prompt is up), a tick once a second (five
    /// a second during a staged scene), and the rest by the in-game hour and day. Everything it
    /// puts in the world it takes away again when the scene ends, the building is left, the
    /// feature is switched off or the city unloads. A fault in any tick is logged once and
    /// switches family life off for the session; the save is not touched by that.
    /// </summary>
    public sealed class FamilyDirector : MonoBehaviour
    {
        private static readonly FeatureGuard FrameGuard = Guard.For("family", "FamilyDirector", typeof(FamilyDirector));   // its Update, LateUpdate and FixedUpdate (Core/Guard)

        private const string ShotsFolder = "RealLifeOverhaul_shots";

        private struct Question
        {
            public string Header, Body, Yes, No;
            public Args Fill;
            public Action OnYes, OnNo;
        }

        public static IModLogger Logger;
        public static FamilyDirector Instance { get; private set; }

        /// <summary>True while one of family life's key prompts is on screen: the mod's own E/F (enter a door, get in a car) stands aside.</summary>
        public static bool TakesInteractKey => Instance != null && Instance._door != null && Instance._door.TakesKey;

        internal FamilyCast Cast;
        internal FamilyProps Props;
        internal JobBody Body;
        internal FamilyScene Scene;
        internal bool Busy;                 // a sequence (a fade, a skip of time) is running

        private readonly List<Question> _questions = new List<Question>(4);
        private JobProps _bodyProps;
        private float _nextTick;
        private float _nextHook;
        private Action _onSave;   // made once: Core/SaveHook compares it, it is never built again
        private int _lastHour = -1;
        private bool _started;
        private bool _failed;
        private bool _social;     // whether the partner came from Social when the phone contacts were last set up

        private DoorPanel _door;          // the game's own panel over the person or place a prompt is about
        private ShiftCard _card;          // a copy of the game's job panel for the objective
        private bool _cardTried;
        private Action _onPrompt;         // made once: the panel's button
        private Func<string> _promptWhat; // made once: for the log
        private string _promptButton;     // the button's locale key (the game writes the player's key after it)
        private bool _promptOn;
        private bool _promptWanted;
        private string _promptKey;
        private string _promptFill;
        private int _promptCost;
        private string _promptText;
        private Action _promptAction;
        private string _objectiveKey;
        private string _objectiveFill;
        private string _objectiveChild;
        private string _objectiveTitle;
        private string _objectiveLine;
        private bool _objectiveWanted;

        private void Awake()
        {
            Instance = this;
            _door = new DoorPanel(Logger, "Family check (prompt panel)");
            _onPrompt = _door.Press(PromptPressed);
            _promptWhat = PromptWhat;
            FamilyState.Logger = Logger;
            FamilyState.Reset();
            FamilyPlaces.Reset();
            FamilyModifiers.Register();   // before Core/ModState puts back the moods a save carried
            Cast = new FamilyCast(transform, Logger);
            Props = new FamilyProps(transform);
            _bodyProps = new JobProps(transform);
            Body = new JobBody(_bodyProps, Logger);
        }

        private void OnDestroy()
        {
            SaveHook.Drop(_onSave);
            Stop(true);
            try
            {
                Cast.Dispose();
                _bodyProps.Dispose();
                _door.Hide("the director went with the city");
                _card?.Destroy();
                _card = null;
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
            FamilyState.Reset();
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private static readonly int HotUpdate = HotSpots.Slot("Family: FamilyDirector.Update");
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
            if (!Settings.Family)
            {
                if (_started)
                {
                    Stop(false);
                }
                return;
            }
            var game = InstanceBehavior<GameManager>.Instance;
            if (game == null || game.playerController == null || SaveGameManager.Current == null || LoadScene.isLoading || !ModState.Loaded)
            {
                return;
            }
            try
            {
                if (!FamilyState.Loaded)
                {
                    if (Partner.UsesSocial && !FamilyBridges.Social.Ready)
                    {
                        return;   // who the partner is can only be read once Social has read its people
                    }
                    FamilyState.Load();
                    FamilyModifiers.Register();
                    _social = Partner.UsesSocial;
                    FamilyPhone.Setup();
                    Partner.Resync();
                    _lastHour = TimeHelper.CurrentHour;
                    if (FamilyState.S.LastDay <= 0)
                    {
                        FamilyState.S.LastDay = TimeHelper.CurrentDay;
                    }
                }
                _started = true;

                // The social life was switched on or off: who the partner is, and the contact that stands in for them, follow.
                if (Partner.UsesSocial != _social)
                {
                    _social = Partner.UsesSocial;
                    FamilyPhone.Setup();
                    Partner.Resync();
                }

                // The game clears its event hub on every load, so the save hook is kept attached.
                if (Time.unscaledTime >= _nextHook)
                {
                    _nextHook = Time.unscaledTime + 2f;
                    SaveHook.Keep(_onSave ?? (_onSave = OnSaveGame));   // the mod's one relay in the game's save event (Core/SaveHook): nothing is allocated while that event has not changed
                }

                if (Cast.AnyOut)
                {
                    Cast.Tick(Time.deltaTime);
                }
                float now = Time.time;
                if (now < _nextTick)
                {
                    return;
                }
                _nextTick = now + (Scene != null ? 0.2f : 1f);
                Tick(now);
            }
            catch (Exception e)
            {
                _failed = true;
                Logger?.Error(e);
                Logger?.Warn("Family: switched off for this session after the error above. The save keeps the family as it was.");
                Stop(false);
            }
        }

        private void Tick(float now)
        {
            int day = TimeHelper.CurrentDay;
            int hour = TimeHelper.CurrentHour;
            FamilySave s = FamilyState.S;
            if (day != s.LastDay)
            {
                int elapsed = Mathf.Clamp(day - s.LastDay, 1, 14);
                s.LastDay = day;
                FamilyCalendar.Daily(day, elapsed);
            }
            if (hour != _lastHour)
            {
                _lastHour = hour;
                FamilyCalendar.Hourly(day, hour);
            }

            Body.Bind(PlayerHelper.PlayerController);
            Body.Tick(now);
            _promptWanted = false;
            _objectiveWanted = false;

            if (!Busy && _questions.Count > 0 && LifeEvents.IsSafeForEvent() && !HudConfirm.isOpen)
            {
                Question q = _questions[0];
                _questions.RemoveAt(0);
                Put(q);
            }

            if (Scene != null)
            {
                Scene.Tick(now);
                if (Scene != null && Scene.Done)
                {
                    EndScene();
                }
            }
            else if (!Busy)
            {
                HomeLife.Tick(now);
                Errands.Tick(now);
                Errands.Counters(this);
            }
            if (!_promptWanted)
            {
                ClearPrompt();
            }
            if (!_objectiveWanted)
            {
                _card?.Show(false);
                _objectiveKey = null;
            }
        }

        // ---------------------------------------------------------------- stopping

        /// <summary>Everything that is going on ends and everything borrowed goes back; the save is not touched. For the save tools.</summary>
        public void Halt()
        {
            Stop(false);
        }

        private void Stop(bool leaving)
        {
            _started = false;
            try
            {
                StopAllCoroutines();
                if (Busy)
                {
                    Busy = false;
                    Recover();
                }
                if (Scene != null)
                {
                    EndScene();
                }
                HomeLife.Leave();
                Cast.ReleaseAll();
                Props.ReturnAll();
                Body.Release();
                FamilyPlaces.Unguide();
                _questions.Clear();
                ClearPrompt();
                _card?.Show(false);
                _objectiveKey = null;
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
            if (leaving)
            {
                FamilyBridges.Reset();
            }
        }

        private void OnSaveGame()
        {
            try
            {
                FamilyState.Write();
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        // ---------------------------------------------------------------- what the parts ask of the director

        /// <summary>Runs something that is not on the director's own tick (a button, a phone choice, a prompt's answer): a fault is logged and goes no further.</summary>
        public void Guarded(Action action)
        {
            if (!FamilyState.Loaded || SaveGameManager.Current == null || action == null)
            {
                return;
            }
            try
            {
                action();
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        /// <summary>True when the player could answer a prompt: no menu, no conversation, no job's key in the way.</summary>
        internal static bool CanAct()
        {
            return !ThirdPersonCamera.IsUiOpen() && DialogController.current == null && !JobDirector.TakesInteractKey
                   && !GameMembers.PlayerDead && LifeEvents.IsSafeForEvent();
        }

        /// <summary>
        /// The prompt for this tick, as the game's own panel over <paramref name="over"/> (a person; the player when null) with one
        /// button, which the game labels with the player's key. Whoever wants one asks every tick; when nobody does it goes. The
        /// words are rebuilt only when the key or what fills it changes; the panel is told nothing when nothing changed.
        /// </summary>
        internal void Prompt(string key, string fill, Action action, Transform over = null, int cost = 0)
        {
            if (_promptWanted)
            {
                return;   // the first to ask in a tick has it
            }
            _promptWanted = true;
            _promptAction = action;
            if (!ReferenceEquals(key, _promptKey) || fill != _promptFill || cost != _promptCost)
            {
                _promptKey = key;
                _promptFill = fill;
                _promptCost = cost;
                // (An errand that costs something names it, at today's prices: {amount}.)
                string line = FamilyText.Fill(key.GetLocalization().Replace("{key}: ", string.Empty),
                    new Args { Name = fill, Child = fill, Place = fill, Amount = cost > 0 ? FamilyText.Money(cost) : null });
                _promptText = line.Length > 0 ? char.ToUpperInvariant(line[0]) + line.Substring(1) : line;
                _promptButton = key.Replace("_prompt_", "_button_");
            }
            PlayerController player = PlayerHelper.PlayerController;
            if (CanAct() && player != null)
            {
                WorldPanel panel = _door.Panel;
                panel.Lines(_promptText, null, null, WorldPanel.White());
                panel.Button(0, _promptButton, _onPrompt, true, true);
                panel.Button(1, null, null, false, false);
                panel.Button(2, null, null, false, false);
                panel.Button(3, null, null, false, false);
                panel.Follow = over;
                panel.FollowLift = 2.1f;
                _promptOn = _door.Show(player.transform.position, player.transform.position, 2.1f, _promptWhat);
            }
            else if (_promptOn || _door.Up)
            {
                ClearPrompt();
            }
        }

        private string PromptWhat()
        {
            return _promptKey ?? string.Empty;
        }

        // The panel's button (the game runs it on the player's key or a click): the prompt's own action, once.
        private void PromptPressed()
        {
            if (!CanAct())
            {
                return;
            }
            try
            {
                Action act = _promptAction;
                ClearPrompt();
                act?.Invoke();
            }
            catch (Exception e)
            {
                _failed = true;
                Logger?.Error(e);
                Logger?.Warn("Family: switched off for this session after the error above. The save keeps the family as it was.");
                Stop(false);
            }
        }

        private void ClearPrompt()
        {
            _promptAction = null;
            if (_promptOn || _door.Up)
            {
                _promptOn = false;
                _door.Panel.Follow = null;
                _door.Hide("nothing to offer here now");
            }
        }

        /// <summary>
        /// The objective for this tick, on the copy of the game's job panel at the top right: the headline says what is going on and
        /// the first line what to do, where and by when. Without that panel the line goes out as one of the game's notifications
        /// each time it changes. Words are rebuilt only when the objective changes.
        /// </summary>
        internal void Objective(string titleKey, string lineKey, string fill, string child = null)
        {
            _objectiveWanted = true;
            bool changed = !ReferenceEquals(lineKey, _objectiveKey) || fill != _objectiveFill || child != _objectiveChild;
            if (changed)
            {
                _objectiveKey = lineKey;
                _objectiveFill = fill;
                _objectiveChild = child;
                _objectiveTitle = FamilyText.T(titleKey);
                _objectiveLine = FamilyText.T(lineKey, new Args { Name = Partner.Name, Place = fill, Child = child ?? fill, Text = fill, N = fill });
            }
            if (!_cardTried || (_card != null && !_card.Alive))
            {
                _cardTried = true;
                _card = ShiftCard.Make(Logger);
                if (Meta.Dev.Verbose)
                {
                    Logger?.Info("Family check (objective card): " + (_card != null ? "a copy of the game's job panel is ready: PASS" : "the game's job panel could not be copied; objectives are sent as notifications: FAIL") + ".");
                }
            }
            if (_card == null)
            {
                if (changed && !ThirdPersonCamera.IsUiOpen())
                {
                    FamilyText.Toast(NotificationType.Info, "reallife_family_text", new Args { Text = _objectiveTitle + ": " + _objectiveLine }, 8f);
                }
                return;
            }
            if (ThirdPersonCamera.IsUiOpen())
            {
                _card.Show(false);
                return;
            }
            _card.Title(_objectiveTitle);
            _card.Line(0, _objectiveLine, WorldPanel.White());
            for (int i = 1; i < ShiftCard.Lines; i++)
            {
                _card.Line(i, null, WorldPanel.White());
            }
            _card.Show(true);
        }

        /// <summary>
        /// A question with two answers, in the game's own confirmation box. Asked as soon as
        /// nothing else has the player's attention: never lost, never over a menu.
        /// </summary>
        internal void Ask(string header, string body, in Args fill, string yes, string no, Action onYes, Action onNo = null)
        {
            _questions.Add(new Question { Header = header, Body = body, Fill = fill, Yes = yes, No = no, OnYes = onYes, OnNo = onNo });
        }

        private void Put(Question q)
        {
            GameConfirm.Show(FamilyText.Say(q.Header, q.Fill), FamilyText.Say(q.Body, q.Fill),
                () => Guarded(q.OnYes), () => Guarded(q.OnNo), q.Yes, q.No, false);
        }

        internal bool HasQuestions => _questions.Count > 0;

        // ---------------------------------------------------------------- sequences

        /// <summary>Runs a sequence (a fade, a skip of time, what follows). One at a time; a second is refused.</summary>
        internal bool Run(IEnumerator routine)
        {
            if (Busy || !isActiveAndEnabled)
            {
                return false;
            }
            StartCoroutine(Wrap(routine));
            return true;
        }

        private IEnumerator Wrap(IEnumerator routine)
        {
            Busy = true;
            while (true)
            {
                bool more;
                try
                {
                    more = routine.MoveNext();
                }
                catch (Exception e)
                {
                    Logger?.Error(e);
                    Recover();
                    break;
                }
                if (!more)
                {
                    break;
                }
                yield return routine.Current;
            }
            Busy = false;
        }

        // A sequence faulted or was cut off part of the way: the screen is given back and nothing stays held.
        private void Recover()
        {
            try
            {
                EnergyHelper.SetCurrentEnergyRegen(EnergyRegen.None);
                Body.Release();
                if (UiFader.isFading && isActiveAndEnabled)
                {
                    StartCoroutine(UiFader.UnFade());
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        /// <summary>
        /// The usual shape of something that takes time: the screen goes dark with a line on it,
        /// <paramref name="during"/> happens unseen, so many hours pass (the game's own time
        /// skip), the screen comes back and <paramref name="after"/> says how it went.
        /// </summary>
        internal IEnumerator Passage(string infoKey, int hours, Action during, Action after)
        {
            // Out of a conversation first: the phone is put away, the counter left.
            float waited = 0f;
            while ((DialogController.current != null || HudConfirm.isOpen) && waited < 20f)
            {
                waited += Time.unscaledDeltaTime;
                yield return null;
            }
            yield return UiFader.Fade(0.5f, infoKey);
            during?.Invoke();
            UIs uis = InstanceBehavior<UIs>.Instance;
            if (hours > 0 && !GoingPublic.ClockShared && uis != null && uis.timeMachine != null && !uis.timeMachine.isRunning)   // in a Going Public session no time passes (Core/GoingPublic)
            {
                // A day at a time: longer than that and hunger alone would put somebody in hospital.
                int left = hours;
                while (left > 0)
                {
                    int chunk = Mathf.Min(left, 24);
                    left -= chunk;
                    if (hours >= 24)
                    {
                        SaveGameManager.Current.Energy = 100f;
                        SaveGameManager.Current.Hunger = 100f;
                    }
                    if (hours >= 4)
                    {
                        EnergyHelper.SetCurrentEnergyRegen(hours >= 24 ? EnergyRegen.Bed : EnergyRegen.Bench);   // nobody collapses half-way through their own wedding
                    }
                    uis.timeMachine.StartTimeMachine(TimeHelper.GetTimeInHours(chunk), true, infoKey);
                    while (uis.timeMachine.isRunning)
                    {
                        yield return null;
                    }
                    EnergyHelper.SetCurrentEnergyRegen(EnergyRegen.None);
                    yield return null;
                }
                if (hours >= 24)
                {
                    SaveGameManager.Current.Energy = 100f;
                    SaveGameManager.Current.Hunger = 100f;
                }
            }
            yield return UiFader.UnFade();
            after?.Invoke();
        }

        /// <summary>Does something once the player is free again (the conversation closed, no menu up).</summary>
        internal void WhenFree(Action action)
        {
            if (isActiveAndEnabled)
            {
                StartCoroutine(Free(action));
            }
        }

        private IEnumerator Free(Action action)
        {
            float waited = 0f;
            yield return null;
            while ((DialogController.current != null || HudConfirm.isOpen || Busy) && waited < 60f)
            {
                waited += Time.unscaledDeltaTime;
                yield return null;
            }
            Guarded(action);
        }

        /// <summary>
        /// A photograph: the game's HUD and the mod's own are hidden for two frames, the screen is
        /// written to the mod's folder in the game's data folder, and everything is put back.
        /// </summary>
        internal IEnumerator Photograph(string name)
        {
            UIs uis = InstanceBehavior<UIs>.Instance;
            bool hud = false, phone = false, top = false;
            string file = null;
            try
            {
                string folder = Path.Combine(Application.persistentDataPath, ShotsFolder);
                Directory.CreateDirectory(folder);
                file = Path.Combine(folder, name + "_day" + TimeHelper.CurrentDay + "_" + DateTime.Now.ToString("HHmmss") + ".png");
                if (uis != null)
                {
                    hud = uis.playerHUD != null && uis.playerHUD.gameObject.activeSelf;
                    phone = uis.smartphoneUI != null && uis.smartphoneUI.gameObject.activeSelf;
                    top = uis.topBar != null && uis.topBar.gameObject.activeSelf;
                    if (hud) uis.playerHUD.gameObject.SetActive(false);
                    if (phone) uis.smartphoneUI.gameObject.SetActive(false);
                    if (top) uis.topBar.gameObject.SetActive(false);
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
                file = null;
            }
            yield return null;
            if (file != null)
            {
                ScreenCapture.CaptureScreenshot(file);
            }
            yield return null;
            yield return null;
            try
            {
                if (uis != null)
                {
                    if (hud) uis.playerHUD.gameObject.SetActive(true);
                    if (phone) uis.smartphoneUI.gameObject.SetActive(true);
                    if (top) uis.topBar.gameObject.SetActive(true);
                }
                if (file != null)
                {
                    Logger?.Info("Family: photograph written to " + file);
                    FamilyText.Toast(NotificationType.Success, "reallife_family_photo_saved", new Args { Text = ShotsFolder });
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        // ---------------------------------------------------------------- scenes

        /// <summary>Why nothing with people in it can be staged right now; None if it can.</summary>
        internal TestRefusal SceneRefusal(bool outdoors)
        {
            if (!ModState.Loaded || !FamilyState.Loaded || SaveGameManager.Current == null || PlayerHelper.PlayerController == null)
            {
                return TestRefusal.NotLoaded;
            }
            if (_failed || !Settings.Family)
            {
                return TestRefusal.FeatureOff;
            }
            if (Scene != null || Busy || JobDirector.OnShift)
            {
                return TestRefusal.SceneActive;
            }
            if (PoliceAttention.IsBusy)
            {
                return TestRefusal.PoliceBusy;
            }
            if (PlayerHelper.IsUsingVehicle)
            {
                return TestRefusal.NothingFits;
            }
            if (outdoors && BuildingManager.IsInsideBuilding)
            {
                return TestRefusal.Indoors;
            }
            return TestRefusal.None;
        }

        internal bool StartScene(FamilyScene scene, out string refusal)
        {
            refusal = null;
            if (Scene != null || Busy)
            {
                refusal = "reallife_family_no_busy";
                return false;
            }
            try
            {
                if (!scene.Begin(this, out refusal))
                {
                    scene.End();
                    Logger?.Info("Family: " + scene.Name + " not started (" + (refusal ?? "no reason given") + ").");
                    return false;
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
                try { scene.End(); } catch (Exception inner) { Logger?.Error(inner); }
                refusal = "reallife_family_no_fault";
                return false;
            }
            Scene = scene;
            _nextTick = 0f;
            Logger?.Info("Family: " + scene.Name + " started.");
            return true;
        }

        internal void EndScene()
        {
            FamilyScene scene = Scene;
            Scene = null;
            if (scene == null)
            {
                return;
            }
            try
            {
                scene.End();
            }
            catch (Exception e)
            {
                Logger?.Error(e);
                Cast.ReleaseAll();
                Props.ReturnAll();
                Body.Release();
            }
            Logger?.Info("Family: " + scene.Name + " ended.");
        }

        // ---------------------------------------------------------------- the test buttons

        private bool TestReady(string test, bool outdoors, bool scene)
        {
            TestRefusal why = scene ? SceneRefusal(outdoors) : !ModState.Loaded || !FamilyState.Loaded || PlayerHelper.PlayerController == null ? TestRefusal.NotLoaded
                : _failed || !Settings.Family ? TestRefusal.FeatureOff : Busy ? TestRefusal.SceneActive : TestRefusal.None;
            if (why != TestRefusal.None)
            {
                TestRefusals.Tell(Logger, test, why);
                return false;
            }
            return true;
        }

        private void Refuse(string test, string reason)
        {
            Logger?.Info(test + " test: not started (" + reason + ").");
            FamilyText.Toast(NotificationType.Warning, reason ?? "reallife_family_no_fault", Args.Us);
        }

        // Somebody to test with: with Social absent a partner is made up on the spot; with it present the tests need its partner.
        private bool TestPartner(string test)
        {
            Partner.Resync();
            if (FamilyState.Together)
            {
                return true;
            }
            if (Partner.UsesSocial)
            {
                Refuse(test, "reallife_family_no_partner_social");
                return false;
            }
            Partner.Meet(Settings.FamilyInterest);
            FamilyState.S.PAffection = 90f;
            FamilyState.S.PMetDay = TimeHelper.CurrentDay - 30;
            FamilyState.S.SinceDay = FamilyState.S.PMetDay;
            FamilyText.Toast(NotificationType.Info, "reallife_family_test_partner_made", Args.Us);
            return true;
        }

        /// <summary>Options button: the partner moves in now.</summary>
        public void TestMoveIn()
        {
            if (!TestReady("Family move-in", false, false) || !TestPartner("Family move-in"))
            {
                return;
            }
            Guarded(() =>
            {
                string why = HomeLife.MoveIn(true);
                if (why != null)
                {
                    Refuse("Family move-in", why);
                }
            });
        }

        /// <summary>Options button: propose where the player stands.</summary>
        public void TestPropose()
        {
            if (!TestReady("Family proposal", false, true) || !TestPartner("Family proposal"))
            {
                return;
            }
            Guarded(() =>
            {
                FamilySave s = FamilyState.S;
                if (FamilyState.Bond != Bond.Dating)
                {
                    Refuse("Family proposal", "reallife_family_no_already_engaged");
                    return;
                }
                if (s.Ring <= 0)
                {
                    s.Ring = 2;
                }
                s.LastProposalDay = 0;
                if (!StartScene(new ProposalScene(true), out string refusal))
                {
                    Refuse("Family proposal", refusal);
                }
            });
        }

        /// <summary>Options button: open the wedding plan (engaged at once if need be).</summary>
        public void TestPlanWedding()
        {
            if (!TestReady("Family wedding plan", false, false) || !TestPartner("Family wedding plan"))
            {
                return;
            }
            Guarded(() =>
            {
                TestEngage();
                FamilyTalk.Open(() => WeddingPlanner.Start(false));
            });
        }

        /// <summary>Options button: hold the wedding here and now.</summary>
        public void TestWedding()
        {
            if (!TestReady("Family wedding", true, true) || !TestPartner("Family wedding"))
            {
                return;
            }
            Guarded(() =>
            {
                if (FamilyState.Bond == Bond.Married)
                {
                    Refuse("Family wedding", "reallife_family_no_already_married");
                    return;
                }
                TestEngage();
                WeddingPlan plan = FamilyState.S.Wedding;
                if (!plan.Active)
                {
                    WeddingPlanner.Quick(plan);
                }
                Vector3 here = PlayerHelper.GetPosition();
                plan.Venue = 1;
                plan.X = here.x;
                plan.Y = here.y;
                plan.Z = here.z;
                plan.Day = TimeHelper.CurrentDay;
                plan.TheirColdFeet = false;
                if (!StartScene(new WeddingScene { Free = true }, out string refusal))   // (free: no bill, no presents)
                {
                    Refuse("Family wedding", refusal);
                }
            });
        }

        private static void TestEngage()
        {
            if (FamilyState.Bond == Bond.Dating)
            {
                if (FamilyState.S.Ring <= 0)
                {
                    FamilyState.S.Ring = 2;
                }
                Courtship.Engage(false);
            }
        }

        /// <summary>Options button: start a pregnancy, or move the one (or the adoption) in progress on by a step.</summary>
        public void TestChild(bool adoption)
        {
            if (!TestReady("Family child", false, false) || !TestPartner("Family child"))
            {
                return;
            }
            Guarded(() =>
            {
                string why = Children.TestAdvance(adoption);
                if (why != null)
                {
                    Refuse("Family child", why);
                }
            });
        }

        /// <summary>Options button: every child is a year older.</summary>
        public void TestYear()
        {
            if (!TestReady("Family year", false, false))
            {
                return;
            }
            Guarded(() =>
            {
                if (FamilyState.S.Children.Count == 0)
                {
                    Refuse("Family year", "reallife_family_no_children");
                    return;
                }
                Children.Grow(1f);
                FamilyText.Toast(NotificationType.Info, "reallife_family_test_year_done", Args.Us);
            });
        }

        /// <summary>Options button: a pushchair walk now.</summary>
        public void TestWalk()
        {
            if (!TestReady("Family pushchair walk", true, true))
            {
                return;
            }
            Guarded(() =>
            {
                if (!StartScene(new WalkScene(true), out string refusal))
                {
                    Refuse("Family pushchair walk", refusal);
                }
            });
        }

        /// <summary>Options button: the Family page, face to face.</summary>
        public void TestPage()
        {
            if (!TestReady("Family page", false, false))
            {
                return;
            }
            if (DialogController.current != null)
            {
                TestRefusals.Tell(Logger, "Family page", TestRefusal.SceneActive);
                return;
            }
            Guarded(() => FamilyTalk.Open(() => FamilyPage.Page(0)));
        }

        /// <summary>Options button: nothing of the family is left.</summary>
        public void TestClear()
        {
            if (!ModState.Loaded || !FamilyState.Loaded)
            {
                TestRefusals.Tell(Logger, "Family clear", TestRefusal.NotLoaded);
                return;
            }
            Guarded(() =>
            {
                Stop(false);
                FamilyModifiers.RemoveAll();
                FamilyState.Clear();
                Errands.Reset();
                Partner.Resync();
                _started = true;
                Logger?.Info("Family: all family state cleared by the test button.");
                FamilyText.Toast(NotificationType.Info, "reallife_family_test_cleared", Args.Us);
            });
        }
    }

    /// <summary>
    /// The moods of family life. The game has no public way to add a happiness modifier type, so
    /// they go into its registry the way Core/ModState adds the mod's others (and, having the
    /// mod's prefix, are taken out of the save and kept in modData by ModState like the rest).
    /// </summary>
    public static class FamilyModifiers
    {
        public const string Together = "reallife_happinessmodifier_together";       // living with somebody you love
        public const string Engaged = "reallife_happinessmodifier_engaged";
        public const string Married = "reallife_happinessmodifier_married";
        public const string Joy = "reallife_happinessmodifier_family_joy";          // a milestone, a walk, a good evening
        public const string NewParent = "reallife_happinessmodifier_new_parent";
        public const string Tired = "reallife_happinessmodifier_broken_night";
        public const string Heartache = "reallife_happinessmodifier_heartache";
        public const string Honeymoon = "reallife_happinessmodifier_honeymoon";

        private static readonly (string type, int amount, int hours)[] Defs =
        {
            (Together, 5, 72), (Engaged, 8, 72), (Married, 10, 72), (Joy, 6, 12), (NewParent, 8, 72), (Tired, -4, 10), (Heartache, -8, 48), (Honeymoon, 12, 96)
        };

        private static bool _registered;

        public static void Register()
        {
            if (_registered && Known())
            {
                return;
            }
            try
            {
                FieldInfo field = GameContract.HappinessRegistry.Field;   // (resolved once: Core/GameContract)
                var registry = field?.GetValue(null) as Dictionary<string, HappinessModifier>;
                if (registry == null)
                {
                    return;   // not loaded yet: asked again when the save is ready
                }
                foreach ((string type, int amount, int hours) in Defs)
                {
                    if (registry.ContainsKey(type))
                    {
                        continue;
                    }
                    var modifier = ScriptableObject.CreateInstance<HappinessModifier>();
                    modifier.type = type;
                    modifier.amount = amount;
                    modifier.hoursDuration = hours;
                    modifier.maxHoursDuration = -1;
                    registry[type] = modifier;
                }
                _registered = true;
            }
            catch (Exception e)
            {
                FamilyDirector.Logger?.Error(e);
            }
        }

        private static bool Known()
        {
            try
            {
                FieldInfo field = GameContract.HappinessRegistry.Field;   // (resolved once: Core/GameContract)
                var registry = field?.GetValue(null) as Dictionary<string, HappinessModifier>;
                return registry != null && registry.ContainsKey(Married);
            }
            catch (Exception)
            {
                return false;
            }
        }

        public static void Add(string type, int hours = -1)
        {
            if (_registered)
            {
                ModState.AddModifier(type, hours);
            }
        }

        public static void Remove(string type)
        {
            if (_registered)
            {
                ModState.RemoveModifier(type);
            }
        }

        /// <summary>
        /// Keeps a lasting mood topped up: added when absent, renewed when it has less than a day
        /// left. Called once an in-game day, so the game's little "happier" face shows at most then.
        /// </summary>
        public static void Keep(string type, bool wanted)
        {
            if (!_registered || SaveGameManager.Current == null || SaveGameManager.Current.happinessModifiers == null)
            {
                return;
            }
            List<HappinessModifierData> all = SaveGameManager.Current.happinessModifiers;
            HappinessModifierData found = null;
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i] != null && all[i].type == type)
                {
                    found = all[i];
                    break;
                }
            }
            if (!wanted)
            {
                if (found != null)
                {
                    ModState.RemoveModifier(type);
                }
                return;
            }
            if (found == null || found.hoursLeft < 30)
            {
                ModState.AddModifier(type);
            }
        }

        public static void RemoveAll()
        {
            foreach ((string type, int _, int _) in Defs)
            {
                Remove(type);
            }
        }
    }
}
