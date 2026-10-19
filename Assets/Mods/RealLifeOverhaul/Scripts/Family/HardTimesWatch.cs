using Helpers;
using UnityEngine;

namespace RealLifeOverhaul.Family
{
    /// <summary>
    /// When a difficult moment comes, and which, from what is really going on: once a game hour this reads the
    /// household (the evening at ten and the night: was the player home), family life's own records, the game's money
    /// and the player's places (HardTimesGame), and through HardTimesBridges what home life, careers, the social life
    /// and travel know; once a game day the economy's figures. HardTimesRules decides, and HardTimes asks the moment as
    /// it always has. Only while the two are together, hard times are on, the player is not locked up and it is not
    /// their wedding day. Only the local player's own family, save and buildings; nothing is sent anywhere. Cost:
    /// nothing per frame; once a game hour a few dozen field reads, one pass over the city's building registrations and
    /// a line of the save rebuilt (about 200 characters); once a game day the economy's walk over the game's last days
    /// of transactions (Wiring/Wiring.HardTimesLinks).
    /// </summary>
    internal static class HardTimesWatch
    {
        private const int MomDinnerFrom = 17;   // a holiday dinner at Mom's from this hour: later and they wait

        private static readonly int[] Raw = new int[HardTimesRules.Strains];
        private static readonly int[] Usable = new int[HardTimesRules.Strains];

        private static FamilySave _of;          // the save the memory below was read from
        private static HardMemory _memory;
        private static HardSigns _money;        // the economy's figures as last read
        private static int _moneyDay = -1;      // the day they were read on
        private static int _moneyStamp = -1;    // day * 24 + hour of the last reading before a moment of money
        private static int _eveningRead = -1;   // the evening read (not saved: a second reading adds nothing)
        private static int _nightRead = -1;     // the day whose small hours were read
        private static int _pending = -1;       // diagnostics: a moment that fitted earlier today, its hour to come
        private static Strain _pendingWhy;
        private static int _pendingDay = -1;

        // The memory of the save now loaded: read once from its field, again whenever the family's state is another
        // object.
        private static HardMemory Memory
        {
            get
            {
                FamilySave s = FamilyState.S;
                if (!ReferenceEquals(s, _of) || _memory == null)
                {
                    _of = s;
                    _memory = HardMemory.Read(s.Hard);
                    _moneyDay = -1;
                    _moneyStamp = -1;
                    _eveningRead = _nightRead = -1;
                    _pending = -1;
                }
                return _memory;
            }
        }

        private static void Keep(HardMemory m)
        {
            FamilyState.S.Hard = m.Write();
        }

        /// <summary>
        /// From HardTimes.Daily, once a game day, while the two are together, hard times are on and the player is free.
        /// </summary>
        public static void Daily(int day)
        {
            HardMemory m = Memory;
            HardTimesRules.Roll(m, day);
            Count(m, day);
            ReadMoney(day);
            Keep(m);
        }

        /// <summary>
        /// From FamilyCalendar.Hourly, once a game hour (the first after any skipped), while the player is not locked
        /// up.
        /// </summary>
        public static void Hourly(int day, int hour)
        {
            FamilyDirector director = FamilyDirector.Instance;
            if (!FamilyState.Together || !Settings.FamilyFriction || director == null)
            {
                return;
            }
            FamilySave s = FamilyState.S;
            HardMemory m = Memory;
            HardTimesRules.Roll(m, day);
            Count(m, day);
            if (_moneyDay != day)
            {
                ReadMoney(day);   // a save loaded during the day: the day's figures are read now
            }
            HardSigns g = Signs(day, hour, m);
            ReadEvening(g, m, day, hour);
            if (director.HasQuestions || FamilyApi.WeddingToday)
            {
                Keep(m);
                return;   // another question waits, or it is their wedding day: the next hour
            }
            int pick = HardTimesRules.Choose(g, m, s.PSeed, s.SeenArguments, s.LastArgument, s.LastArgueDay, Raw,
                                             Usable, out Strain why);
            if (pick >= 0 && OfMoney(why) && _moneyStamp != day * 24 + hour)
            {
                // A moment of money is weighed on the figures of this hour: money that came back since the morning ends
                // it.
                _moneyStamp = day * 24 + hour;
                ReadMoney(day);
                g = Signs(day, hour, m);
                pick = HardTimesRules.Choose(g, m, s.PSeed, s.SeenArguments, s.LastArgument, s.LastArgueDay, Raw,
                                             Usable, out why);
            }
            if (pick < 0)
            {
                Watch(g, m, s.PSeed, day);
                Keep(m);
                return;
            }
            Come(pick, why, g, m, day, "its hour");
        }

        /// <summary>
        /// From HardTimes.Answered: whether the answer was a quick "sorry", for the run of them that one moment is
        /// about.
        /// </summary>
        public static void Answered(bool sorry)
        {
            if (!FamilyState.Loaded)
            {
                return;
            }
            HardMemory m = Memory;
            HardTimesRules.Said(m, sorry, TimeHelper.CurrentDay);
            Keep(m);
        }

        /// <summary>
        /// The options page's test: the moment that fits what is going on now best, whatever its hour, the days since
        /// the last, and how long its cause has lasted. -1 when nothing at all is wrong, and nothing is asked.
        /// </summary>
        public static int Test(out Strain why)
        {
            why = Strain.None;
            FamilySave s = FamilyState.S;
            int day = TimeHelper.CurrentDay, hour = TimeHelper.CurrentHour;
            HardMemory m = Memory;
            HardTimesRules.Roll(m, day);
            Count(m, day);
            ReadMoney(day);
            HardSigns g = Signs(day, hour, m);
            HardTimesRules.Weigh(g, m, s.PSeed, Raw, Usable);
            int best = -1, bestFit = 0, again = -1;
            Strain againWhy = Strain.None;
            for (int k = 0; k < HardTimesRules.Moments; k++)
            {
                // As things are now: ripe or not, resting or not.
                int fit = HardTimesRules.Fit(k, g, m, Raw, Raw, out Strain cause);
                if (fit < HardTimesRules.MinFit)
                {
                    continue;
                }
                if (k == s.LastArgument)
                {
                    again = k;   // the last one again only if nothing else fits
                    againWhy = cause;
                }
                else if (fit > bestFit)
                {
                    best = k;
                    bestFit = fit;
                    why = cause;
                }
            }
            if (best < 0 && again >= 0)
            {
                best = again;
                why = againWhy;
            }
            if (best < 0)
            {
                Keep(m);
                FamilyDirector.Logger?.Info("Family difficult moment test: nothing is wrong between them now ("
                                            + Levels() + ").");
                return -1;
            }
            Come(best, why, g, m, day, "the test");
            return best;
        }

        // The moment comes: its cause rests, the save's line is kept, and HardTimes asks it as it always has.
        private static void Come(int pick, Strain why, in HardSigns g, HardMemory m, int day, string when)
        {
            HardTimesRules.Spoke(m, why, Raw, day);
            Keep(m);
            _pending = -1;
            FamilyDirector.Logger?.Info("Family: a difficult moment (" + (pick + 1) + ") from " + Words(why) + ", at "
                                        + when + ".");
            if (Meta.Dev.Verbose)
            {
                string level = why != Strain.None ? " at " + Raw[(int)why] + " of 100" : string.Empty;
                string home = g.LivesIn ? "living together" : "living apart";
                FamilyDirector.Logger?.Info("Family check (difficult moment): moment " + (pick + 1) + " came of "
                                            + Words(why) + level + ", harmony " + Mathf.RoundToInt(g.Harmony) + ", "
                                            + home + "; causes now " + Levels() + ": PASS.");
            }
            HardTimes.Argue(day, pick);
        }

        // Diagnostics only: a moment that fitted earlier today and whose cause has gone by its hour is said once, as it
        // is let go.
        private static void Watch(in HardSigns g, HardMemory m, int seed, int day)
        {
            if (!Meta.Dev.Verbose)
            {
                return;
            }
            if (_pendingDay != day)
            {
                _pending = -1;
                _pendingDay = day;
            }
            int best = -1, bestFit = 0;
            Strain bestWhy = Strain.None;
            for (int k = 0; k < HardTimesRules.Moments; k++)
            {
                int fit = HardTimesRules.Fit(k, g, m, Raw, Usable, out Strain cause);
                if (fit >= HardTimesRules.MinFit && fit > bestFit)
                {
                    best = k;
                    bestFit = fit;
                    bestWhy = cause;
                }
            }
            if (_pending >= 0 && HardTimesRules.Fit(_pending, g, m, Raw, Usable, out _) < HardTimesRules.MinFit)
            {
                FamilyDirector.Logger?.Info("Family check (difficult moment): moment " + (_pending + 1)
                                            + " fitted earlier today, of " + Words(_pendingWhy)
                                            + ", and its cause has gone by " + g.Hour + ":00; no moment: PASS.");
                _pending = -1;
            }
            if (best >= 0 && best != _pending)
            {
                _pending = best;
                _pendingWhy = bestWhy;
                FamilyDirector.Logger?.Info("Family check (difficult moment): moment " + (best + 1) + " fits, of "
                                            + Words(bestWhy) + " at " + bestFit + " of 100; it can come from "
                                            + HardTimesRules.HourOf(best, day, seed) + ":00 and "
                                            + HardTimesRules.Gap(g.Harmony) + " days after the last: waiting.");
            }
        }

        // What is going on now, as the rules read it.
        private static HardSigns Signs(int day, int hour, HardMemory m)
        {
            FamilySave s = FamilyState.S;
            HardSigns g = HardSigns.Unknown(day, hour);
            g.LivesIn = s.LivesIn;
            g.Committed = FamilyState.Bond == Bond.Engaged || FamilyState.Bond == Bond.Married;
            g.Harmony = s.Harmony;
            g.TalkDay = Mathf.Max(s.LastTalkDay, s.LastSharedDay);
            g.SharedDay = s.LastSharedDay;
            g.ChildDay = Settings.FamilyChildren ? NewestChild(s) : 0;
            g.SinceDay = FamilyState.Bond == Bond.Married && s.MarriedDay > 0 ? s.MarriedDay : s.SinceDay;
            g.YearDays = GameCalendar.DaysPerYear;
            g.MomThere = Settings.FamilyRelatives && PhoneContacts.Mom != null;
            g.OutingLate = OutingLate(s, day, hour);
            g.LetDownDay = m.PromiseDay;
            g.LetDowns = m.PromiseRun;
            g.PlaceDay = m.PlaceDay;
            g.Places = m.Places;
            g.MovedDay = m.MovedDay;
            g.PriceLevel = Prices.Level;
            g.NetKnown = _money.NetKnown;
            g.NetWorth = _money.NetWorth;
            g.OwnsBusiness = _money.OwnsBusiness;
            g.BigBuy = _money.BigBuy;
            g.BigBuyBefore = _money.BigBuyBefore;
            g.Borrowed = _money.Borrowed;
            HardTimesGame.Cash(ref g);
            IHardTimesBridge life = HardTimesBridges.Life;
            if (life != null)
            {
                life.Days(ref g, Partner.Name);
            }
            if (g.AwayTogether)
            {
                g.TalkDay = g.SharedDay = day;   // away on a trip together: every day of it is time together
            }
            return g;
        }

        private static void ReadMoney(int day)
        {
            _moneyDay = day;
            HardSigns money = HardSigns.Unknown(day, 0);
            money.PriceLevel = Prices.Level;
            IHardTimesBridge life = HardTimesBridges.Life;
            if (life != null)
            {
                life.Money(ref money);
            }
            _money = money;
        }

        private static bool OfMoney(Strain why)
        {
            return why == Strain.Money || why == Strain.Loss || why == Strain.Spend;
        }

        // At ten, or the first hour after: was the player home while the partner was? And in the small hours: did the
        // player sleep at home?
        private static void ReadEvening(in HardSigns g, HardMemory m, int day, int hour)
        {
            if (!g.LivesIn)
            {
                return;
            }
            if (hour >= HardTimesRules.LateHour && _eveningRead != day)
            {
                _eveningRead = day;
                if (!g.AwayTogether && HomeLife.Where(day, HardTimesRules.LateHour) == Whereabouts.Home
                    && !FamilyPlaces.AtHome())
                {
                    HardTimesRules.Mark(ref m.Late, m.Base, day);
                    if (HardTimesGame.AtOwnBusiness())
                    {
                        HardTimesRules.Mark(ref m.LateWork, m.Base, day);
                    }
                }
            }
            if (hour >= HardTimesRules.NightFrom && hour < HardTimesRules.NightTo && _nightRead != day)
            {
                _nightRead = day;
                if (!g.AwayTogether && !FamilyPlaces.AtHome())
                {
                    HardTimesRules.Mark(ref m.Away, m.Base, day - 1);   // the night that began yesterday
                    if (HardTimesGame.AtOwnBusiness())
                    {
                        HardTimesRules.Mark(ref m.AwayWork, m.Base, day - 1);
                    }
                }
            }
        }

        // Family life's broken promises, and the player's own places and home, counted again.
        private static void Count(HardMemory m, int day)
        {
            HardTimesRules.NotePromises(m, FamilyState.S.BrokenPromises, day);
            HardTimesRules.NotePlaces(m, HardTimesGame.OwnPlaces(), HardTimesGame.HomeKey(), day);
        }

        private static int NewestChild(FamilySave s)
        {
            int newest = 0;
            for (int i = 0; i < s.Children.Count; i++)
            {
                ChildRecord c = s.Children[i];
                if (c != null && !c.Grown && c.ArrivedDay > newest)
                {
                    newest = c.ArrivedDay;
                }
            }
            return newest;
        }

        // Minutes late for an outing with others the two go to together: a child's school play, parents' evening or
        // party, a holiday dinner at Mom's.
        private static int OutingLate(FamilySave s, int day, int hour)
        {
            int late = 0;
            if (s.LivesIn)
            {
                for (int i = 0; i < s.Errands.Count; i++)
                {
                    Errand e = s.Errands[i];
                    if (e == null || e.Day != day || hour <= e.FromHour || hour >= e.ToHour || !WithOthers(e.Kind))
                    {
                        continue;
                    }
                    string[] places = Errands.PlacesOf(e.Kind);
                    if (places == null || !FamilyPlaces.Inside(places))
                    {
                        late = Mathf.Max(late, (hour - e.FromHour) * 60);   // still to go to, and its start long past
                    }
                }
            }
            if (Settings.FamilyRelatives && s.MomDinnerDay == day && hour > MomDinnerFrom)
            {
                late = Mathf.Max(late, (hour - MomDinnerFrom) * 60);
            }
            return late;
        }

        // An outing the two go to with others there: a child's school play, parents' evening or party.
        private static bool WithOthers(string kind)
        {
            return kind == Errands.SchoolPlay || kind == Errands.ParentsEvening || kind == Errands.Party;
        }

        // The causes' levels now, for the log: "LongHours 72, Worn 40" or "none".
        private static string Levels()
        {
            var b = new System.Text.StringBuilder(96);
            for (int i = 1; i < HardTimesRules.Strains; i++)
            {
                if (Raw[i] > 0)
                {
                    b.Append(b.Length > 0 ? ", " : string.Empty);
                    b.Append(((Strain)i).ToString()).Append(' ').Append(Raw[i]);
                }
            }
            return b.Length > 0 ? b.ToString() : "none";
        }

        private static string Words(Strain why)
        {
            switch (why)
            {
                case Strain.LateHome: return "evenings home after ten";
                case Strain.NightsAway: return "nights away from home";
                case Strain.LongHours: return "long working hours";
                case Strain.Unseen: return "days without a call or a visit";
                case Strain.Worn: return "stress and too little sleep";
                case Strain.Money: return "money trouble or debt";
                case Strain.Loss: return "a big loss of money";
                case Strain.Spend: return "a big purchase";
                case Strain.NewChild: return "a new child";
                case Strain.NewPlace: return "a new place";
                case Strain.LetDown: return "an outing or a promise missed";
                case Strain.Forgot: return "a birthday or an anniversary forgotten";
                case Strain.Skipping: return "meals skipped";
                case Strain.Mess: return "a messy home";
                case Strain.Sorry: return "quick sorries";
                case Strain.Business: return "evenings and nights at the business";
                case Strain.RunningLate: return "running late for an outing";
                default: return "an ordinary day";
            }
        }
    }
}
