namespace RealLifeOverhaul.GameUi
{
    /// <summary>
    /// The rules of the white box's look: the type scale, a text's width in letters, labels and old
    /// "Label <b>value</b>" lines, meters, weekday runs and each app's accent. Game-free; checked offline (ContractCheck LookChecks).
    /// </summary>
    public static class LookRules
    {
        // The type scale and spacing, in canvas pixels of the game's 3840 x 2160. A fact's label and value are one size, so
        // that they stand on one baseline; the label is told from its value by its grey, the value by its weight.
        public const float SubtitleSize = 27f;    // the line under the title: who and where
        public const float BodySize = 28.68f;     // the game's own line of the box (MyEmployees' Age): facts, values and sentences
        public const float LabelSize = BodySize;  // a fact's label: the value's size, in grey
        public const float HeadingSize = 24f;     // a section's heading, in small capitals
        public const float ChipSize = 21f;        // a status chip's word, in capitals
        public const float NoteSize = 24f;        // notes, a list entry's requirement and the Guide's page, at the foot
        public const float LineSpacing = 8f;      // extra between the lines of a wrapped text, in percent (the game's own line of the box has far more)

        public const float RuleHeight = 2f;       // a hairline: one pixel on a 1920 screen
        public const float AccentWidth = 88f;     // the app's colour over the title
        public const float AccentHeight = 8f;
        public const float MeterHeight = 14f;     // a meter's bar, under its row, the card's whole width
        public const float MarkSize = 30f;        // a tick or a dot in front of a need
        public const float HeadingTracking = 6f;  // letter spacing of a heading (TextMeshPro's em/100)
        public const float ChipTracking = 4f;

        /// <summary>A text's width in letters: Latin or Cyrillic one, a square CJK character two, mark-up in angle brackets nothing.</summary>
        public static int Units(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return 0;
            }
            int units = 0;
            bool tag = false;
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c == '<')
                {
                    tag = true;
                    continue;
                }
                if (tag)
                {
                    tag = c != '>';
                    continue;
                }
                units += Wide(c) ? 2 : 1;
            }
            return units;
        }

        /// <summary>A character set square, two letters wide: CJK ideographs and kana, Hangul, full-width forms.</summary>
        public static bool Wide(char c)
        {
            return (c >= 'ᄀ' && c <= 'ᅟ') || (c >= '⺀' && c <= '꓏') || (c >= '가' && c <= '힣')
                || (c >= '豈' && c <= '﫿') || (c >= '︰' && c <= '﹏') || (c >= '＀' && c <= '｠') || (c >= '￠' && c <= '￦');
        }

        /// <summary>A label without the colon (or Chinese full-width colon) and spaces it ends in: a row's column says what the colon did.</summary>
        public static string Label(string label)
        {
            if (string.IsNullOrEmpty(label))
            {
                return string.Empty;
            }
            int end = label.Length;
            while (end > 0 && (label[end - 1] == ' ' || label[end - 1] == ':' || label[end - 1] == '：'))
            {
                end--;
            }
            return end == label.Length ? label : label.Substring(0, end);
        }

        /// <summary>The colon that follows a label in running text: the full-width one after a Chinese label, ": " after any other.</summary>
        public static string Colon(string label)
        {
            return !string.IsNullOrEmpty(label) && Wide(label[label.Length - 1]) ? "：" : ": ";
        }

        /// <summary>An old "Label <b>value</b>" line (FinanceText.Pair, the game's box) read as a row; false for a sentence.</summary>
        public static bool SplitPair(string words, out string label, out string value)
        {
            label = value = null;
            if (string.IsNullOrEmpty(words))
            {
                return false;
            }
            int open = words.IndexOf("<b>", System.StringComparison.Ordinal);
            if (open <= 0 || open != words.LastIndexOf("<b>", System.StringComparison.Ordinal))
            {
                return false;
            }
            int shut = words.IndexOf("</b>", open, System.StringComparison.Ordinal);
            if (shut < 0 || shut != words.LastIndexOf("</b>", System.StringComparison.Ordinal))
            {
                return false;
            }
            string head = words.Substring(0, open);
            if (head.IndexOf('<') >= 0 || head.IndexOf('\n') >= 0)
            {
                return false;
            }
            string body = words.Substring(open + 3, shut - open - 3);
            string tail = words.Substring(shut + 4);
            if (body.Length == 0)
            {
                return false;
            }
            if (tail.Length > 0)
            {
                string end = head.TrimEnd();
                bool colon = end.Length > 0 && (end[end.Length - 1] == ':' || end[end.Length - 1] == '：');
                if (!colon || tail.IndexOf('<') >= 0 || tail.IndexOf('\n') >= 0 || tail.IndexOf(". ", System.StringComparison.Ordinal) >= 0
                    || tail.IndexOf('。') >= 0 || Units(tail) > 24)
                {
                    return false;   // more than a few words after the value: a sentence holding a figure
                }
            }
            string trimmed = Label(head);
            if (trimmed.Length == 0 || Units(trimmed) > 90)
            {
                return false;   // a long run of words before a figure is a sentence
            }
            label = trimmed;
            value = tail.Length > 0 ? body + tail.TrimEnd('.', '。') : body;
            return true;
        }

        /// <summary>A meter's fill, 0 to 1: had over wanted; full when nothing is wanted, empty when nothing is had.</summary>
        public static float Fill(long have, long want)
        {
            if (want <= 0)
            {
                return have > 0 || want == 0 ? 1f : 0f;
            }
            if (have <= 0)
            {
                return 0f;
            }
            return have >= want ? 1f : (float)have / want;
        }

        /// <summary>A share for a meter: 0 to 1, and 0 for anything that is no number.</summary>
        public static float Clamp(float share)
        {
            if (!(share > 0f))
            {
                return 0f;
            }
            return share > 1f ? 1f : share;
        }

        /// <summary>The runs of days in a week's mask (bit 0 Monday): three or more one run ("Mon to Fri"); returns how many were written.</summary>
        public static int DayRuns(int days, int[] from, int[] to)
        {
            int n = 0;
            int d = 0;
            while (d < 7)
            {
                if ((days & (1 << d)) == 0)
                {
                    d++;
                    continue;
                }
                int end = d;
                while (end + 1 < 7 && (days & (1 << (end + 1))) != 0)
                {
                    end++;
                }
                if (end - d >= 2)
                {
                    from[n] = d;
                    to[n] = end;
                    n++;
                }
                else
                {
                    for (int i = d; i <= end; i++)
                    {
                        from[n] = i;
                        to[n] = i;
                        n++;
                    }
                }
                d = end + 1;
            }
            return n;
        }

        /// <summary>Each app's accent, 0xRRGGBB, the deeper of its icon's two colours, matched by the start of the app's id.</summary>
        public static readonly string[] AccentIds =
        {
            "RealLifeCareer", "RealLifeCampus", "RealLifeBank", "RealLifeBrands", "RealLifeCompany", "RealLifeDealer",
            "RealLifeFinance", "RealLifeEstate", "RealLifeOnline", "RealLifeOwnBank", "RealLifeServices", "RealLifeVentures",
            "RealLifeNewsstand", "RealLifeLedger", "RealLifeStocks", "RealLifeGuide", "RealLifeSettings", "RealLifePlanner",
            "RealLifeFlare", "RealLifeTechSector", "RealLifeOpenRoles", "RealLifeOverhaulUtilities", "Interior",
            "RealLifeBuilders", "RealLifeEconomy", "RealLifeStaff"
        };

        public static readonly int[] AccentColours =
        {
            0x284080, 0xA86028, 0x3A5E8C, 0xB05428, 0x344EB0, 0x2454A4,
            0x18946E, 0x227860, 0xC05430, 0x805C1E, 0x227A80, 0x3E4EB0,
            0x2C3A60, 0x2C3A60, 0x20784E, 0xE0680A, 0x3A465C, 0xE23D6C,
            0xD63E62, 0x3C32A0, 0x287078, 0x287896, 0x596975,
            0xC4701C, 0x0E8F82, 0x1E7850
        };

        /// <summary>The game's slate, its headlines' colour in the box, for an app with no colour of its own.</summary>
        public const int Slate = 0x596975;

        /// <summary>An app's accent by its id, the slate for none listed; the longest listed id that starts it wins.</summary>
        public static int Accent(string appId)
        {
            if (string.IsNullOrEmpty(appId))
            {
                return Slate;
            }
            int best = -1, length = 0;
            for (int i = 0; i < AccentIds.Length; i++)
            {
                string id = AccentIds[i];
                if (id.Length > length && appId.StartsWith(id, System.StringComparison.Ordinal))
                {
                    best = i;
                    length = id.Length;
                }
            }
            return best >= 0 ? AccentColours[best] : Slate;
        }
    }
}
