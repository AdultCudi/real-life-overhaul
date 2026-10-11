using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Economy.Ventures
{
    public enum QuestionKind { Fact = 0, Judgement = 1, Founder = 2 }

    /// <summary>A figure an investor can check against the game's own records.</summary>
    public enum Fact
    {
        None = 0, SalesYesterday, SalesWeek, ProfitYesterday, ProfitWeek, Staff, Age, Cash, Rating, Owed, Convictions, ProfitableDays, Businesses, Missed, Payments
    }

    public enum FactUnit { Money, Count, Days }

    /// <summary>The manner of an answer about the plan: each temperament likes a different one.</summary>
    public enum Style { Careful = 0, Bold = 1, Personal = 2, Reckless = 3 }

    /// <summary>What an answer about the founder says of them, tested against what the save shows.</summary>
    public enum Claim
    {
        None = 0, FirstBusiness, SeveralBusinesses, LongRun, BuiltThis, GoodCredit, SteadyProfit, NeverMissed, HasMissed, Worked, CleanRecord, HasRecord, NoBackers, OtherBackers,
        BankInOrder, OwnStake
    }

    /// <summary>One answer to a question about the plan or the founder: what it is worth to each kind of investor, and what it takes to be believed.</summary>
    public struct AnswerDef
    {
        public int[] ByKind;     // points with an angel, a tycoon, a fund
        public Style Style;
        public int Sectors;      // the sectors it suits (a bit per Sector); 0: any
        public Claim Claim;
    }

    public sealed class Question
    {
        public const int Running = 1, New = 2, Both = 3;

        public int Id;
        public QuestionKind Kind;
        public string Key;       // en.json: "question|answer|answer|answer|topic" (a fact: "question|topic")
        public int Fits;         // a business that is running, one not yet open, or both
        public Fact Fact;
        public AnswerDef[] Answers;
    }

    /// <summary>What an investor can find out about the business and its founder: the true figures, read by the director when a meeting starts.</summary>
    public struct PitchFacts
    {
        public bool Running;         // a business the player runs (else one not yet open)
        public Sector Sector;
        public long SalesYesterday, SalesWeek, ProfitYesterday, ProfitWeek;
        public float AvgProfit7;     // the figure a valuation is made from
        public int Staff, AgeDays, ProfitableDays;
        public long Cash, Owed, Payments;
        public int Rating;           // 0: there is no credit file
        public int Convictions, Businesses, Missed;
        public int Jobs, Bankruptcies, OldestDays, Backers;
        public int Budget;           // a business not yet open: what the player says it will cost
    }

    public enum Verdict { Invest, LowScore, TooSmall, NoProfit, NoRoom }

    public struct PitchDecision
    {
        public Verdict Verdict;
        public int Score, Needed;
        public int Amount, ShareBp, ValueDays;
        public bool OnFounder;       // an angel backing a young business on its founder, not its profit
        public int Best, Worst;      // which question helped most and which hurt most (their place in the meeting); -1: none
    }

    /// <summary>What became of one answer: its points, and for a figure that was not the true one, the check.</summary>
    public struct AnswerResult
    {
        public int Points;
        public bool Untrue, Bold, Caught;
        public int Chance, Roll;     // the chance of being caught, in per cent, and the roll against it (caught under it)
    }

    /// <summary>A meeting in progress. Kept in memory only: a meeting does not survive a load.</summary>
    public sealed class PitchState
    {
        public int InvestorId, Number;
        public PitchFacts Facts;
        public string Place = string.Empty, Business = string.Empty, TypeName = string.Empty;
        public readonly List<int> Questions = new List<int>(8);   // indices into Pitch.Bank
        public readonly List<int> Points = new List<int>(8);
        public int Index;
        public bool Caught;
    }

    /// <summary>
    /// The pitch: the bank of questions, which of them an investor asks, what an answer is worth, whether an untrue
    /// figure is caught, and the decision. Game-free.
    ///
    ///   A FACT is offered as the true figure (rounded as people say figures) and one or two that sound better. The
    ///   true one scores from <see cref="VenturesCatalogue.TruthBase"/> to the most by how strong it is; an untrue
    ///   one scores as if it were true, unless it is caught: by a chance that is the investor's diligence times 45%
    ///   (a little better) or 90% (much better). Being caught ends the meeting.
    ///   An answer about THE PLAN has points for each kind of investor, moved by their temperament and, for some, by
    ///   the business's sector: the best answer is not the same for every investor.
    ///   An answer about THE FOUNDER says something the save can show or not: said without the record behind it, it
    ///   scores one point.
    ///
    /// The score is the points as a share of the most, plus a little for a sector the investor likes and for a good
    /// name with investors. At or over what the investor needs, they invest: the better the score, the more days of
    /// profit they value the business at and the smaller the share they take.
    /// </summary>
    public static class Pitch
    {
        public const int Unsupported = 1;        // an answer about the founder that the record does not bear out
        public const int GoodCreditFrom = 670, SteadyDays = 12, LongRunDays = 60, BuiltDays = 30;

        private static readonly int[][] StyleByTemper =
        {
            new[] { 2, -1, 1, -2 },    // patient: careful, bold, personal, reckless
            new[] { 1, 1, 0, -2 },     // even
            new[] { -1, 2, -1, -1 }    // demanding
        };

        // The figure at which a fact is as strong as it gets (for the ones where less is better: at which it is as weak as it gets).
        private static readonly long[] Full = { 0, 4000, 28000, 2000, 2000, 6, 45, 60000, 0, 150000, 2, 0, 4, 4, 3000 };

        private const int Street = (1 << (int)Sector.Retail) | (1 << (int)Sector.Food) | (1 << (int)Sector.Leisure);
        private const int Trade = (1 << (int)Sector.Office) | (1 << (int)Sector.Industry);

        // Every question's text, by name (the locale check looks for each key as it is written here).
        private static readonly string[] FactKeys =
        {
            "reallife_ven_q_f01", "reallife_ven_q_f02", "reallife_ven_q_f03", "reallife_ven_q_f04", "reallife_ven_q_f05", "reallife_ven_q_f06", "reallife_ven_q_f07",
            "reallife_ven_q_f08", "reallife_ven_q_f09", "reallife_ven_q_f10", "reallife_ven_q_f11", "reallife_ven_q_f12", "reallife_ven_q_f13", "reallife_ven_q_f14"
        };
        private static readonly string[] PlanKeys =
        {
            "reallife_ven_q_j01", "reallife_ven_q_j02", "reallife_ven_q_j03", "reallife_ven_q_j04", "reallife_ven_q_j05", "reallife_ven_q_j06", "reallife_ven_q_j07",
            "reallife_ven_q_j08", "reallife_ven_q_j09", "reallife_ven_q_j10", "reallife_ven_q_j11", "reallife_ven_q_j12", "reallife_ven_q_j13", "reallife_ven_q_j14",
            "reallife_ven_q_j15", "reallife_ven_q_j16", "reallife_ven_q_j17", "reallife_ven_q_j18"
        };
        private static readonly string[] FounderKeys =
        {
            "reallife_ven_q_p01", "reallife_ven_q_p02", "reallife_ven_q_p03", "reallife_ven_q_p04", "reallife_ven_q_p05", "reallife_ven_q_p06", "reallife_ven_q_p07",
            "reallife_ven_q_p08", "reallife_ven_q_p09"
        };

        public static readonly Question[] Bank = Build();

        private static Question[] Build()
        {
            var bank = new List<Question>(44);
            Fact[] running = { Fact.SalesYesterday, Fact.SalesWeek, Fact.ProfitYesterday, Fact.ProfitWeek, Fact.Staff, Fact.Age, Fact.ProfitableDays };
            for (int f = 1; f <= (int)Fact.Payments; f++)
            {
                bank.Add(new Question
                {
                    Id = bank.Count, Kind = QuestionKind.Fact, Key = FactKeys[f - 1], Fact = (Fact)f,
                    Fits = Array.IndexOf(running, (Fact)f) >= 0 ? Question.Running : Question.Both
                });
            }
            // The plan. Points with an angel, a tycoon, a fund; then the manner; then the sectors it suits.
            Plan(bank, 1, A(7, 7, 5, Style.Careful), A(4, 6, 8, Style.Bold), A(2, 1, 0, Style.Reckless));
            Plan(bank, 2, A(7, 7, 6, Style.Careful), A(5, 6, 8, Style.Bold), A(8, 5, 4, Style.Personal));
            Plan(bank, 3, A(6, 6, 5, Style.Careful, Street), A(6, 6, 7, Style.Bold, Trade), A(2, 2, 1, Style.Reckless));
            Plan(bank, 4, A(7, 7, 5, Style.Careful), A(5, 6, 8, Style.Bold), A(3, 2, 1, Style.Reckless));
            Plan(bank, 5, A(6, 8, 8, Style.Bold), A(8, 6, 4, Style.Careful), A(2, 2, 1, Style.Reckless));
            Plan(bank, 6, A(7, 8, 7, Style.Careful), A(4, 6, 8, Style.Bold), A(1, 0, 0, Style.Reckless));
            Plan(bank, 7, A(7, 7, 5, Style.Careful), A(5, 6, 8, Style.Bold), A(3, 2, 2, Style.Reckless));
            Plan(bank, 8, A(6, 8, 7, Style.Careful), A(6, 6, 8, Style.Bold), A(3, 3, 1, Style.Reckless));
            Plan(bank, 9, A(8, 6, 4, Style.Personal), A(4, 6, 8, Style.Bold), A(5, 7, 6, Style.Careful));
            Plan(bank, 10, A(7, 8, 7, Style.Careful), A(4, 5, 7, Style.Bold), A(3, 2, 1, Style.Reckless));
            Plan(bank, 11, A(7, 8, 8, Style.Careful), A(6, 3, 2, Style.Personal), A(2, 5, 6, Style.Bold));
            Plan(bank, 12, A(8, 7, 5, Style.Personal), A(6, 6, 7, Style.Bold), A(1, 2, 3, Style.Reckless));
            Plan(bank, 13, A(7, 7, 5, Style.Careful, Street), A(5, 6, 8, Style.Bold, Trade), A(2, 2, 2, Style.Reckless));
            Plan(bank, 14, A(8, 8, 6, Style.Careful), A(5, 6, 8, Style.Bold), A(2, 3, 1, Style.Reckless));
            Plan(bank, 15, A(8, 6, 5, Style.Personal), A(5, 7, 8, Style.Bold), A(4, 2, 1, Style.Reckless));
            Plan(bank, 16, A(8, 6, 3, Style.Personal), A(6, 8, 6, Style.Careful), A(3, 5, 9, Style.Bold));
            Plan(bank, 17, A(7, 7, 4, Style.Careful), A(4, 6, 9, Style.Bold), A(2, 1, 0, Style.Reckless));
            Plan(bank, 18, A(8, 8, 6, Style.Careful), A(5, 6, 8, Style.Bold), A(2, 2, 2, Style.Reckless));
            // The founder. The same, then what the save must show for the answer to be believed.
            Founder(bank, 1, C(7, 8, 8, Style.Bold, Claim.BuiltThis), C(7, 8, 9, Style.Bold, Claim.SeveralBusinesses), C(7, 5, 3, Style.Personal, Claim.None));
            Founder(bank, 2, C(7, 5, 4, Style.Personal, Claim.FirstBusiness), C(8, 8, 9, Style.Bold, Claim.SeveralBusinesses), C(7, 8, 8, Style.Careful, Claim.LongRun));
            Founder(bank, 3, C(8, 8, 7, Style.Careful, Claim.None), C(5, 7, 7, Style.Bold, Claim.GoodCredit), C(6, 5, 5, Style.Bold, Claim.SteadyProfit));
            Founder(bank, 4, C(8, 9, 9, Style.Careful, Claim.NeverMissed), C(7, 6, 5, Style.Personal, Claim.HasMissed), C(1, 1, 0, Style.Reckless, Claim.None));
            Founder(bank, 5, C(8, 7, 6, Style.Personal, Claim.Worked), C(5, 6, 5, Style.Bold, Claim.None), C(4, 3, 2, Style.Reckless, Claim.None));
            Founder(bank, 6, C(8, 8, 9, Style.Careful, Claim.CleanRecord), C(7, 6, 5, Style.Personal, Claim.HasRecord), C(3, 2, 1, Style.Reckless, Claim.None));
            Founder(bank, 7, C(7, 6, 4, Style.Personal, Claim.NoBackers), C(7, 8, 9, Style.Bold, Claim.OtherBackers), C(6, 8, 8, Style.Careful, Claim.BankInOrder));
            Founder(bank, 8, C(8, 8, 8, Style.Careful, Claim.OwnStake), C(6, 6, 6, Style.Personal, Claim.None), C(2, 2, 3, Style.Reckless, Claim.None));
            Founder(bank, 9, C(8, 7, 3, Style.Personal, Claim.None), C(6, 8, 8, Style.Careful, Claim.None), C(3, 5, 8, Style.Bold, Claim.None));
            return bank.ToArray();
        }

        private static AnswerDef A(int angel, int tycoon, int firm, Style style, int sectors = 0)
        {
            return new AnswerDef { ByKind = new[] { angel, tycoon, firm }, Style = style, Sectors = sectors };
        }

        private static AnswerDef C(int angel, int tycoon, int firm, Style style, Claim claim)
        {
            return new AnswerDef { ByKind = new[] { angel, tycoon, firm }, Style = style, Claim = claim };
        }

        private static void Plan(List<Question> bank, int number, AnswerDef a, AnswerDef b, AnswerDef c)
        {
            bank.Add(new Question { Id = bank.Count, Kind = QuestionKind.Judgement, Key = PlanKeys[number - 1], Fits = Question.Both, Answers = new[] { a, b, c } });
        }

        private static void Founder(List<Question> bank, int number, AnswerDef a, AnswerDef b, AnswerDef c)
        {
            bank.Add(new Question { Id = bank.Count, Kind = QuestionKind.Founder, Key = FounderKeys[number - 1], Fits = Question.Both, Answers = new[] { a, b, c } });
        }

        // ---------------------------------------------------------------- facts

        public static FactUnit UnitOf(Fact fact)
        {
            switch (fact)
            {
                case Fact.Staff:
                case Fact.Rating:
                case Fact.Convictions:
                case Fact.Businesses:
                case Fact.Missed:
                    return FactUnit.Count;
                case Fact.Age:
                case Fact.ProfitableDays:
                    return FactUnit.Days;
                default:
                    return FactUnit.Money;
            }
        }

        /// <summary>True for a figure that is better the smaller it is: debts, convictions, missed payments, what loans cost a day.</summary>
        public static bool LowerBetter(Fact fact)
        {
            return fact == Fact.Owed || fact == Fact.Convictions || fact == Fact.Missed || fact == Fact.Payments;
        }

        public static long Truth(Fact fact, in PitchFacts f)
        {
            switch (fact)
            {
                case Fact.SalesYesterday: return f.SalesYesterday;
                case Fact.SalesWeek: return f.SalesWeek;
                case Fact.ProfitYesterday: return f.ProfitYesterday;
                case Fact.ProfitWeek: return f.ProfitWeek;
                case Fact.Staff: return f.Staff;
                case Fact.Age: return f.AgeDays;
                case Fact.Cash: return f.Cash;
                case Fact.Rating: return f.Rating;
                case Fact.Owed: return f.Owed;
                case Fact.Convictions: return f.Convictions;
                case Fact.ProfitableDays: return f.ProfitableDays;
                case Fact.Businesses: return f.Businesses;
                case Fact.Missed: return f.Missed;
                case Fact.Payments: return f.Payments;
                default: return 0;
            }
        }

        /// <summary>
        /// The figures offered as answers: the true one first (money as people say it), then one that sounds a little
        /// better and one that sounds much better. Two or three of them; fewer than two means the true figure is
        /// already as good as it gets and the question is not asked.
        /// </summary>
        public static int Options(Fact fact, in PitchFacts f, long[] values)
        {
            long truth = Truth(fact, f);
            bool money = UnitOf(fact) == FactUnit.Money;
            long shown = money ? VenturesCatalogue.Round2(truth) : truth;
            values[0] = shown;
            if (LowerBetter(fact))
            {
                if (shown <= 0)
                {
                    return 1;
                }
                long half = money ? VenturesCatalogue.Round2(truth * 0.5) : truth / 2;
                if (half > 0 && half < shown)
                {
                    values[1] = half;
                    values[2] = 0;
                    return 3;
                }
                values[1] = 0;
                return 2;
            }
            long cap = fact == Fact.Rating ? CreditCatalogue.ScoreMax : fact == Fact.ProfitableDays ? VenturesCatalogue.ProfitWindowDays : long.MaxValue;
            long stretch, bold;
            if (fact == Fact.Rating)
            {
                if (truth < CreditCatalogue.ScoreMin)
                {
                    return 1;   // no credit file: nothing to ask about
                }
                stretch = truth + 40;
                bold = truth + 100;
            }
            else if (money)
            {
                long full = Full[(int)fact];
                stretch = truth > 0 ? Math.Max(VenturesCatalogue.Round2(truth * 1.3), shown + 1) : VenturesCatalogue.Round2(full * 0.2);
                bold = truth > 0 ? Math.Max(VenturesCatalogue.Round2(truth * 1.8), stretch + 1) : VenturesCatalogue.Round2(full * 0.6);
            }
            else
            {
                stretch = truth + Math.Max(1, (long)Math.Round(truth * 0.3));
                bold = truth + Math.Max(2, (long)Math.Round(truth * 0.8));
            }
            stretch = Math.Min(cap, stretch);
            bold = Math.Min(cap, bold);
            if (stretch <= shown || bold <= stretch)
            {
                return 1;
            }
            values[1] = stretch;
            values[2] = bold;
            return 3;
        }

        /// <summary>How strong a figure is, from 0 to 1.</summary>
        public static double Strength(Fact fact, long value)
        {
            double part;
            switch (fact)
            {
                case Fact.Rating: part = (value - 500) / 300.0; break;
                case Fact.ProfitableDays: part = (value - 6) / 8.0; break;
                default:
                    part = (double)value / Full[(int)fact];
                    part = LowerBetter(fact) ? 1.0 - part : part;
                    break;
            }
            return part < 0.0 ? 0.0 : part > 1.0 ? 1.0 : part;
        }

        /// <summary>What a figure is worth when it is believed: never under <see cref="VenturesCatalogue.TruthBase"/>.</summary>
        public static int FactPoints(Fact fact, long value)
        {
            return VenturesCatalogue.TruthBase + (int)Math.Round((VenturesCatalogue.PointsMost - VenturesCatalogue.TruthBase) * Strength(fact, value));
        }

        /// <summary>The chance, in per cent, that this investor catches a figure that is not the true one.</summary>
        public static int CatchChance(Investor investor, int level, int reputation, bool bold)
        {
            int diligence = investor.Diligence + VenturesCatalogue.DiligenceShift[VenturesCatalogue.Level(level)] + Math.Max(0, (VenturesCatalogue.ReputationStart - reputation) / 2);
            diligence = Math.Max(0, Math.Min(100, diligence));
            int chance = diligence * (bold ? VenturesCatalogue.BoldCatchPct : VenturesCatalogue.StretchCatchPct) / 100;
            return Math.Max(VenturesCatalogue.CatchLeastPct, Math.Min(VenturesCatalogue.CatchMostPct, chance));
        }

        /// <summary>The check's roll for one question of one meeting, from 0 to 99: under the chance, the figure is caught.</summary>
        public static int Roll(int seed, int meeting, int question)
        {
            return VenturesRandom.Percent(seed, meeting * 16 + question, 9001);
        }

        // ---------------------------------------------------------------- the plan and the founder

        public static bool Holds(Claim claim, in PitchFacts f)
        {
            switch (claim)
            {
                case Claim.FirstBusiness: return f.Businesses <= 1;
                case Claim.SeveralBusinesses: return f.Businesses >= 2;
                case Claim.LongRun: return f.OldestDays >= LongRunDays;
                case Claim.BuiltThis: return f.Running && (f.AgeDays >= BuiltDays || f.ProfitableDays >= SteadyDays);
                case Claim.GoodCredit: return f.Rating >= GoodCreditFrom;
                case Claim.SteadyProfit: return f.Running && f.ProfitableDays >= SteadyDays;
                case Claim.NeverMissed: return f.Missed == 0 && f.Bankruptcies == 0;
                case Claim.HasMissed: return f.Missed > 0 || f.Bankruptcies > 0;
                case Claim.Worked: return f.Jobs >= 1;
                case Claim.CleanRecord: return f.Convictions == 0;
                case Claim.HasRecord: return f.Convictions > 0;
                case Claim.NoBackers: return f.Backers == 0;
                case Claim.OtherBackers: return f.Backers >= 1;
                case Claim.BankInOrder: return f.Owed > 0 && f.Missed == 0 && f.Bankruptcies == 0;
                case Claim.OwnStake: return f.Businesses >= 1;
                default: return true;
            }
        }

        /// <summary>What one of the three answers to a question about the plan or the founder is worth to this investor.</summary>
        public static int Points(Question q, int answer, Investor investor, in PitchFacts f)
        {
            AnswerDef a = q.Answers[answer];
            if (q.Kind == QuestionKind.Founder && !Holds(a.Claim, f))
            {
                return Unsupported;
            }
            int points = a.ByKind[(int)investor.Kind] + StyleByTemper[(int)investor.Temper][(int)a.Style];
            if (a.Sectors != 0)
            {
                points += (a.Sectors & (1 << (int)f.Sector)) != 0 ? 2 : -2;
            }
            return Math.Max(0, Math.Min(VenturesCatalogue.PointsMost, points));
        }

        /// <summary>The answer worth most to this investor (the first of them if two are worth the same).</summary>
        public static int Best(Question q, Investor investor, in PitchFacts f)
        {
            int best = 0;
            for (int a = 1; a < q.Answers.Length; a++)
            {
                if (Points(q, a, investor, f) > Points(q, best, investor, f))
                {
                    best = a;
                }
            }
            return best;
        }

        // ---------------------------------------------------------------- the meeting

        public static bool Askable(Question q, in PitchFacts f, long[] scratch)
        {
            if ((q.Fits & (f.Running ? Question.Running : Question.New)) == 0)
            {
                return false;
            }
            return q.Kind != QuestionKind.Fact || Options(q.Fact, f, scratch) >= 2;
        }

        /// <summary>
        /// The questions of one meeting: so many facts, so many about the plan and so many about the founder for this
        /// kind of investor, drawn from those that fit the business; where there are too few of one sort, more about
        /// the plan. The same for the same seed, meeting and investor.
        /// </summary>
        public static void Draw(int seed, int meeting, Investor investor, in PitchFacts f, List<int> into)
        {
            into.Clear();
            var random = new VenturesRandom((int)VenturesRandom.Hash(seed, meeting, investor.Id));
            int[] mix = VenturesCatalogue.QuestionMix[(int)investor.Kind];
            int wanted = VenturesCatalogue.Questions[(int)investor.Kind];
            var scratch = new long[3];
            var pool = new List<int>(Bank.Length);
            for (int kind = 0; kind < 3; kind++)
            {
                Fill(pool, (QuestionKind)kind, f, scratch, into);
                Take(ref random, pool, mix[kind], into);
            }
            Fill(pool, QuestionKind.Judgement, f, scratch, into);
            Take(ref random, pool, wanted - into.Count, into);
            for (int i = into.Count - 1; i > 0; i--)
            {
                int j = random.Below(i + 1);
                int swap = into[i];
                into[i] = into[j];
                into[j] = swap;
            }
        }

        private static void Fill(List<int> pool, QuestionKind kind, in PitchFacts f, long[] scratch, List<int> taken)
        {
            pool.Clear();
            for (int i = 0; i < Bank.Length; i++)
            {
                if (Bank[i].Kind == kind && !taken.Contains(i) && Askable(Bank[i], f, scratch))
                {
                    pool.Add(i);
                }
            }
        }

        private static void Take(ref VenturesRandom random, List<int> pool, int count, List<int> into)
        {
            for (int n = 0; n < count && pool.Count > 0; n++)
            {
                int at = random.Below(pool.Count);
                into.Add(pool[at]);
                pool.RemoveAt(at);
            }
        }

        /// <summary>
        /// A meeting begins: it is counted (its checks are its own), the investor has been approached today, and the
        /// questions are drawn. The facts are the true ones, read by the caller.
        /// </summary>
        public static PitchState Begin(VenturesFile file, Investor investor, in PitchFacts facts, string place, string business, string typeName, int day)
        {
            file.Pitches++;
            investor.PitchedDay = day;
            var state = new PitchState
            {
                InvestorId = investor.Id, Number = file.Pitches, Facts = facts, Place = place ?? string.Empty, Business = business ?? string.Empty, TypeName = typeName ?? string.Empty
            };
            Draw(file.Seed, state.Number, investor, facts, state.Questions);
            return state;
        }

        public static bool Over(PitchState state)
        {
            return state.Caught || state.Index >= state.Questions.Count;
        }

        /// <summary>
        /// The answer to the question the meeting is at: for a fact, 0 is the true figure, the last is the one that
        /// sounds much better and one between them a little better; for the others, the answer's place in the bank.
        /// A figure that is not true is checked: caught, the meeting is over, the investor sees nobody for sixty days
        /// and the founder's name with every investor falls.
        /// </summary>
        public static AnswerResult Answer(VenturesFile file, PitchState state, Investor investor, int option, int level, int day)
        {
            var result = new AnswerResult();
            if (Over(state))
            {
                return result;
            }
            Question q = Bank[state.Questions[state.Index]];
            if (q.Kind == QuestionKind.Fact)
            {
                var values = new long[3];
                int count = Options(q.Fact, state.Facts, values);
                option = Math.Max(0, Math.Min(count - 1, option));
                result.Untrue = option > 0;
                result.Bold = option > 0 && option == count - 1;
                if (result.Untrue)
                {
                    result.Chance = CatchChance(investor, level, file.Reputation, result.Bold);
                    result.Roll = Roll(file.Seed, state.Number, state.Index);
                    result.Caught = result.Roll < result.Chance;
                }
                result.Points = result.Caught ? 0 : FactPoints(q.Fact, values[option]);
            }
            else
            {
                option = Math.Max(0, Math.Min(q.Answers.Length - 1, option));
                result.Points = Points(q, option, investor, state.Facts);
            }
            state.Points.Add(result.Points);
            state.Index++;
            if (result.Caught)
            {
                state.Caught = true;
                investor.ClosedUntil = day + VenturesCatalogue.CaughtCloseDays;
                file.MoveReputation(VenturesCatalogue.ReputationCaught);
            }
            return result;
        }

        /// <summary>The order one question's answers are shown in, for one meeting.</summary>
        public static void Order(int seed, int meeting, int question, int count, int[] order)
        {
            var random = new VenturesRandom((int)VenturesRandom.Hash(seed, meeting * 16 + question, 9002));
            for (int i = 0; i < count; i++)
            {
                order[i] = i;
            }
            for (int i = count - 1; i > 0; i--)
            {
                int j = random.Below(i + 1);
                int swap = order[i];
                order[i] = order[j];
                order[j] = swap;
            }
        }

        /// <summary>The score of a meeting, of 100: the points as a share of the most, a little more for a sector the investor likes, and more or less for the founder's name with investors.</summary>
        public static int Score(List<int> points, Investor investor, in PitchFacts f, int reputation)
        {
            int sum = 0;
            for (int i = 0; i < points.Count; i++)
            {
                sum += points[i];
            }
            int score = points.Count > 0 ? sum * 100 / (points.Count * VenturesCatalogue.PointsMost) : 0;
            score += investor.LikesSector(f.Sector) ? VenturesCatalogue.LikedScore : 0;
            score += (reputation - VenturesCatalogue.ReputationStart) / 5;
            return Math.Max(0, Math.Min(100, score));
        }

        /// <summary>The score this investor wants at this level.</summary>
        public static int Needed(Investor investor, int level)
        {
            return VenturesCatalogue.ScoreNeeded[(int)investor.Kind] + VenturesCatalogue.ThresholdShift[VenturesCatalogue.Level(level)];
        }

        /// <summary>
        /// The decision. Under the score they want, no. At or over it: a business that is running is valued at 40 to
        /// 90 days of its true recent profit and the investor takes from the most to the least of their range of
        /// shares, both by how far over the score is; the sum is their share of that value, inside their smallest and
        /// largest sums. A business not yet open gets 30% to 70% of its budget. An angel backs a business under
        /// thirty days old with their smallest sum whatever its profit.
        /// </summary>
        public static PitchDecision Decide(VenturesFile file, Investor investor, in PitchFacts f, string place, List<int> points, int level, int day)
        {
            var d = new PitchDecision { Score = Score(points, investor, f, file.Reputation), Needed = Needed(investor, level), Best = -1, Worst = -1 };
            for (int i = 0; i < points.Count; i++)
            {
                d.Best = d.Best < 0 || points[i] > points[d.Best] ? i : d.Best;
                d.Worst = d.Worst < 0 || points[i] < points[d.Worst] ? i : d.Worst;
            }
            if (d.Score < d.Needed)
            {
                d.Verdict = Verdict.LowScore;
                return d;
            }
            int k = (int)investor.Kind;
            double over = d.Needed >= 100 ? 1.0 : (d.Score - d.Needed) / (double)(100 - d.Needed);
            int share = VenturesCatalogue.Lerp(VenturesCatalogue.ShareMostBp[k], VenturesCatalogue.ShareLeastBp[k], over);
            d.ValueDays = VenturesCatalogue.Lerp(VenturesCatalogue.ValueDaysLeast, VenturesCatalogue.ValueDaysMost, over);
            if (!f.Running)
            {
                int sum = (int)Math.Min(investor.ChequeMost, (long)f.Budget * VenturesCatalogue.Lerp(VenturesCatalogue.BudgetShareLeastPct, VenturesCatalogue.BudgetShareMostPct, over) / 100) / 100 * 100;
                if (file.Pledges.Count >= VenturesCatalogue.PledgesMost || file.Offers.Count >= VenturesCatalogue.OffersMost)
                {
                    d.Verdict = Verdict.NoRoom;
                }
                else if (sum < investor.ChequeLeast)
                {
                    d.Verdict = Verdict.TooSmall;
                }
                else
                {
                    d.Amount = sum;
                    d.ShareBp = share;
                }
                return d;
            }
            int deals;
            int room = VenturesCatalogue.ShareCapBp - file.ShareOn(place, day, out deals);
            if (room <= 0 || deals >= VenturesCatalogue.DealsPerBusiness || file.Deals.Count >= VenturesCatalogue.DealsMost || file.HasDeal(place, investor.Id)
                || file.Offers.Count >= VenturesCatalogue.OffersMost || file.HasOffer(place))
            {
                d.Verdict = Verdict.NoRoom;
                return d;
            }
            if (OfferRules.Terms(file, investor, place, f.AvgProfit7, d.ValueDays, share, day, out d.Amount, out d.ShareBp))
            {
                return d;
            }
            if (investor.Kind == InvestorKind.Angel && f.AgeDays < VenturesCatalogue.YoungDays)
            {
                d.Amount = investor.ChequeLeast;
                d.ShareBp = Math.Min(share, room);
                d.OnFounder = true;
                return d;
            }
            d.Amount = 0;
            d.ShareBp = 0;
            d.Verdict = f.AvgProfit7 <= 0f ? Verdict.NoProfit : Verdict.TooSmall;
            return d;
        }
    }
}
