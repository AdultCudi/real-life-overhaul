using System;
using UnityEngine;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The interior designer's kit face: the panels over the room as one screen of the kit (UiDesignerPanels), pointer only, over the game's designer which stays open and
    /// hidden underneath (DesignerGame). It writes what the game says into one model, ten times a second at most and only when the game's state changed, and gives each
    /// press back to the game as the call its own button makes. The room, the camera and every overlay in the room stay the game's: the piece takes the mouse over its own
    /// targets and no key. Cost: a comparison a frame; ten times a second a few dozen reads; a layout when the game's state changed.
    /// </summary>
    internal sealed class DesignerFace : IUiScreen, IUiPointing, IUiTyping, IUiDragging, IUiTracking
    {
        public const string ScreenId = "designer";

        private const float Every = 0.1f;   // s between two looks at the game's designer

        private readonly UiDesignerModel _m = new UiDesignerModel();
        private readonly float[] _blocks = new float[12];
        private UiFrame _frame;
        private float _tabX;   // where the pointer last was across the row of tabs, px
        private bool _dirty = true;
        private float _next;
        private long _seen;

        public DesignerFace()
        {
            _m.Prompts = new UiPrompt[9];
            DesignerGame.Stale = Touch;
        }

        public string Id => ScreenId;

        /// <summary>The model, for the tests.</summary>
        internal UiDesignerModel Model => _m;

        public bool Dirty
        {
            get
            {
                if (Time.unscaledTime >= _next)
                {
                    _next = Time.unscaledTime + Every;
                    long now = unchecked(DesignerGame.Signature() * 31 + HintsSignature());
                    _dirty |= now != _seen;
                    _seen = now;
                }
                return _dirty;
            }
        }

        /// <summary>Asks for a layout on the next frame (a press changed what the game shows, or the game said something did).</summary>
        public void Touch()
        {
            _dirty = true;
            _next = 0f;
        }

        // ------------------------------------------------------------------ the screen

        public void Lay(UiScene s, UiFrame frame, bool dark, IUiKeys keys)
        {
            _frame = frame;
            DesignerGame.Fill(_m);
            _m.PromptCount = DesignerHints.Read(_m.Prompts);
            frame.Pointer = _m.PromptCount == 0;   // the game's hint lines are chips only while the layout lets the keys lead, as the HUD does for them
            if (_m.Panel != UiDesignerPanelKind.BusinessType)
            {
                _m.BusinessType.Open = false;
                _m.BusinessType.Offset = 0f;
            }
            UiDesignerPanels.Lay(s, _m, frame, dark, DesignerHints.Wrap(keys));
            int rects = UiDesignerPanels.Blocks(_m, frame, _blocks);
            DesignerKits.Shield(_blocks, rects);
            _seen = unchecked(DesignerGame.Signature() * 31 + HintsSignature());
            _dirty = false;
        }

        private static long HintsSignature() => DesignerHints.Signature();

        public bool Press(int command, int arg)
        {
            bool done = Do(command, arg);
            if (done)
            {
                Touch();
            }
            return done;
        }

        private bool Do(int command, int arg)
        {
            switch (command)
            {
                case UiDesignerPanels.ToolCommand:
                    return DesignerGame.Tool(arg);
                case UiDesignerPanels.StepCommand:
                    return DesignerGame.Step((UiDesignerStep)arg);
                case UiDesignerPanels.ApplyCommand:
                    return DesignerGame.Apply();
                case UiDesignerPanels.CloseCommand:
                    return DesignerGame.Close();
                case UiDesignerPanels.FoldCommand:
                    DesignerGame.InfoFolded = !_m.Info.Folded;
                    return true;
                case UiDesignerPanels.ShowCommand:
                    return DesignerGame.Narrow(arg);
                case UiDesignerPanels.HowCommand:
                    return DesignerGame.Help(arg);
                case UiDesignerPanels.TabStripCommand:
                    int tab = UiDesignerPanels.TabAt(_m.Catalogue, _tabX);
                    if (tab < 0)
                    {
                        return false;
                    }
                    _m.Catalogue.Scroll = 0f;
                    return DesignerGame.Tab(tab);
                case UiDesignerPanels.CardCommand:
                    return DesignerGame.Choose(arg);
                case UiDesignerPanels.FavouritesCommand:
                    _m.Catalogue.Scroll = 0f;
                    return DesignerGame.Favourites(!_m.Catalogue.FavouritesOnly);
                case UiDesignerPanels.StarCommand:
                    return DesignerGame.Star(arg);
                case UiDesignerPanels.PlaceCommand:
                    return DesignerGame.PlaceItem(arg);
                case UiDesignerPanels.ClearCommand:
                    DesignerGame.Unnarrow();
                    _m.Catalogue.Scroll = 0f;
                    return true;
                case UiDesignerPanels.MaterialTabCommand:
                    return DesignerGame.MaterialTab(arg);
                case UiDesignerPanels.MaterialCardCommand:
                    return DesignerGame.ChooseMaterial(arg);
                case UiDesignerPanels.ShadeCommand:
                    return DesignerGame.ChooseShade(arg);
                case UiDesignerPanels.LayCommand:
                    return DesignerGame.LayWholeFloor();
                case UiDesignerPanels.ItemCommand:
                    return _m.Tools.Tool >= 0 && DesignerGame.ChooseThing((UiDesignerTool)_m.Tools.Tool, arg);
                case UiDesignerPanels.TimeLessCommand:
                    return DesignerGame.StepTime(-10);
                case UiDesignerPanels.TimeMoreCommand:
                    return DesignerGame.StepTime(10);
                case UiDesignerPanels.TimeSliderCommand:
                    return true;   // taken by the pointer's press and drag (IUiDragging)
                case UiDesignerPanels.SaveCommand:
                    return DesignerGame.SaveBlueprint();
                case UiDesignerPanels.TypeFieldCommand:
                    _m.BusinessType.Open = !_m.BusinessType.Open;
                    return true;
                case UiDesignerPanels.TypeOptionCommand:
                    _m.BusinessType.Open = false;
                    return DesignerGame.AnswerOption(arg);
                case UiDesignerPanels.TypeContinueCommand:
                    return DesignerGame.AnswerContinue();
                case UiDesignerPanels.TypeCancelCommand:
                    return DesignerGame.AnswerCancel();
                case UiDesignerPanels.SearchCommand:
                case UiDesignerPanels.MaterialSearchCommand:
                case UiDesignerPanels.NameCommand:
                    return true;   // the host opens the field (IUiTyping)
            }
            return false;
        }

        public void Focused(int command, int arg)
        {
        }

        // The mouse over a tool, a step or a tile raises the game's own tooltip for it, through the game's TooltipSystem, which the kit's card draws.
        public void Pointed(int command, int arg)
        {
            Transform under = DesignerKits.Root;
            if (command != UiDesignerPanels.TabStripCommand && _m.Catalogue.TabHover >= 0)
            {
                _m.Catalogue.TabHover = -1;   // the pointer left the row of tabs
                _dirty = true;
            }
            switch (command)
            {
                case UiDesignerPanels.ToolCommand:
                    DesignerTips.Words(under, arg >= 0 && arg < _m.Tools.Tools.Length ? _m.Tools.Tools[arg].Name : null);
                    return;
                case UiDesignerPanels.StepCommand:
                    DesignerTips.Words(under, arg >= 0 && arg < _m.StepNames.Length ? _m.StepNames[arg] : null);
                    return;
                case UiDesignerPanels.CardCommand:
                    DesignerTips.Item(under, DesignerGame.ItemAt(arg));
                    return;
                default:
                    DesignerTips.Off();
                    return;
            }
        }

        // Esc is the game's own (it steps out of a tool before it asks about the changes): the piece takes no key.
        public bool Back() => false;

        public void Tab(int step)
        {
        }

        public bool Act(UiAction action) => false;

        public void Reveal(int list, int index)
        {
        }

        public void Wheel(int clip, float notches)
        {
            switch (_m.Panel)
            {
                case UiDesignerPanelKind.Furniture:
                    _m.Catalogue.Scroll = Math.Max(0f, _m.Catalogue.Scroll - notches * UiDesignerPanels.RowStep);
                    break;
                case UiDesignerPanelKind.Materials:
                    _m.Materials.Scroll = Math.Max(0f, _m.Materials.Scroll - notches * UiDesignerPanels.RowStep);
                    break;
                case UiDesignerPanelKind.Items:
                    _m.Items.Scroll = Math.Max(0f, _m.Items.Scroll - notches * UiDesignerPanels.ItemStep);
                    break;
                case UiDesignerPanelKind.BusinessType:
                    UiDesignerBusinessType t = _m.BusinessType;
                    t.Offset = Math.Max(0f, Math.Min(t.Offset - notches * UiTokens.ControlSmall, Math.Max(0, t.Count - 8) * UiTokens.ControlSmall));
                    break;
                default:
                    return;
            }
            Touch();
        }

        // ------------------------------------------------------------------ typing: the searches and the blueprint's name

        public int TypingCommand
        {
            get
            {
                switch (_m.Panel)
                {
                    case UiDesignerPanelKind.Furniture:
                        return UiDesignerPanels.SearchCommand;
                    case UiDesignerPanelKind.Materials:
                        return UiDesignerPanels.MaterialSearchCommand;
                    case UiDesignerPanelKind.Blueprint:
                        return UiDesignerPanels.NameCommand;
                    default:
                        return -1;
                }
            }
        }

        public string TypingText
        {
            get
            {
                switch (_m.Panel)
                {
                    case UiDesignerPanelKind.Furniture:
                        return _m.Catalogue.Search;
                    case UiDesignerPanelKind.Materials:
                        return _m.Materials.Search;
                    case UiDesignerPanelKind.Blueprint:
                        return _m.Blueprint.Name;
                    default:
                        return string.Empty;
                }
            }
        }

        public void Typed(string text)
        {
            switch (_m.Panel)
            {
                case UiDesignerPanelKind.Furniture:
                    _m.Catalogue.Scroll = 0f;
                    DesignerGame.SearchFor(text);
                    break;
                case UiDesignerPanelKind.Materials:
                    _m.Materials.Scroll = 0f;
                    DesignerGame.MaterialSearch(text);
                    break;
                case UiDesignerPanelKind.Blueprint:
                    DesignerGame.NameBlueprint(text);
                    break;
            }
            Touch();
        }

        // ------------------------------------------------------------------ dragging: the time of day's slider

        public void PointerDown(int command, int arg, float x, float y)
        {
            if (command == UiDesignerPanels.TimeSliderCommand)
            {
                Slide(x);
            }
            else if (command == UiDesignerPanels.TabStripCommand)
            {
                _tabX = x;
            }
        }

        // ------------------------------------------------------------------ tracking: the row of categories is one target

        public bool Tracks(int command) => command == UiDesignerPanels.TabStripCommand && _m.Panel == UiDesignerPanelKind.Furniture;

        public void Moved(int command, int arg, float x, float y)
        {
            _tabX = x;
            int over = UiDesignerPanels.TabAt(_m.Catalogue, x);
            if (over != _m.Catalogue.TabHover)
            {
                _m.Catalogue.TabHover = over;
                _dirty = true;
            }
        }

        public bool DragBegin(int command, int arg, float x, float y)
        {
            if (command != UiDesignerPanels.TimeSliderCommand)
            {
                return false;
            }
            Slide(x);
            return true;
        }

        public void DragMove(float x, float y) => Slide(x);

        public void DragEnd(bool cancelled)
        {
        }

        // The knob to where the pointer is, by the track the layout drew, then the game's own slider moved there.
        private void Slide(float x)
        {
            UiDesignerPanels.TimeSlider(_m, _frame, out float left, out float width);
            if (width <= 0f)
            {
                return;
            }
            float share = UiDraw.SliderShare(width, _m.Time.Figure ?? string.Empty, (x - left) / width);
            if (DesignerGame.SlideTime(share))
            {
                Touch();
            }
        }
    }
}
