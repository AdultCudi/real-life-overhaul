using BAModAPI;
using RealLifeOverhaul.GameUi;
using UnityEngine;

namespace RealLifeOverhaul.Dynasty
{
    /// <summary>The Legacy app on the phone: the game's frame (DynastyApp) for a player with the RLO interface off, the kit's page (Ui/Ventures/DynastyLive) over it with the interface on.</summary>
    public sealed partial class DynastyDirector
    {
        public const string AppId = "RealLifeDynasty";
        private static readonly string[] AppGlyphs = { "Icon-Family", "Icon-People", "Icon-Person", "Icon-Heart" };

        private PhoneApp _app;
        private DynastyApp _view;
        private bool _gaveUp;

        private void Install()
        {
            if (!PhoneApp.PhoneUp)
            {
                return;   // the game's phone is not built yet: asked again next time, nothing made for it
            }
            var view = new DynastyApp();
            var def = new PhoneAppDef
            {
                Id = AppId,
                TitleKey = "reallife_dynasty_app",
                Icon = like => AppIconPainter.Paint(like, Glyph(), new Color32(120, 150, 210, 255), new Color32(60, 84, 150, 255), 0.42f, "AppIcon-Dynasty"),
                Build = view.Build,
                Opened = view.Opened,
                Closed = view.Closed,
                Kit = () => new RealLifeOverhaul.Ui.VentScreen(RealLifeOverhaul.Ui.DynastyLive.Instance, RealLifeOverhaul.Ui.DynastyLive.ScreenId),
                KitScreen = RealLifeOverhaul.Ui.DynastyLive.ScreenId,
                KitIcon = RealLifeOverhaul.Ui.UiIcon.People
            };
            _app = PhoneApp.Install(def, out bool tryAgain);
            if (_app == null)
            {
                _gaveUp = !tryAgain;
                if (_gaveUp)
                {
                    Logger?.Warn("Dynasty: the game's phone is not built as this version expects, so the Legacy app is left off. With no will the order of the law still decides who carries on.");
                }
                return;
            }
            _view = view;
            RealLifeOverhaul.Ui.UiShell.Face(_app, def);   // the kit's face over the app, once its switch is on
        }

        // The app leaves the phone when the option is switched off or the line is forgotten; the next look puts it back while the option is on.
        private void RemoveApp()
        {
            if (_app != null)
            {
                _app.Remove();
                _app = null;
                _view = null;
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

        /// <summary>Opens the app, for the family side, the tax side, the tests and a click on a notice.</summary>
        public static bool OpenApp() => PhoneApp.OpenById(AppId);

        private static void Open(int tab)
        {
            DynastyDirector d = Instance;
            if (d != null && d._view != null)
            {
                d._view.Show(tab);
            }
            OpenApp();
        }

        /// <summary>Opens the app on the will (the entry on the family side).</summary>
        public static void OpenWill() => Open(0);

        /// <summary>Opens the app on the estate and its statements (the entry on the tax side).</summary>
        public static void OpenEstate() => Open(1);
    }
}
