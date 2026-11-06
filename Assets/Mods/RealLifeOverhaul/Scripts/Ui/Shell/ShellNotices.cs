using System;
using System.Collections.Generic;
using Localizor;
using RealLifeOverhaul.GameUi;
using TMPro;
using UI;
using UI.Notification;
using UnityEngine;
using UnityEngine.UI;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The game's notices on the kit: its toasts as notice cards where its own stack stands, and its list of notices as
    /// a panel rising from the status bar, each through the host and with its own switch. The game still makes, times,
    /// sounds and saves every notice (Going Public's relay and the mod's own look included); the kit draws them from the
    /// game's entries after their code ran, hides only the entries' own leaves, and a click on a card runs the entry's own
    /// button. Which kind each toast is comes from its key as the game shows it (Notifications.onShow, heard after the
    /// game's and NoticeLook's), so nothing new is saved. The stack's object is on all the time, so the host shows the cards the
    /// moment it binds (IUiGameStands) and each toast's pieces go unseen the moment the game makes it. Cost: nothing while no notice
    /// shows; while one does, a look at the stack four times a second and a layout when it changed.
    /// </summary>
    public static class ShellNotices
    {
        private static readonly UiGameHost Cards = new UiGameHost(new CardsBox());
        private static readonly UiGameHost List = new UiGameHost(new ListBox());
        private static readonly Dictionary<int, UiIcon> Kinds = new Dictionary<int, UiIcon>(16);   // a toast's object by its id: its kind's glyph
        private static Action<NotificationType, string, Dictionary<string, string>, float, string, Action, bool, bool> _onShow;
        private static NotificationsUI _toasts;
        private static NoticeCardsScreen _live;   // the cards' screen while the kit draws them: told of each toast as the game makes it
        private static bool _joined, _cardsSaid, _lateSaid;

        /// <summary>Once a second while the interface is on: each host put on while its switch is on.</summary>
        public static void Install()
        {
            Join();
            Switch(Cards);
            Switch(List);
        }

        /// <summary>The game's own notices again.</summary>
        public static void Remove(string why)
        {
            Cards.Remove(why);
            List.Remove(why);
            _live = null;
            Leave();
        }

        /// <summary>A new city: the stack is looked for again.</summary>
        public static void Rearm()
        {
            _toasts = null;
            _live = null;
            Kinds.Clear();
            Cards.Rearm();
            List.Rearm();
        }

        /// <summary>The notices' states for the interface's line at load.</summary>
        public static string States() => "notice cards (" + Cards.State + "), notice list (" + List.State + ")";

        /// <summary>The glyph of a notice's kind: the mod's by its key (NoticeKinds), the game's by its type.</summary>
        public static UiIcon Glyph(string key, NotificationType type)
        {
            switch (NoticeKinds.Of(key))
            {
                case NoticeKind.Road: return UiIcon.Car;
                case NoticeKind.Police: return UiIcon.Scales;
                case NoticeKind.Health: return UiIcon.Heartbeat;
                case NoticeKind.Money: return UiIcon.Coin;
                case NoticeKind.Property: return UiIcon.Home;
                case NoticeKind.Work: return UiIcon.Briefcase;
                case NoticeKind.Business: return UiIcon.Store;
                case NoticeKind.Tech: return UiIcon.Cpu;
                case NoticeKind.News: return UiIcon.Newspaper;
                case NoticeKind.People: return UiIcon.People;
                case NoticeKind.Other: return UiIcon.Spark;
            }
            switch (type)
            {
                case NotificationType.Success: return UiIcon.Check;
                case NotificationType.Info: return UiIcon.Info;
                default: return UiIcon.Warning;
            }
        }

        private static void Switch(UiGameHost host)
        {
            if (UiScreenSwitches.On(host.Id))
            {
                host.Install();
            }
            else
            {
                host.Remove("its switch is off");
            }
        }

        private static NotificationsUI Toasts()
        {
            if (_toasts == null)
            {
                UIs uis = InstanceBehavior<UIs>.Instance;
                Transform canvases = uis != null && uis.notificationsListUI != null ? uis.notificationsListUI.transform.parent : null;
                Transform maker = canvases != null ? canvases.Find("NotificationsUI") : null;   // beside the list, as NoticeLook finds it
                _toasts = maker != null ? maker.GetComponent<NotificationsUI>() : null;
            }
            return _toasts;
        }

        // Heard after the game made the toast: its newest entry is this notice, and its key says its kind.
        private static void Shown(NotificationType type, string key, Dictionary<string, string> data, float seconds, string duplicate, Action click, bool sound, bool track)
        {
            NotificationsUI toasts = Toasts();
            Transform stack = toasts != null ? toasts.container : null;
            if (stack == null || stack.childCount == 0)
            {
                return;
            }
            Transform newest = stack.GetChild(stack.childCount - 1);
            if (Kinds.Count > 64)
            {
                Kinds.Clear();   // the stack keeps a few at once: an old id is never asked again
            }
            int id = newest.gameObject.GetInstanceID();
            if (Kinds.ContainsKey(id))
            {
                return;   // a repeat of a notice the stack shows already: the game made no new entry
            }
            Kinds[id] = Glyph(key, type);
            if (_live != null)
            {
                _live.Arrived(newest);   // its own picture goes unseen now, before the game's fade shows any of it
            }
            if (Meta.Dev.Verbose && !_lateSaid && Cards.Root != null && !Cards.Showing)
            {
                _lateSaid = true;
                UiDirector.Logger?.Warn("Notice check (cards): a toast was made and the kit's cards are bound but not showing (" + Cards.State + "), so the game's own toast is seen: FAIL.");
            }
        }

        private static void Join()
        {
            if (_joined)
            {
                return;
            }
            _onShow = _onShow ?? Guard.Wrap<NotificationType, string, Dictionary<string, string>, float, string, Action, bool, bool>(Shown);
            Notifications.onShow = (Action<NotificationType, string, Dictionary<string, string>, float, string, Action, bool, bool>)Delegate.Combine(Notifications.onShow, _onShow);
            _joined = true;
        }

        private static void Leave()
        {
            if (!_joined)
            {
                return;
            }
            Notifications.onShow = (Action<NotificationType, string, Dictionary<string, string>, float, string, Action, bool, bool>)Delegate.Remove(Notifications.onShow, _onShow);
            _joined = false;
        }

        // ------------------------------------------------------------------------------------------------- the cards

        private sealed class CardsBox : IUiGameScreen, IUiGameStands, IUiGameOffers
        {
            private UiConceal _conceal;   // the host's, so the entries' pieces are given back as the host gives its own back

            public string Id => "notice_cards";

            public string Name => "the notices";

            public string Title => null;

            public UiIcon Icon => UiIcon.Bell;

            public UiGameKind Kind => UiGameKind.Overlay;

            public Component Find()
            {
                NotificationsUI toasts = Toasts();
                return toasts != null ? toasts.container : null;
            }

            public string Check(Component found) => null;

            public void Conceal(Component found, UiConceal conceal)
            {
                // The entries come and go: the screen hides each one's leaves as it arrives (NoticeCardsScreen.Arrived and Look), in the host's own hold.
                _conceal = conceal;
            }

            public IUiScreen Make(Component found) => _live = new NoticeCardsScreen(found.transform, _conceal);
        }

        /// <summary>The toasts the game shows now, as notice cards; a click runs the entry's own button.</summary>
        private sealed class NoticeCardsScreen : IUiScreen
        {
            private const float Every = 0.25f;   // seconds between looks at the stack
            private readonly Transform _stack;
            private readonly UiConceal _hidden;   // the host's hold on the game's pieces: given back with them when the cards go or the switch is off
            private readonly UiNoticeCardsModel _m = new UiNoticeCardsModel();
            private readonly List<Transform> _entries = new List<Transform>(8);
            private readonly Vector3[] _corners = new Vector3[4];
            private long _seen;
            private float _next;
            private bool _dirty = true;
            private float _frameW, _frameH;   // the layout's size, last laid: the minimap's card is read into it

            public NoticeCardsScreen(Transform stack, UiConceal hold)
            {
                _stack = stack;
                _hidden = hold;
                _m.Cards = new UiNotice[UiNotices.MostCards * 2];
            }

            public string Id => "notice_cards";

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
                _frameW = frame.Width;
                _frameH = frame.Height;
                Look(frame);
                UiNotices.LayCards(s, _m, frame, dark, keys);
                _dirty = false;
                Said();
            }

            public bool Press(int command, int arg)
            {
                int at = UiNoticeRules.EntryFor(command, arg, _entries.Count);
                if (at < 0 || _entries[at] == null)
                {
                    return false;
                }
                _dirty = true;
                return UiWidgets.Press(_entries[at].GetComponent<Button>());
            }

            /// <summary>The game just made this entry: its pieces go unseen at once and the cards are laid out next frame.</summary>
            public void Arrived(Transform entry)
            {
                if (_hidden.Hiding)
                {
                    Take(entry);   // before the kit shows, or after it went, Look takes it when it does
                    _dirty = true;
                }
            }

            public void Focused(int command, int arg)
            {
            }

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
            }

            // The entries showing, oldest first, each one's leaves hidden as it arrives; where the game's stack starts (the
            // mod's panels push it down), in the kit's px.
            private void Look(UiFrame frame)
            {
                _entries.Clear();
                _hidden.Prune();
                for (int i = 0; i < _stack.childCount; i++)
                {
                    Transform entry = _stack.GetChild(i);
                    if (!UiNoticeRules.IsToast(entry.gameObject.activeSelf, entry.Find("Text") != null))
                    {
                        continue;   // a template, or another mod's own child of the stack
                    }
                    Take(entry);
                    _entries.Add(entry);
                }
                _hidden.Hide();
                if (_m.Cards.Length < _entries.Count)
                {
                    _m.Cards = new UiNotice[_entries.Count + 4];
                }
                for (int i = 0; i < _entries.Count; i++)
                {
                    Transform entry = _entries[i];
                    UiIcon glyph = Kinds.TryGetValue(entry.gameObject.GetInstanceID(), out UiIcon g) ? g : UiIcon.Info;
                    _m.Cards[i] = new UiNotice
                    {
                        Icon = glyph, Header = UiText.T(KindKey(glyph)), Body = UiWidgets.Text(Own(entry.Find("Text"))), Age = UiWidgets.Text(Own(entry.Find("Text/Timestamp")))
                    };
                }
                _m.CardCount = _entries.Count;
                UiMinimapBox.Read(frame.Width, frame.Height, out _m.AvoidX, out _m.AvoidY, out _m.AvoidW, out _m.AvoidH);
                var rect = _stack as RectTransform;
                if (rect != null)
                {
                    rect.GetWorldCorners(_corners);
                    float top = Screen.height - _corners[1].y;   // the stack's top in screen pixels, from the top
                    _m.Top = Math.Max(UiTokens.S4, top / Math.Max(1f, Screen.height) * frame.Height);
                }
            }

            // One toast's pieces unseen and untouchable: each child through the hold, its own picture by its renderer.
            private void Take(Transform entry)
            {
                for (int c = 0; c < entry.childCount; c++)
                {
                    _hidden.Add(entry.GetChild(c));
                }
                // Deaf: the entry's button tints this picture on hover, which would show it again behind the card.
                _hidden.AddDeaf(entry.GetComponent<Graphic>());
            }

            // The once-a-session line (developer diagnostics): the first toast drawn as a card, and how many of the game's entries are unseen.
            private void Said()
            {
                if (_cardsSaid || !Meta.Dev.Verbose || _entries.Count == 0)
                {
                    return;
                }
                _cardsSaid = true;
                int hidden = 0, pieces = 0;
                for (int i = 0; i < _entries.Count; i++)
                {
                    int n = Unseen(_entries[i]);
                    hidden += n >= 0 ? 1 : 0;
                    pieces += Math.Max(0, n);
                }
                int drawn = Math.Min(_entries.Count, UiNotices.MostCards);
                string wrong = UiNoticeRules.Wrong(_entries.Count, hidden, drawn, Cards.Showing);
                UiDirector.Logger?.Info("Notice check (cards): " + _entries.Count + " toast(s) in the game's stack drawn as " + drawn
                    + " notice card(s) by the kit, the game's own entry hidden in " + hidden + " of " + _entries.Count + " (" + pieces + " pieces unseen and untouchable)"
                    + (wrong == null ? ": PASS." : ": FAIL (" + wrong + ")."));
            }

            // The pieces of an entry that are unseen and not pointed at, or -1 when any is still seen.
            private static int Unseen(Transform entry)
            {
                int n = 0;
                for (int c = 0; c < entry.childCount; c++)
                {
                    Transform child = entry.GetChild(c);
                    if (UiHostRules.Shared(child.name))
                    {
                        continue;
                    }
                    CanvasGroup g = child.GetComponent<CanvasGroup>();
                    if (g == null || g.alpha > 0.001f || g.blocksRaycasts)
                    {
                        return -1;
                    }
                    n++;
                }
                Graphic own = entry.GetComponent<Graphic>();
                if (own != null)
                {
                    if (own.canvasRenderer.GetAlpha() > 0.001f)
                    {
                        return -1;
                    }
                    n++;
                }
                return n;
            }

            private long Signature()
            {
                UiMinimapBox.Read(_frameW, _frameH, out float cx, out float cy, out float cw, out float ch);   // the card moved: the cards are laid again beside it
                long sig = _stack.childCount * 31 + UiMinimapBox.Stamp(cx, cy, cw, ch);
                for (int i = 0; i < _stack.childCount; i++)
                {
                    Transform t = _stack.GetChild(i);
                    sig = sig * 31 + t.gameObject.GetInstanceID() + (t.gameObject.activeSelf ? 1 : 0);
                }
                return sig;
            }

            private static TMP_Text Own(Transform t) => t != null ? t.GetComponent<TMP_Text>() : null;
        }

        // A notice's header: its kind's name, by the glyph that stands for it.
        private static string KindKey(UiIcon glyph)
        {
            switch (glyph)
            {
                case UiIcon.Car: return "reallife_ui_notice_road";
                case UiIcon.Scales: return "reallife_ui_notice_police";
                case UiIcon.Heartbeat: return "reallife_ui_notice_health";
                case UiIcon.Coin: return "reallife_ui_notice_money";
                case UiIcon.Home: return "reallife_ui_notice_property";
                case UiIcon.Briefcase: return "reallife_ui_notice_work";
                case UiIcon.Store: return "reallife_ui_notice_business";
                case UiIcon.Cpu: return "reallife_ui_notice_tech";
                case UiIcon.Newspaper: return "reallife_ui_notice_news";
                case UiIcon.People: return "reallife_ui_notice_people";
                case UiIcon.Spark: return "reallife_ui_notice_other";
                case UiIcon.Check: return "reallife_ui_notice_done";
                case UiIcon.Warning: return "reallife_ui_notice_warning";
                default: return "reallife_ui_notice_info";
            }
        }

        // ------------------------------------------------------------------------------------------------- the list

        private sealed class ListBox : IUiGameScreen
        {
            public string Id => "notice_list";

            public string Name => "the list of notices";

            public string Title => UiText.T("reallife_ui_notice_list");

            public UiIcon Icon => UiIcon.Bell;

            public UiGameKind Kind => UiGameKind.Box;

            public Component Find()
            {
                UIs uis = InstanceBehavior<UIs>.Instance;
                var panel = uis != null && uis.notificationsListUI != null ? GameContract.NoticePanel.Field?.GetValue(uis.notificationsListUI) as GameObject : null;
                return panel != null ? panel.transform : null;
            }

            public string Check(Component found) => null;

            public void Conceal(Component found, UiConceal conceal)
            {
                Transform panel = found.transform;
                for (int i = 0; i < panel.childCount; i++)
                {
                    conceal.Add(panel.GetChild(i));
                }
                conceal.Add(panel.GetComponent<Graphic>());
            }

            public IUiScreen Make(Component found) => new NoticeListScreen();
        }

        /// <summary>Every notice the game kept, newest first, from the save as the game's own list reads it; Clear all its own button.</summary>
        private sealed class NoticeListScreen : IUiScreen
        {
            private readonly UiNoticeListModel _m = new UiNoticeListModel();
            private readonly List<Notification> _all = new List<Notification>(64);
            private int _count = -1, _fit = 3;
            private bool _dirty = true;

            public string Id => "notice_list";

            public bool Dirty => _dirty || (SaveGameManager.Current != null && SaveGameManager.Current.notifications.Count != _count);

            public void Lay(UiScene s, UiFrame frame, bool dark, IUiKeys keys)
            {
                _fit = UiNotices.RowsFit(frame);
                Build();
                UiNotices.LayList(s, _m, frame, dark, keys);
                _dirty = false;
            }

            public bool Press(int command, int arg)
            {
                if (command != UiNotices.ClearCommand)
                {
                    return false;
                }
                _dirty = true;
                UIs uis = InstanceBehavior<UIs>.Instance;
                return UiWidgets.Press(uis != null ? GameContract.NoticeClear.Field?.GetValue(uis.notificationsListUI) as Button : null);   // the game asks first, in its own box
            }

            public void Focused(int command, int arg)
            {
            }

            public bool Back()
            {
                UIs uis = InstanceBehavior<UIs>.Instance;
                if (uis == null || uis.notificationsListUI == null)
                {
                    return false;
                }
                uis.notificationsListUI.Toggle(false);
                return true;
            }

            public void Tab(int step)
            {
                Move(step * _fit);
            }

            public bool Act(UiAction action) => action == UiAction.Option && Press(UiNotices.ClearCommand, 0);

            public void Reveal(int list, int index)
            {
            }

            public void Wheel(int clip, float notches)
            {
                Move(-(int)Math.Round(notches));
            }

            private void Move(int rows)
            {
                int next = Math.Max(0, Math.Min(Math.Max(0, _m.RowCount - _fit), _m.First + rows));
                if (next != _m.First)
                {
                    _m.First = next;
                    _dirty = true;
                }
            }

            private void Build()
            {
                _all.Clear();
                GameInstance save = SaveGameManager.Current;
                if (save != null)
                {
                    foreach (Notification n in save.notifications)
                    {
                        _all.Add(n);
                    }
                    _count = save.notifications.Count;
                }
                _all.Reverse();   // newest first, as the game's list shows them
                if (_m.Rows.Length < _all.Count)
                {
                    _m.Rows = new UiNotice[_all.Count + 16];
                }
                for (int i = 0; i < _all.Count; i++)
                {
                    Notification n = _all[i];
                    UiIcon glyph = Glyph(n.key, n.type);
                    _m.Rows[i] = new UiNotice
                    {
                        Icon = glyph, Header = UiText.T(KindKey(glyph)), Body = Plain(LocalizorManager.GetLocalization(n.key, (object)n.notificationData)),
                        Age = LocalizorManager.GetLocalization("timestamp_full", new { day = n.date.Day, time = n.date.Hour.ToString("00") + ":" + ((int)n.date.Minute).ToString("00") })
                    };
                }
                _m.RowCount = _all.Count;
                _m.First = Math.Max(0, Math.Min(_m.First, _m.RowCount - _fit));
                _m.Title = UiText.T("reallife_ui_notice_list");
                _m.Empty = UiText.T("reallife_ui_notice_none");
                _m.ClearLabel = UiText.T("reallife_ui_notice_clear");
                _m.CanClear = _all.Count > 0;
                _m.Prompts = new[]
                {
                    new UiPrompt { Action = UiAction.TabPrev, Label = UiText.T("reallife_ui_prompt_page") },
                    new UiPrompt { Action = UiAction.Back, Label = UiText.T("reallife_ui_prompt_back") },
                };
                _m.PromptCount = 2;
            }

            private static string Plain(string text)
            {
                if (string.IsNullOrEmpty(text) || text.IndexOf('<') < 0)
                {
                    return text;
                }
                var sb = new System.Text.StringBuilder(text.Length);
                bool tag = false;
                foreach (char ch in text)
                {
                    tag = ch == '<' || (tag && ch != '>');
                    if (!tag && ch != '>')
                    {
                        sb.Append(ch);
                    }
                }
                return sb.ToString();
            }
        }
    }
}
