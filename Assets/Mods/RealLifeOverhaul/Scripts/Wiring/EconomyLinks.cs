using System;
using System.Collections.Generic;
using RealLifeOverhaul.Economy;
using RealLifeOverhaul.Planner;

namespace RealLifeOverhaul
{
    public static partial class Wiring
    {
        /// <summary>
        /// The economy with the rest of the mod (Economy/EconomyBridges). Economy calls no other feature and none
        /// calls it: what they have to do with each other is here.
        ///
        ///   Legal        the record the credit rating reads: convictions that still count (<see cref="Standing"/>),
        ///                civil judgments being paid off, fines and court debts owed and the days running on which one
        ///                could not be taken (Justice's debts), a fine waiting at the police desk (ModState), and the
        ///                lines on the police record of the last 30 days (Law)
        ///   LegalReady   Justice has read this save (its record is part of the rating's starting estimate)
        ///   OtherDebts   the same fines and court debts as lines for the Finance app's "what you owe"
        ///   the Planner  a source of its own (<see cref="FinanceSource"/>): tomorrow's payments by account, a missed
        ///                payment, a change of grade, a card's statement, the private lender's due day
        ///   Prices       the level every price of the mod's is at (Core/Prices): what the city adjusts the game's own
        ///                costs by
        ///   GivenBack    the mod's refunds of a game charge (a ride the day pass covered, a lent vehicle's ticket), which
        ///                the city's cost of living line counts against the game's charges of the day
        ///   Takings      the broadband company's bills (Scripts/Utilities), and its network's cost; the player's bank's
        ///   CostRow      takings, what its savers are paid and its losses (Economy/OwnBank): in their headquarters'
        ///                statement among the takings and the goods, so the game's tax and bank count them
        ///   LifeAccounts a card of the mod's banks and an accountant, for the life events that need one
        ///   on unload    the banks' own terms are put back at once (Economy's own object goes at the end of the frame)
        ///
        /// Everything is read from what those features already keep in public; nothing of theirs is changed.
        /// </summary>
        public static class EconomyLinks
        {
            private static readonly string[] DebtKeys = { "reallife_econ_debt_fine", "reallife_econ_debt_fee", "reallife_econ_debt_civil" };
            private static readonly FinanceSource Source = new FinanceSource();

            internal static void Connect()
            {
                Prices.Factor = () => Economy.CityEconomy.CostFactor;   // every price of the mod's follows the city's
                EconomyBridges.GivenBack = GivenBack;
                // the broadband company bills from its headquarters, the player's bank earns there, and a tech firm's takings are its
                // products', contracts' and cloud's (Tech/Firms, Tech/Infra)
                EconomyBridges.Takings = type => type == Utilities.BroadbandCatalogue.TakingsKey || OwnBankCatalogue.IsTakings(type) || Tech.FirmsGame.IsTakings(type);
                EconomyBridges.CostRow = type => type == Utilities.BroadbandCatalogue.NetworkKey || type == Utilities.BroadbandCatalogue.InstallsKey ? Utilities.BroadbandCatalogue.NetworkRow : OwnBankCatalogue.CostRow(type) ?? Tech.FirmsGame.RowOf(type);
                LifeAccounts.Card = HasCard;
                LifeAccounts.Accountant = () => Economy.CompanyDirector.Instance != null && Economy.CompanyDirector.Instance.HasAccountant;
                EconomyBridges.Legal = ReadLegal;
                EconomyBridges.LegalReady = () => JusticeState.Loaded;
                EconomyBridges.OtherDebts = AddDebts;
                PlannerDirector.Instance?.AddSource(Source);
            }

            private static bool GivenBack(string type) => type == CivicBridges.FareGivenBack || type == LentVehicle.RefundKey;

            // A credit card of the mod's banks, open and not one of the test buttons'.
            private static bool HasCard()
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

            internal static void Disconnect()
            {
                Prices.Factor = null;
                LifeAccounts.Card = null;
                LifeAccounts.Accountant = null;
                EconomyBridges.Reset();   // (the bridges of DebtLinks too) the Planner's director goes with the city, and its sources with it
                // The city is unloading or the mod is being switched off: the banks have their own terms again now, not
                // only when the economy's own object is destroyed at the end of the frame.
                BankTerms.Restore();
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
                    UtilityLinks.AddDebts(into);   // power and water bills owed (Scripts/Utilities)
                }
                catch (Exception e)
                {
                    Fault("EconomyLinks.OtherDebts", e);
                }
            }

            /// <summary>
            /// The Finance rows of the Planner: what the game's loans take tomorrow (one row with the total), a row on
            /// the day after a payment was missed, a row on the day the grade changed; and, for the mod's own accounts,
            /// each payment that leaves tomorrow by account, a card's statement day and the day its minimum is taken,
            /// the day the private lender wants its money and the last day of an approval for finance. (A hearing for
            /// a debt claim is Justice's own row.) It reads the credit file and the accounts and changes nothing.
            /// Ids: 1, 2 and 3 with the day; 10 to 13 with the account's number; 14 and 15 with nothing; 20 with the day
            /// and 21 with the kind of goods are the city's economy's (<see cref="CityLinks.Rows"/>).
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
                        due += accounts[i].Key == GameLoans.AccountKey ? Math.Max(0f, accounts[i].PaymentDue) : 0f;   // (the mod's accounts have a row each, below)
                    }
                    Accounts(file, today, into);
                    CityLinks.Rows(Id, today, into);   // the city's economy: tomorrow's rate decision, a shortage or a glut from tomorrow
                    if (due > 0f)
                    {
                        PlannerEntry e = into.Add(Id, 1, today + 1, PlannerCategory.Money, today + 1);
                        if (e != null && into.WantText)
                        {
                            e.Title = PlannerText.T("reallife_econ_plan_due_title");
                            e.What = PlannerText.T("reallife_econ_plan_due_what");
                            e.Money = PlannerText.T("reallife_planner_costs", "amount", FinanceText.Money(due));
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
                            e.Money = PlannerText.T("reallife_planner_costs", "amount", FinanceText.Money(SharkRules.OwedOnDueDay(file.Shark.Borrowed) > file.Shark.Owed && today < file.Shark.DueDay
                                ? file.Shark.Owed + SharkRules.ADay(file.Shark.Borrowed) * (file.Shark.DueDay - today) : file.Shark.Owed));
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

                // The mod's own accounts: what each takes tomorrow, and a card's two days.
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
                                e.Money = PlannerText.T("reallife_planner_costs", "amount", FinanceText.Money(tomorrow));
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
                                e.Money = PlannerText.T("reallife_planner_costs", "amount", FinanceText.Money(a.MinimumLeft));
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
