using System;
using System.Collections.Generic;
using RealLifeOverhaul.GameUi;
using UnityEngine;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// A scene's window (a hearing, a fame or office scene, an interview, a pitch): the feature's <see cref="DeskList"/>
    /// as it always was, its page also kept as the kit reads it (<see cref="SceneList"/>), so the scene stands on the
    /// decision card or the conversation while the RLO interface is on and in the list window as before while it is off.
    /// Costs nothing a frame of its own.
    /// </summary>
    public sealed class SceneDesk
    {
        private readonly DeskList _desk;
        private readonly string _name;
        private SceneList _list;

        /// <param name="name">The window's object name.</param>
        /// <param name="width">In the HUD's units (3,840 across); the game's own list is 775.</param>
        /// <param name="entryHeight">An entry's height; its lines under the name may wrap.</param>
        public SceneDesk(string name, float width, float entryHeight)
        {
            _name = name;
            SceneKit.Name(name);   // before its window can open: the picker leaves it to the scene's kit
            _desk = new DeskList(name, width, entryHeight);
        }

        public bool IsOpen => _desk.IsOpen;

        /// <summary>The window is open with this owner's scene in it.</summary>
        public bool OpenFor(object owner) => _desk.OpenFor(owner);

        /// <summary>The game's window could not be copied: the feature uses its plain way.</summary>
        public bool Failed => _desk.Failed;

        /// <summary>The window for this owner to fill and open, or null if it cannot be made.</summary>
        public SceneList Take(object owner)
        {
            TravelList list = _desk.Take(owner);
            if (list == null)
            {
                return null;
            }
            if (_list == null || !ReferenceEquals(_list.List, list))
            {
                _list = new SceneList(_name, list);   // the window is made again with the city's interface
            }
            return _list;
        }

        /// <summary>Closes the window if open; its owner's closing runs.</summary>
        public void Close() => _desk.Close();

        /// <summary>The window gone with its feature; a later <see cref="Take"/> tries again.</summary>
        public void Destroy(Action<Exception> onError)
        {
            _desk.Destroy(onError);
            SceneKit.Lost(_name);
            _list = null;
        }
    }

    /// <summary>
    /// A scene's list window as its owner fills it, every call passed on to the <see cref="TravelList"/> as before and the
    /// page kept beside it (<see cref="ScenePage"/>): the kit draws that page and answers through the same pick the
    /// window's buttons call, so the scene's rules stay its owner's. The owner may say more of the moment than the window
    /// shows: a moment's place and those present (<see cref="Moment"/>), or who asks the questions (<see cref="Talk"/>).
    /// Costs nothing a frame.
    /// </summary>
    public sealed class SceneList
    {
        internal readonly string Name;
        internal readonly TravelList List;
        internal readonly ScenePage Page = new ScenePage();
        internal readonly List<SceneSaid> Thread = new List<SceneSaid>(16);   // the conversation so far, while the window stays open

        private readonly Action<int> _picked;
        private readonly Action _closed;
        private Action<int> _onPick;
        private Action _onClosed;

        internal SceneList(string name, TravelList list)
        {
            Name = name;
            List = list;
            _picked = Picked;
            _closed = Closed;
        }

        public bool IsOpen => List.IsOpen;

        public bool Alive => List.Alive;

        public int Made => List.Made;

        public RectTransform Window => List.Window;

        /// <summary>The player may not leave: no way out but an answer. Every closing clears it.</summary>
        public bool Locked
        {
            get => List.Locked;
            set
            {
                List.Locked = value;
                if (Page.Locked != value)
                {
                    Page.Locked = value;
                    Page.Stamp++;
                }
            }
        }

        /// <summary>Starts a filling: the headline and the lines under it. The moment is told again for each page.</summary>
        public void Begin(string title, string info)
        {
            Page.Reset(title, info);
            List.Begin(title, info);
        }

        /// <summary>The lines under the headline, as final words.</summary>
        public void Info(string words)
        {
            Page.Info = words;
            Page.Stamp++;
            List.Info(words);
        }

        /// <summary>One answer: its words, a smaller line under them, and the button's locale key.</summary>
        public void Add(int id, string name, string detail, string buttonKey, bool enabled)
        {
            Page.Entries.Add(new SceneEntry { Id = id, Name = name, Detail = detail, Enabled = enabled });
            Page.Stamp++;
            List.Add(id, name, detail, buttonKey, enabled);
        }

        /// <summary>An answer taller than the others, for a detail of several lines.</summary>
        public void Add(int id, string name, string detail, string buttonKey, bool enabled, float height)
        {
            Page.Entries.Add(new SceneEntry { Id = id, Name = name, Detail = detail, Enabled = enabled });
            Page.Stamp++;
            List.Add(id, name, detail, buttonKey, enabled, height);
        }

        /// <summary>
        /// An answer with what it costs: its lines in their colours under its meaning on the kit's card and the time it takes
        /// beside its name; the game's own list says them on a line under the meaning (<see cref="SceneViews.Joined"/>).
        /// </summary>
        public void Add(int id, string name, string detail, string buttonKey, bool enabled, float height, UiConsequence[] lines, string note)
        {
            Page.Entries.Add(new SceneEntry { Id = id, Name = name, Detail = detail, Enabled = enabled, Lines = lines, Note = note });
            Page.Stamp++;
            List.Add(id, name, SceneViews.Joined(detail, lines, note), buttonKey, enabled, height);
        }

        /// <summary>An answer whose line under its name is what the player says by it (a pitch's "Answer 1" over its words): the thread says those words.</summary>
        public void AddAnswer(int id, string name, string words, string buttonKey, bool enabled, float height)
        {
            Page.Entries.Add(new SceneEntry { Id = id, Name = name, Detail = words, Said = words, Enabled = enabled });
            Page.Stamp++;
            List.Add(id, name, words, buttonKey, enabled, height);
        }

        /// <summary>What someone says before the answers.</summary>
        public void AddNote(string name, string detail, float height)
        {
            Page.Notes.Add(new SceneNote { Name = name, Detail = detail });
            Page.Stamp++;
            List.AddNote(name, detail, height);
        }

        /// <summary>
        /// On the RLO interface the notes read as a statement: <paramref name="words"/> over it, its lines (a file's figures
        /// on one right edge, the total under them) and <paramref name="foot"/> under it. The game's own list keeps the notes.
        /// </summary>
        public void Statement(string words, UiLedgerLine[] lines, string foot)
        {
            Page.Statement = lines;
            Page.StatementWords = string.IsNullOrEmpty(words) ? null : words;
            Page.StatementFoot = string.IsNullOrEmpty(foot) ? null : foot;
            Page.Stamp++;
        }

        public float HeightFor(int characters, bool button) => List.HeightFor(characters, button);

        /// <summary>
        /// The page is a moment, on the decision card: its place or time (the lines under the headline, which then are its
        /// caption and not said again), the emblem where no faces are given, and those present. Told after <see cref="Begin"/>.
        /// </summary>
        public void Moment(string kicker, UiIcon emblem, params string[] faces)
        {
            Page.Look = SceneLook.Decision;
            Page.Kicker = string.IsNullOrEmpty(kicker) ? null : kicker;
            Page.Emblem = emblem;
            Page.Faces = faces;
            Page.InfoSaid = false;
            Page.Stamp++;
        }

        /// <summary>
        /// Who each of those present is (the moment's faces, in the same order) and their rings, whose side each is on; the
        /// card then stands them one to a row with their names and roles. Told after <see cref="Moment"/>.
        /// </summary>
        public void Roles(string[] roles, UiRole[] rings)
        {
            Page.Roles = roles;
            Page.Rings = rings;
            Page.Stamp++;
        }

        /// <summary>
        /// The page is questions and answers, in the conversation: who asks or answers (the lines under the headline are
        /// what they say, the progress small under them in a chip) and who they are to the player. Told after <see cref="Begin"/>.
        /// </summary>
        public void Talk(string speaker, string role)
        {
            Page.Look = SceneLook.Talk;
            Page.Speaker = string.IsNullOrEmpty(speaker) ? null : speaker;
            Page.Role = string.IsNullOrEmpty(role) ? null : role;
            Page.InfoSaid = true;
            Page.Stamp++;
        }

        /// <summary>Shows what was filled in. A pick calls <paramref name="onPick"/> with the answer's id; it stays open until <see cref="Close"/>.</summary>
        public void Open(Action<int> onPick, Action onClosed)
        {
            _onPick = onPick;
            _onClosed = onClosed;
            List.Open(_picked, _closed);
            SceneKit.Opened(this);
        }

        public void Close() => List.Close();

        public void ScrollToTop() => List.ScrollToTop();

        /// <summary>Stands the window on another canvas (the game's black cover), or back on the HUD's with null; the kit follows it.</summary>
        public void MoveTo(Transform canvas)
        {
            RectTransform window = List.Window;
            Transform before = window != null ? window.parent : null;
            List.MoveTo(canvas);
            if (window != null && window.parent != before)
            {
                SceneKit.Moved(this);
            }
        }

        /// <summary>The kit's press on an answer: what a click on its button does, through the same pick.</summary>
        internal bool Pick(int id)
        {
            if (!List.IsOpen || _onPick == null || !Offered(id))
            {
                return false;
            }
            UiClone.Safe(() => Picked(id));
            return true;
        }

        private bool Offered(int id)
        {
            for (int i = 0; i < Page.Entries.Count; i++)
            {
                if (Page.Entries[i].Id == id && Page.Entries[i].Enabled)
                {
                    return true;
                }
            }
            return false;
        }

        // An answer given, by the window's button or the kit's. Its owner hears it; where the owner went on to another page,
        // what this one said and the answer join the thread (an answer the owner let pass, too soon after the page came, does not).
        private void Picked(int id)
        {
            int stamp = Page.Stamp;
            var heard = new List<UiMessageModel>(4);
            SceneViews.Said(heard, Page, SceneViews.SaidOf(Page, out string _));
            string answer = SceneViews.Answer(Page, id);
            _onPick?.Invoke(id);
            if (Page.Stamp == stamp || !List.IsOpen)
            {
                return;
            }
            for (int i = 0; i < heard.Count; i++)
            {
                Thread.Add(new SceneSaid { From = UiFrom.Them, Text = heard[i].Text });
            }
            if (answer != null)
            {
                Thread.Add(new SceneSaid { From = UiFrom.Me, Text = answer });
            }
            while (Thread.Count > DialogRules.MostLines)
            {
                Thread.RemoveAt(0);
            }
        }

        private void Closed()
        {
            Thread.Clear();
            Page.Locked = false;   // the window's closing clears its lock
            Page.Stamp++;
            Action closed = _onClosed;
            _onPick = null;
            _onClosed = null;
            closed?.Invoke();
        }
    }
}
