using System;
using System.Collections;
using System.IO;
using BAModAPI;
using RealLifeOverhaul.GameUi;
using UnityEngine;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The RLO interface in the game: its fonts read behind the loading screen, its one pool of tweens, the game's own
    /// font as the last fallback, and once a second the screens it draws over the game's, each while the interface and its
    /// own switch are on: the Contacts app (ContactsKit), EconoView (EconoViewKit), My Employees and a rival's staff (EmployeesKit), Persona,
    /// Rivals and Market Insider (PersonaKit, RivalsKit, MarketKit), the full menu's frame and the mod apps' faces (UiShell, with
    /// the overhaul's pages, ShellPages), the boxes (ShellBoxes, ShellModBox) and the notices (ShellNotices); the game's own
    /// screen the moment a switch is off or anything fails. A switch flipped mid screen gives the old face back at once.
    /// Each piece of the second's look runs in a try of its own (UiPieces): one that fails is taken off alone, said once with
    /// what failed, and asked again in the next city, so no screen's fault is the whole interface's. With the interface
    /// switched off the look takes every kit off once and then only counts the time played and the day's net worth.
    /// When the interface's part stops (the guard switched it off after faults, or the game lacks what its hosts name),
    /// <see cref="On"/> is false for everyone who would draw a kit screen, and everything the kit holds over the screen is let
    /// go (<see cref="LetGoAll"/>, Core/ScreenHolds).
    /// Once a city it says in Player.log whether it is on, whether its fonts came in and which screens it drew over. The
    /// gallery opens from Options, Tests. Cost: nothing per frame; a one-second timer comparing the switches and the screens' states.
    /// </summary>
    public sealed partial class UiDirector : MonoBehaviour
    {
        private static readonly FeatureGuard FrameGuard = Guard.For("ui", "UiDirector", typeof(UiDirector));

        public static IModLogger Logger;
        public static string ModRoot;
        public static UiDirector Instance { get; private set; }

        private const int MostWait = 20;   // seconds after a city loads before the line is said with the Contacts app still not ready
        private const int NoticesMost = 2; // notices a session for a piece taken off after its fault: the first names it, a run of them is not a run of toasts

        // The kits the second's look puts on, in the order it asks them (InstallOne, RemoveOne and RearmOne name them by these numbers).
        private static class Kit
        {
            public const int World = 0, Contacts = 1, BizMan = 2, EconoView = 3, Employees = 4, Persona = 5, Uniforms = 6, Rivals = 7, Market = 8, Dialog = 9, Pages = 10,
                OtherMods = 11, Shell = 12, Boxes = 13, Buy = 14, Pause = 15, Tooltips = 16, Blueprints = 17, Help = 18, Options = 19, Notices = 20, Activity = 21, Map = 22,
                Build = 23, Designer = 24, Hud = 25, Phone = 26, Count = 27;
        }

        // Taken off in the order they always were: the phone before what stands on it, the frame before its pages, the HUD last.
        private static readonly int[] RemoveOrder =
        {
            Kit.World, Kit.Contacts, Kit.BizMan, Kit.EconoView, Kit.Phone, Kit.Employees, Kit.Persona, Kit.Uniforms, Kit.Rivals, Kit.Market, Kit.Dialog, Kit.Shell,
            Kit.Pages, Kit.OtherMods, Kit.Boxes, Kit.Buy, Kit.Pause, Kit.Tooltips, Kit.Blueprints, Kit.Help, Kit.Options, Kit.Notices, Kit.Activity, Kit.Map, Kit.Build,
            Kit.Designer, Kit.Hud,
        };

        private static readonly int[] PieceOf = MakePieces();
        private static int _fontsPiece, _playedPiece, _worthPiece, _linePiece;
        private static int _noticed;       // notices given this session for pieces taken off
        /// <summary>What the kits are told when the player's switch is turned off in a session (not a stop): the build face and the like may keep what is up.</summary>
        internal const string SwitchOffWhy = "the switch is off";
        private static bool _letGo;       // everything was let go for a part that stopped: once, until the part runs again

        private bool _fallbackSet;
        private int _said;                 // the interface's line this city: 0 none yet, 1 off, 2 on
        private int _waited;

        /// <summary>The player's switch: the living economy's master option and the interface's own.</summary>
        public static bool Wanted => Settings.LivingPopulation && Settings.UiInterface;

        /// <summary>
        /// True once the interface cannot run in this session: the guard switched its part off after faults, or the game lacks a
        /// member its hosts name. Nothing of the kit is drawn then, whatever the switch says.
        /// </summary>
        public static bool Stopped => FrameGuard.Off || UiHost.Stopped;

        /// <summary>
        /// The RLO interface is drawn: the player's switch is on and the interface's part runs. Everyone who would draw a kit
        /// screen asks this, so a screen is never opened on an interface that stopped: it would be laid out once and answer nothing.
        /// </summary>
        public static bool On => Wanted && !Stopped;

        /// <summary>The HUD's and the world's pieces, for their own hosts to say a fault of theirs (they stand up by themselves each second).</summary>
        internal static int HudPiece => PieceOf[Kit.Hud];

        internal static int WorldPiece => PieceOf[Kit.World];

        internal static int BuildPiece => PieceOf[Kit.Build];

        /// <summary>
        /// The Flare look option (dark, light, or the phone's clock). The RLO interface has one dark look now, so its screens
        /// pass this on but no colour depends on it; Flare's own pages still follow it.
        /// </summary>
        public static bool Dark()
        {
            int hour = SaveGameManager.Current != null ? SaveGameManager.Current.Hour : 12;
            return Flare.FlareThemeRules.IsDark(Settings.FlareTheme, hour);
        }

        public static UiMotionSetting Motion() => (UiMotionSetting)Mathf.Clamp(Settings.UiMotion, 0, 2);

        private void Awake()
        {
            Instance = this;
            UiFonts.Said = line => Logger?.Warn(line);
            UiReentry.Said = line => Logger?.Warn(line);   // a call refused for coming back into itself: once a session at each place, with its stack
            UiFonts.Begin(Path.Combine(ModRoot ?? string.Empty, UiFonts.BundlePath));
            LoadHold.Add("ui: the interface's fonts", UiFonts.Step, done => { });
            LoadHold.Add("ui: the interface's fallback fonts", FontsStep, done => { });
            if (UiTweens.Instance == null)
            {
                UiTweens.Instance = gameObject.AddComponent<UiTweens>();
            }
            UiGameWindowPrefs.Start();
            UiChrome.Status = UiGame.Status;
            UiChrome.Bar = HudStatusGame.Bar;   // the game's time, day, money and apps (UiGame.Bar), then the HUD's pills
            UiIcons.Own = UiGame.OwnIcon;
            HudLive.ActivityStrip = () => ActivityKit.Strip;   // the activity panel's strip: the HUD keeps clear of it and never draws it twice
            RealLifeOverhaul.GameUi.GameSprites.Want(UiGame.OwnIconNames);
            UiForeignGame.Start(line => Logger?.Info(line), line => Logger?.Warn(line));
            UiShell.Start();
            ShellModBox.Start();
            Holds();
            UiScreenSwitches.Changed = Guard.Wrap(SwitchesChanged);
            StartCoroutine(Guard.Routine(Tick()));
        }

        // What the interface can leave over the screen, told to the guard once (Core/ScreenHolds), and the plain words of the
        // places whose guards can switch the interface off, for the guard's notice.
        private static void Holds()
        {
            ScreenHolds.Add("ui", "the RLO interface's screens, cards and canvases", () => LetGoAll("its part was switched off"), UiRoots.AnyStanding);
            Guard.Named("UiHost", () => Words(UiHost.LastFault != null ? UiScreenSwitches.LabelOf(UiHost.LastFault) : null));
            Guard.Named("HudLive", () => Words(UiPieces.Label(PieceOf[Kit.Hud])));
            Guard.Named("HudHost", () => Words(UiPieces.Label(PieceOf[Kit.Hud])));
            Guard.Named("WorldHost", () => Words(UiPieces.Label(PieceOf[Kit.World])));
            Guard.Named("PhoneKit", () => Words(UiPieces.Label(PieceOf[Kit.Phone])));
            Guard.Named("PhoneKitWatch", () => Words(UiPieces.Label(PieceOf[Kit.Phone])));
            Guard.Named("TooltipHost", () => Words(UiPieces.Label(PieceOf[Kit.Tooltips])));
            Guard.Named("ShellNotices", () => Words(UiPieces.Label(PieceOf[Kit.Notices])));
            Guard.Named("ItemPanelKit", () => Words(UiPieces.Label(PieceOf[Kit.Buy])));
            Guard.Named("ItemPanelKeeper", () => Words(UiPieces.Label(PieceOf[Kit.Buy])));
        }

        // A name in the player's language by its key, or null when the game has no text for it (a screen with no switch of its own).
        private static string Words(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return null;
            }
            string words = UiText.T(key);
            return string.IsNullOrEmpty(words) || string.Equals(words, key, StringComparison.Ordinal) ? null : words;
        }

        // Each kit's piece (its name for Player.log, the key of its name for a notice), and the look's own pieces.
        private static int[] MakePieces()
        {
            var of = new int[Kit.Count];
            of[Kit.World] = UiPieces.Id("the world's boxes and smaller windows", "reallife_opt_sec_uiscreens_hud");
            of[Kit.Contacts] = UiPieces.Id("Contacts", "reallife_ui_screen_contacts");
            of[Kit.BizMan] = UiPieces.Id("BizMan", "reallife_ui_screen_bizman");
            of[Kit.EconoView] = UiPieces.Id("EconoView", "reallife_ui_screen_econoview");
            of[Kit.Employees] = UiPieces.Id("My Employees and a rival's staff", "reallife_ui_screen_my_employees");
            of[Kit.Persona] = UiPieces.Id("Persona", "reallife_ui_screen_persona");
            of[Kit.Uniforms] = UiPieces.Id("the uniform presets", "reallife_ui_screen_uniforms");
            of[Kit.Rivals] = UiPieces.Id("Rivals", "reallife_ui_screen_rivals");
            of[Kit.Market] = UiPieces.Id("Market Insider", "reallife_ui_screen_market_insider");
            of[Kit.Dialog] = UiPieces.Id("the conversations", "reallife_ui_screen_dialog");
            of[Kit.Pages] = UiPieces.Id("the overhaul's pages", "reallife_opt_sec_uiscreens_apps");
            of[Kit.OtherMods] = UiPieces.Id("other mods' registered apps", "reallife_opt_sec_uiscreens_apps");
            of[Kit.Shell] = UiPieces.Id("the full menu's frame", "reallife_opt_sec_uiscreens_apps");
            of[Kit.Boxes] = UiPieces.Id("the boxes", "reallife_opt_sec_uiscreens_boxes");
            of[Kit.Buy] = UiPieces.Id("buying and carrying", "reallife_opt_sec_uiscreens_buy");
            of[Kit.Pause] = UiPieces.Id("the pause menu", "reallife_ui_screen_pause_menu");
            of[Kit.Tooltips] = UiPieces.Id("the tooltips", "reallife_ui_screen_tooltips");
            of[Kit.Blueprints] = UiPieces.Id("the blueprints window", "reallife_ui_screen_blueprints");
            of[Kit.Help] = UiPieces.Id("the help window", "reallife_opt_sec_uiscreens_pause");
            of[Kit.Options] = UiPieces.Id("the game's options", "reallife_ui_screen_options_city");
            of[Kit.Notices] = UiPieces.Id("the notices", "reallife_ui_screen_notice_cards");
            of[Kit.Activity] = UiPieces.Id("activities and time", "reallife_opt_sec_uiscreens_activity");
            of[Kit.Map] = UiPieces.Id("the city map's panels", "reallife_opt_sec_uiscreens_map");
            of[Kit.Build] = UiPieces.Id("build mode", "reallife_ui_screen_build_mode");
            of[Kit.Designer] = UiPieces.Id("the interior designer", "reallife_ui_screen_designer");
            of[Kit.Hud] = UiPieces.Id("the status bar over the city", "reallife_ui_screen_hud");
            of[Kit.Phone] = UiPieces.Id("the phone", "reallife_ui_screen_phone");
            _fontsPiece = UiPieces.Id("the interface's fonts");
            _playedPiece = UiPieces.Id("the count of the time played");
            _worthPiece = UiPieces.Id("the days of net worth");
            _linePiece = UiPieces.Id("the line of other mods' apps");
            return of;
        }

        // A screen's switch flipped on the options page: its old face back (or the kit's) now, not at the next second.
        private void SwitchesChanged()
        {
            if (!Stopped)
            {
                Slow();
            }
        }

        /// <summary>
        /// Every screen the kit draws over given back: the game's own, exactly as found. Each kit in a try of its own, so one
        /// that cannot give its screen back keeps no other from doing so.
        /// </summary>
        internal static void RemoveAll(string why)
        {
            for (int i = 0; i < RemoveOrder.Length; i++)
            {
                try
                {
                    RemoveOne(RemoveOrder[i], why);
                }
                catch (Exception e)
                {
                    Logger?.Warn("UI: " + UiPieces.Name(PieceOf[RemoveOrder[i]]) + " could not be given back whole (" + FaultWords.Of(e) + ").");
                }
            }
        }

        // One kit's screens the game's own again.
        private static void RemoveOne(int kit, string why)
        {
            switch (kit)
            {
                case Kit.World: WorldKits.Remove(why); break;
                case Kit.Contacts: ContactsKit.Remove(why); break;
                case Kit.BizMan: BizManKit.Remove(why); break;
                case Kit.EconoView: EconoViewKit.Remove(why); break;
                case Kit.Phone: PhoneKit.Remove(why); break;
                case Kit.Employees: EmployeesKit.Remove(why); break;
                case Kit.Persona: PersonaKit.Remove(why); break;
                case Kit.Uniforms: UniformsKit.Remove(why); break;
                case Kit.Rivals: RivalsKit.Remove(why); break;
                case Kit.Market: MarketKit.Remove(why); break;
                case Kit.Dialog: DialogKits.Remove(why); break;
                case Kit.Shell: UiShell.Remove(why); break;
                case Kit.Pages: ShellPages.Remove(why); break;
                case Kit.OtherMods:
                    KitForOtherMods.Remove(why);
                    UiForeignWindows.Release();
                    break;
                case Kit.Boxes: ShellBoxes.Remove(why); break;
                case Kit.Buy: BuyKits.Remove(why); break;
                case Kit.Pause: PauseKits.Remove(why); break;
                case Kit.Tooltips: TooltipKit.Remove(why); break;
                case Kit.Blueprints: BlueprintsKits.Remove(why); break;
                case Kit.Help: HelpKits.Remove(why); break;
                case Kit.Options: OptionsKits.Remove(why); break;
                case Kit.Notices: ShellNotices.Remove(why); break;
                case Kit.Activity: ActivityKit.Remove(why); break;
                case Kit.Map: MapKits.Remove(why); break;
                case Kit.Build: BuildKits.Remove(why); break;
                case Kit.Designer: DesignerKits.Remove(why); break;
                case Kit.Hud: HudLive.Remove(why); break;
            }
        }

        // One kit put on, or the game's own while its switch is off.
        private static void InstallOne(int kit)
        {
            switch (kit)
            {
                case Kit.World: WorldKits.Install(); break;
                case Kit.Contacts:
                    if (UiScreenSwitches.On("contacts"))
                    {
                        ContactsKit.Install();
                    }
                    else
                    {
                        ContactsKit.Remove("its switch is off");
                    }
                    break;
                case Kit.BizMan:
                    if (UiScreenSwitches.On("bizman"))
                    {
                        BizManKit.Install();
                    }
                    else
                    {
                        BizManKit.Remove("its switch is off");
                    }
                    break;
                case Kit.EconoView: EconoViewKit.Install(); break;
                case Kit.Employees: EmployeesKit.Install(); break;
                case Kit.Persona: PersonaKit.Install(); break;
                case Kit.Uniforms: UniformsKit.Install(); break;
                case Kit.Rivals: RivalsKit.Install(); break;
                case Kit.Market: MarketKit.Install(); break;
                case Kit.Dialog: DialogKits.Install(); break;
                case Kit.Pages: ShellPages.Install(); break;
                case Kit.OtherMods: KitForOtherMods.Install(); break;
                case Kit.Shell: UiShell.Tick(); break;
                case Kit.Boxes: ShellBoxes.Install(); break;
                case Kit.Buy: BuyKits.Install(); break;
                case Kit.Pause: PauseKits.Install(); break;
                case Kit.Tooltips: TooltipKit.Install(); break;
                case Kit.Blueprints: BlueprintsKits.Install(); break;
                case Kit.Help: HelpKits.Install(); break;
                case Kit.Options: OptionsKits.Install(); break;
                case Kit.Notices: ShellNotices.Install(); break;
                case Kit.Activity: ActivityKit.Install(); break;
                case Kit.Map: MapKits.Install(); break;
                case Kit.Build: BuildKits.Install(); break;
                case Kit.Designer: DesignerKits.Install(); break;
                case Kit.Hud: HudLive.Install(); break;
                case Kit.Phone:
                    if (UiScreenSwitches.On("phone"))
                    {
                        PhoneKit.Install();
                    }
                    else
                    {
                        PhoneKit.Remove("its switch is off");
                    }
                    break;
            }
        }

        // A new city: one kit's screens looked for again.
        private static void RearmOne(int kit)
        {
            switch (kit)
            {
                case Kit.World: WorldKits.Rearm(); break;
                case Kit.Contacts: ContactsKit.Rearm(); break;
                case Kit.BizMan: BizManKit.Rearm(); break;
                case Kit.EconoView: EconoViewKit.Rearm(); break;
                case Kit.Phone: PhoneKit.Rearm(); break;
                case Kit.Employees: EmployeesKit.Rearm(); break;
                case Kit.Persona: PersonaKit.Rearm(); break;
                case Kit.Uniforms: UniformsKit.Rearm(); break;
                case Kit.Rivals: RivalsKit.Rearm(); break;
                case Kit.Market: MarketKit.Rearm(); break;
                case Kit.Dialog: DialogKits.Rearm(); break;
                case Kit.Shell: UiShell.Rearm(); break;
                case Kit.Pages: ShellPages.Rearm(); break;
                case Kit.OtherMods: UiForeignCheck.Rearm(); break;
                case Kit.Boxes: ShellBoxes.Rearm(); break;
                case Kit.Buy: BuyKits.Rearm(); break;
                case Kit.Pause: PauseKits.Rearm(); break;
                case Kit.Tooltips: TooltipKit.Rearm(); break;
                case Kit.Blueprints: BlueprintsKits.Rearm(); break;
                case Kit.Help: HelpKits.Rearm(); break;
                case Kit.Options: OptionsKits.Rearm(); break;
                case Kit.Notices: ShellNotices.Rearm(); break;
                case Kit.Activity: ActivityKit.Rearm(); break;
                case Kit.Map: MapKits.Rearm(); break;
                case Kit.Designer: DesignerKits.Rearm(); break;
                case Kit.Hud: HudLive.Rearm(); break;
            }
        }

        /// <summary>
        /// A piece of the look failed: it is off from now and not asked again until the next city (UiPieces), its kit gives its
        /// screens back, and Player.log says once what failed. The rest of the interface goes on, and the guard does not count it:
        /// one screen's fault is never the whole interface's. A game that lacks what the piece names is said by the guard's own line.
        /// </summary>
        internal static void PieceFailed(int piece, Exception e)
        {
            int kit = -1;
            for (int i = 0; i < Kit.Count; i++)
            {
                kit = PieceOf[i] == piece ? i : kit;   // the look's own pieces (the fonts, the counts) have no kit
            }
            bool was = UiPieces.Trip(piece);
            try
            {
                if (was && !Guard.StandsDown(e, "the RLO interface's piece '" + UiPieces.Name(piece) + "'"))
                {
                    Logger?.Warn("UI: " + UiPieces.Name(piece) + " failed (" + FaultWords.Of(e) + "), so it is the game's own until "
                                 + (UiPieces.Spent(piece) ? "the game is started again" : "the next city is loaded") + "; the rest of the RLO interface goes on.");
                    Logger?.Error(e);
                    Tell(piece);
                }
            }
            catch (Exception)
            {
                // saying it must never be the next fault
            }
            if (kit >= 0)
            {
                try
                {
                    RemoveOne(kit, "a fault");
                }
                catch (Exception again)
                {
                    Logger?.Warn("UI: " + UiPieces.Name(piece) + " could not be given back whole (" + FaultWords.Of(again) + ").");
                }
            }
        }

        /// <summary>
        /// A piece whose face failed where its host already said the fault (a modal piece of the HUD's): off until the next city
        /// like any piece that failed, its kit giving its screens back.
        /// </summary>
        internal static void PieceLost(int piece, string why)
        {
            if (!UiPieces.Trip(piece))
            {
                return;
            }
            Logger?.Warn("UI: " + UiPieces.Name(piece) + " is the game's own until " + (UiPieces.Spent(piece) ? "the game is started again" : "the next city is loaded")
                         + " (" + why + "); the rest of the RLO interface goes on.");
            Tell(piece);
            for (int i = 0; i < Kit.Count; i++)
            {
                if (PieceOf[i] != piece)
                {
                    continue;
                }
                try
                {
                    RemoveOne(i, why);
                }
                catch (Exception e)
                {
                    Logger?.Warn("UI: " + UiPieces.Name(piece) + " could not be given back whole (" + FaultWords.Of(e) + ").");
                }
            }
        }

        // The player is told which screen went back to the game's own, the first times in a session; a piece with no name of its own is not a screen.
        private static void Tell(int piece)
        {
            string what = Words(UiPieces.Label(piece));
            if (what == null || _noticed >= NoticesMost)
            {
                return;
            }
            _noticed++;
            Guard.Tell(UiText.T("reallife_ui_note_piece_off").Replace("{what}", what));
        }

        /// <summary>
        /// Everything the interface holds over the screen let go at once, each in a try of its own: every kit gives its screen back,
        /// every host there is too, the cards on canvases of their own close, whatever was fading in lands, and any canvas of the
        /// kit's still standing is switched off. For the guard switching the part off, the hosts stopping, and the city going.
        /// </summary>
        internal static void LetGoAll(string why)
        {
            RemoveAll(why);
            Quietly(() => UiGameHost.RemoveEvery(why));
            CloseCards();
            Quietly(() => HudHost.Remove(why));
            Quietly(() => UiTweens.Instance?.Land());
            int hidden = 0;
            Quietly(() => hidden = UiRoots.HideAll());
            if (hidden > 0)
            {
                Logger?.Warn("UI: " + hidden + " canvas(es) of the RLO interface still stood and were switched off (" + why + "), so nothing of it takes a click.");
            }
        }

        /// <summary>
        /// The cards the kit stands on canvases of their own, closed: the desk session's card (which the game's panel is then seen under again), the report
        /// card, the mod box and the viewfinder. For a guard stop, the unloading, and the switch turned off in a session.
        /// </summary>
        private static void CloseCards()
        {
            Quietly(() => SessionKit.Hide(null));
            Quietly(BugReportBox.Close);
            Quietly(ShellModBox.Close);
            Quietly(ViewfinderHost.Hide);
        }

        private static void Quietly(Action work)
        {
            try
            {
                work();
            }
            catch (Exception e)
            {
                Logger?.Warn("UI: a piece of the RLO interface could not be let go of whole (" + FaultWords.Of(e) + ").");
            }
        }

        private void OnDestroy()
        {
            LetGoAll("the mod unloading");
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private IEnumerator Tick()
        {
            var second = new WaitForSecondsRealtime(1f);
            while (true)
            {
                yield return second;
                if (Stopped)
                {
                    if (!_letGo)
                    {
                        _letGo = true;
                        LetGoAll("its part stopped");   // once: the guard's own switch-off let go already, a host stopped for a missing member did not
                    }
                    continue;
                }
                _letGo = false;
                try
                {
                    Slow();
                }
                catch (Exception e)
                {
                    Guard.Fail(FrameGuard, e, this);
                }
            }
        }

        private void Slow()
        {
            using var cost = UiCost.Kit.Time(UiWork.Frame);   // the interface's share of the frame (Host/UiCost)
            if (On && !UiPieces.Off(_fontsPiece))
            {
                try
                {
                    if (!_fallbackSet)
                    {
                        _fallbackSet = UiGame.FallbackFont();
                    }
                    if (!UiFonts.Ready && !LoadHold.Holding && UiFonts.Step())
                    {
                        // The loading screen's hold lasts at most two seconds and then forgets its pieces: fonts still reading by then
                        // never came in, and the HUD and the line below waited on them all session. Their read goes on here instead.
                        Logger?.Info("UI: the interface's fonts came in after the loading screen (its hold ended first).");
                    }
                    ShadersLook();
                    FontsStep();
                }
                catch (Exception e)
                {
                    PieceFailed(_fontsPiece, e);
                }
            }
            bool city = SaveGameManager.Current != null && !UI.Load.LoadScene.isLoading;
            if (!city)
            {
                RemoveAll("no city");
                for (int i = 0; i < RemoveOrder.Length; i++)
                {
                    try
                    {
                        RearmOne(RemoveOrder[i]);
                    }
                    catch (Exception e)
                    {
                        PieceFailed(PieceOf[RemoveOrder[i]], e);
                    }
                }
                UiPieces.Rearm();   // a piece taken off after a fault is asked again in the next city, while it has a try left
                _said = 0;
                _waited = 0;
                return;
            }
            // The city's time played, for the pause menu's card, and the day's net worth figures for the money pill's card and the
            // rich list: counted with the interface on or off, each in a try of its own.
            if (!UiPieces.Off(_playedPiece))
            {
                try
                {
                    PlayTime.Tick();
                }
                catch (Exception e)
                {
                    PieceFailed(_playedPiece, e);
                }
            }
            if (!UiPieces.Off(_worthPiece))
            {
                try
                {
                    Economy.WorthWatch.Tick();
                }
                catch (Exception e)
                {
                    PieceFailed(_worthPiece, e);
                }
            }
            if (!On)
            {
                if (_said != 1)
                {
                    // Once, as it goes off or the city comes with it off: while it stays off nothing of the kit is asked, made or looked for.
                    _said = 1;
                    RemoveAll(SwitchOffWhy);
                    CloseCards();   // the cards stand on canvases of their own: a switch turned off in a session takes them away with the rest
                    Logger?.Info("UI: the RLO interface is off (" + (Settings.LivingPopulation ? "its switch is off" : "Living population is off") + "); the game's own screens show.");
                }
                return;
            }
            for (int kit = 0; kit < Kit.Count; kit++)
            {
                if (UiPieces.Off(PieceOf[kit]))
                {
                    continue;
                }
                try
                {
                    InstallOne(kit);
                }
                catch (Exception e)
                {
                    PieceFailed(PieceOf[kit], e);
                }
            }
            if (_said != 2 && UiFonts.Ready && (ContactsKit.Settled || ++_waited >= MostWait))
            {
                _said = 2;
                try
                {
                    Logger?.Info(Said());
                }
                catch (Exception e)
                {
                    Logger?.Warn("UI: the RLO interface is on; its line of the screens could not be written (" + FaultWords.Of(e) + ").");
                }
                UiForeignCheck.Run(Logger);   // the self-check: other mods' pieces on the kit's screens, and how each is held
            }
            if (_said == 2 && !UiPieces.Off(_linePiece))
            {
                try
                {
                    UiForeignCheck.Apps(Logger);   // other mods' apps by where the player finds them: once, and again when a mod adds one late
                }
                catch (Exception e)
                {
                    PieceFailed(_linePiece, e);
                }
            }
        }

        // The check of the shaders the interface's text is drawn with (UiFonts.Mend): its line once the fonts are read, and again
        // only if what it found changes. Written whatever Developer diagnostics says, since a report of pink or missing text is
        // answered by this one line of a player's log; a face left without a shader that draws is a warning.
        private static void ShadersLook()
        {
            if (UiFonts.Watch())
            {
                Logger?.Warn("UI: the shader the interface's text was drawn with went with what held it; the faces were given a text shader of the game's again.");
            }
            if (!UiFonts.ShadersNews)
            {
                return;
            }
            string line = UiFonts.ShadersLine(GameBuild.Running);
            if (UiFonts.ShadersPass)
            {
                Meta.Dev.Always(Logger, line);
            }
            else
            {
                Logger?.Warn(line);
            }
        }

        // The one line a city: on, the fonts, the screens it draws over.
        private static string Said()
        {
            string fonts = UiFonts.Own ? "fonts: the bundle's own (" + UiFonts.Faces + " faces)"
                : UiFonts.Faces > 0 ? "fonts: " + UiFonts.Faces + " of " + UiFonts.FacesWanted + " faces from " + UiFonts.BundleFile + ", the game's font for the rest"
                : "fonts: not loaded from " + (UiFonts.BundleFile ?? "(no path)") + ", the game's font stands in";
            return "UI: the RLO interface is on; " + fonts + "; screens: Contacts (" + ContactsKit.State + "), BizMan (" + BizManKit.State + "), "
                   + WorldKits.State + ", "
                   + EconoViewKit.State + ", the phone (" + PhoneKit.State + "), " + EmployeesKit.State + ", " + PersonaKit.State + ", " + UniformsKit.State + ", " + RivalsKit.State + ", "
                   + MarketKit.State + ", " + DialogKits.State + ", "
                   + ShellBoxes.States() + ", " + ShellNotices.States() + ", " + ActivityKit.States() + ", " + MapKits.States() + ", the HUD (" + HudLive.State + "), "
                   + BuyKits.States() + ", " + PauseKits.States() + ", " + BlueprintsKits.States() + ", " + HelpKits.States()
                   + ", " + TooltipKit.States() + ", " + OptionsKits.States() + ", the interior designer (" + DesignerKits.State + ")"
                   + ", pages (" + ShellPages.States() + "), desk sessions (" + (Settings.LivingDesks ? "over the game's panel as each opens" : "off")
                   + "); pieces taken off after a fault: " + UiPieces.OffNames()
                   + "; the full menu's frame while an app on the kit shows; from Options, Tests: the gallery and each screen.";
        }
    }
}
