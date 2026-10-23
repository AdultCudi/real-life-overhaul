using System;
using BAModAPI;
using Scenes.MainMenu;
using UI.Dialog;
using UI.Smartphone;

namespace RealLifeOverhaul.GameUi
{
    /// <summary>
    /// The game's own text box, through which every text box of the mod's is asked. The game closes it only with its own two
    /// buttons, so each box is kept for what asked (<see cref="TextBoxRules.For"/>) and <see cref="ConversationWatch"/>
    /// closes it with the game's close once that is over. Costs nothing until shown; then a few field reads four times a second.
    /// </summary>
    public static class TextBox
    {
        public static IModLogger Logger;

        private static TextBoxFor _for;        // what the box up now was asked for; Nothing when no box of the mod's is up
        private static Func<bool> _owner;      // the conversation's own test that it goes on (TextBoxFor.Owner)
        private static string _what;           // what asked, for Player.log
        private static int _asked;             // which ask this is: a late done or closed of an earlier ask forgets nothing of a later one
        private static bool _inside;           // where it was asked: indoors or not, and the game's building then
        private static object _building;

        /// <summary>The game has its text box (a field read).</summary>
        public static bool CanType
        {
            get
            {
                UI.UIs uis = InstanceBehavior<UI.UIs>.Instance;
                return uis != null && uis.textInputUi != null;
            }
        }

        /// <summary>A box the mod asked for is kept for something: the watch has work.</summary>
        public static bool Kept => _for != TextBoxFor.Nothing;

        /// <summary>Shows the box, kept for whatever was open; <paramref name="done"/> gets the text, <paramref name="closed"/> runs on closing without. False if the game has none.</summary>
        public static bool Ask(string placeholderKey, string buttonKey, Action<string> done, Action closed, string initial, bool allowEmpty, int most)
        {
            return Ask(placeholderKey, buttonKey, done, closed, initial, allowEmpty, most, null, null);
        }

        /// <summary>As above, kept while <paramref name="wanted"/> (made once) says the asking conversation goes on; <paramref name="what"/> names it in the log.</summary>
        public static bool Ask(string placeholderKey, string buttonKey, Action<string> done, Action closed, string initial, bool allowEmpty, int most,
                               Func<bool> wanted, string what)
        {
            UI.UIs uis = InstanceBehavior<UI.UIs>.Instance;
            if (uis == null || uis.textInputUi == null)
            {
                return false;
            }
            int ask = ++_asked;
            _for = TextBoxRules.For(wanted != null, FullMenu.IsOpen, PanelOpen(uis), Options.IsVisible);
            _owner = wanted;
            _what = what ?? placeholderKey;
            _inside = BuildingManager.IsInsideBuilding;
            _building = Building();
            Action<string> typed = Guard.Way<string>(text =>
            {
                Forget(ask);
                done?.Invoke(text);
            });
            Action shut = Guard.Way(() =>
            {
                Forget(ask);
                closed?.Invoke();
            });
            uis.textInputUi.Show(placeholderKey, buttonKey, typed, shut, initial ?? string.Empty, allowEmpty, most);
            return true;
        }

        /// <summary>The watch's look: the box closed with the game's own close once what it was asked for is over.</summary>
        public static void Sweep(bool unloading)
        {
            if (_for == TextBoxFor.Nothing)
            {
                return;
            }
            UI.UIs uis = InstanceBehavior<UI.UIs>.Instance;
            TextInputUI box = uis != null ? uis.textInputUi : null;
            bool shown = box != null && box.gameObject.activeSelf;
            bool live = _for == TextBoxFor.Owner && Live();
            bool moved = BuildingManager.IsInsideBuilding != _inside || !ReferenceEquals(Building(), _building);
            TextBoxFate fate = TextBoxRules.Fate(_for, shown, live, FullMenu.IsOpen, PanelOpen(uis), Options.IsVisible, moved, unloading);
            if (fate == TextBoxFate.Keep)
            {
                return;
            }
            string what = _what;
            _for = TextBoxFor.Nothing;   // forgotten first: the game's close runs the caller's closed, which may ask again
            _owner = null;
            if (!TextBoxRules.Closes(fate))
            {
                return;
            }
            try
            {
                box.OnClose();
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
            if (Meta.Dev.Verbose)
            {
                Logger?.Info("Text box check (closed with what it was for): the box for " + what + " was still up after " + TextBoxRules.Words(fate)
                             + "; closed with the game's own close: PASS");
            }
        }

        // A done or closed of this ask: it is over.
        private static void Forget(int ask)
        {
            if (ask == _asked)
            {
                _for = TextBoxFor.Nothing;
                _owner = null;
            }
        }

        // An owner whose test fails has ended.
        private static bool Live()
        {
            try
            {
                return _owner != null && _owner();
            }
            catch (Exception e)
            {
                Logger?.Error(e);
                return false;
            }
        }

        private static bool PanelOpen(UI.UIs uis)
        {
            DialogUI panel = uis != null && uis.playerHUD != null ? uis.playerHUD.dialogUI : null;
            return panel != null && panel.isPanelOpen;
        }

        // The game's building the player is in; null in the street.
        private static object Building()
        {
            BuildingManager buildings = InstanceBehavior<BuildingManager>.Instance;
            return BuildingManager.IsInsideBuilding && buildings != null ? buildings.building : null;
        }
    }
}
