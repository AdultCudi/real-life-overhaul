using System.Globalization;
using RealLifeOverhaul.GameUi;
using UnityEngine;

namespace RealLifeOverhaul.Hustles
{
    /// <summary>The Freelance tab: the visits clients ask for today, booked or left to a helper, and how the player is rated.</summary>
    public sealed partial class HustleApp
    {
        public const string GigRow = "gig:";

        private TableSpec WriteFreelance(InfoBox box, HustleState state)
        {
            int pickedId = Picked(_picked, GigRow);
            Gig picked = null;
            for (int i = 0; i < state.Gigs.Count; i++)
            {
                Gig g = state.Gigs[i];
                GigDef def = HustleCatalogue.Gig(g.Kind);
                TableRow row = Row(GigRow + g.Id);
                row.Kind = (int)g.Kind;
                row.Set(0, g.Client);
                row.Set(1, HustleText.Gig(g.Kind));
                HustlePlace where = HustleRules.Where(g);
                row.Set(2, def.TwoStops && g.Stage != GigStage.Loaded
                    ? HustleText.T("reallife_hustle_two_places", "from", Where(g.Street, g.Number), "to", Where(g.Street2, g.Number2))
                    : Where(where.Street, where.Number));
                Money(row, 3, g.Pay);
                row.Set(4, HustleText.T("reallife_hustle_window", "from", HustleText.Clock(g.From), "until", HustleText.Clock(g.Until)), g.From);
                row.Set(5, def.Van ? HustleText.T("reallife_hustle_needs_van") : HustleText.T("reallife_hustle_needs_nothing"));
                row.Set(6, GigState(g), GigTone(g));
                picked = g.Id == pickedId ? g : picked;
            }
            if (picked != null)
            {
                WriteGig(box, state, picked);
                return HustleColumns.Freelance;
            }
            _picked = null;
            box.Title(HustleText.T("reallife_hustle_freelance_title"));
            box.Section("reallife_ibox_sec_now");
            box.Row("reallife_hustle_row_rating", state.Ratings.Count > 0
                ? HustleText.T("reallife_hustle_rating", "stars", HustleText.Stars(state.Rating), "count", state.Ratings.Count.ToString(CultureInfo.InvariantCulture))
                : HustleText.T("reallife_hustle_rating_none"));
            box.Row("reallife_hustle_row_done", state.JobsDone.ToString(CultureInfo.InvariantCulture));
            box.Row("reallife_hustle_row_earned", HustleText.Money(state.Earned));
            box.Note(HustleText.T("reallife_hustle_freelance_about"));
            Meta.Guide.Point(box, GuideTitle);
            return HustleColumns.Freelance;
        }

        private static string GigState(Gig g)
        {
            switch (g.Stage)
            {
                case GigStage.Booked: return HustleText.T("reallife_hustle_state_booked");
                case GigStage.Helper: return HustleText.T("reallife_hustle_state_helper", "time", HustleText.Clock(g.HelperDone));
                case GigStage.Loaded: return HustleText.T("reallife_hustle_state_loaded");
                case GigStage.Done: return HustleText.T("reallife_hustle_state_done", "stars", g.Rating.ToString(CultureInfo.InvariantCulture));
                case GigStage.Missed: return HustleText.T("reallife_hustle_state_missed");
                case GigStage.Cancelled: return HustleText.T("reallife_hustle_state_cancelled");
                default: return HustleText.T("reallife_hustle_state_asked");
            }
        }

        private static CellTone GigTone(Gig g)
        {
            switch (g.Stage)
            {
                case GigStage.Booked:
                case GigStage.Loaded:
                case GigStage.Helper:
                    return TableRow.Yellow;
                case GigStage.Done: return TableRow.Green;
                case GigStage.Missed:
                case GigStage.Cancelled:
                    return TableRow.Red;
                default: return TableRow.Plain;
            }
        }

        private void WriteGig(InfoBox box, HustleState state, Gig g)
        {
            GigDef def = HustleCatalogue.Gig(g.Kind);
            int id = g.Id;
            box.Title(HustleText.Gig(g.Kind));
            box.Subtitle(HustleText.T("reallife_hustle_gig_for", "client", g.Client));
            box.Chip(GigState(g), GigTone(g) == TableRow.Green ? CellTone.Green : GigTone(g) == TableRow.Red ? CellTone.Red : CellTone.Plain);
            box.Section("reallife_ibox_sec_money");
            box.Row("reallife_hustle_row_pay", HustleText.Money(g.Pay));
            box.Row("reallife_hustle_row_helper_fee", HustleText.Money(HustleRules.HelperFee(g.Pay)));
            box.Section("reallife_ibox_sec_where");
            box.Line(def.TwoStops
                ? HustleText.T("reallife_hustle_gig_two", "from", Where(g.Street, g.Number), "to", Where(g.Street2, g.Number2))
                : HustleText.T("reallife_hustle_gig_one", "place", Where(g.Street, g.Number)));
            box.Line(HustleText.T("reallife_hustle_gig_when", "from", HustleText.Clock(g.From), "until", HustleText.Clock(g.Until), "hours",
                (def.Minutes / 60).ToString(CultureInfo.InvariantCulture), "minutes", (def.Minutes % 60).ToString("00", CultureInfo.InvariantCulture)));
            box.Note(HustleText.T(def.Van ? "reallife_hustle_gig_van_note" : "reallife_hustle_gig_note", "skill",
                _director.SkillFor(def).ToString(CultureInfo.InvariantCulture)));
            Transform buttons = box.ButtonRow();
            switch (g.Stage)
            {
                case GigStage.Offered:
                    InfoBox.Button(buttons, InfoBox.Positive, "reallife_hustle_button_book", () => _director.Book(id));
                    InfoBox.Button(buttons, InfoBox.Negative, "reallife_hustle_button_helper", () => SendHelper(g));
                    break;
                case GigStage.Booked:
                case GigStage.Loaded:
                {
                    HustlePlace where = HustleRules.Where(g);
                    InfoBox.Button(buttons, InfoBox.Positive, "reallife_hustle_button_way", () => _director.ShowWay(where.Street, where.Number));
                    if (g.Stage == GigStage.Booked)
                    {
                        InfoBox.Button(buttons, InfoBox.Negative, "reallife_hustle_button_helper", () => SendHelper(g));
                    }
                    InfoBox.Button(box.ButtonRow(), InfoBox.Negative, "reallife_hustle_button_cancel", () => Confirm("reallife_hustle_q_cancel_title",
                        HustleText.T("reallife_hustle_q_cancel", "client", g.Client, "stars", HustleCatalogue.CancelRating.ToString(CultureInfo.InvariantCulture)), () =>
                        {
                            _director.Cancel(id);
                            Refresh();
                        }, true));
                    break;
                }
            }
            InfoBox.Button(box.ButtonRow(), InfoBox.Additional, "common_close", ClosePick);
            Meta.Guide.Point(box, GuideTitle);
        }

        private void SendHelper(Gig g)
        {
            int id = g.Id;
            Confirm("reallife_hustle_q_helper_title", HustleText.T("reallife_hustle_q_helper", "client", g.Client, "fee", HustleText.Money(HustleRules.HelperFee(g.Pay)),
                "pay", HustleText.Money(g.Pay)), () =>
            {
                _director.Send(id);
                Refresh();
            }, false);
        }
    }
}
