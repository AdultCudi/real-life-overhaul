using Helpers;
using UI;
using UI.MiniMenu;
using UnityEngine;
using UnityEngine.UI;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The game's pause menu (MiniMenu on the HUD) as the host binds it: its panel, which the game switches on when the
    /// menu opens and off when it closes or steps aside for Options and Blueprints. The panel stays active (Going Public
    /// reads it and writes the session's name into its field); its pieces and its own backing are hidden under the kit.
    /// The kit takes the keys over the paused world, and leaves the game's question boxes theirs.
    /// </summary>
    internal sealed class PauseGame : IUiGameScreen, IUiGameWay, IUiGameOffers
    {
        public string Id => PauseKits.PauseId;

        public string Name => "the pause menu";

        public string Title => UiText.T("reallife_ui_pause_title");

        public UiIcon Icon => UiIcon.Pause;

        public UiGameKind Kind => UiGameKind.Overlay;

        public bool Keys => true;

        public bool Exclusive => false;   // the game's questions (save over, leave unsaved) stand over the menu and take their own keys

        public bool Blocks => true;       // the paused world under the menu takes no clicks

        public Component Find()
        {
            MiniMenu menu = Menu();
            return menu != null && menu.panel != null ? menu.panel.transform : null;
        }

        public string Check(Component found)
        {
            MiniMenu menu = Menu();
            return menu == null ? "the menu is not there"
                : GameContract.PauseName.Field == null || !(GameContract.PauseName.Field.GetValue(menu) is TMPro.TMP_InputField) ? "its save name's field is not there"
                : found.transform.Find(PauseScreen.SaveButton) == null ? "its save button is not there" : null;
        }

        public void Conceal(Component found, UiConceal conceal)
        {
            Transform panel = found.transform;
            for (int i = 0; i < panel.childCount; i++)
            {
                conceal.Add(panel.GetChild(i));
            }
            conceal.Add(panel.GetComponent<Graphic>());
        }

        public IUiScreen Make(Component found) => new PauseScreen(Menu(), found.transform);

        internal static MiniMenu Menu()
        {
            UIs uis = InstanceBehavior<UIs>.Instance;
            return uis != null ? uis.miniMenuUI : null;
        }
    }
}
