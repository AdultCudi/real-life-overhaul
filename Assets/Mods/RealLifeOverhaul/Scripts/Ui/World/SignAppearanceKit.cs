using System.Collections.Generic;
using UI;
using UnityEngine;
using UnityEngine.UI;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// A business sign's look (SignAppearance, UIs.draggableWindows.signAppearance, opened from a building's panel) drawn
    /// as the kit's small panel where the game's window stands while it is open: over the world, taking no key of its own,
    /// so the game's Cancel still closes it. Game side.
    /// </summary>
    public sealed class SignAppearanceKit : IUiGameScreen
    {
        public string Id => UiWorld.SignId;

        public string Name => "a sign's look";

        public string Title => UiText.T("sign_appearance");

        public UiIcon Icon => UiIcon.PaintBrush;

        public UiGameKind Kind => UiGameKind.Overlay;

        public Component Find()
        {
            UIs uis = InstanceBehavior<UIs>.Instance;
            return uis != null && uis.draggableWindows != null && uis.draggableWindows.signAppearance != null ? uis.draggableWindows.signAppearance.panel : null;
        }

        public string Check(Component found)
        {
            if (GameContract.UiWorldSign.Off)
            {
                return GameContract.UiWorldSign.Why;
            }
            return found.GetComponentInParent<SignAppearance>(true) == null ? "its window is not there" : null;
        }

        public void Conceal(Component found, UiConceal conceal) => conceal.Add(found.transform);

        public IUiScreen Make(Component found) => new SignAppearanceScreen(found.GetComponentInParent<SignAppearance>(true), (RectTransform)found);
    }

    /// <summary>
    /// A sign's look as the kit's tool panel: its kind as a choice of the game's own drop-down, then its neon's colours and
    /// its bulbs' colours as rows of swatches (only those the kind lights, as the game shows them), the player's own colours
    /// first, the chosen one framed, the add button leading each row; the add opens the game's own colour picker, framed by
    /// the kit in turn, and while it is open this panel steps back. Every press is the game's own button or drop-down, so
    /// the sign changes as it does without the kit. NPCs' signs are set by the game, so there is no other side. Cost: a
    /// layout as something changes; four times a second a few reads.
    /// </summary>
    public sealed class SignAppearanceScreen : WorldToolScreen, IUiScreen
    {
        private const string Selected = "Selected";   // the game's mark on the chosen colour (ColorListUI)

        private readonly SignAppearance _sign;
        private readonly RectTransform _panel;
        private readonly List<Button>[] _colours = { new List<Button>(24), new List<Button>(24) };
        private readonly ColorListUI[] _lists = new ColorListUI[2];
        private readonly int[] _listAt = new int[UiToolPanel.MostLines];
        private string[][] _keys = { new string[24], new string[24] };
        private int _kindLine = -1;

        public SignAppearanceScreen(SignAppearance sign, RectTransform panel)
        {
            _sign = sign;
            _panel = panel;
        }

        public override string Id => UiWorld.SignId;

        protected override string Scene => "sign appearance";

        private UI.Elements.Dropdown Kind => GameContract.SignKind.Field != null ? GameContract.SignKind.Field.GetValue(_sign) as UI.Elements.Dropdown : null;

        /// <summary>True while the game's colour picker stands over the sign: the panel steps back for it.</summary>
        private static bool Picking => CustomColorPicker.isOpen;

        protected override long Signature()
        {
            UI.Elements.Dropdown kind = Kind;
            long seen = kind != null ? kind.SelectedOptionIndex : -2;
            seen = Mix(seen, Picking ? 1 : 0);
            for (int l = 0; l < 2; l++)
            {
                ColorListUI list = List(l);
                Transform box = Box(list);
                seen = Mix(seen, list != null && list.gameObject.activeSelf ? 1 : 0);
                seen = Mix(seen, box != null ? box.childCount : -1);
                for (int i = 0; box != null && i < box.childCount; i++)
                {
                    Transform child = box.GetChild(i);
                    Transform mark = child.gameObject.activeSelf ? child.Find(Selected) : null;
                    seen = Mix(seen, mark != null && mark.gameObject.activeSelf ? i + 1 : 0);
                }
            }
            seen = Mix(seen, _panel != null ? (long)_panel.position.x * 7 + (long)_panel.position.y : 0);
            return seen;
        }

        protected override void Draw(UiScene s, UiFrame frame, bool dark, IUiKeys keys, float x, float y, float w)
        {
            if (Picking)
            {
                // The game's colour picker stands over the sign now, framed by the kit: nothing of this panel in its way.
                s.Begin(Scene, frame.Width, frame.Height, dark);
                s.Pointer = frame.Pointer;
                s.Floats = true;
                return;
            }
            base.Draw(s, frame, dark, keys, x, y, w);
        }

        protected override void Read()
        {
            for (int i = 0; i < _listAt.Length; i++)
            {
                _listAt[i] = -1;
            }
            M.Title = UiText.T("sign_appearance");
            M.Icon = UiIcon.PaintBrush;
            M.Close = true;
            M.State = null;
            _kindLine = M.LineCount;
            ref UiToolLine kind = ref Add(UiToolKind.Choice);
            kind.Label = UiText.T("sign_appearance_sign_type");
            kind.Value = DropdownValue(Kind, GameContract.SignOptionsLocalized, "common_select_type");
            Colours(0, "sign_appearance_sign_light_color");
            Colours(1, "sign_appearance_sign_bulb_color");
        }

        // A row of the game's colours as it lists them, the player's own first; only while the kind lights that part.
        private void Colours(int l, string key)
        {
            ColorListUI list = List(l);
            _lists[l] = list;
            _colours[l].Clear();
            if (list == null || !list.gameObject.activeSelf)
            {
                return;
            }
            Transform box = Box(list);
            var add = GameContract.SignAddColour.Field != null ? GameContract.SignAddColour.Field.GetValue(list) as Transform : null;
            int chosen = -1;
            for (int i = 0; box != null && i < box.childCount; i++)
            {
                Transform child = box.GetChild(i);
                Button b = child.gameObject.activeSelf && child != add ? child.GetComponent<Button>() : null;
                Image image = b != null ? child.GetComponent<Image>() : null;
                if (image == null)
                {
                    continue;
                }
                int at = _colours[l].Count;
                if (at == _keys[l].Length)
                {
                    System.Array.Resize(ref _keys[l], at * 2);
                }
                _keys[l][at] = WorldPictures.Of(image.color);
                Transform mark = child.Find(Selected);
                if (mark != null && mark.gameObject.activeSelf)
                {
                    chosen = at;
                }
                _colours[l].Add(b);
            }
            _listAt[M.LineCount] = l;
            ref UiToolLine row = ref Add(UiToolKind.Swatches);
            row.Label = UiText.T(key);
            row.Swatches = _keys[l];
            row.SwatchCount = _colours[l].Count;
            row.SwatchChosen = chosen;
            row.CanAdd = add != null && add.gameObject.activeSelf;
        }

        // Where the game's own window stands (the player may have moved it), kept on the screen.
        protected override void Place(UiFrame frame, float w, float h, out float x, out float y)
        {
            if (Rect(_panel, frame, out float px, out float py, out _, out _))
            {
                x = px;
                y = py;
            }
            else
            {
                x = UiTokens.S8;
                y = UiTokens.S16;
            }
            x = Mathf.Clamp(x, UiTokens.S3, Mathf.Max(UiTokens.S3, frame.Width - w - UiTokens.S3));
            y = Mathf.Clamp(y, UiTokens.S3, Mathf.Max(UiTokens.S3, frame.BandY - h - UiTokens.S3));
        }

        protected override void Options(int line)
        {
            DropdownOptions(Kind, GameContract.SignOptions, GameContract.SignOptionsLocalized);
        }

        protected override bool Pick(int line, int option)
        {
            UI.Elements.Dropdown kind = Kind;
            if (line != _kindLine || kind == null || option < 0 || option >= M.OptionCount)
            {
                return false;
            }
            kind.SelectOption(option);   // the game's own pick: the sign changes and its colour rows follow the kind
            return true;
        }

        protected override bool Colour(int line, int index)
        {
            int l = line >= 0 && line < _listAt.Length ? _listAt[line] : -1;
            return l >= 0 && index >= 0 && index < _colours[l].Count && UiWidgets.Press(_colours[l][index]);
        }

        protected override bool AddColour(int line)
        {
            int l = line >= 0 && line < _listAt.Length ? _listAt[line] : -1;
            ColorListUI list = l >= 0 ? _lists[l] : null;
            var add = list != null && GameContract.SignAddColour.Field != null ? GameContract.SignAddColour.Field.GetValue(list) as Transform : null;
            Button b = add != null ? add.GetComponent<Button>() : null;
            if (b != null)
            {
                return UiWidgets.Press(b);
            }
            if (list == null)
            {
                return false;
            }
            list.AddNewColor();
            return true;
        }

        protected override bool Closed()
        {
            _sign.Close();
            return true;
        }

        private ColorListUI List(int l)
        {
            GameMember member = l == 0 ? GameContract.SignNeon : GameContract.SignBulbs;
            return member.Field != null ? member.Field.GetValue(_sign) as ColorListUI : null;
        }

        private static Transform Box(ColorListUI list) => list != null && GameContract.SignColours.Field != null ? GameContract.SignColours.Field.GetValue(list) as Transform : null;
    }
}
