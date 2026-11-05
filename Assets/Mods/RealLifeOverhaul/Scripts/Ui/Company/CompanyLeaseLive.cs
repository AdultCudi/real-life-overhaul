using System;
using System.Collections.Generic;
using RealLifeOverhaul.Economy;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The lease question's figures, choices and headline, built from the escalation and its lease with no game type in it, so that a check presses the very card the screen shows (the file's
    /// name keeps it out of the preview's copy: it stands on the Economy folder). The rent paid now is the escalation's own A (an agreed rise and a hold-over premium in it), never the game's
    /// base rent: the landlord's offer is measured against what is paid. Cost: a few small arrays when a question is read; nothing per frame.
    /// </summary>
    internal static class CompanyLeaseLive
    {
        internal static void Fill(CompanyQuestion q, CompanyWords w, Escalation e, LeaseFile l, Func<int, int> monthly, Func<int, string> money, Func<int, string> share,
            int daysPerYear, int monthDays, int agree, int haggle, int refuse)
        {
            int rent = e.A, offered = e.B, days = e.C;   // the rent paid now (an agreed rise and a hold-over premium in it), never the game's base rent: the offer is measured against what is paid
            string business = e.Detail;
            q.Title = w.T("reallife_co_q_lease_title", "A lease is up for renewal");
            int rise = e.A > 0 ? (int)((e.B - e.A) * 1000L / e.A) : 0;
            q.FactLabels = new[] { w.T("reallife_co_q_rent_now", "Rent now"), w.T("reallife_co_q_rent_offer", "Offered"), w.T("reallife_co_q_term", "New term"), w.T("reallife_co_q_rise_limit",
                "Rise") };
            q.FactValues = new[] { money(monthly(rent)), money(monthly(offered)), l != null ? w.F("reallife_co_q_months", "{count} months", "count", Math.Max(1, l.Term / Math.Max(1,
                monthDays)).ToString()) : "-", share(rise) };
            int yearMore, yearLess;
            LeaseRules.YearChange(rent, offered, daysPerYear, out yearMore, out yearLess);
            var choices = new List<CompanyChoice>(3)
            {
                new CompanyChoice
                {
                    Name = w.T("reallife_co_q_c_renew", "Renew at the offered rent"), Button = w.T("reallife_co_q_renew_do", "Renew the lease"), Note = w.T("reallife_co_q_standing_note", "standing"),
                    Act = CompanyAct.Of(CompanyDo.LeaseAnswer, e.Place, agree),
                    Lines = new[] { new CompanyConsequence { Text = rise > 0 ? w.F("reallife_co_q_rent_year", "About {amount} a year more in rent", "amount", money(yearMore))
                        : rise < 0 ? w.F("reallife_co_q_rent_less", "About {amount} a year less in rent", "amount", money(yearLess))
                        : w.T("reallife_co_q_rent_same", "The rent stays as it is"), Tone = rise > 0 ? UiConsequence.Loss : rise < 0 ? 1 : 0, Change = rise > 0 ? 1 : rise < 0 ? -1 : 0 } }
                },
                new CompanyChoice
                {
                    Name = w.T("reallife_co_q_c_haggle", "Haggle for a smaller rise"), Button = w.T("reallife_co_q_haggle_do", "Haggle"), Note = w.T("reallife_co_q_once_note", "once"),
                    Act = CompanyAct.Of(CompanyDo.LeaseAnswer, e.Place, haggle), Disabled = l != null && (l.Haggled || rise <= 0),
                    Why = l != null && l.Haggled ? w.T("reallife_co_q_haggled",
                        "The landlord was asked once already.") : rise <= 0 ? w.T("reallife_co_q_nohaggle", "The landlord asks for no rise.") : null,
                    Lines = new[] { new CompanyConsequence { Text = w.T("reallife_co_q_haggle_line", "The landlord may meet you half way, or hold firm"), Tone = 0, Change = 0 } }
                },
                new CompanyChoice
                {
                    Name = w.T("reallife_co_q_c_hold", "Hold over, month to month"), Button = w.T("reallife_co_q_hold_do", "Hold over"), Note = w.T("reallife_co_q_nocost_note", "no cost"),
                    Act = CompanyAct.Of(CompanyDo.LeaseAnswer, e.Place, refuse),
                    Lines = new[] { new CompanyConsequence { Text = w.T("reallife_co_q_hold_line", "A premium on the rent, and no term to rely on"), Tone = -1, Change = 1 } }
                }
            };
            q.Choices = choices.ToArray();
            q.Headline = rise > 0 ? w.F("reallife_co_q_lease_head", "The lease at {place} renews in {days} days: {rent} a month, up {rise}.", "place", business, "days", days.ToString(), "rent",
                money(monthly(offered)), "rise", share(rise))
                : w.F(rise < 0 ? "reallife_co_q_lease_head_less" : "reallife_co_q_lease_head_same", rise < 0 ? "The lease at {place} renews in {days} days: the rent goes down to {rent} a month."
                    : "The lease at {place} renews in {days} days: the rent stays at {rent} a month.", "place", business, "days", days.ToString(), "rent", money(monthly(offered)));
        }
    }
}
