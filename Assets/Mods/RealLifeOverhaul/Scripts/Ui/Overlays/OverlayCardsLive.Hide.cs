using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// Keeps the game's own box whole-hidden while a card stands over it. The box's root is hidden by one group (<see cref="UiConceal"/>),
    /// which is not enough on its own: the game makes and destroys the buttons of a computer's, a golf tee's or a locker's panel each time
    /// it refreshes (CustomizableButtonsOverlay.AddButton), the mod puts its own "Work for" button in the same list (Careers/ComputerButtons),
    /// other mods put pieces in a box, and a button or a nested canvas of the game's may be drawn without the root's group (1.0.5: a player
    /// saw the game's three blue buttons stick out from behind the card). So each button and each nested canvas under the box gets a group
    /// of its own, held the same way, when the box's transforms change in number and at every read of the card, and the groups already held
    /// are hidden once more each frame if anything shows them. The game's box comes back whole with the card (Leave, Give): every group is
    /// written back as found. Cost a frame: one count of the box's transforms and three reads a held group; a walk over the box's own few
    /// transforms only when they changed or at a read (four times a second); nothing allocated, nothing in the scene searched.
    /// </summary>
    internal sealed partial class OverlayCardsLive
    {
        private const int MostSaid = 3;   // lines about a box shown again under a card, said at the most in a session

        private static readonly List<Button> BoxButtons = new List<Button>(16);
        private static readonly List<Canvas> BoxCanvases = new List<Canvas>(4);
        private int _simpleSeen = -1, _detailSeen = -1, _shownAgain;

        // After the card is up: the pieces of the game's box held hidden, and any shown again since hidden once more.
        private void KeepHidden(bool read)
        {
            bool hover = _form == Form.Hover;
            UiConceal hide = hover ? _simpleHide : _detailHide;
            Transform root = hover ? _simple.transform : _detailed.transform;
            int count = root.hierarchyCount;
            if (OverlayRules.SweepDue(hover ? _simpleSeen : _detailSeen, count, read))
            {
                Sweep(hide, root);
                if (hover)
                {
                    _simpleSeen = count;
                }
                else
                {
                    _detailSeen = count;
                }
            }
            int again = hide.Reassert();
            if (again > 0 && _shownAgain < MostSaid && Meta.Dev.Verbose)
            {
                _shownAgain++;
                UiDirector.Logger?.Info("Overlay check (card): " + again + " piece(s) of the game's " + (hover ? "small" : "large") + " box were shown again under the card by"
                                        + " something else and were hidden once more.");
            }
        }

        // Each button and nested canvas under the box a group of its own, held with the root's; what the game destroyed since is forgotten.
        private static void Sweep(UiConceal hide, Transform root)
        {
            hide.Prune();
            BoxButtons.Clear();
            root.GetComponentsInChildren(true, BoxButtons);
            for (int i = 0; i < BoxButtons.Count; i++)
            {
                hide.Add(BoxButtons[i].transform);
            }
            BoxButtons.Clear();
            BoxCanvases.Clear();
            root.GetComponentsInChildren(true, BoxCanvases);
            for (int i = 0; i < BoxCanvases.Count; i++)
            {
                if (BoxCanvases[i].transform != root)
                {
                    hide.Add(BoxCanvases[i].transform);
                }
            }
            BoxCanvases.Clear();
        }
    }
}
