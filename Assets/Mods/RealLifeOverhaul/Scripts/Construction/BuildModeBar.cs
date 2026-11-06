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
    /// Build mode's interface over the map (the owner, 26 Oct 2026: "when a new user sees it they must not feel where to click";
    /// the layout they chose, map first as in Cities Skylines): the map stays in full view; at the top in the middle a hint bar
    /// that always says what to do next, its step's number in a blue circle and, in a quieter line, a tip and what Esc does now;
    /// at the top right a button to leave; at the foot in the middle the tools in the order they are used as big icon buttons, the
    /// one in use in the game's blue and those that cannot be used yet greyed with the reason in their tooltip, then the views as
    /// small icon toggles with the one shown named under them, then the district's demand as four short read-only bars in the
    /// game's colours; and a small panel for the tool in use just over the toolbar's left end, as tall as what it says
    /// (BuildModeSheet). Everything is a copy of the game's interior designer's toolbar and panels (BuildModeKit) on the help's
    /// canvas over the map, laid out by layout groups and content size fitters so nothing is cut. Built at the first opening;
    /// written only when build mode's state changes. Nothing runs a frame.
    /// </summary>
    internal sealed class BuildModeBar
    {
        public static IModLogger Logger;

        private const float Top = 300f;          // from the top of the canvas (3840 by 2160) to the hint bar when the game's HUD cannot be measured: under its top bar and the needs (271)
        private const float LeaveTop = 196f;     // ... to the leave button, under the top bar's buttons at the right (183)
        private const float Foot = 40f;          // from the foot of the canvas to the toolbar
        private const float Edge = 48f;          // from the right edge to the leave button
        private const float ToolWide = 208f;     // a tool's button
        private const float ToolHigh = 184f;
        private const float ToolIcon = 60f;
        private const float ToolWords = 30f;
        private const float ViewSize = 92f;      // a view's toggle
        private const float ViewIcon = 46f;
        private const float BarWide = 36f;       // a demand bar
        private const float BarHigh = 88f;
        private const float SmallCaps = 26f;
        private const float HintSize = 44f;
        private const float HintMore = 34f;
        private const float StepSize = 68f;
        private const float PanelGap = 32f;      // between the toolbar's top and the tool panel's foot
        private const float HintMost = 2600f;    // the hint bar at its widest, however wide the screen
        private const float HintPadSide = 44f;   // the hint bar's padding at each side
        private const float HintSpacing = 32f;   // ... and between its parts
        private const float Gap = 28f;           // between the game's HUD and build mode's parts

        public static readonly Color Text = DarkLook.Text;
        public static readonly Color Soft = DarkLook.Soft;
        public static readonly Color Muted = DarkLook.Muted;
        public static readonly Color Good = new Color(0.56f, 0.86f, 0.60f, 1f);     // "Allowed here"
        public static readonly Color Amber = new Color(1f, 0.82f, 0.54f, 1f);       // "Needs City Hall"
        public static readonly Color Red = new Color(1f, 0.45f, 0.45f, 1f);         // what stops an action
        public static readonly Color ButtonGrey = new Color(0.149f, 0.165f, 0.188f, 1f);   // a button that is not the main one
        public static readonly Color StepBlue = new Color(0.184f, 0.435f, 0.878f, 1f);     // the step's circle, the game's blue

        private static readonly string[] ToolIcons = { "Icon-BuildingForSale", "Icon-Palette", "Icon-Building", "Icon-Delete", "Icon-CopyOutline-64", "Icon-Edit" };
        private static readonly string[] ToolIconsElse = { "Icon-Coin", "Icon-Palette", "Icon-Floors", "Icon-Walls", "Icon-CopyOutline-64", "Icon-Walls" };
        private static readonly string[] ViewIcons = { "Icon-RealEstate", "Icon-Map", "Icon-MarketValue", "Icon-People", "Icon-Construction" };
        private static readonly string[] ViewIconsElse = { "Icon-Building", "Icon-Palette", "Icon-Coin", "Icon-Hand", "Icon-Floors" };

        private static readonly string[] ToolLabels =
        {
            "reallife_bld_bar_tool_0", "reallife_bld_bar_tool_1", "reallife_bld_bar_tool_2", "reallife_bld_bar_tool_3", "reallife_bld_bar_tool_4", "reallife_bld_bar_tool_5"
        };

        private static readonly string[] Letters = { "reallife_bld_bar_letter_0", "reallife_bld_bar_letter_1", "reallife_bld_bar_letter_2", "reallife_bld_bar_letter_3" };

        private BuildModeKit _kit;
        private RectTransform _root;
        private RectTransform _hint, _bottom, _tools, _views, _demand, _leave;
        private Image _step;
        private TMP_Text _stepNumber, _hintMain, _hintMore, _viewName;
        private Image _hintDivider;
        private readonly KitButton[] _toolButtons = new KitButton[(int)BuildTool.Count];
        private readonly KitButton[] _viewButtons = new KitButton[(int)BuildView.Count];
        private readonly Image[] _bars = new Image[4];
        private readonly BuildModeTip[] _barTips = new BuildModeTip[4];
        private Action<int> _onTool, _onView;
        private Action _onLeave;
        private HudFrame _frame = new HudFrame { TopFoot = float.MaxValue, RightFoot = float.MaxValue, TasksRight = float.MinValue, TasksFoot = float.MaxValue };

        /// <summary>The tool panel over the toolbar.</summary>
        public BuildModeSheet Sheet { get; private set; }

        /// <summary>The view's legend over the views.</summary>
        public BuildModeLegend Legend { get; private set; }

        public bool Alive => _root != null;

        public bool Shown => _root != null && _root.gameObject.activeSelf;

        /// <summary>The parts the tour points at, by TourStep.</summary>
        public RectTransform TourTarget(TourStep step)
        {
            switch (step)
            {
                case TourStep.HintBar: return _hint;
                case TourStep.Toolbar: return _tools;
                case TourStep.Views: return _views;
                default: return _demand;
            }
        }

        /// <summary>The whole interface's own rectangle: the canvas's size.</summary>
        public RectTransform Root => _root;

        /// <summary>
        /// What a tour's card must leave in view besides the part it points at (the owner, 26 Oct 2026: the card hid the tool panel),
        /// in the interface's units: every part of build mode's shown (the hint bar, the leave button, the foot, the tool panel, the
        /// legend) but the one pointed at and the box holding it, and the game's HUD parts. Returns how many.
        /// </summary>
        public int TourAvoid(TourStep step, UiBox[] into, List<RectTransform> hud)
        {
            RectTransform target = TourTarget(step);
            int n = 0;
            foreach (RectTransform r in new[] { _hint, _leave, _tools, _views, _demand, Sheet != null ? Sheet.Rect : null, Legend != null ? Legend.Rect : null })
            {
                if (r != null && r != target && r.gameObject.activeInHierarchy && n < into.Length)
                {
                    into[n++] = BuildModeGame.Local(_root, r);
                }
            }
            BuildModeGame.HudParts(hud);
            for (int i = 0; i < hud.Count && n < into.Length; i++)
            {
                into[n++] = BuildModeGame.Local(_root, hud[i]);
            }
            return n;
        }

        /// <summary>The interface, hidden, on the canvas given; null, with one line in Player.log, if the game's designer is not built as expected.</summary>
        public static BuildModeBar Make(Transform canvas, BuildModeKit kit, Action<int> onTool, Action<int> onView, Action<int> onRow, Action onLeave)
        {
            if (canvas == null || kit == null)
            {
                UiClone.Fallback("build mode's toolbar has no canvas or no parts to stand on");
                return null;
            }
            var bar = new BuildModeBar { _kit = kit, _onTool = onTool, _onView = onView, _onLeave = onLeave };
            try
            {
                bar.Build(canvas, onRow);
                return bar;
            }
            catch (Exception e)
            {
                Logger?.Error(e);
                UiClone.Fallback("build mode's toolbar could not be built from the game's interior designer");
                bar.Destroy();
                return null;
            }
        }

        private void Build(Transform canvas, Action<int> onRow)
        {
            var go = new GameObject("RealLife Build Mode", typeof(RectTransform));
            go.layer = canvas.gameObject.layer;
            go.transform.SetParent(canvas, false);
            _root = (RectTransform)go.transform;
            _root.anchorMin = Vector2.zero;
            _root.anchorMax = Vector2.one;
            _root.offsetMin = _root.offsetMax = Vector2.zero;

            // The hint bar: the step's circle, what to do next, and in a quieter line a tip and what Esc does.
            _hint = _kit.Panel(_root, "Hint bar", true, new RectOffset((int)HintPadSide, (int)HintPadSide, 28, 28), HintSpacing);
            Pin(_hint, new Vector2(0.5f, 1f), new Vector2(0f, -Top));
            _step = BuildModeKit.Picture(_hint, "Step", _kit.Icon("circle", null) ?? _kit.White, StepBlue, StepSize, StepSize);
            _stepNumber = _kit.Text(_step.transform, "Number", 36f, Color.white, true, false);
            Fill((RectTransform)_stepNumber.transform);
            _stepNumber.alignment = TextAlignmentOptions.Center;
            _hintMain = _kit.Text(_hint, "Next", HintSize, Text, true, false);
            _hintDivider = _kit.Divider(_hint, 56f, new Color(0.23f, 0.25f, 0.28f, 1f));
            _hintMore = _kit.Text(_hint, "Tip and Esc", HintMore, Soft, false, false);

            // Leave, at the top right.
            KitButton leave = _kit.Button(_root, "Leave build mode", () => _onLeave?.Invoke(), -1f, 96f);
            BuildModeKit.Size(leave.Root.gameObject, -1f, 96f);
            var leaveGroup = leave.Root.gameObject.AddComponent<HorizontalLayoutGroup>();
            leaveGroup.padding = new RectOffset(32, 36, 0, 0);
            leaveGroup.spacing = 20f;
            leaveGroup.childAlignment = TextAnchor.MiddleCenter;
            leaveGroup.childControlWidth = leaveGroup.childControlHeight = true;
            leaveGroup.childForceExpandWidth = leaveGroup.childForceExpandHeight = false;
            var leaveFit = leave.Root.gameObject.AddComponent<ContentSizeFitter>();
            leaveFit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            if (leave.Chosen != null)
            {
                BuildModeKit.Size(leave.Chosen, 0f, 0f).ignoreLayout = true;
            }
            if (leave.Icon != null)
            {
                leave.Icon.sprite = _kit.Icon("Icon-Close", null) ?? leave.Icon.sprite;
                BuildModeKit.Size(leave.Icon.gameObject, 40f, 40f);
                leave.Icon.color = new Color(1f, 0.85f, 0.85f, 1f);
            }
            leave.Label = _kit.Text(leave.Root, "Words", 36f, new Color(1f, 0.85f, 0.85f, 1f), true, false);
            UiClone.Key(leave.Label, "reallife_bld_bar_leave");
            _kit.Frame(leave, new Color(0.227f, 0.133f, 0.141f, 1f), false);
            leave.Back.color = new Color(0.353f, 0.227f, 0.227f, 1f);   // the mockup's #5a3a3a edge: leaving reads apart from the tools
            leave.Tip = leave.Root.gameObject.AddComponent<BuildModeTip>();
            _leave = leave.Root;
            Pin(_leave, new Vector2(1f, 1f), new Vector2(-Edge, -LeaveTop));

            // The foot: the tools, the views, the demand, side by side.
            _bottom = BuildModeKit.Strip(_root, "Foot", true, new RectOffset(), 28f, TextAnchor.LowerCenter);
            var bottomFit = _bottom.gameObject.AddComponent<ContentSizeFitter>();
            bottomFit.horizontalFit = bottomFit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            ((HorizontalLayoutGroup)_bottom.GetComponent<HorizontalLayoutGroup>()).childForceExpandHeight = true;
            Pin(_bottom, new Vector2(0.5f, 0f), new Vector2(0f, Foot));

            _tools = _kit.Panel(_bottom, "Tools", true, new RectOffset(20, 20, 20, 20), 16f);
            UnityEngine.Object.DestroyImmediate(_tools.GetComponent<ContentSizeFitter>());   // the foot's layout sizes it
            for (int t = 0; t < _toolButtons.Length; t++)
            {
                int tool = t;
                KitButton b = _kit.Button(_tools, "Tool " + t, () => _onTool?.Invoke(tool), ToolWide, ToolHigh);
                if (b.Icon != null)
                {
                    b.Icon.sprite = _kit.Icon(ToolIcons[t], ToolIconsElse[t]) ?? b.Icon.sprite;
                    var icon = (RectTransform)b.Icon.transform;
                    icon.sizeDelta = new Vector2(ToolIcon, ToolIcon);
                    icon.anchoredPosition = new Vector2(0f, 26f);
                }
                b.Label = _kit.Text(b.Root, "Words", ToolWords, Text, true, false);
                var words = (RectTransform)b.Label.transform;
                words.anchorMin = new Vector2(0f, 0f);
                words.anchorMax = new Vector2(1f, 0f);
                words.pivot = new Vector2(0.5f, 0f);
                words.anchoredPosition = new Vector2(0f, 22f);
                words.sizeDelta = new Vector2(-12f, 44f);
                b.Label.alignment = TextAlignmentOptions.Center;
                b.Label.enableAutoSizing = true;   // a longer word in another language fits the button, never cut
                b.Label.fontSizeMin = 20f;
                b.Label.fontSizeMax = ToolWords;
                UiClone.Key(b.Label, ToolLabels[t]);   // the toolbar's short name; the tooltip gives the whole
                _kit.Frame(b, ButtonGrey, true);
                Brighten(b);
                b.Tip = b.Root.gameObject.AddComponent<BuildModeTip>();
                _toolButtons[t] = b;
            }

            _views = _kit.Panel(_bottom, "Views", false, new RectOffset(28, 28, 18, 18), 10f);
            UnityEngine.Object.DestroyImmediate(_views.GetComponent<ContentSizeFitter>());   // the foot's layout sizes it
            TMP_Text viewCaps = _kit.Text(_views, "Caps", SmallCaps, Muted, true, false);
            viewCaps.fontStyle = FontStyles.Bold | FontStyles.UpperCase;
            viewCaps.characterSpacing = 4f;
            UiClone.Key(viewCaps, "reallife_bld_bar_views");
            RectTransform row = BuildModeKit.Strip(_views, "Toggles", true, new RectOffset(), 12f, TextAnchor.MiddleLeft);
            for (int v = 0; v < _viewButtons.Length; v++)
            {
                int view = v;
                KitButton b = _kit.Button(row, "View " + v, () => _onView?.Invoke(view), ViewSize, ViewSize);
                if (b.Icon != null)
                {
                    b.Icon.sprite = _kit.Icon(ViewIcons[v], ViewIconsElse[v]) ?? b.Icon.sprite;
                    ((RectTransform)b.Icon.transform).sizeDelta = new Vector2(ViewIcon, ViewIcon);
                }
                _kit.Frame(b, ButtonGrey, true);
                Brighten(b);
                b.Tip = b.Root.gameObject.AddComponent<BuildModeTip>();
                _viewButtons[v] = b;
            }
            _viewName = _kit.Text(_views, "Shown", SmallCaps, Soft, false, false);

            _demand = _kit.Panel(_bottom, "Demand", false, new RectOffset(32, 32, 18, 18), 10f);
            UnityEngine.Object.DestroyImmediate(_demand.GetComponent<ContentSizeFitter>());   // the foot's layout sizes it
            TMP_Text demandCaps = _kit.Text(_demand, "Caps", SmallCaps, Muted, true, false);
            demandCaps.fontStyle = FontStyles.Bold | FontStyles.UpperCase;
            demandCaps.characterSpacing = 4f;
            UiClone.Key(demandCaps, "reallife_bld_bar_demand");
            RectTransform bars = BuildModeKit.Strip(_demand, "Bars", true, new RectOffset(), 20f, TextAnchor.LowerCenter);
            for (int u = 0; u < _bars.Length; u++)
            {
                RectTransform one = BuildModeKit.Strip(bars, "Use " + u, false, new RectOffset(), 8f, TextAnchor.LowerCenter);
                var catcher = one.gameObject.AddComponent<Image>();
                catcher.color = new Color(0f, 0f, 0f, 0f);   // only so the pointer finds it for its tooltip: it is looked at, never clicked
                Image track = BuildModeKit.Picture(one, "Track", _kit.Line, new Color(0.18f, 0.2f, 0.22f, 1f), BarWide, BarHigh);
                Image fill = BuildModeKit.Picture(track.transform, "Filled", _kit.Line, Color.white, BarWide, 0f);
                var fillRect = (RectTransform)fill.transform;
                fillRect.anchorMin = new Vector2(0f, 0f);
                fillRect.anchorMax = new Vector2(1f, 0f);
                fillRect.pivot = new Vector2(0.5f, 0f);
                fillRect.anchoredPosition = Vector2.zero;
                fillRect.sizeDelta = new Vector2(0f, 0f);
                fill.GetComponent<LayoutElement>().ignoreLayout = true;
                TMP_Text letter = _kit.Text(one, "Letter", 24f, Soft, true, false);
                letter.alignment = TextAlignmentOptions.Center;
                UiClone.Key(letter, Letters[u]);
                _bars[u] = fill;
                _barTips[u] = one.gameObject.AddComponent<BuildModeTip>();
            }

            Sheet = new BuildModeSheet(_kit, _root, onRow);
            Legend = new BuildModeLegend(_kit, _root);
            _root.gameObject.SetActive(false);
        }

        // A wide button's picture: the white box of the game's tinted, or the blue.
        public static void Paint(KitButton b, Sprite sprite, Color colour)
        {
            if (b.Back == null)
            {
                return;
            }
            b.Back.sprite = sprite;
            b.Back.type = sprite != null && sprite.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
            b.Back.color = colour;
        }

        // The game's blue under a chosen tool, full strength: the tool in use reads at a glance.
        private static void Brighten(KitButton b)
        {
            Image chosen = b.Chosen != null ? b.Chosen.GetComponent<Image>() : null;
            if (chosen != null)
            {
                chosen.color = Color.white;
                var rect = (RectTransform)chosen.transform;
                rect.sizeDelta = Vector2.zero;   // the whole button, not the designer's inset
            }
        }

        private static void Pin(RectTransform rect, Vector2 anchor, Vector2 at)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = anchor;
            rect.anchoredPosition = at;
        }

        private static void Fill(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            var e = rect.GetComponent<LayoutElement>() ?? rect.gameObject.AddComponent<LayoutElement>();
            e.ignoreLayout = true;
        }

        // ------------------------------------------------------------------ writing

        public void Show(bool shown)
        {
            if (_root != null && _root.gameObject.activeSelf != shown)
            {
                _root.gameObject.SetActive(shown);
                if (shown)
                {
                    _root.SetAsLastSibling();
                }
            }
        }

        /// <summary>The hint bar: the step's number (0: no circle), what to do next, and the quieter line.</summary>
        public void Hint(int step, string next, string more)
        {
            bool circle = step > 0;
            if (_step.gameObject.activeSelf != circle)
            {
                _step.gameObject.SetActive(circle);
            }
            UiClone.Words(_stepNumber, circle ? step.ToString(System.Globalization.CultureInfo.InvariantCulture) : string.Empty);
            UiClone.Words(_hintMain, next ?? string.Empty);
            Cap(_hintMain, HintMost);
            bool hasMore = !string.IsNullOrEmpty(more);
            if (_hintMore.gameObject.activeSelf != hasMore)
            {
                _hintMore.gameObject.SetActive(hasMore);
                _hintDivider.gameObject.SetActive(hasMore);
            }
            UiClone.Words(_hintMore, more ?? string.Empty);
            FitHint();
        }

        // The hint bar's two lines on one row where the room allows (the owner, 26 Oct 2026: a long hint wrapped to two lines):
        // the room is the screen's between the objectives panel and the right edge, the bar kept in the middle; the quieter line
        // wraps first (BuildModeHintRules.HintFit).
        private void FitHint()
        {
            Rect screen = _root.rect;
            float left = _frame.TasksRight > float.MinValue ? Math.Max(screen.xMin + Edge, _frame.TasksRight + Gap) : screen.xMin + Edge;
            float half = Math.Max(0f, Math.Min(-left, screen.xMax - Edge));
            bool circle = _step.gameObject.activeSelf, more = _hintMore.gameObject.activeSelf;
            float fixedParts = HintPadSide * 2f + (circle ? StepSize + HintSpacing : 0f) + (more ? HintSpacing * 2f + 4f : 0f);
            float room = Math.Min(half * 2f, HintMost) - fixedParts;
            float mainWide = _hintMain.GetPreferredValues(_hintMain.text, float.PositiveInfinity, float.PositiveInfinity).x;
            float moreWide = more ? _hintMore.GetPreferredValues(_hintMore.text, float.PositiveInfinity, float.PositiveInfinity).x : 0f;
            BuildModeHintRules.HintFit(mainWide, moreWide, room, out float mainCap, out float moreCap);
            Cap(_hintMain, mainCap);
            Cap(_hintMore, moreCap);
            LayoutRebuilder.ForceRebuildLayoutImmediate(_hint);
        }

        // A line as wide as its words, or wrapping at a width (below zero: no wrap).
        private static void Cap(TMP_Text t, float most)
        {
            bool wrap = most > 0f;
            t.textWrappingMode = wrap ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
            BuildModeKit.Size(t.gameObject, wrap ? most : -1f, -1f);
            LayoutElement e = t.GetComponent<LayoutElement>();
            if (!wrap)
            {
                e.minWidth = e.preferredWidth = -1f;
            }
        }

        /// <summary>True when the hint bar's main line is on one line now, for the tests.</summary>
        public bool HintOneLine
        {
            get
            {
                _hintMain.ForceMeshUpdate();
                return _hintMain.textInfo == null || _hintMain.textInfo.lineCount <= 1;
            }
        }

        /// <summary>The tools: the one in use in the game's blue; one with a reason greyed, the reason in its tooltip.</summary>
        public void Tools(BuildTool inUse, string[] names, string[] lines, string[] why)
        {
            for (int t = 0; t < _toolButtons.Length; t++)
            {
                KitButton b = _toolButtons[t];
                bool usable = why[t] == null;
                b.SetUsable(usable);
                b.SetChosen(t == (int)inUse);
                b.Tip.Set(names[t], usable ? lines[t] : why[t], !usable);
            }
        }

        /// <summary>The views: the one shown in the game's blue and named under the toggles; each named in its tooltip.</summary>
        public void Views(BuildView shown, string[] names, string[] lines)
        {
            for (int v = 0; v < _viewButtons.Length; v++)
            {
                _viewButtons[v].SetChosen(v == (int)shown);
                _viewButtons[v].Tip.Set(names[v], lines[v], false);
            }
            UiClone.Words(_viewName, names[(int)shown]);
        }

        /// <summary>A use's demand: its bar filled in its colour (empty while none is known), its name, figure and words in its tooltip.</summary>
        public void Demand(int use, int value, Color colour, string title, string line)
        {
            if (use < 0 || use >= _bars.Length)
            {
                return;
            }
            var rect = (RectTransform)_bars[use].transform;
            rect.sizeDelta = new Vector2(0f, BarHigh * BuildModeHintRules.DemandFill(value));
            _bars[use].color = new Color(colour.r, colour.g, colour.b, 1f);
            _barTips[use].Set(title, line, false);
        }

        /// <summary>
        /// Where the game's HUD stands now (BuildModeGame.Hud): the hint bar goes under the top bar and everything hung under it
        /// (it covered the needs bars, the owner's test of 26 Oct 2026), clear of the objectives panel; the leave button under the
        /// top bar's buttons at the right. Unmeasured: the fixed places, under the game's own top bar at 3840 by 2160.
        /// </summary>
        public void Hud(HudFrame frame)
        {
            _frame = frame;
            Rect screen = _root.rect;
            float hintTop = frame.Measured && frame.TopFoot < float.MaxValue ? Math.Min(-Top + 120f, frame.TopFoot - screen.yMax - Gap) : -Top;
            float leaveTop = frame.Measured && frame.RightFoot < float.MaxValue ? Math.Min(-LeaveTop + 60f, frame.RightFoot - screen.yMax - Gap) : -LeaveTop;
            _hint.anchoredPosition = new Vector2(0f, hintTop);
            _leave.anchoredPosition = new Vector2(-Edge, leaveTop);
            FitHint();
        }

        /// <summary>Lays everything out now and stands the tool panel over the toolbar's left end, the legend over the views.</summary>
        public void Place()
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(_bottom);
            LayoutRebuilder.ForceRebuildLayoutImmediate(_hint);
            LayoutRebuilder.ForceRebuildLayoutImmediate(_leave);
            float left = -_bottom.rect.width * 0.5f;
            float over = Foot + _bottom.rect.height + PanelGap;
            Sheet.Stand(new Vector2(left, over));
            if (Legend != null)
            {
                UiBox views = BuildModeGame.Local(_root, _views);
                Legend.Stand(new Vector2(views.X0, over));
            }
        }

        public void Destroy()
        {
            if (_root != null)
            {
                UnityEngine.Object.Destroy(_root.gameObject);
            }
            _root = null;
        }

        // ------------------------------------------------------------------ the tests' measures

        /// <summary>The parts shown now that may not meet: the hint bar, the leave button, the foot and the tool panel.</summary>
        private void Parts(List<RectTransform> into)
        {
            into.Clear();
            foreach (RectTransform r in new[] { _hint, _leave, _bottom, Sheet != null ? Sheet.Rect : null, Legend != null ? Legend.Rect : null })
            {
                if (r != null && r.gameObject.activeInHierarchy)
                {
                    into.Add(r);
                }
            }
        }

        /// <summary>
        /// The interface as drawn now: no two parts meet, every part on the screen, no text cut short or running out of its box,
        /// no text over another inside a part. Null when all is well, else what is wrong.
        /// </summary>
        public string Overlap()
        {
            if (_root == null || !_root.gameObject.activeInHierarchy)
            {
                return "the interface is not up";
            }
            Canvas.ForceUpdateCanvases();
            Place();
            var parts = new List<RectTransform>(4);
            Parts(parts);
            Rect screen = World(_root);
            for (int i = 0; i < parts.Count; i++)
            {
                Rect a = World(parts[i]);
                if (a.xMin < screen.xMin - 0.5f || a.xMax > screen.xMax + 0.5f || a.yMin < screen.yMin - 0.5f || a.yMax > screen.yMax + 0.5f)
                {
                    return "'" + parts[i].name + "' runs off the screen";
                }
                for (int k = i + 1; k < parts.Count; k++)
                {
                    if (a.Overlaps(World(parts[k])))
                    {
                        return "'" + parts[i].name + "' meets '" + parts[k].name + "'";
                    }
                }
            }
            // The game's HUD: the top bar and what hangs under it, its buttons, the objectives, and the labels at the foot while drawn.
            var hud = new List<RectTransform>(24);
            BuildModeGame.HudParts(hud);
            for (int i = 0; i < parts.Count; i++)
            {
                Rect a = World(parts[i]);
                for (int k = 0; k < hud.Count; k++)
                {
                    if (hud[k] != null && a.Overlaps(World(hud[k])))
                    {
                        return "'" + parts[i].name + "' meets the game's '" + hud[k].name + "'";
                    }
                }
            }
            // Every box drawn whole: a picture packed with a see-through margin draws smaller than its box (the zone rows' thin lines).
            foreach (Image image in _root.GetComponentsInChildren<Image>(false))
            {
                if (image.enabled && image.type == Image.Type.Sliced && BuildModeKit.Padded(image.sprite))
                {
                    return "'" + image.name + "' under '" + image.transform.parent.name + "' is drawn from '" + image.sprite.name + "', which has a see-through margin";
                }
            }
            foreach (TMP_Text t in _root.GetComponentsInChildren<TMP_Text>(false))
            {
                if (string.IsNullOrEmpty(t.text))
                {
                    continue;
                }
                t.ForceMeshUpdate();
                RectTransform r = t.rectTransform;
                if (t.isTextTruncated || t.preferredWidth > r.rect.width + 2f && t.textWrappingMode == TextWrappingModes.NoWrap && !t.enableAutoSizing || t.preferredHeight > r.rect.height + 2f)
                {
                    return "'" + (t.text.Length > 40 ? t.text.Substring(0, 40) : t.text) + "' is cut short (" + t.preferredWidth.ToString("0") + " by " + t.preferredHeight.ToString("0")
                           + " in " + r.rect.width.ToString("0") + " by " + r.rect.height.ToString("0") + ")";
                }
            }
            string sheet = Sheet != null ? Sheet.Stacked() ?? Sheet.Swatches() ?? Sheet.ButtonsDrawn() : null;
            if (sheet != null)
            {
                return sheet;
            }
            if (Sheet != null && Sheet.HasLegend)
            {
                return "the tool panel holds a legend";
            }
            string legend = Legend != null && Legend.Rect.gameObject.activeInHierarchy ? Legend.Check() : null;
            if (legend != null)
            {
                return legend;
            }
            if (Legend != null && Legend.Rect.gameObject.activeInHierarchy && World(Legend.Rect).height > World(_root).height * 0.2f)
            {
                return "the legend is taller than a fifth of the screen";
            }
            if (_hint.gameObject.activeInHierarchy && !HintOneLine && _hintMain.text.Length <= HintShortest)
            {
                return "the hint '" + _hintMain.text + "' wraps though it is short";
            }
            return null;
        }

        private const int HintShortest = 48;   // characters: a hint this short always fits one line on a screen of 16 by 10 or wider

        /// <summary>True when a part shown covers the middle of the screen, the map's centre, with which part.</summary>
        public bool CoversCentre(out string what)
        {
            what = null;
            if (_root == null || !_root.gameObject.activeInHierarchy)
            {
                return false;
            }
            Canvas.ForceUpdateCanvases();
            var parts = new List<RectTransform>(4);
            Parts(parts);
            Rect screen = World(_root);
            Vector2 middle = screen.center;
            float covered = 0f;
            for (int i = 0; i < parts.Count; i++)
            {
                Rect r = World(parts[i]);
                covered += r.width * r.height;
                if (r.Contains(middle))
                {
                    what = parts[i].name;
                    return true;
                }
            }
            if (covered > screen.width * screen.height * 0.3f)
            {
                what = "a third of the screen";
                return true;
            }
            return false;
        }

        /// <summary>The words in the hint bar now, for the tests.</summary>
        public string HintWords => _hintMain != null ? _hintMain.text : null;

        /// <summary>The second line of the hint bar now, for the tests.</summary>
        public string HintMoreWords => _hintMore != null && _hintMore.gameObject.activeSelf ? _hintMore.text : null;

        /// <summary>A tool's button greyed now, for the tests.</summary>
        public bool ToolGreyed(BuildTool tool, out string why)
        {
            KitButton b = _toolButtons[(int)tool];
            why = b != null && b.Tip != null ? b.Tip.Line : null;
            return b != null && !b.Usable;
        }

        /// <summary>The top of the toolbar along the foot (its tools, views and demand), world y, where it is drawn now; NaN where it is not up. The design window stands clear of it.</summary>
        public float ToolbarTop()
        {
            if (_bottom == null || !_bottom.gameObject.activeInHierarchy)
            {
                return float.NaN;
            }
            LayoutRebuilder.ForceRebuildLayoutImmediate(_bottom);
            return World(_bottom).yMax;
        }

        internal static Rect World(RectTransform r)
        {
            var corners = new Vector3[4];
            r.GetWorldCorners(corners);
            return Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
        }
    }
}
