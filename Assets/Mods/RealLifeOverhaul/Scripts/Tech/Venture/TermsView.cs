using System.Collections.Generic;
using System.Globalization;

namespace RealLifeOverhaul.Tech
{
    /// <summary>The terms of a round in words, for the offer, the firm's page and the pitch: what each term is, and with Law what it costs in a sale and which are hard.</summary>
    internal static class TermsView
    {
        public struct Row
        {
            public string Label, Value;
            public bool Hard;
        }

        public static bool Reads => TechBridges.SkillNow(TalentCatalogue.Law) >= TermsCatalogue.LawToRead;

        private static int PrefIndex(int pref) => pref >= 2000 ? 2 : pref >= 1500 ? 1 : 0;

        public static string Value(TechDirector d, Terms t, int term)
        {
            switch (term)
            {
                case TermsCatalogue.TPref: return TechText.T(TermsCatalogue.PrefKeys[PrefIndex(t.Pref)]);
                case TermsCatalogue.TPart: return TechText.T(TermsCatalogue.PartKeys[t.Participating ? 1 : 0]);
                case TermsCatalogue.TAnti: return TechText.T(TermsCatalogue.AntiKeys[TalentCatalogue.Clamp(t.Anti, 0, 2)]);
                case TermsCatalogue.TVest: return TechText.T(TermsCatalogue.VestKeys[TalentCatalogue.Clamp(t.Vest, 0, 1)]);
                case TermsCatalogue.TInfo: return TechText.T(TermsCatalogue.InfoKeys[TalentCatalogue.Clamp(t.Info, 0, 2)]);
                default:
                    return t.Provision > 0 ? TechText.T("reallife_vc_terms_prov", "amount", TechText.Cents(d.Level(t.Provision))) : TechText.T("reallife_vc_terms_prov_none");
            }
        }

        /// <summary>The six terms as label and value, the hard ones marked when the player's Law reads them (<paramref name="k"/> is the costs, or null).</summary>
        public static void Rows(TechDirector d, Terms t, TermsCosts k, List<Row> into)
        {
            into.Clear();
            Terms use = t ?? new Terms();
            for (int i = 0; i < TermsCatalogue.Count; i++)
            {
                into.Add(new Row { Label = TechText.T(TermsCatalogue.LabelKeys[i]), Value = Value(d, use, i), Hard = k != null && Reads && k.Hard(i) });
            }
        }

        /// <summary>All the terms on lines of their own, for the pitch's page.</summary>
        public static string Plain(TechDirector d, Terms t, TermsCosts k)
        {
            var rows = new List<Row>(TermsCatalogue.Count);
            Rows(d, t, k, rows);
            var b = new System.Text.StringBuilder(256);
            for (int i = 0; i < rows.Count; i++)
            {
                b.Append(b.Length > 0 ? "\n" : string.Empty).Append(rows[i].Label).Append(": ").Append(rows[i].Value);
                if (rows[i].Hard)
                {
                    b.Append(' ').Append(TechText.T("reallife_vc_terms_hard_mark"));
                }
            }
            return b.ToString();
        }

        /// <summary>A line of the terms for a cap table or a startup, short: the preference, participation and the rights that are not plain.</summary>
        public static string Short(TechDirector d, Terms t)
        {
            if (t == null || t.Standard)
            {
                return TechText.T("reallife_vc_terms_plain");
            }
            var b = new System.Text.StringBuilder(128);
            for (int i = 0; i < TermsCatalogue.Count; i++)
            {
                bool plain = i == TermsCatalogue.TPref ? t.Pref == 1000 : i == TermsCatalogue.TPart ? !t.Participating : i == TermsCatalogue.TAnti ? t.Anti == 0
                    : i == TermsCatalogue.TVest ? t.Vest == 0 : i == TermsCatalogue.TInfo ? t.Info == 0 : t.Provision == 0;
                if (!plain)
                {
                    b.Append(b.Length > 0 ? "; " : string.Empty).Append(Value(d, t, i));
                }
            }
            return b.ToString();
        }

        private static string Times(int pref) => (pref / 1000f).ToString("0.#", CultureInfo.InvariantCulture) + "x";

        /// <summary>
        /// What the terms cost in a sale, in the sentences a Law of 40 reads out: worked by the waterfall (<see cref="VentureTerms.Cost"/>), at the round's value
        /// and at three times it.
        /// </summary>
        public static void CostLines(TechDirector d, Terms t, TermsCosts k, List<string> into)
        {
            into.Clear();
            Terms use = t ?? new Terms();
            string v1 = TechText.Cents(d.Level(k.Value)), v3 = TechText.Cents(d.Level(k.Value3));
            if (use.Pref > 1000)
            {
                into.Add(TechText.Fill("reallife_vc_terms_cost_pref", "multiple", Times(use.Pref), "at1", TechText.Cents(d.Level(k.Pref)), "value1", v1,
                    "at3", TechText.Cents(d.Level(k.Pref3)), "value3", v3));
            }
            if (use.Participating)
            {
                into.Add(TechText.Fill("reallife_vc_terms_cost_part", "at1", TechText.Cents(d.Level(k.Part)), "at3", TechText.Cents(d.Level(k.Part3))));
            }
            if (use.Pref <= 1000 && !use.Participating)
            {
                into.Add(TechText.T("reallife_vc_terms_cost_none"));
            }
            if (use.Anti > 0)
            {
                into.Add(TechText.Fill("reallife_vc_terms_cost_anti", "cut", TechText.Mille(TermsCatalogue.DownCut), "with", TechText.Mille(k.ShareDownAnti), "without", TechText.Mille(k.ShareDown)));
            }
            var hard = new System.Text.StringBuilder(96);
            for (int i = 0; i < TermsCatalogue.Count; i++)
            {
                if (k.Hard(i))
                {
                    hard.Append(hard.Length > 0 ? ", " : string.Empty).Append(TechText.T(TermsCatalogue.LabelKeys[i]));
                }
            }
            if (hard.Length > 0)
            {
                into.Add(TechText.T("reallife_vc_terms_hard", "terms", hard.ToString()));
            }
        }

        public static bool AnyHard(TermsCosts k)
        {
            for (int i = 0; i < TermsCatalogue.Count; i++)
            {
                if (k.Hard(i))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>Whether Analysis or Statistics is good enough to see an offer against the market.</summary>
        public static bool Sees
        {
            get
            {
                return TechBridges.SkillNow(TalentCatalogue.Analysis) >= TermsCatalogue.AnalysisToSee || TechBridges.SkillNow(TalentCatalogue.Statistics) >= TermsCatalogue.AnalysisToSee;
            }
        }

        /// <summary>An offer's value before the money against what the market would pay.</summary>
        public static string MarketLine(TechDirector d, string key, long pre, long market)
        {
            int percent = market > 0 ? (int)(pre * 100 / market) : 0;
            return TechText.Fill(key, "market", TechText.Cents(d.Level(market)), "pre", TechText.Cents(d.Level(pre)), "percent", percent.ToString(CultureInfo.InvariantCulture));
        }
    }
}
