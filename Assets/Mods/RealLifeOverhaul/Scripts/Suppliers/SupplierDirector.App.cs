using RealLifeOverhaul.GameUi;
using UnityEngine;

namespace RealLifeOverhaul.Suppliers
{
    /// <summary>
    /// The Suppliers app on the phone: the game's frame (SupplierApp) for a player with the RLO interface off, the kit's page (Ui/Ventures/SupplyLive) over it with the
    /// interface on.
    /// </summary>
    public sealed partial class SupplierDirector
    {
        public const string AppId = "RealLifeSuppliers";
        private static readonly string[] AppGlyphs = { "Icon-Truck", "Icon-Box", "Icon-Package" };

        private PhoneApp _app;
        private bool _gaveUp;

        /// <summary>The place a page opens on next: BizMan's Deliveries page asks for the business it shows. Read once.</summary>
        public static string WantPlace;

        private void Install()
        {
            if (!PhoneApp.PhoneUp)
            {
                return;   // the game's phone is not built yet: asked again next time, nothing made for it
            }
            var view = new SupplierApp();
            var def = new PhoneAppDef
            {
                Id = AppId,
                TitleKey = "reallife_supply_app",
                Icon = like => AppIconPainter.Paint(like, Glyph(), new Color32(96, 150, 210, 255), new Color32(44, 92, 150, 255), 0.42f, "AppIcon-Suppliers"),
                Build = view.Build,
                Opened = view.Opened,
                Closed = view.Closed,
                Kit = () => new RealLifeOverhaul.Ui.VentScreen(RealLifeOverhaul.Ui.SupplyLive.Instance, RealLifeOverhaul.Ui.SupplyLive.ScreenId),
                KitScreen = RealLifeOverhaul.Ui.SupplyLive.ScreenId,
                KitIcon = RealLifeOverhaul.Ui.UiIcon.Truck
            };
            _app = PhoneApp.Install(def, out bool tryAgain);
            if (_app == null)
            {
                _gaveUp = !tryAgain;
                if (_gaveUp)
                {
                    Logger?.Warn("Suppliers: the game's phone is not built as this version expects, so the Suppliers app is left off. Contracts already signed go on.");
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

        /// <summary>Opens the app, for a notice's click, the tests and BizMan's Deliveries page (which names the business to open on).</summary>
        public static bool OpenApp(string place = null)
        {
            WantPlace = place;
            return PhoneApp.OpenById(AppId);
        }
    }
}
