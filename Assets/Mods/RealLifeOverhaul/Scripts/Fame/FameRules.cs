using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Fame
{
    /// <summary>
    /// Standing by rule: wealth, recognition and four reputations (customers, workers, neighbourhoods, integrity), how
    /// the street, the shops and the staff take them, and the daily round. Game-free; EconomyCheck runs it.
    /// </summary>
    public static class FameRules
    {
        public const int Start = 50;               // every reputation and district opinion, 0 to 100
        public const int NotesMost = 16;           // changes kept for the Standing page
        public const int DistrictsMost = 24;

        // ---- wealth: logarithmic, so each tenfold adds the same few points
        public const double WorthWeight = 14.0, WorthUnit = 1000.0;
        public const double IncomeWeight = 8.0, IncomeUnit = 250.0;
        public const int BusinessPoints = 2, BusinessPointsMost = 10;

        // ---- recognition: a rich owner who never posts nor talks to the press stays little known
        public const int WealthShare = 35;         // per cent of the wealth
        public const double FollowerWeight = 6.0, FollowerUnit = 10.0;
        public const int FollowersMost = 30;       // points from Flare at most
        public const int PressMost = 300;          // tenths of a point: what the papers can make of the player
        public const int PressForget = 25;         // thousandths of it forgotten each day
        public const int BrandShare = 10;          // per cent of the best brand's recognition
        public static readonly int[] TierFrom = { 0, 15, 35, 55, 75 };   // Unknown, Local, Known, Prominent, Famous

        public const int DistrictShare = 80;       // per cent of the city's recognition
        public const int ShopKnown = 6, ShopKnownMost = 24;
        public const int DistrictBrandShare = 15;
        public const int JobPoints = 2, JobPointsMost = 30;
        public const int RaisedPoints = 10, RaisedPointsMost = 40;
        public const int AskMarket = 1000;         // thousandths: a rent asked at the market's

        public const int DriftShare = 8;           // an eighth of the way a day...
        public const int DriftMost = 2;            // ...and never more than this
        public const int SatisfactionShare = 70;   // the customers: satisfaction against the prices
        public const int InteriorMost = 2;         // points a shop's interior moves its customers' figure, at most
        public const int LowPenalty = 20;          // the workers: points at most for the share who stand low...
        public const int StrikePenalty = 15;       // ...and for a strike
        public const int ForgetDays = 7;           // integrity forgets a point towards 50 this often
        public const int DayMost = 8;              // what the player does moves a part this much a day at most
        public static readonly int[] Weights = { 30, 25, 20, 25 };   // the parts in the overall reputation, per cent

        public const int StreetFrom = 15;          // recognition in the district from which people in the street know the player
        public const int StreetChanceStep = 3;     // ...per mille for each point over 10, halved: 90 per mille at 70
        public const int StreetMost = 8;           // reactions a day at most
        public const int StreetGapMinutes = 20;    // game minutes between two
        public const int ShopFrom = 20;            // recognition in the district from which customers know the player
        public const int ShopMost = 3;             // a day
        public const int VisitsMost = 3;           // businesses a day whose staff take a visit to heart
        public const int ProudFrom = 60, NervousUnder = 40;   // the workers' reputation: proud or nervous on a visit
        public const int VisitStanding = 1;        // points of each one's standing, up or down

        public static int Clamp(int value, int least, int most) => value < least ? least : value > most ? most : value;

        private static int Round(double value) => (int)Math.Floor(value + 0.5);

        /// <summary>Removes entries <paramref name="old"/> picks (else the oldest) until one more fits under <paramref name="most"/>.</summary>
        internal static void MakeRoom<T>(List<T> list, int most, Predicate<T> old)
        {
            while (list.Count >= most)
            {
                int at = list.FindIndex(old);
                list.RemoveAt(at >= 0 ? at : 0);
            }
        }

        public static int Wealth(long netWorth, long weekly, int businesses)
        {
            double w = WorthWeight * Math.Log10(1.0 + Math.Max(0L, netWorth) / WorthUnit)
                       + IncomeWeight * Math.Log10(1.0 + Math.Max(0L, weekly) / IncomeUnit)
                       + Math.Min(BusinessPointsMost, BusinessPoints * Math.Max(0, businesses));
            return Clamp(Round(w), 0, 100);
        }

        public static int FromFollowers(int followers)
        {
            return Math.Min(FollowersMost, Round(FollowerWeight * Math.Log10(1.0 + Math.Max(0, followers) / FollowerUnit)));
        }

        public static int Recognition(int wealth, int followers, int press, int brandBest)
        {
            double r = wealth * WealthShare / 100.0 + FromFollowers(followers) + Clamp(press, 0, PressMost) / 10.0
                       + Clamp(brandBest, 0, 100) * BrandShare / 100.0;
            return Clamp(Round(r), 0, 100);
        }

        public static int DistrictKnown(int recognition, int shops, int brand)
        {
            double r = recognition * DistrictShare / 100.0 + Math.Min(ShopKnownMost, ShopKnown * Math.Max(0, shops))
                       + Clamp(brand, 0, 100) * DistrictBrandShare / 100.0;
            return Clamp(Round(r), 0, 100);
        }

        public static FameTier TierOf(int recognition)
        {
            for (int i = TierFrom.Length - 1; i > 0; i--)
            {
                if (recognition >= TierFrom[i])
                {
                    return (FameTier)i;
                }
            }
            return FameTier.Unknown;
        }

        public static int Overall(int[] rep)
        {
            int sum = 0;
            for (int i = 0; i < Weights.Length; i++)
            {
                sum += rep[i] * Weights[i];
            }
            return Round(sum / 100.0);
        }

        public static FameProfile Profile(int wealth, int recognition, int overall)
        {
            if (recognition >= TierFrom[(int)FameTier.Prominent])
            {
                return overall >= 60 ? FameProfile.Admired : overall < 40 ? FameProfile.Notorious : FameProfile.WellKnown;
            }
            if (wealth >= 45 && recognition < 30)
            {
                return FameProfile.QuietMoney;
            }
            if (recognition >= 25)
            {
                return overall >= 55 ? FameProfile.LocalFavourite : FameProfile.WellKnown;
            }
            return FameProfile.Newcomer;
        }

        public static int CustomersTarget(List<FameShop> shops)
        {
            int sum = 0, n = 0;
            for (int i = 0; i < shops.Count; i++)
            {
                FameShop s = shops[i];
                if (!s.Figures)
                {
                    continue;
                }
                int figure = (Clamp(s.Satisfaction, 0, 100) * SatisfactionShare + Clamp(s.Pricing, 0, 100) * (100 - SatisfactionShare)) / 100;
                sum += Clamp(figure + Clamp(s.Interior, -InteriorMost, InteriorMost), 0, 100);
                n++;
            }
            return n > 0 ? Round((double)sum / n) : Start;
        }

        public static int WorkersTarget(List<FameShop> shops)
        {
            long standing = 0;
            int staff = 0, low = 0, striking = 0;
            for (int i = 0; i < shops.Count; i++)
            {
                FameShop s = shops[i];
                if (s.Staff <= 0 || s.Standing < 0)
                {
                    continue;
                }
                standing += (long)Clamp(s.Standing, 0, 100) * s.Staff;
                staff += s.Staff;
                low += Clamp(s.Low, 0, s.Staff);
                striking += s.Strike ? s.Staff : 0;
            }
            if (staff == 0)
            {
                return Start;
            }
            double t = (double)standing / staff - LowPenalty * (double)low / staff - StrikePenalty * (double)striking / staff;
            return Clamp(Round(t), 0, 100);
        }

        public static int DistrictTarget(int jobs, int raised)
        {
            int jobPoints = Math.Min(JobPointsMost, JobPoints * Math.Max(0, jobs));
            int raisedPoints = Math.Min(RaisedPointsMost, RaisedPoints * Math.Max(0, raised));
            return Clamp(Start + jobPoints - raisedPoints, 5, 95);
        }

        public static int Drift(int now, int target)
        {
            int gap = target - now;
            if (gap == 0)
            {
                return 0;
            }
            int step = gap / DriftShare;
            if (step == 0)
            {
                step = Math.Sign(gap);
            }
            return Clamp(step, -DriftMost, DriftMost);
        }

        private static bool Present(FameDistrict d) => d.Shops > 0 || d.Jobs > 0 || d.Raised > 0;

        /// <summary>The neighbourhoods' reputation: the average opinion of the districts the player has a part in. Unchanged with none.</summary>
        public static void Neighbourhoods(FameFile f)
        {
            int sum = 0, n = 0;
            for (int i = 0; i < f.Districts.Count; i++)
            {
                if (Present(f.Districts[i]))
                {
                    sum += f.Districts[i].Opinion;
                    n++;
                }
            }
            if (n > 0)
            {
                f.Rep[(int)FamePart.Neighbourhood] = Round((double)sum / n);
            }
        }

        /// <summary>Something the player did moves a part, up to <see cref="DayMost"/> a day either way. Returns what was moved.</summary>
        public static int Move(FameFile f, FamePart part, FameCause cause, int delta, int day, string district = null)
        {
            if (delta == 0)
            {
                return 0;
            }
            if (f.MovedDay != day)
            {
                f.MovedDay = day;
                Array.Clear(f.Moved, 0, f.Moved.Length);
            }
            int p = (int)part;
            int room = delta > 0 ? DayMost - Math.Max(0, f.Moved[p]) : DayMost + Math.Min(0, f.Moved[p]);
            int applied = delta > 0 ? Math.Min(delta, room) : -Math.Min(-delta, room);
            if (applied == 0 || Math.Sign(applied) != Math.Sign(delta))
            {
                return 0;
            }
            FameDistrict d = part == FamePart.Neighbourhood ? f.DistrictOrNew(district) : null;
            if (d != null)
            {
                int before = d.Opinion;
                d.Opinion = Clamp(d.Opinion + applied, 0, 100);
                applied = d.Opinion - before;
                Neighbourhoods(f);
                if (!Present(d))
                {
                    f.Rep[p] = Clamp(f.Rep[p] + applied, 0, 100);   // the average leaves out a district the player has no part in
                }
            }
            else
            {
                int before = f.Rep[p];
                f.Rep[p] = Clamp(f.Rep[p] + applied, 0, 100);
                applied = f.Rep[p] - before;
            }
            if (applied != 0)
            {
                f.Moved[p] += applied;
                Note(f, p, cause, applied, day, d != null ? d.Name : null);
            }
            return applied;
        }

        /// <summary>What the papers and the television made of the player, in tenths of a point of recognition. Returns what was applied.</summary>
        public static int Publicity(FameFile f, int tenths, int day, FameCause cause)
        {
            int before = f.Press;
            f.Press = Clamp(f.Press + tenths, 0, PressMost);
            int applied = f.Press - before;
            if (applied != 0)
            {
                Note(f, FameNote.Recognition, cause, applied, day, null);
            }
            return applied;
        }

        // One note per part, cause and district a day.
        private static void Note(FameFile f, int part, FameCause cause, int delta, int day, string district)
        {
            for (int i = f.Notes.Count - 1; i >= 0; i--)
            {
                FameNote n = f.Notes[i];
                if (n.Day != day)
                {
                    break;
                }
                if (n.Part == part && n.Cause == cause && string.Equals(n.District, district, StringComparison.Ordinal))
                {
                    n.Delta += delta;
                    return;
                }
            }
            if (f.Notes.Count >= NotesMost)
            {
                f.Notes.RemoveAt(0);
            }
            f.Notes.Add(new FameNote { Day = day, Part = part, Cause = cause, Delta = delta, District = district });
        }

        public static FameFile Open(int seed, int day) => new FameFile { Seed = seed, StartDay = day };

        /// <summary>The daily round. A day already played changes nothing.</summary>
        public static FameDayReport Day(FameFile f, FameInputs inputs, int day)
        {
            var r = new FameDayReport
            {
                WealthBefore = f.Wealth, RecognitionBefore = f.Recognition, OverallBefore = f.Overall, TierBefore = (int)f.Tier
            };
            if (day != f.SettledDay)
            {
                int gone = f.SettledDay < 0 ? 1 : Clamp(day - f.SettledDay, 1, 30);
                Settle(f, inputs, gone);
                SettleDistricts(f, inputs);
                r.Moved = SettleParts(f, inputs, day);
                f.SettledDay = day;
            }
            r.WealthAfter = f.Wealth;
            r.RecognitionAfter = f.Recognition;
            r.OverallAfter = f.Overall;
            r.TierAfter = (int)f.Tier;
            return r;
        }

        // Wealth and recognition from what was read, the papers a little forgotten for each day gone.
        private static void Settle(FameFile f, FameInputs inputs, int gone)
        {
            f.NetWorth = inputs.NetWorth;
            f.Weekly = inputs.Weekly;
            f.Followers = Math.Max(0, inputs.Followers);
            f.Businesses = inputs.Shops.Count;
            f.Properties = Math.Max(0, inputs.Properties);
            f.Staff = 0;
            for (int i = 0; i < inputs.Shops.Count; i++)
            {
                f.Staff += Math.Max(0, inputs.Shops[i].Staff);
            }
            f.Wealth = Wealth(f.NetWorth, f.Weekly, f.Businesses);
            for (int i = 0; i < gone; i++)
            {
                f.Press -= (f.Press * PressForget + 999) / 1000;
            }
            int brandBest = 0;
            foreach (KeyValuePair<string, int> b in inputs.Brand)
            {
                brandBest = Math.Max(brandBest, b.Value);
            }
            f.Recognition = Recognition(f.Wealth, f.Followers, f.Press, brandBest);
        }

        private static void SettleDistricts(FameFile f, FameInputs inputs)
        {
            for (int i = 0; i < f.Districts.Count; i++)
            {
                FameDistrict d = f.Districts[i];
                d.Jobs = d.Shops = d.Raised = d.Brand = 0;
            }
            for (int i = 0; i < inputs.Shops.Count; i++)
            {
                FameDistrict d = f.DistrictOrNew(inputs.Shops[i].District);
                if (d != null)
                {
                    d.Shops++;
                    d.Jobs += Math.Max(0, inputs.Shops[i].Staff);
                }
            }
            for (int i = 0; i < inputs.Holdings.Count; i++)
            {
                FameHolding h = inputs.Holdings[i];
                FameDistrict d = h.Let && h.Ask > AskMarket ? f.DistrictOrNew(h.District) : null;
                if (d != null)
                {
                    d.Raised++;
                }
            }
            foreach (KeyValuePair<string, int> b in inputs.Brand)
            {
                FameDistrict d = f.District(b.Key);
                if (d != null)
                {
                    d.Brand = Clamp(b.Value, 0, 100);
                }
            }
            for (int i = 0; i < f.Districts.Count; i++)
            {
                FameDistrict d = f.Districts[i];
                d.Known = DistrictKnown(f.Recognition, d.Shops, d.Brand);
                d.Opinion = Clamp(d.Opinion + Drift(d.Opinion, Present(d) ? DistrictTarget(d.Jobs, d.Raised) : Start), 0, 100);
            }
        }

        // The four parts a day nearer their targets; integrity a point nearer 50 once a week. Returns the points moved.
        private static int SettleParts(FameFile f, FameInputs inputs, int day)
        {
            int moved = Step(f, FamePart.Customers, CustomersTarget(inputs.Shops)) + Step(f, FamePart.Workers, WorkersTarget(inputs.Shops));

            const int neighbourhoods = (int)FamePart.Neighbourhood;
            int before = f.Rep[neighbourhoods], targets = 0, present = 0;
            for (int i = 0; i < f.Districts.Count; i++)
            {
                if (Present(f.Districts[i]))
                {
                    targets += DistrictTarget(f.Districts[i].Jobs, f.Districts[i].Raised);
                    present++;
                }
            }
            f.Target[neighbourhoods] = present > 0 ? Round((double)targets / present) : Start;
            Neighbourhoods(f);
            if (present == 0)
            {
                f.Rep[neighbourhoods] = Clamp(f.Rep[neighbourhoods] + Drift(f.Rep[neighbourhoods], Start), 0, 100);
            }
            moved += Math.Abs(f.Rep[neighbourhoods] - before);

            const int integrity = (int)FamePart.Integrity;
            f.Target[integrity] = Start;
            if ((day - f.StartDay) % ForgetDays == 0 && f.Rep[integrity] != Start && day >= f.ScandalUntil)
            {
                f.Rep[integrity] += Math.Sign(Start - f.Rep[integrity]);
                moved++;
            }
            return moved;
        }

        private static int Step(FameFile f, FamePart part, int target)
        {
            int p = (int)part;
            f.Target[p] = target;
            int step = Drift(f.Rep[p], target);
            f.Rep[p] = Clamp(f.Rep[p] + step, 0, 100);
            return Math.Abs(step);
        }

        /// <summary>The chance, per mille, that somebody near the player in the street knows them, at each look.</summary>
        public static int StreetChance(int known) => known < StreetFrom ? 0 : (known - 10) * StreetChanceStep / 2;

        /// <summary>True if the street may react now: under the day's most, and long enough since the last one. Counts it if so.</summary>
        public static bool StreetMay(FameFile f, int day, int minute)
        {
            if (f.StreetDay != day)
            {
                f.StreetDay = day;
                f.StreetToday = 0;
                f.StreetMinute = -10000;
            }
            if (f.StreetToday >= StreetMost || minute - f.StreetMinute < StreetGapMinutes)
            {
                return false;
            }
            f.StreetToday++;
            f.StreetMinute = minute;
            return true;
        }

        /// <summary>What somebody in the street does, from a roll of 0 to 999.</summary>
        public static FameReaction React(int overall, int wealth, FameDistrict district, int roll)
        {
            roll = Clamp(roll, 0, 999);
            if (district != null && district.Raised > 0 && district.Opinion < 40 && roll < 500)
            {
                return FameReaction.Rents;
            }
            if (overall < 35)
            {
                return roll < 600 ? FameReaction.Complaint : FameReaction.Look;
            }
            bool rich = wealth >= 50;
            if (overall >= 65)
            {
                return roll < 300 ? FameReaction.Praise : roll < 750 ? FameReaction.Greeting : rich ? FameReaction.Remark : FameReaction.Look;
            }
            return roll < 400 ? FameReaction.Look : roll < 800 ? FameReaction.Greeting : rich ? FameReaction.Remark : FameReaction.Look;
        }

        public static bool Speaks(FameReaction reaction) => reaction != FameReaction.None && reaction != FameReaction.Look;

        /// <summary>True if a customer in one of the player's own shops may know them now. Counts it if so.</summary>
        public static bool ShopMay(FameFile f, int day, int known)
        {
            if (f.ShopDay != day)
            {
                f.ShopDay = day;
                f.ShopToday = 0;
            }
            if (known < ShopFrom || f.ShopToday >= ShopMost)
            {
                return false;
            }
            f.ShopToday++;
            return true;
        }

        /// <summary>True for the first visit today to a business with staff, for <see cref="VisitsMost"/> businesses a day.</summary>
        public static bool VisitCounts(FameFile f, string place, int day)
        {
            if (f.VisitDay != day)
            {
                f.VisitDay = day;
                f.Visited.Clear();
            }
            if (string.IsNullOrEmpty(place) || f.Visited.Contains(place) || f.Visited.Count >= VisitsMost)
            {
                return false;
            }
            f.Visited.Add(place);
            return true;
        }

        /// <summary>How the staff take a visit: +1 proud, -1 nervous, 0 neither.</summary>
        public static int VisitMood(int workers) => workers >= ProudFrom ? 1 : workers < NervousUnder ? -1 : 0;

        /// <summary>True the first time today: routine things share one notice a day. Takes it if so.</summary>
        public static bool MayNotice(FameFile f, int day)
        {
            if (f.NoticeDay == day)
            {
                return false;
            }
            f.NoticeDay = day;
            return true;
        }
    }
}
