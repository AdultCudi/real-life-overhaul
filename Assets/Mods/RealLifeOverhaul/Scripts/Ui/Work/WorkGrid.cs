using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// A tab laid as a catalogue (UiCatalogue): the rows as cards in a grid with their glyph, name, figure and state, the chosen one's facts
    /// and buttons beside it, and the filter chips above. For courses and for things on sale. Game-free.
    /// </summary>
    public static class WorkGrid
    {
        public static UiCatalogueModel Model(IList<WorkRow> shown, int chosen, UtilitiesWords w, string kicker, out WorkDo[] acts)
        {
            var m = new UiCatalogueModel { Cards = new UiCatalogueCard[shown.Count], CardCount = shown.Count, Chosen = chosen, EmptyIcon = UiIcon.Search };
            for (int i = 0; i < shown.Count; i++)
            {
                WorkRow r = shown[i];
                m.Cards[i] = new UiCatalogueCard { Icon = r.Icon, Name = r.Cells.Length > 0 ? r.Cells[0] : string.Empty, Figure = r.Cells.Length > 0 ? r.Cells[r.Cells.Length - 1] : null,
                    State = r.Status, StateTone = r.Tone };
            }
            WorkRow c = chosen >= 0 && chosen < shown.Count ? shown[chosen] : null;
            acts = new WorkDo[c != null ? c.Acts.Count : 0];
            if (c != null)
            {
                m.DetailKicker = kicker;
                m.DetailLabels = new string[c.Facts.Count];
                m.DetailValues = new string[c.Facts.Count];
                for (int i = 0; i < c.Facts.Count; i++)
                {
                    m.DetailLabels[i] = c.Facts[i].Key;
                    m.DetailValues[i] = c.Facts[i].Value;
                }
                m.DetailNote = c.Detail;
                m.Actions = new UiActionModel[c.Acts.Count];
                for (int i = 0; i < c.Acts.Count; i++)
                {
                    WorkAct a = c.Acts[i];
                    int k = c.Acts.Count - 1 - i;   // the catalogue draws the main button last
                    acts[k] = WorkBuilder.Do(a.Kind, a.Arg != 0 ? a.Arg : c.Id, a.Why, a.Opens);
                    m.Actions[k] = new UiActionModel { Label = a.Label, Kind = i == 0 ? UiButtonKind.Primary : UiButtonKind.Secondary, Icon = a.Icon,
                        Key = i == 0 ? UiAction.Alt : i == 1 ? UiAction.Option : UiAction.Count, Command = UiCatalogue.First + UiCatalogue.Action, Arg = k, Disabled = a.Why != null };
                }
            }
            return m;
        }

        public static void Lay(UiScene s, UiAt at, UiCatalogueModel m, UiAppModel app, float x, float y, float w, float bottom, IUiKeys keys)
        {
            float cx = x;
            for (int i = 0; i < app.Actions.Length; i++)
            {
                UiActionModel a = app.Actions[i];
                if (a.Kind != UiButtonKind.Chip)
                {
                    continue;
                }
                float bw = UiDraw.ButtonWidth(a.Kind, a.Label, a.Icon, a.Key, keys);
                UiDraw.Button(s, at, cx, y, bw, UiTokens.Control, a.Kind, a.Label, a.Icon, a.Key, keys, a.Command, a.Arg, a.Disabled);
                cx += bw + UiTokens.S3;
            }
            if (cx > x)
            {
                y += UiTokens.Control + UiTokens.S4;
            }
            UiCatalogue.Lay(s, at, m, x, y, w, bottom - y, keys, out int _);
        }
    }
}
