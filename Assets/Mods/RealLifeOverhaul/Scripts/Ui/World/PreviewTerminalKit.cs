using UI;
using UI.CustomUI;
using UnityEngine;
using UnityEngine.UI;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The interior firm's preview terminal (UI.CustomUI.PreviewTerminalUI, UIs.previewTerminalUI, opened at the firm's
    /// terminal) drawn as the kit's small panel while it is open: over the world, taking no key of its own, so the game's
    /// Cancel still closes it (and lets the clock run again, as the game's own close does). The screen's backdrop keeps
    /// holding the clicks off the world. Game side.
    /// </summary>
    public sealed class PreviewTerminalKit : IUiGameScreen
    {
        internal const string Backdrop = "ScreenBackground", Panel = "Panel";

        public string Id => UiWorld.TerminalId;

        public string Name => "the preview terminal";

        public string Title => UiText.T("ba:itemname_previewterminal");

        public UiIcon Icon => UiIcon.Eye;

        public UiGameKind Kind => UiGameKind.Overlay;

        public Component Find()
        {
            UIs uis = InstanceBehavior<UIs>.Instance;
            return uis != null ? uis.previewTerminalUI : null;
        }

        public string Check(Component found)
        {
            if (GameContract.UiWorldTerminal.Off)
            {
                return GameContract.UiWorldTerminal.Why;
            }
            return found.transform.Find(Backdrop) == null || found.transform.Find(Panel) == null ? "its backdrop or its panel is not there" : null;
        }

        public void Conceal(Component found, UiConceal conceal)
        {
            conceal.Add(found.transform.Find(Backdrop), true);
            conceal.Add(found.transform.Find(Panel));
        }

        public IUiScreen Make(Component found) => new PreviewTerminalScreen((PreviewTerminalUI)found);
    }

    /// <summary>
    /// The preview terminal as the kit's tool panel centred over the paused world: a line on what it does, the four choices
    /// of the game's own drop-downs as the game opens them one after another (the building, its size, the business, the
    /// design), then Preview design on the cyan (greyed until the game's own button takes a press) beside the blueprints.
    /// Every press is the game's own drop-down or button. NPCs see no previews, so there is no other side. Cost: a layout as
    /// something changes; four times a second a few reads.
    /// </summary>
    public sealed class PreviewTerminalScreen : WorldToolScreen, IUiScreen
    {
        private static readonly string[] Placeholders =
        {
            "preview_terminal_select_building_type", "preview_terminal_select_building_size", "preview_terminal_select_business_type", "preview_terminal_select_design"
        };

        private static readonly string[] Labels =
        {
            "reallife_ui_world_terminal_building", "reallife_ui_world_terminal_size", "reallife_ui_world_terminal_business", "reallife_ui_world_terminal_design"
        };

        private readonly PreviewTerminalUI _ui;
        private readonly int[] _choiceAt = new int[UiToolPanel.MostLines];
        private int _buttonsLine = -1;

        public PreviewTerminalScreen(PreviewTerminalUI ui)
        {
            _ui = ui;
        }

        public override string Id => UiWorld.TerminalId;

        protected override string Scene => "preview terminal";

        protected override long Signature()
        {
            long seen = 17;
            for (int i = 0; i < Placeholders.Length; i++)
            {
                UI.Elements.Dropdown d = Dropdown(i);
                seen = Mix(seen, d != null && d.gameObject.activeSelf ? d.SelectedOptionIndex + 2 : 0);
            }
            Button preview = Preview();
            return Mix(seen, preview != null && preview.interactable ? 1 : 0);
        }

        protected override void Read()
        {
            for (int i = 0; i < _choiceAt.Length; i++)
            {
                _choiceAt[i] = -1;
            }
            M.Title = UiText.T("ba:itemname_previewterminal");
            M.Icon = UiIcon.Eye;
            M.Close = true;
            M.State = null;
            ref UiToolLine said = ref Add(UiToolKind.Text);
            said.Label = UiText.T("reallife_ui_world_terminal_note");
            for (int i = 0; i < Placeholders.Length; i++)
            {
                UI.Elements.Dropdown d = Dropdown(i);
                if (d == null || !d.gameObject.activeSelf)
                {
                    continue;
                }
                _choiceAt[M.LineCount] = i;
                ref UiToolLine choice = ref Add(UiToolKind.Choice);
                choice.Label = UiText.T(Labels[i]);
                choice.Value = DropdownValue(d, GameContract.TerminalOptionsLocalized, Placeholders[i]);
            }
            Button preview = Preview();
            _buttonsLine = M.LineCount;
            ref UiToolLine buttons = ref Add(UiToolKind.Buttons);
            buttons.Label = UiText.T("preview_terminal_preview_button");
            buttons.Icon = UiIcon.Eye;
            buttons.Disabled = preview == null || !preview.interactable;
            buttons.Value = UiText.T("reallife_ui_world_terminal_blueprints");
        }

        // Centred over the world above the status bar, as the game's own terminal stands.
        protected override void Place(UiFrame frame, float w, float h, out float x, out float y)
        {
            x = (frame.Width - w) / 2f;
            y = Mathf.Max(UiTokens.S8, (frame.BandY - h) / 2f);
        }

        protected override void Options(int line)
        {
            int i = line >= 0 && line < _choiceAt.Length ? _choiceAt[line] : -1;
            if (i < 0)
            {
                base.Options(line);
                return;
            }
            DropdownOptions(Dropdown(i), GameContract.TerminalOptions, GameContract.TerminalOptionsLocalized);
        }

        protected override bool Pick(int line, int option)
        {
            int i = line >= 0 && line < _choiceAt.Length ? _choiceAt[line] : -1;
            UI.Elements.Dropdown d = i >= 0 ? Dropdown(i) : null;
            if (d == null || option < 0 || option >= M.OptionCount)
            {
                return false;
            }
            d.SelectOption(option);   // the game's own pick: it opens the next choice and readies its preview
            return true;
        }

        protected override bool Line(int line) => line == _buttonsLine && UiWidgets.Press(Preview());

        protected override bool Second(int line)
        {
            if (line != _buttonsLine)
            {
                return false;
            }
            _ui.OpenBlueprintPanel();
            return true;
        }

        protected override bool Closed()
        {
            _ui.HideTerminal(true);
            return true;
        }

        private UI.Elements.Dropdown Dropdown(int i)
        {
            GameMember member = i == 0 ? GameContract.TerminalBuilding : i == 1 ? GameContract.TerminalSize : i == 2 ? GameContract.TerminalBusiness : GameContract.TerminalDesign;
            return member.Field != null ? member.Field.GetValue(_ui) as UI.Elements.Dropdown : null;
        }

        private Button Preview() => GameContract.TerminalPreview.Field != null ? GameContract.TerminalPreview.Field.GetValue(_ui) as Button : null;
    }
}
