using System;
using RealLifeOverhaul.GameUi;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RealLifeOverhaul.Construction
{
    /// <summary>
    /// Build mode's tour at its first opening (the owner, 26 Oct 2026): four tips, each a white card beside the part it is about
    /// (the hint bar, the tools, the views, the demand), that part outlined in yellow and the map dimmed behind; "Tip 1 of 4",
    /// a heading, two sentences, four dots, "Skip the tour" and "Next" (on the last, "Done"), inside the mockup's padding. The
    /// card stands where it meets neither its part nor any other part shown, the tool panel above all (BuildModeHintRules.TourCard:
    /// the owner's test found it over the tool panel, its words at its edges). Remembered once seen, for every save (one
    /// preference). Its parts are the designer's (BuildModeKit) under build mode's interface; made at the first tour. Nothing
    /// runs a frame.
    /// </summary>
    internal sealed class BuildModeTour
    {
        private const string Pref = "reallife.buildmode.tour";   // not a locale key: a dot, not an underscore
        private const float CardWide = 1120f;
        private const float Ring = 6f;           // the outline's thickness
        private const float RingOut = 14f;       // ... and how far out of the part
        private const float Apart = 48f;         // between the part and the card
        private const int PadSide = 48;          // the card's padding: the mockup's 24 by 22 at 1080 lines
        private const int PadEnds = 44;
        private const int AvoidMost = 32;        // parts a card keeps clear of: build mode's and the game's HUD's

        private static readonly Color Yellow = new Color(1f, 0.81f, 0.35f, 1f);
        private static readonly Color Ink = new Color(0.1f, 0.11f, 0.13f, 1f);
        private static readonly Color InkSoft = new Color(0.23f, 0.26f, 0.3f, 1f);
        private static readonly Color DotOff = new Color(0.78f, 0.8f, 0.82f, 1f);

        private readonly BuildModeKit _kit;
        private readonly BuildModeBar _bar;
        private Image _dim;
        private readonly Image[] _ring = new Image[4];
        private RectTransform _card;
        private TMP_Text _count, _title, _body;
        private readonly Image[] _dots = new Image[4];
        private KitButton _skip, _next;
        private TourStep _step = TourStep.Done;
        private readonly UiBox[] _avoid = new UiBox[AvoidMost];
        private readonly System.Collections.Generic.List<RectTransform> _hud = new System.Collections.Generic.List<RectTransform>(24);
        private bool _placedClear;

        public BuildModeTour(BuildModeKit kit, BuildModeBar bar)
        {
            _kit = kit;
            _bar = bar;
        }

        /// <summary>True while a tip is up.</summary>
        public bool Running => _step != TourStep.Done;

        /// <summary>The step shown now (Done: none), for the tests.</summary>
        public TourStep Step => _step;

        /// <summary>True once the tour has been seen or skipped, on this machine.</summary>
        public static bool Seen
        {
            get
            {
                try
                {
                    return UnityEngine.PlayerPrefs.GetInt(Pref, 0) == 1;
                }
                catch (Exception)
                {
                    return false;
                }
            }
            set
            {
                UnityEngine.PlayerPrefs.SetInt(Pref, value ? 1 : 0);
                UnityEngine.PlayerPrefs.Save();
            }
        }

        /// <summary>Starts the tour at its first tip.</summary>
        public void Start()
        {
            if (_card == null)
            {
                Build();
            }
            Go(TourStep.HintBar);
        }

        /// <summary>The next tip, or the end after the last.</summary>
        public void Next() => Go(BuildModeHintRules.TourNext(_step, false));

        /// <summary>Ends the tour now and remembers it.</summary>
        public void Skip() => Go(TourStep.Done);

        /// <summary>Takes the tour away without remembering it (build mode closed under it).</summary>
        public void Hide()
        {
            _step = TourStep.Done;
            Show(false);
        }

        private void Go(TourStep step)
        {
            _step = step;
            if (step == TourStep.Done)
            {
                Show(false);
                Seen = true;
                return;
            }
            Show(true);
            int i = (int)step;
            UiClone.Words(_count, ConstructionText.Fill("reallife_bld_tour_count", "number", ConstructionText.N(i + 1), "count", ConstructionText.N(BuildModeHintRules.TourTitles.Length)));
            UiClone.Key(_title, BuildModeHintRules.TourTitles[i]);
            UiClone.Key(_body, BuildModeHintRules.TourBodies[i]);
            UiClone.Key(_next.Label, step == TourStep.Demand ? "reallife_bld_tour_done" : "reallife_bld_tour_next");
            for (int d = 0; d < _dots.Length; d++)
            {
                _dots[d].color = d == i ? BuildModeBar.StepBlue : DotOff;
            }
            Place(_bar.TourTarget(step), step == TourStep.HintBar);
        }

        private void Show(bool shown)
        {
            if (_card == null)
            {
                return;
            }
            _dim.gameObject.SetActive(shown);
            _card.gameObject.SetActive(shown);
            for (int r = 0; r < _ring.Length; r++)
            {
                _ring[r].gameObject.SetActive(shown);
            }
            if (shown)
            {
                _dim.transform.SetAsFirstSibling();   // under every part of the interface: the map dimmed, its clicks held
                for (int r = 0; r < _ring.Length; r++)
                {
                    _ring[r].transform.SetAsLastSibling();
                }
                _card.SetAsLastSibling();
            }
        }

        private void Build()
        {
            RectTransform root = _bar.Root;
            Sprite fill = _kit.Fill ?? _kit.White ?? _kit.Line;   // a plain fill: the dimming and the outline
            _dim = BuildModeKit.Picture(root, "Tour dim", fill, new Color(0.02f, 0.05f, 0.08f, 0.55f), 0f, 0f);
            _dim.type = Image.Type.Simple;
            _dim.raycastTarget = true;   // the map under the tour takes no click
            var dim = (RectTransform)_dim.transform;
            dim.anchorMin = Vector2.zero;
            dim.anchorMax = Vector2.one;
            dim.offsetMin = new Vector2(-400f, -400f);   // past every edge, past the corners' rounding
            dim.offsetMax = new Vector2(400f, 400f);
            for (int r = 0; r < _ring.Length; r++)
            {
                _ring[r] = BuildModeKit.Picture(root, "Tour outline", fill, Yellow, Ring, Ring);
                _ring[r].type = Image.Type.Simple;
            }

            var card = new GameObject("Tour tip", typeof(RectTransform), typeof(Image));
            card.layer = root.gameObject.layer;
            card.transform.SetParent(root, false);
            _card = (RectTransform)card.transform;
            var back = card.GetComponent<Image>();
            back.sprite = _kit.White ?? _kit.Line;
            back.type = back.sprite != null && back.sprite.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
            back.color = Color.white;
            var layout = card.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(PadSide, PadSide, PadEnds, PadEnds);
            layout.spacing = 22f;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            var fit = card.AddComponent<ContentSizeFitter>();
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            _card.sizeDelta = new Vector2(CardWide, 400f);
            _count = _kit.Text(_card, "Count", 30f, BuildModeBar.StepBlue, true, false);
            _count.fontStyle = FontStyles.Bold | FontStyles.UpperCase;
            _count.characterSpacing = 4f;
            _title = _kit.Text(_card, "Title", 48f, Ink, true, true);
            _body = _kit.Text(_card, "Words", 36f, InkSoft, false, true);
            RectTransform row = BuildModeKit.Strip(_card, "Buttons", true, new RectOffset(0, 0, 12, 0), 12f, TextAnchor.MiddleLeft);
            for (int d = 0; d < _dots.Length; d++)
            {
                _dots[d] = BuildModeKit.Picture(row, "Dot", _kit.Icon("circle", null) ?? _kit.White, DotOff, 20f, 20f);
            }
            var gap = new GameObject("Gap", typeof(RectTransform), typeof(LayoutElement));
            gap.transform.SetParent(row, false);
            gap.GetComponent<LayoutElement>().flexibleWidth = 1f;
            _skip = Button(row, "reallife_bld_tour_skip", Skip, 340f, false);
            _next = Button(row, "reallife_bld_tour_next", Next, 240f, true);
        }

        private KitButton Button(Transform row, string key, Action click, float wide, bool main)
        {
            KitButton b = _kit.Button(row, key, click, wide, 92f);
            if (b.Icon != null)
            {
                b.Icon.gameObject.SetActive(false);
            }
            b.Label = _kit.Text(b.Root, "Words", 34f, main ? Color.white : InkSoft, true, false);
            var r = (RectTransform)b.Label.transform;
            r.anchorMin = Vector2.zero;
            r.anchorMax = Vector2.one;
            r.offsetMin = new Vector2(12f, 0f);
            r.offsetMax = new Vector2(-12f, 0f);
            b.Label.alignment = TextAlignmentOptions.Center;
            b.Label.enableAutoSizing = true;
            b.Label.fontSizeMin = 22f;
            b.Label.fontSizeMax = 34f;
            UiClone.Key(b.Label, key);
            if (main)
            {
                BuildModeBar.Paint(b, _kit.Blue, Color.white);
            }
            else
            {
                _kit.Frame(b, Color.white, false);   // the mockup's white button with a grey edge
                b.Back.color = DotOff;
            }
            return b;
        }

        // The outline round the part and the card beside it: under the hint bar, over a part at the foot, else where it meets
        // neither the part (with its outline) nor any other part shown; inside the screen.
        private void Place(RectTransform target, bool below)
        {
            RectTransform root = _bar.Root;
            _bar.Place();
            LayoutRebuilder.ForceRebuildLayoutImmediate(_card);
            Rect t = Local(root, target);
            t.xMin -= RingOut;
            t.xMax += RingOut;
            t.yMin -= RingOut;
            t.yMax += RingOut;
            Bar(_ring[0], t.xMin, t.yMax - Ring, t.width, Ring);   // top
            Bar(_ring[1], t.xMin, t.yMin, t.width, Ring);          // foot
            Bar(_ring[2], t.xMin, t.yMin, Ring, t.height);         // left
            Bar(_ring[3], t.xMax - Ring, t.yMin, Ring, t.height);  // right
            _card.anchorMin = _card.anchorMax = new Vector2(0.5f, 0.5f);
            _card.pivot = Vector2.zero;
            Rect screen = root.rect;
            int n = _bar.TourAvoid(_step, _avoid, _hud);
            _placedClear = BuildModeHintRules.TourCard(new UiBox(screen.xMin, screen.yMin, screen.xMax, screen.yMax), new UiBox(t.xMin, t.yMin, t.xMax, t.yMax), _avoid, n,
                _card.rect.width, _card.rect.height, Apart, below, out UiBox at);
            _card.anchoredPosition = new Vector2(at.X0, at.Y0);
        }

        private static void Bar(Image image, float x, float y, float wide, float high)
        {
            var r = (RectTransform)image.transform;
            r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
            r.pivot = Vector2.zero;
            r.anchoredPosition = new Vector2(x, y);
            r.sizeDelta = new Vector2(wide, high);
        }

        // A part's rectangle in the interface's own units, from its middle.
        private static Rect Local(RectTransform root, RectTransform part)
        {
            var corners = new Vector3[4];
            part.GetWorldCorners(corners);
            Vector3 a = root.InverseTransformPoint(corners[0]);
            Vector3 b = root.InverseTransformPoint(corners[2]);
            return Rect.MinMaxRect(a.x, a.y, b.x, b.y);
        }

        /// <summary>The tip's card on the screen and clear of the part it points at, its words not cut; null when so (the tests).</summary>
        public string Check()
        {
            if (!Running || _card == null)
            {
                return "no tip is up";
            }
            Canvas.ForceUpdateCanvases();
            Rect card = BuildModeBar.World(_card);
            Rect screen = BuildModeBar.World(_bar.Root);
            Rect part = BuildModeBar.World(_bar.TourTarget(_step));
            if (card.xMin < screen.xMin || card.xMax > screen.xMax || card.yMin < screen.yMin || card.yMax > screen.yMax)
            {
                return "the tip runs off the screen";
            }
            if (card.Overlaps(part))
            {
                return "the tip covers the part it points at";
            }
            if (!_placedClear)
            {
                return "no place clear of the other parts was found for the tip";
            }
            RectTransform root = _bar.Root;
            UiBox local = BuildModeGame.Local(root, _card);
            int n = _bar.TourAvoid(_step, _avoid, _hud);
            for (int i = 0; i < n; i++)
            {
                if (local.Meets(_avoid[i]))
                {
                    return "the tip covers another part of the screen (the tool panel, the legend or the game's)";
                }
            }
            // The card's picture fills its box, and its words keep the padding: none at the card's edges, none over another.
            Image back = _card.GetComponent<Image>();
            if (back == null || BuildModeKit.Padded(back.sprite))
            {
                return "the tip's card is drawn from a picture with a see-through margin";
            }
            float scale = screen.width / Mathf.Max(1f, root.rect.width);   // the canvas's units to the screen's
            float lastFoot = float.MaxValue;
            for (int i = 0; i < _card.childCount; i++)
            {
                var child = _card.GetChild(i) as RectTransform;
                if (child == null || !child.gameObject.activeSelf)
                {
                    continue;
                }
                Rect c = BuildModeBar.World(child);
                float side = (PadSide - 2f) * scale, ends = (PadEnds - 2f) * scale;
                if (c.xMin < card.xMin + side || c.xMax > card.xMax - side || c.yMax > card.yMax - ends || c.yMin < card.yMin + ends)
                {
                    return "'" + child.name + "' of the tip is not inside its padding";
                }
                if (c.yMax > lastFoot + 0.5f)
                {
                    return "'" + child.name + "' of the tip lies over the part above it";
                }
                lastFoot = c.yMin;
            }
            if (BuildModeKit.Padded(_ring[0].sprite) || !_ring[0].gameObject.activeInHierarchy)
            {
                return "the outline round the part is not drawn";
            }
            foreach (TMP_Text t in _card.GetComponentsInChildren<TMP_Text>(false))
            {
                t.ForceMeshUpdate();
                if (t.isTextTruncated || t.preferredHeight > t.rectTransform.rect.height + 2f)
                {
                    return "'" + t.text + "' is cut short";
                }
            }
            return null;
        }
    }
}
