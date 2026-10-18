using System;

namespace RealLifeOverhaul.GameUi
{
    /// <summary>
    /// The game's own text box (UIs.textInputUi: Canvases/TextInputUI, its own canvas above the menus and the dialogue
    /// panel), as the game asks for a business's name: a line of text with the game's placeholder, button and character
    /// limit. Flare's composer, street talk and the calls of the social life type in it. The caller wraps what it hands
    /// over in its own guard (Guard.Wrap, UiClone.Safe): the game calls it from its button. The cost: none until shown.
    /// </summary>
    public static class TextBox
    {
        /// <summary>True when the game has its text box (a field read).</summary>
        public static bool CanType
        {
            get
            {
                UI.UIs uis = InstanceBehavior<UI.UIs>.Instance;
                return uis != null && uis.textInputUi != null;
            }
        }

        /// <summary>
        /// Shows the box with an empty or given line. <paramref name="done"/> has what was typed, <paramref name="closed"/>
        /// runs when it is closed without (may be null). False, and nothing shown, if the game has no box.
        /// </summary>
        public static bool Ask(string placeholderKey, string buttonKey, Action<string> done, Action closed, string initial, bool allowEmpty, int most)
        {
            UI.UIs uis = InstanceBehavior<UI.UIs>.Instance;
            if (uis == null || uis.textInputUi == null)
            {
                return false;
            }
            uis.textInputUi.Show(placeholderKey, buttonKey, done, closed, initial ?? string.Empty, allowEmpty, most);
            return true;
        }
    }
}
