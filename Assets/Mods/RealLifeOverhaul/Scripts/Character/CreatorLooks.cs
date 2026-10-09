using System;
using System.Collections.Generic;
using System.Reflection;
using BAModAPI;
using BigAmbitions.Characters;
using BigAmbitions.Characters.Appearance;
using Character.Customization;
using Extensions;
using Helpers;
using Localizor;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The mod's starter garments and shoes as entries of the game's own character creator: in the grid of the
    /// Torso, Legs and Feet categories, after the game's pieces, one tile per garment (a swatch of its cloth, its
    /// name and an "RLO" tag), with the garment's colourways in the game's colour row.
    ///
    /// What the game allows, and what this rests on (all read from its code):
    ///  - <c>CharacterCustomizer</c> raises <c>onSubCategoryChanged</c> each time it has filled the grid
    ///    (<c>ElementPicker.SetList</c>) or the colour row; this adds its tiles then, as copies of the grid's own
    ///    entry template, so they look, lay out and scroll as the game's entries do. The game clears the grid by
    ///    destroying everything that carries the template's tag, which the copies do: nothing of ours outlives a
    ///    category.
    ///  - The grid marks one entry as selected and switches the mark it remembers off when another is clicked;
    ///    this hands it our tile's mark when a look of ours is picked, and hands the game's back when it is not.
    ///  - <c>AppearanceColorPicker.SetList(sprites, callback, selected)</c> is public: the colour row shows our
    ///    swatches while a garment of ours is the pick. The game fills it again itself on its own clicks.
    /// A click on a tile is a pick like any other (<see cref="CharacterCatalogue.Pick"/>); a click on one of the
    /// game's entries takes the mod's look of that kind off (<see cref="CharacterCatalogue.Observe"/>).
    ///
    /// Everything happens on a click or a change of category; nothing runs per frame. If any of the game's parts is
    /// not as expected this logs once and leaves the creator exactly as the game made it.
    /// </summary>
    internal sealed class CreatorLooks
    {
        private const BindingFlags Any = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private const int SwatchSize = 48;
        private const string OurName = "RealLifeOverhaul";

        private sealed class Tile
        {
            public CharacterCatalogue.Slot Slot;
            public int First;        // the slot's first entry of this garment
            public int Count;        // its colourways
            public GameObject Object;
            public GameObject Mark;  // the entry's "selected" outline
            public Image Icon;
        }

        // The two colours of the shoes whose colourway is more than one (the shoes' colour is in their textures, not a tint).
        private static readonly Dictionary<string, Color32> ShoeColours = new Dictionary<string, Color32>(12)
        {
            { "white", new Color32(236, 234, 228, 255) }, { "black", new Color32(30, 30, 33, 255) }, { "grey", new Color32(138, 140, 146, 255) },
            { "navy", new Color32(36, 46, 82, 255) }, { "green", new Color32(236, 234, 228, 255) }, { "brown", new Color32(96, 60, 38, 255) },
            { "tan", new Color32(172, 126, 80, 255) }, { "oxblood", new Color32(92, 30, 36, 255) }
        };
        private static readonly Dictionary<string, Color32> ShoeAccents = new Dictionary<string, Color32>(6)
        {
            { "court_white", new Color32(36, 46, 82, 255) }, { "court_green", new Color32(30, 110, 70, 255) }, { "runner_grey", new Color32(50, 96, 170, 255) },
            { "runner_black", new Color32(190, 40, 40, 255) }, { "runner_navy", new Color32(226, 120, 40, 255) }
        };

        private readonly CharacterCustomizer _customizer;
        private readonly AppearanceSetter _character;
        private readonly IModLogger _logger;
        private readonly ElementPicker _picker;
        private readonly Transform _template;
        private readonly FieldInfo _markField;
        private readonly UnityEvent<AppearanceElementType> _categoryShown;
        private readonly UnityAction<AppearanceElementType> _onCategoryShown;
        private readonly UnityAction<int> _onColour;
        private readonly List<Tile> _tiles = new List<Tile>(8);
        private readonly List<Sprite> _colourRow = new List<Sprite>(32);
        private readonly Dictionary<string, Sprite> _swatches = new Dictionary<string, Sprite>(64);
        private readonly Color32[] _pixels = new Color32[SwatchSize * SwatchSize];
        private readonly string _tag;
        private TMP_FontAsset _font;
        private GameObject _gameMark;        // the game's own selected entry, to hand back
        private Tile _picked;
        private AppearanceElementType _category;
        private bool _hasCategory;
        private bool _colourRowOurs;
        private bool _failed;

        /// <summary>Null (and one log line) if the creator is not there or not built as expected.</summary>
        public static CreatorLooks Attach(CharacterCustomizer customizer, AppearanceSetter character, IModLogger logger)
        {
            try
            {
                if (customizer == null || character == null || customizer.appearanceSetter != character || customizer.appearanceColorPicker == null)
                {
                    logger?.Info("Looks: the game's character creator was not found as expected; the mod's garments are not added to its grids (the options page still has them).");
                    return null;
                }
                var picker = typeof(CharacterCustomizer).GetField("elementPicker", Any)?.GetValue(customizer) as ElementPicker;
                var shown = typeof(CharacterCustomizer).GetField("onSubCategoryChanged", Any)?.GetValue(customizer) as UnityEvent<AppearanceElementType>;
                var template = picker != null ? typeof(ElementPicker).GetField("entryTemplate", Any)?.GetValue(picker) as Transform : null;
                FieldInfo mark = typeof(ElementPicker).GetField("_currentSelectedOutline", Any);
                if (picker == null || shown == null || template == null || template.parent == null || mark == null || mark.FieldType != typeof(GameObject)
                    || template.Find("Selected") == null || template.GetComponent<Button>() == null)
                {
                    logger?.Info("Looks: the creator's grid is not built as this version of the mod expects; the mod's garments are not added to it (the options page still has them).");
                    return null;
                }
                return new CreatorLooks(customizer, character, logger, picker, template, mark, shown);
            }
            catch (Exception e)
            {
                logger?.Warn("Looks: the mod's garments could not be added to the creator's grids (" + e.Message + "); the creator is left as the game made it.");
                return null;
            }
        }

        private CreatorLooks(CharacterCustomizer customizer, AppearanceSetter character, IModLogger logger, ElementPicker picker, Transform template, FieldInfo markField,
            UnityEvent<AppearanceElementType> categoryShown)
        {
            _customizer = customizer;
            _character = character;
            _logger = logger;
            _picker = picker;
            _template = template;
            _markField = markField;
            _categoryShown = categoryShown;
            _onCategoryShown = OnCategoryShown;
            _onColour = OnColour;
            _tag = "reallife_creator_tag".GetLocalization();
            TMP_Text sample = customizer.GetComponentInChildren<TMP_Text>(true);
            _font = sample != null ? sample.font : null;
            _categoryShown.AddListener(_onCategoryShown);
            logger?.Info("Looks: the mod's starter garments are added to the creator's Torso, Legs and Feet grids, after the game's own pieces.");
        }

        public void Dispose()
        {
            try
            {
                _categoryShown.RemoveListener(_onCategoryShown);
                RemoveTiles();
            }
            catch (Exception e)
            {
                _logger?.Error(e);
            }
            foreach (Sprite sprite in _swatches.Values)
            {
                if (sprite != null)
                {
                    Texture2D texture = sprite.texture;
                    UnityEngine.Object.Destroy(sprite);
                    UnityEngine.Object.Destroy(texture);
                }
            }
            _swatches.Clear();
        }

        /// <summary>A pick changed somewhere else (the options page), or the looks have come in from the bundle: the tiles follow.</summary>
        public void Refresh()
        {
            if (_hasCategory)
            {
                Rebuild();
            }
        }

        // The game has just filled the grid for this category, or its colour row after a click on one of its own entries.
        private void OnCategoryShown(AppearanceElementType category)
        {
            _category = category;
            _hasCategory = true;
            _colourRowOurs = false;   // the game has just filled or hidden it
            Rebuild();
        }

        private void Rebuild()
        {
            if (_failed)
            {
                return;
            }
            try
            {
                RemoveTiles();
                CharacterCatalogue.Observe(_character);   // a click on one of the game's entries has taken our look of that kind off
                if (!Settings.CharacterNewLooks || !ModAssets.Ready || _picker == null || !_picker.gameObject.activeInHierarchy || _character == null
                    || _character.data == null || !KindOf(_category, out LookKind kind))
                {
                    ReleaseColourRow();
                    return;
                }
                LookSex sex = _character.data.gender == BigAmbitions.Characters.Gender.Female ? LookSex.Female : LookSex.Male;
                foreach (CharacterCatalogue.Slot slot in CharacterCatalogue.Slots)
                {
                    if (slot.Sex != sex || slot.Kind != kind)
                    {
                        continue;
                    }
                    int next;
                    for (int first = 1; first < slot.Ids.Length; first = next)
                    {
                        next = first + 1;
                        while (next < slot.Ids.Length && slot.Meshes[next] == slot.Meshes[first])
                        {
                            next++;
                        }
                        if (CharacterCatalogue.Starter(slot, first) && ModAssets.Find(slot.Ids[first]) != null)
                        {
                            _tiles.Add(MakeTile(slot, first, next - first));
                        }
                    }
                }
                ShowPick(kind);
            }
            catch (Exception e)
            {
                Fail(e);
            }
        }

        private static bool KindOf(AppearanceElementType category, out LookKind kind)
        {
            switch (category)
            {
                case AppearanceElementType.Torso: kind = LookKind.Top; return true;
                case AppearanceElementType.Legs: kind = LookKind.Legs; return true;
                case AppearanceElementType.Feet: kind = LookKind.Feet; return true;
                default: kind = LookKind.Hair; return false;
            }
        }

        // ---------------------------------------------------------------- tiles

        private Tile MakeTile(CharacterCatalogue.Slot slot, int first, int count)
        {
            Transform entry = UnityEngine.Object.Instantiate(_template, _template.parent);   // tagged like the template: the game clears it with its own
            var tile = new Tile { Slot = slot, First = first, Count = count, Object = entry.gameObject, Mark = entry.Find("Selected").gameObject };
            Transform icon = entry.Find("Icon");
            tile.Icon = icon != null ? icon.GetComponent<Image>() : entry.GetComponent<Image>();
            if (tile.Icon != null)
            {
                tile.Icon.sprite = Swatch(slot, EntryOf(tile));
                tile.Icon.color = Color.white;
            }
            Transform sports = entry.Find("SportsIcon");
            if (sports != null)
            {
                sports.gameObject.SetActive(false);
            }
            tile.Mark.SetActive(false);

            // The garment's name along the foot of the tile, and the mod's tag in its corner.
            string name = ShortName(slot.Paired ? slot.Garments[(first - 1) / CharacterCatalogue.Palette.Length].LabelKey : RowKey(slot, first));
            Band(entry, new Vector2(0f, 0f), new Vector2(1f, 0.36f), name, 15f, TextAlignmentOptions.Center);
            Band(entry, new Vector2(0f, 0.80f), new Vector2(0.46f, 1f), _tag, 12f, TextAlignmentOptions.Center);
            tile.Mark.transform.SetAsLastSibling();

            Button button = entry.GetComponent<Button>();
            button.onClick.AddListener(() => OnTile(tile));
            entry.gameObject.SetActive(true);
            return tile;
        }

        // A shoe's label is that of its colourway ("Court sneakers, white and navy"): the style is what stands before the comma.
        private static string RowKey(CharacterCatalogue.Slot slot, int first)
        {
            foreach (CharacterCatalogue.Row row in CharacterCatalogue.Rows)
            {
                if (row.Id == slot.Ids[first])
                {
                    return row.LabelKey;
                }
            }
            return slot.LabelKey;
        }

        // "Everyday: Crew-neck T-shirt" and "Court sneakers, white and navy" as what fits on a tile.
        private static string ShortName(string key)
        {
            string text = key.GetLocalization() ?? string.Empty;
            int cut = text.IndexOf(": ", StringComparison.Ordinal);
            if (cut >= 0)
            {
                text = text.Substring(cut + 2);
            }
            cut = text.IndexOf(',');
            return cut > 0 ? text.Substring(0, cut) : text;
        }

        private void Band(Transform entry, Vector2 from, Vector2 to, string words, float size, TextAlignmentOptions alignment)
        {
            var band = new GameObject(OurName + " band", typeof(RectTransform));
            band.layer = entry.gameObject.layer;
            var rect = (RectTransform)band.transform;
            rect.SetParent(entry, false);
            rect.anchorMin = from;
            rect.anchorMax = to;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            band.AddComponent<LayoutElement>().ignoreLayout = true;
            var back = band.AddComponent<Image>();
            back.color = new Color(0.07f, 0.08f, 0.10f, 0.78f);
            back.raycastTarget = false;

            var label = new GameObject(OurName + " text", typeof(RectTransform));
            label.layer = band.layer;
            var labelRect = (RectTransform)label.transform;
            labelRect.SetParent(rect, false);
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(3f, 1f);
            labelRect.offsetMax = new Vector2(-3f, -1f);
            var text = label.AddComponent<TextMeshProUGUI>();
            if (_font != null)
            {
                text.font = _font;
            }
            text.text = words;
            text.color = Color.white;
            text.alignment = alignment;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.overflowMode = TextOverflowModes.Ellipsis;
            text.enableAutoSizing = true;
            text.fontSizeMin = 8f;
            text.fontSizeMax = size;
            text.raycastTarget = false;
        }

        private void RemoveTiles()
        {
            // The grid must never be left remembering a mark of ours that is about to go: it switches that mark off on its next click.
            var current = _markField.GetValue(_picker) as GameObject;
            bool ours = false;
            for (int i = 0; i < _tiles.Count; i++)
            {
                ours |= ReferenceEquals(_tiles[i].Mark, current);
            }
            if (ours || current == null)
            {
                _markField.SetValue(_picker, _gameMark != null ? _gameMark : _template.Find("Selected").gameObject);
            }
            else
            {
                _gameMark = current;
            }
            for (int i = 0; i < _tiles.Count; i++)
            {
                if (_tiles[i].Object != null)
                {
                    UnityEngine.Object.Destroy(_tiles[i].Object);
                }
            }
            _tiles.Clear();
            _picked = null;
        }

        // ---------------------------------------------------------------- what is picked

        // The entry of the slot that a tile stands for now: the colourway worn, or the one its garment would come in.
        private static int EntryOf(Tile tile)
        {
            CharacterCatalogue.Slot slot = tile.Slot;
            if (slot.Index >= tile.First && slot.Index < tile.First + tile.Count)
            {
                return slot.Index;
            }
            return tile.First + (slot.Paired ? Mathf.Clamp(slot.ColourIndex, 0, tile.Count - 1) : 0);
        }

        private Tile TileOf(string id)
        {
            for (int i = 0; id != null && i < _tiles.Count; i++)
            {
                Tile tile = _tiles[i];
                if (tile.Slot.Index >= tile.First && tile.Slot.Index < tile.First + tile.Count
                    && (tile.Slot.Ids[tile.Slot.Index] == id || tile.Slot.LegsIds[tile.Slot.Index] == id))
                {
                    return tile;
                }
            }
            return null;
        }

        // Marks the tile of the look that is worn in this category, if any, and shows its colourways.
        private void ShowPick(LookKind kind)
        {
            string worn = CharacterCatalogue.Selected(kind, _character.data.gender);
            var current = _markField.GetValue(_picker) as GameObject;
            for (int i = 0; i < _tiles.Count; i++)
            {
                _tiles[i].Mark.SetActive(false);
                if (_tiles[i].Icon != null)
                {
                    _tiles[i].Icon.sprite = Swatch(_tiles[i].Slot, EntryOf(_tiles[i]));
                }
            }
            _picked = TileOf(worn);
            if (worn == null)
            {
                // The game's own piece is worn: its entry is the marked one again.
                if (_gameMark != null)
                {
                    _gameMark.SetActive(true);
                    _markField.SetValue(_picker, _gameMark);
                }
                ReleaseColourRow();
                return;
            }
            if (current != null)
            {
                current.SetActive(false);
            }
            if (_gameMark != null)
            {
                _gameMark.SetActive(false);
            }
            if (_picked == null)
            {
                // A look of ours that has no tile here (the skirt of a dress, in the Legs grid): nothing is marked.
                ReleaseColourRow();
                return;
            }
            _picked.Mark.SetActive(true);
            _markField.SetValue(_picker, _picked.Mark);
            _colourRow.Clear();
            for (int i = 0; i < _picked.Count; i++)
            {
                _colourRow.Add(Swatch(_picked.Slot, _picked.First + i));
            }
            _customizer.appearanceColorPicker.SetList(_colourRow, _onColour, _picked.Slot.Index - _picked.First);
            _colourRowOurs = true;
        }

        // The colour row showed our swatches and no look of ours is worn any more: it is hidden until the game fills it again.
        private void ReleaseColourRow()
        {
            if (_colourRowOurs)
            {
                _colourRowOurs = false;
                _customizer.appearanceColorPicker.Hide();
            }
        }

        private void OnTile(Tile tile)
        {
            if (_failed)
            {
                return;
            }
            try
            {
                CharacterCatalogue.Pick(tile.Slot, EntryOf(tile), LookSource.Creator);
                if (KindOf(_category, out LookKind kind))
                {
                    ShowPick(kind);
                }
            }
            catch (Exception e)
            {
                Fail(e);
            }
        }

        private void OnColour(int colour)
        {
            if (_failed || _picked == null)
            {
                return;
            }
            try
            {
                Tile tile = _picked;
                CharacterCatalogue.Pick(tile.Slot, tile.First + Mathf.Clamp(colour, 0, tile.Count - 1), LookSource.Creator);
                if (tile.Icon != null)
                {
                    tile.Icon.sprite = Swatch(tile.Slot, EntryOf(tile));
                }
            }
            catch (Exception e)
            {
                Fail(e);
            }
        }

        private void Fail(Exception error)
        {
            if (_failed)
            {
                return;
            }
            _failed = true;
            _logger?.Warn("Looks: the mod's entries in the creator's grids are switched off for this session (" + error.Message + "); the game's own entries are untouched. (Logged once.)");
            _logger?.Error(error);
            try
            {
                RemoveTiles();
                ReleaseColourRow();
            }
            catch (Exception e)
            {
                _logger?.Error(e);
            }
        }

        // ---------------------------------------------------------------- swatches

        /// <summary>
        /// A small picture of one colourway: the cloth's colour with its print drawn in (the three colours are the
        /// item's own tints from the bundle's catalogue; nothing is read from a texture). Made once per colourway
        /// shown, 48 px, and destroyed with the creator.
        /// </summary>
        private Sprite Swatch(CharacterCatalogue.Slot slot, int entry)
        {
            string id = slot.Ids[entry];
            if (_swatches.TryGetValue(id, out Sprite made) && made != null)
            {
                return made;
            }
            string colourway = id.Substring(slot.Meshes[entry].Length + 1);
            Color32 cloth = new Color32(128, 128, 128, 255);
            Color32 print = cloth;
            Color32 trim = cloth;
            LookItem item = ModAssets.Find(id);
            Color[] tints = item != null && item.Layers != null && item.Layers.Length > 0 ? item.Layers[0].Tints : null;
            if (tints != null && tints.Length >= 2)
            {
                cloth = Opaque(tints[1]);
                print = Opaque(tints[0]);
                trim = tints.Length > 2 ? Opaque(tints[2]) : cloth;
            }
            else
            {
                if (ShoeColours.TryGetValue(colourway, out Color32 leather))
                {
                    cloth = leather;
                }
                string mesh = slot.Meshes[entry];
                string style = mesh.Substring(mesh.LastIndexOf('_') + 1);
                trim = ShoeAccents.TryGetValue(style + "_" + colourway, out Color32 accent) ? accent : cloth;
                print = cloth;
                colourway = "trim";
            }
            // A garment that carries the colourway in one of the two other colours only: that one is its second colour.
            if (Same(print, cloth))
            {
                print = trim;
            }
            if (Same(trim, cloth))
            {
                trim = print;
            }
            for (int y = 0; y < SwatchSize; y++)
            {
                for (int x = 0; x < SwatchSize; x++)
                {
                    _pixels[y * SwatchSize + x] = Pixel(colourway, x, y, cloth, print, trim);
                }
            }
            var texture = new Texture2D(SwatchSize, SwatchSize, TextureFormat.RGBA32, false) { name = OurName + " swatch " + id, wrapMode = TextureWrapMode.Clamp };
            texture.SetPixels32(_pixels);
            texture.Apply(false, true);
            made = Sprite.Create(texture, new Rect(0f, 0f, SwatchSize, SwatchSize), new Vector2(0.5f, 0.5f), 100f);
            made.name = texture.name;
            _swatches[id] = made;
            return made;
        }

        private static bool Same(Color32 a, Color32 b)
        {
            return Mathf.Abs(a.r - b.r) + Mathf.Abs(a.g - b.g) + Mathf.Abs(a.b - b.b) < 12;
        }

        private static Color32 Opaque(Color colour)
        {
            Color32 c = colour;
            c.a = 255;
            return c;
        }

        // One pixel of a colourway's swatch. The prints are drawn at the scale they read at on a garment seen whole.
        private static Color32 Pixel(string colourway, int x, int y, Color32 cloth, Color32 print, Color32 trim)
        {
            if (x == 0 || y == 0 || x == SwatchSize - 1 || y == SwatchSize - 1)
            {
                return Color32.Lerp(cloth, new Color32(0, 0, 0, 255), 0.45f);
            }
            switch (colourway)
            {
                case "dots":
                case "dotsred":
                    return Dot(x, y, 12, 3.2f) ? print : cloth;
                case "finedots":
                    return Dot(x, y, 6, 1.5f) ? print : cloth;
                case "breton":
                    return y % 10 < 4 ? print : cloth;
                case "pinstripe":
                    return x % 8 == 3 ? print : cloth;
                case "gingham":
                {
                    bool across = x % 12 < 6;
                    bool up = y % 12 < 6;
                    return across && up ? print : across || up ? Color32.Lerp(cloth, print, 0.5f) : cloth;
                }
                case "block":
                    return y < SwatchSize / 2 ? trim : cloth;
                case "trim":
                case "ringer":
                    return y >= SwatchSize - 9 ? trim : cloth;
                case "heather":
                {
                    // A fixed speckle: the melange of a heather yarn.
                    int hash = (x * 73856093) ^ (y * 19349663);
                    float shade = ((hash & 255) / 255f - 0.5f) * 0.22f;
                    return Color32.Lerp(cloth, shade < 0f ? new Color32(0, 0, 0, 255) : new Color32(255, 255, 255, 255), Mathf.Abs(shade));
                }
                default:
                    return cloth;
            }
        }

        private static bool Dot(int x, int y, int pitch, float radius)
        {
            // Every other row is set off by half a pitch, as dots are printed.
            int row = y / pitch;
            float cx = (x + (row % 2 == 0 ? 0 : pitch / 2)) % pitch - pitch * 0.5f;
            float cy = y % pitch - pitch * 0.5f;
            return cx * cx + cy * cy <= radius * radius;
        }
    }
}
