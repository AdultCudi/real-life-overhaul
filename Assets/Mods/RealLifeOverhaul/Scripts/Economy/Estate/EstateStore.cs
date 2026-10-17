using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace RealLifeOverhaul.Economy
{
    /// <summary>
    /// The property business's line in the save's modData. Game-free. "v=" and the version, then parts divided by '^',
    /// each a letter and a colon (h header, p buildings, u other units, g agency, d districts, i instructions, t hotels,
    /// c car hire, v cars), records by ';' and fields by '|' as <see cref="Write"/> writes them. A later version only
    /// adds fields at a record's end or new parts. <see cref="TestKey"/> holds the line from before the first test tool.
    /// </summary>
    public static class EstateStore
    {
        public const string Key = "reallife:economy_estate";
        public const string TestKey = "reallife:economy_estate_before_tests";
        public const int Version = 2;   // 2: part u, the units after the first

        private const int HoldingFields = 36, UnitFields = 15, ApplicantFields = 6;
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
            return SaveLines.Checked(data, key, Read(text, out version), version, Version);   // SaveLines keeps a line that cannot be read
        }

        public static string Write(EstateState s)
        {
            var t = new StringBuilder(256 + s.Holdings.Count * 220 + s.Hire.Cars.Count * 90);
            t.Append("v=").Append(Version).Append("^h:");
            Join(t, s.Seed, s.FirstDay, s.LastDay, s.Real, s.Months, s.YearFrom, s.TaxFrom, s.TaxTakings, s.TaxCosts);
            t.Append("^p:");
            for (int i = 0; i < s.Holdings.Count && i < EstateCatalogue.HoldingsMost; i++)
            {
                EstateHolding h = s.Holdings[i];
                EstateUnit u = h.Units[0];
                t.Append(i > 0 ? ";" : string.Empty).Append(Clean(h.Place)).Append('|');
                Join(t, h.Paid, h.BoughtDay, h.Spent, h.Use, h.Cond, h.Level, u.Ask, u.ToLet ? 1 : 0, h.Cover ? 1 : 0,
                    u.Tenant, u.Record, u.Rent, u.Deposit, u.Until, u.Arrears, u.ArrearsDays, u.NoticeEnd, u.CourtDay, u.VoidDays,
                    h.Work, h.WorkEnd, h.WorkCost, h.WorkTo, h.PlanUse, h.List, h.Offer, h.OfferLapse, h.SaleDay, h.SalePrice, h.SaleFails ? 1 : 0,
                    h.RentYear, h.CostYear, h.NextApplicant, h.Managed ? 1 : 0);
                Applicants(t, u);
            }
            t.Append("^u:");
            bool first = true;
            for (int i = 0; i < s.Holdings.Count && i < EstateCatalogue.HoldingsMost; i++)
            {
                EstateHolding h = s.Holdings[i];
                for (int n = 1; n < h.Units.Count && n < EstateCatalogue.UnitsMost; n++)
                {
                    EstateUnit u = h.Units[n];
                    if (!h.Managed && !u.Let && u.Applicants.Count == 0 && u.Ask == EstateCatalogue.AskStart && u.ToLet)
                    {
                        continue;   // a blank unit of a building the game lets: made again from the game's count
                    }
                    t.Append(first ? string.Empty : ";").Append(Clean(h.Place)).Append('|');
                    first = false;
                    Join(t, n, u.Ask, u.ToLet ? 1 : 0, u.Tenant, u.Record, u.Rent, u.Deposit, u.Until, u.Arrears, u.ArrearsDays, u.NoticeEnd, u.CourtDay, u.VoidDays);
                    Applicants(t, u);
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
                Join(t, f.Leased ? 1 : 0, f.Paid, f.Day, f.LeaseDaily, f.Rate[0], f.Rate[1], f.Staff, f.Clean, f.Rating, f.PushEnd,
                    f.GroupRooms, f.GroupNights, f.GroupRate, f.AskRooms, f.AskNights, f.AskRate, f.AskLapse, f.OutRooms, f.OutEnd, f.InspectDay,
                    f.Sold[0], f.Sold[1], f.Takings, f.Costs, f.TakingsYear, f.CostsYear);
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
                Join(t, car.Price, car.Rate, car.State, car.Day, car.HireDays, car.HireRate, car.Deposit, car.HireSeed, car.X, car.Y, car.Z, car.Yaw, car.Takings, car.Hires, car.HireRating);
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
                            s.TaxFrom = Math.Max(0, I(c, 6));     // 0 when absent: TradingYear starts the count
                            s.TaxTakings = Math.Max(0, I(c, 7));
                            s.TaxCosts = Math.Max(0, I(c, 8));
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
                        case 'u':
                            foreach (string record in Records(body))
                            {
                                string[] c = record.Split('|');
                                EstateHolding h = c.Length >= UnitFields ? s.Holding(c[0]) : null;
                                int n = I(c, 1);
                                if (h != null && n >= 1 && n < EstateCatalogue.UnitsMost && (n >= h.Units.Count || IsBlank(h.Units[n])))
                                {
                                    ReadUnit(h, n, c);
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
                                if (c.Length >= 12 && id > 0 && stage >= 0 && stage <= (int)InstructionStage.Completing
                                    && s.Agency.Open.Count < EstateCatalogue.InstructionsMost && s.Agency.Find(id) == null)
                                {
                                    s.Agency.Open.Add(new Instruction
                                    {
                                        Id = id, District = c[1], Sale = I(c, 2) != 0, Sqm = Math.Max(1, I(c, 3, 1)), Value = Math.Max(0, I(c, 4)),
                                        Ask = EstateCatalogue.Clamp(I(c, 5), EstateCatalogue.OwnerAskLeast, EstateCatalogue.OwnerAskMost), Stage = stage,
                                        Hours = Math.Max(1, I(c, 7, 1)), Done = Math.Max(0, I(c, 8)), Day = Math.Max(0, I(c, 9)), Price = Math.Max(0, I(c, 10)), Fails = I(c, 11) != 0
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
                                        Id = c[0], Type = c[1], Price = Math.Max(0, I(c, 2)),
                                        Rate = EstateCatalogue.Clamp(I(c, 3, 1000), EstateCatalogue.RateLeast, EstateCatalogue.RateMost), State = state,
                                        Day = Math.Max(0, I(c, 5)), HireDays = Math.Max(0, I(c, 6)), HireRate = Math.Max(0, I(c, 7)), Deposit = Math.Max(0, I(c, 8)), HireSeed = I(c, 9),
                                        X = I(c, 10), Y = I(c, 11), Z = I(c, 12), Yaw = I(c, 13), Takings = Math.Max(0, I(c, 14)), Hires = Math.Max(0, I(c, 15)),
                                        HireRating = EstateCatalogue.Clamp(I(c, 16, -1), -1, 1000)
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
                Cover = I(c, 9) != 0, WorkEnd = Math.Max(0, I(c, 21)), WorkCost = Math.Max(0, I(c, 22)), WorkTo = Math.Max(0, I(c, 23)),
                PlanUse = EstateCatalogue.Clamp(I(c, 24, -1), -1, EstateCatalogue.Uses - 1), List = Math.Max(0, I(c, 25)), Offer = Math.Max(0, I(c, 26)), OfferLapse = Math.Max(0, I(c, 27)),
                SaleDay = Math.Max(0, I(c, 28)), SalePrice = Math.Max(0, I(c, 29)), SaleFails = I(c, 30) != 0, RentYear = Math.Max(0, I(c, 31)), CostYear = Math.Max(0, I(c, 32)),
                NextApplicant = Math.Max(1, I(c, 33, 1)), Managed = I(c, 34) != 0
            };
            int work = I(c, 20);
            h.Work = work >= 0 && work <= (int)EstateWork.Convert ? work : 0;
            EstateUnit u = h.Units[0];
            u.Ask = EstateCatalogue.Clamp(I(c, 7, EstateCatalogue.AskStart), EstateCatalogue.AskLeast, EstateCatalogue.AskMost);
            u.ToLet = I(c, 8, 1) != 0;
            Lease(u, c, 10);
            ReadApplicants(h, u, c, HoldingFields - 1);
            return h;
        }

        // A unit after the first (part u). Units in between that no record names stay empty.
        private static void ReadUnit(EstateHolding h, int n, string[] c)
        {
            while (h.Units.Count <= n)
            {
                h.Units.Add(new EstateUnit());
            }
            EstateUnit u = h.Units[n];
            u.Ask = EstateCatalogue.Clamp(I(c, 2, EstateCatalogue.AskStart), EstateCatalogue.AskLeast, EstateCatalogue.AskMost);
            u.ToLet = I(c, 3, 1) != 0;
            Lease(u, c, 4);
            ReadApplicants(h, u, c, UnitFields - 1);
        }

        // A tenancy's ten fields from 'at': tenant, record, rent, deposit, until, arrears, days in arrears, notice ends, court day, days empty.
        private static void Lease(EstateUnit u, string[] c, int at)
        {
            u.Tenant = EstateCatalogue.Clamp(I(c, at, -1), -1, EstateCatalogue.TenantKinds - 1);
            u.Record = EstateCatalogue.Clamp(I(c, at + 1), 0, 100);
            u.Rent = Math.Max(0, I(c, at + 2));
            u.Deposit = Math.Max(0, I(c, at + 3));
            u.Until = Math.Max(0, I(c, at + 4));
            u.Arrears = Math.Max(0, I(c, at + 5));
            u.ArrearsDays = Math.Max(0, I(c, at + 6));
            u.NoticeEnd = Math.Max(0, I(c, at + 7));
            u.CourtDay = Math.Max(0, I(c, at + 8));
            u.VoidDays = Math.Max(0, I(c, at + 9));
            if (u.Tenant < 0)
            {
                u.Vacant();
            }
        }

        // The count of applicants at 'at' and six fields each after it; a number the building has already ends them.
        private static void ReadApplicants(EstateHolding h, EstateUnit u, string[] c, int at)
        {
            int applicants = EstateCatalogue.Clamp(I(c, at), 0, EstateCatalogue.ApplicantsMost);
            for (int a = 0; a < applicants; a++)
            {
                int from = at + 1 + a * ApplicantFields;
                if (from + ApplicantFields > c.Length || I(c, from) <= 0 || h.ApplicantOf(I(c, from)) != null)
                {
                    break;
                }
                u.Applicants.Add(new RentApplicant
                {
                    Id = I(c, from), Kind = EstateCatalogue.Clamp(I(c, from + 1), 0, EstateCatalogue.TenantKinds - 1), Offer = Math.Max(1, I(c, from + 2, 1)),
                    Record = EstateCatalogue.Clamp(I(c, from + 3), 0, 100), Term = Math.Max(1, I(c, from + 4, 1)), Lapse = Math.Max(0, I(c, from + 5))
                });
            }
        }

        // A unit nothing has been read into yet: a second record for the same unit is left out.
        private static bool IsBlank(EstateUnit u)
        {
            return !u.Let && u.Applicants.Count == 0 && u.VoidDays == 0 && u.Ask == EstateCatalogue.AskStart && u.ToLet;
        }

        // A unit's applicants, capped, after its count.
        private static void Applicants(StringBuilder t, EstateUnit u)
        {
            int applicants = Math.Min(u.Applicants.Count, EstateCatalogue.ApplicantsMost);
            t.Append('|').Append(applicants.ToString(Inv));
            for (int a = 0; a < applicants; a++)
            {
                RentApplicant p = u.Applicants[a];
                t.Append('|');
                Join(t, p.Id, p.Kind, p.Offer, p.Record, p.Term, p.Lapse);
            }
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

        /// <summary>A text with the three dividers taken out, so it can never break the line apart.</summary>
        public static string Clean(string text)
        {
            return string.IsNullOrEmpty(text) ? string.Empty : text.Replace('|', ' ').Replace(';', ' ').Replace('^', ' ');
        }
    }
}
