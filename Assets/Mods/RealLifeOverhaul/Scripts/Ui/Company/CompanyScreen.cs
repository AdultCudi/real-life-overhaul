using System;

namespace RealLifeOverhaul.Ui
{
    /// <summary>The Company app's source: it reads the director into the facts for the view's choices, and does what an action stands for. Implemented by the live source (CompanyLive) and by the checks' fakes.</summary>
    public interface ICompanySource
    {
        /// <summary>A number that changes when the company's state does, so the screen reads again only then.</summary>
        int Generation { get; }

        /// <summary>A place in the app another part of the game asked for (a notification's press): the tab, what is picked on it, its sub page, what is picked in that and a question to open as a card (0 for none), taken once; false for none.</summary>
        bool TakeWanted(out int tab, out string pick, out int sub, out string subPick, out int question);

        /// <summary>The app has been opened since the last read: its sub pages and open question start from the tab as left.</summary>
        bool TakeOpened();

        /// <summary>Fills the facts for the view's choices, from a reset state.</summary>
        void Read(CompanyFacts facts, CompanyView view);

        /// <summary>Does what an action stands for; false when it could not be done (the host plays the error).</summary>
        bool Do(CompanyAct act, CompanyView view);
    }

    /// <summary>
    /// The Company app on the kit: seven tabs, each its own page of its own facts (CompanyFace). The facts are read when the company changes or the player's choice
    /// does (a tab, a row, a day), never every frame; a button goes to the director that already owns it through <see cref="ICompanySource.Do"/>. The old app's pages stay
    /// underneath for the per-screen switch. A typed figure (the reserve, the most hours) goes through the host's text field. Nothing a frame; nothing allocated while idle.
    /// </summary>
    public sealed class CompanyScreen : IUiScreen, IUiTypingSubmit
    {
        public const string ScreenId = "company";

        private readonly ICompanySource _source;
        private readonly CompanyWords _words;
        private readonly CompanyFacts _facts = new CompanyFacts();
        private readonly CompanyView _view = new CompanyView();
        private readonly CompanyLayout _layout = new CompanyLayout();
        private int _seen = -1;
        private bool _dirty = true, _stale = true;
        private int _typingIndex = -1;

        public CompanyScreen(ICompanySource source, CompanyWords words)
        {
            _source = source;
            _words = words;
        }

        public string Id => ScreenId;

        public bool Dirty => _dirty || _stale || (_source != null && _source.Generation != _seen);

        /// <summary>The facts as last read, for the checks.</summary>
        public CompanyFacts Facts => _facts;

        public CompanyView View => _view;

        public CompanyLayout Layout => _layout;

        public void Lay(UiScene s, UiFrame frame, bool dark, IUiKeys keys)
        {
            if (_source != null)
            {
                if (_source.TakeOpened())
                {
                    ClearSubPages();
                    _stale = true;
                }
                if (_source.TakeWanted(out int tab, out string pick, out int sub, out string subPick, out int question) && tab >= 0 && tab < CompanyView.Tabs)
                {
                    _view.Tab = tab;
                    _view.Pick[tab] = pick ?? string.Empty;
                    _view.Sub[tab] = sub;
                    _view.SubPick[tab] = subPick ?? string.Empty;
                    _view.SubArg[tab] = 0;
                    _view.Question = question;
                    _view.Choice = 0;
                    _stale = true;
                }
                if (_stale || _seen != _source.Generation)
                {
                    _seen = _source.Generation;
                    _source.Read(_facts, _view);
                }
            }
            _stale = false;
            CompanyFace.Lay(s, _facts, _view, _words, _layout, frame, dark, keys);
            _dirty = false;
        }

        // The app opens on the tab it was left on, never in a sub page or a question's card.
        private void ClearSubPages()
        {
            for (int i = 0; i < CompanyView.Tabs; i++)
            {
                _view.Sub[i] = 0;
                _view.SubPick[i] = string.Empty;
                _view.SubArg[i] = 0;
            }
            _view.Question = 0;
            _view.Typing = null;
            _view.ArmedId = null;
            _view.Armed = -1;
        }

        // ------------------------------------------------------------------------------------------------- presses

        public bool Press(int command, int arg)
        {
            CommitTyping();
            bool done = Route(command, arg);
            if (done)
            {
                _stale = true;
                _dirty = true;
            }
            return done;
        }

        private bool Route(int command, int arg)
        {
            // Every kind of target has a number of its own (CompanyFace), the question card's choices and buttons among them, so a number means the same on every page and
            // in every state; only the kit's parts keep theirs (the sidebar's, the tree's, the board's), which no other kind shares.
            switch (command)
            {
                case UiAppWindow.NavCommand:
                    return ShowTab(arg);
                case CompanyFace.Act:
                case CompanyFace.Item:
                case CompanyFace.Opt:
                    return Act(arg);
                case CompanyFace.Row:
                    return SelectRow(arg);
                case CompanyFace.Choice:
                    return Choose(arg);
                case CompanyFace.Filter:
                    return Filter(arg);
                case CompanyFace.Badge:
                    return OpenBusiness(arg);
                case CompanyFace.DayHead:
                    return arg >= 0 && arg < 7 && SetDay(arg);
                case CompanyFace.StepLess:
                    return arg >= 0 && arg < _layout.ActCount && !_layout.Acts[arg].Disabled && Run(_layout.Acts[arg].Act);
                case CompanyFace.StepMore:
                    return arg + 1 >= 0 && arg + 1 < _layout.ActCount && !_layout.Acts[arg + 1].Disabled && Run(_layout.Acts[arg + 1].Act);
                case CompanyFace.StepType:
                    if (arg + 2 < 0 || arg + 2 >= _layout.ActCount || _layout.Acts[arg + 2].Act.Do == CompanyDo.None)
                    {
                        return false;
                    }
                    BeginTyping(arg + 2);
                    return true;
                case UiBoard.DayCommand:
                    return arg >= 0 && arg < 7 && SetDay(arg);
                case UiBoard.ShiftCommand:
                case UiBoard.LaneCommand:
                    return true;   // a shift or a lane chosen on the day's board: it is read, the planner writes it
                case UiBoard.ActionCommand:
                    return false;
                default:
                    break;
            }
            if (command == UiOrgTree.First + UiOrgTree.Pick)
            {
                return PickNode(arg, true);
            }
            if (command == UiOrgTree.First + UiOrgTree.All)
            {
                return FoldAll();
            }
            if (command == UiOrgTree.First + UiOrgTree.Foot)
            {
                return Run(CompanyAct.Of(CompanyDo.Go, null, CompanyView.Managers, CompanyView.SubPolicy));
            }
            return false;
        }

        // A chip over a list: the filter of a tab; on the policy centre the scope, which is a node of the tree.
        private bool Filter(int arg)
        {
            CompanyManagersFacts m = _facts.Managers;
            if (_view.Tab == CompanyView.Managers && _view.Sub[CompanyView.Managers] == CompanyView.SubPolicy)
            {
                if (arg < 0 || arg >= m.FilterIds.Length)
                {
                    return false;
                }
                _view.Pick[CompanyView.Managers] = m.FilterIds[arg];
                _view.SubPick[CompanyView.Managers] = string.Empty;
                return true;
            }
            _view.Filter = arg;
            return true;
        }

        private bool ShowTab(int tab)
        {
            if (tab < 0 || tab >= CompanyView.Tabs)
            {
                return false;
            }
            _view.Tab = tab;
            _view.Typing = null;
            _view.Armed = -1;
            _view.ArmedId = null;
            _view.Filter = 0;
            _view.Sub[tab] = 0;   // a tab opens on its own page
            _view.SubPick[tab] = string.Empty;
            return true;
        }

        private bool SetDay(int day)
        {
            _view.Day = day;
            return true;
        }

        private bool Choose(int choice)
        {
            CompanyQuestion q = _view.Question != 0 ? _facts.Question : _facts.Overview.Lead;
            if (q == null || choice < 0 || choice >= q.Choices.Length)
            {
                return false;
            }
            _view.Choice = choice;
            return true;
        }

        // A row of the tab's list picked: the page beside it shows that one.
        private bool SelectRow(int row)
        {
            if (row < 0 || row >= _layout.RowIds.Length || string.IsNullOrEmpty(_layout.RowIds[row]))
            {
                return false;
            }
            int tab = _view.Tab;
            if (tab == CompanyView.Managers && _view.Sub[tab] != CompanyView.SubMain)
            {
                _view.SubPick[tab] = _layout.RowIds[row];   // a sub page's list: the row is chosen inside the page, which stays
                return true;
            }
            if (_view.Pick[tab] != _layout.RowIds[row])
            {
                _view.Pick[tab] = _layout.RowIds[row];
                _view.Choice = 0;
                _view.Insurer = 0;
                _view.Kind = 0;
            }
            _view.Sub[tab] = CompanyView.SubMain;
            return true;
        }

        private bool OpenBusiness(int index)
        {
            CompanyBusiness[] list = _facts.Overview.Businesses;
            if (index < 0 || index >= list.Length)
            {
                return false;
            }
            _view.Tab = CompanyView.Overview;
            _view.Pick[CompanyView.Overview] = list[index].Place ?? string.Empty;
            _view.Sub[CompanyView.Overview] = CompanyView.SubBusiness;
            return true;
        }

        // A node of the managers' tree: chosen, or (the chosen one pressed again, with rows below it) folded or unfolded.
        private bool PickNode(int index, bool pressed)
        {
            if (index < 0 || index >= _layout.TreeIds.Length)
            {
                return false;
            }
            string id = _layout.TreeIds[index];
            CompanyManagersFacts m = _facts.Managers;
            if (pressed && id == m.Chosen && HasKids(id))
            {
                return Fold(id);
            }
            _view.Pick[CompanyView.Managers] = id;
            _view.Sub[CompanyView.Managers] = CompanyView.SubMain;
            return true;
        }

        private bool HasKids(string id)
        {
            CompanyManagersFacts m = _facts.Managers;
            for (int i = 0; i < m.TreeIds.Length && i < m.Tree.Length; i++)
            {
                if (m.TreeIds[i] == id)
                {
                    return m.Tree[i].HasKids;
                }
            }
            return false;
        }

        private bool Fold(string id)
        {
            if (!_view.FoldedNodes.Remove(id))
            {
                _view.FoldedNodes.Add(id);
            }
            return true;
        }

        // "Collapse all", or "Expand all" when everything that folds is folded.
        private bool FoldAll()
        {
            CompanyManagersFacts m = _facts.Managers;
            bool all = true;
            for (int i = 0; i < m.Tree.Length && i < m.TreeIds.Length; i++)
            {
                all &= !m.Tree[i].HasKids || _view.FoldedNodes.Contains(m.TreeIds[i]);
            }
            for (int i = 0; i < m.Tree.Length && i < m.TreeIds.Length; i++)
            {
                if (!m.Tree[i].HasKids)
                {
                    continue;
                }
                if (all)
                {
                    _view.FoldedNodes.Remove(m.TreeIds[i]);
                }
                else
                {
                    _view.FoldedNodes.Add(m.TreeIds[i]);
                }
            }
            return true;
        }

        // The index of an action as the layout registered it: a destructive one is armed by a first press and done by the second.
        private bool Act(int index)
        {
            if (index < 0 || index >= _layout.ActCount)
            {
                return false;
            }
            CompanyButton b = _layout.Acts[index];
            if (b.Disabled)
            {
                return false;
            }
            if (b.Arms)
            {
                string id = (int)b.Act.Do + ":" + b.Act.S + ":" + b.Act.S2 + ":" + b.Act.A + ":" + b.Act.B + ":" + b.Act.C;
                if (_view.ArmedId != id)
                {
                    _view.ArmedId = id;
                    return true;
                }
                _view.ArmedId = null;
            }
            return Run(b.Act);
        }

        // What an action does: moving about is the screen's own, the rest the director's.
        private bool Run(CompanyAct act)
        {
            switch (act.Do)
            {
                case CompanyDo.None:
                    return false;
                case CompanyDo.Go:
                    return Go(act);
                default:
                    return _source != null && _source.Do(act, _view);
            }
        }

        private bool Go(CompanyAct a)
        {
            if (a.S == "later")
            {
                _view.Later.Add(a.A);
                if (_view.Question == a.A)
                {
                    _view.Question = 0;
                }
                _view.Choice = 0;
                return true;
            }
            if (a.S == "amount")
            {
                _view.Amount = a.C > 0 ? a.C : Math.Max(0, (_view.Amount > 0 ? _view.Amount : a.B) + a.A);   // typed, or stepped from what is shown (B)
                return true;
            }
            if (a.S == "kind")
            {
                _view.Kind = a.A;   // the campaign kind or the accountant's tier chosen on its page
                return true;
            }
            if (a.S == "insurer")
            {
                _view.Insurer = a.A;
                return true;
            }
            if (a.S == "card")
            {
                _view.Question = a.A;
                _view.Choice = 0;
                return true;
            }
            if (a.A < 0 || a.A >= CompanyView.Tabs)
            {
                return false;
            }
            _view.Tab = a.A;
            _view.Sub[a.A] = a.B;
            _view.SubArg[a.A] = a.C;
            _view.SubPick[a.A] = string.Empty;
            if (a.S != null)
            {
                _view.Pick[a.A] = a.S;
            }
            _view.Question = 0;
            _view.Filter = 0;
            return true;
        }

        // ------------------------------------------------------------------------------------------------- typing

        public int TypingCommand => CompanyFace.StepType;

        public int TypingLimit => _view.TypingKind == CompanyTyping.PlanName ? CompanyTyping.NameMost : 9;

        public string TypingText => _view.Typing ?? string.Empty;

        public void Typed(string text)
        {
            _view.Typing = Filtered(text);
            _dirty = true;
        }

        public void Submitted(string text)
        {
            _view.Typing = Filtered(text);
            CommitTyping();
            _stale = true;
            _dirty = true;
        }

        // A field is for a figure (digits) or a plan's name (words), by the action it commits.
        private void BeginTyping(int index)
        {
            _typingIndex = index;
            CompanyAct act = _layout.Acts[index].Act;
            bool name = act.Do == CompanyDo.PlanChange && act.B == CompanyTyping.PlanNameField;
            _view.TypingKind = name ? CompanyTyping.PlanName : CompanyTyping.Amount;
            _view.TypingId = name ? act.S + ":" + act.A : null;
            _view.Typing = name ? (act.S2 ?? string.Empty) : null;   // a name is edited from what it is; a figure is typed anew
        }

        private string Filtered(string text)
        {
            return _view.TypingKind == CompanyTyping.PlanName ? Words(text) : Digits(text);
        }

        private static string Words(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }
            var chars = new char[Math.Min(text.Length, CompanyTyping.NameMost)];
            int n = 0;
            for (int i = 0; i < text.Length && n < chars.Length; i++)
            {
                if (!char.IsControl(text[i]))
                {
                    chars[n++] = text[i];
                }
            }
            return new string(chars, 0, n);
        }

        private void CommitTyping()
        {
            if (_view.Typing == null)
            {
                return;
            }
            string typed = _view.Typing;
            _view.Typing = null;
            int index = _typingIndex;
            _typingIndex = -1;
            if (typed.Length == 0 || index < 0 || index >= _layout.ActCount)
            {
                return;
            }
            CompanyAct act = _layout.Acts[index].Act;
            if (_view.TypingKind == CompanyTyping.PlanName)
            {
                act.S2 = typed.Trim();   // a plan's new name rides in S2
                if (act.S2.Length == 0)
                {
                    return;
                }
            }
            else if (int.TryParse(typed, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out int value))
            {
                act.C = value;   // the typed figure rides in C: the director's handlers of a typed field read it before the step's own
            }
            else
            {
                return;
            }
            Run(act);
            _stale = true;
        }

        private static string Digits(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }
            var chars = new char[Math.Min(text.Length, 9)];
            int n = 0;
            for (int i = 0; i < text.Length && n < chars.Length; i++)
            {
                if (text[i] >= '0' && text[i] <= '9')
                {
                    chars[n++] = text[i];
                }
            }
            return new string(chars, 0, n);
        }

        // ------------------------------------------------------------------------------------------------- the host's calls

        public void Focused(int command, int arg)
        {
            if (command == CompanyFace.StepType)
            {
                if (arg + 2 >= 0 && arg + 2 < _layout.ActCount)
                {
                    BeginTyping(arg + 2);
                }
                else
                {
                    _view.Typing = null;
                }
                return;
            }
            // A list follows the cursor, as a game's does: the row under focus is what the page shows.
            if (_view.Question == 0 && command == CompanyFace.Row && SelectRow(arg))
            {
                _stale = true;
                _dirty = true;
            }
            else if (_view.Question == 0 && command == UiOrgTree.First + UiOrgTree.Pick && PickNode(arg, false))
            {
                _stale = true;
                _dirty = true;
            }
        }

        public bool Back()
        {
            if (_view.Typing != null)
            {
                _view.Typing = null;
                _dirty = true;
                return true;
            }
            if (_view.ArmedId != null)
            {
                _view.ArmedId = null;
                _dirty = true;
                return true;
            }
            if (_view.Question != 0)
            {
                _view.Later.Add(_view.Question);   // the card closed without an answer: put off, asked again when the Lead comes round
                _view.Question = 0;
                _view.Choice = 0;
                _stale = true;
                _dirty = true;
                return true;
            }
            if (_view.Sub[_view.Tab] != CompanyView.SubMain)
            {
                _view.Sub[_view.Tab] = CompanyView.SubMain;
                _stale = true;
                _dirty = true;
                return true;
            }
            return false;
        }

        public void Tab(int step)
        {
            CommitTyping();
            if (_view.Question != 0)
            {
                return;
            }
            ShowTab((_view.Tab + step + CompanyView.Tabs) % CompanyView.Tabs);
            _stale = true;
            _dirty = true;
        }

        // F runs the page's main button and P its second: the old app's own keys.
        public bool Act(UiAction action)
        {
            if (action != UiAction.Alt && action != UiAction.Option)
            {
                return false;
            }
            CommitTyping();
            int index = action == UiAction.Alt ? _layout.Main : _layout.Second;
            if (index < 0)
            {
                return false;
            }
            bool done = Act(index);
            if (done)
            {
                _stale = true;
                _dirty = true;
            }
            return done;
        }

        public void Reveal(int list, int index)
        {
            int slot = _layout.SlotOfList(list);
            if (slot >= 0)
            {
                _layout.Reveal[slot] = index;
                _dirty = true;
            }
        }

        public void Wheel(int clip, float notches)
        {
            int slot = _layout.SlotOfClip(clip);
            if (slot >= 0)
            {
                _layout.Offset[slot] = Math.Max(0f, _layout.Offset[slot] - notches * UiTokens.S16);
                _dirty = true;
            }
        }
    }
}
