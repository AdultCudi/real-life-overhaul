using RealLifeOverhaul.GameUi;
using UnityEngine;

namespace RealLifeOverhaul.Outlook
{
    /// <summary>Part of the Economy app's director: the app on the game's phone, and the ways other features open it.</summary>
    public sealed partial class OutlookDirector
    {
        // The icon is the game's tile with a glyph of the app's own (OutlookIcon); these are the game's, for a game that lacks the tile's read-back.
        private static readonly string[] AppGlyphs = { "Icon-Neighborhood", "Icon-Statistics", "Icon-Graph", "Icon-Coin" };

        private OutlookApp _view;
        private PhoneApp _app;
        private bool _gaveUp;

        /// <summary>The Economy app, while it is on the phone.</summary>
        internal OutlookApp View => _view;

        /// <summary>True while the app is on the phone.</summary>
        public bool Installed => _app != null && _app.Installed;

        // Once a second until it is on, since the game's phone is built a little after the city.
        private void Install()
        {
            if (!PhoneApp.PhoneUp)
            {
                return;   // the game's phone is not built yet: asked again next time, nothing made for it
            }
            var view = new OutlookApp(this);
            _app = PhoneApp.Install(new PhoneAppDef
            {
                Id = AppId,
                TitleKey = "reallife_econapp_app",
                Icon = like => AppIconPainter.Paint(like, OutlookIcon.Glyph() ?? Glyph(), new Color32(84, 226, 204, 255), new Color32(10, 124, 114, 255), 0.46f, "AppIcon-Economy"),
                Build = view.Build,
                Opened = view.Opened,
                Closed = view.Closed
            }, out bool tryAgain);
            if (_app == null)
            {
                _gaveUp = !tryAgain;
                if (_gaveUp)
                {
                    Logger?.Warn("Economy app: the game's phone is not built as this version expects, so the Economy app is left off; the city's economy still runs.");
                }
                return;
            }
            _view = view;
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

        private void RemoveApp()
        {
            if (_app != null)
            {
                _app.Remove();
                _app = null;
            }
            _view?.Dispose();
            _view = null;
            _gaveUp = false;
        }

        /// <summary>Opens the app on a tab; false while it is not on the phone.</summary>
        public bool Open(int tab)
        {
            if (_app == null || _view == null || _broken || !Settings.EconomyApp)
            {
                return false;
            }
            _view.Want(OutlookTab.Clamp(tab));
            _app.Open();
            return true;
        }

        /// <summary>Opens the app on the tab a notice about the city's economy leads to (the prices and the rate).</summary>
        public static bool OpenPrices()
        {
            return Instance != null && Instance.Open(OutlookTab.Prices);
        }
    }
}
