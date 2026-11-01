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
    /// game's own app the moment it is off or anything fails. The gallery opens from Options, Tests. Cost: nothing per
    /// frame; a one-second timer comparing two switches and an app's state.
    /// </summary>
    public sealed partial class UiDirector : MonoBehaviour
    {
        private static readonly FeatureGuard FrameGuard = Guard.For("ui", "UiDirector", typeof(UiDirector));

        public static IModLogger Logger;
        public static string ModRoot;
        public static UiDirector Instance { get; private set; }

        private bool _fallbackSet;

        /// <summary>The RLO interface is drawn: the living economy's master option and the interface's own switch.</summary>
        public static bool On => Settings.LivingPopulation && Settings.UiInterface;

        /// <summary>The look now: the Flare look option, which the RLO interface shares (dark, light, or the phone's clock).</summary>
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
            if (!On || SaveGameManager.Current == null || UI.Load.LoadScene.isLoading)
            {
                ContactsKit.Remove(On ? "no city" : "the switch is off");
                return;
            }
            ContactsKit.Install();
        }
    }
}
