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
    /// font as the last fallback, the City Ledger's look by the Flare look option (dark, light or the phone's clock), and
    /// once a second the screens it draws over the game's: the Contacts app (ContactsKit) while the switch is on, the
    /// game's own app the moment it is off or anything fails. Once a city it says in Player.log whether it is on, whether
    /// its fonts came in and which screens it drew over. The gallery opens from Options, Tests. Cost: nothing per frame;
    /// a one-second timer comparing two switches and an app's state.
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
            UiChrome.Status = UiGame.Status;
            UiChrome.Bar = UiGame.Bar;
            UiIcons.Own = UiGame.OwnIcon;
            RealLifeOverhaul.GameUi.GameSprites.Want(UiGame.OwnIconNames);
            StartCoroutine(Guard.Routine(Tick()));
        }

        private void OnDestroy()
        {
            ContactsKit.Remove("the mod unloading");
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
                    ContactsKit.Remove("its faults");
                    continue;
                }
                try
                {
                    Slow();
                }
                catch (Exception e)
                {
                    Guard.Fail(FrameGuard, e, this);
                    ContactsKit.Remove("a fault");
                }
            }
        }

        private void Slow()
        {
            if (!_fallbackSet)
            {
                _fallbackSet = UiGame.FallbackFont();
            }
            bool city = SaveGameManager.Current != null && !UI.Load.LoadScene.isLoading;
            if (!city)
            {
                ContactsKit.Remove("no city");
                ContactsKit.Rearm();
                _said = 0;
                _waited = 0;
                return;
            }
            if (!On)
            {
                ContactsKit.Remove("the switch is off");
                if (_said == 0)
                {
                    _said = 1;
                    Logger?.Info("UI: the RLO interface is off (" + (Settings.LivingPopulation ? "its switch is off" : "Living population is off") + "); the game's own screens show.");
                }
                return;
            }
            ContactsKit.Install();
            if (_said != 2 && UiFonts.Ready && (ContactsKit.Settled || ++_waited >= MostWait))
            {
                _said = 2;
                Logger?.Info(Said());
            }
        }

        // The one line a city: on, the fonts, the screens it draws over.
        private static string Said()
        {
            string fonts = UiFonts.Own ? "fonts: the bundle's own (" + UiFonts.Faces + " faces)"
                : UiFonts.Faces > 0 ? "fonts: " + UiFonts.Faces + " of " + UiFonts.FacesWanted + " faces from " + UiFonts.BundleFile + ", the game's font for the rest"
                : "fonts: not loaded from " + (UiFonts.BundleFile ?? "(no path)") + ", the game's font stands in";
            return "UI: the RLO interface is on; " + fonts + "; screens: Contacts (" + ContactsKit.State + "), desk sessions ("
                   + (Settings.LivingDesks ? "over the game's panel as each opens" : "off") + "), and from Options, Tests: the gallery, Contacts, the city's people, Flare in the city, a firm.";
        }
    }
}
