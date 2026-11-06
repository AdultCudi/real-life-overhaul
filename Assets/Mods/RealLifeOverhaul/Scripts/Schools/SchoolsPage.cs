using System;
using System.Collections.Generic;
using System.Globalization;
using RealLifeOverhaul.GameUi;

namespace RealLifeOverhaul.Schools
{
    /// <summary>
    /// The Campus app's Schools tab (the app is Careers'; it asks this page through CareersBridges.SchoolsTab): the city's
    /// schools in the league table's order, the box with the school picked, and Birchwood School's page, from which the player
    /// buys it, runs it with the levers the trusts pull, takes or leaves an offer, and enrols their children. Written only when
    /// the app opens or changes, in the game's own frame and box (GameUi).
    /// </summary>
    public static class SchoolsPage
    {
        public const string RowPrefix = "school:";
        public const int KindPrivate = 0, KindCouncil = 1;
        private const string Guide = "reallife_meta_title_schools";
        private static readonly CultureInfo Plain = CultureInfo.InvariantCulture;

        /// <summary>Schools: 0 school, 1 district, 2 run by, 3 fee, 4 pupils, 5 results, 6 inspection, 7 rank.</summary>
        public static readonly TableSpec Columns = new TableSpec("Campus", "Schools")
            .Column(SchoolsText.ColSchool, 330f, TableSort.Text, flexible: true)
            .Column(SchoolsText.ColDistrict, 230f, TableSort.Text)
            .Column(SchoolsText.ColOwner, 280f, TableSort.Text)
            .Column(SchoolsText.ColFee, 170f, TableSort.Money)
            .Column(SchoolsText.ColPupils, 200f, TableSort.Number)
            .Column(SchoolsText.ColResults, 150f, TableSort.Number)
            .Column(SchoolsText.ColGrade, 250f, TableSort.Text)
            .Column(SchoolsText.ColRank, 130f, TableSort.Number, right: true)
            .Filter(SchoolsText.AllKinds, new[] { SchoolsText.KindPrivate, SchoolsText.KindCouncil })
            .Empty(SchoolsText.Empty);

        private static readonly TableSpec Off = new TableSpec("Campus", "SchoolsOff").Column(SchoolsText.ColSchool, 600f, TableSort.None, flexible: true);

        /// <summary>
        /// The tab written: its rows through <paramref name="row"/>, its box for the row picked; a press goes through
        /// <paramref name="act"/>, which says why not and writes the app again.
        /// </summary>
        public static TableSpec Write(InfoBox box, Func<string, TableRow> row, string picked, Action<Func<string>> act)
        {
            SchoolState s = SchoolsDirector.State;
            if (!SchoolsDirector.Running || s == null)
            {
                box.Title(SchoolsText.T(SchoolsText.BoxTitle));
                box.Line(SchoolsText.T(Settings.Schools ? SchoolsText.BoxNotYet : SchoolsText.BoxOff), CellTone.Yellow);
                Meta.Guide.Point(box, Guide);
                return Off;
            }
            int[] order = SchoolRules.League(s);
            int chosen = -1;
            for (int r = 0; r < order.Length; r++)
            {
                SchoolFile f = s.Files[order[r]];
                SchoolDef d = SchoolRules.Def(f.Id);
                TableRow t = row(RowPrefix + f.Id.ToString(Plain));
                bool council = d.Kind == SchoolKind.Council;
                t.Set(0, SchoolsGame.Name(f.Id), f.Id == SchoolRules.CampusId ? TableRow.Green : TableRow.Plain);
                t.Set(1, SchoolsGame.District(d.District));
                t.Set(2, SchoolsText.Owner(f.Owner), f.Owner == SchoolOwner.Player ? TableRow.Green : TableRow.Plain);
                int fee = council ? 0 : SchoolsGame.Today(f.Fee);
                t.Set(3, council ? SchoolsText.T(SchoolsText.KindCouncil) : SchoolsText.Money(fee), fee);
                t.Set(4, f.Pupils.ToString(Plain) + " / " + f.Places.ToString(Plain), f.Pupils);
                t.Set(5, f.Results >= 0 ? f.Results.ToString(Plain) + "%" : "-", f.Results);
                t.Set(6, SchoolsText.Grade(f.Grade), Tone(f.Grade));
                t.Set(7, f.Results >= 0 ? (r + 1).ToString(Plain) : "-", r + 1);
                t.Kind = council ? KindCouncil : KindPrivate;
                t.Kind2 = -1;
                if (picked == RowPrefix + f.Id.ToString(Plain))
                {
                    chosen = f.Id;
                }
            }
            if (chosen < 0)
            {
                Overview(box, s);
            }
            else
            {
                School(box, s, chosen, Array.IndexOf(order, chosen) + 1, act);
            }
            return Columns;
        }

        private static CellTone Tone(int grade)
        {
            switch ((SchoolGrade)grade)
            {
                case SchoolGrade.Outstanding:
                case SchoolGrade.Good:
                    return CellTone.Green;
                case SchoolGrade.Improve:
                    return CellTone.Yellow;
                case SchoolGrade.Inadequate:
                    return CellTone.Red;
                default:
                    return CellTone.Plain;
            }
        }

        // Nothing picked: what the tab is, who runs Birchwood School, the player's children there.
        private static void Overview(InfoBox box, SchoolState s)
        {
            box.Title(SchoolsText.T(SchoolsText.BoxTitle));
            box.Note(SchoolsText.T(SchoolsText.BoxAbout));
            box.Section(SchoolsText.SecFigures);
            box.Row(SchoolsText.RowOwner, SchoolsText.Owner(s.Campus.Owner), s.Campus.Owner == SchoolOwner.Player ? CellTone.Green : CellTone.Plain);
            box.Row(SchoolsText.RowPupils, s.Campus.Pupils.ToString(Plain) + " / " + s.Campus.Places.ToString(Plain));
            box.Row(SchoolsText.RowFee, SchoolsText.Money(SchoolsGame.Today(s.Campus.Fee)));
            Meta.Guide.Point(box, Guide);
        }

        // A school picked: its figures; Birchwood School's also its running, its sale and the player's children.
        private static void School(InfoBox box, SchoolState s, int id, int rank, Action<Func<string>> act)
        {
            SchoolFile f = s.Files[id];
            SchoolDef d = SchoolRules.Def(id);
            bool council = d.Kind == SchoolKind.Council;
            bool mine = f.Owner == SchoolOwner.Player;
            box.Title(SchoolsGame.Name(id));
            box.Subtitle(SchoolsGame.District(d.District) + ", " + SchoolsText.Owner(f.Owner));
            box.Chip(SchoolsText.Grade(f.Grade), Tone(f.Grade));
            box.Section(SchoolsText.SecFigures);
            box.Row(SchoolsText.RowPupils, f.Pupils.ToString(Plain) + " / " + f.Places.ToString(Plain));
            if (!council)
            {
                box.Row(SchoolsText.RowWaiting, f.Waiting.ToString(Plain), f.Waiting > 0 ? CellTone.Green : CellTone.Plain);
                box.Row(SchoolsText.RowFee, SchoolsText.Money(SchoolsGame.Today(f.Fee)));
                box.Row(SchoolsText.RowScholar, f.Scholar.ToString(Plain) + "%");
            }
            box.Row(SchoolsText.RowName, (f.Reputation / 10).ToString(Plain) + " / 100");
            box.Row(SchoolsText.RowResults, f.Results >= 0 ? f.Results.ToString(Plain) + "%" : "-");
            box.Row(SchoolsText.RowRank, f.Results >= 0 ? SchoolsText.Rank(rank, SchoolRules.Count) : "-");
            box.Row(SchoolsText.RowTeaching, SchoolRules.Teaching(f).ToString(Plain) + " / 100");
            box.Section(SchoolsText.SecStaff);
            box.Row(SchoolsText.RowTeachers, f.Teachers.ToString(Plain) + " (" + SchoolRules.Need(f).ToString(Plain) + ")");
            box.Row(SchoolsText.RowPay, (f.PayBp / 100).ToString(Plain) + "%", f.PayBp >= 10000 ? CellTone.Plain : CellTone.Yellow);
            box.Row(SchoolsText.RowHead, SchoolsGame.Person(f.Head) + " (" + f.HeadSkill.ToString(Plain) + ")");
            if (id != SchoolRules.CampusId)
            {
                return;
            }
            box.Row(SchoolsText.RowDay, SchoolsText.Money(Signed(SchoolRules.Profit(f))), SchoolRules.Profit(f) >= 0 ? CellTone.Green : CellTone.Red);
            if (mine)
            {
                Running(box, f, act);
            }
            Trade(box, s, f, act);
            Children(box, s, act);
        }

        private static long Signed(long startPrices) => startPrices >= 0 ? SchoolsGame.Today(startPrices) : -SchoolsGame.Today(-startPrices);

        // The player's own school: the levers the trusts pull by the rules, each a step within its bounds.
        private static void Running(InfoBox box, SchoolFile f, Action<Func<string>> act)
        {
            box.Section(SchoolsText.SecRunning);
            box.Line(SchoolsText.T(SchoolsText.LineYours), CellTone.Green);
            Pair(box, SchoolsText.ButtonFeeUp, () => SchoolsDirector.Fee(+1), SchoolsText.ButtonFeeDown, () => SchoolsDirector.Fee(-1), act);
            Pair(box, SchoolsText.ButtonClassOpen, () => SchoolsDirector.Class(+1), SchoolsText.ButtonClassClose, () => SchoolsDirector.Class(-1), act);
            Pair(box, SchoolsText.ButtonPayUp, () => SchoolsDirector.Pay(+1), SchoolsText.ButtonPayDown, () => SchoolsDirector.Pay(-1), act);
            Pair(box, SchoolsText.ButtonScholarUp, () => SchoolsDirector.Scholar(+1), SchoolsText.ButtonScholarDown, () => SchoolsDirector.Scholar(-1), act);
            int week = SchoolsGame.Day / 7;
            for (int n = 0; n < 3; n++)
            {
                int candidate = n;
                SchoolRules.Candidate(SchoolRules.CampusId, week, n, out int seed, out int skill);
                box.Line(SchoolsText.Fill(SchoolsText.LineCandidate, "name", SchoolsGame.Person(seed), "skill", skill.ToString(Plain),
                    "cost", SchoolsText.Money(SchoolsGame.Today(SchoolRules.HireCost(skill)))), skill > f.HeadSkill ? CellTone.Green : CellTone.Plain);
                InfoBox.Button(box.ButtonRow(), InfoBox.Additional, SchoolsText.ButtonHead, () => act(() => SchoolsDirector.Head(candidate)));
            }
        }

        // Buying, selling and a trust's offer; never in a Going Public session.
        private static void Trade(InfoBox box, SchoolState s, SchoolFile f, Action<Func<string>> act)
        {
            box.Section(SchoolsText.SecTrade);
            box.Row(SchoolsText.RowWorth, SchoolsText.Money(SchoolsGame.Today(SchoolRules.Valuation(f))));
            if (SchoolsGame.InSession)
            {
                box.Line(SchoolsText.T(SchoolsText.LineSession), CellTone.Yellow);
                return;
            }
            if (f.Owner != SchoolOwner.Player)
            {
                box.Line(SchoolsText.Fill(SchoolsText.LineBuy, "price", SchoolsText.Money(SchoolsGame.Today(SchoolRules.Asking(f)))), CellTone.Plain);
                InfoBox.Button(box.ButtonRow(), InfoBox.Positive, SchoolsText.ButtonBuy, () => act(SchoolsDirector.Buy));
                return;
            }
            if (s.OfferPrice > 0 && SchoolOwner.IsTrust(s.OfferBy))
            {
                box.Line(SchoolsText.Fill(SchoolsText.LineOffer, "trust", SchoolsText.Trust(s.OfferBy), "price", SchoolsText.Money(SchoolsGame.Today(s.OfferPrice * 1000L)),
                    "day", s.OfferEnd.ToString(Plain)), CellTone.Green);
                InfoBox.Button(box.ButtonRow(), InfoBox.Positive, SchoolsText.ButtonAccept, () => act(SchoolsDirector.Accept));
            }
            box.Line(SchoolsText.Fill(SchoolsText.LineSell, "price", SchoolsText.Money(SchoolsGame.Today(SchoolRules.SalePrice(f)))), CellTone.Plain);
            InfoBox.Button(box.ButtonRow(), InfoBox.Negative, SchoolsText.ButtonSell, () => act(SchoolsDirector.Sell));
        }

        // The player's children: each at Birchwood School or not, with the button that moves them; the way there.
        private static void Children(InfoBox box, SchoolState s, Action<Func<string>> act)
        {
            box.Section(SchoolsText.SecChildren);
            List<SchoolChild> kids = SchoolsDirector.Children();
            int shown = 0;
            for (int i = 0; i < kids.Count; i++)
            {
                SchoolChild k = kids[i];
                if (k.Grown || k.Age < SchoolRules.SchoolFrom || k.Age >= SchoolRules.SchoolTo)
                {
                    continue;
                }
                shown++;
                string name = k.Name;
                bool there = s.Children.Contains(SchoolStore.Clean(name));
                box.Line(SchoolsText.Fill(there ? SchoolsText.LineChildAt : SchoolsText.LineChildAway, "child", name), there ? CellTone.Green : CellTone.Plain);
                InfoBox.Button(box.ButtonRow(), there ? InfoBox.Negative : InfoBox.Positive, there ? SchoolsText.ButtonLeave : SchoolsText.ButtonEnrol,
                    () => act(() => there ? SchoolsDirector.Leave(name) : SchoolsDirector.Enrol(name)));
            }
            if (shown == 0)
            {
                box.Line(SchoolsText.T(SchoolsText.LineNoChildren), CellTone.Plain);
            }
            else if (!SchoolRules.HasPlace(s.Campus))
            {
                box.Line(SchoolsText.T(SchoolsText.LineFull), CellTone.Yellow);
            }
            InfoBox.Button(box.ButtonRow(), InfoBox.Additional, SchoolsText.ButtonGo, () => act(SchoolsDirector.Go));
        }

        // Two buttons on one row: a lever's step up and down.
        private static void Pair(InfoBox box, string up, Func<string> upAct, string down, Func<string> downAct, Action<Func<string>> act)
        {
            UnityEngine.Transform buttons = box.ButtonRow();
            InfoBox.Button(buttons, InfoBox.Positive, up, () => act(upAct));
            InfoBox.Button(buttons, InfoBox.Additional, down, () => act(downAct));
        }
    }
}
