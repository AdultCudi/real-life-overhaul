using System.Globalization;
using RealLifeOverhaul.GameUi;

namespace RealLifeOverhaul.Flare
{
    /// <summary>
    /// The Flare app's light and dark switch: Flare's filled pill at the right of its top bar, on the title's line,
    /// naming the look a press gives in the options page's words. One press sets that look and paints every part of
    /// the app in it at once. It is the option on the options page (Flare's look, <see cref="Settings.FlareTheme"/>),
    /// written as the Settings app writes an option (<see cref="Meta.OptionsLayout.AppSet"/>: the option's own key,
    /// then its handler) and put on disk when the app closes, as the Settings app and the game's options page do. The
    /// look is this machine's only: nothing is sent in a Going Public session. Cost: nothing per frame; a press writes
    /// one value and the screen once.
    /// </summary>
    public sealed partial class FlareApp
    {
        private const float ThemeTop = 19f, ThemeHeight = 58f;   // canvas units, centred on the title's line

        private Pill _theme;
        private bool _themeWritten;   // the switch changed the option since it was last put on disk

        private void BuildTheme()
        {
            _theme = new Pill(_header, "Theme", ThemeClicked);
        }

        // With the header, on every refresh: the pill at the right of the title's line, naming the look a press gives.
        // The width it takes from the title; none when the options page has no row to write through (not in order).
        private float WriteTheme(float width)
        {
            if (Meta.OptionsLayout.AppFind(Settings.FlareThemeId) == null)
            {
                _theme.Hide();
                return 0f;
            }
            int next = FlareThemeRules.Next(Settings.FlareTheme, _look.Dark);
            // Filled, as the Follow pill: the look's text colour behind its card colour reads clearly on either look's
            // background and shows the look a press gives (dark while light, light while dark); a quiet pill is lost.
            float w = _theme.Show(_look, FlareText.T(FlareThemeRules.ChoiceKey(next)), true, 0f, ThemeTop, ThemeHeight);
            FlareKit.Place(_theme.Back.rectTransform, width - w, ThemeTop, w, ThemeHeight);
            return w + FlareKit.Gap;
        }

        // A press: the other look, set as the Settings app sets an option, then the whole app written again in it (the
        // frame, the cards in view and those kept for later, an open sheet), the list kept where it was.
        private void ThemeClicked()
        {
            bool wasDark = _look.Dark;
            int next = FlareThemeRules.Next(Settings.FlareTheme, wasDark);
            if (!Meta.OptionsLayout.AppSet(Meta.OptionsLayout.AppFind(Settings.FlareThemeId), next))
            {
                return;
            }
            _themeWritten = true;
            Refresh(false);
            if (Meta.Dev.Verbose)
            {
                string key = Settings.PrefsKey(Settings.FlareThemeId);   // the key AppSet wrote under
                int saved = UnityEngine.PlayerPrefs.GetInt(key, -1);
                bool painted = _look.Dark == (next == FlareThemeRules.Dark) && _look.Dark != wasDark;
                bool pass = Settings.FlareTheme == next && saved == next && painted;
                Logger?.Info("Flare check (look): the switch set the look to "
                             + next.ToString(CultureInfo.InvariantCulture)
                             + " (painted " + (wasDark ? "dark" : "light") + " to " + (_look.Dark ? "dark" : "light")
                             + "); the option reads " + Settings.FlareTheme.ToString(CultureInfo.InvariantCulture)
                             + ", its key holds " + saved.ToString(CultureInfo.InvariantCulture) + ": "
                             + (pass ? "PASS" : "FAIL, the option, its key or the look is not the one pressed") + ".");
            }
        }

        // What the switch wrote goes on disk as the app closes or Flare stops, as the Settings app puts its changes.
        private void SaveTheme()
        {
            if (_themeWritten)
            {
                _themeWritten = false;
                UiClone.Safe(UnityEngine.PlayerPrefs.Save);
            }
        }
    }
}
