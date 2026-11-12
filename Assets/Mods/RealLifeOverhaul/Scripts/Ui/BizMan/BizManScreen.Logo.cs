using System.Collections.Generic;
using UI.Smartphone.Apps.BizMan;
using UnityEngine;
using UnityEngine.UI;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The logo editor of a business's settings, read from the game's own LogoCustomizer: its two lists of shapes and its three lists of
    /// colours are read after the game built them (every entry's picture and button), and a pick on the kit presses that entry's
    /// own button, so the customizer's handlers run as for a click and its Save logo button wakes by itself. The logo shown is painted
    /// once a pick from the customizer's own current picks. Cost: a look at a few counts four times a second while it shows; a
    /// layout and one 128 px picture a pick.
    /// </summary>
    public sealed partial class BizManScreen
    {
        private const int PreviewSide = 128;   // px: the logo as the kit paints it; the game's own render is made when the logo is saved
        private const int Rows = 3;            // the shape's colour, the name's, the background

        private static Sprite _preview;
        private static long _previewFor;

        private bool _logoEdit, _ownOpened;
        private int _logoTab;
        private readonly List<Button> _shapeButtons = new List<Button>(160);
        private readonly List<Button>[] _colourButtons = { new List<Button>(32), new List<Button>(32), new List<Button>(32) };
        private readonly ColorListUI[] _colourLists = new ColorListUI[Rows];
        private string[] _shapeKeys = new string[160];
        private readonly string[][] _colourKeys = { new string[32], new string[32], new string[32] };

        // The editor in the model: the shapes of the player first, then the game's; each colour row as the game lists it, the chosen one by the
        // customizer's current pick. Gives the main actions too: Done, and the game's Save logo.
        private void ReadLogo(LogoCustomizer logo, Button saveLogo)
        {
            _m.LogoEdit = true;
            _m.LogoTabs = new[] { UiText.T("reallife_ui_bizman_logo_shapes"), UiText.T("reallife_ui_bizman_logo_colours") };
            _m.LogoTab = _logoTab;
            _m.PartTitle = UiText.T("reallife_ui_bizman_logo_more");
            _m.ShapesTitle = UiText.T("reallife_ui_bizman_logo_shapes");
            _m.ShapesNone = UiText.T("reallife_ui_bizman_logo_shapes_none");
            _m.LogoNote = UiText.T("reallife_ui_bizman_logo_note");
            _m.ShapesOwn = Get<LogoShapes>(GameContract.LogoShapesOf, logo) != null ? UiText.T("reallife_ui_bizman_logo_own") : null;
            string chosenShape = Get<string>(GameContract.LogoShapeNow, logo);
            _shapeButtons.Clear();
            int chosen = -1;
            LogoShapes shapes = Get<LogoShapes>(GameContract.LogoShapesOf, logo);
            Shapes(Get<LogoShapesList>(GameContract.LogoShapesPlayer, shapes), chosenShape, ref chosen);
            Shapes(Get<LogoShapesList>(GameContract.LogoShapesBuiltIn, shapes), chosenShape, ref chosen);
            _m.ShapePics = _shapeKeys;
            _m.ShapeCount = _shapeButtons.Count;
            _m.ShapeChosen = chosen;

            var colours = new BizManColours[Rows];
            Colours(0, Get<ColorListUI>(GameContract.LogoShapeColours, logo), "reallife_ui_bizman_logo_colour_shape", GameContract.LogoShapeColourNow, logo, ref colours[0]);
            Colours(1, Get<ColorListUI>(GameContract.LogoFontColours, logo), "reallife_ui_bizman_logo_colour_text", GameContract.LogoFontColourNow, logo, ref colours[1]);
            Colours(2, Get<ColorListUI>(GameContract.LogoBackColours, logo), "reallife_ui_bizman_logo_colour_back", GameContract.LogoBackColourNow, logo, ref colours[2]);
            _m.Colours = colours;
            _m.PreviewPic = Preview(chosenShape, ColourNow(GameContract.LogoShapeColourNow, logo), ColourNow(GameContract.LogoBackColourNow, logo));

            _actions.Clear();
            _actions.Add(Do(BizManPages.DoLogoDone, UiText.T("reallife_ui_bizman_logo_done"), UiButtonKind.Quiet, UiIcon.None, UiAction.Count));
            _actions.Add(Do(BizManPages.DoSaveLogo, UiText.T("bizman_settings_save_logo"), UiButtonKind.Primary, UiIcon.Check, UiAction.Alt, saveLogo == null || !saveLogo.interactable));
            _m.Actions = _actions.ToArray();
        }

        // One list of the game's shapes: its entries are copies of its template, shown, each with its picture and its button.
        private void Shapes(LogoShapesList list, string chosenShape, ref int chosen)
        {
            Transform template = Get<Transform>(GameContract.LogoShapeTemplate, list);
            Transform box = template != null ? template.parent : null;
            for (int i = 0; box != null && i < box.childCount; i++)
            {
                Transform child = box.GetChild(i);
                Image image = child != template && child.gameObject.activeSelf ? child.GetComponent<Image>() : null;
                Button button = image != null && image.sprite != null ? child.GetComponent<Button>() : null;
                if (button == null)
                {
                    continue;
                }
                int at = _shapeButtons.Count;
                if (at == _shapeKeys.Length)
                {
                    System.Array.Resize(ref _shapeKeys, at * 2);
                }
                _shapeKeys[at] = WorldPictures.Of(image.sprite);
                if (chosen < 0 && chosenShape != null && image.sprite.name == chosenShape)
                {
                    chosen = at;
                }
                _shapeButtons.Add(button);
            }
        }

        // One row of the game's colours, the player's own first, as its list built them; the add button leads it where the game shows it.
        private void Colours(int row, ColorListUI list, string label, GameMember now, LogoCustomizer logo, ref BizManColours into)
        {
            _colourLists[row] = list;
            _colourButtons[row].Clear();
            Transform box = Get<Transform>(GameContract.SignColours, list);
            Transform add = Get<Transform>(GameContract.SignAddColour, list);
            Color32 current = ColourNow(now, logo);
            int chosen = -1;
            string[] keys = _colourKeys[row];
            for (int i = 0; box != null && i < box.childCount; i++)
            {
                Transform child = box.GetChild(i);
                Image image = child != add && child.gameObject.activeSelf ? child.GetComponent<Image>() : null;
                Button button = image != null ? child.GetComponent<Button>() : null;
                if (button == null)
                {
                    continue;
                }
                int at = _colourButtons[row].Count;
                if (at == keys.Length)
                {
                    System.Array.Resize(ref keys, at * 2);
                    _colourKeys[row] = keys;
                }
                keys[at] = WorldPictures.Of(image.color);
                Color32 c = image.color;
                if (chosen < 0 && c.r == current.r && c.g == current.g && c.b == current.b)
                {
                    chosen = at;
                }
                _colourButtons[row].Add(button);
            }
            into = new BizManColours { Label = UiText.T(label), Pics = keys, Count = _colourButtons[row].Count, Chosen = chosen, CanAdd = list != null && add != null && add.gameObject.activeSelf };
        }

        private static Color32 ColourNow(GameMember member, LogoCustomizer logo) =>
            logo != null && member.Field != null && member.Field.GetValue(logo) is Color c ? (Color32)c : new Color32(255, 255, 255, 255);

        // The logo as it stands: the shape's sprite tinted by its colour on the background's, painted again only when a pick changed one of
        // the three. The shape's pixels are read back through the GPU, as a sprite the game compressed cannot be read on the CPU.
        private static string Preview(string shape, Color32 shapeColour, Color32 back)
        {
            Sprite sprite = shape != null ? LogoHelper.GetLogoSprite(shape) : null;
            if (sprite == null || sprite.texture == null)
            {
                return null;
            }
            long key = (long)shape.GetHashCode() * 31 + (shapeColour.r << 16 | shapeColour.g << 8 | shapeColour.b) * 7L + ((long)(back.r << 16 | back.g << 8 | back.b) << 24);
            if (_preview == null || key != _previewFor)
            {
                Sprite painted = Paint(sprite.texture, shapeColour, back);
                if (painted != null)
                {
                    CodeAssets.DropSprite(_preview);
                    _preview = painted;
                    _previewFor = key;
                }
            }
            return Pic(_preview);
        }

        private static Sprite Paint(Texture source, Color32 tint, Color32 back)
        {
            RenderTexture rt = RenderTexture.GetTemporary(PreviewSide, PreviewSide, 0, RenderTextureFormat.ARGB32);
            RenderTexture before = RenderTexture.active;
            var read = new Texture2D(PreviewSide, PreviewSide, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
            try
            {
                Graphics.Blit(source, rt);
                RenderTexture.active = rt;
                read.ReadPixels(new Rect(0f, 0f, PreviewSide, PreviewSide), 0, 0);
            }
            catch (System.Exception)
            {
                UnityEngine.Object.Destroy(read);   // the sprite the game compressed could not be read back: no preview this time
                return null;
            }
            finally
            {
                RenderTexture.active = before;
                RenderTexture.ReleaseTemporary(rt);
            }
            Color32[] px = read.GetPixels32();
            for (int i = 0; i < px.Length; i++)
            {
                // The game tints the sprite by multiplying it, then lays it over the background.
                int a = px[i].a;
                px[i] = new Color32(
                    (byte)((back.r * (255 - a) + px[i].r * tint.r / 255 * a) / 255),
                    (byte)((back.g * (255 - a) + px[i].g * tint.g / 255 * a) / 255),
                    (byte)((back.b * (255 - a) + px[i].b * tint.b / 255 * a) / 255), 255);
            }
            read.SetPixels32(px);
            read.Apply(false, false);
            Sprite sprite = Sprite.Create(read, new Rect(0f, 0f, PreviewSide, PreviewSide), new Vector2(0.5f, 0.5f), 100f);
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }

        // What changes the editor: the customizer's picks and how many entries its five lists hold (the shapes load a few a frame).
        private long LogoSignature(LogoCustomizer logo)
        {
            long sig = (_logoEdit ? 1 : 0) + _logoTab * 2;
            if (!_logoEdit || logo == null)
            {
                return sig;
            }
            sig = sig * 31 + Hash(Get<string>(GameContract.LogoShapeNow, logo));
            sig = sig * 31 + Colour32(ColourNow(GameContract.LogoShapeColourNow, logo)) + Colour32(ColourNow(GameContract.LogoFontColourNow, logo)) * 3
                  + Colour32(ColourNow(GameContract.LogoBackColourNow, logo)) * 5;
            LogoShapes shapes = Get<LogoShapes>(GameContract.LogoShapesOf, logo);
            sig = sig * 31 + Entries(Get<LogoShapesList>(GameContract.LogoShapesPlayer, shapes)) * 7 + Entries(Get<LogoShapesList>(GameContract.LogoShapesBuiltIn, shapes));
            sig = sig * 31 + Count(GameContract.LogoShapeColours, logo) * 5 + Count(GameContract.LogoFontColours, logo) * 3 + Count(GameContract.LogoBackColours, logo);
            return sig;
        }

        private static long Colour32(Color32 c) => c.r << 16 | c.g << 8 | c.b;

        private static int Entries(LogoShapesList list)
        {
            Transform template = Get<Transform>(GameContract.LogoShapeTemplate, list);
            return template != null && template.parent != null ? template.parent.childCount : -1;
        }

        private static int Count(GameMember member, LogoCustomizer logo)
        {
            Transform box = Get<Transform>(GameContract.SignColours, Get<ColorListUI>(member, logo));
            return box != null ? box.childCount : -1;
        }

        // ------------------------------------------------------------------------------------------------- presses

        // A shape: the game's own entry button, which its list answers with the selection and the customizer's handler.
        private bool PressShape(int index) =>
            index >= 0 && index < _shapeButtons.Count && Click(_shapeButtons[index], "choose a logo shape");

        private bool PressColour(int row, int index) =>
            row >= 0 && row < Rows && index >= 0 && index < _colourButtons[row].Count && Click(_colourButtons[row][index], "choose a logo colour");

        // The add button of a row opens the game's own colour picker (framed by the kit in turn); its answer comes back through the list.
        private bool PressAddColour(int row)
        {
            ColorListUI list = row >= 0 && row < Rows ? _colourLists[row] : null;
            if (list == null)
            {
                return false;
            }
            Transform add = Get<Transform>(GameContract.SignAddColour, list);
            Button button = add != null ? add.GetComponent<Button>() : null;
            if (button != null)
            {
                return Click(button, "add a logo colour");
            }
            list.AddNewColor();
            return true;
        }

        // The folder the game reads the player's own shapes from, opened as the game's own button opens it; the lists read again next time.
        private bool PressOwnShapes(BizManBusiness b)
        {
            LogoShapes shapes = Get<LogoShapes>(GameContract.LogoShapesOf, Get<LogoCustomizer>(GameContract.SetLogo, b.bizManSettings));
            if (shapes == null)
            {
                return false;
            }
            shapes.BrowseFiles();
            _ownOpened = true;
            return true;
        }

        private bool OpenLogoEditor(BizManBusiness b)
        {
            LogoShapes shapes = Get<LogoShapes>(GameContract.LogoShapesOf, Get<LogoCustomizer>(GameContract.SetLogo, b.bizManSettings));
            if (_ownOpened && shapes != null)
            {
                shapes.RefreshLogoShapes();   // the game's own refresh: the shapes the player put in the folder
                _ownOpened = false;
            }
            _logoEdit = true;
            _kindOpen = _fontOpen = false;
            _logoTab = 0;
            _m.ShapesOffset = 0f;
            _m.ShapesReveal = -1;
            return true;
        }
    }
}
