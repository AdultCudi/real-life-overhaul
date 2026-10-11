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
        ///   the Planner  a source of its own (<see cref="FinanceSource"/>): tomorrow's payments, a missed payment,
        ///                a change of grade
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
                EconomyBridges.Legal = ReadLegal;
                EconomyBridges.LegalReady = () => JusticeState.Loaded;
                EconomyBridges.OtherDebts = AddDebts;
                PlannerDirector.Instance?.AddSource(Source);
            }

            internal static void Disconnect()
            {
                EconomyBridges.Reset();   // the Planner's director goes with the city, and its sources with it
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
                }
                catch (Exception e)
                {
                    Fault("EconomyLinks.OtherDebts", e);
                }
            }

            /// <summary>
            /// The Finance rows of the Planner: what the credit accounts take tomorrow (one row with the total), a
            /// row on the day after a payment was missed, and a row on the day the grade changed. It reads the credit
            /// file and the accounts and changes nothing. Ids: 1, 2 and 3 with the day, so a reminder keeps its row.
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
                        due += Math.Max(0f, accounts[i].PaymentDue);
                    }
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
            }
        }
    }
}
