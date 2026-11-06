using Helpers;
using Scenes.MainMenu;
using UI;
using UnityEngine;
using UnityEngine.UI;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The game's Options (UIs.options) as the host binds it in the city: the window the pause menu's Options row opens. The
    /// object stays active under the kit, so every mod that reads it (Going Public's locks on the difficulty and the game
    /// speed, Alcware's Seasons view, BAScheduler and LIB_BaUnifiedUI reading the Mods view's controller) finds it as the game
    /// leaves it; only its own children are made unseen and untouchable, given back exactly as found. The kit takes the keys
    /// over the paused world and leaves the game's question boxes theirs. The main menu's Options is never bound: this screen
    /// is looked for in the city's UIs alone.
    /// </summary>
    internal sealed class OptionsGame : IUiGameScreen, IUiGameWay, IUiGameOffers
    {
        private UiConceal _conceal;

        public string Id => OptionsKits.CityId;

        public string Name => "the game's options";

        public string Title => UiText.T("reallife_ui_opt_title");

        public UiIcon Icon => UiIcon.Gear;

        public UiGameKind Kind => UiGameKind.Overlay;

        public bool Keys => true;

        public bool Exclusive => false;   // the game's questions (reset all, the custom difficulty's) stand over it and take their own keys

        public bool Blocks => true;       // the paused world under the window takes no clicks

        public Component Find()
        {
            UIs uis = InstanceBehavior<UIs>.Instance;
            return uis != null ? uis.options : null;
        }

        public string Check(Component found)
        {
            Transform t = found.transform;
            return t.Find("Menu") == null ? "its category buttons are not there"
                : t.Find("Views") == null ? "its views are not there"
                : GameContract.OptBindings.Field == null ? "its key bindings are not there" : null;
        }

        public void Conceal(Component found, UiConceal conceal)
        {
            _conceal = conceal;
            Transform root = found.transform;
            for (int i = 0; i < root.childCount; i++)
            {
                conceal.Add(root.GetChild(i));
            }
            conceal.Add(root.GetComponent<Graphic>());
        }

        /// <summary>Another mod's piece that came after the bind (Alcware adds its tab and its view as the window opens) is hidden with the rest.</summary>
        internal void Hide(Transform piece)
        {
            _conceal?.Add(piece);
        }

        public IUiScreen Make(Component found) => new OptionsScreen((Options)found, this);
    }
}
