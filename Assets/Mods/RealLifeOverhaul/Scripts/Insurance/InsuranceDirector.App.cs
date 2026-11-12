using System;
using BAModAPI;
using RealLifeOverhaul.GameUi;
using UnityEngine;

namespace RealLifeOverhaul.Insurance
{
    /// <summary>
    /// Insurance's app on the phone: the game's frame (InsuranceApp) for a player with the RLO interface off, the kit's page (Ui/Ventures/InsuranceLive) over it with
    /// the interface on.
    /// </summary>
    public sealed partial class InsuranceDirector
    {
        public const string AppId = "RealLifeInsurance";
        private static readonly string[] AppGlyphs = { "Icon-Security", "Icon-Info", "Icon-Coin" };

        // A notice's click: the app opens on the tab the notice is about (made once, so a notice allocates nothing for its click).
        private static readonly Action OpenCars = () => OpenApp(RealLifeOverhaul.Ui.InsurePages.TabCars);
        private static readonly Action OpenHomes = () => OpenApp(RealLifeOverhaul.Ui.InsurePages.TabHomes);
        private static readonly Action OpenClaims = () => OpenApp(RealLifeOverhaul.Ui.InsurePages.TabClaims);

        private static int _wantTab = -1;
        private PhoneApp _app;
        private bool _gaveUp;

        private void Install()
        {
            if (!PhoneApp.PhoneUp)
            {
                return;   // the game's phone is not built yet: asked again next time, nothing made for it
            }
            var view = new InsuranceApp();
            var def = new PhoneAppDef
            {
                Id = AppId,
                TitleKey = "reallife_ins_app",
                Icon = like => AppIconPainter.Paint(like, Glyph(), new Color32(70, 150, 200, 255), new Color32(30, 96, 150, 255), 0.42f, "AppIcon-Insurance"),
                Build = view.Build,
                Opened = view.Opened,
                Closed = view.Closed,
                Kit = () => new RealLifeOverhaul.Ui.VentScreen(RealLifeOverhaul.Ui.InsuranceLive.Instance, RealLifeOverhaul.Ui.InsuranceLive.ScreenId),
                KitScreen = RealLifeOverhaul.Ui.InsuranceLive.ScreenId,
                KitIcon = RealLifeOverhaul.Ui.UiIcon.ShieldCheck
            };
            _app = PhoneApp.Install(def, out bool tryAgain);
            if (_app == null)
            {
                _gaveUp = !tryAgain;
                if (_gaveUp)
                {
                    Logger?.Warn("Insurance: the game's phone is not built as this version expects, so the Insurance app is left off. A car can still be insured at the Garage's standard tier.");
                }
                return;
            }
            RealLifeOverhaul.Ui.UiShell.Face(_app, def);   // the kit's face over the app, once its switch is on
        }

        // The app leaves the phone when the option is switched off or the line is forgotten; the next look puts it back while the option is on.
        private void RemoveApp()
        {
            if (_app != null)
            {
                _app.Remove();
                _app = null;
            }
        }

        private static Sprite Glyph()
        {
            for (int i = 0; i < AppGlyphs.Length; i++)
            {
                Sprite sprite = GameSprites.Get(AppGlyphs[i]);
                if (sprite != null)
                {
                    return sprite;
                }
            }
            return null;
        }

        /// <summary>Opens the app, on a tab if one is given: for the tests and a notice's click.</summary>
        public static bool OpenApp(int tab = -1)
        {
            _wantTab = tab;
            return PhoneApp.OpenById(AppId);
        }

        /// <summary>The tab a notice or a test asked the app to open on, once; -1 for none.</summary>
        public static int TakeWantedTab()
        {
            int tab = _wantTab;
            _wantTab = -1;
            return tab;
        }
    }
}
