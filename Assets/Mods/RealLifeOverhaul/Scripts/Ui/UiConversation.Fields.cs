using System;

namespace RealLifeOverhaul.Ui
{
    /// <summary>One field of a conversation's form: what it asks for, its label, its value in the game's words and its steps.</summary>
    public struct UiFormField
    {
        public UiFormKind Kind;
        public string Label;         // "Hourly wage"
        public string Value;         // the figure, the choice, the words typed
        public bool On;              // a switch
        public bool CanLess, CanMore;
        public bool Disabled;
    }

    public static partial class UiConversation
    {
        /// <summary>A form's height with a few fields: a control's row each on one band, a step between them.</summary>
        private static float FieldsHeight(UiConversationModel m) =>
            UiTokens.S3 * 2f + m.FieldCount * UiTokens.Control + (m.FieldCount - 1) * UiTokens.S2;

        /// <summary>
        /// The form slot with several fields, as the game's salary offer asks for a wage and a signing bonus: one band, each
        /// field's label at the left and its control at the right on one edge. Each field answers
        /// <see cref="FormCommand"/>, <see cref="FormLess"/> and <see cref="FormMore"/> with its index.
        /// </summary>
        private static void Fields(UiScene s, UiAt at, UiConversationModel m, float x, float y, float w)
        {
            UiDraw.Band(s, at, x, y, w, FieldsHeight(m));
            float cw = Math.Min(FormControl, UiDraw.SnapDown(w / 2f)), cx = x + w - UiTokens.S3 - cw;
            float cy = y + UiTokens.S3;
            for (int i = 0; i < m.FieldCount; i++)
            {
                ref UiFormField f = ref m.Fields[i];
                UiDraw.TextMiddle(s, at, x + UiTokens.S3, cy, UiTokens.Control, cx - UiTokens.S4 - x - UiTokens.S3, UiStyle.Title, UiRole.Text, f.Label, UiFlags.Label | UiFlags.Truncate);
                switch (f.Kind)
                {
                    case UiFormKind.Amount:
                        UiDraw.Stepper(s, at, cx, cy, cw, f.Value, f.CanLess, f.CanMore, FormLess, FormCommand, FormMore, i, f.Disabled);
                        break;
                    case UiFormKind.Choice:
                        UiDraw.Choice(s, at, cx, cy, cw, f.Value, FormCommand, i, f.Disabled);
                        break;
                    case UiFormKind.Switch:
                        UiDraw.Switch(s, at, cx, cy, cw, f.On, f.Value, FormCommand, i, f.Disabled);
                        break;
                    default:
                        UiDraw.TextField(s, at, cx, cy, cw, f.Value, null, FormCommand, i, f.Disabled);
                        break;
                }
                cy += UiTokens.Control + UiTokens.S2;
            }
        }
    }
}
