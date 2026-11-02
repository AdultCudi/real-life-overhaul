using System;

namespace RealLifeOverhaul.Ui
{
    /// <summary>A piece of a uniform or one of its colours: the game's own picture by its key, and whether it is the one worn.</summary>
    public struct UiUniformPiece
    {
        public string Picture;   // a picture's key the game resolves (UiPictures), or null for the glyph
        public bool Chosen;
        public bool Sport;       // sports wear, as the game marks it
    }

    /// <summary>
    /// The uniform presets' editor as the kit draws it beside the game's own 3D preview of the figure: the presets as a
    /// list with the chosen one's name, copy and delete, the parts of a uniform as tabs (and a part's own kinds under
    /// them), the pieces of the part shown as the game's own pictures in a grid, its colours as swatches in a palette
    /// its field opens, the figure's sex, and Save. Plain data; the screen fills it from the game's own editor after the
    /// game filled it.
    /// </summary>
    public sealed class UiUniformsModel
    {
        public string Title;
        public string[] Presets = new string[0];
        public int PresetCount;
        public int Preset = -1;                    // the chosen preset, or -1
        public string Name;                        // the chosen preset's name as its own field holds it
        public bool NameLocked;
        public bool CanCopy, CanDelete, CanNew = true, CanSave;
        public string[] Parts = new string[0];     // the parts of a uniform, in the game's order
        public int Part = -1;
        public string[] Kinds = new string[0];     // the shown part's own kinds (a top and what goes over it), or none
        public int Kind = -1;
        public UiUniformPiece[] Pieces = new UiUniformPiece[0];
        public int PieceCount;
        public UiUniformPiece[] Colours = new UiUniformPiece[0];
        public int ColourCount;
        public bool Female;
        public bool ColoursOpen;                   // the worn piece's colours open as a palette over the catalogue
        public float PiecesOffset, PresetsOffset;
        public float StageX, StageY, StageW, StageH;   // px: where the game's own 3D preview stands; none wide: the screen's right
        public bool StandIn;                       // a sample's stand in for the game's 3D preview, which only the game draws
        public UiPrompt[] Prompts = new UiPrompt[0];
        public int PromptCount;
        public UiWords Words;                      // the editor's own labels by key, with their English when there is no lookup

        // What the last layout made, for the screen's wheel and reveal.
        public short PiecesClip = -1, PresetsClip = -1, PiecesList = -1, PresetsList = -1;
        public int PieceColumns = 1;
        public float PiecesRoom, PresetsRoom, PiecesContent, PresetsContent;
    }

    /// <summary>
    /// The uniform presets' editor: one panel at the left of the game's own 3D preview of the figure (which stays the
    /// game's, drawn where the game draws it, and turns as the game turns it when it is dragged), holding the presets'
    /// list and the catalogue of the shown part beside it, Save at its foot. Commands: <see cref="Preset"/> (its index),
    /// <see cref="Copy"/>, <see cref="Delete"/>, <see cref="NewPreset"/>, <see cref="Name"/> (typing), <see cref="Part"/>
    /// and <see cref="Kind"/> (their index), <see cref="Piece"/> and <see cref="Colour"/> (their index), <see cref="Colours"/>
    /// (the palette's field) and <see cref="ColoursDone"/>, <see cref="Sex"/> (0 male, 1 female), <see cref="Save"/> and
    /// <see cref="Stage"/> (the preview, dragged). Game-free. Cost: a layout a change, the cards in view only.
    /// </summary>
    public static class UiUniforms
    {
        public const int Preset = 1, Copy = 2, Delete = 3, NewPreset = 4, Name = 5, Part = 6, Kind = 7, Piece = 8, Colour = 9, Sex = 10, Save = 11, Stage = 12,
            Colours = 13, ColoursDone = 14;

        /// <summary>Every command the editor answers, for the checks' list of known targets.</summary>
        public static readonly int[] Commands = { Preset, Copy, Delete, NewPreset, Name, Part, Kind, Piece, Colour, Sex, Save, Stage, Colours, ColoursDone };

        private const float PresetsShare = 0.3f, LeastPresets = 240f, MostPresets = 320f;   // px: the presets' column
        private const float Card = 144f;         // px: a piece's card at the least, so the game's pictures read and a screen's targets stay few
        private const float Swatch = 40f;        // px: a colour's swatch
        private const float ColourField = 104f;  // px: the colour's field: the worn colour's swatch and the chevron that opens the rest
        private const float LeastPanel = 720f;   // px: the panel's narrowest, the preview's left edge or not
        private const float StageShare = 0.34f;  // of the width, the preview's room when the game's own is not measured

        /// <summary>Lays the editor out over the whole screen, the game's preview left clear.</summary>
        public static void Lay(UiScene s, UiUniformsModel m, UiFrame frame, bool dark, IUiKeys keys)
        {
            s.Begin("uniforms", frame.Width, frame.Height, dark);
            s.Floats = true;   // a panel docked over the game's own scene, its 3D figure at the right: never meant to fill the screen
            StageBox(m, frame, out float sx, out float sy, out float sw, out float sh);
            float px = UiTokens.S16;
            float pw = UiDraw.SnapDown(Math.Min(frame.Width - px * 2f, Math.Max(LeastPanel, sx - UiTokens.S6 - px)));
            UiAt at = UiAt.Screen;
            if (m.StandIn)
            {
                // Where the game's figure stands, in its place's quiet colour: no frame, as the game's own scene has none.
                int stand = UiDraw.Box(s, at, sx, sy, sw, sh, UiRole.Panel, UiRole.None, UiTokens.RadiusPanel, UiFlags.Decor);
                float g = UiDraw.Snap(Math.Min(sw, sh) * 0.4f);
                UiDraw.Icon(s, at.In(stand), sx + (sw - g) / 2f, sy + (sh - g) / 2f, g, UiIcon.Person, UiRole.Text2, UiFlags.Middle | UiFlags.Centre, true);
            }
            int panel = UiDraw.Box(s, at, px, sy, pw, sh, UiRole.Panel, UiRole.None, UiTokens.RadiusPanel, UiFlags.Panel);
            UiAt on = at.In(panel);
            float y = UiDraw.TitleStrip(s, on, px, sy, pw, UiIcon.PaintBrush, m.Title, true);
            float bx = px + UiTokens.S5, by = y + UiTokens.S5, bw = pw - UiTokens.S5 * 2f, bottom = sy + sh - UiTokens.S5;
            float cw = UiDraw.Snap(Math.Min(MostPresets, Math.Max(LeastPresets, bw * PresetsShare)));
            Presets(s, on.InRegion(1), m, bx, by, cw, bottom, keys);
            float gx = bx + cw + UiTokens.S6, gw = bw - cw - UiTokens.S6;
            UiDraw.Box(s, on, gx - UiTokens.S3, by, UiTokens.Hairline, bottom - by, UiRole.Rule, UiRole.None, 0f, UiFlags.Decor);
            Catalogue(s, on.InRegion(2), m, gx, by, gw, bottom, keys, out float fieldX, out float fieldY);
            // The game's own preview: a target over it only the pointer takes, so a drag turns the figure as the game's own does.
            UiDraw.Node(s, at.InRegion(3), sx, sy, sw, sh, Stage, 0, -1, true);
            string hint = W(m, "reallife_ui_bizops_uniform_turn", "Drag the figure to turn it");
            float hw = UiDraw.Snap(UiTokens.S2 * 3f + UiTokens.StatusDot + UiTextRules.Width(hint, UiStyle.SmallStrong) + UiTokens.S1);
            UiDraw.Chip(s, at, sx + UiDraw.SnapDown((sw - hw) / 2f), sy + sh - UiTokens.Chip - UiTokens.S4, hint, UiRole.None, sw - UiTokens.S4 * 2f);
            if (m.ColoursOpen && m.ColourCount > 1 && fieldY > 0f)
            {
                Palette(s, m, px, sy, pw, sh, fieldX, fieldY, gx + gw, keys);
            }
            for (int i = 0; i < m.PromptCount; i++)
            {
                s.AddPrompt(m.Prompts[i].Action, m.Prompts[i].Label);
            }
            UiDraw.PromptBar(s, frame, keys);
        }

        // A label's words: the model's own lookup, or its English.
        private static string W(UiUniformsModel m, string key, string english) => m.Words != null ? m.Words(key, english) : english;

        /// <summary>Where the game's preview stands, in px: as measured from the game's own, or the screen's right.</summary>
        public static void StageBox(UiUniformsModel m, UiFrame frame, out float x, out float y, out float w, out float h)
        {
            float room = frame.Height - frame.Band;
            if (m.StageW > UiTokens.S16 && m.StageH > UiTokens.S16)
            {
                x = m.StageX;
                y = Math.Max(UiTokens.S8, m.StageY);
                w = m.StageW;
                h = Math.Min(room - UiTokens.S8 - y, m.StageH - (y - m.StageY));
                return;
            }
            w = UiDraw.Snap(frame.Width * StageShare);
            x = frame.Width - UiTokens.S16 - w;
            y = UiDraw.Snap(room * 0.08f);
            h = UiDraw.SnapDown(room - y * 2f);
        }

        // ------------------------------------------------------------------------------------------------- the presets

        // The presets' list (each its name, the chosen one marked), a new one, and the chosen one's name, copy and delete.
        private static void Presets(UiScene s, UiAt at, UiUniformsModel m, float x, float y, float w, float bottom, IUiKeys keys)
        {
            const float row = UiTokens.Control;
            UiDraw.Text(s, at, x, y, w, UiStyle.SectionHeader, UiRole.Text2, W(m, "reallife_ui_bizops_uniform_presets", "Presets"), UiFlags.Label | UiFlags.Truncate);
            y += UiDraw.Snap(UiTextRules.LineHeight(UiStyle.SectionHeader)) + UiTokens.S2;
            float foot = UiTokens.Control + UiTokens.S3;                                                      // the new preset's button
            float chosen = m.Preset >= 0 ? UiTokens.S6 + UiDraw.Snap(UiTextRules.LineHeight(UiStyle.SectionHeader)) + UiTokens.S2
                                           + UiTokens.Control + UiTokens.S3 + UiTokens.ControlSmall : 0f;        // the chosen one's name and its two actions
            float room = Math.Max(row, bottom - y - foot - chosen);
            float content = m.PresetCount * (row + UiTokens.S1);
            float offset = Math.Max(0f, Math.Min(m.PresetsOffset, content - room));
            float vh = Math.Min(room, Math.Max(row, content));
            short clip = s.AddClip(x, y, w, vh, offset);
            s.Clips[clip].ContentHeight = content;
            m.PresetsClip = clip;
            m.PresetsRoom = vh;
            m.PresetsContent = content;
            short list = s.AddList(m.PresetCount, 0, m.PresetCount, clip, m.Preset);
            m.PresetsList = list;
            var inClip = new UiAt { Parent = -1, Clip = clip, Layer = at.Layer, Order = -1, Region = at.Region };
            if (m.PresetCount == 0)
            {
                UiDraw.Text(s, at, x, y + UiTokens.S2, w, UiStyle.Body, UiRole.Text2, W(m, "reallife_ui_bizops_uniform_none", "No presets yet."), UiFlags.Wrap, 3);
            }
            for (int i = 0; i < m.PresetCount; i++)
            {
                float ry = i * (row + UiTokens.S1);
                if (ry + row < offset || ry > offset + vh)
                {
                    continue;
                }
                bool sel = i == m.Preset;
                int face = UiDraw.Box(s, inClip, 0f, ry, w, row, sel ? UiRole.RowSel : UiRole.Row, sel ? UiRole.RowSelEdge : UiRole.None, UiTokens.RadiusLarge,
                    sel ? UiFlags.Selected : UiFlags.None);
                float tw = w - UiTokens.S4 * 2f;   // the chosen preset's row is tinted and edged, as a chosen row is everywhere
                UiDraw.TextMiddle(s, inClip.In(face), UiTokens.S4, ry, row, tw, UiStyle.Title, UiRole.Text, m.Presets[i], UiFlags.Truncate);
                UiDraw.Node(s, inClip, 0f, ry, w, row, Preset, i, face, false, list, i);
            }
            UiDraw.ScrollThumb(s, at, x + w + UiTokens.S1, y, vh, offset, content);
            y += vh + UiTokens.S3;
            UiDraw.Button(s, at, x, y, w, UiTokens.Control, UiButtonKind.Secondary, W(m, "uniform_customizer_create_new_preset", "Create new preset"), UiIcon.Plus, UiAction.Count, keys,
                NewPreset, 0, !m.CanNew);
            y += UiTokens.Control + UiTokens.S6;
            if (m.Preset < 0)
            {
                return;
            }
            UiDraw.Text(s, at, x, y, w, UiStyle.SectionHeader, UiRole.Text2, W(m, "reallife_ui_bizops_uniform_name", "Name"), UiFlags.Label | UiFlags.Truncate);
            y += UiDraw.Snap(UiTextRules.LineHeight(UiStyle.SectionHeader)) + UiTokens.S2;
            UiDraw.TextField(s, at, x, y, w, m.Name, W(m, "reallife_ui_bizops_uniform_name", "Name"), Name, 0, m.NameLocked);
            y += UiTokens.Control + UiTokens.S3;
            float half = UiDraw.SnapDown((w - UiTokens.S2) / 2f);
            UiDraw.Button(s, at, x, y, half, UiTokens.ControlSmall, UiButtonKind.Secondary, W(m, "reallife_ui_bizops_uniform_copy", "Copy"), UiIcon.Plus, UiAction.Count, keys,
                Copy, 0, !m.CanCopy);
            UiDraw.Button(s, at, x + half + UiTokens.S2, y, w - half - UiTokens.S2, UiTokens.ControlSmall, UiButtonKind.Destructive, W(m, "reallife_ui_bizops_uniform_delete", "Delete"),
                UiIcon.Trash, UiAction.Count, keys, Delete, 0, !m.CanDelete);
        }

        // ------------------------------------------------------------------------------------------------- the catalogue

        // The parts as tabs (a part's own kinds under them), the shown part's pieces as cards, the worn piece's colour as a
        // field that opens its palette beside the figure's sex, and Save with what is unsaved at the foot. Writes where the
        // colour's field stands (its y 0 without one).
        private static void Catalogue(UiScene s, UiAt at, UiUniformsModel m, float x, float y, float w, float bottom, IUiKeys keys, out float fieldX, out float fieldY)
        {
            fieldX = fieldY = 0f;
            if (m.Preset < 0)
            {
                UiDraw.EmptyState(s, at, x, y, w, bottom - y, UiIcon.PaintBrush, W(m, "reallife_ui_bizops_uniform_pick", "Choose a preset"),
                    (m.PresetCount == 0 ? W(m, "reallife_ui_bizops_uniform_none_body", "Create a preset, dress it, then give it to a role in each business's settings.")
                        : W(m, "reallife_ui_bizops_uniform_pick_body", "Pick a preset on the left to dress it.")), null, -1, keys);
                return;
            }
            if (m.Parts.Length > 0)
            {
                y += UiDraw.Tabs(s, at, x, y, w, m.Parts, m.Part, keys, Part) + UiTokens.S2;
            }
            if (m.Kinds.Length > 1)
            {
                y += UiDraw.Tabs(s, at, x, y, w, m.Kinds, m.Kind, null, Kind) + UiTokens.S2;
            }
            y += UiTokens.S2;
            // The foot: the worn colour's field (when the piece has more than one) and the figure's sex on one line, Save.
            float foot = UiTokens.Control + UiTokens.S4 + UiTokens.Control;
            float room = Math.Max(Card, bottom - y - foot - UiTokens.S4);
            Pieces(s, at, m, x, y, w, room);
            float fy = bottom - foot;
            if (m.ColourCount > 1)
            {
                string label = W(m, "reallife_ui_bizops_uniform_colour", "Colour");
                float lw = UiDraw.Snap(UiTextRules.Width(label, UiStyle.Body) + UiTokens.S1);
                UiDraw.TextMiddle(s, at, x, fy, UiTokens.Control, lw, UiStyle.Body, UiRole.Text2, label, UiFlags.Label);
                fieldX = x + lw + UiTokens.S3;
                fieldY = fy;
                ColourBox(s, at, m, fieldX, fy);
            }
            string[] sexes = { W(m, "reallife_ui_bizops_uniform_male", "Male"), W(m, "reallife_ui_bizops_uniform_female", "Female") };
            float tabsW = 0f;
            for (int i = 0; i < sexes.Length; i++)
            {
                tabsW += UiDraw.Snap(UiTextRules.Width(sexes[i], UiStyle.Nav) + UiTokens.S4 * 2f) + (i > 0 ? UiTokens.S1 : 0f);
            }
            UiDraw.Tabs(s, at, x + w - tabsW, fy, tabsW, sexes, m.Female ? 1 : 0, null, Sex);
            fy += UiTokens.Control + UiTokens.S4;
            string save = W(m, "uniform_customizer_save_preset_changes", "Save changes to selected preset");
            float bw = Math.Min(w, UiDraw.ButtonWidth(UiButtonKind.Primary, save, UiIcon.Check, UiAction.Alt, keys));
            if (m.CanSave)
            {
                UiDraw.TextMiddle(s, at, x, fy, UiTokens.Control, w - bw - UiTokens.S3, UiStyle.Small, UiRole.WarnText,
                    W(m, "reallife_ui_bizops_uniform_unsaved", "Unsaved changes"), UiFlags.Truncate);
            }
            UiDraw.Button(s, at, x + w - bw, fy, bw, UiTokens.Control, UiButtonKind.Primary, save, UiIcon.Check, UiAction.Alt, keys, Save, 0, !m.CanSave);
        }

        // The worn colour as a field: its swatch, the game's own picture, and the chevron that opens the others over the page.
        private static void ColourBox(UiScene s, UiAt at, UiUniformsModel m, float x, float y)
        {
            int face = UiDraw.Box(s, at, x, y, ColourField, UiTokens.Control, UiRole.Field, UiRole.None, UiTokens.RadiusLarge, m.ColoursOpen ? UiFlags.Selected : UiFlags.None);
            UiAt inside = at.In(face);
            int worn = 0;
            for (int i = 0; i < m.ColourCount; i++)
            {
                worn = m.Colours[i].Chosen ? i : worn;
            }
            float sw = UiTokens.ControlSmall - UiTokens.S2;
            int chip = UiDraw.Box(s, inside, x + UiTokens.S2, y + (UiTokens.Control - sw) / 2f, sw, sw, UiRole.Row, UiRole.None, UiTokens.RadiusMedium, UiFlags.Middle);
            var card = new UiCatalogueCard { Picture = m.Colours[worn].Picture, Aspect = 1f, Icon = UiIcon.PaintBrush };
            UiCatalogue.Picture(s, inside.In(chip), ref card, x + UiTokens.S2 + UiTokens.S1, y + (UiTokens.Control - sw) / 2f + UiTokens.S1, sw - UiTokens.S2, sw - UiTokens.S2, UiTokens.IconSmall);
            UiDraw.Icon(s, inside, x + ColourField - UiTokens.S3 - UiTokens.Icon, y + (UiTokens.Control - UiTokens.Icon) / 2f, UiTokens.Icon, m.ColoursOpen ? UiIcon.Up : UiIcon.Down,
                UiRole.Text2, UiFlags.Middle);
            UiDraw.Node(s, at, x, y, ColourField, UiTokens.Control, Colours, 0, face);
        }

        // The worn piece's colours over the panel, on a card rising from their field: each a swatch, the worn one ringed,
        // pressed to wear it with the palette kept open to try the next on the figure; Done (or Back) puts it away. The
        // figure beside stays clear, so each colour shows on it as it is worn.
        private static void Palette(UiScene s, UiUniformsModel m, float px, float py, float pw, float ph, float fieldX, float fieldY, float right, IUiKeys keys)
        {
            var on = new UiAt { Parent = -1, Clip = -1, Layer = 1, Order = -1, Region = 4 };
            UiDraw.Scrim(s, px, py, pw, ph, 1);
            string done = W(m, "reallife_ui_bizops_done", "Done");
            float dw = UiDraw.ButtonWidth(UiButtonKind.Primary, done, UiIcon.Check, UiAction.Count, keys);
            float most = right - fieldX - UiTokens.S5 * 2f;
            int perRow = Math.Max(1, Math.Min(m.ColourCount, (int)((most + UiTokens.S2) / (Swatch + UiTokens.S2))));
            int rows = (m.ColourCount + perRow - 1) / perRow;
            float gridW = perRow * (Swatch + UiTokens.S2) - UiTokens.S2;
            float cw = Math.Max(gridW, dw) + UiTokens.S5 * 2f;
            float ch = UiTokens.S5 + rows * (Swatch + UiTokens.S2) - UiTokens.S2 + UiTokens.S4 + UiTokens.Control + UiTokens.S5;
            float cx = Math.Min(fieldX, right - cw), cy = UiDraw.Snap(fieldY - UiTokens.S2 - ch);
            int card = UiDraw.Box(s, on, cx, cy, cw, ch, UiRole.Panel, UiRole.PanelEdge, UiTokens.RadiusPanel, UiFlags.Panel);
            UiAt inside = on.In(card);
            short list = s.AddList(m.ColourCount, 0, m.ColourCount, -1, -1);
            int first = -1;
            for (int i = 0; i < m.ColourCount; i++)
            {
                float x = cx + UiTokens.S5 + (i % perRow) * (Swatch + UiTokens.S2), y = cy + UiTokens.S5 + (i / perRow) * (Swatch + UiTokens.S2);
                PieceCard(s, inside, ref m.Colours[i], x, y, Swatch, Colour, i, list);
                first = first < 0 || m.Colours[i].Chosen ? s.NodeCount - 1 : first;
            }
            UiDraw.Button(s, inside, cx + cw - UiTokens.S5 - dw, cy + ch - UiTokens.S5 - UiTokens.Control, dw, UiTokens.Control, UiButtonKind.Primary, done, UiIcon.Check, UiAction.Count, keys,
                ColoursDone, 0);
            if (first >= 0)
            {
                s.FocusFirst = first;   // the worn colour, so the d-pad starts where the figure is
            }
        }

        // The shown part's pieces as the game's own pictures on cards, in rows that scroll inside their room.
        private static void Pieces(UiScene s, UiAt at, UiUniformsModel m, float x, float y, float w, float room)
        {
            int columns = Math.Max(1, (int)((w + UiTokens.S3) / (Card + UiTokens.S3)));
            float card = UiDraw.SnapDown((w - UiTokens.S3 * (columns - 1)) / columns);
            int rows = (m.PieceCount + columns - 1) / columns;
            float content = rows * (card + UiTokens.S3);
            float offset = Math.Max(0f, Math.Min(m.PiecesOffset, content - room));
            short clip = s.AddClip(x, y, w, room, offset);
            s.Clips[clip].ContentHeight = content;
            s.Clips[clip].Fills = true;   // the room is the grid's: a part of few pieces ends its rows high in it
            m.PiecesClip = clip;
            m.PieceColumns = columns;
            m.PiecesRoom = room;
            m.PiecesContent = content;
            if (m.PieceCount == 0)
            {
                UiDraw.EmptyState(s, at, x, y, w, room, UiIcon.PaintBrush, W(m, "reallife_ui_bizops_uniform_pieces_none", "Nothing to choose for this part"), null, null, -1, null);
                return;
            }
            var inClip = new UiAt { Parent = -1, Clip = clip, Layer = at.Layer, Order = -1, Region = at.Region };
            short list = s.AddList(m.PieceCount, 0, m.PieceCount, clip, -1);
            m.PiecesList = list;
            for (int i = 0; i < m.PieceCount; i++)
            {
                float ry = (i / columns) * (card + UiTokens.S3);
                if (ry + card < offset || ry > offset + room)
                {
                    continue;
                }
                PieceCard(s, inClip, ref m.Pieces[i], (i % columns) * (card + UiTokens.S3), ry, card, Piece, i, list);
            }
            UiDraw.ScrollThumb(s, at, x + w + UiTokens.S1, y, room, offset, content);
        }

        // A piece or a colour: its picture letterboxed on a slate card, the one worn ringed in cyan with its tick.
        private static void PieceCard(UiScene s, UiAt at, ref UiUniformPiece p, float x, float y, float size, int command, int i, short list)
        {
            int face = UiDraw.Box(s, at, x, y, size, size, p.Chosen ? UiRole.AccentSoft : UiRole.Row, p.Chosen ? UiRole.Accent : UiRole.RowEdge, UiTokens.RadiusLarge,
                p.Chosen ? UiFlags.Selected : UiFlags.None);
            var card = new UiCatalogueCard { Picture = p.Picture, Aspect = 1f, Icon = UiIcon.PaintBrush };
            float inset = size > Swatch ? UiTokens.S2 : UiTokens.S1;
            UiCatalogue.Picture(s, at.In(face), ref card, x + inset, y + inset, size - inset * 2f, size - inset * 2f, UiTokens.Icon);
            if (p.Sport)
            {
                UiDraw.Icon(s, at.In(face), x + UiTokens.S1, y + size - UiTokens.IconSmall - UiTokens.S1, UiTokens.IconSmall, UiIcon.Trophy, UiRole.Text2, UiFlags.None, true);
            }
            if (p.Chosen && size > Swatch)
            {
                UiDraw.Icon(s, at.In(face), x + size - UiTokens.IconSmall - UiTokens.S1, y + UiTokens.S1, UiTokens.IconSmall, UiIcon.Check, UiRole.AccentText, UiFlags.None, true);
            }
            UiDraw.Node(s, at, x, y, size, size, command, i, face, false, list, i);
        }
    }

    /// <summary>The words of a uniform's parts, by the kind of piece the game's own menu links each part to. Game-free.</summary>
    public static class UniformWords
    {
        // The kinds of piece a uniform is made of (the game's own AppearanceElementType names, in its customiser's order),
        // and the key of each one's words at the same place.
        public static readonly string[] Kinds = { "headaccessory", "torso", "torsoaccessory", "legs", "legsaccessory", "feet", "feetaccessory" };

        private static readonly string[] Keys =
        {
            "reallife_ui_bizops_wear_headaccessory", "reallife_ui_bizops_wear_torso", "reallife_ui_bizops_wear_torsoaccessory", "reallife_ui_bizops_wear_legs",
            "reallife_ui_bizops_wear_legsaccessory", "reallife_ui_bizops_wear_feet", "reallife_ui_bizops_wear_feetaccessory"
        };

        /// <summary>The key of a part's words by its link ("Torso", "torsoAccessory"), or null for a kind the mod does not know.</summary>
        public static string KeyOf(string linkId)
        {
            if (string.IsNullOrEmpty(linkId))
            {
                return null;
            }
            for (int i = 0; i < Kinds.Length; i++)
            {
                if (string.Equals(Kinds[i], linkId, StringComparison.OrdinalIgnoreCase))
                {
                    return Keys[i];
                }
            }
            return null;
        }
    }
}
