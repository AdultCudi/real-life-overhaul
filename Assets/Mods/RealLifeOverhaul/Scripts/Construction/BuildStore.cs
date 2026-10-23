using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace RealLifeOverhaul.Construction
{
    /// <summary>
    /// The construction game in the save, game-free: one line under <see cref="Key"/>, "v=1^h:header^f:firm^l:lots^c:crews
    /// ^q:plant^s:staff^p:projects^t:tenders^r:rivals^d:developments^o:offers". Parts are divided by '^', records by ';' and
    /// fields by '|'; a text is escaped, so no divider is ever in one. A newer version adds fields at the end of a record, or
    /// a part with a new letter: this reader takes what it knows, gives a missing field its default, bounds every figure
    /// and caps every list. It names the district's lots by their ids only: nothing of the game's part of the save.
    /// </summary>
    public static class BuildStore
    {
        public const string Key = "reallife:construction";
        public const string TestKey = "reallife:construction_before_tests";
        public const int Version = 1;

        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        /// <summary>This save's state; null if it has none or it cannot be read (Core/SaveLines keeps such a line as it was read).</summary>
        public static BuildState Load(Dictionary<string, string> data, string key, out int version)
        {
            version = 0;
            if (data == null || !data.TryGetValue(key, out string text) || string.IsNullOrEmpty(text))
            {
                return null;
            }
            return SaveLines.Checked(data, key, Read(text, out version), version, Version);
        }

        public static string Write(BuildState s)
        {
            var t = new StringBuilder(1024 + s.Projects.Count * 640);
            t.Append("v=").Append(Version).Append("^h:");
            Join(t, s.Seed, s.FirstDay, s.LastDay, s.NextId, s.NextClientDay, s.NextPublicDay, s.OffersWeek, s.NextRefitDay);
            FirmFile f = s.Firm;
            t.Append("^f:");
            Join(t, f.Founded, (int)f.Licence, f.LicenceDay, f.LicenceUntil, f.LicenceReminded ? 1 : 0, f.Reputation, (int)f.Speciality, f.SpecialSince, (int)f.Cover,
                f.Finished, f.OnTime, f.Late, f.Defects, f.TendersWon, f.TendersLost);
            t.Append('|').Append(f.Earned.ToString(Inv)).Append('|').Append(f.Spent.ToString(Inv)).Append('|').Append(Esc(f.Office)).Append('|').Append(Esc(f.Yard));
            t.Append("^l:");
            for (int i = 0; i < s.Lots.Count && i < BuildCatalogue.LotsMost; i++)
            {
                LotFile l = s.Lots[i];
                t.Append(i > 0 ? ";" : string.Empty).Append(Esc(l.Id)).Append('|');
                Join(t, l.Bought, l.Zones, l.ZoneAsk, l.ZoneDay, l.TaxedTo, l.EmptySince);
                t.Append('|').Append(l.Paid.ToString(Inv));
            }
            t.Append("^c:");
            for (int i = 0; i < s.Crews.Count && i < BuildCatalogue.CrewsMost; i++)
            {
                CrewFile c = s.Crews[i];
                t.Append(i > 0 ? ";" : string.Empty);
                Join(t, c.Id, c.Gang, c.Foreman, c.Skill, c.Morale, c.Rate, c.Hired, c.Project, c.IdleDays, c.LowDays, c.StrikeUntil, c.PoachRival, c.PoachPay, c.PoachUntil,
                    c.LastWeekLooked);
            }
            t.Append("^q:");
            for (int i = 0; i < s.Plant.Count && i < 16; i++)
            {
                PlantFile q = s.Plant[i];
                t.Append(i > 0 ? ";" : string.Empty);
                Join(t, q.Id, (int)q.Kind, q.Bought, q.Price, q.Project);
            }
            t.Append("^s:");
            for (int i = 0; i < s.Staff.Count && i < BuildCatalogue.StaffMost; i++)
            {
                StaffFile e = s.Staff[i];
                t.Append(i > 0 ? ";" : string.Empty).Append(Esc(e.Employee)).Append('|');
                Join(t, (int)e.Role, e.Project);
            }
            t.Append("^p:");
            for (int i = 0, n = 0; i < s.Projects.Count && n < BuildCatalogue.ProjectsMost; i++, n++)
            {
                t.Append(n > 0 ? ";" : string.Empty);
                WriteProject(t, s.Projects[i]);
            }
            t.Append("^t:");
            for (int i = Math.Max(0, s.Tenders.Count - BuildCatalogue.TendersKept), n = 0; i < s.Tenders.Count; i++, n++)
            {
                TenderFile d = s.Tenders[i];
                t.Append(n > 0 ? ";" : string.Empty);
                Join(t, d.Id, (int)d.Source, d.SiteId, d.CivicKind, d.Number, (int)d.Use, d.Area, d.Hours, d.Opened, d.Closes, d.BidPermille, d.Fast ? 1 : 0, (int)d.State,
                    d.Winner, d.WinningDays, d.Client, d.Project);
                t.Append('|').Append(d.Value.ToString(Inv)).Append('|').Append(d.WinningPrice.ToString(Inv)).Append('|').Append(Esc(d.Street)).Append('|').Append(Esc(d.Lot));
            }
            t.Append("^r:");
            for (int i = 0; i < s.Rivals.Length; i++)
            {
                RivalFile r = s.Rivals[i];
                t.Append(i > 0 ? ";" : string.Empty);
                Join(t, r.Reputation, r.Won, r.Lost, r.Built);
            }
            t.Append("^d:");
            for (int i = 0; i < s.Developments.Count && i < 600; i++)
            {
                DevFile d = s.Developments[i];
                t.Append(i > 0 ? ";" : string.Empty).Append(Esc(d.Lot)).Append('|');
                Join(t, d.SiteId, d.Handed ? 1 : 0);
                t.Append('|').Append(Esc(d.Design)).Append('|').Append(d.Public ? '1' : '0');
            }
            t.Append("^o:");
            for (int i = 0; i < s.HiredOffers.Count && i < BuildCatalogue.OffersAWeek; i++)
            {
                t.Append(i > 0 ? "|" : string.Empty).Append(s.HiredOffers[i].ToString(Inv));
            }
            return t.ToString();
        }

        private static void WriteProject(StringBuilder t, ProjectFile p)
        {
            Join(t, p.Id, (int)p.Kind, (int)p.Stage, p.Created, p.Number, (int)p.Use, p.Version, p.Area, p.Storeys, p.Width, p.Depth, p.Height, (int)p.Architect, p.Firm,
                p.PlansDay, p.PlansDone ? 1 : 0, p.Quality, p.Error, p.Exits, p.ExitsNeeded, p.ParkingNeeded, p.ParkingUnder ? 1 : 0, p.EngineerDay, p.Engineered ? 1 : 0,
                p.EngineerStaff ? 1 : 0, (int)p.Planning, p.Applications, p.PlanningDay, p.DecideDay, p.Objections, p.Consulted ? 1 : 0, p.Reasons, p.Conditions ? 1 : 0,
                p.AppealDay, p.SiteId, p.Hours, p.StartDay, p.DueDay, p.Rework, p.OrderDay, p.Merchant, p.Orders, p.StageSeen, p.Visited ? 1 : 0, p.PaceSum, p.PaceDays,
                p.BribeDay, p.BribeAmount, p.LoanRateBp, p.LoanDraws, p.LoanFrom, p.Investor, p.InvestorDue, p.Flats, p.FlatsSold, p.FlatsOffPlan, p.Handed, p.Opening,
                p.Ribbon ? 1 : 0, p.Opened ? 1 : 0, (int)p.After, p.Ask, p.DefectDay, (int)p.Source, p.CivicKind, p.Client, p.RainHours, p.WaitHours, p.StrikeDays, p.Fails,
                p.InDistrict ? 1 : 0, p.Tender);
            for (int m = 0; m < (int)Material.Count; m++)
            {
                t.Append('|');
                Join(t, p.Have[m], p.Need[m], p.OnOrder[m]);
            }
            for (int k = 0; k < (int)Plant.Count; k++)
            {
                t.Append('|').Append(p.Plant[k].ToString(Inv));
            }
            for (int k = 0; k < (int)Inspection.Count; k++)
            {
                t.Append('|');
                Join(t, (int)p.Insp[k], p.InspDay[k], p.InspTries[k]);
            }
            long[] money = { p.Estimate, p.Spent, p.LoanAgreed, p.LoanDrawn, p.LoanOwed, p.LoanCarry, p.InvestorPut, p.InvestorOwed, p.Deposits, p.OffPlanBalance, p.Value,
                p.DefectClaim, p.ClientValue, p.ClientPaid };
            for (int k = 0; k < money.Length; k++)
            {
                t.Append('|').Append(money[k].ToString(Inv));
            }
            t.Append('|').Append(Esc(p.Lot)).Append('|').Append(Esc(p.Street)).Append('|').Append(Esc(p.District)).Append('|').Append(Esc(p.Design)).Append('|')
                .Append(Esc(p.Size)).Append('|').Append(Esc(p.ArchitectId)).Append('|');
            Join(t, p.CostPermille, p.Units, p.Contractor, p.Grade, p.WithRefit ? 1 : 0, p.ShowSince, p.ShowAgent ? 1 : 0, p.ShowInterior, p.ShowDecor, p.ShowStandard,
                p.ShowViewings, p.ShowSold, p.ShowShown);
            t.Append('|').Append(p.ContractorPrice.ToString(Inv)).Append('|').Append(p.ShowSpent.ToString(Inv)).Append('|').Append(Esc(p.ShowDesign));
        }

        /// <summary>Reads a line; null for one that is not this feature's.</summary>
        public static BuildState Read(string text, out int version)
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
                var s = new BuildState();
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
                            var r = new Fields(body);
                            s.Seed = r.I(0);
                            s.FirstDay = Math.Max(0, r.I(0));
                            s.LastDay = Math.Max(0, r.I(0));
                            s.NextId = Math.Max(1, r.I(1));
                            s.NextClientDay = Math.Max(0, r.I(0));
                            s.NextPublicDay = Math.Max(0, r.I(0));
                            s.OffersWeek = r.I(-1);
                            s.NextRefitDay = Math.Max(0, r.I(0));
                            header = true;
                            break;
                        }
                        case 'f':
                            ReadFirm(new Fields(body), s.Firm);
                            break;
                        case 'l':
                            foreach (string rec in Records(body))
                            {
                                if (s.Lots.Count >= BuildCatalogue.LotsMost)
                                {
                                    break;
                                }
                                var r = new Fields(rec);
                                var l = new LotFile { Id = r.S() };
                                l.Bought = r.I(0);
                                l.Zones = r.I(0) & 15;
                                l.ZoneAsk = r.I(0) & 15;
                                l.ZoneDay = r.I(0);
                                l.TaxedTo = r.I(0);
                                l.EmptySince = r.I(0);
                                l.Paid = Math.Max(0L, r.L(0L));
                                if (l.Id.Length > 0)
                                {
                                    s.Lots.Add(l);
                                }
                            }
                            break;
                        case 'c':
                            foreach (string rec in Records(body))
                            {
                                if (s.Crews.Count >= BuildCatalogue.CrewsMost)
                                {
                                    break;
                                }
                                var r = new Fields(rec);
                                var c = new CrewFile
                                {
                                    Id = r.I(0), Gang = BuildCatalogue.Clamp(r.I(0), 0, BuildCatalogue.GangCount - 1), Foreman = BuildCatalogue.Clamp(r.I(0), 0, BuildCatalogue.ForemanKeys.Length - 1),
                                    Skill = BuildCatalogue.Clamp(r.I(50), 0, 100), Morale = BuildCatalogue.Clamp(r.I(BuildCatalogue.MoraleStart), 0, 100), Rate = Math.Max(0, r.I(0)),
                                    Hired = r.I(0), Project = Math.Max(0, r.I(0)), IdleDays = Math.Max(0, r.I(0)), LowDays = Math.Max(0, r.I(0)), StrikeUntil = r.I(0),
                                    PoachRival = BuildCatalogue.Clamp(r.I(-1), -1, BuildCatalogue.RivalCount - 1), PoachPay = Math.Max(0, r.I(0)), PoachUntil = r.I(0),
                                    LastWeekLooked = r.I(-1)
                                };
                                if (c.Id > 0)
                                {
                                    s.Crews.Add(c);
                                }
                            }
                            break;
                        case 'q':
                            foreach (string rec in Records(body))
                            {
                                if (s.Plant.Count >= 16)
                                {
                                    break;
                                }
                                var r = new Fields(rec);
                                var q = new PlantFile
                                {
                                    Id = r.I(0), Kind = (Plant)BuildCatalogue.Clamp(r.I(0), 0, (int)Plant.Count - 1), Bought = r.I(0), Price = Math.Max(0, r.I(0)), Project = Math.Max(0, r.I(0))
                                };
                                if (q.Id > 0)
                                {
                                    s.Plant.Add(q);
                                }
                            }
                            break;
                        case 's':
                            foreach (string rec in Records(body))
                            {
                                if (s.Staff.Count >= BuildCatalogue.StaffMost)
                                {
                                    break;
                                }
                                var r = new Fields(rec);
                                var e = new StaffFile { Employee = r.S() };
                                e.Role = (StaffRole)BuildCatalogue.Clamp(r.I(0), 0, (int)StaffRole.Count - 1);
                                e.Project = Math.Max(0, r.I(0));
                                if (e.Employee.Length > 0)
                                {
                                    s.Staff.Add(e);
                                }
                            }
                            break;
                        case 'p':
                            foreach (string rec in Records(body))
                            {
                                if (s.Projects.Count >= BuildCatalogue.ProjectsMost)
                                {
                                    break;
                                }
                                ProjectFile pr = ReadProject(new Fields(rec));
                                if (pr != null)
                                {
                                    s.Projects.Add(pr);
                                }
                            }
                            break;
                        case 't':
                            foreach (string rec in Records(body))
                            {
                                if (s.Tenders.Count >= BuildCatalogue.TendersKept)
                                {
                                    break;
                                }
                                var r = new Fields(rec);
                                var d = new TenderFile
                                {
                                    Id = r.I(0), Source = (TenderSource)BuildCatalogue.Clamp(r.I(0), 0, 3), SiteId = r.I(0), CivicKind = BuildCatalogue.Clamp(r.I(0), 0, 5), Number = r.I(0),
                                    Use = (BuildUse)BuildCatalogue.Clamp(r.I(0), 0, (int)BuildUse.Count - 1), Area = Math.Max(0, r.I(0)), Hours = Math.Max(1, r.I(1)), Opened = r.I(0),
                                    Closes = r.I(0), BidPermille = Math.Max(0, r.I(0)), Fast = r.I(0) == 1, State = (TenderState)BuildCatalogue.Clamp(r.I(0), 0, 3),
                                    Winner = BuildCatalogue.Clamp(r.I(-2), -2, BuildCatalogue.RivalCount - 1), WinningDays = Math.Max(0, r.I(0)), Client = Math.Max(0, r.I(0)),
                                    Project = Math.Max(0, r.I(0))
                                };
                                d.Value = Math.Max(0L, r.L(0L));
                                d.WinningPrice = Math.Max(0L, r.L(0L));
                                d.Street = r.S();
                                d.Lot = r.S();
                                if (d.Id > 0)
                                {
                                    s.Tenders.Add(d);
                                }
                            }
                            break;
                        case 'r':
                        {
                            int i = 0;
                            foreach (string rec in Records(body))
                            {
                                if (i >= s.Rivals.Length)
                                {
                                    break;
                                }
                                var r = new Fields(rec);
                                RivalFile rival = s.Rivals[i++];
                                rival.Reputation = BuildCatalogue.Clamp(r.I(rival.Reputation), 0, 100);
                                rival.Won = Math.Max(0, r.I(0));
                                rival.Lost = Math.Max(0, r.I(0));
                                rival.Built = Math.Max(0, r.I(0));
                            }
                            break;
                        }
                        case 'd':
                            foreach (string rec in Records(body))
                            {
                                if (s.Developments.Count >= 600)
                                {
                                    break;
                                }
                                var r = new Fields(rec);
                                var d = new DevFile { Lot = r.S() };
                                d.SiteId = Math.Max(0, r.I(0));
                                d.Handed = r.I(0) == 1;
                                d.Design = r.S();
                                d.Public = r.I(0) == 1;
                                if (d.Lot.Length > 0)
                                {
                                    s.Developments.Add(d);
                                }
                            }
                            break;
                        case 'o':
                        {
                            var r = new Fields(body);
                            for (int i = 0; i < BuildCatalogue.OffersAWeek && r.More; i++)
                            {
                                int slot = r.I(-1);
                                if (slot >= 0 && slot < BuildCatalogue.OffersAWeek && !s.HiredOffers.Contains(slot))
                                {
                                    s.HiredOffers.Add(slot);
                                }
                            }
                            break;
                        }
                    }
                }
                return header ? s : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static void ReadFirm(Fields r, FirmFile f)
        {
            f.Founded = Math.Max(0, r.I(0));
            f.Licence = (LicenceState)BuildCatalogue.Clamp(r.I(0), 0, 4);
            f.LicenceDay = r.I(0);
            f.LicenceUntil = r.I(0);
            f.LicenceReminded = r.I(0) == 1;
            f.Reputation = BuildCatalogue.Clamp(r.I(BuildCatalogue.ReputationStart), 0, 100);
            f.Speciality = (Speciality)BuildCatalogue.Clamp(r.I(0), 0, (int)Speciality.Count - 1);
            f.SpecialSince = r.I(0);
            f.Cover = (CoverLevel)BuildCatalogue.Clamp(r.I(0), 0, 2);
            f.Finished = Math.Max(0, r.I(0));
            f.OnTime = Math.Max(0, r.I(0));
            f.Late = Math.Max(0, r.I(0));
            f.Defects = Math.Max(0, r.I(0));
            f.TendersWon = Math.Max(0, r.I(0));
            f.TendersLost = Math.Max(0, r.I(0));
            f.Earned = Math.Max(0L, r.L(0L));
            f.Spent = Math.Max(0L, r.L(0L));
            f.Office = r.S();
            f.Yard = r.S();
        }

        private static ProjectFile ReadProject(Fields r)
        {
            var p = new ProjectFile
            {
                Id = r.I(0), Kind = (ProjectKind)BuildCatalogue.Clamp(r.I(0), 0, 3), Stage = (ProjectStage)BuildCatalogue.Clamp(r.I(0), 0, 5), Created = r.I(0), Number = r.I(0),
                Use = (BuildUse)BuildCatalogue.Clamp(r.I(0), 0, (int)BuildUse.Count - 1), Version = r.I(1), Area = Math.Max(0, r.I(0)), Storeys = BuildCatalogue.Clamp(r.I(1), 1, 60),
                Width = Math.Max(0, r.I(0)), Depth = Math.Max(0, r.I(0)), Height = Math.Max(0, r.I(0)), Architect = (ArchitectKind)BuildCatalogue.Clamp(r.I(0), 0, 2),
                Firm = BuildCatalogue.Clamp(r.I(0), 0, BuildCatalogue.FirmCount - 1), PlansDay = r.I(0), PlansDone = r.I(0) == 1, Quality = BuildCatalogue.Clamp(r.I(0), 0, 100),
                Error = BuildCatalogue.Clamp(r.I(0), -500, 500), Exits = Math.Max(0, r.I(0)), ExitsNeeded = Math.Max(0, r.I(0)), ParkingNeeded = Math.Max(0, r.I(0)),
                ParkingUnder = r.I(0) == 1, EngineerDay = r.I(0), Engineered = r.I(0) == 1, EngineerStaff = r.I(0) == 1,
                Planning = (PlanningState)BuildCatalogue.Clamp(r.I(0), 0, 5), Applications = Math.Max(0, r.I(0)), PlanningDay = r.I(0), DecideDay = r.I(0),
                Objections = BuildCatalogue.Clamp(r.I(0), 0, BuildCatalogue.ObjectionsMost), Consulted = r.I(0) == 1, Reasons = r.I(0) & 127, Conditions = r.I(0) == 1,
                AppealDay = r.I(0), SiteId = Math.Max(0, r.I(0)), Hours = BuildCatalogue.Clamp(r.I(BuildCatalogue.HoursLeast), 1, 2000), StartDay = r.I(0), DueDay = r.I(0),
                Rework = Math.Max(0, r.I(0)), OrderDay = r.I(0), Merchant = BuildCatalogue.Clamp(r.I(0), 0, BuildCatalogue.MerchantCount - 1), Orders = Math.Max(0, r.I(0)),
                StageSeen = BuildCatalogue.Clamp(r.I(-1), -1, (int)WorkStage.Count - 1), Visited = r.I(0) == 1, PaceSum = Math.Max(0, r.I(0)), PaceDays = Math.Max(0, r.I(0)),
                BribeDay = r.I(0), BribeAmount = Math.Max(0, r.I(0)), LoanRateBp = Math.Max(0, r.I(0)), LoanDraws = r.I(0) & 15, LoanFrom = r.I(0),
                Investor = BuildCatalogue.Clamp(r.I(-1), -1, BuildCatalogue.InvestorCount - 1), InvestorDue = r.I(0), Flats = Math.Max(0, r.I(0)), FlatsSold = Math.Max(0, r.I(0)),
                FlatsOffPlan = Math.Max(0, r.I(0)), Handed = r.I(0), Opening = r.I(0), Ribbon = r.I(0) == 1, Opened = r.I(0) == 1,
                After = (AfterUse)BuildCatalogue.Clamp(r.I(0), 0, 1), Ask = BuildCatalogue.Clamp(r.I(1), 0, BuildCatalogue.AskPermille.Length - 1), DefectDay = r.I(0),
                Source = (TenderSource)BuildCatalogue.Clamp(r.I(0), 0, 3), CivicKind = BuildCatalogue.Clamp(r.I(-1), -1, 5), Client = Math.Max(0, r.I(0)),
                RainHours = Math.Max(0, r.I(0)), WaitHours = Math.Max(0, r.I(0)), StrikeDays = Math.Max(0, r.I(0)), Fails = Math.Max(0, r.I(0)), InDistrict = r.I(0) == 1,
                Tender = Math.Max(0, r.I(0))
            };
            for (int m = 0; m < (int)Material.Count; m++)
            {
                p.Have[m] = Math.Max(0, r.I(0));
                p.Need[m] = Math.Max(0, r.I(0));
                p.OnOrder[m] = Math.Max(0, r.I(0));
            }
            for (int k = 0; k < (int)Plant.Count; k++)
            {
                p.Plant[k] = Math.Max(-1, r.I(-1));
            }
            for (int k = 0; k < (int)Inspection.Count; k++)
            {
                p.Insp[k] = (InspectionState)BuildCatalogue.Clamp(r.I(0), 0, 4);
                p.InspDay[k] = r.I(0);
                p.InspTries[k] = Math.Max(0, r.I(0));
            }
            p.Estimate = Math.Max(0L, r.L(0L));
            p.Spent = Math.Max(0L, r.L(0L));
            p.LoanAgreed = Math.Max(0L, r.L(0L));
            p.LoanDrawn = Math.Max(0L, r.L(0L));
            p.LoanOwed = Math.Max(0L, r.L(0L));
            p.LoanCarry = Math.Max(0L, Math.Min(99L, r.L(0L)));   // hundredths: under a whole unit
            p.InvestorPut = Math.Max(0L, r.L(0L));
            p.InvestorOwed = Math.Max(0L, r.L(0L));
            p.Deposits = Math.Max(0L, r.L(0L));
            p.OffPlanBalance = Math.Max(0L, r.L(0L));
            p.Value = Math.Max(0L, r.L(0L));
            p.DefectClaim = Math.Max(0L, r.L(0L));
            p.ClientValue = Math.Max(0L, r.L(0L));
            p.ClientPaid = Math.Max(0L, r.L(0L));
            p.Lot = r.S();
            p.Street = r.S();
            p.District = r.S();
            p.Design = r.S();
            p.Size = r.S();
            p.ArchitectId = r.S();
            p.CostPermille = BuildCatalogue.Clamp(r.I(1000), BuildCatalogue.DesignCostLeast, BuildCatalogue.DesignCostMost);
            p.Units = BuildCatalogue.Clamp(r.I(0), 0, 2000);
            p.Contractor = BuildCatalogue.Clamp(r.I(-1), -1, BuildCatalogue.RivalCount - 1);
            p.Grade = BuildCatalogue.Clamp(r.I(-1), -1, BuildCatalogue.RefitGradeFrom.Length - 1);
            p.WithRefit = r.I(0) == 1;
            p.ShowSince = Math.Max(0, r.I(0));
            p.ShowAgent = r.I(0) == 1;
            p.ShowInterior = BuildCatalogue.Clamp(r.I(0), 0, 100);
            p.ShowDecor = BuildCatalogue.Clamp(r.I(0), 0, 100);
            p.ShowStandard = BuildCatalogue.Clamp(r.I(0), 0, 100);
            p.ShowViewings = Math.Max(0, r.I(0));
            p.ShowSold = Math.Max(0, r.I(0));
            p.ShowShown = Math.Max(0, r.I(0));
            p.ContractorPrice = Math.Max(0L, r.L(0L));
            p.ShowSpent = Math.Max(0L, r.L(0L));
            p.ShowDesign = r.S();
            if (p.ShowDesign.Length == 0)
            {
                p.ShowSince = 0;   // no design, no show flat
            }
            if (p.FlatsSold > p.Flats)
            {
                p.FlatsSold = p.Flats;
            }
            return p.Id > 0 ? p : null;
        }

        private static IEnumerable<string> Records(string body)
        {
            if (string.IsNullOrEmpty(body))
            {
                yield break;
            }
            foreach (string rec in body.Split(';'))
            {
                if (rec.Length > 0)
                {
                    yield return rec;
                }
            }
        }

        private static void Join(StringBuilder t, params int[] values)
        {
            for (int i = 0; i < values.Length; i++)
            {
                if (i > 0)
                {
                    t.Append('|');
                }
                t.Append(values[i].ToString(Inv));
            }
        }

        /// <summary>A text with every character a divider could be escaped.</summary>
        public static string Esc(string text)
        {
            return string.IsNullOrEmpty(text) ? string.Empty : Uri.EscapeDataString(text);
        }

        /// <summary>One record's fields, read in order; a field that is missing or not a number reads as the default given.</summary>
        private sealed class Fields
        {
            private readonly string[] _f;
            private int _next;

            public Fields(string record)
            {
                _f = (record ?? string.Empty).Split('|');
            }

            public bool More => _next < _f.Length;

            public int I(int otherwise)
            {
                return _next < _f.Length && int.TryParse(_f[_next++], NumberStyles.Integer, Inv, out int v) ? v : otherwise;
            }

            public long L(long otherwise)
            {
                return _next < _f.Length && long.TryParse(_f[_next++], NumberStyles.Integer, Inv, out long v) ? v : otherwise;
            }

            public string S()
            {
                if (_next >= _f.Length)
                {
                    return string.Empty;
                }
                string raw = _f[_next++];
                try
                {
                    return raw.Length == 0 ? string.Empty : Uri.UnescapeDataString(raw);
                }
                catch (Exception)
                {
                    return string.Empty;
                }
            }
        }
    }
}
