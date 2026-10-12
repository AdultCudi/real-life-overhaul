using RealLifeOverhaul.GameUi;
using UnityEngine;

namespace RealLifeOverhaul.Markets
{
    public sealed partial class MarketDirector
    {
        private static readonly string[] AppGlyphs = { "Icon-Graph", "Icon-Statistics", "Icon-Money" };

        private MarketApp _view;
        private PhoneApp _app;
        private bool _gaveUp;

        /// <summary>The Stocks app, while it is on the phone.</summary>
        public MarketApp View => _view;

        // Once a second while the market runs: the app on the phone (the game's phone is built a little after the city).
        partial void InstallApp()
        {
            if (_app != null || _gaveUp)
            {
                return;
            }
            var view = new MarketApp(this);
            GameSprites.Want(AppGlyphs);
            bool tryAgain;
            _app = PhoneApp.Install(new PhoneAppDef
            {
                Id = AppId,
                TitleKey = "reallife_mkt_app",
                Icon = like => AppIconPainter.Paint(like, Glyph(), new Color32(92, 196, 132, 255), new Color32(32, 120, 78, 255), 0.42f, "AppIcon-Stocks"),
                Build = view.Build,
                Opened = view.Opened,
                Closed = view.Closed
            }, out tryAgain);
            if (_app == null)
            {
                _gaveUp = !tryAgain;
                if (_gaveUp)
                {
                    Logger?.Warn("Stock market: the game's phone is not built as this version expects, so the Stocks app is left off. The market still runs, and orders that wait are still filled.");
                }
                return;
            }
            _view = view;
        }

        partial void RemoveApp()
        {
            if (_app != null)
            {
                _app.Remove();
                _app = null;
                _view = null;
            }
            _gaveUp = false;
        }

        partial void OpenAppPartial()
        {
            if (_app != null && Running)
            {
                _app.Open();
            }
        }

        /// <summary>Opens the Stocks app on a company's page (a notification was clicked, or the test tools).</summary>
        public void OpenCompany(int stockId)
        {
            if (_app != null && _view != null && Running)
            {
                _view.WantCompany(stockId);
                _app.Open();
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
    }
}
