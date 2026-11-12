using System;

namespace RealLifeOverhaul.Ui
{
    /// <summary>The key an answer carries at its right, as the conversation and the decision card show it: F the main answer, P the second, the way back.</summary>
    public enum UiChoiceCap : byte
    {
        None = 0,
        Main,       // the game's Interact (F): the answer the screen leads with
        Second,     // the game's SecondaryInteract (P): the next one
        Leave       // the game's Cancel (Escape): the way out
    }

    /// <summary>
    /// An answer's parts the decision card and the conversation draw alike: its lines under its name, each behind its mark
    /// in its colour (green what it brings, red the money it costs, amber a risk or a reason, grey what simply happens),
    /// and the bound key's cap at the right of its name. One place, so the two read the same. Game-free. Cost: a few items
    /// a line.
    /// </summary>
    public static partial class UiDraw
    {
        /// <summary>The action a cap stands for, or Count for none.</summary>
        public static UiAction CapAction(UiChoiceCap cap) =>
            cap == UiChoiceCap.Main ? UiAction.Alt : cap == UiChoiceCap.Second ? UiAction.Option : cap == UiChoiceCap.Leave ? UiAction.Back : UiAction.Count;

        /// <summary>The room an answer's cap takes at the right of its name, its gap included; 0 where there is none or no key is bound.</summary>
        public static float ChoiceCapWidth(UiChoiceCap cap, IUiKeys keys)
        {
            UiAction action = CapAction(cap);
            return action != UiAction.Count && HasKey(action, keys) ? KeyCapWidth(action, keys) + UiTokens.S3 : 0f;
        }

        /// <summary>An answer's cap, its right edge at <paramref name="right"/>, in the middle of a line <paramref name="h"/> tall. Returns its room, as <see cref="ChoiceCapWidth"/>.</summary>
        public static float ChoiceCap(UiScene s, UiAt at, float right, float y, float h, UiChoiceCap cap, IUiKeys keys)
        {
            float room = ChoiceCapWidth(cap, keys);
            if (room <= 0f)
            {
                return 0f;
            }
            UiAction action = CapAction(cap);
            KeyCap(s, at, right - (room - UiTokens.S3), y + (h - UiTokens.KeyCap) / 2f, action, keys, UiRole.Track, UiRole.Text);
            s.Items[s.ItemCount - 1].Flags |= UiFlags.Middle;
            return room;
        }

        /// <summary>A line's colour by its tone: green for the player, red for money paid out, amber against them, grey for neither.</summary>
        public static UiRole ConsequenceInk(int tone) =>
            tone > 0 ? UiRole.OkText : tone <= UiConsequence.Loss ? UiRole.DangerText : tone < 0 ? UiRole.WarnText : UiRole.Text2;

        /// <summary>A line's mark: its figure's rise or fall, else a tick for good, the falling arrow for money out, the warning against, a dash for neither.</summary>
        public static UiIcon ConsequenceMark(int tone, int change) =>
            change > 0 ? UiIcon.TrendUp : change < 0 || tone <= UiConsequence.Loss ? UiIcon.TrendDown : tone > 0 ? UiIcon.Check : tone < 0 ? UiIcon.Warning : UiIcon.Minus;

        /// <summary>
        /// A line under an answer's name, its mark at <paramref name="x"/> and its words after it, one line high. On the
        /// chosen answer's fill a line of neither tone takes the main ink, as the grey would not read on it.
        /// </summary>
        public static void ConsequenceLine(UiScene s, UiAt at, float x, float y, float w, ref UiConsequence line, bool chosen)
        {
            float lh = UiTextRules.LineHeight(UiStyle.Small);
            UiRole tone = ConsequenceInk(line.Tone);
            tone = chosen && tone == UiRole.Text2 ? UiRole.Text : tone;
            UiDraw.Icon(s, at, x, y + (lh - UiTokens.IconSmall) / 2f, UiTokens.IconSmall, ConsequenceMark(line.Tone, line.Change), tone, UiFlags.Middle);
            UiDraw.Text(s, at, x + UiTokens.IconSmall + UiTokens.S2, y, Math.Max(0f, w - UiTokens.IconSmall - UiTokens.S2), UiStyle.Small, tone, line.Text,
                UiFlags.Label | UiFlags.Truncate | UiFlags.Middle);
        }
    }
}
