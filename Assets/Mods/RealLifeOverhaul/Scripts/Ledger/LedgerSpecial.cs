using System.Collections.Generic;

namespace RealLifeOverhaul.Ledger
{
    /// <summary>A front page of its own outside the morning's editions (Permadeath's obituary), in another feature's words. Game-free.</summary>
    public sealed class LedgerSpecial
    {
        public int Day;                      // the day in the dateline
        public string Header = string.Empty; // the menu's header over the sheet
        public string EarLeftLabel = string.Empty, EarLeft = string.Empty;
        public string EarRightLabel = string.Empty, EarRight = string.Empty;
        public string DateMiddle = string.Empty, DateRight = string.Empty;   // the dateline's middle and right; its left is the day
        public string Kicker = string.Empty, Headline = string.Empty, Stand = string.Empty, Byline = string.Empty;
        public string Body = string.Empty;   // the lead's text, under the drop cap

        /// <summary>The sections in the columns after the lead: a label each, and its paragraphs one after the other.</summary>
        public readonly List<string> Labels = new List<string>(4);
        public readonly List<List<string>> Texts = new List<List<string>>(4);

        /// <summary>The box down the side: labels and their lines, in order (a label is followed by the lines up to the next).</summary>
        public readonly List<string> BoxLabels = new List<string>(3);
        public readonly List<List<string>> BoxLines = new List<List<string>>(3);

        /// <summary>The framed notice at the foot of the page, where there is room for it.</summary>
        public string NoticeHead = string.Empty, Notice = string.Empty;

        public void Clear()
        {
            Day = 0;
            Header = EarLeftLabel = EarLeft = EarRightLabel = EarRight = DateMiddle = DateRight = string.Empty;
            Kicker = Headline = Stand = Byline = Body = NoticeHead = Notice = string.Empty;
            Labels.Clear();
            Texts.Clear();
            BoxLabels.Clear();
            BoxLines.Clear();
        }

        /// <summary>A new section of the columns; its paragraphs are added to the list returned.</summary>
        public List<string> Section(string label)
        {
            var lines = new List<string>(6);
            Labels.Add(label ?? string.Empty);
            Texts.Add(lines);
            return lines;
        }

        /// <summary>A new label in the box; its lines are added to the list returned.</summary>
        public List<string> Box(string label)
        {
            var lines = new List<string>(12);
            BoxLabels.Add(label ?? string.Empty);
            BoxLines.Add(lines);
            return lines;
        }
    }
}
