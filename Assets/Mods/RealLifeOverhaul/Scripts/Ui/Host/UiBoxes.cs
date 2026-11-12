using UI;
using UnityEngine;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// Whether one of the game's boxes stands over the screens (its question box, its acknowledgement, its text box), and
    /// whether the kit draws it or something of its own over them (All apps), so a kit screen under it holds its keys or
    /// steps aside. Game side. Cost: three field reads when asked, a frame at most while a kit screen shows.
    /// </summary>
    public static class UiBoxes
    {
        /// <summary>Set by the box kit while it draws the box that is up: the screen under it holds its keys and stays drawn.</summary>
        public static bool Drawn;

        /// <summary>Set while the kit stands something of its own over the screens (All apps): they hold their keys.</summary>
        public static bool Over;

        /// <summary>A box of the game's is up: the question, the acknowledgement or the text box.</summary>
        public static bool Up => GameConfirm.IsOpen || AcknowledgeWarning.isOpen || Typing;

        /// <summary>The game's text box is up (the mod's own boxes ask through it too).</summary>
        public static bool Typing
        {
            get
            {
                UIs uis = InstanceBehavior<UIs>.Instance;
                return uis != null && uis.textInputUi != null && uis.textInputUi.gameObject.activeSelf;
            }
        }
    }
}
