using System;
using System.Globalization;
using RealLifeOverhaul.GameUi;

namespace RealLifeOverhaul.Fame
{
    /// <summary>
    /// The Standing page in the Finance app's frame: a table of the parts, districts, changes, invitations, promises,
    /// requests and the council, and the white box for the row picked. Written on a click, never per frame.
    /// </summary>
    public static partial class FamePage
    {
        private const int KindPart = 0, KindDistrict = 1, KindChange = 2, KindInvite = 3, KindPromise = 4, KindRequest = 5, KindCouncil = 6;
        private const string GuideTitle = "reallife_meta_title_standing";

        private static readonly string[] Kinds =
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
                box.Line(FameText.T("reallife_fame_off"), CellTone.Yellow);
                InfoBox.Button(box.ButtonRow(), InfoBox.Additional, "reallife_fame_back", back);
                Meta.Guide.Point(box, GuideTitle);
                return;
            }
            int day = FameGame.Day;
            Rows(f, row);
            PressRows(f, row);
            CityRows(f, row, day);
            bool written = picked != null
                           && (WritePicked(f, box, picked, pick, back) || PressBox(director, f, box, picked, pick, back) || CityBox(director, f, box, picked, pick, back));
            if (!written)
            {
                WriteSummary(f, box, back);
            }
        }

        private static void Rows(FameFile f, Func<string, TableRow> row)
        {
            string kind = FameText.T(Kinds[KindPart]);
            TableRow r = row("wealth");
            Cells(r, KindPart, FameText.T("reallife_fame_row_wealth"), kind, f.Wealth, FameText.N(f.Wealth), null, 0,
                FameText.Fill("reallife_fame_row_worth", "worth", FameText.Money(f.NetWorth)), f.NetWorth);
            r = row("recognition");
            Cells(r, KindPart, FameText.T("reallife_fame_row_recognition"), kind, f.Recognition, FameText.N(f.Recognition), FameText.Tier(f.Tier), (int)f.Tier,
                FameText.Fill("reallife_fame_row_followers", "count", FameText.N(f.Followers)), f.Followers);
            for (int i = 0; i < 4; i++)
            {
                r = row("part:" + i);
                FameNote last = LastNote(f, i);
                string heading = i == (int)FamePart.Integrity ? FameText.T("reallife_fame_heading_deeds") : FameText.Fill("reallife_fame_heading", "value", FameText.N(f.Target[i]));
                Cells(r, KindPart, FameText.Part((FamePart)i), kind, f.Rep[i], FameText.Rep(f.Rep[i]), heading, f.Target[i],
                    last != null ? FameText.Cause(last.Cause) : string.Empty, last != null ? last.Day : 0);
                r.Tones[2] = (byte)Tone(f.Rep[i]);
            }
            kind = FameText.T(Kinds[KindDistrict]);
            for (int i = 0; i < f.Districts.Count; i++)
            {
                FameDistrict d = f.Districts[i];
                r = row("district:" + i);
                Cells(r, KindDistrict, FameGame.DistrictName(d.Name), kind, d.Opinion, FameText.Rep(d.Opinion), FameText.Fill("reallife_fame_known_by", "value", FameText.N(d.Known)),
                    d.Known, FameText.Fill("reallife_fame_district_jobs", "jobs", FameText.N(d.Jobs), "raised", FameText.N(d.Raised)), d.Jobs - d.Raised * 5);
                r.Tones[2] = (byte)Tone(d.Opinion);
            }
            kind = FameText.T(Kinds[KindChange]);
            for (int i = f.Notes.Count - 1; i >= 0; i--)
            {
                FameNote n = f.Notes[i];
                r = row("note:" + i);
                bool press = n.Part == FameNote.Recognition;
                string what = press ? FameText.T("reallife_fame_row_recognition") : FameText.Part((FamePart)FameRules.Clamp(n.Part, 0, 3));
                string amount = press
                    ? FameText.Fill("reallife_fame_press_change", "value", (n.Delta / 10f).ToString("+0.0;-0.0", CultureInfo.InvariantCulture))
                    : FameText.Signed(n.Delta);
                Cells(r, KindChange, FameText.Cause(n.Cause) + (n.District != null ? ", " + FameGame.DistrictName(n.District) : string.Empty), kind, n.Delta, amount, what, 0,
                    FameText.Fill("reallife_fame_day", "day", FameText.N(n.Day)), n.Day);
                r.Pick = false;
                r.Tones[2] = (byte)(n.Delta > 0 ? TableRow.Green : TableRow.Red);
            }
        }

        private static void Cells(TableRow r, int kind, string what, string kindName, double stands, string standsWords, string heading, double headingKey, string note, double noteKey)
        {
            r.Kind = kind;
            r.Pick = true;
            r.Set(0, what);
            r.Set(1, kindName);
            r.Set(2, standsWords, stands);
            r.Set(3, heading ?? string.Empty, headingKey);
            r.Set(4, note ?? string.Empty, noteKey);
        }

        private static CellTone Tone(int value)
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

        private static void WriteSummary(FameFile f, InfoBox box, Action back)
        {
            box.Title(FameText.T("reallife_fame_title"));
            string profile = FameText.Profile(FameRules.Profile(f.Wealth, f.Recognition, f.Overall));
            box.Chip(profile);
            if (FameCity.InScandal(f, FameGame.Day))
            {
                box.Line(FameText.Fill("reallife_fame_scandal_line", "day", FameText.N(f.ScandalUntil)), CellTone.Red);
            }
            box.Section("reallife_ibox_sec_now");
            box.Row("reallife_fame_label_profile", profile);
            box.Row("reallife_fame_label_reputation", FameText.Rep(f.Overall));
            box.Row("reallife_fame_label_wealth", FameText.N(f.Wealth));
            box.Row("reallife_fame_label_recognition", RecognitionValue(f));
            box.Row("reallife_fame_label_influence", FameText.N(FameCity.Influence(f)));
            box.Headline("reallife_fame_head_reputation");
            for (int i = 0; i < 4; i++)
            {
                box.Row(LabelKeys[i], FameText.Rep(f.Rep[i]));
            }
            InfoBox.Button(box.ButtonRow(), InfoBox.Additional, "reallife_fame_back", back);
            Meta.Guide.Point(box, GuideTitle);
        }

        private static string RecognitionValue(FameFile f)
        {
            return FameText.Fill("reallife_fame_recognition_value", "value", FameText.N(f.Recognition), "tier", FameText.Tier(f.Tier));
        }

        // The wealth, recognition, a part or a district picked; false for anything else.
        private static bool WritePicked(FameFile f, InfoBox box, string picked, Action<string> pick, Action back)
        {
            string about;
            if (picked == "wealth")
            {
                box.Title(FameText.T("reallife_fame_row_wealth"));
                box.Section("reallife_ibox_sec_now");
                box.Row("reallife_fame_label_wealth", FameText.N(f.Wealth));
                box.Line(FameText.Fill("reallife_fame_wealth_parts", "worth", FameText.Money(f.NetWorth), "week", FameText.Money(f.Weekly), "count", FameText.N(f.Businesses),
                    "properties", FameText.N(f.Properties)));
                about = FameText.T("reallife_fame_wealth_about");
            }
            else if (picked == "recognition")
            {
                box.Title(FameText.T("reallife_fame_row_recognition"));
                box.Section("reallife_ibox_sec_now");
                box.Row("reallife_fame_label_recognition", RecognitionValue(f));
                box.Line(FameText.Fill("reallife_fame_recognition_parts", "followers", FameText.N(f.Followers), "press",
                    (f.Press / 10f).ToString("0.0", CultureInfo.InvariantCulture)));
                about = FameText.T("reallife_fame_recognition_about");
            }
            else if (picked.StartsWith("part:", StringComparison.Ordinal))
            {
                int i = FameRules.Clamp(Number(picked, 5), 0, 3);
                box.Title(FameText.Part((FamePart)i));
                box.Section("reallife_ibox_sec_now");
                box.Row(LabelKeys[i], FameText.Rep(f.Rep[i]));
                if (i != (int)FamePart.Integrity)
                {
                    box.Line(FameText.Fill("reallife_fame_page_heading", "value", FameText.N(f.Target[i])));
                }
                about = FameText.T(FameText.PartAboutKeys[i]);
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
                box.Section("reallife_ibox_sec_now");
                box.Row("reallife_fame_label_opinion", FameText.Rep(d.Opinion));
                box.Row("reallife_fame_label_known", FameText.N(d.Known));
                box.Line(FameText.Fill("reallife_fame_page_district", "shops", FameText.N(d.Shops), "jobs", FameText.N(d.Jobs), "raised", FameText.N(d.Raised)));
                about = FameText.T("reallife_fame_district_about");
            }
            else
            {
                return false;
            }
            Buttons(box, pick, back, about);
            return true;
        }

        /// <summary>The foot of a picked row's page: close and back, then what it is (null for nothing more) and the Guide's page.</summary>
        private static void Buttons(InfoBox box, Action<string> pick, Action back, string about = null)
        {
            UnityEngine.Transform buttons = box.ButtonRow();
            InfoBox.Button(buttons, InfoBox.Additional, "common_close", () => pick(null));
            InfoBox.Button(buttons, InfoBox.Additional, "reallife_fame_back", back);
            if (!string.IsNullOrEmpty(about))
            {
                box.Line(about);
            }
            Meta.Guide.Point(box, GuideTitle);
        }

        /// <summary>The number after a row id's prefix ("district:3" from 9 is 3); -1 if there is none.</summary>
        private static int Number(string id, int from)
        {
            int value;
            return id != null && id.Length > from && int.TryParse(id.Substring(from), NumberStyles.Integer, CultureInfo.InvariantCulture, out value) ? value : -1;
        }
    }
}
