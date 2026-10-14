namespace RealLifeOverhaul.GameUi
{
    /// <summary>
    /// The rules of the mod's look for the white box of every app (InfoBox): how wide a text is in the box's own
    /// measure, when a value goes under its label instead of beside it, how a label loses the colon a row no longer
    /// needs, how an old "Label <b>value</b>" line is read as a row, how full a meter is, which days of a week run on,
    /// and each app's accent. Plain arithmetic and strings: no Unity or game types, nothing kept. Called only while a
    /// page is written (on a click); checked offline (ContractCheck, LookChecks).
    /// </summary>
    public static class LookRules
    {
        // ---------------------------------------------------------------- the type scale and the spacing (canvas pixels of the game's 3840 x 2160)

        public const float TitleSize = 50f;       // the game's own title of the box (MyEmployees' EmployeeName)
        public const float SubtitleSize = 27f;    // the line under the title: who and where
        public const float BodySize = 28.68f;     // the game's own line of the box (MyEmployees' Age): rows' values and sentences
        public const float LabelSize = 27f;       // a row's label, a step under its value
        public const float HeadingSize = 25f;     // a section's heading, in small capitals
        public const float ChipSize = 21f;        // a status chip's word, in capitals
        public const float NoteSize = 24f;        // notes and the Guide's page, at the foot

        public const float BoxSpacing = 12f;      // between the box's parts (the game's own box has 30: one size for every kind of line)
        public const float SectionGap = 26f;      // above a section's heading, on top of BoxSpacing
        public const float SectionSpacing = 10f;  // between the rows of a section
        public const float Indent = 28f;          // a section's rows stand in from its heading
        public const float RowGap = 24f;          // between a row's label and its value
        public const float RuleHeight = 2f;       // a hairline: one pixel on a 1920 screen
        public const float AccentWidth = 88f;     // the app's colour over the title
        public const float AccentHeight = 8f;
        public const float MeterWidth = 220f;     // a short meter, beside its figure
        public const float MeterHeight = 14f;
        public const float MarkSize = 30f;        // a tick or a dot in front of a need
        public const float HeadingTracking = 6f;  // letter spacing of a heading (TextMeshPro's em/100)
        public const float ChipTracking = 4f;

        /// <summary>The share of an em an average letter of the game's body font (Rubik) takes, a little over: how many letters a row holds.</summary>
        public const float LetterEm = 0.55f;

        /// <summary>The width of a row in the phone apps' box (MyEmployees' 949 less its padding of 80 a side and a section's indent): for a box not yet laid out.</summary>
        public const float DefaultWidth = 761f;

        // ---------------------------------------------------------------- widths

        /// <summary>
        /// How wide a text is, in letters: a letter of a Latin or Cyrillic script is one, a Chinese, Japanese or Korean
        /// character two (they are set square), and mark-up in angle brackets nothing.
        /// </summary>
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

        /// <summary>How many letters a line of the box holds: its width over an average letter of the body font.</summary>
        public static int Capacity(float width, float fontSize)
        {
            if (!(width > 0f) || !(fontSize > 0f))
            {
                return 0;
            }
            return (int)(width / (fontSize * LetterEm));
        }

        /// <summary>
        /// Whether a row's value goes under its label rather than beside it: when the two and the gap between them do not
        /// fit one line of the box, with a gap of four letters between them. A box of no known width (0) keeps them side by side.
        /// </summary>
        public static bool Stack(string label, string value, int capacity)
        {
            if (capacity <= 0)
            {
                return false;
            }
            if ((label != null && label.IndexOf('\n') >= 0) || (value != null && value.IndexOf('\n') >= 0))
            {
                return true;   // a value of several lines (or a label) stands under its label
            }
            return Units(label) + Units(value) + 4 > capacity;
        }

        /// <summary>
        /// How wide a chip is, in canvas pixels: its word in capitals (a capital about two thirds of an em, a square
        /// character a whole one), its letter spacing and its padding. The title leaves this much room at its right.
        /// </summary>
        public static float ChipWidth(string words)
        {
            float em = 0f;
            int letters = 0;
            if (!string.IsNullOrEmpty(words))
            {
                for (int i = 0; i < words.Length; i++)
                {
                    em += Wide(words[i]) ? 1f : 0.68f;
                    letters++;
                }
            }
            return em * ChipSize + letters * ChipSize * ChipTracking / 100f + 36f;
        }

        // ---------------------------------------------------------------- labels

        /// <summary>A label without the colon (or the full-width colon of Chinese) and spaces it ends in: a row's column says what a colon said.</summary>
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

        /// <summary>
        /// An old line of the form "Label <b>value</b>" (or "Label: <b>value</b>"), as FinanceText.Pair and the game's own
        /// box write one, read as a row: true with the label (without its colon) and the value when the line is a label
        /// with no mark-up of its own followed by one bold value that ends it; false for a sentence.
        /// </summary>
        public static bool SplitPair(string words, out string label, out string value)
        {
            label = value = null;
            if (string.IsNullOrEmpty(words) || !words.EndsWith("</b>", System.StringComparison.Ordinal))
            {
                return false;
            }
            int open = words.IndexOf("<b>", System.StringComparison.Ordinal);
            if (open <= 0 || open != words.LastIndexOf("<b>", System.StringComparison.Ordinal))
            {
                return false;
            }
            string head = words.Substring(0, open);
            if (head.IndexOf('<') >= 0 || head.IndexOf('\n') >= 0)
            {
                return false;
            }
            string body = words.Substring(open + 3, words.Length - open - 3 - 4);
            if (body.Length == 0 || body.IndexOf("</b>", System.StringComparison.Ordinal) >= 0)
            {
                return false;
            }
            string trimmed = Label(head);
            if (trimmed.Length == 0 || Units(trimmed) > 90)
            {
                return false;   // a long run of words before a figure is a sentence, not a label
            }
            label = trimmed;
            value = body;
            return true;
        }

        // ---------------------------------------------------------------- meters

        /// <summary>How full a meter is, 0 to 1: what is had over what is wanted; full when nothing is wanted, empty for nothing had.</summary>
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

        /// <summary>A share brought into a meter: 0 to 1, and 0 for anything that is not a number.</summary>
        public static float Clamp(float share)
        {
            if (!(share > 0f))
            {
                return 0f;
            }
            return share > 1f ? 1f : share;
        }

        // ---------------------------------------------------------------- days of a week

        /// <summary>
        /// The runs of days in a week's mask (bit 0 Monday to bit 6 Sunday), in order: a run of three days or more is
        /// one run ("Mon to Fri"), a run of one or two is a run of each day. Returns how many runs were written into
        /// 'from' and 'to' (each at least 7 long).
        /// </summary>
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

        // ---------------------------------------------------------------- each app's colour

        /// <summary>
        /// Each app's accent, as 0xRRGGBB: the deeper of the two colours its icon is painted in (each Director's
        /// AppIconPainter.Paint), so that the thin bar over a box's title is the colour of the icon that opened it.
        /// Matched by the start of the app's id (the banks are "RealLifeBank1" and on); an app not listed has the
        /// game's own slate.
        /// </summary>
        public static readonly string[] AccentIds =
        {
            "RealLifeCareer", "RealLifeCampus", "RealLifeBank", "RealLifeBrands", "RealLifeCompany", "RealLifeDealer",
            "RealLifeFinance", "RealLifeEstate", "RealLifeOnline", "RealLifeOwnBank", "RealLifeServices", "RealLifeVentures",
            "RealLifeNewsstand", "RealLifeLedger", "RealLifeStocks", "RealLifeGuide", "RealLifeSettings", "RealLifePlanner",
            "RealLifeFlare", "RealLifeTechSector", "RealLifeOpenRoles", "RealLifeOverhaulUtilities", "Interior"
        };

        public static readonly int[] AccentColours =
        {
            0x284080, 0xA86028, 0x3A5E8C, 0xB05428, 0x344EB0, 0x2454A4,
            0x18946E, 0x227860, 0xC05430, 0x805C1E, 0x227A80, 0x3E4EB0,
            0x2C3A60, 0x2C3A60, 0x20784E, 0xE0680A, 0x3A465C, 0xE23D6C,
            0xD63E62, 0x3C32A0, 0x287078, 0x287896, 0x596975
        };

        /// <summary>The game's slate (the colour of its own headlines in the box), for an app with no colour of its own.</summary>
        public const int Slate = 0x596975;

        /// <summary>The accent of an app by its id; the slate for none listed. The longest id that starts the given one wins.</summary>
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
