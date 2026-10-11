using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace RealLifeOverhaul.Economy
{
    /// <summary>
    /// The property business in the save: one line of the save's modData, under <see cref="Key"/>. Game-free.
    ///
    ///     v=1^h:...^p:...^g:...^d:...^i:...^t:...^c:...^v:...
    ///
    /// Parts are divided by '^' and begin with a letter and a colon; records inside a part by ';', fields by '|'.
    ///   h  the header: seed|first day|last day|the market|months|the year's first day
    ///   p  the buildings: place|paid|bought|spent|use|condition|level|asking rent|to let|cover|tenant|record|rent|
    ///      deposit|until|arrears|days in arrears|notice ends|court day|days empty|work|its end|its cost|its target|
    ///      use allowed|asking price|offer|offer lapses|sale day|sale price|sale fails|rent this year|costs this year|
    ///      next applicant|let by the player|applicants, then six fields each: id|description|offer|record|term|lapses
    ///   g  the agency: office|next id|commission|deals|lost
    ///   d  the districts: name|reputation
    ///   i  the instructions: id|district|sale|square metres|value|asked|stage|hours|done|day|price|fails
    ///   t  the hotels: id|leased|paid|day|lease a day|two rates|staff|clean|rating|campaign ends|group rooms|nights|
    ///      rate|asked rooms|nights|rate|lapses|rooms out|until|inspection|two sold|takings|costs|takings this
    ///      year|costs this year|six reviews
    ///   c  car hire: lot|rating|cover|takings this year|costs this year
    ///   v  the cars: id|type|price|rate|state|day|hire days|hire rate|deposit|hire seed|x|y|z|yaw|takings|hires
    ///
    /// Every figure is a whole number, so what is read back is exactly what was written. A NEWER VERSION adds fields at
    /// the end of a record or a part with a new letter; this reader takes the fields it knows and skips parts it does
    /// not, so a save written by a later version still loads. A figure out of its bounds is put inside them; a line
    /// that cannot be read is left alone and the feature starts afresh. The lists are capped when written. Nothing
    /// here is the game's: a save loads in the plain game with this line ignored.
    ///
    /// <see cref="TestKey"/> holds the same line as it was before the first test tool changed it, for as long as test
    /// changes are in place.
    /// </summary>
    public static class EstateStore
    {
        public const string Key = "reallife:economy_estate";
        public const string TestKey = "reallife:economy_estate_before_tests";
        public const int Version = 1;

        private const int HoldingFields = 36, ApplicantFields = 6;
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        /// <summary>The state of this save; null if the save has none or it cannot be read.</summary>
        public static EstateState Load(Dictionary<string, string> data, string key, out int version)
        {
            version = 0;
            string text;
            if (data == null || !data.TryGetValue(key, out text) || string.IsNullOrEmpty(text))
            {
                return null;
            }
            return Read(text, out version);
        }

        public static string Write(EstateState s)
        {
            var t = new StringBuilder(256 + s.Holdings.Count * 160 + s.Hire.Cars.Count * 90);
            t.Append("v=").Append(Version).Append("^h:");
            Join(t, s.Seed, s.FirstDay, s.LastDay, s.Real, s.Months, s.YearFrom);
            t.Append("^p:");
            for (int i = 0; i < s.Holdings.Count && i < EstateCatalogue.HoldingsMost; i++)
            {
                EstateHolding h = s.Holdings[i];
                int applicants = Math.Min(h.Applicants.Count, EstateCatalogue.ApplicantsMost);
                t.Append(i > 0 ? ";" : string.Empty).Append(Clean(h.Place)).Append('|');
                Join(t, h.Paid, h.BoughtDay, h.Spent, h.Use, h.Cond, h.Level, h.Ask, h.ToLet ? 1 : 0, h.Cover ? 1 : 0, h.Tenant, h.Record, h.Rent, h.Deposit, h.Until, h.Arrears, h.ArrearsDays, h.NoticeEnd,
                    h.CourtDay, h.VoidDays, h.Work, h.WorkEnd, h.WorkCost, h.WorkTo, h.PlanUse, h.List, h.Offer, h.OfferLapse, h.SaleDay, h.SalePrice, h.SaleFails ? 1 : 0, h.RentYear, h.CostYear, h.NextApplicant,
                    h.Managed ? 1 : 0, applicants);
                for (int a = 0; a < applicants; a++)
                {
                    Applicant p = h.Applicants[a];
                    t.Append('|');
                    Join(t, p.Id, p.Kind, p.Offer, p.Record, p.Term, p.Lapse);
                }
            }
            AgencyFile g = s.Agency;
            t.Append("^g:").Append(Clean(g.Office)).Append('|');
            Join(t, g.NextId, g.Commission, g.Deals, g.Lost);
            t.Append("^d:");
            for (int i = 0; i < g.Districts.Count && i < EstateCatalogue.DistrictsMost; i++)
            {
                t.Append(i > 0 ? ";" : string.Empty).Append(Clean(g.Districts[i].Name)).Append('|');
                Join(t, g.Districts[i].Rep);
            }
            t.Append("^i:");
            for (int i = 0; i < g.Open.Count && i < EstateCatalogue.InstructionsMost; i++)
            {
                Instruction n = g.Open[i];
                t.Append(i > 0 ? ";" : string.Empty);
                Join(t, n.Id);
                t.Append('|').Append(Clean(n.District)).Append('|');
                Join(t, n.Sale ? 1 : 0, n.Sqm, n.Value, n.Ask, n.Stage, n.Hours, n.Done, n.Day, n.Price, n.Fails ? 1 : 0);
            }
            t.Append("^t:");
            for (int i = 0; i < s.Hotels.Count && i < EstateCatalogue.HotelsMost; i++)
            {
                HotelFile f = s.Hotels[i];
                t.Append(i > 0 ? ";" : string.Empty).Append(Clean(f.Slot)).Append('|');
                Join(t, f.Leased ? 1 : 0, f.Paid, f.Day, f.LeaseDaily, f.Rate[0], f.Rate[1], f.Staff, f.Clean, f.Rating, f.PushEnd, f.GroupRooms, f.GroupNights, f.GroupRate, f.AskRooms, f.AskNights, f.AskRate,
                    f.AskLapse, f.OutRooms, f.OutEnd, f.InspectDay, f.Sold[0], f.Sold[1], f.Takings, f.Costs, f.TakingsYear, f.CostsYear);
                for (int r = 0; r < f.Reviews.Length; r++)
                {
                    t.Append('|').Append(f.Reviews[r].ToString(Inv));
                }
            }
            HireFile hire = s.Hire;
            t.Append("^c:").Append(Clean(hire.Lot)).Append('|');
            Join(t, hire.Rating, hire.Cover ? 1 : 0, hire.TakingsYear, hire.CostsYear);
            t.Append("^v:");
            for (int i = 0; i < hire.Cars.Count && i < EstateCatalogue.HireCarsMost; i++)
            {
                HireCar car = hire.Cars[i];
                t.Append(i > 0 ? ";" : string.Empty).Append(Clean(car.Id)).Append('|').Append(Clean(car.Type)).Append('|');
                Join(t, car.Price, car.Rate, car.State, car.Day, car.HireDays, car.HireRate, car.Deposit, car.HireSeed, car.X, car.Y, car.Z, car.Yaw, car.Takings, car.Hires);
            }
            return t.ToString();
        }

        /// <summary>Reads a line. Null for one that is not this feature's at all.</summary>
        public static EstateState Read(string text, out int version)
        {
            version = 0;
            if (string.IsNullOrEmpty(text) || !text.StartsWith("v=", StringComparison.Ordinal))
            {
                return null;
            }
            try
            {
                string[] parts = text.Split('^');
                if (!int.TryParse(parts[0].Substring(2), NumberStyles.Integer, Inv, out version) || version < 1)
                {
                    return null;
                }
                var s = new EstateState();
                bool header = false;
                for (int p = 1; p < parts.Length; p++)
                {
                    string part = parts[p];
                    if (part.Length < 2 || part[1] != ':')
                    {
                        continue;
                    }
                    string body = part.Substring(2);
                    switch (part[0])
                    {
                        case 'h':
                        {
                            string[] c = body.Split('|');
                            s.Seed = I(c, 0);
                            s.FirstDay = Math.Max(0, I(c, 1));
                            s.LastDay = Math.Max(0, I(c, 2));
                            s.Real = EstateCatalogue.Clamp(I(c, 3, EstateCatalogue.RealStart), EstateCatalogue.RealLeast, EstateCatalogue.RealMost);
                            s.Months = Math.Max(0, I(c, 4));
                            s.YearFrom = Math.Max(0, I(c, 5));
                            header = true;
                            break;
                        }
                        case 'p':
                            foreach (string record in Records(body))
                            {
                                string[] c = record.Split('|');
                                if (c[0].Length > 0 && s.Holdings.Count < EstateCatalogue.HoldingsMost && s.Holding(c[0]) == null)
                                {
                                    s.Holdings.Add(ReadHolding(c));
                                }
                            }
                            break;
                        case 'g':
                        {
                            string[] c = body.Split('|');
                            s.Agency.Office = c[0];
                            s.Agency.NextId = Math.Max(1, I(c, 1, 1));
                            s.Agency.Commission = Math.Max(0, I(c, 2));
                            s.Agency.Deals = Math.Max(0, I(c, 3));
                            s.Agency.Lost = Math.Max(0, I(c, 4));
                            break;
                        }
                        case 'd':
                            foreach (string record in Records(body))
                            {
                                string[] c = record.Split('|');
                                if (c[0].Length > 0 && s.Agency.Districts.Count < EstateCatalogue.DistrictsMost && s.Agency.District(c[0]) == null)
                                {
                                    s.Agency.Districts.Add(new DistrictRep { Name = c[0], Rep = EstateCatalogue.Clamp(I(c, 1, EstateCatalogue.ReputationStart), 0, 1000) });
                                }
                            }
                            break;
                        case 'i':
                            foreach (string record in Records(body))
                            {
                                string[] c = record.Split('|');
                                int id = I(c, 0), stage = I(c, 6, -1);
                                if (c.Length >= 12 && id > 0 && stage >= 0 && stage <= (int)InstructionStage.Completing && s.Agency.Open.Count < EstateCatalogue.InstructionsMost && s.Agency.Find(id) == null)
                                {
                                    s.Agency.Open.Add(new Instruction
                                    {
                                        Id = id, District = c[1], Sale = I(c, 2) != 0, Sqm = Math.Max(1, I(c, 3, 1)), Value = Math.Max(0, I(c, 4)),
                                        Ask = EstateCatalogue.Clamp(I(c, 5), EstateCatalogue.OwnerAskLeast, EstateCatalogue.OwnerAskMost), Stage = stage, Hours = Math.Max(1, I(c, 7, 1)), Done = Math.Max(0, I(c, 8)),
                                        Day = Math.Max(0, I(c, 9)), Price = Math.Max(0, I(c, 10)), Fails = I(c, 11) != 0
                                    });
                                }
                            }
                            break;
                        case 't':
                            foreach (string record in Records(body))
                            {
                                string[] c = record.Split('|');
                                if (c.Length >= 27 && c[0].Length > 0 && s.Hotels.Count < EstateCatalogue.HotelsMost && s.Hotel(c[0]) == null)
                                {
                                    s.Hotels.Add(ReadHotel(c));
                                }
                            }
                            break;
                        case 'c':
                        {
                            string[] c = body.Split('|');
                            s.Hire.Lot = c[0];
                            s.Hire.Rating = EstateCatalogue.Clamp(I(c, 1, EstateCatalogue.HireRatingStart), 0, 1000);
                            s.Hire.Cover = I(c, 2) != 0;
                            s.Hire.TakingsYear = Math.Max(0, I(c, 3));
                            s.Hire.CostsYear = Math.Max(0, I(c, 4));
                            break;
                        }
                        case 'v':
                            foreach (string record in Records(body))
                            {
                                string[] c = record.Split('|');
                                int state = I(c, 4, -1);
                                if (c.Length >= 16 && c[0].Length > 0 && state >= 0 && state <= (int)HireState.Waiting && s.Hire.Cars.Count < EstateCatalogue.HireCarsMost && s.Hire.Car(c[0]) == null)
                                {
                                    s.Hire.Cars.Add(new HireCar
                                    {
                                        Id = c[0], Type = c[1], Price = Math.Max(0, I(c, 2)), Rate = EstateCatalogue.Clamp(I(c, 3, 1000), EstateCatalogue.RateLeast, EstateCatalogue.RateMost), State = state,
                                        Day = Math.Max(0, I(c, 5)), HireDays = Math.Max(0, I(c, 6)), HireRate = Math.Max(0, I(c, 7)), Deposit = Math.Max(0, I(c, 8)), HireSeed = I(c, 9),
                                        X = I(c, 10), Y = I(c, 11), Z = I(c, 12), Yaw = I(c, 13), Takings = Math.Max(0, I(c, 14)), Hires = Math.Max(0, I(c, 15))
                                    });
                                }
                            }
                            break;
                    }
                }
                return header ? s : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static EstateHolding ReadHolding(string[] c)
        {
            var h = new EstateHolding
            {
                Place = c[0], Paid = Math.Max(0, I(c, 1)), BoughtDay = Math.Max(0, I(c, 2)), Spent = Math.Max(0, I(c, 3)), Use = EstateCatalogue.Clamp(I(c, 4), 0, EstateCatalogue.Uses - 1),
                Cond = EstateCatalogue.Clamp(I(c, 5, EstateCatalogue.ConditionNew), 0, 1000), Level = EstateCatalogue.Clamp(I(c, 6), 0, EstateCatalogue.LevelMost),
                Ask = EstateCatalogue.Clamp(I(c, 7, EstateCatalogue.AskStart), EstateCatalogue.AskLeast, EstateCatalogue.AskMost), ToLet = I(c, 8, 1) != 0, Cover = I(c, 9) != 0,
                Tenant = EstateCatalogue.Clamp(I(c, 10, -1), -1, EstateCatalogue.TenantKinds - 1), Record = EstateCatalogue.Clamp(I(c, 11), 0, 100), Rent = Math.Max(0, I(c, 12)), Deposit = Math.Max(0, I(c, 13)),
                Until = Math.Max(0, I(c, 14)), Arrears = Math.Max(0, I(c, 15)), ArrearsDays = Math.Max(0, I(c, 16)), NoticeEnd = Math.Max(0, I(c, 17)), CourtDay = Math.Max(0, I(c, 18)),
                VoidDays = Math.Max(0, I(c, 19)), WorkEnd = Math.Max(0, I(c, 21)), WorkCost = Math.Max(0, I(c, 22)), WorkTo = Math.Max(0, I(c, 23)),
                PlanUse = EstateCatalogue.Clamp(I(c, 24, -1), -1, EstateCatalogue.Uses - 1), List = Math.Max(0, I(c, 25)), Offer = Math.Max(0, I(c, 26)), OfferLapse = Math.Max(0, I(c, 27)),
                SaleDay = Math.Max(0, I(c, 28)), SalePrice = Math.Max(0, I(c, 29)), SaleFails = I(c, 30) != 0, RentYear = Math.Max(0, I(c, 31)), CostYear = Math.Max(0, I(c, 32)),
                NextApplicant = Math.Max(1, I(c, 33, 1)), Managed = I(c, 34) != 0
            };
            int work = I(c, 20);
            h.Work = work >= 0 && work <= (int)EstateWork.Convert ? work : 0;
            if (h.Tenant < 0)
            {
                h.Record = h.Rent = h.Deposit = h.Until = h.Arrears = h.ArrearsDays = h.NoticeEnd = h.CourtDay = 0;   // (nobody in: nothing is owed and no notice runs)
            }
            int applicants = EstateCatalogue.Clamp(I(c, 35), 0, EstateCatalogue.ApplicantsMost);
            for (int a = 0; a < applicants; a++)
            {
                int at = HoldingFields + a * ApplicantFields;
                if (at + ApplicantFields > c.Length || I(c, at) <= 0 || h.ApplicantOf(I(c, at)) != null)
                {
                    break;
                }
                h.Applicants.Add(new Applicant
                {
                    Id = I(c, at), Kind = EstateCatalogue.Clamp(I(c, at + 1), 0, EstateCatalogue.TenantKinds - 1), Offer = Math.Max(1, I(c, at + 2, 1)),
                    Record = EstateCatalogue.Clamp(I(c, at + 3), 0, 100), Term = Math.Max(1, I(c, at + 4, 1)), Lapse = Math.Max(0, I(c, at + 5))
                });
            }
            return h;
        }

        private static HotelFile ReadHotel(string[] c)
        {
            var f = new HotelFile
            {
                Slot = c[0], Leased = I(c, 1) != 0, Paid = Math.Max(0, I(c, 2)), Day = Math.Max(0, I(c, 3)), LeaseDaily = Math.Max(0, I(c, 4)),
                Staff = EstateCatalogue.Clamp(I(c, 7, 1), 0, EstateCatalogue.StaffLevels - 1), Clean = EstateCatalogue.Clamp(I(c, 8), 0, 1000), Rating = EstateCatalogue.Clamp(I(c, 9), 0, 1000),
                PushEnd = Math.Max(0, I(c, 10)), GroupRooms = Math.Max(0, I(c, 11)), GroupNights = Math.Max(0, I(c, 12)), GroupRate = Math.Max(0, I(c, 13)), AskRooms = Math.Max(0, I(c, 14)),
                AskNights = Math.Max(0, I(c, 15)), AskRate = Math.Max(0, I(c, 16)), AskLapse = Math.Max(0, I(c, 17)), OutRooms = Math.Max(0, I(c, 18)), OutEnd = Math.Max(0, I(c, 19)),
                InspectDay = Math.Max(0, I(c, 20)), Takings = Math.Max(0, I(c, 23)), Costs = Math.Max(0, I(c, 24)), TakingsYear = Math.Max(0, I(c, 25)), CostsYear = Math.Max(0, I(c, 26))
            };
            f.Rate[0] = EstateCatalogue.Clamp(I(c, 5, 1000), EstateCatalogue.RateLeast, EstateCatalogue.RateMost);
            f.Rate[1] = EstateCatalogue.Clamp(I(c, 6, 1000), EstateCatalogue.RateLeast, EstateCatalogue.RateMost);
            f.Sold[0] = Math.Max(0, I(c, 21));
            f.Sold[1] = Math.Max(0, I(c, 22));
            for (int r = 0; r < f.Reviews.Length; r++)
            {
                f.Reviews[r] = EstateCatalogue.Clamp(I(c, 27 + r), 0, HotelRules.ReviewDear);
            }
            return f;
        }

        private static string[] Records(string body)
        {
            return body.Length == 0 ? Array.Empty<string>() : body.Split(';');
        }

        private static void Join(StringBuilder t, params int[] values)
        {
            for (int i = 0; i < values.Length; i++)
            {
                t.Append(i > 0 ? "|" : string.Empty).Append(values[i].ToString(Inv));
            }
        }

        private static int I(string[] c, int at, int fallback = 0)
        {
            int value;
            return at < c.Length && int.TryParse(c[at], NumberStyles.Integer, Inv, out value) ? value : fallback;
        }

        /// <summary>A text with the three dividers taken out, so that it can never break the line apart.</summary>
        public static string Clean(string text)
        {
            return string.IsNullOrEmpty(text) ? string.Empty : text.Replace('|', ' ').Replace(';', ' ').Replace('^', ' ');
        }
    }
}
