using System;
using System.Collections.Generic;
using RealLifeOverhaul.Economy;
using RealLifeOverhaul.Economy.Ventures;
using RealLifeOverhaul.Planner;
using UnityEngine;

namespace RealLifeOverhaul
{
    public static partial class Wiring
    {
        /// <summary>
        /// The cleaning service and selling a business (ServicesBridges) with Ventures (investors bought out through its
        /// own buy-out), the credit side (business loans paid off exactly as the game's EconoView does it), running a
        /// business and the Planner. Called when a business's value is shown and when a sale completes.
        /// </summary>
        public static class ServicesLinks
        {
            /// <summary>The services' rows in the Planner: offers on their last day, and a part of a price on its day. Read only.</summary>
            private sealed class ServicesSource : IPlannerSource
            {
                public string Id => "services";

                public bool Available => Settings.Economy && (Settings.EconomyCleaning || Settings.EconomySale) && ServicesDirector.Instance != null && ServicesDirector.Instance.Running;

                public void Collect(int fromDay, int toDay, PlannerList into)
                {
                    ServicesDirector d = ServicesDirector.Instance;
                    ServicesState s = d.State;
                    if (s == null)
                    {
                        return;
                    }
                    for (int i = 0; d.CleaningOn && i < s.Offers.Count; i++)
                    {
                        CleaningOffer o = s.Offers[i];
                        PlannerEntry e = into.Add(Id, 1, ServicesCatalogue.Number(o.Place), PlannerCategory.Business, o.Until);
                        if (e != null && into.WantText)
                        {
                            e.Title = ServicesText.T("reallife_svc_plan_contract_title", "business", o.Name);
                            e.What = ServicesText.T("reallife_svc_plan_contract_what", "amount", ServicesText.Money(o.Fee), "standard", o.Standard.ToString());
                        }
                    }
                    for (int i = 0; d.SaleOn && i < s.SaleOffers.Count; i++)
                    {
                        SaleOffer o = s.SaleOffers[i];
                        SaleListing l = s.Listing(o.Place);
                        PlannerEntry e = into.Add(Id, 2, o.Id, PlannerCategory.Business, o.Deadline);
                        if (e != null && into.WantText)
                        {
                            e.Title = ServicesText.T("reallife_svc_plan_offer_title", "business", l != null ? l.Name : "?");
                            e.What = ServicesText.T("reallife_svc_plan_offer_what", "buyer", ServicesText.Buyer(o), "amount", ServicesText.Money(o.Price));
                        }
                    }
                    for (int i = 0; d.SaleOn && i < s.Dues.Count; i++)
                    {
                        SaleDue due = s.Dues[i];
                        PlannerEntry e = into.Add(Id, 3, (ServicesCatalogue.Number(due.Name) + i) & 0x3fffffff, PlannerCategory.Business, due.NextDay);
                        if (e != null && into.WantText)
                        {
                            e.Title = ServicesText.T("reallife_svc_plan_part_title", "business", due.Name);
                            e.What = ServicesText.T("reallife_svc_plan_part_what", "amount", ServicesText.Money(due.Part), "count", due.Left.ToString());
                        }
                    }
                }
            }

            private const string PayOffKey = "ba:transaction_loanpayoff";   // the game's own, for a loan paid off (LoanEntryUi.PayOffLoan)

            private static readonly ServicesSource Source = new ServicesSource();
            private static readonly List<int> Deals = new List<int>(4);
            private static readonly List<Loan> Loans = new List<Loan>(4);

            internal static void Connect()
            {
                ServicesBridges.InvestorsOwed = InvestorsOwed;
                ServicesBridges.PayInvestors = PayInvestors;
                ServicesBridges.LoansOwed = LoansOwed;
                ServicesBridges.SettleLoans = SettleLoans;
                ServicesBridges.BusinessGone = BusinessGone;
                CompanyBridges.OtherIncome = BusinessSales.GainsBetween;   // a sale's gain goes into the quarter's return
                CompanyBridges.Cleaned = CleaningService.Covers;            // the staff rota planner asks for no cleaning shift where the service cleans
                PlannerDirector.Instance?.AddSource(Source);
            }

            internal static void Disconnect()
            {
                ServicesBridges.Reset();   // the Planner's director goes with the city, and its sources with it
                CompanyBridges.OtherIncome = null;
                CompanyBridges.Cleaned = null;
            }

            // The deals on a business, by the key Ventures keeps a business under (Books.Key).
            private static VenturesDirector DealsOn(BuildingRegistration reg)
            {
                Deals.Clear();
                VenturesDirector ventures = VenturesDirector.Instance;
                VenturesFile file = ventures != null && ventures.Running ? ventures.File : null;
                if (file == null || reg == null || reg.Address == null)
                {
                    return null;
                }
                string key = Books.Key(reg.Address);
                for (int i = 0; i < file.Deals.Count; i++)
                {
                    if (file.Deals[i] != null && string.Equals(file.Deals[i].Place, key, StringComparison.Ordinal))
                    {
                        Deals.Add(file.Deals[i].Id);
                    }
                }
                return ventures;
            }

            private static int InvestorsOwed(BuildingRegistration reg)
            {
                try
                {
                    VenturesDirector ventures = DealsOn(reg);
                    long sum = 0;
                    for (int i = 0; ventures != null && i < Deals.Count; i++)
                    {
                        Deal deal = ventures.File.Deal(Deals[i]);
                        sum += deal != null ? Math.Max(0, ventures.BuyOutPrice(deal)) : 0;
                    }
                    return (int)Math.Min(int.MaxValue / 4, sum);
                }
                catch (Exception e)
                {
                    Fault("ServicesLinks.InvestorsOwed", e);
                    return 0;
                }
            }

            private static bool PayInvestors(BuildingRegistration reg)
            {
                try
                {
                    VenturesDirector ventures = DealsOn(reg);
                    for (int i = 0; ventures != null && i < Deals.Count; i++)
                    {
                        if (!ventures.BuyOut(Deals[i]))
                        {
                            return false;
                        }
                    }
                    return true;
                }
                catch (Exception e)
                {
                    Fault("ServicesLinks.PayInvestors", e);
                    return false;
                }
            }

            // The game's loans that the credit side has marked as business loans secured on this address.
            private static void SecuredOn(Address address)
            {
                Loans.Clear();
                EconomyDirector credit = EconomyDirector.Instance;
                CreditFile file = credit != null && credit.Running && credit.Desk != null ? credit.Desk.File : null;
                if (file == null || address == null)
                {
                    return;
                }
                string key = Books.Key(address);
                for (int i = 0; i < file.Loans.Count; i++)
                {
                    LoanMark mark = file.Loans[i];
                    var loan = mark != null && mark.Purpose == LoanPurpose.Business && string.Equals(mark.Security, key, StringComparison.Ordinal) ? mark.Ref as Loan : null;
                    if (loan != null && loan.remainingAmount > 0f)
                    {
                        Loans.Add(loan);
                    }
                }
            }

            private static int LoansOwed(Address address)
            {
                try
                {
                    SecuredOn(address);
                    float sum = 0f;
                    for (int i = 0; i < Loans.Count; i++)
                    {
                        sum += Loans[i].remainingAmount;
                    }
                    Loans.Clear();
                    return Mathf.CeilToInt(Mathf.Min(sum, int.MaxValue / 4));
                }
                catch (Exception e)
                {
                    Fault("ServicesLinks.LoansOwed", e);
                    return 0;
                }
            }

            private static bool SettleLoans(Address address)
            {
                try
                {
                    SecuredOn(address);
                    GameInstance save = SaveGameManager.Current;
                    bool all = true;
                    for (int i = 0; save != null && i < Loans.Count; i++)
                    {
                        Loan loan = Loans[i];
                        float amount = loan.remainingAmount;
                        var info = new TransactionInfo(PayOffKey, new Dictionary<string, string>(1) { { "businessName", GameLoans.BankName(loan) } });
                        if (!GameManager.ChangeMoneySafe(-amount, info, null, null, false, false))
                        {
                            all = false;
                            continue;
                        }
                        loan.remainingAmount -= amount;
                        save.Loans.Remove(loan);
                        GameEvent.Invoke(GameLoans.LoanPaidEvent);
                    }
                    Loans.Clear();
                    return all;
                }
                catch (Exception e)
                {
                    Fault("ServicesLinks.SettleLoans", e);
                    return false;
                }
            }

            private static void BusinessGone(string place)
            {
                try
                {
                    CompanyDirector.Instance?.BusinessGone(place);
                }
                catch (Exception e)
                {
                    Fault("ServicesLinks.BusinessGone", e);
                }
            }
        }
    }
}
