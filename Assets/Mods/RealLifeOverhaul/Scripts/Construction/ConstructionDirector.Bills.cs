using System;
using UI.Notification;

namespace RealLifeOverhaul.Construction
{
    /// <summary>
    /// Unpaid bills (BillRules): a tender's client or a block's off plan buyers who pay late or not at all, the company's
    /// letters and its claim in court (a civil claim of Justice's, the Sue bridge); and the company's own debts, a crew's
    /// wages it could not pay and a merchant's account, paid as soon as the cash is there or taken to court by the creditor
    /// (the Unpaid bridge). On a handover, a day's pay, an order and the day's pass; nothing per frame.
    /// </summary>
    public sealed partial class ConstructionDirector
    {
        private BillFile NewBill(BillKind kind, int project, int who, long amount, int day, int due)
        {
            var b = new BillFile { Id = _state.NextBill++, Kind = kind, Project = project, Who = who, Amount = amount, Raised = day, Due = due, PaysOn = due };
            _state.Bills.Add(b);
            _state.Tidy();
            return b;
        }

        // ------------------------------------------------------------------ owed to the company

        // A contract's final payment at the handover: on the day, or a bill the client pays late or not at all. True when it is a bill.
        private bool LateClient(ProjectFile p, long pay, int day)
        {
            if (!Settings.ConstructionCity || pay <= 0)
            {
                return false;
            }
            bool city = p.Source != TenderSource.Client;
            int paysOn = BillRules.ClientPays(_state.Seed, p.Id, day, city);
            if (paysOn == day)
            {
                return false;
            }
            BillFile b = NewBill(BillKind.Client, p.Id, p.Client, pay, day, day);
            b.PaysOn = paysOn;
            b.City = city;
            Logger?.Info("Construction: project " + p.Id + "'s client owes its final " + pay + (paysOn == BillRules.Never ? ", and will not pay unless chased or sued." : ", to come on day "
                         + paysOn + "."));
            Tell(NotificationType.Warning, "reallife_bld_notice_bill_late", ProjectName(p), null, TabCity, b.Id);
            return true;
        }

        // A block's off plan buyers at the handover: those who pay late keep their share back, a bill of the company's.
        private void LateBuyers(ProjectFile p, int day)
        {
            if (!Settings.ConstructionCity || p.OffPlanBalance <= 0 || p.FlatsOffPlan <= 0)
            {
                return;
            }
            int late = BillRules.BuyersLate(_state.Seed, p.Id, p.FlatsOffPlan, out int never);
            if (late <= 0)
            {
                return;
            }
            long share = p.OffPlanBalance * late / p.FlatsOffPlan;
            p.OffPlanBalance -= share;
            BillFile b = NewBill(BillKind.Buyers, p.Id, late, share, day, day);
            b.PaysOn = BillRules.BuyersPay(_state.Seed, p.Id, day, late, never);
            Logger?.Info("Construction: " + late + " of project " + p.Id + "'s off plan buyers keep back " + share + (b.PaysOn == BillRules.Never ? " (" + never + " will not pay)" : " until day "
                         + b.PaysOn) + ".");
            Tell(NotificationType.Warning, "reallife_bld_notice_buyers_late", ProjectName(p), null, TabCity, b.Id, late);
        }

        /// <summary>The company writes to a debtor who has not paid. Returns a text key.</summary>
        public string Chase(BillFile b)
        {
            int day = ConstructionGame.Day;
            if (b == null || !b.Live || !b.Owed || b.Case > 0)
            {
                return "reallife_bld_answer_bill_closed";
            }
            if (!BillRules.MayChase(b.Chased, b.ChasedDay, b.Due, day))
            {
                return b.Chased >= BillRules.ChaseMost ? "reallife_bld_answer_chase_done" : "reallife_bld_answer_chase_wait";
            }
            b.Chased++;
            b.ChasedDay = day;
            b.PaysOn = BillRules.AfterChase(_state.Seed, b.Id, b.Chased, day, b.PaysOn);
            Logger?.Info("Construction: bill " + b.Id + " chased (" + b.Chased + "): the debtor " + (b.PaysOn == BillRules.Never ? "still will not pay." : "pays on day " + b.PaysOn + "."));
            Changed();
            return "reallife_bld_answer_chased";
        }

        /// <summary>The company takes a debtor to court: a civil claim of its own, the court's fee now. Returns a text key.</summary>
        public string SueFor(BillFile b)
        {
            int day = ConstructionGame.Day;
            if (b == null || !b.Live || !b.Owed || b.Case > 0)
            {
                return "reallife_bld_answer_bill_closed";
            }
            if (!BillRules.MaySue(b.Chased, b.Due, day))
            {
                return "reallife_bld_answer_sue_wait";
            }
            int number = Sue(b.Amount, DebtorWords(b), BillRules.Strength(b.Chased, b.City));
            if (number <= 0)
            {
                return "reallife_bld_answer_no_court";
            }
            b.Case = number;
            b.ChasedDay = day;
            Logger?.Info("Construction: bill " + b.Id + " taken to court as claim " + number + ".");
            Changed();
            return "reallife_bld_answer_sued";
        }

        // ------------------------------------------------------------------ owed by the company

        // A crew's day the company could not pay: its wages run up on the crew's bill.
        private void OweWages(CrewFile c, int day)
        {
            if (!Settings.ConstructionCity)
            {
                return;
            }
            BillFile b = _state.OpenBill(BillKind.Wages, 0, c.Id) ?? NewBill(BillKind.Wages, 0, c.Foreman, 0, day, day);
            b.Crew = c.Id;
            b.Amount += c.Rate;
        }

        // A day's pay made: what the crew is owed from before is paid too, if the cash is there.
        private void PayArrears(CrewFile c, int day)
        {
            BillFile b = _state.OpenBill(BillKind.Wages, 0, c.Id);
            if (b != null && ConstructionGame.Take(b.Amount, "reallife_transaction_bld_crews", ForemanWords(c), true))
            {
                Close(b, BillFile.Settled, day);
                _state.PayOut(b.Amount, ConstructionGame.Day);
            }
        }

        /// <summary>An order a merchant puts on the company's account when its cash is short: false when it will not (no standing, an account overdue).</summary>
        private bool OnAccount(ProjectFile p, int merchant, long cost, int day)
        {
            if (!Settings.ConstructionCity || !BillRules.Account(_state.Firm.Reputation, Overdue(BillKind.Merchant, day)))
            {
                return false;
            }
            NewBill(BillKind.Merchant, p.Id, merchant, cost, day, day + BillRules.MerchantTerms);
            Logger?.Info("Construction: project " + p.Id + "'s order of " + cost + " is on merchant " + merchant + "'s account, due on day " + (day + BillRules.MerchantTerms) + ".");
            return true;
        }

        private bool Overdue(BillKind kind, int day)
        {
            for (int i = 0; i < _state.Bills.Count; i++)
            {
                BillFile b = _state.Bills[i];
                if (b.Live && b.Kind == kind && day > b.Due)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>The company pays a debt of its own now (a crew's wages, a merchant's account). Returns a text key.</summary>
        public string Settle(BillFile b)
        {
            if (b == null || !b.Live || b.Owed)
            {
                return "reallife_bld_answer_bill_closed";
            }
            if (!ConstructionGame.Take(b.Amount, b.Kind == BillKind.Wages ? "reallife_transaction_bld_crews" : "reallife_transaction_bld_materials", CreditorWords(b), true))
            {
                return "reallife_bld_answer_no_money";
            }
            Paid(b);
            Close(b, BillFile.Settled, ConstructionGame.Day);
            Changed();
            return "reallife_bld_answer_settled";
        }

        // What a debt of the company's paid is spent on: the project's books, or the company's.
        private void Paid(BillFile b)
        {
            ProjectFile p = _state.Project(b.Project);
            if (p != null)
            {
                Spend(p, b.Amount);
            }
            else
            {
                _state.PayOut(b.Amount, ConstructionGame.Day);
            }
        }

        // ------------------------------------------------------------------ the day

        private void BillsDay(int day)
        {
            for (int i = 0; i < _state.Bills.Count; i++)
            {
                BillFile b = _state.Bills[i];
                if (!b.Live)
                {
                    continue;
                }
                if (b.Owed)
                {
                    OwedDay(b, day);
                }
                else
                {
                    OwingDay(b, day);
                }
            }
        }

        // A debt owed to the company: paid on its day, or decided by the court once it is there.
        private void OwedDay(BillFile b, int day)
        {
            if (b.Case > 0)
            {
                int result = SuitResult(b.Case);
                if (result == 1)
                {
                    Collected(b);
                    Close(b, BillFile.Won, day);
                    Tell(NotificationType.Success, "reallife_bld_notice_claim_won", DebtorWords(b), null, TabCity, b.Id);
                }
                else if (result == 0 || (result == -2 && day - b.ChasedDay > JusticeHearDaysMost))
                {
                    Close(b, BillFile.Lost, day);
                    Tell(NotificationType.Warning, "reallife_bld_notice_claim_lost", DebtorWords(b), null, TabCity, b.Id);
                }
                return;
            }
            if (b.PaysOn != BillRules.Never && day >= b.PaysOn)
            {
                Collected(b);
                Pay(b);
                Close(b, BillFile.Paid, day);
                Tell(NotificationType.Success, "reallife_bld_notice_bill_paid", DebtorWords(b), null, TabCity, b.Id);
            }
        }

        private const int JusticeHearDaysMost = 20;   // days a claim with no court to read it waits before it is taken as lost

        // Money owed to the company comes in: the books, and the project's loan and investor take their share of a sale's.
        private void Collected(BillFile b)
        {
            _state.TakeIn(b.Amount, ConstructionGame.Day);
            ProjectFile p = _state.Project(b.Project);
            if (p != null && b.Kind == BillKind.Client)
            {
                p.ClientPaid += b.Amount;
            }
        }

        private void Pay(BillFile b)
        {
            ProjectFile p = _state.Project(b.Project);
            string name = p != null ? ProjectName(p) : DebtorWords(b);
            if (b.Kind == BillKind.Buyers && p != null)
            {
                long toPlayer = MoneyRules.Share(b.Amount, p.LoanOwed, p.InvestorOwed, out long toLoan, out long toInvestor);
                SalePaid(p, toLoan, toInvestor);
                ConstructionGame.Give(toPlayer, "reallife_transaction_bld_sales", name);
                return;
            }
            ConstructionGame.Give(b.Amount, b.Kind == BillKind.Buyers ? "reallife_transaction_bld_sales" : "reallife_transaction_bld_contract", name);
        }

        // A debt of the company's: paid on its day if the cash is there; left too long, the creditor sues.
        private void OwingDay(BillFile b, int day)
        {
            if (b.Kind == BillKind.Merchant && day >= b.Due && ConstructionGame.Take(b.Amount, "reallife_transaction_bld_materials", CreditorWords(b), true))
            {
                Paid(b);
                Close(b, BillFile.Settled, day);
                return;
            }
            if (!BillRules.CreditorSues(b.Kind, b.Due, day))
            {
                return;
            }
            b.Case = Unpaid(b.Amount, CreditorWords(b), ConstructionText.T(b.Kind == BillKind.Wages ? "reallife_bld_bill_what_wages" : "reallife_bld_bill_what_materials"));
            _state.Firm.Reputation = FirmRules.Moved(_state.Firm.Reputation, -BillRules.RepPaidLate);
            Close(b, BillFile.Sued, day);
            Logger?.Info("Construction: bill " + b.Id + " (" + b.Kind + ", " + b.Amount + ") left unpaid: the creditor sued, " + (b.Case > 0 ? "claim " + b.Case : "with no court to file with") + ".");
            Tell(NotificationType.Error, "reallife_bld_notice_sued_unpaid", CreditorWords(b), null, TabCity, b.Id);
        }

        private static void Close(BillFile b, int outcome, int day)
        {
            b.Closed = Math.Max(1, day);
            b.Outcome = outcome;
        }

        // ------------------------------------------------------------------ words

        /// <summary>Who owes a bill to the company, in words.</summary>
        public string DebtorWords(BillFile b)
        {
            if (b.Kind == BillKind.Buyers)
            {
                ProjectFile p = _state.Project(b.Project);
                return ConstructionText.T("reallife_bld_bill_buyers", "count", ConstructionText.N(b.Who), "name", p != null ? PlaceWords(p) : ConstructionText.T("reallife_bld_place_lot"));
            }
            ProjectFile project = _state.Project(b.Project);
            if (b.City || (project != null && project.Source != TenderSource.Client))
            {
                return ConstructionText.T("reallife_bld_bill_city");
            }
            return ConstructionText.T(BuildCatalogue.ClientKeys[b.Who % BuildCatalogue.ClientKeys.Length]);
        }

        /// <summary>Whom the company owes a bill, in words.</summary>
        public string CreditorWords(BillFile b)
        {
            return b.Kind == BillKind.Wages
                ? ConstructionText.T("reallife_bld_crew_name", "foreman", ConstructionText.T(BuildCatalogue.ForemanKeys[BuildCatalogue.Clamp(b.Who, 0, BuildCatalogue.ForemanKeys.Length - 1)]))
                : ConstructionText.T(BuildCatalogue.MerchantKeys[BuildCatalogue.Clamp(b.Who, 0, BuildCatalogue.MerchantCount - 1)]);
        }
    }
}
