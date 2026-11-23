using System;
using RealLifeOverhaul.Ui;

namespace RealLifeOverhaul.Economy
{
    /// <summary>
    /// The facts of the bank's risk, cards and trading desk (and the automatic dividends' next sum) for both interfaces, read from the same rules the day runs, so
    /// the old app and the kit's screen show one figure. Game-free; a pass over a few dozen figures when a page is filled.
    /// </summary>
    public static partial class OwnBankReader
    {
        private static void ReadGrowth(OwnBankFacts f, OwnBank b, BankCity c, OwnBankWorld world, IOwnBankSay say)
        {
            ReadDividendsNext(f, b, c, world);
            ReadRisk(f, b, c, say);
            ReadCards(f, b, c, world, say);
            ReadDesk(f, b, c, say);
        }

        /// <summary>The keys of the warning signs, in the order of the BankSign flags.</summary>
        public static readonly string[] SignKeys =
        {
            "reallife_ob_sign_confidence", "reallife_ob_sign_cover", "reallife_ob_sign_ratio", "reallife_ob_sign_shock", "reallife_ob_sign_flight", "reallife_ob_sign_conc"
        };

        /// <summary>The warning signs of a set of flags as words, joined by commas.</summary>
        public static string SignWords(int signs, Func<string, string> words)
        {
            var t = new System.Text.StringBuilder(128);
            for (int i = 0; i < SignKeys.Length; i++)
            {
                if ((signs & (1 << i)) != 0)
                {
                    t.Append(t.Length > 0 ? ", " : string.Empty).Append(words(SignKeys[i]));
                }
            }
            return t.ToString();
        }

        private static void ReadDividendsNext(OwnBankFacts f, OwnBank b, BankCity c, OwnBankWorld world)
        {
            f.DivExpected = OwnBankRules.DividendExpected(b, c);
            Func<long, long> tax = world.Tax;
            f.DivExpectedTax = tax != null && f.DivExpected > 0 ? Math.Max(0, Math.Min(f.DivExpected, tax(f.DivExpected))) : 0;
            f.DivTaxBp = tax != null ? (int)(Math.Max(0, tax(1000000L)) / 100) : 0;
        }

        private static void ReadRisk(OwnBankFacts f, OwnBank b, BankCity c, IOwnBankSay say)
        {
            int lv = OwnBankRules.RiskOf(b);
            f.RiskLevel = lv;
            f.RiskOn = lv > BankRisk.Off;
            f.RiskNames = new string[BankRisk.Count];
            for (int i = 0; i < BankRisk.Count; i++)
            {
                f.RiskNames[i] = say.T(BankRisk.NameKeys[i]);
            }
            f.WarnRatio = OwnBankRules.LendRatioOf(b);
            f.Warned = OwnBankRules.Warned(b);
            f.WatchHeld = OwnBankRules.WatchHeld(b);
            f.Signs = OwnBankRules.Signs(b, c, f.Day, f.Confidence);
            f.SignWords = OwnBankReader.SignWords(f.Signs, say.T);
            f.CoverTenths = OwnBankRules.CoverDays(b);
            f.RunNeed = OwnBankRules.RunNeedDay(b);
            f.ShockNow = lv > BankRisk.Off ? OwnBankRules.ShockSegment(b.Seed, b.Month, b.Risk) : -1;
            f.ShockAhead = OwnBankRules.ShockAhead(b, f.Day, c.MonthDays);
            f.ShockNowName = f.ShockNow >= 0 ? say.T(OwnBankCatalogue.SegmentKeys[f.ShockNow]) : string.Empty;
            f.ShockAheadName = f.ShockAhead >= 0 ? say.T(OwnBankCatalogue.SegmentKeys[f.ShockAhead]) : string.Empty;
            f.ConcLimit = BankRisk.ConcLimit[BankRisk.Level(b.Risk)];
            f.Conc = new OwnConcFact[OwnBankCatalogue.Segments];
            for (int j = 0; j < f.Conc.Length; j++)
            {
                f.Conc[j] = new OwnConcFact { Name = say.T(OwnBankCatalogue.SegmentKeys[j]), Share = OwnBankRules.Concentration(b, j), Excess = OwnBankRules.ConcExcess(b, j) };
            }
            f.Borrowed = b.Interbank;
            f.BorrowRoom = OwnBankRules.BorrowRoom(b);
            f.BorrowBp = OwnBankRules.BorrowBp(c, b);
            f.BorrowedBp = b.InterbankBp;
            f.BorrowDue = b.InterbankDue;
            f.BorrowFee = OwnBankRules.BorrowFee(f.BorrowRoom);
            f.SaleDiscount = OwnBankRules.SaleDiscountOf(c, b);
            f.SaleMostShare = BankRisk.SaleMost;
            f.SoldFace = b.SoldFace;
            f.SoldLoss = b.SoldLoss;
            f.LendScale = OwnBankRules.LendScale(b);
            f.ToClear = OwnBankRules.CapitalToClear(b);
            f.RunHold = OwnBankRules.RunHold(c, b, OwnBankCatalogue.People);
            f.ReserveShare = OwnBankRules.ReserveShareOf(b);
            f.SaverFlight = BankRisk.FlightSpeed[BankRisk.Level(b.Risk)];
            Waterfall w = OwnBankRules.Resolve(b, OwnBankRules.WindFactor(b, c));
            f.WfAssets = w.Assets;
            f.WfSavers = w.Savers;
            f.WfSaversLoss = w.SaversLoss;
            f.WfBanks = w.Banks;
            f.WfBanksLoss = w.BanksLoss;
            f.WfFee = w.Fee;
            f.WfOwner = w.Owner;
            for (int j = 0; j < f.Segments.Length; j++)
            {
                f.Segments[j].SaleMost = OwnBankRules.SaleMostFace(b, j);
            }
        }

        private static void ReadCards(OwnBankFacts f, OwnBank b, BankCity c, OwnBankWorld world, IOwnBankSay say)
        {
            f.CardsOn = b.CardsOn;
            f.CardAuto = b.CardAuto;
            f.CardDecline = b.CardDecline;
            f.CardRate = b.CardRate;
            f.CardFee = b.CardFee;
            f.CardAutoLeast = CardTerms.AutoLeast;
            f.CardAutoMost = CardTerms.AutoMost;
            f.CardDeclineLeast = CardTerms.DeclineLeast;
            f.CardLineStep = CardTerms.LineStep;
            f.CardRateLeast = CardTerms.RateLeast;
            f.CardRateMost = CardTerms.RateMost;
            f.CardRateStep = CardTerms.RateStep;
            f.CardFees = new long[CardTerms.FeeSteps.Length];
            for (int i = 0; i < f.CardFees.Length; i++)
            {
                f.CardFees[i] = CardTerms.FeeCents(i, c.Level);
            }
            f.CardRivalBp = OwnBankRules.CardRivalBp(c);
            f.CardRateNow = OwnBankRules.CardBp(c, b);
            f.CardLimits = new long[CardTerms.Kinds][];
            f.CardKinds = new OwnCardKindFact[CardTerms.Kinds];
            int queued = 0;
            for (int j = 0; j < CardTerms.Kinds; j++)
            {
                f.CardLimits[j] = new long[CardTerms.LimitSteps];
                for (int s = 0; s < CardTerms.LimitSteps; s++)
                {
                    f.CardLimits[j][s] = CardTerms.LimitCents(j, s, c.Level);
                }
                OwnBankRules.CardShares(j, b.CardAuto, b.CardDecline, out int auto, out int queue, out _, out _);
                f.CardKinds[j] = new OwnCardKindFact
                {
                    Tier = say.T(CardTerms.TierKeys[j]), Name = say.T(OwnBankCatalogue.SegmentKeys[j]), Issue = b.CardIssue[j], LimitStep = b.CardLimit[j],
                        LimitCents = f.CardLimits[j][b.CardLimit[j]],
                    Holders = b.CardHolders[j], Queue = b.CardQueue[j], Wanted = OwnBankRules.CardWanted(c, b, j), Balance = b.CardBal[j], Arrears = b.CardArr[j],
                    DefaultPerMille = (int)Math.Round(OwnBankRules.DefaultPerMille(c, b, j) * (double)CardTerms.DefaultMult[j] / 1000.0 * Math.Max(1, b.CardRisk[j]) / 1000.0),
                    AutoShare = auto, QueueShare = queue
                };
                queued += b.CardQueue[j];
            }
            f.CardBook = b.CardBook;
            f.CardArrearsAll = b.CardArrears;
            f.CardLostAll = b.CardLostAll;
            f.CardsHeld = b.CardsHeld;
            f.CardsQueued = queued;
            f.CardFig = (long[])b.CardFig.Clone();
            f.CardLast = (long[])b.CardLast.Clone();
            f.OwnCard = b.OwnCard;
            f.OwnOwed = b.OwnOwed;
            f.OwnLimit = b.OwnLimit;
            f.OwnLostAll = b.OwnLostAll;
            f.OwnRateBp = b.OwnRateBp;
            f.OwnMost = OwnBankRules.OwnCardMost(b, world.Level);
            f.OwnLeast = OwnBankRules.At(CardTerms.BlackLeastCents, world.Level);
            f.OwnStep = OwnBankRules.At(CardTerms.BlackStepCents, world.Level);
            f.OwnName = say.T(CardTerms.BlackKey);
        }

        private static void ReadDesk(OwnBankFacts f, OwnBank b, BankCity c, IOwnBankSay say)
        {
            f.DeskOn = b.DeskOn;
            f.DeskMandate = b.DeskMandate;
            f.DeskShare = b.DeskShare;
            f.DeskLimit = b.DeskLimit;
            f.MandateNames = new string[BankDesk.Mandates];
            for (int i = 0; i < f.MandateNames.Length; i++)
            {
                f.MandateNames[i] = say.T(BankDesk.MandateKeys[i]);
            }
            f.DeskShareMost = BankDesk.ShareMost;
            f.DeskShareStep = BankDesk.ShareStep;
            f.DeskLimitLeast = BankDesk.LimitLeast;
            f.DeskLimitMost = BankDesk.LimitMost;
            f.DeskLimitStep = BankDesk.LimitStep;
            f.DeskCompanies = BankDesk.Companies[BankDesk.MandateOf(b.DeskMandate)];
            f.DeskCutKeep = BankDesk.CutKeep;
            f.BookValue = b.BookValue;
            f.BookCost = b.BookCost;
            f.BookRoom = OwnBankRules.DeskRoom(b);
            f.DeskPeak = b.DeskPeak;
            f.Custody = OwnBankRules.DeskCustody(b);
            f.DeskDrawdown = OwnBankRules.DeskDrawdown(b);
            f.DeskCuts = b.DeskCuts;
            f.DeskCutDay = b.DeskCutDay;
            f.Activity = OwnBankRules.ActivityOf(c);
            f.DeskBuying = b.DeskCutDay == 0 || f.Day >= b.DeskCutDay + BankDesk.CutMonths * Math.Max(1, c.MonthDays);
            f.Derivs = c.Derivs;
            f.DeskBrokerage = b.DeskBrokerage;
            f.DeskDerivs = b.DeskDerivs;
            f.DeskLastBrokerage = b.DeskLastBrokerage;
            f.DeskLastDerivs = b.DeskLastDerivs;
            f.DeskAll = b.DeskCommissionAll;
            f.Book = new OwnHoldingFact[b.Book.Count];
            for (int i = 0; i < b.Book.Count; i++)
            {
                FundHolding h = b.Book[i];
                f.Book[i] = new OwnHoldingFact { Name = string.IsNullOrEmpty(h.Ticker) ? h.Name : h.Name + " (" + h.Ticker + ")", Qty = h.Qty, Cost = h.CostCents,
                    Value = h.Qty * h.LastCents, Year = h.Qty * h.DpsCents * 4 };
            }
        }
    }
}
