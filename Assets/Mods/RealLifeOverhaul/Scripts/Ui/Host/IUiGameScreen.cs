using UnityEngine;

namespace RealLifeOverhaul.Ui
{
    /// <summary>How a game screen the kit draws over stands among the others.</summary>
    public enum UiGameKind : byte
    {
        App = 0,   // an app of the full menu: fades with the menu, sleeps as it closes, holds its keys under a box; the frame owns the chrome
        Box,       // a box over everything (the question, the text box): its keys are its own while the game's box is up
        Overlay,   // over the world and every screen (the notices): takes no key, the pointer presses it
        Panel      // a panel of the game's HUD over the world (its dialogue): the keys its own, held under a box, away under the pause menu
    }

    /// <summary>
    /// A screen of the game's that the kit draws over, as the host needs it: where the game keeps the object, how to tell
    /// it is built as this version expects, which of its own leaves to hide, and the kit screen that mirrors it. The host
    /// (UiGameHost) does the rest the same way for every screen: the bind over switched off objects, the looks again, the
    /// watcher, the fade with the full menu, stepping aside, the fallback. Game side; nothing a frame.
    /// </summary>
    public interface IUiGameScreen
    {
        /// <summary>The screen's id in UiScreens, which its switch is keyed by.</summary>
        string Id { get; }

        /// <summary>What it is in Player.log's words: "the Contacts app".</summary>
        string Name { get; }

        /// <summary>The window's title and icon, as the host is bound with them.</summary>
        string Title { get; }

        UiIcon Icon { get; }

        /// <summary>What kind of screen it is: an app of the full menu, a box over everything, or an overlay that takes no key.</summary>
        UiGameKind Kind { get; }

        /// <summary>The game's object, switched off or on; null while the game has not built it (the host looks again later).</summary>
        Component Find();

        /// <summary>Why the object is not as this version expects ("its layout is not there"), or null.</summary>
        string Check(Component found);

        /// <summary>The game's own leaves of the screen to hide under the kit, and any shared parent it must leave alone.</summary>
        void Conceal(Component found, UiConceal conceal);

        /// <summary>The kit screen that mirrors it, made once a city.</summary>
        IUiScreen Make(Component found);
    }
}
