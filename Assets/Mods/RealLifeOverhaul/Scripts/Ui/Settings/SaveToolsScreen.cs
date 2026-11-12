using RealLifeOverhaul.Meta;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The save tools' question, the same card the tools already open through the host. This screen is the fixture
    /// the tests and the previews use: it explains the tool and does not run it. Nothing per frame.
    /// </summary>
    public sealed class SaveToolsScreen : IUiScreen
    {
        public const string ScreenId = "save_tools";

        private readonly UiCardModel _card = new UiCardModel();
        private bool _dirty = true;
        private int _which;

        public string Id => ScreenId;

        public bool Dirty => _dirty;

        /// <summary>0 prepares a save for removal, 1 resets the mod's data, 2 repairs.</summary>
        public void Show(int which)
        {
            _which = which;
            _dirty = true;
        }

        public void Lay(UiScene s, UiFrame frame, bool dark, IUiKeys keys)
        {
            string title;
            string body;
            string yes;
            if (_which == 1)
            {
                title = T("reallife_meta_resetdata_title");
                body = T("reallife_save_reset_body");
                yes = T("reallife_meta_resetdata_yes");
            }
            else if (_which == 2)
            {
                title = T("reallife_meta_repair_title");
                body = T("reallife_save_repair_body");
                yes = T("reallife_save_repair_yes");
            }
            else
            {
                title = T("reallife_meta_remove_title");
                body = T("reallife_save_prepare_body");
                yes = T("reallife_meta_remove_yes");
            }
            _card.Icon = UiIcon.Warning;
            _card.Title = title;
            _card.Body = body;
            _card.BodyLines = 12;
            _card.Buttons = new[]
            {
                new UiActionModel { Label = T("reallife_ui_prompt_back"), Kind = UiButtonKind.Secondary, Key = UiAction.Back },
                new UiActionModel { Label = yes, Kind = UiButtonKind.Destructive, Key = UiAction.Alt }
            };
            _card.Prompts = new[]
            {
                new UiPrompt { Action = UiAction.Move, Label = T("reallife_ui_prompt_move") },
                new UiPrompt { Action = UiAction.Back, Label = T("reallife_ui_prompt_back") }
            };
            _card.PromptCount = 2;
            _card.Focus = 0;
            UiCard.Lay(s, _card, frame, dark, keys);
            _dirty = false;
        }

        public bool Press(int command, int arg) => command == UiCard.ButtonCommand;

        public void Focused(int command, int arg)
        {
        }

        public bool Back() => true;

        public void Tab(int step)
        {
        }

        public bool Act(UiAction action) => false;

        public void Reveal(int list, int index)
        {
        }

        public void Wheel(int clip, float notches)
        {
        }

        private static string T(string key) => UiText.T(key);
    }
}
