using System.Collections.Generic;
using UnityEngine;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// Marks a game screen whose kit screen shows another mod's pieces itself (BizMan's sidebar, Contacts' header, the pause menu's rows): the host
    /// only says them once in Player.log and never falls back to the game's own view for them.
    /// </summary>
    public interface IUiGameOffers
    {
    }

    /// <summary>One container of a game screen whose children the game made, with the names it gives them.</summary>
    public struct UiGamePart
    {
        public Transform Container;
        public string[] GameNames;
    }

    /// <summary>
    /// Optional for a game screen that knows the names the game gives the children of a container (its tab bar, its header's
    /// row of buttons): an active child under it that is neither the game's nor the mod's is another mod's addition, whether
    /// or not a script of its can be found on it. The host looks at the parts as the screen opens; a piece it finds is logged
    /// once with its mod, and the screen is the game's own view until it closes (unless the screen offers the piece itself,
    /// as BizMan and Contacts do, and leaves the parts out). Every screen also gets the host's default look at its top level.
    /// </summary>
    public interface IUiGameParts
    {
        /// <summary>Adds the containers to look at, with the game's names for their children.</summary>
        void Parts(Component found, List<UiGamePart> into);
    }
}
