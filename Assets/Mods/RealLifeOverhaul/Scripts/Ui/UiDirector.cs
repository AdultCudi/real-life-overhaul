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

        private bool _fallbackSet;
        private int _said;                 // the interface's line this city: 0 none yet, 1 off, 2 on
        private int _waited;

        /// <summary>The RLO interface is drawn: the living economy's master option and the interface's own switch.</summary>
        public static bool On => Settings.LivingPopulation && Settings.UiInterface;

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
            UiFonts.Begin(Path.Combine(ModRoot ?? string.Empty, UiFonts.BundlePath));
            LoadHold.Add("ui: the interface's fonts", UiFonts.Step, done => { });
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
            UiScreenSwitches.Changed = Guard.Wrap(SwitchesChanged);
            StartCoroutine(Guard.Routine(Tick()));
        }

        // A screen's switch flipped on the options page: its old face back (or the kit's) now, not at the next second.
        private void SwitchesChanged()
        {
            if (!FrameGuard.Off)
            {
                Slow();
            }
        }

        // Every screen the kit draws over given back: the game's own, exactly as found.
        internal static void RemoveAll(string why)
        {
            WorldKits.Remove(why);
            ContactsKit.Remove(why);
            BizManKit.Remove(why);
            EconoViewKit.Remove(why);
            PhoneKit.Remove(why);
            EmployeesKit.Remove(why);
            PersonaKit.Remove(why);
            UniformsKit.Remove(why);
            RivalsKit.Remove(why);
            MarketKit.Remove(why);
            DialogKits.Remove(why);
            UiShell.Remove(why);
            ShellPages.Remove(why);
            KitForOtherMods.Remove(why);
            UiForeignWindows.Release();
            ShellBoxes.Remove(why);
            BuyKits.Remove(why);
            PauseKits.Remove(why);
            TooltipKit.Remove(why);
            BlueprintsKits.Remove(why);
            HelpKits.Remove(why);
            ShellNotices.Remove(why);
            ActivityKit.Remove(why);
            MapKits.Remove(why);
            BuildKits.Remove(why);
            HudLive.Remove(why);
        }

        private void OnDestroy()
        {
            RemoveAll("the mod unloading");
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
                if (FrameGuard.Off)
                {
                    RemoveAll("its faults");
                    continue;
                }
                try
                {
                    Slow();
                }
                catch (Exception e)
                {
                    Guard.Fail(FrameGuard, e, this);
                    RemoveAll("a fault");
                }
            }
        }

        private void Slow()
        {
            using var cost = UiCost.Kit.Time(UiWork.Frame);   // the interface's share of the frame (Host/UiCost)
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
            bool city = SaveGameManager.Current != null && !UI.Load.LoadScene.isLoading;
            if (!city)
            {
                RemoveAll("no city");
                WorldKits.Rearm();
                ContactsKit.Rearm();
                BizManKit.Rearm();
                EconoViewKit.Rearm();
                PhoneKit.Rearm();
                EmployeesKit.Rearm();
                PersonaKit.Rearm();
                UniformsKit.Rearm();
                RivalsKit.Rearm();
                MarketKit.Rearm();
                DialogKits.Rearm();
                UiShell.Rearm();
                ShellPages.Rearm();
                ShellBoxes.Rearm();
                BuyKits.Rearm();
                PauseKits.Rearm();
                TooltipKit.Rearm();
                BlueprintsKits.Rearm();
                HelpKits.Rearm();
                ShellNotices.Rearm();
                ActivityKit.Rearm();
                MapKits.Rearm();
                HudLive.Rearm();
                _said = 0;
                _waited = 0;
                return;
            }
            PlayTime.Tick();   // the city's time played, for the pause menu's card: counted with the interface on or off
            Economy.WorthWatch.Tick();   // the day's net worth figures for the money pill's card and the rich list: taken with the interface on or off
            if (!On)
            {
                RemoveAll("the switch is off");
                if (_said == 0)
                {
                    _said = 1;
                    Logger?.Info("UI: the RLO interface is off (" + (Settings.LivingPopulation ? "its switch is off" : "Living population is off") + "); the game's own screens show.");
                }
                return;
            }
            WorldKits.Install();
            if (UiScreenSwitches.On("contacts"))
            {
                ContactsKit.Install();
            }
            else
            {
                ContactsKit.Remove("its switch is off");
            }
            if (UiScreenSwitches.On("bizman"))
            {
                BizManKit.Install();
            }
            else
            {
                BizManKit.Remove("its switch is off");
            }
            EconoViewKit.Install();
            EmployeesKit.Install();
            PersonaKit.Install();
            UniformsKit.Install();
            RivalsKit.Install();
            MarketKit.Install();
            DialogKits.Install();
            ShellPages.Install();
            KitForOtherMods.Install();
            UiShell.Tick();
            ShellBoxes.Install();
            BuyKits.Install();
            PauseKits.Install();
            TooltipKit.Install();
            BlueprintsKits.Install();
            HelpKits.Install();
            ShellNotices.Install();
            ActivityKit.Install();
            MapKits.Install();
            BuildKits.Install();
            HudLive.Install();
            if (UiScreenSwitches.On("phone"))
            {
                PhoneKit.Install();
            }
            else
            {
                PhoneKit.Remove("its switch is off");
            }
            if (_said != 2 && UiFonts.Ready && (ContactsKit.Settled || ++_waited >= MostWait))
            {
                _said = 2;
                Logger?.Info(Said());
                UiForeignCheck.Run(Logger);   // the self-check: other mods' pieces on the kit's screens, and how each is held
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
                   + BuyKits.States() + ", " + PauseKits.States() + ", " + BlueprintsKits.States() + ", " + HelpKits.States() + ", " + TooltipKit.States()
                   + ", pages (" + ShellPages.States() + "), desk sessions (" + (Settings.LivingDesks ? "over the game's panel as each opens" : "off")
                   + "); the full menu's frame while an app on the kit shows; from Options, Tests: the gallery and each screen.";
        }
    }
}
