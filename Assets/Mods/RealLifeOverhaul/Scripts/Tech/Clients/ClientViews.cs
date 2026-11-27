using System.Collections.Generic;

namespace RealLifeOverhaul.Tech
{
    /// <summary>One line of a contract's page about its client: the text's key, the figures it says and whether it warns. <see cref="ClientWords"/> writes it in words.</summary>
    public struct DealLine
    {
        public string Key;
        public bool Warn;
        public int A, B, C;      // the figures the line says, in the order its text names them
        public long Cents;       // money, at the start's prices
        public string Name;      // the firm a line speaks of
    }

    /// <summary>
    /// What a client of the new kinds asks of the firm and what it risks, as lines of a contract's page: the roles that do the work, the security posture and reliability it wants (and
    /// whether the firm has them, when its crew is known), how and when it pays, a change it may ask for, the penalty for a late day, and what the firm's name gains and loses. One
    /// source for both interfaces. Game-free. COST: a few small structs when a contract's page is written, never per frame.
    /// </summary>
    public static class ClientViews
    {
        private static string RolesKey(WorkKind kind)
        {
            switch (kind)
            {
                case WorkKind.Website: return "reallife_deal_roles_website";
                case WorkKind.Integration: return "reallife_deal_roles_integration";
                case WorkKind.Managed: return "reallife_deal_roles_managed";
                case WorkKind.Branding: return "reallife_deal_roles_branding";
                default: return "reallife_deal_roles_security";
            }
        }

        /// <summary>The lines for a contract of the new kinds; none for an old client's.</summary>
        public static void Lines(List<DealLine> into, Contract w, bool crewKnown, in Crew crew, string firm)
        {
            if (w.Deal == (int)ClientKind.Classic)
            {
                return;
            }
            int k = System.Math.Max(0, System.Math.Min((int)ClientKind.Count - 1, w.Deal));
            into.Add(new DealLine { Key = RolesKey(w.K) });
            bool needSec = w.NeedSecurity > 0, needRel = w.NeedReliability > 0;
            int missing = crewKnown ? ClientRules.Short(w, crew) : 0;
            if (needSec || needRel)
            {
                into.Add(new DealLine
                {
                    Key = needSec && needRel ? "reallife_deal_need_both" : needSec ? "reallife_deal_need_security" : "reallife_deal_need_reliability",
                    A = w.NeedSecurity, B = w.NeedReliability, Warn = missing != 0
                });
                if (missing != 0)
                {
                    into.Add(new DealLine { Key = "reallife_deal_short", Name = firm, A = crew.Security, B = crew.Reliability, Warn = true });
                }
            }
            if (!w.Monthly)
            {
                if (w.Deal == (int)ClientKind.Government)
                {
                    into.Add(new DealLine { Key = "reallife_deal_risk_government_pay", A = ClientCatalogue.Upfront[k], B = ClientCatalogue.GovernmentLagLeast, C = ClientCatalogue.GovernmentLagMost });
                }
                else if (w.Deal == (int)ClientKind.Individual)
                {
                    into.Add(new DealLine { Key = "reallife_deal_risk_individual_pay", A = ClientCatalogue.IndividualLagMost });
                }
                else
                {
                    into.Add(new DealLine { Key = "reallife_deal_risk_company_pay", A = ClientCatalogue.CompanyTermsDays });
                }
                if (w.Deal != (int)ClientKind.Government)
                {
                    into.Add(new DealLine { Key = "reallife_deal_risk_creep", A = ClientCatalogue.CreepShare });
                }
                into.Add(new DealLine { Key = "reallife_deal_risk_late", A = ClientCatalogue.LatePenalty[k] });
                if (w.Deal == (int)ClientKind.Government)
                {
                    into.Add(new DealLine { Key = "reallife_deal_risk_government_audit", A = ClientCatalogue.ScrutinyLateDays, B = ClientCatalogue.ScrutinyDays });
                }
            }
            into.Add(new DealLine { Key = "reallife_deal_reputation", A = ClientCatalogue.ReputationOnTime[k], B = ClientCatalogue.ReputationLate[k], C = ClientCatalogue.ReputationFailed[k] });
            if (w.Extra > 0)
            {
                into.Add(new DealLine { Key = "reallife_deal_grew", A = w.Extra, Warn = true });
            }
            if (w.Owed > 0)
            {
                into.Add(new DealLine { Key = "reallife_deal_owed_line", Cents = w.Owed, A = w.PayDay });
            }
        }
    }
}
