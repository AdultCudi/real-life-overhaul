using System;
using System.Globalization;
using Helpers;

namespace RealLifeOverhaul.Economy
{
    /// <summary>
    /// The words of the cleaning service and of selling a business, the same on the page and in a notification: money as
    /// the Finance app writes it, a time of day as the game does.
    /// </summary>
    public static class ServicesText
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        private static readonly string[] KindKeys = { "reallife_svc_buyer_rival", "reallife_svc_buyer_investor", "reallife_svc_buyer_staff", "reallife_svc_buyer_chain" };
        private static readonly string[] ModeKeys = { "reallife_svc_mode_needed", "reallife_svc_mode_1", "reallife_svc_mode_2", "reallife_svc_mode_3" };
        private static readonly string[] WhyKeys = { null, "reallife_svc_why_nobody", "reallife_svc_why_far", "reallife_svc_why_time" };

        public static string T(string key) => FinanceText.T(key);

        /// <summary>
        /// What happens to the staff in a sale: a buyer that keeps them holds them to their jobs for 30 days where the living
        /// population can; one that does not leaves them with the player.
        /// </summary>
        public static string StaffLine(bool keeps)
        {
            if (!keeps)
            {
                return FinanceText.T("reallife_svc_bid_staff_not");
            }
            return ServicesBridges.HoldsStaff != null && ServicesBridges.HoldsStaff()
                ? FinanceText.T("reallife_svc_bid_staff_hold", "days", RealLifeOverhaul.Population.HoldRules.Days.ToString())
                : FinanceText.T("reallife_svc_bid_staff_keep");
        }

        public static string T(string key, string name, string value) => FinanceText.T(key, name, value);

        public static string T(string key, string name, string value, string name2, string value2) => FinanceText.T(key, name, value, name2, value2);

        public static string T(string key, string name, string value, string name2, string value2, string name3, string value3)
        {
            return FinanceText.T(key, name, value, name2, value2, name3, value3);
        }

        /// <summary>A text with any number of its names filled in: name, value, name, value.</summary>
        public static string Fill(string key, params string[] pairs) => FinanceText.Fill(key, pairs);

        public static string Money(float amount) => FinanceText.Money(amount);

        /// <summary>A sum that may be below nothing, written so that it reads as one.</summary>
        public static string Signed(int amount) => amount < 0 ? "-" + FinanceText.Money(-amount) : FinanceText.Money(amount);

        /// <summary>A minute of the day as the game writes a time (its own 12 or 24 hour option).</summary>
        public static string Clock(int minute)
        {
            ServicesCatalogue.Clock(minute, out int hour, out int minutes);
            return hour.GetFormattedTime(minutes);
        }

        public static string Hour(int hour) => Clock(ServicesCatalogue.Clamp(hour, 0, 23) * 60);

        public static string Minutes(int minutes) => T("reallife_svc_minutes", "count", minutes.ToString(Inv));

        /// <summary>A way: "850 m", "1.3 km".</summary>
        public static string Distance(int metres) => metres < 1000 ? metres.ToString(Inv) + " m" : (metres / 1000f).ToString("0.#", Inv) + " km";

        public static string Percent(int percent) => percent.ToString(Inv) + "%";

        /// <summary>Cleanliness now against the morning's: "82 (+4 since the morning)".</summary>
        public static string Trend(int now, int before)
        {
            if (before < 0 || now == before)
            {
                return T("reallife_svc_trend_level", "now", now.ToString(Inv));
            }
            return T(now > before ? "reallife_svc_trend_up" : "reallife_svc_trend_down", "now", now.ToString(Inv), "by", Math.Abs(now - before).ToString(Inv));
        }

        public static string Mode(int mode) => T(ModeKeys[ServicesCatalogue.Clamp(mode, 0, ModeKeys.Length - 1)]);

        /// <summary>Why a visit that is due is not planned; null if nothing is left over.</summary>
        public static string Why(int why) => why > 0 && why < WhyKeys.Length ? T(WhyKeys[why]) : null;

        /// <summary>A kind of buyer: "a rival owner".</summary>
        public static string Kind(int buyer) => T(KindKeys[ServicesCatalogue.Clamp(buyer, 0, KindKeys.Length - 1)]);

        /// <summary>Who makes an offer: the buyer by name ("Anya Petrova and the staff" for a buy-out); an offer made before named buyers has one of three names of its kind.</summary>
        public static string Buyer(SaleOffer offer)
        {
            if (offer.Named)
            {
                return offer.Buyer == (int)BuyerKind.Staff ? T("reallife_svc_who_staff", "name", offer.WhoName) : offer.WhoName;
            }
            return T(KindKeys[ServicesCatalogue.Clamp(offer.Buyer, 0, KindKeys.Length - 1)] + "_" + ServicesCatalogue.Clamp(offer.Face, 0, 2).ToString(Inv));
        }

        /// <summary>What the buyer is: "Rival, 12 businesses", "Listed company, BLB", "Your staff, 9 of them", "Investor"; the kind alone before named buyers.</summary>
        public static string Who(SaleOffer offer)
        {
            if (!offer.Named)
            {
                return Kind(offer.Buyer);
            }
            switch ((BuyerKind)offer.Buyer)
            {
                case BuyerKind.Rival:
                    return T("reallife_svc_who_rival", "count", offer.WhoCount.ToString(Inv));
                case BuyerKind.Chain:
                    RealLifeOverhaul.Markets.CompanyDef d = RealLifeOverhaul.Markets.MarketCatalogue.Find(offer.WhoCount);
                    return T("reallife_svc_who_chain", "ticker", d != null ? d.Ticker : "?");
                case BuyerKind.Staff:
                    return T("reallife_svc_who_staff_note", "count", offer.WhoCount.ToString(Inv));
                default:
                    return T("reallife_svc_who_investor");
            }
        }

        /// <summary>A line of what would raise a business's price: what it is now, and what it takes.</summary>
        public static string RaiseLabel(SaleRaise r)
        {
            switch (r.Kind)
            {
                case RaiseKind.Interior:
                    return T("reallife_svc_raise_interior", "now", r.Now.ToString(Inv), "want", r.Want.ToString(Inv));
                case RaiseKind.Rating:
                    return T("reallife_svc_raise_rating", "now", r.Now.ToString(Inv));
                case RaiseKind.Skill:
                    return T("reallife_svc_raise_skill", "now", r.Now.ToString(Inv));
                default:
                    return T("reallife_svc_raise_books", "now", r.Now.ToString(Inv), "want", r.Want.ToString(Inv));
            }
        }

        /// <summary>About how much a line adds: "At 80% adds about $48,000", or "In 3 days adds about ..." for the books; met with nothing more to gain.</summary>
        public static string RaiseValue(SaleRaise r)
        {
            if (r.Gain <= 0)
            {
                return T("reallife_svc_raise_met");
            }
            return r.Kind == RaiseKind.Books
                ? T("reallife_svc_raise_days", "count", Math.Max(1, r.To - r.Now).ToString(Inv), "amount", Money(r.Gain))
                : T(r.Kind == RaiseKind.Skill ? "reallife_svc_raise_to_skill" : "reallife_svc_raise_to", "to", r.To.ToString(Inv), "amount", Money(r.Gain));
        }

        public static string District(string district) => CompanyText.District(district);
    }
}
