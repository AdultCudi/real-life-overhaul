using System;
using System.Collections.Generic;
using RealLifeOverhaul.Careers;
using RealLifeOverhaul.Economy;
using RealLifeOverhaul.Family;
using RealLifeOverhaul.Texts;

namespace RealLifeOverhaul
{
    public static partial class Wiring
    {
        /// <summary>
        /// RLO Texts' city talk of data v6 (Texts/TextsDirector.City) with the features it reads, through TextsBridges; none
        /// of them is changed, and each answer is what the feature already keeps in public:
        ///
        ///   Staff       running a business's staff files (Economy/Company: pay, the going rate, a raise asked for or agreed),
        ///               the player's own businesses only; the game's own facts of each are TextsGame's
        ///   Life        family life (children, the wedding, the partner), education (a graduation today), the court (the
        ///               newest case closed and its outcome), the stock market (the player's company listed), the books
        ///               (the newest business, and the net worth once a day at <see cref="WorthHour"/>)
        ///   People      the social life's people, as gossip and the life's texts read them
        ///   PersonSay   a text through the social life's own (SocialLife.SendText: its switch, daily cap and quiet hours,
        ///               one a person a day), worded through the phone's hold like every other (Wiring/TextsPhone)
        ///   BigFloor    the least move of the net worth that counts as a big win or loss, at today's prices
        ///
        /// COST: nothing per frame; a pass over the staff, the people and the businesses once a game hour.
        /// </summary>
        public static class TextsCity
        {
            public const int WorthHour = 6;          // the game hour the net worth is read for a day's big win or loss
            public const int BigFloor = 25000;       // the least such move at the start's prices
            public const int KnownFrom = 60;         // the city's recognition of the player (0 to 100) from which people know of them
            public const int LikedFrom = 50;         // ...and the public's view (0 to 100) from which they are well known rather than infamous

            private static readonly List<BuildingRegistration> Regs = new List<BuildingRegistration>(8);

            internal static void Connect()
            {
                TextsBridges.Staff = Staff;
                TextsBridges.Life = Life;
                TextsBridges.People = People;
                TextsBridges.PersonSay = PersonSay;
                TextsBridges.BigFloor = () => Prices.At(BigFloor);
            }

            internal static void Disconnect()
            {
                TextsBridges.Reset();
            }

            private static void Staff(List<TextStaff> into)
            {
                try
                {
                    into.Clear();
                    CompanyDirector company = CompanyDirector.Instance;
                    CompanyState state = company != null ? company.State : null;
                    if (state == null)
                    {
                        return;
                    }
                    for (int i = 0; i < state.Staff.Count; i++)
                    {
                        StaffFile f = state.Staff[i];
                        if (f == null || string.IsNullOrEmpty(f.Id) || !f.Seen)
                        {
                            continue;
                        }
                        into.Add(new TextStaff
                        {
                            Id = f.Id, Name = f.Name, First = First(f.Name), Shop = company.ShopName(f.Place), Seed = (int)TextsCityRules.Hash(0, f.Id, 0), Paid = f.Paid,
                            Going = f.Going, AskDay = f.AskDay, AskTo = f.AskTo, Raise = f.Raise
                        });
                    }
                }
                catch (Exception e)
                {
                    Fault("RLO Texts, the staff", e);
                    into.Clear();
                }
            }

            private static void Life(TextLife v)
            {
                try
                {
                    v.Children = 0;
                    v.ChildName = v.Partner = v.Degree = v.Listed = v.Business = null;
                    v.Adopted = v.Married = v.CourtGuilty = v.HasWorth = false;
                    v.MarriedDay = v.CourtClosed = v.ListedDay = v.BusinessDay = 0;
                    Family(v);
                    v.Degree = CareersDirector.Instance != null ? CareersDirector.Instance.GraduatedToday() : null;
                    for (int i = 0; JusticeState.Loaded && i < JusticeState.Cases.Count; i++)
                    {
                        JusticeCase c = JusticeState.Cases[i];
                        if (c != null && c.Stage == CaseStage.Closed && c.Outcome != CaseOutcome.None && c.Number > v.CourtClosed)
                        {
                            v.CourtClosed = c.Number;   // (the newest case closed: its number only grows)
                            v.CourtGuilty = c.WasConviction;
                        }
                    }
                    Markets.Stock own = Markets.MarketDirector.Instance != null ? Markets.MarketDirector.Instance.MyCompany : null;
                    if (own != null && !string.IsNullOrEmpty(own.Name))
                    {
                        v.Listed = own.Name;
                        v.ListedDay = own.ListedDay;
                    }
                    Books.Businesses(Regs);
                    for (int i = 0; i < Regs.Count; i++)
                    {
                        BuildingRegistration reg = Regs[i];
                        if (reg != null && reg.HasEstablishedBusiness && reg.creationDay > v.BusinessDay)
                        {
                            v.Business = reg.BusinessName;
                            v.BusinessDay = reg.creationDay;
                        }
                    }
                    GameInstance save = SaveGameManager.Current;
                    if (save != null && save.Hour == WorthHour)
                    {
                        v.Worth = Books.NetWorth();
                        v.HasWorth = true;
                    }
                }
                catch (Exception e)
                {
                    Fault("RLO Texts, the player's life", e);
                    v.HasWorth = false;
                }
            }

            private static void Family(TextLife v)
            {
                if (!FamilyState.Loaded)
                {
                    return;
                }
                int newest = -1;
                for (int i = 0; i < FamilyState.S.Children.Count; i++)
                {
                    ChildRecord c = FamilyState.S.Children[i];
                    if (c == null)
                    {
                        continue;
                    }
                    v.Children++;
                    if (c.ArrivedDay >= newest)
                    {
                        newest = c.ArrivedDay;
                        v.ChildName = string.IsNullOrEmpty(c.Name) ? null : First(c.Name);
                        v.Adopted = c.Adopted;
                    }
                }
                if (FamilyState.Together && !string.IsNullOrEmpty(FamilyState.S.PName))
                {
                    v.Partner = First(FamilyState.S.PName);
                }
                v.Married = FamilyState.Bond == Bond.Married;
                v.MarriedDay = FamilyState.S.MarriedDay;
            }

            private static void People(List<TextPerson> into)
            {
                try
                {
                    into.Clear();
                    if (!SocialState.Loaded)
                    {
                        return;
                    }
                    int hour = SocialLife.Hour;
                    for (int i = 0; i < SocialState.People.Length; i++)
                    {
                        SocialPerson p = SocialState.People[i];
                        if (p == null || string.IsNullOrEmpty(p.Name))
                        {
                            continue;
                        }
                        // The day they last texted, whichever feature sent it (Mom's news from the family is not the social life's own
                        // text): so one who has texted today is not the one who texts again about the same news.
                        int lastText = p.Met ? Math.Max(p.LastTextDay, SocialPhone.LastTextDay(p)) : p.LastTextDay;
                        into.Add(new TextPerson
                        {
                            Index = p.Index, First = SocialLife.First(p), Seed = p.Seed, Stage = (int)p.Stage, Together = p.Together, Family = p.IsFamily, Met = p.Met,
                            Drifted = p.Drifted, Asleep = p.Asleep(hour), District = p.District, Origin = (int)p.Origin, LastTextDay = lastText, Closeness = p.Closeness
                        });
                    }
                }
                catch (Exception e)
                {
                    Fault("RLO Texts, the people", e);
                    into.Clear();
                }
            }

            // A text from a person of the social life, through its own: the person's values, then the event's.
            private static bool PersonSay(int index, string key, Dictionary<string, string> values)
            {
                try
                {
                    SocialPerson p = SocialState.Loaded ? SocialState.Get(index) : null;
                    if (p == null || !p.Met)
                    {
                        return false;
                    }
                    Dictionary<string, string> data = SocialLife.Data(p);
                    if (values != null)
                    {
                        foreach (KeyValuePair<string, string> v in values)
                        {
                            data[v.Key] = v.Value;
                        }
                    }
                    return SocialLife.SendText(p, key, data);
                }
                catch (Exception e)
                {
                    Fault("RLO Texts, a person's text", e);
                    return false;
                }
            }

            private static string First(string name)
            {
                if (string.IsNullOrEmpty(name))
                {
                    return name;
                }
                int space = name.IndexOf(' ');
                return space > 0 ? name.Substring(0, space) : name;
            }
        }
    }
}
