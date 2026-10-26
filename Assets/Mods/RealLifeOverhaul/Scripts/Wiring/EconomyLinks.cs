using System;
using System.Collections.Generic;
using RealLifeOverhaul.Economy;
using RealLifeOverhaul.Planner;

namespace RealLifeOverhaul
{
    public static partial class Wiring
    {
        /// <summary>
        /// The economy (EconomyBridges) with the record the credit rating reads (Standing, Justice's debts, a fine at the
        /// police desk, Law's last 30 days), the debts in the Finance app's "what you owe", the Planner's Finance rows, the
        /// city's price level, the takings and cost rows of the broadband company, the player's bank and the tech firms
        /// in their headquarters' statement, and the life events' card and accountant.
        /// </summary>
        public static class EconomyLinks
        {
            private static readonly string[] DebtKeys = { "reallife_econ_debt_fine", "reallife_econ_debt_fee", "reallife_econ_debt_civil" };
            private static readonly FinanceSource Source = new FinanceSource();

            internal static void Connect()
            {
                Prices.Factor = () => Economy.CityEconomy.CostFactor;   // every price of the mod's follows the city's
                EconomyBridges.GivenBack = GivenBack;
                // a tech firm's takings are its products', contracts' and cloud's
                EconomyBridges.Takings = type => type == Utilities.BroadbandCatalogue.TakingsKey || OwnBankCatalogue.IsTakings(type) || Tech.FirmsGame.IsTakings(type);
                EconomyBridges.CostRow = type => type == Utilities.BroadbandCatalogue.NetworkKey || type == Utilities.BroadbandCatalogue.InstallsKey
                    ? Utilities.BroadbandCatalogue.NetworkRow
                    : OwnBankCatalogue.CostRow(type) ?? Tech.FirmsGame.RowOf(type);
                LifeAccounts.Card = HasCard;
                LifeAccounts.Accountant = () => Economy.CompanyDirector.Instance != null && Economy.CompanyDirector.Instance.HasAccountant;
                EconomyBridges.Legal = ReadLegal;
                EconomyBridges.LegalReady = () => JusticeState.Loaded;
                EconomyBridges.OtherDebts = AddDebts;
                PlannerDirector.Instance?.AddSource(Source);
                DayWarm.FeatureDays = () => EconomyDirector.AfterDay + EconomyDirector.AfterHour;   // the features' days the economy shares out in the game's midnight frame
            }

            private static bool GivenBack(string type) => type == CivicBridges.FareGivenBack || type == LentVehicle.RefundKey;

            /// <summary>What the private lender wants on its due day: what is owed now, with the days still to run on it.</summary>
            internal static int SharkOwed(SharkDebt shark, int today) =>
                SharkRules.OwedOnDueDay(shark.Borrowed) > shark.Owed && today < shark.DueDay
                    ? shark.Owed + SharkRules.ADay(shark.Borrowed) * (shark.DueDay - today) : shark.Owed;

            // An open credit card of the mod's banks, not one of the test buttons'.
            private static bool HasCard()
            {
                try
                {
                    Economy.CreditFile file = Economy.CreditLedger.File;
                    for (int i = 0; file != null && i < file.Accounts.Count; i++)
                    {
                        Economy.CreditAccount a = file.Accounts[i];
                        if (a != null && a.Kind == Economy.ProductKind.CreditCard && !a.Closed && !a.Test)
                        {
                            return true;
                        }
                    }
                    return false;
                }
                catch (Exception e)
                {
                    Fault("EconomyLinks.HasCard", e);
                    return false;
                }
            }

            internal static void Disconnect()
            {
                Prices.Factor = null;
                DayWarm.FeatureDays = null;
                LifeAccounts.Card = null;
                LifeAccounts.Accountant = null;
                EconomyBridges.Reset();   // DebtLinks' bridges too; the Planner's director goes with the city, and its sources with it
                BankTerms.Restore();   // now, not only once the economy's director is destroyed
            }

            private static LegalStanding ReadLegal()
            {
                var legal = new LegalStanding();
                try
                {
                    legal.Convictions = Standing.Convictions;
                    if (JusticeState.Loaded)
                    {
                        List<JusticeDebt> debts = JusticeState.Debts;
                        for (int i = 0; i < debts.Count; i++)
                        {
                            JusticeDebt debt = debts[i];
                            if (debt.Left <= 0)
                            {
                                continue;
                            }
                            legal.Outstanding += debt.Left;
                            legal.LateDays += Math.Max(0, debt.Missed);
                            legal.Judgments += debt.Kind == JusticeDebt.Civil ? 1 : 0;
                        }
                    }
                    legal.Outstanding += Math.Max(0, ModState.PendingFine);
                    legal.RecentOffences = Law.Record.Recent(CreditCatalogue.OffenceDays);
                }
                catch (Exception e)
                {
                    Fault("EconomyLinks.Legal", e);
                }
                return legal;
            }

            private static void AddDebts(List<DebtLine> into)
            {
                try
                {
                    if (JusticeState.Loaded)
                    {
                        List<JusticeDebt> debts = JusticeState.Debts;
                        for (int i = 0; i < debts.Count; i++)
                        {
                            JusticeDebt debt = debts[i];
                            if (debt.Left > 0)
                            {
                                into.Add(new DebtLine
                                {
                                    NameKey = DebtKeys[debt.Kind >= 0 && debt.Kind < DebtKeys.Length ? debt.Kind : 0],
                                    Owed = debt.Left, PerDay = Math.Max(0, debt.PerDay), LateDays = Math.Max(0, debt.Missed)
                                });
                            }
                        }
                    }
                    if (ModState.PendingFine > 0)
                    {
                        into.Add(new DebtLine { NameKey = "reallife_econ_debt_pending", Owed = ModState.PendingFine });
                    }
                    UtilityLinks.AddDebts(into);
                }
                catch (Exception e)
                {
                    Fault("EconomyLinks.OtherDebts", e);
                }
            }

            /// <summary>
            /// The Finance rows of the Planner: the game's loans tomorrow, a missed payment, a change of grade, the mod's
            /// accounts, the private lender and an approval running out. Read only. Ids: 1 to 3 with the day; 10 to 13 with
            /// the account's number; 14 and 15 with nothing; 20 and 21 are the city's economy's (<see cref="CityLinks.Rows"/>).
            /// </summary>
            private sealed class FinanceSource : IPlannerSource
            {
                public string Id => "finance";

                public bool Available => Settings.Economy && CreditLedger.Ready && SaveGameManager.Current != null;

                public void Collect(int fromDay, int toDay, PlannerList into)
                {
                    int today = SaveGameManager.Current.Day;
                    CreditFile file = CreditLedger.File;
                    float due = 0f;
                    IReadOnlyList<ICreditAccount> accounts = CreditLedger.Accounts;
                    for (int i = 0; i < accounts.Count; i++)
                    {
                        due += accounts[i].Key == GameLoans.AccountKey ? Math.Max(0f, accounts[i].PaymentDue) : 0f;   // the mod's accounts have a row each
                    }
                    Accounts(file, today, into);
                    CityLinks.Rows(Id, today, into);
                    if (due > 0f)
                    {
                        PlannerEntry e = into.Add(Id, 1, today + 1, PlannerCategory.Money, today + 1);
                        if (e != null && into.WantText)
                        {
                            e.Title = PlannerText.T("reallife_econ_plan_due_title");
                            e.What = PlannerText.T("reallife_econ_plan_due_what");
                            PlannerText.Costs(e, due, FinanceText.Money(due));
                            e.IfMissed = PlannerText.T("reallife_econ_plan_due_missed");
                        }
                    }
                    if (file.LastMissedCount > 0 && file.LastMissedDay == today - 1)
                    {
                        PlannerEntry e = into.Add(Id, 2, today, PlannerCategory.Money, today);
                        if (e != null && into.WantText)
                        {
                            e.Title = PlannerText.T("reallife_econ_plan_missed_title");
                            e.What = PlannerText.T("reallife_econ_plan_missed_what", "amount", FinanceText.Money(file.LastMissedAmount));
                        }
                    }
                    if (file.Shark.Active)
                    {
                        PlannerEntry e = into.Add(Id, 14, 0, PlannerCategory.Money, Math.Max(today, file.Shark.DueDay));
                        if (e != null && into.WantText)
                        {
                            e.Title = PlannerText.T("reallife_econ_plan_lender_title");
                            e.What = PlannerText.T("reallife_econ_plan_lender_what", "day", file.Shark.DueDay.ToString());
                            float owed = SharkOwed(file.Shark, today);
                            PlannerText.Costs(e, owed, FinanceText.Money(owed));
                            e.IfMissed = PlannerText.T("reallife_econ_plan_lender_missed");
                        }
                    }
                    if (file.Relief.HasApproval)
                    {
                        PlannerEntry e = into.Add(Id, 15, 0, PlannerCategory.Money, Math.Max(today, file.Relief.ApprovedDay + ProductCatalogue.ApprovalDays));
                        if (e != null && into.WantText)
                        {
                            e.Title = PlannerText.T("reallife_econ_plan_approval_title", "kind", FinanceText.T(ProductCatalogue.KindKeys[file.Relief.ApprovedKind]));
                            e.What = PlannerText.T("reallife_econ_plan_approval_what", "amount", FinanceText.Money(file.Relief.ApprovedAmount));
                        }
                    }
                    if (file.LastGradeDay == today && file.LastGradeWas >= 0 && file.LastGradeWas != file.LastGrade && file.LastGrade >= 0)
                    {
                        PlannerEntry e = into.Add(Id, 3, today, PlannerCategory.Money, today);
                        if (e != null && into.WantText)
                        {
                            e.Title = PlannerText.T("reallife_econ_plan_grade_title", "grade", FinanceText.Grade((CreditGrade)file.LastGrade));
                            e.What = PlannerText.T("reallife_econ_plan_grade_what", "score", file.LastScore.ToString(), "was", FinanceText.Grade((CreditGrade)file.LastGradeWas));
                        }
                    }
                }

                // What each of the mod's accounts takes tomorrow, and a card's two days.
                private void Accounts(CreditFile file, int today, PlannerList into)
                {
                    for (int i = 0; i < file.Accounts.Count; i++)
                    {
                        CreditAccount a = file.Accounts[i];
                        if (a.Closed || a.Frozen || a.Test)
                        {
                            continue;
                        }
                        int tomorrow = AccountRules.DueTomorrow(a, today);
                        if (tomorrow > 0)
                        {
                            PlannerEntry e = into.Add(Id, 10, a.Id, PlannerCategory.Money, today + 1);
                            if (e != null && into.WantText)
                            {
                                e.Title = CreditDesk.Name(a);
                                e.What = PlannerText.T("reallife_econ_plan_account_what", "lender", CreditDesk.LenderName(a));
                                PlannerText.Costs(e, tomorrow, FinanceText.Money(tomorrow));
                                e.IfMissed = PlannerText.T("reallife_econ_plan_account_missed");
                            }
                        }
                        if (a.Kind != ProductKind.CreditCard)
                        {
                            continue;
                        }
                        if (a.DueDay > today + 1 && a.MinimumLeft > 0)
                        {
                            PlannerEntry e = into.Add(Id, 11, a.Id, PlannerCategory.Money, a.DueDay);
                            if (e != null && into.WantText)
                            {
                                e.Title = PlannerText.T("reallife_econ_plan_minimum_title");
                                e.What = PlannerText.T("reallife_econ_plan_minimum_what", "amount", FinanceText.Money(a.StatementLeft));
                                PlannerText.Costs(e, a.MinimumLeft, FinanceText.Money(a.MinimumLeft));
                                e.IfMissed = PlannerText.T("reallife_econ_plan_account_missed");
                            }
                        }
                        if (a.Owed > 0 && a.NextDay > today)
                        {
                            PlannerEntry e = into.Add(Id, 12, a.Id, PlannerCategory.Money, a.NextDay);
                            if (e != null && into.WantText)
                            {
                                e.Title = PlannerText.T("reallife_econ_plan_statement_title");
                                e.What = PlannerText.T("reallife_econ_plan_statement_what", "days", ProductCatalogue.CardGraceDays.ToString());
                            }
                        }
                    }
                }
            }
        }
    }
}
