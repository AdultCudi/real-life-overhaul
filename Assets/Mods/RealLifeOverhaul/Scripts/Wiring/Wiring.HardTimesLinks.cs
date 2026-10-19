using System;
using System.Collections.Generic;
using RealLifeOverhaul.Economy;
using HardSigns = RealLifeOverhaul.Family.HardSigns;
using HardTimesBridges = RealLifeOverhaul.Family.HardTimesBridges;
using IHardTimesBridge = RealLifeOverhaul.Family.IHardTimesBridge;

namespace RealLifeOverhaul
{
    public static partial class Wiring
    {
        /// <summary>
        /// Family life's difficult moments (Family/HardTimesWatch) with what the other features know of the player's
        /// days. The door that is filled in, HardTimesBridges.Life, answers from what each feature already keeps:
        ///
        ///   home life     hours on a job over the last days, stress, sleep owed, meals skipped and the diet, how tidy
        ///                 the home is (each only while its part of home life is on)
        ///   careers       the professional job's burnout
        ///   social life   the partner's last call, text or meeting, their last outing together, the last time they
        ///                 were stood up or kept waiting, and their birthday (only while it keeps the family's partner)
        ///   travel        a trip the partner came along on
        ///   the economy   net worth as the Finance app shows it, the businesses, and from the game's own transactions
        ///                 of the last three days the largest purchase (not a running cost, not the family's) and what
        ///                 was borrowed
        ///
        /// Read once a game hour (the days) and once a game day (the money), never per frame. A fault is said once
        /// (<see cref="Fault"/>) and the figures it would have given stay unknown, so no moment comes of them.
        /// </summary>
        public static class HardTimesLinks
        {
            private const string FamilyOwn = "reallife_transaction_family";   // the family's own: no surprise to it
            private const int StatementDays = 3;   // days a purchase or a loan on the statement is still news

            internal static void Connect()
            {
                HardTimesBridges.Life = new DaysForHardTimes();
            }

            internal static void Disconnect()
            {
                HardTimesBridges.Life = null;
            }

            private sealed class DaysForHardTimes : IHardTimesBridge
            {
                private readonly List<BuildingRegistration> _businesses = new List<BuildingRegistration>(16);

                public void Days(ref HardSigns signs, string partner)
                {
                    Home(ref signs);
                    Career(ref signs);
                    Social(ref signs, partner);
                    Trip(ref signs, partner);
                }

                public void Money(ref HardSigns signs)
                {
                    try
                    {
                        Books.Businesses(_businesses);
                        signs.OwnsBusiness = _businesses.Count > 0;
                        _businesses.Clear();
                        signs.NetWorth = Books.NetWorth();
                        signs.NetKnown = SaveGameManager.Current != null;
                    }
                    catch (Exception e)
                    {
                        _businesses.Clear();
                        signs.NetKnown = false;
                        Fault("HardTimesLinks.Money (net worth)", e);
                    }
                    try
                    {
                        Statement(ref signs);
                    }
                    catch (Exception e)
                    {
                        signs.BigBuy = signs.BigBuyBefore = signs.Borrowed = 0f;
                        Fault("HardTimesLinks.Money (the statement)", e);
                    }
                }

                private static void Home(ref HardSigns signs)
                {
                    try
                    {
                        if (!HomeState.Loaded || !Settings.HomeLife)
                        {
                            return;
                        }
                        signs.WorkHours = HomeState.WorkHours;
                        if (Settings.HomeNeeds)
                        {
                            signs.Stress = HomeState.Stress;
                            signs.Tidy = HomeTidy.HasHome ? HomeState.Tidy : -1f;
                        }
                        if (Settings.HomeSleep)
                        {
                            signs.SleepDebt = HomeState.SleepDebt;
                        }
                        if (Settings.HomeFitness)
                        {
                            int skipped = 0;
                            for (int d = 0; d < 3; d++)
                            {
                                skipped += HomeState.Meals(d, Meal.Skipped);
                            }
                            signs.Skipped = skipped;
                            signs.Diet = HomeState.DietScore;
                        }
                    }
                    catch (Exception e)
                    {
                        Fault("HardTimesLinks.Home", e);
                    }
                }

                private static void Career(ref HardSigns signs)
                {
                    try
                    {
                        Careers.CareersDirector careers = Careers.CareersDirector.Instance;
                        Careers.WorkFile work = careers != null ? careers.Work : null;
                        if (work != null && work.Job != null)
                        {
                            signs.Burnout = work.Job.Burnout;
                        }
                    }
                    catch (Exception e)
                    {
                        Fault("HardTimesLinks.Career", e);
                    }
                }

                private static void Social(ref HardSigns signs, string partner)
                {
                    try
                    {
                        SocialPerson p = SocialState.Loaded && Settings.Social ? SocialState.Partner : null;
                        if (p == null || string.IsNullOrEmpty(partner) || p.Name != partner)
                        {
                            return;   // the social life keeps somebody else, or nobody
                        }
                        signs.ContactDay = p.LastContactDay;
                        signs.OutingDay = p.LastOutingDay;
                        int stood = -1;
                        for (int i = 0; i < p.Memories.Count; i++)
                        {
                            SocialMemory memory = p.Memories[i];
                            bool letDown = memory.Code == MemoryCode.StoodUp || memory.Code == MemoryCode.Late;
                            if (letDown && memory.Day > stood)
                            {
                                stood = memory.Day;
                            }
                        }
                        signs.StoodUpDay = stood;
                        signs.BirthdayToday = SocialTalk.BirthdayIn(p) == 0;
                        int year = (Math.Max(1, SocialLife.Day) - 1) / Math.Max(1, SocialPlaces.DaysPerYear);
                        signs.BirthdayWished = p.BirthdayYear == year;
                    }
                    catch (Exception e)
                    {
                        Fault("HardTimesLinks.Social", e);
                    }
                }

                private static void Trip(ref HardSigns signs, string partner)
                {
                    try
                    {
                        signs.AwayTogether = TravelState.Away && !string.IsNullOrEmpty(partner)
                                             && TravelState.CompanionName == partner;
                    }
                    catch (Exception e)
                    {
                        Fault("HardTimesLinks.Trip", e);
                    }
                }

                // The game's own transactions of the last days (oldest first, a week kept): the largest purchase, and
                // what was borrowed.
                private static void Statement(ref HardSigns signs)
                {
                    GameInstance save = SaveGameManager.Current;
                    if (save == null || save.Transactions == null)
                    {
                        return;
                    }
                    int from = save.Day - (StatementDays - 1);
                    float biggest = 0f, before = 0f, borrowed = 0f;
                    foreach (Transaction t in save.Transactions)
                    {
                        if (t == null || t.timestamp == null || t.timestamp.Day < from || t.amount == 0f)
                        {
                            continue;
                        }
                        string type = t.transactionType ?? string.Empty;
                        if (type.StartsWith(FamilyOwn, StringComparison.Ordinal))
                        {
                            continue;
                        }
                        int category = CashCategory.For(type);
                        bool bought = category == CashCategory.Investments || category == CashCategory.Other;
                        if (t.amount < 0f && bought && -t.amount > biggest)
                        {
                            biggest = -t.amount;
                            before = t.balance - t.amount;   // the balance after it, and what it took
                        }
                        else if (t.amount > 0f && category == CashCategory.Loans && Borrowing(type))
                        {
                            borrowed += t.amount;
                        }
                    }
                    signs.BigBuy = biggest;
                    signs.BigBuyBefore = before;
                    signs.Borrowed = borrowed;
                }

                // Money in of the credit side that is a loan taken (a bank's, a card drawn, an advance, a friend's),
                // not interest earned or a loan paid back to the player.
                private static bool Borrowing(string type)
                {
                    for (int i = 0; i < NotBorrowing.Length; i++)
                    {
                        if (type.IndexOf(NotBorrowing[i], StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            return false;
                        }
                    }
                    return true;
                }

                private static readonly string[] NotBorrowing = { "interest", "back", "returned", "paid", "refund" };
            }
        }
    }
}
