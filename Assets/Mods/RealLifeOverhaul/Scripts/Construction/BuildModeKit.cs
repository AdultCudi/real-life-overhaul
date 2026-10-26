using System;
using System.Collections.Generic;
using BAModAPI;
using RealLifeOverhaul.GameUi;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RealLifeOverhaul.Construction
{
    /// <summary>
    /// The parts build mode's interface is made of, all copies of the game's interior designer (UIs.interiorDesignerUI,
    /// Canvases/InteriorDesignerUI): its toolbar's section (the dark box, sprite Box-Darker-Small-125), one of its tool buttons
    /// (a Button with the game's hover and click effects, its blue SelectedBackground, sprite Button-Blue, and its Icon), its
    /// divider (Square-With-Padding-8) and a label of its panels (TextMeshPro 32, the game's font). The icons are the game's own
    /// sprites: the designer's toolbar's, read from it, or others by name (GameSprites). Copied asleep once at the first opening;
    /// each part made from them as the interface needs it. Nothing runs a frame.
    /// </summary>
    internal sealed class BuildModeKit
    {
        public static IModLogger Logger;

        public const float LabelSize = 32f;   // the designer's panels' own text size, in the canvas's units (3840 by 2160)

        private GameObject _section;   // the toolbar's section: the box, its layout, asleep with nothing in it
        private GameObject _button;    // one tool button, its scripts but the effects taken off
        private GameObject _divider;
        private GameObject _label;
        private readonly Dictionary<string, Sprite> _icons = new Dictionary<string, Sprite>(32, StringComparer.Ordinal);

        /// <summary>The dark box of the designer's toolbar.</summary>
        public Sprite Box { get; private set; }

        /// <summary>The game's blue of a chosen tool and of a main button.</summary>
        public Sprite Blue { get; private set; }

        /// <summary>The thin rounded bar of the toolbar's dividers: a line, a track, a filled bar.</summary>
        public Sprite Line { get; private set; }

        /// <summary>A white rounded box of the game's with nothing see-through round it (white-rounded-box, else round-corner-no-drop): a card, a button, a swatch, tinted.</summary>
        public Sprite White { get; private set; }

        /// <summary>The game's one white pixel: a plain fill (the tour's dimming, its outline), tinted; null without it.</summary>
        public Sprite Fill { get; private set; }

        private static readonly string[] BoxSprites = { "white-rounded-box", "round-corner-no-drop" };   // 192 square borders 25, 74 by 58 borders 10; 56 and 60 of the UI scene's Images use them
        private const string FillSprite = "1pixelwhite";

        /// <summary>
        /// True when a sprite is packed with a see-through margin cut away (Unity's tight packing): drawn as a box it fills less than
        /// its rectangle, by its margin scaled to the box (UnityEngine.Sprites.DataUtility.GetPadding).
        /// </summary>
        public static bool Padded(Sprite sprite)
        {
            return sprite != null && UnityEngine.Sprites.DataUtility.GetPadding(sprite) != Vector4.zero;
        }

        /// <summary>The parts copied asleep under <paramref name="dormant"/>; null, with one line in Player.log, if the game's designer is not built as expected.</summary>
        public static BuildModeKit Take(Transform dormant)
        {
            try
            {
                Transform designer = BuildModeGame.Designer();
                if (designer == null)
                {
                    UiClone.Fallback("the game's interior designer is not there to copy build mode's toolbar from");
                    return null;
                }
                Transform section = UiClone.Need(designer, "Topbar/Section", "build mode's toolbar");
                Transform label = UiClone.Need(designer, "ActionPanels/QueuePanel/NoPointOfSaleItemsLabel", "build mode's toolbar");
                Transform white = UiClone.Need(designer, "InteriorElementColorPicker/Background", "build mode's toolbar");
                if (section == null || label == null)
                {
                    return null;
                }
                var kit = new BuildModeKit();
                kit.ReadIcons(designer.Find("Topbar"));
                Image whiteImage = white != null ? white.GetComponent<Image>() : null;
                // A box's picture must fill its rectangle. The colour picker's white-round (232 square, borders 64) is packed tight:
                // 50 of its pixels each side are cut away as see-through, so a row 116 high drew as a thin line through the row and
                // a swatch of 60 as a dot (the owner's test, 26 Oct 2026). The game's rounded white boxes with nothing see-through
                // come first; the picker's only if it has no such margin.
                Sprite picker = whiteImage != null ? whiteImage.sprite : null;
                kit.White = Find(BoxSprites[0]) ?? Find(BoxSprites[1]) ?? (picker != null && !Padded(picker) ? picker : null);
                kit.Fill = Find(FillSprite);
                if (!kit.Copy(section.gameObject, label.gameObject, dormant))
                {
                    return null;
                }
                return kit;
            }
            catch (Exception e)
            {
                Logger?.Error(e);
                UiClone.Fallback("build mode's toolbar could not be copied from the game's interior designer");
                return null;
            }
        }

        // Every icon of the designer's toolbar by its sprite's name: they are certainly in memory while the designer is.
        private void ReadIcons(Transform topbar)
        {
            if (topbar == null)
            {
                return;
            }
            foreach (Image image in topbar.GetComponentsInChildren<Image>(true))
            {
                if (image != null && image.sprite != null && image.name == "Icon" && !_icons.ContainsKey(image.sprite.name))
                {
                    _icons.Add(image.sprite.name, image.sprite);
                }
            }
        }

        private bool Copy(GameObject section, GameObject label, Transform dormant)
        {
            _section = UiClone.Asleep(section, dormant, "Build mode section");
            _label = UiClone.Asleep(label, dormant, "Build mode label");
            if (_section == null || _label == null)
            {
                return false;
            }
            Transform button = null, divider = null, background = _section.transform.Find("Background");
            for (int i = 0; i < _section.transform.childCount; i++)
            {
                Transform child = _section.transform.GetChild(i);
                if (button == null && child.Find("SelectedBackground") != null && child.Find("Icon") != null && child.GetComponent<Button>() != null)
                {
                    button = child;
                }
                else if (divider == null && child.name.StartsWith("Divider", StringComparison.Ordinal))
                {
                    divider = child;
                }
            }
            if (button == null || divider == null || background == null)
            {
                UiClone.Fallback("build mode's toolbar: the designer's section has no tool button, divider or box as expected");
                return false;
            }
            Strip(_section);   // the hover and click are the game's; the tool, its key tooltip and its hints go
            Strip(_label);
            _button = UnityEngine.Object.Instantiate(button.gameObject, dormant, false);
            _button.name = "Build mode button";
            _divider = UnityEngine.Object.Instantiate(divider.gameObject, dormant, false);
            _divider.name = "Build mode divider";
            for (int i = _section.transform.childCount - 1; i >= 0; i--)
            {
                Transform child = _section.transform.GetChild(i);
                if (child != background)
                {
                    UnityEngine.Object.DestroyImmediate(child.gameObject);
                }
            }
            Box = background.GetComponent<Image>()?.sprite;
            Blue = _button.transform.Find("SelectedBackground")?.GetComponent<Image>()?.sprite;
            Line = _divider.GetComponent<Image>()?.sprite;
            var text = _label.GetComponent<TMP_Text>();
            if (text != null)
            {
                text.richText = true;
                text.margin = Vector4.zero;
                text.raycastTarget = false;
            }
            return Box != null && Blue != null && text != null;
        }

        // Every script of the game's (of any of its assemblies) taken off a sleeping copy but what only draws, sounds or writes its words:
        // the designer's ToolButton, ActionKeyTooltip and ConfigurableControlsHintsToggler would wake with it and act on the game's.
        private static void Strip(GameObject copy)
        {
            var all = new List<MonoBehaviour>(32);
            for (int pass = 0; pass < 3; pass++)
            {
                copy.GetComponentsInChildren(true, all);
                for (int i = all.Count - 1; i >= 0; i--)
                {
                    MonoBehaviour m = all[i];
                    if (m == null || Keep(m))
                    {
                        continue;
                    }
                    UnityEngine.Object.DestroyImmediate(m);   // one another script requires stays until that one has gone (the next pass)
                }
            }
        }

        private static bool Keep(MonoBehaviour m)
        {
            Type t = m.GetType();
            string space = t.Namespace ?? string.Empty;
            return space.StartsWith("UnityEngine", StringComparison.Ordinal) || space.StartsWith("TMPro", StringComparison.Ordinal) || t.Name == "ButtonEffects"
                   || t.Name == "TextLocalizationComponent";
        }

        /// <summary>Every picture of the game's build mode asks for by name, wanted at the one look behind the loading screen (BuildMode.Awake).</summary>
        public static readonly string[] Names =
        {
            "Icon-BuildingForSale", "Icon-Palette", "Icon-Building", "Icon-Delete", "Icon-CopyOutline-64", "Icon-Edit", "Icon-RealEstate", "Icon-Map", "Icon-MarketValue", "Icon-People",
            "Icon-Construction", "Icon-Coin", "Icon-Floors", "Icon-Walls", "Icon-Hand", "Icon-Close", "circle", "Icon-Residential", "Icon-Retail", "Icon-Office", "Icon-Cargo",
            "white-rounded-box", "round-corner-no-drop", "1pixelwhite"
        };

        // A picture among the sprites in memory by one of the names wanted; null for a name not among them.
        private static Sprite Find(string name)
        {
            int i = Array.IndexOf(Names, name);
            return i >= 0 ? GameSprites.Get(Names[i]) : null;
        }

        /// <summary>An icon of the game's: one of the designer's toolbar's, else by name among the sprites in memory, else the second name's.</summary>
        public Sprite Icon(string name, string otherwise)
        {
            if (_icons.TryGetValue(name, out Sprite s) && s != null)
            {
                return s;
            }
            s = Find(name);
            if (s != null)
            {
                return s;
            }
            if (otherwise == null)
            {
                return null;
            }
            return _icons.TryGetValue(otherwise, out Sprite o) && o != null ? o : Find(otherwise);
        }

        // ------------------------------------------------------------------ the parts

        /// <summary>The toolbar's dark box: an empty rectangle of its sprite at its own tint, with a layout group of the kind given.</summary>
        public RectTransform Panel(Transform parent, string name, bool across, RectOffset padding, float spacing)
        {
            GameObject box = UnityEngine.Object.Instantiate(_section, parent, false);
            box.name = name;
            UnityEngine.Object.DestroyImmediate(box.GetComponent<HorizontalLayoutGroup>());
            UnityEngine.Object.DestroyImmediate(box.GetComponent<ContentSizeFitter>());
            Group(box, across, padding, spacing, TextAnchor.MiddleCenter);
            var fitter = box.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var rect = (RectTransform)box.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            box.SetActive(true);
            return rect;
        }

        /// <summary>A plain rectangle that lays out its children in a row or a column (no picture).</summary>
        public static RectTransform Strip(Transform parent, string name, bool across, RectOffset padding, float spacing, TextAnchor align)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            go.transform.SetParent(parent, false);
            Group(go, across, padding, spacing, align);
            return (RectTransform)go.transform;
        }

        private static void Group(GameObject go, bool across, RectOffset padding, float spacing, TextAnchor align)
        {
            HorizontalOrVerticalLayoutGroup g = across ? go.AddComponent<HorizontalLayoutGroup>() : (HorizontalOrVerticalLayoutGroup)go.AddComponent<VerticalLayoutGroup>();
            g.padding = padding ?? new RectOffset();
            g.spacing = spacing;
            g.childAlignment = align;
            g.childControlWidth = true;
            g.childControlHeight = true;
            g.childForceExpandWidth = !across;
            g.childForceExpandHeight = false;
        }

        /// <summary>A label of the designer's panels: one line or wrapping, in a size, colour and weight.</summary>
        public TMP_Text Text(Transform parent, string name, float size, Color colour, bool bold, bool wrap)
        {
            GameObject go = UnityEngine.Object.Instantiate(_label, parent, false);
            go.name = name;
            var t = go.GetComponent<TMP_Text>();
            t.fontSize = size;
            t.enableAutoSizing = false;
            t.color = colour;
            t.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
            t.alignment = TextAlignmentOptions.MidlineLeft;
            t.textWrappingMode = wrap ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
            t.overflowMode = TextOverflowModes.Overflow;   // never cut: the layout makes room (the test measures it)
            UiClone.Words(t, string.Empty);
            go.SetActive(true);
            return t;
        }

        /// <summary>A plain picture: a sprite in a colour, a fixed size in a layout, taking no pointer.</summary>
        public static Image Picture(Transform parent, string name, Sprite sprite, Color colour, float wide, float high)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            go.layer = parent.gameObject.layer;
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.type = sprite != null && sprite.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
            image.color = colour;
            image.raycastTarget = false;
            image.preserveAspect = sprite != null && sprite.border == Vector4.zero;
            Size(go, wide, high);
            ((RectTransform)go.transform).sizeDelta = new Vector2(wide, high);
            return image;
        }

        /// <summary>A part's fixed size in a layout (a negative side is left to the layout).</summary>
        public static LayoutElement Size(GameObject go, float wide, float high)
        {
            var e = go.GetComponent<LayoutElement>() ?? go.AddComponent<LayoutElement>();
            if (wide >= 0f)
            {
                e.minWidth = e.preferredWidth = wide;
            }
            if (high >= 0f)
            {
                e.minHeight = e.preferredHeight = high;
            }
            return e;
        }

        /// <summary>The toolbar's divider, standing in a row.</summary>
        public Image Divider(Transform parent, float high, Color colour)
        {
            GameObject go = UnityEngine.Object.Instantiate(_divider, parent, false);
            go.SetActive(true);
            Size(go, 4f, high);
            var image = go.GetComponent<Image>();
            image.color = colour;
            return image;
        }

        /// <summary>A copy of the designer's tool button, its click given; its icon kept or taken off.</summary>
        public KitButton Button(Transform parent, string name, Action click, float wide, float high)
        {
            GameObject go = UnityEngine.Object.Instantiate(_button, parent, false);
            go.name = name;
            go.SetActive(true);
            var b = new KitButton
            {
                Root = (RectTransform)go.transform, Button = UiClone.Rewire(go.transform, click), Back = go.GetComponent<Image>(),
                Chosen = go.transform.Find("SelectedBackground")?.gameObject, Icon = go.transform.Find("Icon")?.GetComponent<Image>(), Group = go.AddComponent<CanvasGroup>()
            };
            if (b.Chosen != null)
            {
                b.Chosen.SetActive(false);
            }
            Size(go, wide, high);
            b.Root.sizeDelta = new Vector2(wide, high);
            return b;
        }

        public const float EdgeWide = 3f;   // a framed button's edge, in the canvas's units (a pixel and a half at 1080 lines)

        public static readonly Color EdgeColour = new Color(0.227f, 0.251f, 0.282f, 1f);       // the mockup's #3a4048: a button's edge on the dark box
        public static readonly Color EdgeChosen = new Color(0.416f, 0.655f, 1f, 1f);           // #6aa7ff: the one picked
        public static readonly Color FaceChosen = new Color(0.137f, 0.251f, 0.431f, 1f);       // #23406e

        /// <summary>
        /// A button that reads as one on the dark box (the owner, 26 Oct 2026: the zone rows did not): a rounded face in its colour
        /// inside a thin lighter edge, as the approved mockup draws them; picked, a blue edge round a dark blue face. The face sits
        /// under the button's own parts; the designer's blue under a chosen tool is left to the toolbar's buttons.
        /// </summary>
        public void Frame(KitButton b, Color face, bool useChosen)
        {
            if (b == null || b.Back == null)
            {
                return;
            }
            Sprite box = White ?? Line;
            b.Back.sprite = box;
            b.Back.type = box != null && box.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
            b.Back.color = EdgeColour;
            if (b.Face == null)
            {
                Image f = Picture(b.Root, "Face", box, face, 0f, 0f);
                f.type = b.Back.type;
                f.GetComponent<LayoutElement>().ignoreLayout = true;
                var r = (RectTransform)f.transform;
                r.anchorMin = Vector2.zero;
                r.anchorMax = Vector2.one;
                r.offsetMin = new Vector2(EdgeWide, EdgeWide);
                r.offsetMax = new Vector2(-EdgeWide, -EdgeWide);
                r.SetAsFirstSibling();   // under the designer's blue, the icon and the words
                b.Face = f;
            }
            b.FaceOff = face;
            b.Face.color = face;
            b.Framed = true;
            b.ChosenBlue = useChosen;
            if (!useChosen && b.Chosen != null)
            {
                b.Chosen.SetActive(false);
            }
        }

        public void Destroy()
        {
            foreach (GameObject go in new[] { _section, _button, _divider, _label })
            {
                if (go != null)
                {
                    UnityEngine.Object.Destroy(go);
                }
            }
        }
    }

    /// <summary>A button made from the designer's tool button: its look for chosen, usable and not.</summary>
    internal sealed class KitButton
    {
        public RectTransform Root;
        public Button Button;
        public Image Back;          // the button's own picture: none on a tool, the box or the blue on a wide button
        public GameObject Chosen;   // the designer's blue under a chosen tool
        public Image Icon;
        public TMP_Text Label;
        public CanvasGroup Group;
        public BuildModeTip Tip;
        public Image Face;          // a framed button's face inside its edge (BuildModeKit.Frame), else none
        public Color FaceOff;
        public bool Framed;
        public bool ChosenBlue = true;   // the designer's blue shows under it while chosen (a framed row shows its blue edge instead)
        private bool _usable = true;
        private bool _chosen;

        public bool Usable => _usable;

        /// <summary>True while it is the one chosen.</summary>
        public bool IsChosen => _chosen;

        /// <summary>The game's blue under it while it is the one chosen; a framed button's edge and face turn blue.</summary>
        public void SetChosen(bool chosen)
        {
            _chosen = chosen;
            bool blue = chosen && ChosenBlue;
            if (Chosen != null && Chosen.activeSelf != blue)
            {
                Chosen.SetActive(blue);
            }
            if (Framed && Back != null)
            {
                Back.color = chosen ? BuildModeKit.EdgeChosen : BuildModeKit.EdgeColour;
                if (Face != null)
                {
                    Face.color = chosen && !ChosenBlue ? BuildModeKit.FaceChosen : FaceOff;
                }
            }
        }

        /// <summary>Usable or not: a button that is not is greyed (faded, its words and icon grey) and takes no click, though its tooltip still says why.</summary>
        public void SetUsable(bool usable)
        {
            _usable = usable;
            if (Button != null)
            {
                Button.interactable = usable;
            }
            if (Group != null)
            {
                Group.alpha = usable ? 1f : 0.42f;
            }
            if (Icon != null)
            {
                Icon.color = usable ? Color.white : new Color(0.62f, 0.64f, 0.67f, 1f);
            }
        }

        public void Show(bool shown)
        {
            if (Root != null && Root.gameObject.activeSelf != shown)
            {
                Root.gameObject.SetActive(shown);
            }
        }
    }
}
