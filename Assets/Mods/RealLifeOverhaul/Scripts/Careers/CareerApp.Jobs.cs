using System.Collections.Generic;
using BAModAPI;
using Localizor;
using RealLifeOverhaul.GameUi;
using RealLifeOverhaul.Tech;
using UI;
using UI.Notification;
using UI.Smartphone;
using UnityEngine;

namespace RealLifeOverhaul.Careers
{
    /// <summary>The Career app's Jobs tab: the city's vacancies, and one picked with what it pays and asks and the chance of an offer.</summary>
    public sealed partial class CareerApp
    {
        private TableSpec WriteJobs(InfoBox box, WorkFile w, StudyFile f, int day)
        {
            TableFilter professions = CareerColumns.Jobs.Filters[0];
            professions.Names.Clear();
            Opening picked = null;
            int id = PickedId(_picked, OpeningRow), open = 0;
            Candidate template = _director.Template();
            for (int i = 0; i < w.Openings.Count; i++)
            {
                Opening o = w.Openings[i];
                Profession p = o.Def;
                if (p == null || !o.Open(day))
                {
                    continue;
                }
                TableRow row = Row(OpeningRow + o.Id);
                int points = ProRules.Points(f, p, o.Rank), need = ProRules.Need(f, p, o.Rank);
                bool qualified = ProRules.Qualified(f, p, o.Rank), eligible = qualified && _director.OpenToMe(o, template);
                bool applied = w.Applied(o) != null;
                row.Set(0, CareersText.Title(o.Prof, o.Rank));
                row.Set(1, CareersText.ProName(o.Prof));
                row.Set(2, CareersText.Employer(o.Employer));
                row.Set(3, CareersText.ViaName(o.Via), o.Via == (int)Via.Board ? TableRow.Plain : TableRow.Green);
                int pay = Prices.At(o.Pay);
                row.Set(4, CareersText.T("reallife_career_row_rate", "rate", CareersText.Rate(pay)), pay);
                row.Set(5, CareersText.WorkWay(o.Work));
                row.Set(6, CareersText.Day(o.To), o.To);
                row.Set(7, CareersText.T("reallife_career_row_fit", "points", points.ToString(), "need", need.ToString()), points - need,
                    !qualified ? TableRow.Red : points >= need ? TableRow.Green : eligible ? TableRow.Yellow : TableRow.Red);
                row.Set(8, CareersText.T(StateKey(applied, qualified, eligible)),
                    applied ? TableRow.Green : eligible ? TableRow.Plain : TableRow.Yellow);
                row.Kind = KindOf(professions, CareersText.ProName(o.Prof));
                row.Kind2 = applied ? CareerColumns.Applied : eligible ? CareerColumns.Open : CareerColumns.Short;
                open += eligible && !applied ? 1 : 0;
                picked = o.Id == id ? o : picked;
            }
            if (picked != null)
            {
                WriteOpening(box, w, f, picked, day, template);
                return CareerColumns.Jobs;
            }
            _picked = null;
            box.Title(CareersText.T("reallife_career_jobs_title"));
            box.Note(CareersText.T("reallife_career_jobs_about"));
            box.Section("reallife_ibox_sec_now");
            box.Row("reallife_career_sum_open", open.ToString(), open > 0 ? CellTone.Green : CellTone.Plain);
            if (open == 0)
            {
                box.Line(CareersText.T("reallife_career_jobs_none_open"), CellTone.Yellow);
            }
            box.Line(w.AgencyUntil >= day
                ? CareersText.T("reallife_career_jobs_agency_on", "day", CareersText.Day(w.AgencyUntil))
                : CareersText.T("reallife_career_jobs_agency_off"), w.AgencyUntil >= day ? CellTone.Green : CellTone.Plain);
            if (CareersGame.InsideType(HiringCatalogue.AgencyType))
            {
                InfoBox.Button(box.ButtonRow(), InfoBox.Positive, "reallife_career_button_register", () => Do(_director.Register));
            }
            Meta.Guide.Point(box, JobsGuide);
            return CareerColumns.Jobs;
        }

        // How a post stands for the player, in the table's last column and the box's chip.
        private static string StateKey(bool applied, bool qualified, bool eligible)
        {
            return applied ? "reallife_career_state_applied" : !qualified ? "reallife_career_state_unqualified" : eligible ? "reallife_career_state_open" : "reallife_career_state_short";
        }

        private void WriteOpening(InfoBox box, WorkFile w, StudyFile f, Opening o, int day, Candidate template)
        {
            Profession p = o.Def;
            ProRank r = p.Rank(o.Rank);
            bool applied = w.Applied(o) != null, qualified = ProRules.Qualified(f, p, o.Rank);
            bool eligible = qualified && _director.OpenToMe(o, template);
            int points = ProRules.Points(f, p, o.Rank), need = ProRules.Need(f, p, o.Rank);

            // The post, who offers it and where, and how it stands for the player (the table's last column's word and tone).
            box.Title(CareersText.Title(o.Prof, o.Rank));
            box.Subtitle(CareersText.Fill("reallife_ibox_post_sub", "employer", CareersText.Employer(o.Employer), "place", WorkGame.Address(o.Street, o.Number)));
            box.Chip(CareersText.T(StateKey(applied, qualified, eligible)),
                applied ? CellTone.Green : !qualified ? CellTone.Red : eligible ? CellTone.Plain : CellTone.Yellow);
            if (!qualified)
            {
                box.Line(CareersText.T("reallife_career_post_unqualified"), CellTone.Red);
            }
            Application turned = applied ? null : w.TurnedDown(o, day);
            if (turned != null)
            {
                box.Line(CareersDirector.TurnedDownText(turned), CellTone.Red);   // and no Apply until then (ProRules.ReapplyDays)
            }

            box.Section("reallife_ibox_sec_pay");
            box.Row("reallife_career_col_pay", CareersText.T("reallife_career_row_rate", "rate", CareersText.Rate(Prices.At(o.Pay))));
            box.Row("reallife_career_col_hours", CareersText.Week(p));
            box.Row("reallife_career_col_mode", CareersText.WorkWay(o.Work));
            box.Row("reallife_ibox_employer_kind", CareersText.Kind((EmployerKind)o.Kind));

            box.Section("reallife_ibox_sec_asks");
            int a, b;
            p.SkillsOf(o.Rank, out a, out b);
            box.MeterWords(CareersText.Fill("reallife_ibox_skills", "a", CareersText.Skill(a), "b", CareersText.Skill(b)), LookRules.Fill(points, need),
                CareersText.T("reallife_career_row_fit", "points", points.ToString(), "need", need.ToString()),
                !qualified ? CellTone.Red : points >= need ? CellTone.Green : eligible ? CellTone.Yellow : CellTone.Red);
            box.Note(CareersText.T("reallife_ibox_first_counts"));
            string needs = Needs(p, o.Rank);
            if (needs.Length > 0)
            {
                box.Check(CareersText.T("reallife_career_post_needs", "needs", needs), qualified);
            }
            if (qualified && ProRules.SelfTaught(p) && o.Rank == 0 && r.Quals.Length > 0 && ProRules.Need(f, p, 0) > r.Need)
            {
                box.Line(CareersText.T("reallife_career_post_self_taught", "extra", ProRules.SelfTaughtExtra.ToString()), CellTone.Yellow);
            }

            // The chance the app works out for an offer (ProPath.OfferChance), and a friend who puts the player's name forward.
            bool chanceShown = !applied && turned == null && qualified && o.Via != (int)Via.Headhunter;
            if (chanceShown || o.Via == (int)Via.Referral)
            {
                box.Section("reallife_ibox_sec_chance");
            }
            if (chanceShown)
            {
                Candidate c = _director.CandidateFor(o, template);
                int chance = ProPath.OfferChance(ProRules.HireScore(c));
                if (chance > 0)
                {
                    box.Meter("reallife_ibox_chance", chance / 100f, CareersText.T("reallife_ibox_about", "value", CareersText.Percent(chance)),
                        chance >= 50 ? CellTone.Green : chance >= 20 ? CellTone.Plain : CellTone.Yellow);
                }
                else
                {
                    box.Line(CareersText.T("reallife_career_post_no_chance", "points", ProPath.PointsShort(c).ToString()), CellTone.Red);
                }
                int more = ProPath.PointsFor(c, ProPath.GoodChance);   // how far it is to an even chance or better
                box.Row("reallife_career_good_chance", more == 0 ? CareersText.T("reallife_career_good_chance_have") : more > 100 ? CareersText.T("reallife_career_good_chance_far")
                    : more == 1 ? CareersText.T("reallife_career_good_chance_point") : CareersText.T("reallife_career_good_chance_points", "points", more.ToString()),
                    more == 0 ? CellTone.Green : CellTone.Plain);
            }
            if (o.Via == (int)Via.Referral)
            {
                box.Line(CareersText.T("reallife_career_post_referral"), CellTone.Green);
            }

            box.Section("reallife_ibox_sec_dates");
            box.Row("reallife_career_col_closes", CareersText.Day(o.To));
            Transform buttons = box.ButtonRow();
            int id = o.Id;
            if (!applied && turned == null)
            {
                InfoBox.Button(buttons, InfoBox.Positive, "reallife_career_button_apply", () => Do(() => _director.Apply(id)));
            }
            InfoBox.Button(buttons, InfoBox.Additional, "common_close", ClosePick);
            Meta.Guide.Point(box, JobsGuide);
        }

        /// <summary>What a rank needs, in words: the courses of the last rung that names some (any one of them), and the certificates up to it.</summary>
        private static string Needs(Profession p, int rank)
        {
            var b = new System.Text.StringBuilder(96);
            int last = -1;
            for (int r = 0; r <= rank && r < p.Ranks.Length; r++)
            {
                last = p.Ranks[r].Quals.Length > 0 ? r : last;
            }
            if (last >= 0)
            {
                int[] quals = p.Ranks[last].Quals;
                for (int i = 0; i < quals.Length; i++)
                {
                    if (i > 0)
                    {
                        b.Append(' ').Append(CareersText.T("reallife_campus_or")).Append(' ');
                    }
                    b.Append(CareersText.Course(quals[i]));
                }
            }
            int seen = 0;
            for (int r = 0; r <= rank && r < p.Ranks.Length; r++)
            {
                int cert = p.Ranks[r].Cert;
                if (cert >= 0 && (seen & (1 << cert)) == 0)
                {
                    seen |= 1 << cert;
                    if (b.Length > 0)
                    {
                        b.Append(", ");
                    }
                    b.Append(TechText.Cert(cert));
                }
            }
            return b.ToString();
        }
    }
}
