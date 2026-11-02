using System;
using System.Collections.Generic;
using Localizor.LanguageChangeEvent;
using TMPro;
using UI;
using UI.Smartphone.Apps.BizMan;
using UnityEngine;
using UnityEngine.UI;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// What the operations' pages share: the form's rows, each mirroring one of the game's own widgets (a toggle, a drop-down,
    /// a slider, an amount, a field, a button) and pressing it as a click would, so the game's questions, refusals and other
    /// mods' patches run; the lists a drop-down opens; a held slider's steps; and the words typed into a field. A row is read
    /// only while the game shows its widget, and held while the game holds it. Cost: what each press does, only when pressed.
    /// </summary>
    public sealed partial class BizManScreen
    {
        // A row's widget: the game's own control, and for an amount its minus and plus.
        private struct OpsWidget
        {
            public Component Main;
            public Button Less, More;
            public int Mod;   // a row the mod answers itself (a pricing plan's strategy): its code; 0 for one of the game's widgets
        }

        // The operations' own switch in Mod settings (UiScreens.BizOps): off, their tabs open the game's own view.
        private static bool Ops => UiScreenSwitches.On(BizOpsId);

        public const string BizOpsId = "bizops";

        // What each page cannot be drawn without (its plans and its plan, its machines, its drivers, its stock): one an
        // update took away hands that tab to the game's own view, as its contract line said in Player.log at the start,
        // rather than a page that says there is nothing.
        private static readonly GameMember[] PricingCore = { GameContract.PriceList, GameContract.PriceOrder, GameContract.PriceGoods };
        private static readonly GameMember[] RoutesCore = { GameContract.RouteOrder, GameContract.RouteStops, GameContract.RouteFrom };
        private static readonly GameMember[] BuyingCore = { GameContract.BuyOrder };
        private static readonly GameMember[] HrCore = { GameContract.HrOrder, GameContract.HrStaff };
        private static readonly GameMember[] HuntCore = { GameContract.HuntPlan, GameContract.HuntOrder };
        private static readonly GameMember[] FactoryCore = { GameContract.FactoryMachineName, GameContract.FactoryMachineItem, GameContract.FactoryRecipe };
        private static readonly GameMember[] FleetCore = { GameContract.DriverChoice };
        private static readonly GameMember[] StockCore = { GameContract.StockBoxes, GameContract.StockSell };

        private static bool Lacks(GameMember[] core)
        {
            for (int i = 0; i < core.Length; i++)
            {
                if (core[i].Field == null)
                {
                    return true;
                }
            }
            return false;
        }

        private readonly List<OpsWidget> _widgets = new List<OpsWidget>(32);
        private readonly List<BizOpsRow> _rows = new List<BizOpsRow>(32);
        private int _opsHeld = -1;                  // the slider row held to take Left and Right
        private bool _right;                        // rows go to the form's right column

        // ------------------------------------------------------------------------------------------------- the form's rows

        private void RowsBegin()
        {
            _widgets.Clear();
            _rows.Clear();
            _right = false;
        }

        private void RowsEnd() => _m.Ops.Rows = _rows.ToArray();

        private int Widget(Component main, Button less = null, Button more = null)
        {
            _widgets.Add(new OpsWidget { Main = main, Less = less, More = more });
            return _widgets.Count - 1;
        }

        // A row the mod answers itself, in place of one of the game's widgets: a switch or a choice of words.
        private int ModWidget(int code)
        {
            _widgets.Add(new OpsWidget { Mod = code });
            return _widgets.Count - 1;
        }

        private void ModSwitch(int code, string label, bool on, bool disabled)
        {
            _rows.Add(new BizOpsRow
            {
                Kind = BizOpsRowKind.Switch, Label = label, Value = UiText.T(on ? "reallife_ui_bizops_on" : "reallife_ui_bizops_off"), On = on, Disabled = disabled,
                Arg = ModWidget(code), Right = _right
            });
        }

        private void ModChoice(int code, string label, string[] options, int chosen, bool disabled)
        {
            int arg = ModWidget(code);
            _rows.Add(new BizOpsRow
            {
                Kind = BizOpsRowKind.Choice, Label = label, Value = chosen >= 0 && chosen < options.Length ? options[chosen] : string.Empty, Disabled = disabled || options.Length == 0,
                Arg = arg, Right = _right
            });
            if (_m.Ops.ListOpen == arg)
            {
                _m.Ops.Options = options;
                _m.Ops.OptionChosen = chosen;
            }
        }

        private int ModOf(int arg) => arg >= 0 && arg < _widgets.Count ? _widgets[arg].Mod : 0;

        private void Heading(string words)
        {
            if (!string.IsNullOrEmpty(words))
            {
                _rows.Add(new BizOpsRow { Kind = BizOpsRowKind.Heading, Label = words, Right = _right });
            }
        }

        private void InfoLine(string label, string value, UiRole tone = UiRole.None)
        {
            if (!string.IsNullOrEmpty(value))
            {
                _rows.Add(new BizOpsRow { Kind = BizOpsRowKind.Info, Label = label, Value = value, Tone = tone, Right = _right });
            }
        }

        private void NoteLine(string words)
        {
            if (!string.IsNullOrEmpty(words))
            {
                _rows.Add(new BizOpsRow { Kind = BizOpsRowKind.Note, Value = words, Right = _right });
            }
        }

        private void MeterLine(string label, UI.Elements.ProgressBar bar)
        {
            if (bar != null && bar.gameObject.activeInHierarchy && bar.slider != null)
            {
                float span = bar.slider.maxValue - bar.slider.minValue;
                int grade = span > 0f ? Mathf.RoundToInt((bar.slider.value - bar.slider.minValue) / span * 100f) : 0;
                _rows.Add(new BizOpsRow { Kind = BizOpsRowKind.Meter, Label = label, Grade = Mathf.Clamp(grade, 0, 100), Right = _right });
            }
        }

        private void SwitchLine(Toggle t, string label)
        {
            if (t != null && t.gameObject.activeInHierarchy)
            {
                _rows.Add(new BizOpsRow
                {
                    Kind = BizOpsRowKind.Switch, Label = label, Value = UiText.T(t.isOn ? "reallife_ui_bizops_on" : "reallife_ui_bizops_off"), On = t.isOn,
                    Disabled = !t.interactable, Arg = Widget(t), Right = _right
                });
            }
        }

        private void ChoiceLine(UI.Elements.Dropdown d, string label)
        {
            if (d == null || !d.gameObject.activeInHierarchy)
            {
                return;
            }
            string[] options = Options(d);
            int chosen = d.SelectedOptionIndex;
            int arg = Widget(d);
            _rows.Add(new BizOpsRow
            {
                Kind = BizOpsRowKind.Choice, Label = label, Value = chosen >= 0 && chosen < options.Length ? options[chosen] : UiText.T("common_unassigned"),
                Disabled = Held(d) || options.Length == 0, Arg = arg, Right = _right
            });
            if (_m.Ops.ListOpen == arg)
            {
                _m.Ops.Options = options;
                _m.Ops.OptionChosen = chosen;
            }
        }

        private void SliderLine(Slider s, TMP_Text figure, string label)
        {
            if (s == null || !s.gameObject.activeInHierarchy)
            {
                return;
            }
            int arg = Widget(s);
            float span = s.maxValue - s.minValue;
            _rows.Add(new BizOpsRow
            {
                Kind = BizOpsRowKind.Slider, Label = label, Value = TextOf(figure) ?? UiText.Whole(Mathf.RoundToInt(s.value)), Share = span > 0f ? (s.value - s.minValue) / span : 0f,
                Held = _opsHeld == arg, Disabled = !s.interactable, Arg = arg, Right = _right
            });
        }

        private void ButtonLine(Button b, string label, string words, UiIcon icon, bool danger)
        {
            if (b != null && b.gameObject.activeInHierarchy)
            {
                _rows.Add(new BizOpsRow
                {
                    Kind = BizOpsRowKind.Button, Label = label, Value = words ?? UiWidgets.Label(b), Icon = icon, Tone = danger ? UiRole.Danger : UiRole.None,
                    Disabled = !b.interactable, Arg = Widget(b), Right = _right
                });
            }
        }

        private void TextLine(TMP_InputField f, string label, string placeholder, bool number)
        {
            if (f != null && f.gameObject.activeInHierarchy)
            {
                int arg = Widget(f);
                _rows.Add(new BizOpsRow
                {
                    Kind = number ? BizOpsRowKind.Stepper : BizOpsRowKind.Text, Label = label, Value = f.text, Placeholder = placeholder, Disabled = !f.interactable,
                    Typing = _typeCommand == BizManPages.OpsType && _typeArg == arg, Arg = arg, Right = _right
                });
            }
        }

        // An amount: the game's own minus and plus pressed, so its limit and its lock (Interactable, which Going Public sets) hold.
        private void AmountLine(AmountSelector a, string label)
        {
            if (a != null && a.gameObject.activeInHierarchy)
            {
                Button less = Get<Button>(GameContract.AmountLess, a), more = Get<Button>(GameContract.AmountMore, a);
                _rows.Add(new BizOpsRow
                {
                    Kind = BizOpsRowKind.Stepper, Label = label, Value = UiText.Whole(a.Amount), CanLess = less != null && less.interactable, CanMore = more != null && more.interactable,
                    Disabled = !a.Interactable, Arg = Widget(a, less, more), Right = _right
                });
            }
        }

        // A drop-down the game holds (its canvas group not taking presses: Going Public's locks, a contract past its order time).
        private static bool Held(UI.Elements.Dropdown d)
        {
            CanvasGroup group = d.GetComponent<CanvasGroup>();
            return group != null && !group.interactable;
        }

        private static string Loc(TextLocalizationComponent t) => t != null && t.gameObject.activeInHierarchy && t.TextContainer != null ? UiWidgets.Text(t.TextContainer) : null;

        private static Button ButtonOf(GameObject go) => go != null && go.activeInHierarchy ? go.GetComponentInChildren<Button>(true) : null;

        // ------------------------------------------------------------------------------------------------- presses

        // A row's press: its switch flipped or its button pressed; its list opened; an option of the list open chosen; a slider
        // held; an amount stepped; a field typed into. Each goes to the game's own widget.
        private bool PressRow(int command, int arg)
        {
            BizOpsModel o = _m.Ops;
            switch (command)
            {
                case BizManPages.OpsRow:
                {
                    if (ModOf(arg) != 0)
                    {
                        return ModPress(ModOf(arg), ModToggle);
                    }
                    Component c = WidgetAt(arg);
                    if (c is Toggle t)
                    {
                        bool done = UiWidgets.Set(t, !t.isOn);
                        UiDirector.Logger?.Info("BizMan (kit): a setting switched through the game's own toggle" + (done ? string.Empty : " was refused by the game"));
                        return done;
                    }
                    return c is Button b && Click(b, "a button of " + _shown);
                }
                case BizManPages.OpsChoice:
                    o.ListOpen = o.ListOpen == arg ? -1 : arg;
                    o.OptionOffset = 0f;
                    return true;
                case BizManPages.OpsOption:
                {
                    int list = o.ListOpen;
                    o.ListOpen = -1;
                    if (list >= 0 && list < BizManPages.OpsStopList && ModOf(list) != 0)
                    {
                        return arg >= 0 && ModPress(ModOf(list), ModOption + arg);
                    }
                    UI.Elements.Dropdown d = list >= BizManPages.OpsStopList && list < BizManPages.OpsPeopleList ? StopChoice(list - BizManPages.OpsStopList) : WidgetAt(list) as UI.Elements.Dropdown;
                    return Pick(d, arg);
                }
                case BizManPages.OpsSlider:
                    _opsHeld = _opsHeld == arg ? -1 : arg;
                    return true;
                case BizManPages.OpsLess:
                case BizManPages.OpsMore:
                {
                    bool more = command == BizManPages.OpsMore;
                    OpsWidget w = arg >= 0 && arg < _widgets.Count ? _widgets[arg] : default;
                    return w.Mod == 0 && Click(more ? w.More : w.Less, "an amount stepped");   // the mod's own rows step nothing
                }
                case BizManPages.OpsType:
                    return true;
                default:
                    return false;
            }
        }

        private Component WidgetAt(int arg) => arg >= 0 && arg < _widgets.Count ? _widgets[arg].Main : null;

        // A held slider stepped a twentieth of its way, or one whole step where it takes whole numbers, through the game's own slider.
        private void StepSlider(int arg, int step)
        {
            if (WidgetAt(arg) is Slider s)
            {
                float by = s.wholeNumbers ? step : step * (s.maxValue - s.minValue) / 20f;
                UiWidgets.Slide(s, Mathf.Clamp(s.value + by, s.minValue, s.maxValue));
                _dirty = true;
            }
        }

        // The words a row's field holds now, and the words typed into it, its end handed to the game as the field's own end is.
        private string RowTyping(int arg)
        {
            Component c = WidgetAt(arg);
            return c is TMP_InputField f ? f.text : c is AmountSelector a ? UiText.Whole(a.Amount) : null;
        }

        private void RowTyped(int arg, string text)
        {
            Component c = WidgetAt(arg);
            if (c is TMP_InputField f)
            {
                UiWidgets.Type(f, text, false);
                if (!string.IsNullOrEmpty(text))
                {
                    f.onEndEdit?.Invoke(f.text);   // the game takes a field's words when it is done with it: each word typed is that end here
                }
            }
            else if (c is AmountSelector a && int.TryParse(text, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.CurrentCulture, out int amount))
            {
                a.SetAmount(amount);   // the game's own setter, which keeps it to its limit and tells its listeners
            }
        }

        // Back on an operations page: a list open, then a slider held, closes first.
        private bool OpsBack()
        {
            if (_m.Ops.ListOpen >= 0 || _opsHeld >= 0)
            {
                _m.Ops.ListOpen = -1;
                _opsHeld = -1;
                return true;
            }
            return false;
        }

        // The operations' fields emptied, so a page never shows what another left.
        private void ClearOps()
        {
            BizOpsModel o = _m.Ops;
            o.Tabs = new string[0];
            o.Tab = 0;
            o.Plans = new BizManItem[0];
            o.PlansTitle = o.PlansEmpty = o.PlansEmptyBody = o.FormTitle = o.FormLead = o.FormEmpty = o.FormEmptyBody = o.TableTitle = o.TableLead = o.StopsTitle = null;
            o.PlanActions = new UiActionModel[0];
            o.Rows = new BizOpsRow[0];
            o.Stops = new BizOpsStop[0];
            o.Actions = new UiActionModel[0];
            o.Options = new string[0];
            o.OptionChosen = -1;
            o.Card = null;
            o.Progress = null;
            o.Filters = new string[0];
            o.Filter = 0;
            o.Status = null;
            o.FootLabel = o.FootWords = o.FootRight = null;
            o.FootShare = -1;
            o.FactLabels = o.FactValues = o.Problems = new string[0];
            o.FactWarn = new bool[0];
            o.FactActions = new UiActionModel[0];
            if (o.Table != null && _tableFor != _shown)
            {
                o.Table = null;
            }
            _tableFor = _shown;
            if (_shown != BizManRules.Schedule)
            {
                _chosen = null;
                _renaming = null;
                _hoursOpen = false;
            }
        }

        // ------------------------------------------------------------------------------------------------- the page's plumbing

        private short _boardClip = -1, _boardList = -1;   // the board's lanes' scroll region and list, as the last layout made them

        // A press of an operations page, to the page that answers it.
        private bool PressOps(BizManBusiness b, int command, int arg)
        {
            switch (_m.Part)
            {
                case BizManPart.Schedule: return PressSchedule(b, command, arg);
                case BizManPart.Managers: return PressManagers(b, command, arg);
                case BizManPart.Factory:
                case BizManPart.Drivers:
                case BizManPart.Stock: return PressPlant(b, command, arg);
                default: return false;
            }
        }

        // Back with the game's own question over the page: its own Cancel.
        private bool CardBack()
        {
            BizManBusiness b = _app.business;
            if (_m.Ops.Card == null || b == null || b.bizManSchedule == null)
            {
                return false;
            }
            PressConfirm(b.bizManSchedule, UiCard.ButtonCommand, 0);
            return true;
        }

        // Where the board's lanes scroll, found once a layout, so the wheel and a revealed lane move the board and not the people beside it.
        private void Clips(UiScene s)
        {
            _boardClip = _boardList = -1;
            UiBoardModel board = _m.Ops.Board;
            if (_m.Part != BizManPart.Schedule || board == null)
            {
                return;
            }
            for (int i = 0; i < s.ClipCount; i++)
            {
                if (Math.Abs(s.Clips[i].Y - board.LanesTop) < 0.5f && Math.Abs(s.Clips[i].H - board.LanesHeight) < 0.5f)
                {
                    _boardClip = (short)i;
                    _boardList = s.Clips[i].List;
                    break;
                }
            }
        }

        private bool OpsReveal(int list, int index)
        {
            if (_m.Page != BizManPage.Business)
            {
                return false;
            }
            BizOpsModel o = _m.Ops;
            if (_m.Part == BizManPart.Schedule)
            {
                if (o.Board != null && list == _boardList)
                {
                    o.Board.Offset = UiBoardRules.Reveal(o.Board, index);
                    return true;
                }
                if (o.People != null)
                {
                    o.People.Reveal = index;
                    return true;
                }
                return false;
            }
            if ((_m.Part == BizManPart.Managers || _m.Part == BizManPart.Factory || _m.Part == BizManPart.Stock) && o.Table != null)
            {
                o.Table.Reveal = index;
                return true;
            }
            return false;
        }

        private bool OpsWheel(int clip, float by)
        {
            if (_m.Page != BizManPage.Business)
            {
                return false;
            }
            BizOpsModel o = _m.Ops;
            if (_m.Part == BizManPart.Schedule)
            {
                if (o.Board != null && clip == _boardClip)
                {
                    o.Board.Offset = Math.Max(0f, o.Board.Offset + by);
                    return true;
                }
                if (o.People != null)
                {
                    o.People.Offset = Math.Max(0f, o.People.Offset + by);
                    return true;
                }
                return false;
            }
            if ((_m.Part == BizManPart.Managers || _m.Part == BizManPart.Factory || _m.Part == BizManPart.Stock) && o.Table != null)
            {
                o.Table.Offset = Math.Max(0f, o.Table.Offset + by);
                return true;
            }
            return false;
        }

        // The words an operations field holds when typing starts, and the words typed into it: the lanes' search or a name,
        // the picker's search, a form's field, a table's figure.
        private bool OpsTyping(int command, out string text)
        {
            text = null;
            BizManBusiness b = _app.business;
            if (b == null || _m.Page != BizManPage.Business)
            {
                return false;
            }
            switch (command)
            {
                case BizManPages.OpsSearch:
                case BizManPages.People + UiDataTable.Search:
                    text = ScheduleTyping(b, command);
                    return true;
                case BizManPages.OpsType:
                    text = RowTyping(_typeArg);
                    return true;
                case BizManPages.Table + UiDataTable.Type:
                    text = ManagersField(b, _typeArg)?.text;
                    return true;
                default:
                    return false;
            }
        }

        private bool OpsTyped(int command, int arg, string text)
        {
            BizManBusiness b = _app.business;
            if (b == null || _m.Page != BizManPage.Business)
            {
                return false;
            }
            switch (command)
            {
                case BizManPages.OpsSearch:
                case BizManPages.People + UiDataTable.Search:
                    ScheduleTyped(b, command, text);
                    return true;
                case BizManPages.OpsType:
                    RowTyped(arg, text);
                    return true;
                case BizManPages.Table + UiDataTable.Type:
                {
                    TMP_InputField f = ManagersField(b, arg);
                    if (f != null && UiWidgets.Type(f, text, false) && double.TryParse(text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.CurrentCulture, out _))
                    {
                        f.onEndEdit?.Invoke(f.text);   // the row's own end of editing applies the amount, as the game's field does when it is left
                    }
                    return true;
                }
                default:
                    return false;
            }
        }

        private string _tableFor;   // the tab the page's table was last laid for: its scroll kept while the tab stays
    }
}
