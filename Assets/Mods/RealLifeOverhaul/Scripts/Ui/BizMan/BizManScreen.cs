using System;
using System.Collections.Generic;
using Streets;
using TMPro;
using UI.Smartphone.Apps.BizMan;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// BizMan as a screen of the kit, mirrored from the game's own app: the portfolio from its list (the businesses as the
    /// game sorts and filters them, the homes, the warehouses, the headquarters), and a place's page from its business view
    /// (its tab bar's buttons as the sidebar, another mod's among them, the head, and the shown tab's page). Every press goes
    /// to the game's own button or the public method a button calls, so the game's questions, refusals and other mods'
    /// patches all run; nothing of the game's is worked out here. A page only the game's own view shows hands over to it.
    /// Cost: a look at a few counts and flags four times a second while it shows; a layout only when they change.
    /// </summary>
    public sealed partial class BizManScreen : IUiScreen, IUiTypingSubmit, IUiTypingQuiet, IUiTypingFocus, IUiStepping, IUiPointing, IUiTracking
    {
        private const float Every = 0.25f;   // seconds between looks at the game's BizMan
        private const float Away = 1f;       // seconds with no look: the app was closed or stood aside meanwhile

        private static readonly List<Button> Buttons = new List<Button>(64);
        private static readonly Dictionary<int, Sprite> Pictures = new Dictionary<int, Sprite>(32);
        private static bool _picturesOn;

        private readonly BizMan _app;
        private readonly BizManModel _m = new BizManModel();
        private bool _dirty = true;
        private float _next;
        private long _seen;
        private int _typeCommand = BizManPages.Type, _typeArg = BizManPages.FieldName;

        public BizManScreen(BizMan app)
        {
            _app = app;
            if (!_picturesOn)
            {
                // The game's own pictures (logos, the building's photo, the kinds of business) by the keys this screen gives them;
                // any other key is another part's.
                _picturesOn = true;
                UiPictures.Answer(key => key != null && key.StartsWith(PictureKey, StringComparison.Ordinal) ? Picture(key) : null);
            }
        }

        public string Id => "bizman";

        public bool Dirty
        {
            get
            {
                if (Time.unscaledTime >= _next)
                {
                    if (_refused != null && Time.unscaledTime - _next > Away)
                    {
                        _refused = null;   // the app was away since: what a press was told before is not said to whoever opens it again
                        _dirty = true;
                    }
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
            BizManPages.Lay(s, _m, frame, dark, keys);
            Clips(s);
            _dirty = false;
        }

        // ------------------------------------------------------------------------------------------------- what changed

        // What changing would change the screen: which face shows, the place and its tab, the list's rows and filters, and
        // the shown page's own few counts and flags.
        private long Signature()
        {
            BizManBusiness b = _app.business;
            bool page = b != null && b.gameObject.activeSelf;
            long sig = page ? 1 : 2;
            if (!page)
            {
                return sig * 31 + PortfolioSignature();
            }
            sig = sig * 31 + (b.address != null ? b.address.GetHashCode() : 0);
            sig = sig * 31 + Hash(GameContract.BizTab.Field?.GetValue(b) as string);
            sig = sig * 31 + Hash(_shown);
            Transform menu = GameContract.BizMenu.Field?.GetValue(b) as Transform;
            for (int i = 0; menu != null && i < menu.childCount; i++)
            {
                sig = sig * 3 + (menu.GetChild(i).gameObject.activeSelf ? 1 : 0);
            }
            return sig * 31 + PageSignature(b);
        }

        private static int Hash(string text) => text != null ? text.GetHashCode() : 0;

        // ------------------------------------------------------------------------------------------------- the model

        private void Build()
        {
            Pictures.Clear();
            _m.Title = UiText.T("smartphone_bizman");
            BizManBusiness b = _app.business;
            if (b != null && b.gameObject.activeSelf && b.buildingRegistration != null)
            {
                BuildPage(b);
            }
            else
            {
                BuildPortfolio();
            }
            _m.Refused = _refused;
            var prompts = new List<UiPrompt>(5)
            {
                new UiPrompt { Action = UiAction.Move, Label = UiText.T("reallife_ui_prompt_move") },
                new UiPrompt { Action = UiAction.Confirm, Label = UiText.T("reallife_ui_prompt_select") },
                new UiPrompt { Action = UiAction.TabPrev, Label = UiText.T(_m.Page == BizManPage.Portfolio ? "reallife_ui_prompt_group" : "reallife_ui_bizman_prompt_section") },
                new UiPrompt { Action = UiAction.Back, Label = UiText.T("reallife_ui_prompt_back") },
            };
            _m.Prompts = prompts.ToArray();
            _m.PromptCount = _m.Prompts.Length;
        }

        // ------------------------------------------------------------------------------------------------- presses

        private static BizManRefusal _why;   // why the press under way cannot be done, where its path knows: a fault unless it says otherwise
        private string _refused;             // what the page says of the last press that came to nothing, until a press is taken
        private long _refusedWhat;           // which press and which answer that sentence is about, so one made again and again is written to the log once
        private int _refusedTimes;           // how often in a row; 0 while the last press was taken
        private bool _refusedTold;           // that run's press was told on the page: its count is a warning in the log, another's a plain line
        private string _said;               // what a press that was taken still has to say on that line (a business opened and closed again at once by the utilities)

        public bool Press(int command, int arg)
        {
            _why = BizManRefusal.Fault;
            _said = null;
            int heard = ShellNotices.Heard;
            bool done = Run(command, arg);
            Answered(command, arg, done, heard);
            return done;
        }

        // What a press does, by the face that shows; false when nothing came of it.
        private bool Run(int command, int arg)
        {
            _dirty = true;
            return _m.Page == BizManPage.Portfolio ? PressPortfolio(command, arg) : PressPage(command, arg);
        }

        /// <summary>
        /// What became of a press, said where the player is looking: a button that came to nothing is never silent (Start business
        /// once only played the error sound, and no log could say so). The page's foot says the game's own refusal in its words
        /// (it was asked and answered with an error notice); of a page's own button (BizManRules.Promises), that the game holds it,
        /// does not show it, or that nothing answered it, the page's own fault; of any other button with words, that the game
        /// holds or does not show it. Player.log has one line either way, written without Developer diagnostics.
        /// </summary>
        private void Answered(int command, int arg, bool done, int heardBefore)
        {
            bool byGame = done && ShellNotices.Heard != heardBefore && ShellNotices.LastType == UI.Notification.NotificationType.Error;
            if (done && !byGame)
            {
                Repeats();
                _refused = _said;   // nothing, but for a press with more to say than that it was taken
                return;
            }
            if (!done)
            {
                // Written below with its page and place (a run of the same press once, with its count): the host that sent the
                // press writes no line of its own for it, so one refusal is in the log once.
                UiRefusals.Told();
            }
            string action = Pressed(command, arg);
            // A target without words (a chart's point, a tab already chosen, whose button the game holds) may have nothing to do
            // where it stands: it keeps to the error cue and a plain line in the log, and is never called a fault.
            bool told = byGame || BizManRules.Promises(command) || (action != null && _why != BizManRefusal.Fault);
            long what = (((long)command << 32) | (uint)arg) * 31 + (byGame ? Hash(ShellNotices.LastKey) : (int)_why);
            if (told)
            {
                // Asked and refused, the game's own words are the reason, as its notice says them: no fault of the page's.
                _refused = byGame ? ShellNotices.LastWords() ?? _refused
                    : UiText.Fill(BizManRules.RefusalKey(_why), "action", action ?? UiText.T("reallife_ui_bizman_refused_press"));
            }
            if (_refusedTimes > 0 && what == _refusedWhat)
            {
                _refusedTimes++;   // the same press with the same answer: counted, and said once with its count when the run ends
                return;
            }
            Repeats();
            _refusedWhat = what;
            _refusedTimes = 1;
            _refusedTold = told;
            string where = Target(command, arg, action) + " on " + PressedOn();
            if (byGame)
            {
                UiDirector.Logger?.Warn("BizMan check (press): " + where + " was handed to the game, which refused it with its notice '" + ShellNotices.LastKey
                                        + "'; the page says the game's words: PASS.");
            }
            else if (told)
            {
                UiDirector.Logger?.Warn("BizMan check (press): " + where + " came to nothing: " + BizManRules.RefusalSaid(_why) + Widget() + "; the page says so in words"
                                        + (_why == BizManRefusal.Fault ? ": FAIL." : "."));
            }
            else
            {
                UiDirector.Logger?.Info("BizMan (kit): a press of " + where + " did nothing and got the error sound"
                                        + (_why != BizManRefusal.Fault ? " (" + BizManRules.RefusalSaid(_why) + ")" : string.Empty) + Widget() + "; the page says nothing of it.");
            }
        }

        // What the game's own widget said of the press it refused (UiWidgets gives it: which button, switched off or held), for the line above.
        private static string Widget() => UiRefusals.Why != null ? " (" + UiRefusals.Why + ")" : string.Empty;

        // A press refused more than once in a row is said once more with its count, when another press ends the run: as loud as its first line was.
        private void Repeats()
        {
            if (_refusedTimes > 1 && _refusedTold)
            {
                UiDirector.Logger?.Warn("BizMan check (press): that press was made " + _refusedTimes + " times in a row, with the same answer each time.");
            }
            else if (_refusedTimes > 1)
            {
                UiDirector.Logger?.Info("BizMan (kit): that press was made " + _refusedTimes + " times in a row, with the same answer each time.");
            }
            _refusedTimes = 0;
        }

        private static string Target(int command, int arg, string action) =>
            (action != null ? "'" + action + "' (" : "target (") + command.ToString(System.Globalization.CultureInfo.InvariantCulture) + "/"
            + arg.ToString(System.Globalization.CultureInfo.InvariantCulture) + ")";

        // Where the press was made, for the log: the portfolio, or a place's page by its part and address.
        private string PressedOn()
        {
            if (_m.Page == BizManPage.Portfolio)
            {
                return "the portfolio";
            }
            BizManBusiness b = _app.business;
            return "the " + _m.Part + " page" + (b != null && b.address != null ? " of " + b.address.ToFormattedString() : string.Empty);
        }

        // The words on the target pressed, where it has words of its own: a button's label, the open switch.
        private string Pressed(int command, int arg)
        {
            if (command == UiFirmHead.First + UiFirmHead.Switch)
            {
                return UiText.T("reallife_ui_bizman_refused_switch");
            }
            if (command == UiFirmHead.First + UiFirmHead.Action)
            {
                return _m.Head != null && arg >= 0 && arg < _m.Head.Actions.Length ? _m.Head.Actions[arg].Label : null;
            }
            if (command == BizManPages.OpsAction)
            {
                return Named(_m.Ops.Actions, arg) ?? Named(_m.Ops.PlanActions, arg);   // an operations page's own buttons
            }
            if (command != BizManPages.Action && command != BizManPages.LineAction)
            {
                return null;
            }
            string label = Named(_m.Actions, arg);
            if (label != null)
            {
                return label;
            }
            if (command == BizManPages.Action && _m.Amount != null && _m.Amount.Send.Arg == arg)
            {
                return _m.Amount.Send.Label;
            }
            return command == BizManPages.Action && _m.Second != null && _m.Second.Send.Arg == arg ? _m.Second.Send.Label : null;
        }

        // The words of the action with that argument, or null: one drawn as a glyph alone has no words to name it by.
        private static string Named(UiActionModel[] actions, int arg)
        {
            for (int i = 0; actions != null && i < actions.Length; i++)
            {
                if (actions[i].Arg == arg)
                {
                    return string.IsNullOrEmpty(actions[i].Label) ? null : actions[i].Label;
                }
            }
            return null;
        }

        public void Focused(int command, int arg)
        {
            Aim(command, arg);
            // A tab is chosen as the pad comes to it, so the ring is never on one tab while another is chosen. No press of the
            // player's: what comes of it is not told as one.
            if ((command == BizManPages.Period && arg != _m.Period) || (command == BizManPages.Page && arg != _m.PageShown))
            {
                Run(command, arg);
            }
            if (command == UiChart.First + UiChart.Point && _m.Chart != null && _m.Chart.Focus != arg)
            {
                _m.Chart.Focus = arg;
                _dirty = true;
            }
        }

        public void Pointed(int command, int arg)
        {
            Aim(command, arg);
            if (command != UiBoard.LaneCommand && _hoverLane >= 0)
            {
                _hoverLane = _hoverHour = -1;   // the mouse left the lanes: no + is aimed
                _dirty = true;
            }
            if (command == UiChart.First + UiChart.Point && _m.Chart != null && _m.Chart.Focus != arg)
            {
                _m.Chart.Focus = arg;   // the mouse resting on a point reads it in its card
                _dirty = true;
            }
        }

        // The field a press would type into: the last one the keys or the mouse came to.
        private void Aim(int command, int arg)
        {
            if (command == BizManPages.Type || command == UiDataTable.First + UiDataTable.Search || command == BizManPages.Goods + UiDataTable.Type || command == BizManPages.OpsType
                || command == BizManPages.OpsSearch || command == BizManPages.People + UiDataTable.Search || command == BizManPages.Table + UiDataTable.Type
                || command == BizManPages.Table + UiDataTable.Search || command == BizManPages.StartKinds + UiDataTable.Search)
            {
                _typeCommand = command;
                _typeArg = arg;
            }
        }

        public bool Back()
        {
            _dirty = true;
            _refused = null;   // the player has moved on: what the last press was told does not follow to the list or stay under a closed sheet
            if (CloseLists() || OpsBack() || CardBack() || (_m.Part == BizManPart.Schedule && ScheduleBack()))
            {
                return true;
            }
            if (_m.Page != BizManPage.Business)
            {
                return false;
            }
            BackToList();
            return true;
        }

        public void Tab(int step)
        {
            int count = _m.Nav.Length;
            if (count < 2)
            {
                return;
            }
            int next = ((_m.NavSelected + step) % count + count) % count;
            Press(BizManPages.Section, next);
        }

        public bool Act(UiAction action)
        {
            // A page written as lines offers its own buttons under their own command, the other pages their actions.
            int own = _m.Page == BizManPage.Business && _m.Part == BizManPart.Interior ? BizManPages.LineAction : BizManPages.Action;
            if (Pressing(_m.Head?.Actions, action, UiFirmHead.First + UiFirmHead.Action, true) || Pressing(_m.Actions, action, own, false))
            {
                return true;
            }
            if (_m.Amount != null && _m.Amount.Send.Key == action && !_m.Amount.Send.Disabled)
            {
                return Press(BizManPages.Action, _m.Amount.Send.Arg);
            }
            if (_m.Page == BizManPage.Business && (Pressing(_m.Ops.Actions, action, BizManPages.OpsAction, false) || Pressing(_m.Ops.PlanActions, action, BizManPages.OpsAction, false)))
            {
                return true;
            }
            return false;
        }

        // The action a key runs from anywhere, pressed as its button would be: by its index in the head, by what it does on a page.
        private bool Pressing(UiActionModel[] actions, UiAction key, int command, bool byIndex)
        {
            for (int i = 0; actions != null && i < actions.Length; i++)
            {
                if (actions[i].Key == key && !actions[i].Disabled)
                {
                    return Press(command, byIndex ? i : actions[i].Arg);
                }
            }
            return false;
        }

        public void Reveal(int list, int index)
        {
            if (_m.LogoEdit && _m.Part == BizManPart.Settings && list >= _m.ShapesList && list < _m.ShapesList + _m.ShapesColumns && _m.ShapesList >= 0)
            {
                _m.ShapesReveal = index;   // a row of the shapes' grid the pad stepped to
                _dirty = true;
                return;
            }
            if (_m.Part == BizManPart.Interior && _m.Page == BizManPage.Business && (list == _m.LinesList || list == _m.SecondList))
            {
                if (list == _m.LinesList)
                {
                    _m.LinesReveal = index;
                }
                else
                {
                    _m.SecondReveal = index;
                }
                _dirty = true;
                return;
            }
            for (int k = 0; list >= 0 && k < BizManModel.Scrolls; k++)
            {
                if (list == _m.ListList[k])
                {
                    _m.ListReveal[k] = index;   // a row or a switch of a list that scrolls, which the pad stepped to
                    _dirty = true;
                    return;
                }
            }
            if (OpsReveal(list, index))
            {
                _dirty = true;
                return;
            }
            if (StartReveal(list, index))
            {
                _dirty = true;
                return;
            }
            UiDataTableModel table = _m.Page == BizManPage.Portfolio ? _m.Table : _m.Goods;
            if (table != null)
            {
                table.Reveal = index;
            }
            _dirty = true;
        }

        public void Wheel(int clip, float notches)
        {
            float by = -notches * UiTokens.S16;
            if (_m.LogoEdit && _m.Part == BizManPart.Settings && clip == _m.ShapesClip && clip >= 0)
            {
                _m.ShapesOffset = Math.Max(0f, _m.ShapesOffset + by);
                _dirty = true;
                return;
            }
            for (int k = 0; clip >= 0 && k < BizManModel.Scrolls; k++)
            {
                if (clip == _m.ListClip[k])
                {
                    _m.ListOffset[k] = Math.Max(0f, _m.ListOffset[k] + by);
                    _dirty = true;
                    return;
                }
            }
            if (OpsWheel(clip, by))
            {
                _dirty = true;
                return;
            }
            if (_m.Part == BizManPart.Interior && _m.Page == BizManPage.Business && (clip == _m.LinesClip || clip == _m.SecondClip))
            {
                if (clip == _m.LinesClip)
                {
                    _m.LinesOffset = Math.Max(0f, _m.LinesOffset + by);
                }
                else
                {
                    _m.SecondOffset = Math.Max(0f, _m.SecondOffset + by);
                }
                _dirty = true;
                return;
            }
            if (StartWheel(clip, by))
            {
                _dirty = true;
                return;
            }
            UiDataTableModel table = _m.Page == BizManPage.Portfolio ? _m.Table : _m.Goods;
            if (table != null)
            {
                table.Offset = Math.Max(0f, table.Offset + by);
            }
            _dirty = true;
        }

        // ------------------------------------------------------------------------------------------------- typing

        public int TypingCommand => _typeCommand;

        public string TypingText => _typeCommand == BizManPages.StartKinds + UiDataTable.Search ? _kindQuery
            : OpsTyping(_typeCommand, out string ops) ? ops : Field(_typeCommand, _typeArg) is TMP_InputField f ? f.text : null;


        /// <summary>The game's own limit of the box typed into (a name's, as its Steam input reads it), or none of the page's own for a search.</summary>
        public int TypingLimit
        {
            get
            {
                if (_typeCommand != BizManPages.Type)
                {
                    return 0;
                }
                TMP_InputField f = Field(BizManPages.Type, _typeArg);
                return f != null && f.characterLimit > 0 ? f.characterLimit : _typeArg == BizManPages.FieldName ? BizManStartRules.NameLimit : 0;
            }
        }

        /// <summary>Enter in a box does what the game's own box does with it: start the business, send the offer, or put the building up for sale.</summary>
        public void Submitted(string text)
        {
            int what = BizManStartRules.Entered(_typeCommand == BizManPages.Type, _typeArg, _m.Page == BizManPage.Business && _m.Part == BizManPart.Start);
            if (what < 0)
            {
                return;   // a search, or a name that has no Enter of its own (the settings' name is saved by its button)
            }
            Press(BizManPages.Action, what);
            _focusName = what == BizManPages.DoStartHere && _refused != null;   // the game's box takes the keys again after Enter, so a name it refused is mended where it stands
        }

        public bool TakeFocus()
        {
            bool want = _focusName && _m.Page == BizManPage.Business && _m.Part == BizManPart.Start;
            _focusName = false;
            return want;
        }

        public void Typed(string text)
        {
            if (_refused != null)
            {
                _refused = null;   // typing answers what a press was told (no name yet): the line goes as the words come
                _dirty = true;
            }
            if (_typeCommand == BizManPages.StartKinds + UiDataTable.Search)
            {
                KindTyped(text);
                _dirty = true;
                return;
            }
            if (OpsTyped(_typeCommand, _typeArg, text))
            {
                _dirty = true;
                return;
            }
            if (Field(_typeCommand, _typeArg) is TMP_InputField f)
            {
                UiWidgets.Type(f, text, false);   // its change listeners run as typing into it would run them
                if (_typeCommand == UiDataTable.First + UiDataTable.Search)
                {
                    ShopsTable?.RefreshFilters();   // the list filtered again even while its filter panel, which hears its field, is closed
                }
                _dirty = true;
            }
        }


        // ------------------------------------------------------------------------------------------------- stepping

        public bool Steps(int command) => (command == BizManPages.Slider && _sliderHeld) || (command == BizManPages.OpsSlider && _opsHeld >= 0);

        public void Step(int command, int arg, int step)
        {
            if (command == BizManPages.Slider)
            {
                StepRent(step);
            }
            else if (command == BizManPages.OpsSlider && arg == _opsHeld)
            {
                StepSlider(arg, step);
            }
        }

        // ------------------------------------------------------------------------------------------------- the game's pieces

        private const string PictureKey = "bizman:";

        // A sprite of the game's as a picture key the renderer asks for again only when the sprite itself changes.
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

        private static float Aspect(Sprite sprite) => sprite != null && sprite.rect.height > 0f ? sprite.rect.width / sprite.rect.height : 1f;

        private static Sprite Picture(string key)
        {
            return int.TryParse(key.Substring(PictureKey.Length), System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out int id)
                   && Pictures.TryGetValue(id, out Sprite sprite) ? sprite : null;
        }

        // A field of the game's read from its contract line; null when this version lacks it.
        private static T Get<T>(GameMember member, object owner) where T : class => owner != null && member.Field != null ? member.Field.GetValue(owner) as T : null;

        // The words a piece shows, its rich text taken off; null when it is not shown.
        private static string Words(Component c) => c != null && c.gameObject.activeInHierarchy ? UiWidgets.Label(c) : null;

        // The words at a path under a root, as the game's own GetLabelByName finds them.
        private static string WordsAt(Transform root, string path)
        {
            Transform t = root != null ? root.Find(path) : null;
            return t != null ? Words(t) : null;
        }

        // The button under a root whose click the game wired to a method of that name and that shows now; null when none does.
        private static Button Calling(Component root, string method)
        {
            Buttons.Clear();
            if (root == null)
            {
                return null;
            }
            root.GetComponentsInChildren(true, Buttons);
            Button found = null;
            for (int i = 0; i < Buttons.Count && found == null; i++)
            {
                UnityEvent click = Buttons[i].onClick;
                int n = click.GetPersistentEventCount();
                for (int k = 0; k < n; k++)
                {
                    if (click.GetPersistentMethodName(k) == method && Buttons[i].gameObject.activeInHierarchy)
                    {
                        found = Buttons[i];
                        break;
                    }
                }
            }
            Buttons.Clear();
            return found;
        }

        // A button pressed as a click presses it, with one line in Player.log so a report shows each press reaching the game. One not
        // there to press says which way, for the page's line.
        private static bool Click(Button b, string what)
        {
            bool done = UiWidgets.Press(b);
            if (!done)
            {
                _why = b != null && b.gameObject.activeInHierarchy ? BizManRefusal.Held : BizManRefusal.Gone;
            }
            UiDirector.Logger?.Info("BizMan (kit): " + what + (done ? ": handed to the game"
                : _why == BizManRefusal.Held ? " is held by the game, so nothing was pressed" : " was not shown by the game, so nothing was pressed"));
            return done;
        }

        // What came of a press that asked the game for one thing (to rent, to start a business, to open): written to Player.log when
        // it did not come about and the game gave no notice either, since nothing else would say so; a refusal the game gave its
        // notice for is written with the press. With Developer diagnostics on, also when it came about.
        private static void Outcome(string asked, BizManBusiness b, bool came, int heardBefore)
        {
            string place = b != null && b.address != null ? b.address.ToFormattedString() : "no place";
            if (came)
            {
                if (Meta.Dev.Verbose)
                {
                    UiDirector.Logger?.Info("BizMan check (" + asked + "): " + place + ": asked of the game and done: PASS.");
                }
                return;
            }
            if (ShellNotices.Heard == heardBefore)
            {
                UiDirector.Logger?.Warn("BizMan check (" + asked + "): " + place + ": asked of the game, which did not do it and gave no notice: FAIL.");
            }
        }

        // A role for the colour the game paints a word in: its green good, its red bad, its yellow or orange a warning.
        private static UiRole Tone(Color c)
        {
            if (c.a <= 0.01f)
            {
                return UiRole.Muted;
            }
            if (c.g > 0.5f && c.r < 0.5f)
            {
                return UiRole.Ok;
            }
            if (c.r > 0.6f && c.g < 0.35f)
            {
                return UiRole.Danger;
            }
            if (c.r > 0.6f && c.g > 0.4f && c.b < 0.4f)
            {
                return UiRole.Warn;
            }
            return UiRole.Muted;
        }

        private static UiRole Ink(UiRole tone) => tone == UiRole.Ok ? UiRole.OkText : tone == UiRole.Danger ? UiRole.DangerText : tone == UiRole.Warn ? UiRole.WarnText : UiRole.None;
    }
}
