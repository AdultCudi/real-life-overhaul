using BAModAPI;
using RealLifeOverhaul.GameUi;
using UnityEngine;

namespace RealLifeOverhaul.Health
{
    /// <summary>Health's app on the phone: the game's frame (HealthApp) for a player with the RLO interface off, the kit's page (Ui/Ventures/HealthLive) over it with the interface on.</summary>
    public sealed partial class HealthDirector
    {
        public const string AppId = "RealLifeHealth";
        private static readonly string[] AppGlyphs = { "BusinessIcon-Hospital", "Icon-Heart", "Icon-Condition" };

        private PhoneApp _app;
        private bool _gaveUp;

        private void Install()
        {
            if (!PhoneApp.PhoneUp)
            {
                return;   // the game's phone is not built yet: asked again next time, nothing made for it
            }
            var view = new HealthApp();
            var def = new PhoneAppDef
            {
                Id = AppId,
                TitleKey = "reallife_hc_app",
                Icon = like => AppIconPainter.Paint(like, Glyph(), new Color32(90, 190, 170, 255), new Color32(30, 120, 110, 255), 0.42f, "AppIcon-Health"),
                Build = view.Build,
                Opened = view.Opened,
                Closed = view.Closed,
                Kit = () => new RealLifeOverhaul.Ui.VentScreen(RealLifeOverhaul.Ui.HealthLive.Instance, RealLifeOverhaul.Ui.HealthLive.ScreenId),
                KitScreen = RealLifeOverhaul.Ui.HealthLive.ScreenId,
                KitIcon = RealLifeOverhaul.Ui.UiIcon.Heart
            };
            _app = PhoneApp.Install(def, out bool tryAgain);
            if (_app == null)
            {
                _gaveUp = !tryAgain;
                if (_gaveUp)
                {
                    Logger?.Warn("Health: the game's phone is not built as this version expects, so the Health app is left off. The doctor's phone line still books an appointment.");
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

        /// <summary>Opens the app for the tests and a notice's click.</summary>
        public static bool OpenApp() => PhoneApp.OpenById(AppId);
    }
}
