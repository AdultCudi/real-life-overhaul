using System;
using UnityEngine;

namespace RealLifeOverhaul.GameUi
{
    /// <summary>One entry of a choice as the kit's face of the window reads it: nothing is copied, the entries are read as they stand.</summary>
    public struct MapPickerLine
    {
        public int Id;
        public string Name;       // empty for a note
        public string Detail;     // the smaller line, or a note's words
        public bool Enabled;
        public bool Note;
        public bool HasPlace;
    }

    /// <summary>
    /// The choice's part for the kit's face of its window (Ui/Map): the kit asks, as the choice opens, whether it draws the choice (then the game's copy of a window stays
    /// unseen and unclicked, as a windowless choice's), reads the entries and the words typed, and presses what the window's own entries, search and Cancel do. The map, the
    /// markers, the glide to a pointed place and every owner's pick stay this class's. Costs nothing while closed.
    /// </summary>
    public sealed partial class MapPicker
    {
        /// <summary>Set by the kit: asked as a choice opens, true when it draws the choice itself.</summary>
        public static Func<MapPicker, bool> KitShow;

        /// <summary>Set by the kit: told as a choice it draws closes, by any means.</summary>
        public static Action<MapPicker> KitHide;

        /// <summary>True while the kit draws this choice.</summary>
        public bool KitOn { get; private set; }

        /// <summary>The headline of the choice.</summary>
        public string TitleText => _titleText ?? string.Empty;

        /// <summary>The words typed to narrow the entries.</summary>
        public string Query => _query;

        /// <summary>Counts every filling of the entries, so the kit can tell they changed.</summary>
        public int Fills => _fills;

        /// <summary>The entries filled in now.</summary>
        public int Used => _used;

        /// <summary>The entry at an index as it stands; false past the last.</summary>
        public bool LineAt(int index, out MapPickerLine line)
        {
            line = default;
            if (index < 0 || index >= _used)
            {
                return false;
            }
            Row row = _rows[index];
            line = new MapPickerLine { Id = row.Id, Name = row.Name, Detail = row.Detail, Enabled = row.Enabled, Note = row.Note, HasPlace = row.HasPlace };
            return true;
        }

        /// <summary>The kit's face taken away (the interface switched off while a choice is up): the game's copy of the window is seen and clicked again.</summary>
        public void KitGiveBack()
        {
            if (!KitOn)
            {
                return;
            }
            KitOn = false;
            CanvasGroup group = _root != null ? _root.GetComponent<CanvasGroup>() : null;
            if (group != null && !Windowless)
            {
                group.alpha = 1f;
                group.blocksRaycasts = true;
                group.interactable = true;
            }
        }

        /// <summary>The pointer is on an entry: the map glides to its place after the pause, as for the window's own entry.</summary>
        public void KitPoint(int index) => EntryEnter(index);

        /// <summary>The pointer left an entry.</summary>
        public void KitUnpoint(int index) => EntryExit(index);

        /// <summary>An entry chosen, as its own button does.</summary>
        public void KitPick(int index) => Picked(index);

        /// <summary>The words typed in the search: the entries narrow.</summary>
        public void KitSearch(string words) => Filter(words);

        /// <summary>Cancel: the choice and the map closed, the game's own way out.</summary>
        public void KitClose() => Close();
    }
}
