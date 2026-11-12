using System;
using System.Collections.Generic;
using Character.Customization;
using TMPro;
using UI;
using UI.Components.MenuVerticalCategorized;
using UnityEngine;
using UnityEngine.UI;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The uniform presets' editor on the kit (UniformsKit): the presets read from the game's own list in its own order,
    /// the parts from its own menu, the pieces and colours from its own pickers with the game's own pictures, the figure's
    /// sex and Save from its own buttons; every press is a click on the game's own button, so its questions (a change
    /// unsaved, a preset deleted) and its rules stay its own. The game's 3D preview stays where the game draws it, and a
    /// drag over it turns the figure through the game's own turning. Cost: a look at the editor's few rows four times a
    /// second; a layout only when they change.
    /// </summary>
    public sealed class UniformsScreen : IUiScreen, IUiDragging, IUiTypingSubmit
    {
        private const float Every = 0.25f;       // seconds between looks at the game's editor
        private const int NameLimit = 25;        // the game's own name field's limit, should it hold none
        private const string PictureKey = "uniform:";
        private const string SwatchKey = "uniform:rgb:";   // a colour the game tints a swatch with, as six hex digits

        private static readonly Dictionary<int, Sprite> Pictures = new Dictionary<int, Sprite>(64);
        private static readonly Dictionary<uint, Sprite> Swatches = new Dictionary<uint, Sprite>(32);   // a few dozen colours, kept while the kit stands in
        private static readonly Vector3[] Corners = new Vector3[4];
        private static readonly UiWords Words = (key, english) => UiText.T(key);
        private static bool _picturesOn;

        private readonly EmployeePresetCustomizer _editor;
        private readonly UiUniformsModel _m = new UiUniformsModel();
        private readonly List<Transform> _presets = new List<Transform>(16);
        private readonly List<CategoryButton> _parts = new List<CategoryButton>(8);
        private readonly List<SubCategoryButton> _kinds = new List<SubCategoryButton>(8);
        private readonly List<Button> _pieces = new List<Button>(64);
        private readonly List<Button> _colours = new List<Button>(16);
        private bool _dirty = true, _turning;
        private bool _coloursOpen;   // the worn piece's colours open as their palette over the catalogue
        private float _next;
        private long _seen;

        public UniformsScreen(EmployeePresetCustomizer editor)
        {
            _editor = editor;
            if (!_picturesOn)
            {
                // The game's own pictures of the pieces and colours by the keys this screen gives them; any other key is another part's.
                _picturesOn = true;
                UiPictures.Answer(key => key != null && key.StartsWith(PictureKey, StringComparison.Ordinal) ? Picture(key) : null);
            }
        }

        public string Id => UniformsKit.Id;

        public bool Dirty
        {
            get
            {
                if (Time.unscaledTime >= _next)
                {
                    _next = Time.unscaledTime + Every;
                    long now = Signature();
                    _dirty |= now != _seen;
                    _seen = now;
                }
                return _dirty;
            }
        }

        public void Lay(UiScene s, UiFrame frame, bool dark, IUiKeys keys)
        {
            Build();
            UiUniforms.Lay(s, _m, frame, dark, keys);
            _dirty = false;
        }

        // ------------------------------------------------------------------------------------------------- the model

        private void Build()
        {
            Pictures.Clear();
            UniformCustomizer u = Get<UniformCustomizer>(GameContract.UniformEditor, _editor);
            _m.Title = UiText.T("myemployees_configure_uniform_presets");
            _m.Words = Words;
            Presets();
            bool shown = u != null && u.gameObject.activeInHierarchy && _m.Preset >= 0;
            Parts(shown ? u : null);
            Pieces(shown ? u : null);
            _coloursOpen &= _m.ColourCount > 1;   // a piece of one colour, or none worn, has no palette to keep open
            _m.ColoursOpen = _coloursOpen;
            Stage(shown ? u : null);
            Button save = Get<Button>(GameContract.UniformSave, u);
            _m.CanSave = shown && save != null && save.interactable && save.gameObject.activeInHierarchy;
            Transform female = Get<Transform>(GameContract.UniformFemale, u);
            Transform on = female != null ? female.Find("Selected") : null;
            _m.Female = on != null && on.gameObject.activeSelf;
            var prompts = new List<UiPrompt>(4)
            {
                new UiPrompt { Action = UiAction.Move, Label = UiText.T("reallife_ui_prompt_move") },
                new UiPrompt { Action = UiAction.Confirm, Label = UiText.T("reallife_ui_prompt_select") },
                new UiPrompt { Action = UiAction.TabPrev, Label = UiText.T("reallife_ui_bizops_uniform_part") },
                new UiPrompt { Action = UiAction.Back, Label = UiText.T("reallife_ui_prompt_back") },
            };
            _m.Prompts = prompts.ToArray();
            _m.PromptCount = _m.Prompts.Length;
        }

        // The presets as the game's own list holds them, in its order: each its name in its own field, the chosen one marked.
        private void Presets()
        {
            _presets.Clear();
            Transform template = Get<Transform>(GameContract.UniformTemplate, _editor);
            Transform fresh = Get<Transform>(GameContract.UniformNew, _editor);
            Transform list = template != null ? template.parent : null;
            for (int i = 0; list != null && i < list.childCount; i++)
            {
                Transform c = list.GetChild(i);
                if (c != template && c != fresh && c.gameObject.activeSelf && c.Find("NameInputField") != null)
                {
                    _presets.Add(c);
                }
            }
            _m.Presets = new string[_presets.Count];
            _m.PresetCount = _presets.Count;
            _m.Preset = -1;
            for (int i = 0; i < _presets.Count; i++)
            {
                TMP_InputField f = NameOf(_presets[i]);
                _m.Presets[i] = f != null && f.text.Length > 0 ? f.text : UiText.T("myemployees_uniform");
                Transform sel = _presets[i].Find("Selected");
                _m.Preset = sel != null && sel.gameObject.activeSelf ? i : _m.Preset;
            }
            Transform chosen = _m.Preset >= 0 ? _presets[_m.Preset] : null;
            TMP_InputField name = NameOf(chosen);
            _m.Name = name != null ? name.text : null;
            _m.NameLocked = name == null || !name.interactable || name.readOnly;
            _m.CanCopy = UiWidgets.Shows(ButtonAt(chosen, "Duplicate"));
            _m.CanDelete = UiWidgets.Shows(ButtonAt(chosen, "Delete"));
            _m.CanNew = UiWidgets.Shows(fresh != null ? fresh.GetComponent<Button>() : null);
        }

        // The parts as the game's own menu shows them (only those with more than one piece), and the shown part's own kinds.
        private void Parts(UniformCustomizer u)
        {
            _parts.Clear();
            _kinds.Clear();
            MenuVertical menu = Get<MenuVertical>(GameContract.UniformMenu, u);
            if (menu != null && menu.gameObject.activeInHierarchy)
            {
                menu.GetComponentsInChildren(false, _parts);
            }
            _m.Parts = new string[_parts.Count];
            _m.Part = -1;
            MenuVertical.Category shown = null;
            for (int i = 0; i < _parts.Count; i++)
            {
                MenuVertical.Category c = CategoryOf(menu, i);
                _m.Parts[i] = c == null ? UiText.Fill("reallife_ui_bizops_wear_other", "number", UiText.Whole(i + 1))
                    : Wear(!string.IsNullOrEmpty(c.linkId) ? c.linkId : c.subCategories != null && c.subCategories.Length > 0 ? c.subCategories[0].linkId : null, i);
                if (Selected(_parts[i]))
                {
                    _m.Part = i;
                    shown = c;
                }
            }
            MenuVerticalSubMenu sub = Get<MenuVerticalSubMenu>(GameContract.UniformKinds, menu);
            if (sub != null && sub.gameObject.activeInHierarchy)
            {
                sub.GetComponentsInChildren(false, _kinds);
            }
            _m.Kinds = new string[_kinds.Count];
            _m.Kind = -1;
            for (int j = 0; j < _kinds.Count; j++)
            {
                MenuVertical.SubCategory k = SubOf(shown, j);
                _m.Kinds[j] = Wear(k != null ? k.linkId : null, j);
                _m.Kind = Selected(_kinds[j]) ? j : _m.Kind;
            }
        }

        // The shown part's pieces from the game's own picker, and the worn piece's colours from its own swatches.
        private void Pieces(UniformCustomizer u)
        {
            _pieces.Clear();
            _colours.Clear();
            ElementPicker picker = Get<ElementPicker>(GameContract.UniformPicker, u);
            Entries(picker != null && picker.gameObject.activeInHierarchy ? Get<Transform>(GameContract.UniformPieces, picker) : null, _pieces);
            AppearanceColorPicker colours = u != null ? u.appearanceColorPicker : null;
            Entries(colours != null && colours.gameObject.activeInHierarchy ? Get<Transform>(GameContract.UniformColours, colours) : null, _colours);
            _m.Pieces = Cards(_pieces, "Icon");
            _m.PieceCount = _m.Pieces.Length;
            _m.Colours = Cards(_colours, "Sprite");
            _m.ColourCount = _m.Colours.Length;
        }

        // The game's own entries made from a template, in its order: those it shows.
        private static void Entries(Transform template, List<Button> into)
        {
            Transform parent = template != null ? template.parent : null;
            for (int i = 0; parent != null && i < parent.childCount; i++)
            {
                Transform c = parent.GetChild(i);
                if (c != template && c.gameObject.activeSelf && c.TryGetComponent(out Button b))
                {
                    into.Add(b);
                }
            }
        }

        private static UiUniformPiece[] Cards(List<Button> entries, string picture)
        {
            var cards = new UiUniformPiece[entries.Count];
            for (int i = 0; i < entries.Count; i++)
            {
                Transform t = entries[i].transform;
                Transform p = t.Find(picture);
                Image image = p != null ? p.GetComponent<Image>() : t.GetComponent<Image>();
                Transform sel = t.Find("Selected"), sport = t.Find("SportsIcon");
                // A colour's swatch is the template's own sprite tinted (AppearanceColorPicker.SetList): its picture is the colour.
                Color32 tint = image != null ? (Color32)image.color : new Color32(255, 255, 255, 255);
                bool tinted = tint.r != 255 || tint.g != 255 || tint.b != 255;
                cards[i] = new UiUniformPiece
                {
                    Picture = tinted ? Swatch(tint) : Pic(image != null ? image.sprite : null), Chosen = sel != null && sel.gameObject.activeSelf,
                    Sport = sport != null && sport.gameObject.activeSelf
                };
            }
            return cards;
        }

        // Where the game's own preview shows the figure, in the kit's px: the preview's box right of the colours' column.
        private void Stage(UniformCustomizer u)
        {
            _m.StageW = 0f;
            RectTransform preview = UniformsKit.PreviewOf(u);
            RectTransform root = UniformsKit.Root;   // the host's own root, stretched over the editor's canvas
            if (preview == null || root == null || !preview.gameObject.activeInHierarchy)
            {
                return;
            }
            Rect r = root.rect;
            preview.GetWorldCorners(Corners);
            Vector3 low = root.InverseTransformPoint(Corners[0]), high = root.InverseTransformPoint(Corners[2]);
            float left = (low.x - r.xMin) / UiTokens.CanvasPerPx, right = (high.x - r.xMin) / UiTokens.CanvasPerPx;
            float top = (r.yMax - high.y) / UiTokens.CanvasPerPx, bottom = (r.yMax - low.y) / UiTokens.CanvasPerPx;
            if (u.appearanceColorPicker != null && u.appearanceColorPicker.transform is RectTransform column)
            {
                column.GetWorldCorners(Corners);
                left = Math.Max(left, (root.InverseTransformPoint(Corners[2]).x - r.xMin) / UiTokens.CanvasPerPx + UiTokens.S4);
            }
            if (right - left > UiTokens.S16 && bottom - top > UiTokens.S16)
            {
                _m.StageX = UiDraw.Snap(left);
                _m.StageY = UiDraw.Snap(top);
                _m.StageW = UiDraw.SnapDown(right - left);
                _m.StageH = UiDraw.SnapDown(bottom - top);
            }
        }

        // ------------------------------------------------------------------------------------------------- presses

        public bool Press(int command, int arg)
        {
            _dirty = true;
            UniformCustomizer u = Get<UniformCustomizer>(GameContract.UniformEditor, _editor);
            Transform chosen = _m.Preset >= 0 && _m.Preset < _presets.Count ? _presets[_m.Preset] : null;
            switch (command)
            {
                case UiUniforms.Preset:
                    // The game's own row: it asks first when the shown preset has a change unsaved.
                    return arg >= 0 && arg < _presets.Count && (arg == _m.Preset || Click(_presets[arg].GetComponent<Button>(), "choose a preset"));
                case UiUniforms.Copy:
                    return Click(ButtonAt(chosen, "Duplicate"), "copy the preset");
                case UiUniforms.Delete:
                    return Click(ButtonAt(chosen, "Delete"), "delete the preset (the game asks first)");
                case UiUniforms.NewPreset:
                {
                    Transform fresh = Get<Transform>(GameContract.UniformNew, _editor);
                    return Click(fresh != null ? fresh.GetComponent<Button>() : null, "a new preset");
                }
                case UiUniforms.Part:
                    // The part shown already stays: the game's own button would fold its kinds away.
                    return arg >= 0 && arg < _parts.Count && (arg == _m.Part || Click(Get<Button>(GameContract.UniformTabPress, _parts[arg]), "show a part"));
                case UiUniforms.Kind:
                    return arg >= 0 && arg < _kinds.Count && (arg == _m.Kind || Click(Get<Button>(GameContract.UniformKindPress, _kinds[arg]), "show a kind"));
                case UiUniforms.Piece:
                    return arg >= 0 && arg < _pieces.Count && Click(_pieces[arg], "wear a piece");
                case UiUniforms.Colour:
                    return arg >= 0 && arg < _colours.Count && Click(_colours[arg], "a colour");   // the palette stays open to try the next
                case UiUniforms.Colours:
                    _coloursOpen = !_coloursOpen && _m.ColourCount > 1;
                    return true;
                case UiUniforms.ColoursDone:
                    _coloursOpen = false;
                    return true;
                case UiUniforms.Sex:
                {
                    Transform t = Get<Transform>(arg == 1 ? GameContract.UniformFemale : GameContract.UniformMale, u);
                    return Click(t != null ? t.GetComponent<Button>() : null, arg == 1 ? "show the uniform on a woman" : "show the uniform on a man");
                }
                case UiUniforms.Save:
                    return Click(Get<Button>(GameContract.UniformSave, u), "save the preset");
                default:
                    return false;
            }
        }

        private static bool Click(Button b, string what)
        {
            bool done = UiWidgets.Press(b);
            if (!done)
            {
                UiDirector.Logger?.Info("Uniforms (kit): the game did not take \"" + what + "\" now");
            }
            return done;
        }

        public void Focused(int command, int arg)
        {
        }

        // Back closes the editor through its own close button, which asks first when a change is unsaved.
        public bool Back()
        {
            if (_coloursOpen)
            {
                _coloursOpen = false;   // the palette first, then the editor
                _dirty = true;
                return true;
            }
            Turn(false);
            _dirty = true;
            return Click(UniformsKit.CloseButton(_editor), "close the uniforms' editor (the game asks first if a change is unsaved)");
        }

        public void Tab(int step)
        {
            int count = _parts.Count;
            if (count > 1)
            {
                Press(UiUniforms.Part, (((_m.Part < 0 ? 0 : _m.Part) + step) % count + count) % count);
            }
        }

        public bool Act(UiAction action) => action == UiAction.Alt && _m.CanSave && Press(UiUniforms.Save, 0);

        public void Reveal(int list, int index)
        {
            if (list == _m.PiecesList && _m.PieceColumns > 0)
            {
                float card = _m.PieceCount > 0 && _m.PieceColumns > 0 && _m.PiecesContent > 0f ? _m.PiecesContent / ((_m.PieceCount + _m.PieceColumns - 1) / _m.PieceColumns) : 0f;
                _m.PiecesOffset = Into(_m.PiecesOffset, (index / _m.PieceColumns) * card, card, _m.PiecesRoom);
                _dirty = true;
            }
            else if (list == _m.PresetsList && _m.PresetCount > 0)
            {
                float row = _m.PresetsContent / _m.PresetCount;
                _m.PresetsOffset = Into(_m.PresetsOffset, index * row, row, _m.PresetsRoom);
                _dirty = true;
            }
        }

        // An offset that shows a row from top to top + height in a room.
        private static float Into(float offset, float top, float height, float room) => top < offset ? top : top + height > offset + room ? top + height - room : offset;

        public void Wheel(int clip, float notches)
        {
            float by = -notches * UiTokens.S16;
            if (clip == _m.PiecesClip)
            {
                _m.PiecesOffset = Math.Max(0f, Math.Min(_m.PiecesOffset + by, _m.PiecesContent - _m.PiecesRoom));
                _dirty = true;
            }
            else if (clip == _m.PresetsClip)
            {
                _m.PresetsOffset = Math.Max(0f, Math.Min(_m.PresetsOffset + by, _m.PresetsContent - _m.PresetsRoom));
                _dirty = true;
            }
        }

        // ------------------------------------------------------------------------------------------------- the name, typed

        public int TypingCommand => UiUniforms.Name;

        public string TypingText => _m.Name ?? string.Empty;

        public int TypingLimit
        {
            get
            {
                TMP_InputField f = NameOf(_m.Preset >= 0 && _m.Preset < _presets.Count ? _presets[_m.Preset] : null);
                return f != null && f.characterLimit > 0 ? f.characterLimit : NameLimit;
            }
        }

        public void Typed(string text)
        {
        }

        // The name goes into the game's own field and is ended there, which names the preset as the game's own typing does.
        public void Submitted(string text)
        {
            TMP_InputField f = NameOf(_m.Preset >= 0 && _m.Preset < _presets.Count ? _presets[_m.Preset] : null);
            if (f != null && !f.readOnly && UiWidgets.Type(f, text, true))
            {
                _dirty = true;
                return;
            }
            UiDirector.Logger?.Info("Uniforms (kit): the game's name field did not take the name now");
        }

        // ------------------------------------------------------------------------------------------------- the figure turned

        public void PointerDown(int command, int arg, float x, float y)
        {
        }

        public bool DragBegin(int command, int arg, float x, float y)
        {
            if (command != UiUniforms.Stage)
            {
                return false;
            }
            Turn(true);
            return true;
        }

        public void DragMove(float x, float y)
        {
        }

        public void DragEnd(bool cancelled) => Turn(false);

        // The game's own turning of its preview, as a press on the preview starts it and its release stops it.
        private void Turn(bool on)
        {
            if (!on && !_turning)
            {
                return;
            }
            GameManager game = InstanceBehavior<GameManager>.Instance;
            Character.Customization.Rotate rotate = game != null && game.employeeUniformPreview != null && game.employeeUniformPreview.appearanceSetter != null
                ? game.employeeUniformPreview.appearanceSetter.GetComponent<Character.Customization.Rotate>() : null;
            _turning = on && rotate != null;
            if (rotate == null)
            {
                return;
            }
            if (on)
            {
                rotate.StartDrag();
            }
            else
            {
                rotate.StopRotating();
            }
        }

        // ------------------------------------------------------------------------------------------------- what changed

        // What changing would change the screen: the presets and the chosen one, the part and kind shown, the pieces and
        // colours with the ones worn, the figure's sex and whether there is a change to save.
        private long Signature()
        {
            long sig = _editor.gameObject.activeInHierarchy ? 1 : 2;
            Transform template = Get<Transform>(GameContract.UniformTemplate, _editor);
            Transform list = template != null ? template.parent : null;
            for (int i = 0; list != null && i < list.childCount; i++)
            {
                Transform c = list.GetChild(i);
                if (c == template || !c.gameObject.activeSelf)
                {
                    continue;
                }
                Transform sel = c.Find("Selected");
                TMP_InputField f = NameOf(c);
                sig = sig * 31 + (sel != null && sel.gameObject.activeSelf ? 7 : 3) + (f != null ? f.text.GetHashCode() : 0);
            }
            UniformCustomizer u = Get<UniformCustomizer>(GameContract.UniformEditor, _editor);
            if (u == null || !u.gameObject.activeInHierarchy)
            {
                return sig;
            }
            MenuVertical menu = Get<MenuVertical>(GameContract.UniformMenu, u);
            for (int i = 0; menu != null && i < menu.transform.childCount; i++)
            {
                Transform c = menu.transform.GetChild(i);
                sig = sig * 3 + (c.gameObject.activeSelf ? 1 : 0);
                if (c.TryGetComponent(out SelectableButton b))
                {
                    sig = sig * 3 + (Selected(b) ? 2 : 1);
                }
            }
            MenuVerticalSubMenu sub = Get<MenuVerticalSubMenu>(GameContract.UniformKinds, menu);
            for (int i = 0; sub != null && sub.gameObject.activeSelf && i < sub.transform.childCount; i++)
            {
                if (sub.transform.GetChild(i).TryGetComponent(out SelectableButton b))
                {
                    sig = sig * 3 + (Selected(b) ? 2 : 1);
                }
            }
            sig = sig * 31 + Worn(Get<Transform>(GameContract.UniformPieces, Get<ElementPicker>(GameContract.UniformPicker, u)));
            sig = sig * 31 + (u.appearanceColorPicker != null && u.appearanceColorPicker.gameObject.activeSelf ? Worn(Get<Transform>(GameContract.UniformColours, u.appearanceColorPicker)) : 0);
            Button save = Get<Button>(GameContract.UniformSave, u);
            Transform female = Get<Transform>(GameContract.UniformFemale, u);
            Transform on = female != null ? female.Find("Selected") : null;
            return sig * 31 + (save != null && save.interactable ? 1 : 0) * 2 + (on != null && on.gameObject.activeSelf ? 1 : 0);
        }

        // A picker's entries and the one worn among them, as one figure.
        private static long Worn(Transform template)
        {
            Transform parent = template != null ? template.parent : null;
            long n = 0;
            for (int i = 0; parent != null && i < parent.childCount; i++)
            {
                Transform c = parent.GetChild(i);
                if (c == template || !c.gameObject.activeSelf)
                {
                    continue;
                }
                Transform sel = c.Find("Selected");
                n = n * 3 + (sel != null && sel.gameObject.activeSelf ? 2 : 1);
            }
            return n;
        }

        // ------------------------------------------------------------------------------------------------- helpers

        private static T Get<T>(GameMember member, object owner) where T : class => owner != null && member.Field != null ? member.Field.GetValue(owner) as T : null;

        private static TMP_InputField NameOf(Transform preset)
        {
            Transform t = preset != null ? preset.Find("NameInputField") : null;
            return t != null ? t.GetComponent<TMP_InputField>() : null;
        }

        private static Button ButtonAt(Transform row, string name)
        {
            Transform t = row != null ? row.Find(name) : null;
            return t != null ? t.GetComponent<Button>() : null;
        }

        private static bool Selected(SelectableButton b)
        {
            var outline = Get<GameObject>(GameContract.UniformTabOn, b);
            return outline != null && outline.activeSelf;
        }

        // The menu's category the k-th shown button stands for (the game numbers the shown ones from 0 in order).
        private static MenuVertical.Category CategoryOf(MenuVertical menu, int k)
        {
            for (int i = 0; menu != null && menu.categories != null && i < menu.categories.Length; i++)
            {
                if (menu.categories[i].categoryId == k)
                {
                    return menu.categories[i];
                }
            }
            return null;
        }

        private static MenuVertical.SubCategory SubOf(MenuVertical.Category c, int k)
        {
            for (int i = 0; c != null && c.subCategories != null && i < c.subCategories.Length; i++)
            {
                if (c.subCategories[i].subCategoryId == k)
                {
                    return c.subCategories[i];
                }
            }
            return null;
        }

        // A part's words by the kind of piece the game links it to (a top, a hat); one the mod does not know by its number.
        private static string Wear(string linkId, int index)
        {
            string key = UniformWords.KeyOf(linkId);
            return key != null ? UiText.T(key) : UiText.Fill("reallife_ui_bizops_wear_other", "number", UiText.Whole(index + 1));
        }

        private static string Pic(Sprite sprite)
        {
            if (sprite == null)
            {
                return null;
            }
            int id = sprite.GetInstanceID();
            Pictures[id] = sprite;
            return PictureKey + id.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        /// <summary>The palette's colours let go with the kit: each destroyed with its texture, painted again at the next look.</summary>
        internal static void DropSwatches()
        {
            if (Swatches.Count == 0)
            {
                return;
            }
            foreach (Sprite sprite in Swatches.Values)
            {
                CodeAssets.DropSprite(sprite);
            }
            Swatches.Clear();
        }

        // A colour as a picture of it, made once a colour.
        private static string Swatch(Color32 c)
        {
            uint rgb = (uint)(c.r << 16 | c.g << 8 | c.b);
            if (!Swatches.ContainsKey(rgb))
            {
                var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave, filterMode = FilterMode.Point };
                var pixels = new Color32[16];
                for (int i = 0; i < pixels.Length; i++)
                {
                    pixels[i] = new Color32(c.r, c.g, c.b, 255);
                }
                tex.SetPixels32(pixels);
                tex.Apply(false, true);
                Sprite sprite = Sprite.Create(tex, new Rect(0f, 0f, 4f, 4f), new Vector2(0.5f, 0.5f));
                sprite.hideFlags = HideFlags.HideAndDontSave;
                Swatches[rgb] = sprite;
            }
            return SwatchKey + rgb.ToString("x6", System.Globalization.CultureInfo.InvariantCulture);
        }

        private static Sprite Picture(string key)
        {
            if (key.StartsWith(SwatchKey, StringComparison.Ordinal))
            {
                return uint.TryParse(key.Substring(SwatchKey.Length), System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out uint rgb)
                       && Swatches.TryGetValue(rgb, out Sprite swatch) ? swatch : null;
            }
            return int.TryParse(key.Substring(PictureKey.Length), System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out int id)
                   && Pictures.TryGetValue(id, out Sprite sprite) ? sprite : null;
        }
    }
}
