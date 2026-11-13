using System;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// Who a page is with, a person or a firm, as a contact's head reads: a person's face in the ring of their mood, or a
    /// firm's picture on a tile of the same size; the name, what they do and for whom, a state's chip and a second chip
    /// for whose they are (another player's, under Going Public).
    /// </summary>
    public sealed class UiHeadModel
    {
        public string Name;
        public string Face;                 // the key the face is drawn by (two of one name); null: the name
        public float Hue;
        public UiRole Ring = UiRole.None;   // the mood; None where it is not known
        public UiIcon Icon = UiIcon.None;   // a firm or a place: its picture on a tile in place of a face
        public UiRole Tint = UiRole.None;   // None for the icon's own colour
        public string Picture;              // the firm's own picture (its logo, a UiPictures key) on the tile in place of the icon; null for the icon
        public string Role;                 // "Customer Service", "Wholesale store"
        public string Employer;             // "Bloom Cafe"; null for none
        public string Status;               // "On shift"; null for none
        public UiRole StatusTone = UiRole.Ok;
        public string Owner;                // "Another player's"; null for none
    }

    public static partial class UiAppWindow
    {
        /// <summary>The head's face or tile.</summary>
        public const float HeadFace = 80f;   // px: the approved Contacts window's face, a step under a profile's

        /// <summary>
        /// A contact's head, Football Manager's profile head without its facts: the face in its mood's ring or the firm's
        /// tile, the name, what they do and for whom, the chips, and the actions at the right, the primary last, standing
        /// level with the face. Returns the y under it.
        /// </summary>
        public static float Head(UiScene s, UiAt at, UiHeadModel h, UiActionModel[] actions, float x, float y, float w, IUiKeys keys)
        {
            float ring = UiTokens.S1;
            if (!string.IsNullOrEmpty(h.Picture))
            {
                UiDraw.PictureFace(s, at, x + ring, y + ring, HeadFace, h.Picture);
            }
            else if (h.Icon != UiIcon.None)
            {
                int tile = UiDraw.Tile(s, at, x + ring, y + ring, HeadFace, h.Tint, false);
                float g = UiDraw.Snap(HeadFace * 0.5f);
                UiDraw.Icon(s, at.In(tile), x + ring + (HeadFace - g) / 2f, y + ring + (HeadFace - g) / 2f, g, h.Icon, h.Tint, UiFlags.Middle | UiFlags.Centre, true);
            }
            else
            {
                UiDraw.Portrait(s, at, x + ring, y + ring, HeadFace, h.Face ?? h.Name, h.Hue, h.Ring);
            }
            float side = HeadFace + ring * 2f;
            float right = Buttons(s, at, actions, x + w, y + (side - UiTokens.Control) / 2f, keys);
            float tx = x + side + UiTokens.S5, tw = Math.Max(UiTokens.S16, right - UiTokens.S6 - tx);
            UiStyle nameStyle = h.Icon != UiIcon.None ? UiStyle.AppTitle : UiStyle.Hero;   // the profile's large name is a person's alone
            float nameH = UiDraw.Snap(UiTextRules.LineHeight(nameStyle)), lineH = UiTokens.PairRow;
            float top = y + UiDraw.SnapDown((side - nameH - UiTokens.S2 - lineH) / 2f);
            UiDraw.Text(s, at, tx, top, tw, nameStyle, UiRole.Text, h.Name, UiFlags.Truncate);
            float ly = top + nameH + UiTokens.S2;
            float statusW = string.IsNullOrEmpty(h.Status) ? 0f : ChipWidth(h.Status);
            float ownerW = string.IsNullOrEmpty(h.Owner) ? 0f : ChipWidth(h.Owner);
            float chips = (statusW > 0f ? statusW + UiTokens.S3 : 0f) + (ownerW > 0f ? ownerW + UiTokens.S3 : 0f);
            string line = string.IsNullOrEmpty(h.Employer) ? h.Role : string.IsNullOrEmpty(h.Role) ? h.Employer : h.Role + " \u00b7 " + h.Employer;
            float lw = Math.Min(UiDraw.Snap(UiTextRules.Width(line ?? string.Empty, UiStyle.Body) + UiTokens.S1), Math.Max(0f, tw - chips));
            UiDraw.TextMiddle(s, at, tx, ly, lineH, lw, UiStyle.Body, UiRole.Text2, line, UiFlags.Truncate);
            float cx = tx + lw + (lw > 0f ? UiTokens.S3 : 0f), cy = ly + (lineH - UiTokens.Badge - UiTokens.S1) / 2f;
            if (statusW > 0f)
            {
                cx += UiDraw.Chip(s, at, cx, cy, h.Status, h.StatusTone) + UiTokens.S3;
            }
            if (ownerW > 0f)
            {
                UiDraw.Chip(s, at, cx, cy, h.Owner, UiRole.Warn);
            }
            return y + side;
        }

        private static float ChipWidth(string words) => UiDraw.Snap(UiTokens.S2 * 3f + UiTokens.StatusDot + UiTextRules.Width(words, UiStyle.SmallStrong) + UiTokens.S1);
    }
}
