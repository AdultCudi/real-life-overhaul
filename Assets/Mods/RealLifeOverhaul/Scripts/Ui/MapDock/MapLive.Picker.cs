using System;
using System.Collections.Generic;
using RealLifeOverhaul.GameUi;

namespace RealLifeOverhaul.Ui
{
    /// <summary>The picker's face of the live dock: the entries of one of the mod's choices of a place read into the dock's list, pressed through the choice's own pick.</summary>
    internal sealed partial class MapLive
    {
        private readonly List<int> _pickerRows = new List<int>(64);   // each listed row's entry in the choice, in step with the rows

        private void BuildPicker()
        {
            MapPanelModel m = _panel;
            m.Page = MapPage.Picker;
            m.Title = string.IsNullOrWhiteSpace(_picker.TitleText) ? UiText.T("reallife_map_choose_title") : _picker.TitleText;
            m.Icon = UiIcon.MapPin;
            m.Note = null;
            m.Rail = new MapRailItem[0];
            m.RailShown = -1;
            m.SearchPlaceholder = UiText.T("citymap_map_search_placeholder");
            m.Search = _search;
            m.Results = new MapResult[0];
            m.ResultsNone = null;
            m.Chips = new MapChip[0];
            m.ShowAll = false;
            m.Picked = false;
            _rows.Clear();
            _pickerRows.Clear();
            string query = _picker.Query;
            for (int i = 0; _picker.LineAt(i, out MapPickerLine line); i++)
            {
                if (line.Note)
                {
                    m.Note = m.Note ?? Clean(line.Detail);   // the owner's sentence about the choice: the first, under the strip
                    continue;
                }
                if (query.Length > 0 && line.Name.IndexOf(query, StringComparison.CurrentCultureIgnoreCase) < 0)
                {
                    continue;
                }
                _rows.Add(new MapRow
                {
                    Kind = MapRowKind.Choice, Name = line.Name, Note = string.IsNullOrWhiteSpace(line.Detail) ? null : Clean(line.Detail), Icon = line.HasPlace ? UiIcon.MapPin : UiIcon.Cube,
                    Off = !line.Enabled
                });
                _pickerRows.Add(i);
            }
            m.Rows = _rows.ToArray();
            m.Empty = _rows.Count == 0 ? UiText.T("reallife_map_no_place") : null;
            m.FooterLabel = UiText.T("common_cancel");
            m.Prompts = new MapPrompts(Words).Filters;
            m.PromptCount = m.Prompts.Length;
        }

        // The owner's words on one line: a new line in a choice's detail is a space on a row.
        private static string Clean(string words) => words.Replace("\r", " ").Replace("\n", " ").Trim();
    }
}
