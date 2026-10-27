using System;
using System.Collections.Generic;
using System.Globalization;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.Tech
{
    /// <summary>One tech firm with what was read from the game for it today. Nothing of this is saved.</summary>
    public sealed class FirmPlace
    {
        public Firm File;
        public BuildingRegistration Reg;
        public Crew Crew;
        public int Spare = 1000;      // thousandths of its staff hours free for the sector's work (all but at a game office)
        public int People;
        public int Hours;             // the hours its people worked yesterday
        public long Today;            // takings booked today, cents
        public int Mbps = NetSpeedRules.Unknown;   // the office's plan speed read for the day's work (TechBridges.PlanMbps)
        public int Net = 1000;        // thousandths of the day's work points its connection gives (FirmsRules.Connection)
    }

    /// <summary>The firms part: each of the player's tech businesses is a firm, worked through each game day after the people's.</summary>
    public sealed partial class TechDirector
    {
        public const string TechAppId = "RealLifeTechSector";
        public const int TabFirms = 0, TabProducts = 1, TabContracts = 2;
        private const int QuarterDays = 90;   // days between research and development reliefs

        private readonly List<FirmPlace> _firms = new List<FirmPlace>(8);
        private readonly List<BuildingRegistration> _firmRegs = new List<BuildingRegistration>(8);
        private readonly List<KeyValuePair<string, string>> _clients = new List<KeyValuePair<string, string>>(16);
        private readonly List<Crew> _crews = new List<Crew>(8);
        private readonly List<WorkNote> _work = new List<WorkNote>(8);
        private readonly List<int> _taken = new List<int>(2), _behind = new List<int>(4);   // contracts to tell of after the day's notes
        private FirmsFile _firmsFile;
        private bool _firmsOn;

        /// <summary>The firms' file; null while the part is off.</summary>
        public FirmsFile Firms => _firmsOn ? _firmsFile : null;

        public IReadOnlyList<FirmPlace> FirmPlaces => _firms;

        private void StartFirms()
        {
            GameInstance save = Save;
            FirmsFile file = FirmsStore.Load(save.modData, out int version);
            bool fresh = file == null;
            if (fresh)
            {
                file = new FirmsFile { Seed = new System.Random().Next(1, int.MaxValue), FirstDay = save.Day, LastTender = save.Day };
            }
            _firmsFile = file;
            _firmsOn = true;
            CatchUpMoves(MoveRules.PartFirms);
            ReadFirms(save.Day);
            StartInfra();
            Logger?.Info("Tech: firms " + (fresh ? "opened for this save" : "loaded (version " + version + ")") + " on day " + save.Day + ": " + _firms.Count + " firm(s), " + _firmsFile.Products.Count
                         + " product(s), " + _firmsFile.Contracts.Count + " contract(s).");
        }

        private void StopFirms(bool keep, bool unloading)
        {
            if (!_firmsOn)
            {
                return;
            }
            if (keep && Save != null && _firmsFile != null)
            {
                WriteFirms(Save.modData);
                WriteInfra(Save.modData);
            }
            _firmsOn = false;
            _firms.Clear();
            if (!unloading && _techApp != null)
            {
                _techApp.Remove();
                _techApp = null;
                _techView = null;
            }
        }

        private void WriteFirms(Dictionary<string, string> data)
        {
            if (_firmsFile != null && data != null)
            {
                data[FirmsStore.Key] = FirmsStore.Write(_firmsFile);
            }
        }

        /// <summary>
        /// Ties each firm to its business: a new business is a new firm, and one no longer the player's is closed with its work.
        /// A business the moving company moved is followed first (FollowMoves), so its firm is not taken for one that closed.
        /// </summary>
        private void ReadFirms(int day)
        {
            FollowMoves();
            FirmsGame.Firms(_firmRegs);
            _firms.Clear();
            for (int i = 0; i < _firmsFile.Firms.Count; i++)
            {
                _firmsFile.Firms[i].Seen = false;
            }
            for (int i = 0; i < _firmRegs.Count; i++)
            {
                BuildingRegistration reg = _firmRegs[i];
                string place = TalentGame.Place(reg);
                Firm f = _firmsFile.FirmAt(place);
                int kind = FirmsGame.KindOf(reg.businessTypeName);
                if (f == null)
                {
                    f = new Firm { Place = place, Kind = kind, Founded = day };
                    _firmsFile.Firms.Add(f);
                }
                f.Kind = kind;
                f.Seen = true;
                f.Name = TalentGame.NameOf(reg);
                _firms.Add(new FirmPlace { File = f, Reg = reg });
            }
            for (int i = _firmsFile.Firms.Count - 1; i >= 0; i--)
            {
                Firm f = _firmsFile.Firms[i];
                if (f.Seen)
                {
                    continue;
                }
                int products = FirmsRules.Close(_firmsFile, f, day);
                Logger?.Info("Tech: the firm at " + Where(f.Place) + " has no business of the player's there any more and no move took it elsewhere; closed with " + products + " product(s).");
            }
        }

        internal FirmPlace FirmOf(string place)
        {
            for (int i = 0; i < _firms.Count; i++)
            {
                if (_firms[i].File.Place == place)
                {
                    return _firms[i];
                }
            }
            return null;
        }

        /// <summary>What each firm's people can do on the day, counted from their files.</summary>
        private void Crews()
        {
            for (int f = 0; f < _firms.Count; f++)
            {
                FirmPlace firm = _firms[f];
                var c = new Crew();
                int security = FirmsCatalogue.SecurityBase, reliability = FirmsCatalogue.ReliabilityBase, seniors = 0, hours = 0;
                long faultWeighted = 0;
                firm.People = 0;
                for (int i = 0; i < _people.Count; i++)
                {
                    Person person = _people[i];
                    Profile p = person.File;
                    if (p.Place != firm.File.Place)
                    {
                        continue;
                    }
                    firm.People++;
                    c.Seats++;
                    int h = person.HoursYesterday;
                    hours += h;
                    int points = TalentRules.Output(p, person.MainPoints, Save.Day) * FirmsCatalogue.PointsPerDay * h / TalentCatalogue.DayHours / 1000;
                    RoleDef r = p.Def;
                    switch (r.Family)
                    {
                        case Family.Engineering:
                        case Family.Data:
                        case Family.Security:
                        case Family.Infrastructure:
                            if (r.Id == TalentCatalogue.QaTester)
                            {
                                c.Testing += points;
                            }
                            else
                            {
                                c.Engineering += points;
                                seniors += p.Level >= TalentCatalogue.Senior ? points : 0;
                                faultWeighted += (long)TalentRules.Faults(p, person.MainPoints) * Math.Max(1, points);
                            }
                            c.Wages += (long)p.GameWage * h;
                            break;
                        case Family.Design:
                            c.Design += points;
                            c.Web += r.Id == TalentCatalogue.WebDesigner ? points : 0;
                            c.Wages += (long)p.GameWage * h;
                            break;
                        case Family.Product:
                            c.Product += points;
                            c.Wages += (long)p.GameWage * h;
                            break;
                        case Family.Commercial:
                            c.Selling += points;
                            break;
                        case Family.Support:
                            c.Support += h > 0 ? 1 : 0;
                            break;
                    }
                    if (r.Family == Family.Security && h > 0)
                    {
                        security += r.Id == TalentCatalogue.DataProtection ? FirmsCatalogue.SecurityOfficer : Math.Min(FirmsCatalogue.SecurityEach, p.A / 1000 * FirmsCatalogue.SecurityPerPoint);
                        security += p.Has(2) || p.Has(9) ? FirmsCatalogue.SecurityCert : 0;   // security practitioner, privacy professional
                    }
                    if ((r.Id == TalentCatalogue.Sre || r.Id == TalentCatalogue.DevOps) && h > 0)
                    {
                        reliability += Math.Min(FirmsCatalogue.ReliabilityEach, (p.A + p.B) / 2000 * FirmsCatalogue.ReliabilityPerPoint);
                    }
                }
                c.Security = Math.Min(FirmsCatalogue.SecurityMost, security);
                c.Reliability = Math.Min(FirmsCatalogue.ReliabilityMost, reliability);
                c.SeniorShare = c.Engineering > 0 ? seniors * 1000 / c.Engineering : 0;
                c.Faults = c.Engineering > 0 ? (int)(faultWeighted / Math.Max(1, c.Engineering)) : 150;
                firm.Hours = hours;
                firm.Spare = FirmsGame.Spare(firm.Reg, hours);
                if (firm.Spare < 1000)
                {
                    c.Engineering = c.Engineering * firm.Spare / 1000;
                    c.Design = c.Design * firm.Spare / 1000;
                    c.Web = c.Web * firm.Spare / 1000;
                    c.Testing = c.Testing * firm.Spare / 1000;
                }
                // read once with the day's crew, so the page and the day's work go by the same plan
                firm.Mbps = TechBridges.PlanMbpsAt(firm.Reg);
                firm.Net = FirmsRules.Connection(firm.File.Kind, firm.Mbps);
                FirmsRules.Connected(ref c, firm.Net);
                firm.Crew = c;
            }
        }

        private void FirmsDay(int day)
        {
            if (_firmsFile.LastDay >= day)
            {
                return;
            }
            float started = Time.realtimeSinceStartup;
            ReadFirms(day);
            Crews();
            _taken.Clear();
            _behind.Clear();
            int cycle = HiringGame.Cycle;
            _crews.Clear();
            for (int i = 0; i < _firmsFile.Firms.Count; i++)
            {
                FirmPlace place = FirmOf(_firmsFile.Firms[i].Place);
                _crews.Add(place != null ? place.Crew : default(Crew));
            }
            FirmsGame.Clients(_clients);
            _work.Clear();
            FirmsRules.Offers(_firmsFile, day, _crews, _clients, _work);
            FirmsRules.Lapse(_firmsFile, day, _work);
            for (int i = 0; i < _firmsFile.Contracts.Count; i++)
            {
                Contract t = _firmsFile.Contracts[i];
                if (t.Stage == (int)WorkStage.Bid && day >= t.End)
                {
                    FirmPlace bidder = FirmOf(t.Place);
                    if (bidder != null && FirmsRules.Decide(_firmsFile, t, bidder.File, day, TechBridges.OfficeBidAt(t.Place)))
                    {
                        long upfront = FirmsRules.Take(t, t.Place, day);
                        bidder.Today += upfront * Prices.Level / 1000;
                        _work.Add(new WorkNote { Contract = t.Id, Kind = FirmsRules.NoteWon });
                        Story("reallife_tech_news_tender", "firm", bidder.File.Name);
                        _taken.Add(t.Id);   // told after the day's notes if nobody can work on it
                    }
                    else
                    {
                        t.Stage = (int)WorkStage.Lost;
                        _work.Add(new WorkNote { Contract = t.Id, Kind = FirmsRules.NoteLostTender });
                    }
                }
            }
            int level = Prices.Level, outages = 0, breaches = 0;
            for (int f = 0; f < _firms.Count; f++)
            {
                FirmPlace firm = _firms[f];
                Firm file = firm.File;
                Crew c = firm.Crew;
                int engineering = c.Engineering, design = c.Design, web = c.Web;
                for (int i = 0; i < _firmsFile.Contracts.Count; i++)
                {
                    Contract w = _firmsFile.Contracts[i];
                    if (w.Place != file.Place || w.Stage != (int)WorkStage.Active)
                    {
                        continue;
                    }
                    int need = FirmsRules.Need(w, day), give = FirmsRules.Give(w.K, need, ref engineering, ref design, ref web);
                    if (give > 0)
                    {
                        w.Told &= ~Contract.ToldBehind;
                    }
                    else if (need > 0 && day > w.Start + 1 && (w.Told & Contract.ToldBehind) == 0)
                    {
                        _behind.Add(w.Id);   // a whole day with nothing done: told after the day's notes if nobody worked
                    }
                    long paid = FirmsRules.Work(w, file, give, day, _work);
                    if (paid > 0)
                    {
                        Paid(firm, w, paid * level / 1000);
                    }
                }
                int shares = 0;
                for (int i = 0; i < _firmsFile.Products.Count; i++)
                {
                    Product p = _firmsFile.Products[i];
                    shares += p.Place == file.Place && p.Stage < 2 ? Math.Max(0, p.Share) : 0;
                }
                int quality = 0, rated = 0;
                long hosting = 0, marketing = 0;
                long rndPoints = 0;
                for (int i = 0; i < _firmsFile.Products.Count; i++)
                {
                    Product p = _firmsFile.Products[i];
                    if (p.Place != file.Place || p.Stage >= 2)
                    {
                        continue;
                    }
                    int points = shares > 0 ? (int)((long)engineering * Math.Max(0, p.Share) / shares) : 0;
                    int testing = shares > 0 ? (int)((long)c.Testing * Math.Max(0, p.Share) / shares) : 0;
                    rndPoints += points;
                    int before = p.Features;
                    FirmsRules.Build(file, p, c, points, testing);
                    FirmsRules.Hype(p);
                    if (p.Live && p.Features > before)
                    {
                        p.LastRelease = day;
                        if (p.Features > p.RivalFeatures && before <= p.RivalFeatures)
                        {
                            TechBridges.Stock?.Invoke(FirmsCatalogue.RivalStock(p.K, p.S), -40);
                            Story("reallife_tech_news_overtakes", "product", p.Name, "rival", FirmsCatalogue.RivalName(FirmsCatalogue.RivalStock(p.K, p.S)));
                        }
                    }
                    if (!p.Live)
                    {
                        marketing += p.K == ProductKind.Game ? FirmsCatalogue.MarketingDay[TalentCatalogue.Clamp(p.Marketing, 0, 4)] : 0;
                        continue;
                    }
                    ProductDay r = FirmsRules.Sell(_firmsFile, file, p, c, c.Support, day, cycle, HostingRate());
                    firm.Today += r.Takings * level / 1000;
                    hosting += r.Hosting * level / 1000;
                    marketing += r.Marketing;
                    quality += FirmsRules.Quality(p);
                    rated++;
                    if (r.Outage)
                    {
                        outages++;
                        TechBridges.Fame?.Invoke(0, -1);
                        TellTech(NotificationType.Warning, "reallife_tech_note_outage", Data("product", p.Name, "lost", r.Lost.ToString()), TabProducts, TechApp.ProductRow + p.Id);
                        if (p.Users + p.Free >= 500)
                        {
                            Story("reallife_tech_news_outage", "product", p.Name, "firm", file.Name);
                        }
                    }
                    if (r.Breach)
                    {
                        breaches++;
                        Breach(firm, p, r, day);
                    }
                    if (r.RivalShipped)
                    {
                        TechBridges.Stock?.Invoke(FirmsCatalogue.RivalStock(p.K, p.S), 20);
                        if (p.RivalFeatures == p.Features + 1)
                        {
                            TellTech(NotificationType.Info, "reallife_tech_note_rival", Data("product", p.Name, "rival", FirmsCatalogue.RivalName(FirmsCatalogue.RivalStock(p.K, p.S))),
                                TabProducts, TechApp.ProductRow + p.Id);
                        }
                    }
                }
                if (rated > 0)
                {
                    FirmsRules.Reputation(file, quality / rated);
                }
                // the relief counts the share of the qualifying pay that went to products
                if (c.Engineering > 0 && rndPoints > 0)
                {
                    file.RnD += c.Wages * Math.Min(c.Engineering, rndPoints) / Math.Max(1, c.Engineering);
                }
                if (day - file.ReliefDay >= QuarterDays && file.RnD > 0)
                {
                    long relief = FirmsRules.Relief(file.RnD);
                    FirmsGame.Relief(relief);
                    TellTech(NotificationType.Success, "reallife_tech_note_relief", Data("firm", file.Name, "amount", TechText.Cents(relief)), TabFirms, TechApp.FirmRow + file.Place);
                    file.RnD = 0;
                    file.ReliefDay = day;
                }
                else if (file.ReliefDay == 0)
                {
                    file.ReliefDay = day;
                }
                int dated = day - 1;   // the day's money is booked on the day that ended
                FirmsGame.Takings(firm.Today, firm.Reg, file.Name, dated);
                FirmsGame.Cost(hosting, FirmsGame.HostingKey, firm.Reg, file.Name, dated);
                if (HostingRate() < 1000)
                {
                    _ownHosting += hosting;   // the player's own cloud's takings, booked on the infrastructure's day
                }
                FirmsGame.Cost((long)c.Seats * FirmsCatalogue.SoftwarePerSeat * level / 1000, FirmsGame.SoftwareKey, firm.Reg, file.Name, dated);
                FirmsGame.Cost(marketing * level / 1000, FirmsGame.MarketingKey, firm.Reg, file.Name, dated);
                FirmsRules.Roll(file, day, firm.Today);
                firm.Today = 0;
            }
            for (int i = 0; i < _work.Count; i++)
            {
                Worked(_work[i]);
            }
            for (int i = 0; i < _taken.Count; i++)
            {
                TellIfNobody(_firmsFile.ContractOf(_taken[i]));
            }
            for (int i = 0; i < _behind.Count; i++)
            {
                TellBehind(_firmsFile.ContractOf(_behind[i]));
            }
            _taken.Clear();
            _behind.Clear();
            _firmsFile.LastDay = day;
            TechBridges.Books?.Invoke(day - 1);
            if (Verbose)
            {
                Logger?.Info("Tech check (firms, day " + day + "): " + _firms.Count + " firm(s), " + _firmsFile.Products.Count + " product(s), " + _firmsFile.Contracts.Count
                             + " contract(s), " + _work.Count + " contract event(s), " + outages + " outage(s), " + breaches + " breach(es); "
                             + ((Time.realtimeSinceStartup - started) * 1000f).ToString("0.0", CultureInfo.InvariantCulture) + " ms: PASS");
            }
        }

        // Hosting, thousandths of the list price: the rival clouds' price until the player runs a cloud of their own.
        private int HostingRate()
        {
            return Save != null && OwnCloud(Save.Day) ? InfraCatalogue.OwnHostingShare : 1000;
        }

        // A contract has paid the firm; a client of the player's own pays it from that business.
        private void Paid(FirmPlace firm, Contract w, long cents)
        {
            firm.Today += cents;
            ChargeClient(w, firm, cents, Save.Day - 1);
        }

        private static void ChargeClient(Contract w, FirmPlace firm, long cents, int? day)
        {
            if (!string.IsNullOrEmpty(w.OwnPlace))
            {
                BuildingRegistration client = TalentGame.Find(w.OwnPlace);
                if (client != null)
                {
                    FirmsGame.Cost(cents, FirmsGame.ServicesKey, client, firm.File.Name, day);
                }
            }
        }

        // A breach: the fine, court for a firm off the register or caught twice, the insurer told, Standing and the paper.
        private void Breach(FirmPlace firm, Product p, ProductDay r, int day)
        {
            Firm file = firm.File;
            FirmsGame.Fine(r.Fine * Prices.Level / 1000, firm.Reg, file.Name);
            TechBridges.Claim?.Invoke(firm.Reg, (int)Math.Min(int.MaxValue, r.Fine / 100));
            TechBridges.Fame?.Invoke(3, -2);
            bool court = day >= file.RegisteredUntil || file.Breaches >= 2;
            int number = court && TechBridges.Prosecute != null ? TechBridges.Prosecute(file.Name) : 0;
            TellTech(NotificationType.Error, number > 0 ? "reallife_tech_note_breach_court" : "reallife_tech_note_breach",
                Data("product", p.Name, "affected", TechText.Whole(r.Affected), "fine", TechText.Cents(r.Fine * Prices.Level / 1000)), TabFirms, TechApp.FirmRow + file.Place);
            Story("reallife_tech_news_breach", "firm", file.Name, "count", TechText.Whole(r.Affected));
        }

        private void Worked(WorkNote note)
        {
            Contract w = _firmsFile.ContractOf(note.Contract);
            if (w == null)
            {
                return;
            }
            string row = TechApp.ContractRow + w.Id;
            switch (note.Kind)
            {
                case FirmsRules.NoteOffered:   // an offer and a tender are on the Contracts tab either way; their notifications can be switched off
                    if (Settings.TechOfferNotes)
                    {
                        TellTech(NotificationType.Info, "reallife_tech_note_offer",
                            Data("client", w.Client, "work", TechText.Work((WorkKind)w.Kind), "value", TechText.Cents(w.Value * Prices.Level / 1000)), TabContracts, row);
                    }
                    break;
                case FirmsRules.NoteTender:
                    if (Settings.TechOfferNotes)
                    {
                        TellTech(NotificationType.Info, "reallife_tech_note_tender", Data("value", TechText.Cents(w.Value * Prices.Level / 1000), "day", w.End.ToString()), TabContracts, row);
                    }
                    break;
                case FirmsRules.NoteWon:
                    TellTech(NotificationType.Success, "reallife_tech_note_tender_won", Data("value", TechText.Cents(w.Value)), TabContracts, row);
                    break;
                case FirmsRules.NoteLostTender:
                    TellTech(NotificationType.Info, "reallife_tech_note_tender_lost", Data("client", w.Client), TabContracts, row);
                    break;
                case FirmsRules.NoteDone:
                    TellTech(NotificationType.Success, "reallife_tech_note_done", Data("client", w.Client, "amount", TechText.Cents(note.Amount)), TabContracts, row);
                    break;
                case FirmsRules.NoteFailed:
                    TellTech(NotificationType.Warning, "reallife_tech_note_failed", Data("client", w.Client), TabContracts, row);
                    break;
            }
        }

        // A contract just taken on that nobody at the firm can work on: told once, with why and what to do.
        private void TellIfNobody(Contract w)
        {
            FirmPlace firm = w != null ? FirmOf(w.Place) : null;
            if (firm == null || w.Stage != (int)WorkStage.Active || (w.Told & Contract.ToldNobody) != 0)
            {
                return;
            }
            FirmFacts facts = FactsOf(firm, w.Kind);
            Idle why = FirmsRules.Why(facts, false);
            if (why == Idle.None)
            {
                return;
            }
            w.Told |= Contract.ToldNobody;
            TellTech(NotificationType.Warning, "reallife_tech_note_nobody", Data("client", w.Client, "why", TechText.IdleLine(why, facts, false)), TabContracts, TechApp.ContractRow + w.Id);
        }

        // A contract nobody worked on yesterday: told once until somebody works on it again, with why and what to do.
        private void TellBehind(Contract w)
        {
            FirmPlace firm = w != null ? FirmOf(w.Place) : null;
            if (firm == null || w.Stage != (int)WorkStage.Active)
            {
                return;
            }
            FirmFacts facts = FactsOf(firm, w.Kind);
            Idle why = FirmsRules.Why(facts, true);
            if (why == Idle.None)
            {
                return;   // somebody worked: the points went to the firm's other work first
            }
            w.Told |= Contract.ToldBehind;
            TellTech(NotificationType.Warning, "reallife_tech_note_behind", Data("client", w.Client, "why", TechText.IdleLine(why, facts, true)), TabContracts, TechApp.ContractRow + w.Id);
        }
    }
}
