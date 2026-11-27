using System.Globalization;

namespace RealLifeOverhaul.Tech
{
    /// <summary>
    /// The words of the new kinds of client for the Contracts tab, written once for both interfaces: the kind's tag, the brief in the client's own voice and each line of what it asks
    /// and risks (<see cref="ClientViews"/>). COST: a few strings when a contract's page or the list is written, never per frame.
    /// </summary>
    public static class ClientWords
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        /// <summary>"Government", "Individual" or "Company"; empty for an old client.</summary>
        public static string KindTag(Contract w)
        {
            switch ((ClientKind)w.Deal)
            {
                case ClientKind.Government: return TechText.T("reallife_deal_kind_government");
                case ClientKind.Individual: return TechText.T("reallife_deal_kind_individual");
                case ClientKind.Company: return TechText.T("reallife_deal_kind_company");
                default: return string.Empty;
            }
        }

        /// <summary>The kind of work, with the kind of client after it for a client of the new kinds ("Integration (Government)").</summary>
        public static string WorkTag(Contract w) =>
            w.Deal == (int)ClientKind.Classic ? TechText.Work(w.K) : TechText.Fill("reallife_deal_row_work", "work", TechText.Work(w.K), "kind", KindTag(w));

        /// <summary>The client's brief, as the client wrote it; empty for an old client or a brief this version does not know.</summary>
        public static string Brief(Contract w)
        {
            ClientBrief b = w.Brief > 0 ? ClientCatalogue.BriefOf(w.Brief) : null;
            return b != null ? TechText.T(b.Key) : string.Empty;
        }

        /// <summary>A line in words. <paramref name="level"/> is the prices' level, thousandths, for the money a line names.</summary>
        public static string Say(in DealLine l, int level)
        {
            switch (l.Key)
            {
                case "reallife_deal_need_both":
                    return TechText.Fill(l.Key, "security", TechText.Mille(l.A), "reliability", TechText.Mille(l.B));
                case "reallife_deal_need_security":
                    return TechText.Fill(l.Key, "security", TechText.Mille(l.A));
                case "reallife_deal_need_reliability":
                    return TechText.Fill(l.Key, "reliability", TechText.Mille(l.B));
                case "reallife_deal_short":
                    return TechText.Fill(l.Key, "firm", l.Name, "security", TechText.Mille(l.A), "reliability", TechText.Mille(l.B));
                case "reallife_deal_risk_government_pay":
                    return TechText.Fill(l.Key, "upfront", TechText.Mille(l.A), "least", l.B.ToString(Inv), "most", l.C.ToString(Inv));
                case "reallife_deal_risk_individual_pay":
                case "reallife_deal_risk_company_pay":
                    return TechText.Fill(l.Key, "days", l.A.ToString(Inv));
                case "reallife_deal_risk_creep":
                    return TechText.Fill(l.Key, "share", TechText.Mille(l.A));
                case "reallife_deal_risk_late":
                    return TechText.Fill(l.Key, "penalty", TechText.Mille(l.A));
                case "reallife_deal_risk_government_audit":
                    return TechText.Fill(l.Key, "late", l.A.ToString(Inv), "hold", l.B.ToString(Inv));
                case "reallife_deal_reputation":
                    return TechText.Fill(l.Key, "good", l.A.ToString(Inv), "bad", l.B.ToString(Inv), "fail", l.C.ToString(Inv));
                case "reallife_deal_grew":
                    return TechText.Fill(l.Key, "extra", l.A.ToString(Inv));
                case "reallife_deal_owed_line":
                    return TechText.Fill(l.Key, "amount", TechText.Cents(l.Cents * level / 1000), "day", l.A.ToString(Inv));
                default:
                    return TechText.T(l.Key);   // the roles' sentences, which name nothing
            }
        }
    }
}
