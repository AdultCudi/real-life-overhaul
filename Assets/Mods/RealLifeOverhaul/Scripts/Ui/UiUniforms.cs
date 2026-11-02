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
    /// them), the pieces of the part shown as the game's own pictures in a grid, its colours as swatches, the figure's
    /// sex, and Save. Plain data; the screen fills it from the game's own editor after the game filled it.
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
        public float PiecesOffset, PresetsOffset;
        public float StageX, StageY, StageW, StageH;   // px: where the game's own 3D preview stands; none wide: the screen's right
        public bool StandIn;                       // a sample's stand in for the game's 3D preview, which only the game draws
        public UiPrompt[] Prompts = new UiPrompt[0];
        public int PromptCount;

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
    /// and <see cref="Kind"/> (their index), <see cref="Piece"/> and <see cref="Colour"/> (their index), <see cref="Sex"/>
    /// (0 male, 1 female), <see cref="Save"/> and <see cref="Stage"/> (the preview, dragged). Game-free. Cost: a layout a
    /// change, the cards in view only.
    /// </summary>
    public static class UiUniforms
    {
        public const int Preset = 1, Copy = 2, Delete = 3, NewPreset = 4, Name = 5, Part = 6, Kind = 7, Piece = 8, Colour = 9, Sex = 10, Save = 11, Stage = 12;

        private const float PresetsShare = 0.3f, LeastPresets = 240f, MostPresets = 320f;   // px: the presets' column
        private const float Card = 96f;          // px: a piece's card
        private const float Swatch = 40f;        // px: a colour's swatch
        private const float LeastPanel = 720f;   // px: the panel's narrowest, the preview's left edge or not
        private const float StageShare = 0.34f;  // of the width, the preview's room when the game's own is not measured

        /// <summary>Lays the editor out over the whole screen, the game's preview left clear.</summary>
        public static void Lay(UiScene s, UiUniformsModel m, UiFrame frame, bool dark, IUiKeys keys)
        {
            s.Begin("uniforms", frame.Width, frame.Height, dark);
            StageBox(m, frame, out float sx, out float sy, out float sw, out float sh);
            float px = UiTokens.S16;
            float pw = UiDraw.SnapDown(Math.Min(frame.Width - px * 2f, Math.Max(LeastPanel, sx - UiTokens.S6 - px)));
            UiAt at = UiAt.Screen;
            if (m.StandIn)
            {
                int stand = UiDraw.Box(s, at, sx, sy, sw, sh, UiRole.Row, UiRole.RowEdge, UiTokens.RadiusPanel, UiFlags.Decor);
                float g = UiDraw.Snap(Math.Min(sw, sh) * 0.4f);
                UiDraw.Icon(s, at.In(stand), sx + (sw - g) / 2f, sy + (sh - g) / 2f, g, UiIcon.Person, UiRole.Muted, UiFlags.Middle | UiFlags.Centre, true);
            }
            int panel = UiDraw.Box(s, at, px, sy, pw, sh, UiRole.Panel, UiRole.PanelEdge, UiTokens.RadiusPanel);
            UiAt on = at.In(panel);
            float y = UiDraw.TitleStrip(s, on, px, sy, pw, UiIcon.PaintBrush, m.Title, true);
            float bx = px + UiTokens.S5, by = y + UiTokens.S5, bw = pw - UiTokens.S5 * 2f, bottom = sy + sh - UiTokens.S5;
            float cw = UiDraw.Snap(Math.Min(MostPresets, Math.Max(LeastPresets, bw * PresetsShare)));
            Presets(s, on.InRegion(1), m, bx, by, cw, bottom, keys);
            float gx = bx + cw + UiTokens.S6, gw = bw - cw - UiTokens.S6;
            UiDraw.Box(s, on, gx - UiTokens.S3, by, UiTokens.Hairline, bottom - by, UiRole.Rule, UiRole.None, 0f, UiFlags.Decor);
            Catalogue(s, on.InRegion(2), m, gx, by, gw, bottom, keys);
            // The game's own preview: a target over it only the pointer takes, so a drag turns the figure as the game's own does.
            UiDraw.Node(s, at.InRegion(3), sx, sy, sw, sh, Stage, 0, -1, true);
            string hint = UiText.T("reallife_ui_bizops_uniform_turn");
            float hw = UiDraw.Snap(UiTokens.S2 * 3f + UiTokens.StatusDot + UiTextRules.Width(hint, UiStyle.SmallStrong) + UiTokens.S1);
            UiDraw.Chip(s, at, sx + UiDraw.SnapDown((sw - hw) / 2f), sy + sh - UiTokens.Chip - UiTokens.S4, hint, UiRole.None, sw - UiTokens.S4 * 2f);
            for (int i = 0; i < m.PromptCount; i++)
            {
                s.AddPrompt(m.Prompts[i].Action, m.Prompts[i].Label);
            }
            UiDraw.PromptBar(s, frame, keys);
        }

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
            UiDraw.Text(s, at, x, y, w, UiStyle.SectionHeader, UiRole.Text2, UiText.T("reallife_ui_bizops_uniform_presets"), UiFlags.Label | UiFlags.Truncate);
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
                UiDraw.Text(s, at, x, y + UiTokens.S2, w, UiStyle.Body, UiRole.Text2, UiText.T("reallife_ui_bizops_uniform_none"), UiFlags.Wrap, 3);
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
                if (sel)
                {
                    UiDraw.Box(s, inClip.In(face), 0f, ry, UiTokens.SelectBar, row, UiRole.Accent, UiRole.None, 0f, UiFlags.Decor);
                }
                UiDraw.TextMiddle(s, inClip.In(face), UiTokens.S4, ry, row, w - UiTokens.S4 * 2f, UiStyle.Title, UiRole.Text, m.Presets[i], UiFlags.Truncate);
                UiDraw.Node(s, inClip, 0f, ry, w, row, Preset, i, face, false, list, i);
            }
            UiDraw.ScrollThumb(s, at, x + w + UiTokens.S1, y, vh, offset, content);
            y += vh + UiTokens.S3;
            UiDraw.Button(s, at, x, y, w, UiTokens.Control, UiButtonKind.Secondary, UiText.T("uniform_customizer_create_new_preset"), UiIcon.Plus, UiAction.Count, keys,
                NewPreset, 0, !m.CanNew);
            y += UiTokens.Control + UiTokens.S6;
            if (m.Preset < 0)
            {
                return;
            }
            UiDraw.Text(s, at, x, y, w, UiStyle.SectionHeader, UiRole.Text2, UiText.T("reallife_ui_bizops_uniform_name"), UiFlags.Label | UiFlags.Truncate);
            y += UiDraw.Snap(UiTextRules.LineHeight(UiStyle.SectionHeader)) + UiTokens.S2;
            UiDraw.TextField(s, at, x, y, w, m.Name, UiText.T("reallife_ui_bizops_uniform_name"), Name, 0, m.NameLocked);
            y += UiTokens.Control + UiTokens.S3;
            float half = UiDraw.SnapDown((w - UiTokens.S2) / 2f);
            UiDraw.Button(s, at, x, y, half, UiTokens.ControlSmall, UiButtonKind.Secondary, UiText.T("reallife_ui_bizops_uniform_copy"), UiIcon.Plus, UiAction.Count, keys,
                Copy, 0, !m.CanCopy);
            UiDraw.Button(s, at, x + half + UiTokens.S2, y, w - half - UiTokens.S2, UiTokens.ControlSmall, UiButtonKind.Destructive, UiText.T("reallife_ui_bizops_uniform_delete"),
                UiIcon.Trash, UiAction.Count, keys, Delete, 0, !m.CanDelete);
        }

        // ------------------------------------------------------------------------------------------------- the catalogue

        // The parts as tabs (a part's own kinds under them), the shown part's pieces as cards, its colours, the figure's
        // sex, and Save with what is unsaved at the foot.
        private static void Catalogue(UiScene s, UiAt at, UiUniformsModel m, float x, float y, float w, float bottom, IUiKeys keys)
        {
            if (m.Preset < 0)
            {
                UiDraw.EmptyState(s, at, x, y, w, bottom - y, UiIcon.PaintBrush, UiText.T("reallife_ui_bizops_uniform_pick"),
                    UiText.T(m.PresetCount == 0 ? "reallife_ui_bizops_uniform_none_body" : "reallife_ui_bizops_uniform_pick_body"), null, -1, keys);
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
            // The foot: the colours (when the piece has more than one), the figure's sex, Save.
            float swatchRows = 0f;
            int perRow = Math.Max(1, (int)((w + UiTokens.S2) / (Swatch + UiTokens.S2)));
            if (m.ColourCount > 1)
            {
                swatchRows = (m.ColourCount + perRow - 1) / perRow;
            }
            float labelH = UiDraw.Snap(UiTextRules.LineHeight(UiStyle.SectionHeader)) + UiTokens.S2;
            float colours = swatchRows > 0f ? labelH + swatchRows * (Swatch + UiTokens.S2) + UiTokens.S3 : 0f;
            float foot = colours + UiTokens.Control + UiTokens.S4 + UiTokens.Control;
            float room = Math.Max(Card, bottom - y - foot - UiTokens.S4);
            Pieces(s, at, m, x, y, w, room);
            float fy = bottom - foot;
            if (swatchRows > 0f)
            {
                UiDraw.Text(s, at, x, fy, w, UiStyle.SectionHeader, UiRole.Text2, UiText.T("reallife_ui_bizops_uniform_colour"), UiFlags.Label | UiFlags.Truncate);
                float cy = fy + labelH;
                short list = s.AddList(m.ColourCount, 0, m.ColourCount, -1, -1);
                for (int i = 0; i < m.ColourCount; i++)
                {
                    float cx = x + (i % perRow) * (Swatch + UiTokens.S2), ry = cy + (i / perRow) * (Swatch + UiTokens.S2);
                    PieceCard(s, at, ref m.Colours[i], cx, ry, Swatch, Colour, i, list);
                }
                fy += colours;
            }
            string[] sexes = { UiText.T("reallife_ui_bizops_uniform_male"), UiText.T("reallife_ui_bizops_uniform_female") };
            UiDraw.Tabs(s, at, x, fy, w, sexes, m.Female ? 1 : 0, null, Sex);
            fy += UiTokens.Control + UiTokens.S4;
            string save = UiText.T("uniform_customizer_save_preset_changes");
            float bw = Math.Min(w, UiDraw.ButtonWidth(UiButtonKind.Primary, save, UiIcon.Check, UiAction.Alt, keys));
            if (m.CanSave)
            {
                UiDraw.TextMiddle(s, at, x, fy, UiTokens.Control, w - bw - UiTokens.S3, UiStyle.Small, UiRole.WarnText, UiText.T("reallife_ui_bizops_uniform_unsaved"), UiFlags.Truncate);
            }
            UiDraw.Button(s, at, x + w - bw, fy, bw, UiTokens.Control, UiButtonKind.Primary, save, UiIcon.Check, UiAction.Alt, keys, Save, 0, !m.CanSave);
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
            m.PiecesClip = clip;
            m.PieceColumns = columns;
            m.PiecesRoom = room;
            m.PiecesContent = content;
            if (m.PieceCount == 0)
            {
                UiDraw.EmptyState(s, at, x, y, w, room, UiIcon.PaintBrush, UiText.T("reallife_ui_bizops_uniform_pieces_none"), null, null, -1, null);
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
        // The kinds of piece a uniform is made of (the game's own AppearanceElementType names, in its customiser's order).
        public static readonly string[] Kinds = { "headaccessory", "torso", "torsoaccessory", "legs", "legsaccessory", "feet", "feetaccessory" };

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
                    return "reallife_ui_bizops_wear_" + Kinds[i];
                }
            }
            return null;
        }
    }
}
