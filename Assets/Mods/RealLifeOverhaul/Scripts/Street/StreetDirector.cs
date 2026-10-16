using System;
using System.Collections.Generic;
using BAModAPI;
using RealLifeOverhaul.GameUi;
using Streets.Pedestrians;
using UI.Load;
using UnityEngine;

namespace RealLifeOverhaul.Street
{
    /// <summary>
    /// Talking to anybody in the street (Scripts/Street). Walk up to one of the game's own walkers, or somebody standing about
    /// or sitting on a bench, and face them: the game's own panel over them (the box the game puts over a petrol pump:
    /// GameUi.DoorPanel) offers Talk on the interact key. Who is offered is the nearest roughly in front within talking
    /// distance (StreetOffer), once they have been in front a moment, counted on the same person and kept for a short grace
    /// when a passer-by steps out of the front, so somebody walking by can be caught. The jobs' and the social life's own
    /// boxes and keys come first: street talk offers nothing while either has the key.
    /// They stop and turn to the player (StreetHold: a walker by the game's own turn, somebody standing on the spot,
    /// somebody seated stays as posed), and a conversation of a few turns plays in the
    /// game's own dialogue panel (StreetDialog, StreetDirector.Talk.cs): their line on top, the player's choices below.
    /// The game decides everything (StreetRules); the words are written lines, or the model's when RLO Texts has them in
    /// time (StreetTexts, through <see cref="Words"/>). What comes of it: the day's memory of the person (StreetMemory),
    /// their number (they join People, met in the street), an outing booked, the way to a place with the game's marker, word
    /// of mouth for the player's business (kept in the save: StreetStore), the police on a threat.
    ///
    /// Off (Settings.StreetTalk): no panel, nobody stops; the save's line is kept as it is.
    /// COST: per frame one time comparison. Five times a second on foot out of doors a pass over the people that are out
    /// (StreetGame.Facing: a few dozen distance tests); ten times a second while a conversation waits to open, a few field
    /// reads; texts and choices only on a press or a click. Nothing allocated per frame; with Developer diagnostics on, one
    /// check line at most every five seconds (<see cref="CheckEvery"/>).
    /// </summary>
    public sealed partial class StreetDirector : MonoBehaviour
    {
        private static readonly FeatureGuard FrameGuard = Guard.For("street", "StreetDirector", typeof(StreetDirector));   // its Update (Core/Guard)
        private static readonly int HotUpdate = HotSpots.Slot("Street: StreetDirector.Update");
        private static readonly int HotLook = HotSpots.Slot("Street: StreetDirector.Look");

        private const float Busy = 0.1f;            // real seconds between two ticks while a conversation is about to open or is open
        private const float CheckEvery = 5f;        // real seconds between two of the offer's check lines while nothing is offered (Developer diagnostics)

        public static IModLogger Logger;
        public static StreetDirector Instance { get; private set; }

        /// <summary>
        /// RLO Texts' words for a line (set by Wiring/StreetLinks: StreetTexts.Ask): asked with the line the rules chose and what the
        /// player said; true if asked, and then the words (or null for the written line) come back through the action within
        /// <see cref="StreetRules.Hold"/> seconds, on the game's thread. Null or false: the written line at once.
        /// </summary>
        public static Func<StreetChat, StreetLine, Dictionary<string, string>, string, Action<string>, bool> Words;

        /// <summary>RLO Texts: the opening line guessed while the player looks at somebody (asked ahead, taken at the press). Null: none.</summary>
        public static Action<StreetChat, StreetLine, Dictionary<string, string>> Guess;

        /// <summary>RLO Texts: the guessed opening line, taken at the press: the words, or null for the written line.</summary>
        public static Func<StreetChat, string> TakeGuess;

        /// <summary>
        /// RLO Texts: the answer to the blue button, asked as soon as it is shown (the line the rules would choose and the
        /// player's words), on a machine where a reply takes more than half the hold. Null: never.
        /// </summary>
        public static Action<StreetChat, StreetLine, Dictionary<string, string>, string> Ahead;

        /// <summary>RLO Texts: the answer asked ahead, taken at the click: the words, or null (then it is asked as usual).</summary>
        public static Func<StreetChat, string> TakeAhead;

        /// <summary>True while the interact key is street talk's (its panel is up, or its button ran this frame): the mod's own key stands aside.</summary>
        public static bool TakesInteractKey => Instance != null && Instance._door.TakesKey;

        /// <summary>True while somebody is stopped for the player or talking to them.</summary>
        public static bool Talking => Instance != null && Instance._chat != null;

        /// <summary>True while this figure is stopped for a talk: another feature's reactions (a horn's look round) leave them alone.</summary>
        public static bool Holds(BaseHuman figure) => Instance != null && Instance._hold.Holds(figure);

        private DoorPanel _door;
        private Action _onTalk;               // made once: the panel's button
        private Func<string> _doorWhat;       // made once: for the log
        private Action _onSave;
        private float _next, _nextHook;
        private bool _failed;
        private GameInstance _save;
        private BaseHuman _offered;           // who the panel is (about to be) over
        private Pedestrian _offeredWalker;    // ...their walker, or null for somebody standing or sitting
        private StreetFigure _offeredKind;
        private string _offeredLine;          // the panel's words for them
        private int _offeredKey;
        private readonly StreetAim _aim = new StreetAim();
        private bool _shown;                  // the box went up over them (its check line written)
        private float _nextCheck;             // when the offer's check line may be written again
        private readonly StreetMemory _memory = new StreetMemory();
        private readonly StreetFacts _facts = new StreetFacts();
        private readonly List<string> _addresses = new List<string>(4);   // the player's businesses, in step with _facts.Shops
        private StreetMouth _mouth = new StreetMouth();

        private void Awake()
        {
            Instance = this;
            StreetGame.Logger = Logger;
            _door = new DoorPanel(Logger, "Street check (talk panel)");
            _onTalk = _door.Press(TalkPressed);
            _doorWhat = () => _offeredLine ?? string.Empty;
            _onSave = OnSaveGame;
            _onClosed = DialogClosed;
            _onSaid = Said;
            _onTyped = TypingClosed;
            _onSayOrBye = SayOrBye;
            _onGoodbye = Goodbye;
            _onWords = WordsCame;
        }

        private void OnDestroy()
        {
            SaveHook.Drop(_onSave);
            try
            {
                Stop("the mod is unloading");
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
            if (Instance == this)
            {
                Instance = null;
            }
        }

        // ---------------------------------------------------------------- running

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
            catch (Exception e)
            {
                Guard.Fail(FrameGuard, e, this);
            }
        }

        private void OnUpdate()
        {
            float now = Time.unscaledTime;
            if (now < _next)
            {
                return;
            }
            _next = now + (_chat != null ? Busy : StreetRules.Tick);
            if (_failed)
            {
                return;
            }
            try
            {
                using var look = HotSpots.Time(HotLook);
                Look(now);
            }
            catch (Exception e)
            {
                Fault("its look", e);
            }
        }

        private void Look(float now)
        {
            GameInstance save = SaveGameManager.Current;
            if (!Settings.StreetTalk || save == null || LoadScene.isLoading || StreetGame.Player == null)
            {
                if (_save != null || _chat != null)
                {
                    Stop(!Settings.StreetTalk ? "switched off" : "no game");
                }
                return;
            }
            if (!ReferenceEquals(save, _save))
            {
                Load(save);
            }
            if (now >= _nextHook)
            {
                _nextHook = now + 2f;
                SaveHook.Keep(_onSave);   // the game clears its event hub on every load: the save hook is kept attached
            }
            if (_chat != null)
            {
                Tend(now);
                return;
            }
            Offer(now);
        }

        // Who stands in front of the player: the game's panel over them with Talk, once they have been in front a moment.
        private void Offer(float now)
        {
            PlayerController player = StreetGame.Player;
            Vector3 at = player.transform.position;
            string closed = Closed();
            BaseHuman body = null;
            Pedestrian walker = null;
            StreetFigure kind = StreetFigure.Busy;
            if (closed == null)
            {
                body = StreetGame.Facing(at, player.transform.forward, _offered, now, out walker, out kind);
            }
            else
            {
                _aim.Clear();   // (a gate closed: no grace)
            }
            bool near = closed == null && _offered != null && StreetGame.Near(_offered, _offeredWalker, at);
            bool moved = _aim.Step(body != null ? StreetGame.Key(body) : 0, near, now);
            if (moved || closed != null)
            {
                Aim(body, walker, kind);
            }
            else if (body != null && ReferenceEquals(body, _offered))
            {
                _offeredKind = kind;   // (sat down or stood up: the panel's height follows)
            }
            if (_offered == null || !_aim.Ready())
            {
                Check(now, closed, at, player.transform.forward);
                return;
            }
            WorldPanel panel = _door.Panel;
            float lift = StreetOffer.Lift(_offeredKind);
            panel.Lines(_offeredLine, null, null, WorldPanel.White());
            panel.Button(0, "reallife_street_btn_talk", _onTalk, true, true);
            panel.Button(1, null, null, false, false);
            panel.Button(2, null, null, false, false);
            panel.Button(3, null, null, false, false);
            panel.Follow = _offered.transform;
            panel.FollowLift = lift;
            bool up = _door.Show(_offered.transform.position, at, lift, _doorWhat);
            if (up && !_shown)
            {
                _shown = true;
                if (Meta.Dev.Verbose)
                {
                    Logger?.Info("Street talk check (offer): the box is up over " + What(at, player.transform.forward) + ", after " + (_aim.Seen - _aim.Since).ToString("0.0")
                                 + " s in front (" + StreetRules.Dwell + " s wanted)" + (_aim.InGrace(now) ? ", kept by the grace as they step out of the front" : string.Empty) + ": PASS.");
                }
            }
            else if (!up)
            {
                Check(now, closed, at, player.transform.forward);
            }
        }

        // The aim moved: the panel's words for the new person (or the panel goes), their opening line guessed ahead.
        private void Aim(BaseHuman body, Pedestrian walker, StreetFigure kind)
        {
            if (ReferenceEquals(body, _offered))
            {
                return;
            }
            _offered = body;
            _offeredWalker = walker;
            _offeredKind = kind;
            _offeredLine = null;
            _shown = false;
            if (body == null)
            {
                _door.Hide("nobody in front of the player");
                return;
            }
            _offeredKey = StreetGame.Key(body);
            StreetMemo memo = _memory.Find(_offeredKey, TimeHelper.CurrentDay);
            _offeredLine = memo != null && (memo.Named || memo.Cast >= 0) ? FirstOf(body) : StreetText.T("reallife_street_stranger");
            GuessFor(body);
        }

        // The first gate that keeps the box away, in the order they win: the game's own state, then the jobs, then the social life; null when none.
        private static string Closed()
        {
            string why = StreetGame.NotOnFoot();
            if (why != null)
            {
                return why;
            }
            if (StreetGame.DialogOpen)
            {
                return "the game's dialogue panel is open";
            }
            if (JobDirector.OnShift)
            {
                return "on a job's shift (the job has the key)";
            }
            if (JobDirector.TakesInteractKey)
            {
                return "a job board or an employer's door has the interact key";
            }
            if (SocialDirector.Busy)
            {
                return "out with somebody of the social life";
            }
            if (SocialDirector.TakesInteractKey)
            {
                return "the social life has the interact key (a call, its panel or a scene's prompt)";
            }
            return null;
        }

        // The offer's check line while nothing is offered, on foot out of doors, at most every CheckEvery seconds: which gate is
        // closed, how many people are out, the nearest one's distance, angle and whether they could be talked to.
        private void Check(float now, string closed, Vector3 at, Vector3 forward)
        {
            if (!Meta.Dev.Verbose || now < _nextCheck || !StreetGame.OutOfDoorsOnFoot)
            {
                return;
            }
            _nextCheck = now + CheckEvery;
            string why, verdict;
            if (closed != null)
            {
                why = "closed: " + closed;
                verdict = "PASS";
            }
            else if (_offered != null && !_aim.Ready())
            {
                why = "waiting the " + StreetRules.Dwell + " s in front on " + What(at, forward);
                verdict = "PASS";
            }
            else if (_offered != null)
            {
                why = "the game's box is showing another place (" + What(at, forward) + " is offered as soon as it is free)";
                verdict = "PASS";
            }
            else
            {
                bool shouldHave = StreetGame.NearFound && StreetGame.NearUsable && StreetGame.NearSqr <= StreetRules.Reach * StreetRules.Reach
                                  && StreetGame.NearCos >= StreetRules.Facing;
                why = "nobody who can be talked to within " + StreetRules.Reach + " m and " + StreetOffer.Degrees(StreetRules.Facing) + " deg of the player's facing";
                verdict = shouldHave ? "FAIL (the nearest is in reach and in front and was not offered)" : "PASS";
            }
            string nearest = closed != null ? "not looked for"
                : StreetGame.NearFound ? Mathf.Sqrt(StreetGame.NearSqr).ToString("0.0") + " m away, " + StreetOffer.Degrees(StreetGame.NearCos) + " deg off the facing, " + KindName(StreetGame.NearKind)
                                         + (StreetGame.NearUsable ? ", usable" : ", not usable (hidden, held by another feature or another player's)")
                : "nobody within 12 m";
            Logger?.Info("Street talk check (offer): nothing offered: " + why + "; out: " + StreetGame.LastWalkers + " walker(s), " + StreetGame.LastFigures
                         + " standing or sitting; the nearest: " + nearest + ": " + verdict + ".");
        }

        // The one offered, for a check line: who, what kind, how far and at what angle.
        private string What(Vector3 at, Vector3 forward)
        {
            if (_offered == null)
            {
                return "nobody";
            }
            Vector3 o = _offered.transform.position - at;
            return "'" + _offeredLine + "' (" + KindName(_offeredKind) + ") " + Mathf.Sqrt(o.x * o.x + o.z * o.z).ToString("0.0") + " m away, "
                   + StreetOffer.Degrees(StreetOffer.Cos(o.x, o.z, forward.x, forward.z)) + " deg off the facing";
        }

        private static string KindName(StreetFigure kind)
        {
            return kind == StreetFigure.Walker ? "a walker" : kind == StreetFigure.Standing ? "standing about" : kind == StreetFigure.Seated ? "seated" : "busy";
        }

        // The name somebody gave the player today (they remember having said it).
        private string FirstOf(BaseHuman p)
        {
            StreetStranger s = Who(p, TimeHelper.CurrentDay);
            return s != null ? s.First : StreetText.T("reallife_street_stranger");
        }

        // Who somebody is today, from the save, the day and the game's figure .
        private StreetStranger Who(BaseHuman p, int day)
        {
            if (p == null)
            {
                return null;
            }
            return StreetRules.Identity(StreetGame.SaveSeed(), day, StreetGame.Key(p), StreetGame.Female(p), StreetGame.Age(p), SocialPlaces.Districts,
                -1);
        }

        private void Load(GameInstance save)
        {
            if (_save != null)
            {
                Stop("another save");
            }
            _save = save;
            _memory.Clear();
            string line = null;
            StreetMouth mouth = save.modData != null && save.modData.TryGetValue(StreetStore.Key, out line) ? StreetStore.Read(line) : new StreetMouth();
            if (mouth == null)
            {
                SaveLines.Unreadable(save.modData, StreetStore.Key, "it could not be read");   // (put back as it was read at every save: Core/SaveLines)
                Logger?.Warn("Street talk: the save's line of word of mouth could not be read (" + StreetStore.Key + "); it starts again and the line is kept in the save as it is.");
                mouth = new StreetMouth();
            }
            _mouth = mouth;
            Logger?.Info("Street talk: on in this save" + (mouth.Told.Count > 0 ? "; word of mouth for " + mouth.Told.Count + " told" : string.Empty) + ".");
        }

        private void OnSaveGame()
        {
            if (_save == null || !ReferenceEquals(_save, SaveGameManager.Current) || _save.modData == null)
            {
                return;
            }
            _mouth.Forget(TimeHelper.CurrentDay);
            _save.modData[StreetStore.Key] = StreetStore.Write(_mouth);
        }

        // Everything given back: the person walks on, the panel goes, the dialogue closes if it is ours.
        private void Stop(string why)
        {
            _door.Hide(why);
            _offered = null;
            _offeredWalker = null;
            _aim.Clear();
            if (_chat != null)
            {
                End(why);
            }
            if (_save != null)
            {
                Logger?.Info("Street talk: stopped (" + why + ").");
            }
            _save = null;
        }

        private void Fault(string where, Exception e)
        {
            _failed = true;
            Logger?.Error(e);
            Logger?.Warn("Street talk: switched off for this session after an error in " + where + ". Whoever was stopped walks on.");
            try
            {
                Stop("a fault");
            }
            catch (Exception e2)
            {
                Logger?.Error(e2);
            }
        }

        /// <summary>Per cent more customers for a business of the player's: the word of mouth of the last days (StreetRules.MouthMost at most). For BusinessGame.Ambient, through Wiring/StreetLinks.</summary>
        public static int FootfallOf(BuildingRegistration reg)
        {
            StreetDirector d = Instance;
            if (d == null || reg == null || !Settings.StreetTalk || d._save == null || !GoingPublic.Mine(reg))
            {
                return 0;
            }
            return d._mouth.Percent(StreetGame.AddressKey(reg.Address), TimeHelper.CurrentDay);
        }

        /// <summary>Everything remembered of this save is gone (its line was removed): no word of mouth, nobody remembers today.</summary>
        public void Forget()
        {
            _mouth = new StreetMouth();
            _memory.Clear();
        }
    }
}
