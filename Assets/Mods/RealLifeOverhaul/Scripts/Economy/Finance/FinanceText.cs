using System.Text;
using Extensions;
using Localizor;

namespace RealLifeOverhaul.Economy
{
    /// <summary>The economy's words, the mod's keys or the game's, and money as the game's loan pages write it.</summary>
    public static class FinanceText
    {
        private static readonly string[] EventKeys =
        {
            "reallife_econ_event_paid", "reallife_econ_event_missed", "reallife_econ_event_part", "reallife_econ_event_new",
            "reallife_econ_event_paid_off", "reallife_econ_event_paid_off_early", "reallife_econ_event_payment_changed", "reallife_econ_event_extra",
            "reallife_econ_event_application", "reallife_econ_event_refused", "reallife_econ_event_fine_late", "reallife_econ_event_judgment",
            "reallife_econ_event_conviction", "reallife_econ_event_cleared", "reallife_econ_event_started", "reallife_econ_event_test_set",
            "reallife_econ_event_consolidated", "reallife_econ_event_late_fee", "reallife_econ_event_penalty", "reallife_econ_event_collections",
            "reallife_econ_event_repossessed", "reallife_econ_event_claim", "reallife_econ_event_plan", "reallife_econ_event_arrangement", "reallife_econ_event_bankruptcy"
        };

        private static readonly string[] WordKeys =
        {
            "reallife_econ_word_poor", "reallife_econ_word_fair", "reallife_econ_word_good", "reallife_econ_word_very_good"
        };

        public static string T(string key)
        {
            return key.GetLocalization();
        }

        public static string T(string key, string name, string value)
        {
            return key.GetLocalization().Replace("{" + name + "}", value ?? string.Empty);
        }

        public static string T(string key, string name, string value, string name2, string value2)
        {
            return T(key, name, value).Replace("{" + name2 + "}", value2 ?? string.Empty);
        }

        public static string T(string key, string name, string value, string name2, string value2, string name3, string value3)
        {
            return T(key, name, value, name2, value2).Replace("{" + name3 + "}", value3 ?? string.Empty);
        }

        /// <summary>A text with any number of its names filled in: name, value, name, value.</summary>
        public static string Fill(string key, params string[] pairs)
        {
            string text = key.GetLocalization();
            for (int i = 0; i + 1 < pairs.Length; i += 2)
            {
                text = text.Replace("{" + pairs[i] + "}", pairs[i + 1] ?? string.Empty);
            }
            return text;
        }

        public static string Money(float amount)
        {
            return amount.ToShortCurrencyFormat();
        }

        public static string Grade(CreditGrade grade)
        {
            return T(CreditCatalogue.GradeKeys[(int)grade]);
        }

        public static string Day(int day)
        {
            return T("reallife_econ_day", "day", day.ToString());
        }

        /// <summary>A label and its value in bold, as the game's white box writes a line.</summary>
        public static string Pair(string labelKey, string value)
        {
            return T(labelKey) + " <b>" + value + "</b>";
        }

        /// <summary>A plain word for how a factor of the rating stands.</summary>
        public static string Word(float factor)
        {
            return T(WordKeys[factor >= 0.85f ? 3 : factor >= 0.65f ? 2 : factor >= 0.4f ? 1 : 0]);
        }

        /// <summary>A factor's name with its weight: "Payment history (40%)".</summary>
        public static string FactorName(CreditFactor factor)
        {
            return T("reallife_econ_factor_name", "name", T(CreditCatalogue.FactorKeys[(int)factor]), "weight", CreditCatalogue.Weights[(int)factor].ToString());
        }

        /// <summary>How a factor stands, in a plain word and the figures behind it: "good, 42 of 44 payments on time".</summary>
        public static string Stands(CreditScore s, CreditFactor factor)
        {
            string word = Word(s.Factor[(int)factor]);
            switch (factor)
            {
                case CreditFactor.Payments:
                    return s.OnTime + s.Missed == 0
                        ? T("reallife_econ_stands_payments_none")
                        : T("reallife_econ_stands_payments", "word", word, "on", s.OnTime.ToString(), "all", (s.OnTime + s.Missed).ToString());
                case CreditFactor.Owed:
                    {
                        CreditInputs inputs = CreditLedger.Gather();
                        string text = T("reallife_econ_stands_owed", "word", word, "owed", Money(inputs.Owed), "limit", Money(inputs.Limit));
                        if (s.ServiceShare > 0f)
                        {
                            text += T("reallife_econ_stands_owed_income", "share", (s.ServiceShare * 100f).ToString("0") + "%");
                        }
                        else if (s.ServiceShare < 0f)
                        {
                            text += T("reallife_econ_stands_owed_no_income");
                        }
                        return text;
                    }
                case CreditFactor.Length:
                    return s.HistoryDays <= 0 ? T("reallife_econ_stands_length_none") : T("reallife_econ_stands_length", "word", word, "days", s.HistoryDays.ToString());
                case CreditFactor.Legal:
                    {
                        LegalStanding legal = s.Legal;
                        if (legal.Convictions == 0 && legal.Judgments == 0 && legal.LateDays == 0 && legal.Outstanding == 0 && legal.RecentOffences == 0)
                        {
                            return T("reallife_econ_stands_legal_clean");
                        }
                        var text = new StringBuilder(96).Append(word);
                        Count(text, "reallife_econ_stands_legal_convictions", legal.Convictions);
                        Count(text, "reallife_econ_stands_legal_judgments", legal.Judgments);
                        if (legal.Outstanding > 0)
                        {
                            text.Append(", ").Append(T("reallife_econ_stands_legal_owed", "amount", Money(legal.Outstanding)));
                        }
                        Count(text, "reallife_econ_stands_legal_late", legal.LateDays);
                        Count(text, "reallife_econ_stands_legal_offences", legal.RecentOffences);
                        return text.ToString();
                    }
                default:
                    return T("reallife_econ_stands_applications", "word", word, "count", s.Applications.ToString(), "days", CreditCatalogue.ApplicationFadeDays.ToString());
            }
        }

        private static void Count(StringBuilder text, string key, int count)
        {
            if (count > 0)
            {
                text.Append(", ").Append(T(key, "count", count.ToString()));
            }
        }

        public static string Tip(CreditScore s, CreditFactor factor)
        {
            switch (factor)
            {
                case CreditFactor.Payments:
                    return s.Missed > 0
                        ? T("reallife_econ_tip_payments_missed", "days", CreditCatalogue.PaymentFadeDays.ToString())
                        : s.OnTime == 0 ? T("reallife_econ_tip_payments_none") : T("reallife_econ_tip_payments");
                case CreditFactor.Owed:
                    return T(s.ServiceShare < 0f || s.ServiceShare > s.OwedShare ? "reallife_econ_tip_owed_income" : "reallife_econ_tip_owed");
                case CreditFactor.Length:
                    return T(s.HistoryDays <= 0 ? "reallife_econ_tip_length_none" : "reallife_econ_tip_length", "days", CreditCatalogue.HistoryFullDays.ToString());
                case CreditFactor.Legal:
                    return T(s.Legal.Outstanding > 0 ? "reallife_econ_tip_legal_owed" : "reallife_econ_tip_legal");
                default:
                    return T("reallife_econ_tip_applications", "days", CreditCatalogue.ApplicationFadeDays.ToString());
            }
        }

        /// <summary>The one or two things that would raise the rating most, as sentences one after the other; empty if nothing would.</summary>
        public static string Tips(CreditScore s, CreditFactor[] buffer)
        {
            int count = CreditScoring.Tips(s, buffer);
            if (count == 0)
            {
                return string.Empty;
            }
            string text = T("reallife_econ_tips_lead") + " " + Tip(s, buffer[0]);
            return count > 1 ? text + " " + Tip(s, buffer[1]) : text;
        }

        public static string Event(in CreditEvent e)
        {
            int kind = (int)e.Kind;
            if (kind < 0 || kind >= EventKeys.Length)
            {
                return string.Empty;
            }
            // Only a file's start and a test's setting name the amount, a score; money has a column of its own.
            string text = T(EventKeys[kind], "amount", e.Amount.ToString(), "count", e.Count.ToString());
            return e.Test && e.Kind != CreditKind.TestAdjust ? T("reallife_econ_event_test", "text", text) : text;
        }

        /// <summary>The money a line of the credit file is about; empty where it is about none.</summary>
        public static string EventAmount(in CreditEvent e)
        {
            switch (e.Kind)
            {
                case CreditKind.Started:
                case CreditKind.TestAdjust:
                case CreditKind.Refused:
                case CreditKind.Judgment:
                case CreditKind.Conviction:
                    return string.Empty;
                default:
                    return e.Amount > 0 ? Money(e.Amount) : string.Empty;
            }
        }

        public static string State(CreditAccount a)
        {
            return State(a.Ladder, a.Frozen, ProductCatalogue.Instalment(a.Kind) && !a.Secured);
        }

        /// <summary>How a debt stands with its lender, in a few words; the gravest state is the one named.</summary>
        public static string State(LadderState s, bool frozen, bool unsecured)
        {
            if (frozen)
            {
                return T("reallife_econ_state_arranged");
            }
            if (s.RepoPending)
            {
                return T("reallife_econ_state_repo_due");
            }
            if (s.CallDue)
            {
                return T("reallife_econ_state_collections");
            }
            if (s.PlanLeft > 0)
            {
                return T("reallife_econ_state_plan");
            }
            if (s.Misses > 0)
            {
                return T("reallife_econ_state_behind", "count", s.Misses.ToString());
            }
            if (s.RepoDay > 0)
            {
                return T(unsecured ? "reallife_econ_state_repossessed" : "reallife_econ_state_diverted");
            }
            if (s.Penalty)
            {
                return T("reallife_econ_state_penalty");
            }
            return T(unsecured ? "reallife_econ_state_unsecured" : "reallife_econ_state_ok");
        }

        /// <summary>An effect on the score: "+12", "-45" or "0".</summary>
        public static string Effect(int points)
        {
            return points > 0 ? "+" + points : points.ToString();
        }
    }
}
