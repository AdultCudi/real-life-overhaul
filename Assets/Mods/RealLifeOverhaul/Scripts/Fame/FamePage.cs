using System;
using RealLifeOverhaul.GameUi;

namespace RealLifeOverhaul.Fame
{
    /// <summary>
    /// The Standing page, in the Finance app's frame (opened from its Overview with a button: EconomyBridges.Standing,
    /// filled in by Wiring/StandingLinks). The white box has the three parts and the four reputations, or the one thing
    /// picked; the table lists the parts, the districts, the newest changes and (FamePage.Press.cs, FamePage.City.cs)
    /// the invitations, promises, requests and the council, which the filter over it tells apart. Everything on it is
    /// the game's. PER FRAME: NOTHING. Written when the page is shown, a row is picked or a button is pressed: a few
    /// dozen strings, on a click.
    /// </summary>
    public static partial class FamePage
    {
        public const int KindPart = 0, KindDistrict = 1, KindChange = 2, KindInvite = 3, KindPromise = 4, KindRequest = 5, KindCouncil = 6;

        public static readonly string[] Kinds =
        {
            "reallife_fame_kind_part", "reallife_fame_kind_district", "reallife_fame_kind_change", "reallife_fame_kind_invite", "reallife_fame_kind_promise",
            "reallife_fame_kind_request", "reallife_fame_kind_council"
        };

        private static readonly string[] LabelKeys = { "reallife_fame_label_part_0", "reallife_fame_label_part_1", "reallife_fame_label_part_2", "reallife_fame_label_part_3" };

        /// <summary>The page's table: 0 what, 1 kind, 2 how it stands, 3 where it is heading, 4 a note.</summary>
        public static readonly TableSpec Spec = new TableSpec("Finance", "Standing")
            .Column("reallife_fame_col_what", 560f, TableSort.Text, flexible: true)
            .Column("reallife_fame_col_kind", 300f, TableSort.Text)
            .Column("reallife_fame_col_stands", 220f, TableSort.Number)
            .Column("reallife_fame_col_heading", 300f, TableSort.Custom)
            .Column("reallife_fame_col_note", 440f, TableSort.Custom, right: true)
            .Filter("reallife_tab_all_kinds", Kinds)
            .Empty("reallife_fame_empty");

        static partial void MoreRows(FameDirector director, FameFile f, Func<string, TableRow> row, int day);
        static partial void MoreBox(FameDirector director, FameFile f, InfoBox box, string picked, Action<string> pick, Action back, ref bool written);

        /// <summary>True while there is a page to show: Standing runs and has played its first day.</summary>
        public static bool On()
        {
            FameDirector director = FameDirector.Instance;
            return director != null && director.File != null && director.File.SettledDay >= 0;
        }

        public static void Write(InfoBox box, Func<string, TableRow> row, string picked, Action<string> pick, Action back)
        {
            FameDirector director = FameDirector.Instance;
            FameFile f = director != null ? director.File : null;
            if (f == null)
            {
                box.Title(FameText.T("reallife_fame_title"));
                box.Line(FameText.T("reallife_fame_off"));
                InfoBox.Button(box.ButtonRow(), InfoBox.Additional, "reallife_fame_back", back);
                return;
            }
            int day = FameGame.Day;
            Rows(director, f, row, day);
            bool written = false;
            if (picked != null)
            {
                written = WritePicked(f, box, picked, pick, back);
                if (!written)
                {
                    MoreBox(director, f, box, picked, pick, back, ref written);
                }
            }
            if (!written)
            {
                WriteSummary(f, box, back);
            }
        }

        // ---------------------------------------------------------------- the table

        private static void Rows(FameDirector director, FameFile f, Func<string, TableRow> row, int day)
        {
            string kind = FameText.T(Kinds[KindPart]);
            TableRow r = row("wealth");
            Cells(r, KindPart, FameText.T("reallife_fame_row_wealth"), kind, f.Wealth, FameText.N(f.Wealth), null, 0, FameText.Fill("reallife_fame_row_worth", "worth", FameText.Money(f.NetWorth)), f.NetWorth);
            r = row("recognition");
            Cells(r, KindPart, FameText.T("reallife_fame_row_recognition"), kind, f.Recognition, FameText.N(f.Recognition), FameText.Tier(f.Tier), (int)f.Tier,
                FameText.Fill("reallife_fame_row_followers", "count", FameText.N(f.Followers)), f.Followers);
            for (int i = 0; i < 4; i++)
            {
                r = row("part:" + i);
                FameNote last = LastNote(f, i);
                Cells(r, KindPart, FameText.Part((FamePart)i), kind, f.Rep[i], FameText.Rep(f.Rep[i]),
                    i == (int)FamePart.Integrity ? FameText.T("reallife_fame_heading_deeds") : FameText.Fill("reallife_fame_heading", "value", FameText.N(f.Target[i])), f.Target[i],
                    last != null ? FameText.Cause(last.Cause) : string.Empty, last != null ? last.Day : 0);
                r.Tones[2] = (byte)Tone(f.Rep[i]);
            }
            kind = FameText.T(Kinds[KindDistrict]);
            for (int i = 0; i < f.Districts.Count; i++)
            {
                FameDistrict d = f.Districts[i];
                r = row("district:" + i);
                Cells(r, KindDistrict, FameGame.DistrictName(d.Name), kind, d.Opinion, FameText.Rep(d.Opinion), FameText.Fill("reallife_fame_known_by", "value", FameText.N(d.Known)), d.Known,
                    FameText.Fill("reallife_fame_district_jobs", "jobs", FameText.N(d.Jobs), "raised", FameText.N(d.Raised)), d.Jobs - d.Raised * 5);
                r.Tones[2] = (byte)Tone(d.Opinion);
            }
            kind = FameText.T(Kinds[KindChange]);
            for (int i = f.Notes.Count - 1; i >= 0; i--)
            {
                FameNote n = f.Notes[i];
                r = row("note:" + i);
                string what = n.Part == FameNote.Recognition ? FameText.T("reallife_fame_row_recognition") : FameText.Part((FamePart)FameRules.Clamp(n.Part, 0, 3));
                string amount = n.Part == FameNote.Recognition ? FameText.Fill("reallife_fame_press_change", "value", (n.Delta / 10f).ToString("+0.0;-0.0", System.Globalization.CultureInfo.InvariantCulture)) : FameText.Signed(n.Delta);
                Cells(r, KindChange, FameText.Cause(n.Cause) + (n.District != null ? ", " + FameGame.DistrictName(n.District) : string.Empty), kind, n.Delta, amount, what, 0,
                    FameText.Fill("reallife_fame_day", "day", FameText.N(n.Day)), n.Day);
                r.Pick = false;
                r.Tones[2] = (byte)(n.Delta > 0 ? TableRow.Green : TableRow.Red);
            }
            MoreRows(director, f, row, day);
        }

        internal static void Cells(TableRow r, int kind, string what, string kindName, double stands, string standsWords, string heading, double headingKey, string note, double noteKey)
        {
            r.Kind = kind;
            r.Pick = true;
            r.Set(0, what);
            r.Set(1, kindName);
            r.Set(2, standsWords, stands);
            r.Set(3, heading ?? string.Empty, headingKey);
            r.Set(4, note ?? string.Empty, noteKey);
        }

        internal static CellTone Tone(int value)
        {
            return value < 35 ? TableRow.Red : value < 50 ? TableRow.Yellow : value >= 65 ? TableRow.Green : TableRow.Plain;
        }

        private static FameNote LastNote(FameFile f, int part)
        {
            for (int i = f.Notes.Count - 1; i >= 0; i--)
            {
                if (f.Notes[i].Part == part)
                {
                    return f.Notes[i];
                }
            }
            return null;
        }

        // ---------------------------------------------------------------- the white box

        private static void WriteSummary(FameFile f, InfoBox box, Action back)
        {
            box.Title(FameText.T("reallife_fame_title"));
            box.Line(FameText.Pair("reallife_fame_label_profile", FameText.Profile(FameRules.Profile(f.Wealth, f.Recognition, f.Overall))));
            box.Line(FameText.Pair("reallife_fame_label_wealth", FameText.N(f.Wealth)));
            box.Line(FameText.Fill("reallife_fame_wealth_parts", "worth", FameText.Money(f.NetWorth), "week", FameText.Money(f.Weekly), "count", FameText.N(f.Businesses),
                "properties", FameText.N(f.Properties)));
            box.Line(FameText.Pair("reallife_fame_label_recognition", FameText.Fill("reallife_fame_recognition_value", "value", FameText.N(f.Recognition), "tier", FameText.Tier(f.Tier))));
            box.Line(FameText.Fill("reallife_fame_recognition_parts", "followers", FameText.N(f.Followers), "press", (f.Press / 10f).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)));
            box.Headline("reallife_fame_head_reputation");
            box.Line(FameText.Pair("reallife_fame_label_reputation", FameText.Rep(f.Overall)));
            for (int i = 0; i < 4; i++)
            {
                box.Line(FameText.Pair(LabelKeys[i], FameText.Rep(f.Rep[i])));
            }
            InfoBox.Button(box.ButtonRow(), InfoBox.Additional, "reallife_fame_back", back);
        }

        private static bool WritePicked(FameFile f, InfoBox box, string picked, Action<string> pick, Action back)
        {
            Action close = () => pick(null);
            if (picked == "wealth")
            {
                box.Title(FameText.T("reallife_fame_row_wealth"));
                box.Line(FameText.Pair("reallife_fame_label_wealth", FameText.N(f.Wealth)));
                box.Line(FameText.Fill("reallife_fame_wealth_parts", "worth", FameText.Money(f.NetWorth), "week", FameText.Money(f.Weekly), "count", FameText.N(f.Businesses),
                    "properties", FameText.N(f.Properties)));
                box.Line(FameText.T("reallife_fame_wealth_about"));
            }
            else if (picked == "recognition")
            {
                box.Title(FameText.T("reallife_fame_row_recognition"));
                box.Line(FameText.Pair("reallife_fame_label_recognition", FameText.Fill("reallife_fame_recognition_value", "value", FameText.N(f.Recognition), "tier", FameText.Tier(f.Tier))));
                box.Line(FameText.Fill("reallife_fame_recognition_parts", "followers", FameText.N(f.Followers), "press", (f.Press / 10f).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)));
                box.Line(FameText.T("reallife_fame_recognition_about"));
            }
            else if (picked.StartsWith("part:", StringComparison.Ordinal))
            {
                int i = FameRules.Clamp(Number(picked, 5), 0, 3);
                box.Title(FameText.Part((FamePart)i));
                box.Line(FameText.Pair(LabelKeys[i], FameText.Rep(f.Rep[i])));
                if (i != (int)FamePart.Integrity)
                {
                    box.Line(FameText.Fill("reallife_fame_page_heading", "value", FameText.N(f.Target[i])));
                }
                box.Line(FameText.T(FameText.PartAboutKeys[i]));
            }
            else if (picked.StartsWith("district:", StringComparison.Ordinal))
            {
                int i = Number(picked, 9);
                if (i < 0 || i >= f.Districts.Count)
                {
                    return false;
                }
                FameDistrict d = f.Districts[i];
                box.Title(FameGame.DistrictName(d.Name));
                box.Line(FameText.Pair("reallife_fame_label_opinion", FameText.Rep(d.Opinion)));
                box.Line(FameText.Pair("reallife_fame_label_known", FameText.N(d.Known)));
                box.Line(FameText.Fill("reallife_fame_page_district", "shops", FameText.N(d.Shops), "jobs", FameText.N(d.Jobs), "raised", FameText.N(d.Raised)));
                box.Line(FameText.T("reallife_fame_district_about"));
            }
            else
            {
                return false;
            }
            Buttons(box, close, back);
            return true;
        }

        internal static void Buttons(InfoBox box, Action close, Action back)
        {
            UnityEngine.Transform buttons = box.ButtonRow();
            InfoBox.Button(buttons, InfoBox.Additional, "common_close", close);
            InfoBox.Button(buttons, InfoBox.Additional, "reallife_fame_back", back);
        }

        /// <summary>The number after a row id's prefix ("district:3" from 9 is 3); -1 if there is none.</summary>
        internal static int Number(string id, int from)
        {
            int value;
            return id != null && id.Length > from && int.TryParse(id.Substring(from), System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out value) ? value : -1;
        }
    }
}
