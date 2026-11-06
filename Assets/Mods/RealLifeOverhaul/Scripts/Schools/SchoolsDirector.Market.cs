using System;
using System.Collections.Generic;
using System.Globalization;
using UI.Notification;

namespace RealLifeOverhaul.Schools
{
    /// <summary>
    /// The director's part for the market: a day's pass of the rules and what the player hears of it, the player's school's
    /// week in the game's books, and every lever the player has (each answers a locale key of why not, for the Campus app's
    /// notice, or null): buying, selling, an offer taken, the fee, a class, the
    /// teachers' pay, scholarships, a headteacher, their own children enrolled. Each lever is the one the trusts pull by the
    /// rules (SchoolRules.Policy): the same steps and bounds. On a day's pass or a press, never a frame.
    /// </summary>
    public sealed partial class SchoolsDirector
    {
        private static readonly CultureInfo Plain = CultureInfo.InvariantCulture;
        private static readonly List<SchoolChild> Kids = new List<SchoolChild>(4);

        // One day: the rules' pass, the week booked, the notices that touch the player, the district's traffic when the name moves it.
        private void DayWork(int day)
        {
            int council = SchoolsBridges.Council != null ? SchoolsBridges.Council() : 0;
            SchoolDayFlags flags = SchoolRules.Day(_state, day, SchoolsGame.Year, SchoolsGame.InSession, council);
            SchoolFile campus = _state.Campus;
            bool mine = campus.Owner == SchoolOwner.Player;
            bool family = _state.Children.Count > 0;
            if ((flags & SchoolDayFlags.WeekBooked) != 0 && mine)
            {
                long week = campus.Week;
                campus.Week = 0;
                SchoolsGame.Book(week, SchoolsText.Transaction);
                SchoolsGame.Tell(week >= 0 ? NotificationType.Info : NotificationType.Warning, SchoolsText.NoticeWeek, "money", SchoolsText.Money(Signed(week)));
            }
            if ((flags & SchoolDayFlags.Offer) != 0 && mine)
            {
                SchoolsGame.Tell(NotificationType.Info, SchoolsText.NoticeOffer, "trust", SchoolsText.Trust(_state.OfferBy), "price", SchoolsText.Money(SchoolsGame.Today(_state.OfferPrice * 1000L)),
                    "day", _state.OfferEnd.ToString(Plain));
            }
            if ((flags & SchoolDayFlags.OfferLapsed) != 0 && mine)
            {
                SchoolsGame.Tell(NotificationType.Info, SchoolsText.NoticeOfferGone);
            }
            if ((flags & SchoolDayFlags.Results) != 0 && (mine || family))
            {
                int[] order = SchoolRules.League(_state);
                SchoolsGame.Tell(NotificationType.Info, SchoolsText.NoticeResults, "rank", (Array.IndexOf(order, SchoolRules.CampusId) + 1).ToString(Plain),
                    "count", order.Length.ToString(Plain), "score", campus.Results.ToString(Plain));
            }
            if ((flags & SchoolDayFlags.Inspected) != 0 && (mine || family))
            {
                SchoolsGame.Tell(NotificationType.Info, SchoolsText.NoticeInspected, "grade", SchoolsText.Grade(campus.Grade));
            }
            if ((flags & SchoolDayFlags.Poached) != 0 && mine)
            {
                TellPoaching(day);
            }
            Traffic(false);
            SchoolRules.TrimNews(_state, day);
            if (Verbose)
            {
                Logger?.Info("Schools check (day " + day + "): " + Describe() + (flags != SchoolDayFlags.None ? "; " + flags : string.Empty) + ": PASS.");
            }
        }

        private static long Signed(long startPrices) => startPrices >= 0 ? SchoolsGame.Today(startPrices) : -SchoolsGame.Today(-startPrices);

        // The pay war's moves that touched the player's school this term, one notice each way.
        private static void TellPoaching(int day)
        {
            for (int i = 0; i < _state.News.Count; i++)
            {
                SchoolNews n = _state.News[i];
                if (n.Day != day || n.Kind != SchoolRules.NewsPoached)
                {
                    continue;
                }
                bool took = n.School == SchoolRules.CampusId;
                SchoolsGame.Tell(took ? NotificationType.Info : NotificationType.Warning, took ? SchoolsText.NoticePoachedBy : SchoolsText.NoticePoachedFrom,
                    "other", SchoolsGame.Name(took ? n.Other : n.School), "count", n.Value.ToString(Plain));
            }
        }

        // ---------------------------------------------------------------- the player's levers (each returns why not, or null)

        // Why the market cannot be touched now; null when it can.
        private static string NotNow(bool owned)
        {
            if (!Running)
            {
                return SchoolsText.BoxOff;
            }
            if (owned && _state.Campus.Owner != SchoolOwner.Player)
            {
                return SchoolsText.LineNotYours;
            }
            return null;
        }

        /// <summary>Birchwood School bought from its owner at their asking price (the same a trust pays another).</summary>
        public static string Buy()
        {
            string why = NotNow(false);
            if (why != null)
            {
                return why;
            }
            if (SchoolsGame.InSession)
            {
                return SchoolsText.NoticeSession;
            }
            SchoolFile campus = _state.Campus;
            if (campus.Owner == SchoolOwner.Player)
            {
                return null;
            }
            long price = SchoolRules.Asking(campus);
            if (!SchoolsGame.Pay(price, SchoolsText.TransactionBuy))
            {
                return null;   // the game's own notice said the player cannot pay
            }
            SchoolRules.Sell(_state, SchoolRules.CampusId, SchoolOwner.Player, price, SchoolsGame.Day);
            Written();
            SchoolsGame.Tell(NotificationType.Success, SchoolsText.NoticeBought, "price", SchoolsText.Money(SchoolsGame.Today(price)));
            Logger?.Info("Schools: the player bought Birchwood School for " + price + " at the start's prices.");
            Instance?.Traffic(true);
            return null;
        }

        /// <summary>Birchwood School sold to the trusts at what they pay (the richest buys).</summary>
        public static string Sell()
        {
            string why = NotNow(true);
            if (why != null)
            {
                return why;
            }
            if (SchoolsGame.InSession)
            {
                return SchoolsText.NoticeSession;
            }
            int buyer = SchoolRules.RichestTrust(_state, -1);
            long price = SchoolRules.SalePrice(_state.Campus);
            return buyer < 0 ? SchoolsText.LineNotYours : SellTo(buyer, price);
        }

        /// <summary>A trust's offer for Birchwood School taken.</summary>
        public static string Accept()
        {
            string why = NotNow(true);
            if (why != null)
            {
                return why;
            }
            if (_state.OfferPrice <= 0 || !SchoolOwner.IsTrust(_state.OfferBy))
            {
                return SchoolsText.NoticeOfferGone;
            }
            return SellTo(_state.OfferBy, _state.OfferPrice * 1000L);
        }

        private static string SellTo(int trust, long price)
        {
            long campusWeek = _state.Campus.Week;
            _state.Campus.Week = 0;
            SchoolsGame.Book(campusWeek + price, SchoolsText.TransactionSell);
            SchoolRules.Sell(_state, SchoolRules.CampusId, trust, price, SchoolsGame.Day);
            Written();
            SchoolsGame.Tell(NotificationType.Success, SchoolsText.NoticeSold, "trust", SchoolsText.Trust(trust), "price", SchoolsText.Money(SchoolsGame.Today(price)));
            Logger?.Info("Schools: the player sold Birchwood School to trust " + trust + " for " + price + " at the start's prices.");
            Instance?.Traffic(true);
            return null;
        }

        /// <summary>The fee a step up (+1) or down (-1).</summary>
        public static string Fee(int way) => Lever(f => SchoolRules.StepFee(f, way), "fee " + way);

        /// <summary>A class opened (+1) or closed (-1).</summary>
        public static string Class(int way) => Lever(f => SchoolRules.StepClass(f, way), "class " + way);

        /// <summary>The teachers' pay a step up (+1) or down (-1) against the going rate.</summary>
        public static string Pay(int way) => Lever(f => SchoolRules.StepPay(f, way), "pay " + way);

        /// <summary>Scholarships a step up (+1) or down (-1).</summary>
        public static string Scholar(int way) => Lever(f => SchoolRules.StepScholar(f, way), "scholarships " + way);

        private static string Lever(Func<SchoolFile, bool> step, string what)
        {
            string why = NotNow(true);
            if (why != null)
            {
                return why;
            }
            if (step(_state.Campus))
            {
                Written();
                Logger?.Info("Schools: the player set Birchwood School's " + what + ": " + Describe() + ".");
            }
            return null;
        }

        /// <summary>One of the week's three candidates (0 to 2) hired as Birchwood School's headteacher, paying for the search.</summary>
        public static string Head(int candidate)
        {
            string why = NotNow(true);
            if (why != null)
            {
                return why;
            }
            SchoolRules.Candidate(SchoolRules.CampusId, SchoolsGame.Day / 7, candidate, out int seed, out int skill);
            if (seed == _state.Campus.Head)
            {
                return null;
            }
            if (!SchoolsGame.Pay(SchoolRules.HireCost(skill), SchoolsText.TransactionHead))
            {
                return null;   // the game's own notice said the player cannot pay
            }
            _state.Campus.Head = seed;
            _state.Campus.HeadSkill = skill;
            Written();
            SchoolsGame.Tell(NotificationType.Success, SchoolsText.NoticeHead, "name", SchoolsGame.Person(seed));
            return null;
        }

        /// <summary>The game's way to the main school set: its guider on the map and in the street.</summary>
        public static string Go() => SchoolsGame.Guide() ? null : SchoolsText.NoticeNoCampus;

        /// <summary>The player's children as family life has them (into the scratch list), or none.</summary>
        public static List<SchoolChild> Children()
        {
            Kids.Clear();
            SchoolsBridges.Children?.Invoke(Kids);
            return Kids;
        }

        /// <summary>A child of school age moved to Birchwood School, a place taken; family life charges its fee from then.</summary>
        public static string Enrol(string name)
        {
            string why = NotNow(false);
            if (why != null)
            {
                return why;
            }
            SchoolChild child = Find(name, out bool found);
            if (!found)
            {
                return SchoolsText.LineNoChildren;
            }
            if (child.Grown || child.Age < SchoolRules.SchoolFrom || child.Age >= SchoolRules.SchoolTo)
            {
                SchoolsGame.Tell(NotificationType.Warning, SchoolsText.NoticeAge, "child", child.Name);
                return null;
            }
            if (_state.Children.Contains(SchoolStore.Clean(name)))
            {
                return null;
            }
            if (!SchoolRules.HasPlace(_state.Campus))
            {
                return SchoolsText.NoticeFull;
            }
            if (SchoolsBridges.SetSchool == null || !SchoolsBridges.SetSchool(name, true))
            {
                return SchoolsText.LineNoChildren;
            }
            _state.Children.Add(SchoolStore.Clean(name));
            Written();
            SchoolsGame.Tell(NotificationType.Success, SchoolsText.NoticeEnrolled, "child", child.Name, "fee", SchoolsText.Money(FeeToday()));
            return null;
        }

        /// <summary>A child taken out of Birchwood School, back to the public school.</summary>
        public static string Leave(string name)
        {
            string clean = SchoolStore.Clean(name);
            if (!_state.Children.Remove(clean))
            {
                return null;
            }
            SchoolsBridges.SetSchool?.Invoke(name, false);
            Written();
            SchoolsGame.Tell(NotificationType.Info, SchoolsText.NoticeLeft, "child", name);
            return null;
        }

        private static SchoolChild Find(string name, out bool found)
        {
            List<SchoolChild> kids = Children();
            for (int i = 0; i < kids.Count; i++)
            {
                if (kids[i].Name == name)
                {
                    found = true;
                    return kids[i];
                }
            }
            found = false;
            return default(SchoolChild);
        }

        // ---------------------------------------------------------------- what family life asks (FamilyBridges.School)

        /// <summary>A pupil's day at Birchwood School in today's money: none at the player's own school.</summary>
        public static int FeeToday() => _state.Campus.Owner == SchoolOwner.Player ? 0 : SchoolsGame.Today(_state.Campus.Fee);

        /// <summary>A child's day of fees at Birchwood School, today's money; -1 when the child is not there (or the schools rest).</summary>
        public static int FeeFor(string child) => At(child) ? FeeToday() : -1;

        /// <summary>A child's reports at Birchwood School, points over a public school's; NaN when not there.</summary>
        public static float StandingOf(string child) => At(child) ? SchoolRules.Standing(_state.Campus) : float.NaN;

        /// <summary>Birchwood School's address for a child there: their school run and parents' evenings; false when not there.</summary>
        public static bool AddressFor(string child, out string street, out int number)
        {
            bool at = At(child) && CampusStands;
            street = at ? SchoolGrounds.Street : null;
            number = at ? SchoolGrounds.MainNumber : 0;
            return at;
        }

        private static bool At(string child) => Running && !string.IsNullOrEmpty(child) && _state.Children.Contains(SchoolStore.Clean(child));
    }
}
