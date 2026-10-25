using System;
using System.Collections.Generic;
using BAModAPI;
using TMPro;
using UI;
using UI.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace RealLifeOverhaul.GameUi
{
    /// <summary>
    /// A group of the mod's own lines in the game's objectives window (TasksUI, top left), made by the game's own call for a
    /// group (TasksUI.SetUpTasksGroup, as its side quests and workout plan make theirs) with its own task entries
    /// (taskEntryTemplate): a headline, then each line's words, a press on the line and the entry's own Set destination
    /// button. The game shows, folds, hides and sizes the window itself: with the full menu or the phone open, in its mini
    /// games and cutscenes, with the interface hidden. Its own headline folds the group to its first line. Nothing runs a
    /// frame: the lines are written only when the owner writes them, and a line whose words did not change is not touched.
    /// </summary>
    public sealed class TaskGroup
    {
        private const string EntryName = "RealLifeTask";   // never a template's name: the game counts the entry as shown

        private readonly IModLogger _logger;
        private readonly string _what;      // what the group is for, in English, for Player.log
        private readonly string _headKey;   // the game's call takes a text key for the headline
        private readonly List<Entry> _entries = new List<Entry>(8);
        private Transform _group;
        private TMP_Text _head;
        private string _headWords;
        private Color _plain;
        private bool _made, _failed;

        private sealed class Entry
        {
            public GameObject Root;
            public TMP_Text Label;
            public Component Words;
            public GameObject Map;
            public string Shown;
            public CellTone Tone = (CellTone)255;
            public bool Mapped;
        }

        /// <param name="what">What the group is for, in English, for Player.log.</param>
        /// <param name="headKey">The headline's text key, given to the game's own call.</param>
        public TaskGroup(IModLogger logger, string what, string headKey)
        {
            _logger = logger;
            _what = what;
            _headKey = headKey;
        }

        /// <summary>A press on a line, by its place in the group (0 first).</summary>
        public Action<int> Pressed;

        /// <summary>A press on a line's Set destination button, by its place in the group.</summary>
        public Action<int> Mapped;

        /// <summary>A press on the group's headline.</summary>
        public Action HeadPressed;

        /// <summary>True while the group is in the game's window and shown.</summary>
        public bool Showing => _group != null && _group.gameObject.activeSelf;

        /// <summary>The lines shown now, for the tests.</summary>
        public int Lines { get; private set; }

        /// <summary>
        /// The group written: its headline and lines (each its words, its tone, and whether it has the Set destination button).
        /// No lines hides it. False when the game's objectives window is not there to hold it (said once in Player.log).
        /// </summary>
        public bool Show(string head, IList<string> words, IList<CellTone> tones, IList<bool> mapped)
        {
            int count = words != null ? words.Count : 0;
            if (count == 0)
            {
                Hide();
                return true;
            }
            TasksUI tasks = Tasks();
            if (tasks == null || !Make(tasks))
            {
                return false;
            }
            bool changed = !_group.gameObject.activeSelf;
            if (!ReferenceEquals(head, _headWords) && head != _headWords)
            {
                _headWords = head;
                UiClone.Words(_head, head);
            }
            for (int i = 0; i < count; i++)
            {
                Entry e = i < _entries.Count ? _entries[i] : New(tasks, i);
                if (e == null)
                {
                    return false;
                }
                changed |= Write(e, words[i], tones != null && i < tones.Count ? tones[i] : CellTone.Plain, mapped != null && i < mapped.Count && mapped[i]);
            }
            for (int i = count; i < _entries.Count; i++)
            {
                if (_entries[i].Root.activeSelf)
                {
                    _entries[i].Root.SetActive(false);
                    changed = true;
                }
            }
            Lines = count;
            if (changed)
            {
                _group.gameObject.SetActive(true);
                tasks.ScheduleUpdateObjectivesHeight();   // the game sizes its window, and opens it when it was folded for having nothing
            }
            return true;
        }

        /// <summary>The group put away while it has nothing to show; it stays in the game's window for the next lines.</summary>
        public void Hide()
        {
            Lines = 0;
            if (_group == null || !_group.gameObject.activeSelf)
            {
                return;
            }
            _group.gameObject.SetActive(false);
            Tasks()?.ScheduleUpdateObjectivesHeight();
        }

        /// <summary>The group taken out of the game's window: the feature is off or going.</summary>
        public void Drop()
        {
            Lines = 0;
            if (_group != null)
            {
                UnityEngine.Object.Destroy(_group.gameObject);
                Tasks()?.ScheduleUpdateObjectivesHeight();
            }
            Forget();
        }

        private static TasksUI Tasks()
        {
            UIs uis = InstanceBehavior<UIs>.Instance;
            return uis != null ? uis.tasksUI : null;
        }

        // The game's window and its group made once a city; again after a load, which destroys the window with the scene.
        private bool Make(TasksUI tasks)
        {
            if (_group != null)
            {
                return true;
            }
            if (_made)
            {
                Forget();   // the scene went with the old group
            }
            if (tasks.taskEntryTemplate == null)
            {
                return Failed("the game's objectives window has no task entry template");
            }
            Transform group = tasks.SetUpTasksGroup(_headKey);
            Transform head = group != null ? group.Find("Headline") : null;
            Transform label = head != null ? head.Find("Label") : null;
            if (label == null)
            {
                if (group != null)
                {
                    UnityEngine.Object.Destroy(group.gameObject);
                }
                return Failed("the game's objectives group has no Headline/Label");
            }
            group.name = "RealLifeTasks " + _what;
            _group = group;
            _head = label.GetComponent<TMP_Text>();
            if (_head != null)
            {
                _head.raycastTarget = true;   // the headline is pressed to fold the group: its words take the press
            }
            Button press = head.GetComponent<Button>();
            if (press == null)
            {
                press = head.gameObject.AddComponent<Button>();   // where the game gave the headline no press of its own: added on this group alone
            }
            press.transition = Selectable.Transition.None;
            press.targetGraphic = _head;
            UiClone.Rewire(head, () => HeadPressed?.Invoke());
            _headWords = null;
            _made = true;
            _group.gameObject.SetActive(false);
            if (Meta.Dev.Verbose)
            {
                _logger?.Info("Game UI check (objectives group): " + _what + " has a group of its own in the game's objectives window, made by the game's own call: PASS.");
            }
            return true;
        }

        private Entry New(TasksUI tasks, int index)
        {
            Transform root = UnityEngine.Object.Instantiate(tasks.taskEntryTemplate, _group);
            root.name = EntryName + index;
            Transform label = root.Find("Label");
            if (label == null)
            {
                UnityEngine.Object.Destroy(root.gameObject);
                Failed("the game's task entry has no Label");
                return null;
            }
            Off(root, "Checkmark");     // a line of what goes on, not a box to tick: as the game's own time entry has none
            Off(root, "CloseButton");
            var e = new Entry { Root = root.gameObject, Label = label.GetComponent<TMP_Text>(), Words = label };
            if (e.Label != null && _entries.Count == 0)
            {
                _plain = e.Label.color;
            }
            Transform map = root.Find("DestinationButton");
            e.Map = map != null ? map.gameObject : null;
            Button line = UiClone.Rewire(root, () => Pressed?.Invoke(index));
            if (line != null)
            {
                line.interactable = true;
            }
            if (map != null)
            {
                UiClone.Rewire(map, () => Mapped?.Invoke(index));
            }
            _entries.Add(e);
            return e;
        }

        // True if anything about the line changed.
        private bool Write(Entry e, string words, CellTone tone, bool mapped)
        {
            bool changed = false;
            if (!e.Root.activeSelf)
            {
                e.Root.SetActive(true);
                changed = true;
            }
            if (words != e.Shown)
            {
                e.Shown = words;
                UiClone.Words(e.Words, words);
                changed = true;
            }
            if (tone != e.Tone && e.Label != null)
            {
                e.Tone = tone;
                e.Label.color = Colour(tone);
            }
            if (mapped != e.Mapped || e.Map != null && e.Map.activeSelf != mapped)
            {
                e.Mapped = mapped;
                if (e.Map != null)
                {
                    e.Map.SetActive(mapped);
                }
            }
            return changed;
        }

        // The game's own priorities: yellow for medium, red for high, as its business alerts are written.
        private Color Colour(CellTone tone)
        {
            GlobalReferences references = InstanceBehavior<GlobalReferences>.Instance;
            if (references == null || references.colors == null)
            {
                return _plain;
            }
            switch (tone)
            {
                case CellTone.Yellow: return references.colors.yellow;
                case CellTone.Red: return references.colors.red;
                case CellTone.Green: return references.colors.green;
                default: return _plain;
            }
        }

        private static void Off(Transform root, string child)
        {
            Transform t = root.Find(child);
            if (t != null)
            {
                t.gameObject.SetActive(false);
            }
        }

        private void Forget()
        {
            _group = null;
            _head = null;
            _headWords = null;
            _entries.Clear();
            _made = false;
        }

        private bool Failed(string why)
        {
            if (!_failed)
            {
                _failed = true;
                _logger?.Warn("Game UI: " + why + ", so " + _what + " are not shown.");
            }
            return false;
        }
    }
}
