using UI;
using UnityEngine;
using UnityEngine.UI;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The game's custom colour picker (CustomColorPicker, UIs.customColorPicker: a sign's colour, an item's in the
    /// interior designer) framed by the kit while it is open: the game's own colour field (its hue ring, its box and its
    /// hex field) stays as it is and is pressed as it is, the kit's slate drawn round it with the title over it and the
    /// buttons under it. Over the world, taking no key of its own, so the game's Cancel still closes it. Game side.
    /// </summary>
    public sealed class ColourPickerKit : IUiGameScreen
    {
        internal const string Backdrop = "Background", Panel = "Panel", Headline = "Headline", Buttons = "Buttons";

        public string Id => UiWorld.ColourId;

        public string Name => "the colour picker";

        public string Title => UiText.T("custom_color_picker_headline");

        public UiIcon Icon => UiIcon.PaintBrush;

        public UiGameKind Kind => UiGameKind.Overlay;

        public Component Find()
        {
            UIs uis = InstanceBehavior<UIs>.Instance;
            return uis != null ? uis.customColorPicker : null;
        }

        public string Check(Component found)
        {
            if (GameContract.UiWorldColour.Off)
            {
                return GameContract.UiWorldColour.Why;
            }
            Transform panel = found.transform.Find(Panel);
            return found.transform.Find(Backdrop) == null || panel == null || panel.Find(Headline) == null || panel.Find(Buttons) == null
                   || !(GameContract.ColourField.Field?.GetValue(found) is Component)
                ? "its backdrop, its panel's headline and buttons or its colour field are not there" : null;
        }

        public void Conceal(Component found, UiConceal conceal)
        {
            Transform panel = found.transform.Find(Panel);
            conceal.Add(found.transform.Find(Backdrop), true);
            conceal.Add(panel.GetComponent<Image>());   // the panel's own slate only: the colour field on it stays seen
            conceal.Add(panel.Find(Headline));
            conceal.Add(panel.Find(Buttons));
        }

        public IUiScreen Make(Component found) => new ColourPickerScreen((CustomColorPicker)found);
    }

    /// <summary>
    /// The colour picker's frame: the kit's slate round the game's own colour field, the title strip over it with the
    /// close, Cancel and Add under it as the game's own buttons (Cancel puts the colour back as it was; Add keeps it among
    /// the player's colours and uses it). The field itself is never drawn by the kit. NPCs pick no colours, so there is no
    /// other side. Cost: a layout as the field moves; four times a second a read of its place.
    /// </summary>
    public sealed class ColourPickerScreen : WorldToolScreen, IUiScreen
    {
        private readonly CustomColorPicker _picker;
        private int _buttonsLine = -1;

        public ColourPickerScreen(CustomColorPicker picker)
        {
            _picker = picker;
        }

        public override string Id => UiWorld.ColourId;

        protected override string Scene => "colour picker";

        private RectTransform Field()
        {
            var field = GameContract.ColourField.Field != null ? GameContract.ColourField.Field.GetValue(_picker) as Component : null;
            return field != null ? field.transform as RectTransform : null;
        }

        protected override long Signature()
        {
            RectTransform f = Field();
            return f == null ? 0 : Mix(Mix((long)f.position.x, (long)f.position.y), (long)(f.rect.width * 10f + f.rect.height));
        }

        protected override void Read()
        {
            M.Title = UiText.T("custom_color_picker_headline");
            M.Icon = UiIcon.PaintBrush;
            M.Close = true;
            M.State = null;
            _buttonsLine = M.LineCount;
            ref UiToolLine buttons = ref Add(UiToolKind.Buttons);
            buttons.Label = UiText.T("common_add");
            buttons.Icon = UiIcon.Plus;
            buttons.Value = UiText.T("common_cancel");
        }

        protected override void Place(UiFrame frame, float w, float h, out float x, out float y)
        {
            x = y = 0f;   // the frame stands round the game's field, wherever that is
        }

        protected override void Draw(UiScene s, UiFrame frame, bool dark, IUiKeys keys, float x, float y, float w)
        {
            s.Begin(Scene, frame.Width, frame.Height, dark);
            s.Pointer = frame.Pointer;
            s.Floats = true;
            if (Rect(Field(), frame, out float fx, out float fy, out float fw, out float fh))
            {
                UiToolPanel.Around(s, UiAt.Screen, M, fx, fy, fw, fh, keys);
            }
        }

        protected override bool Line(int line) => line == _buttonsLine && Press("AddButton");

        protected override bool Second(int line) => line == _buttonsLine && Press("CancelButton");

        protected override bool Closed() => Press("CancelButton");

        // The game's own button under its panel, so its Add keeps the colour and its Cancel puts the old one back.
        private bool Press(string name)
        {
            Transform buttons = _picker.transform.Find(ColourPickerKit.Panel + "/" + ColourPickerKit.Buttons);
            Transform b = buttons != null ? buttons.Find(name) : null;
            if (b != null && UiWidgets.Press(b.GetComponent<Button>()))
            {
                return true;
            }
            if (name == "AddButton")
            {
                _picker.Add();
            }
            else
            {
                _picker.Cancel();
            }
            return true;
        }
    }
}
