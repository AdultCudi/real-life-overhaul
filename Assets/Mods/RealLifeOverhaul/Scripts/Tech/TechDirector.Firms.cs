using System;
using System.Collections.Generic;
using RealLifeOverhaul.GameUi;
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
        public int Spare = 1000;      // thousandths of its staff hours free for the tech sector's work (all, but at a game office)
        public int People;
        public int Hours;             // the hours its people worked yesterday
        public long Today;            // takings booked today, cents
    }

    /// <summary>
    /// The firms part: every business of the tech sector's types, and every web development agency and graphic designer
    /// of the player's, is a firm (Tech/Firms/FirmsRules). It runs with the people part, whose files say what each firm's
    /// people can do. Each game day, after the people's day: contracts offered and tenders put out and decided, each
    /// firm's engineering time shared out (contracts under way first, then its products by their shares), products built,
    /// sold and their risks rolled, the money of the day that ended booked at each firm, the research and development
    /// relief given each quarter, and the paper, Standing and the stock market told what matters.
    /// </summary>
    public sealed partial class TechDirector
    {
        public const string TechAppId = "RealLifeTechSector";
        public const int TabFirms = 0, TabProducts = 1, TabContracts = 2;
        private const int QuarterDays = 90;

        private readonly List<FirmPlace> _firms = new List<FirmPlace>(8);
        private readonly List<BuildingRegistration> _firmRegs = new List<BuildingRegistration>(8);
        private readonly List<KeyValuePair<string, string>> _clients = new List<KeyValuePair<string, string>>(16);
        private readonly List<Crew> _crews = new List<Crew>(8);
        private readonly List<WorkNote> _work = new List<WorkNote>(8);
        private readonly List<int> _taken = new List<int>(2), _behind = new List<int>(4);   // contracts to tell of after the day's notes
        private FirmsFile _firmsFile;
        private bool _firmsOn;
        private PhoneApp _techApp;
        private TechApp _techView;
        private bool _techGaveUp;

        public bool FirmsRunning => _firmsOn;

        /// <summary>The firms' file; null while the part is off.</summary>
        public FirmsFile Firms => _firmsOn ? _firmsFile : null;

        public IReadOnlyList<FirmPlace> FirmPlaces => _firms;

        // ---------------------------------------------------------------- starting and stopping

        private void StartFirms()
        {
            GameInstance save = Save;
            int version;
            FirmsFile file = FirmsStore.Load(save.modData, out version);
            bool fresh = file == null;
            if (fresh)
            {
                file = new FirmsFile { Seed = new System.Random().Next(1, int.MaxValue), FirstDay = save.Day, LastTender = save.Day };
            }
            _firmsFile = file;
            _firmsOn = true;
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

        // ---------------------------------------------------------------- the game's businesses and the firms

        /// <summary>Each firm is tied to its business; a business seen for the first time is a new firm, and a firm whose business is no longer the player's is closed with its products and work.</summary>
        private void ReadFirms(int day)
        {
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
                _firmsFile.Products.RemoveAll(p => p.Place == f.Place);
                for (int c = 0; c < _firmsFile.Contracts.Count; c++)
                {
                    Contract w = _firmsFile.Contracts[c];
                    if (w.Place == f.Place && w.Stage <= (int)WorkStage.Active)
                    {
                        w.Stage = (int)WorkStage.Failed;
                        w.End = day;
                    }
                }
                _firmsFile.Firms.RemoveAt(i);
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

        /// <summary>
        /// What each firm's people can do on the day: their points by family (a mid-level in good heart at eight hours is
        /// eight points), the share from seniors, the security posture (the analysts, their certificates, a data protection
        /// officer), the reliability (the site reliability and DevOps people), how often their work needs doing again, and
        /// the day's pay of the people whose work counts for research and development.
        /// </summary>
        private void Crews()
        {
            for (int f = 0; f < _firms.Count; f++)
            {
                FirmPlace firm = _firms[f];
                var c = new Crew();
                int security = 200, reliability = 150, seniors = 0, hours = 0;
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
                        security += r.Id == TalentCatalogue.DataProtection ? 150 : Math.Min(250, p.A / 1000 * 3);
                        security += p.Has(2) || p.Has(9) ? 50 : 0;   // security practitioner, privacy professional
                    }
                    if ((r.Id == TalentCatalogue.Sre || r.Id == TalentCatalogue.DevOps) && h > 0)
                    {
                        reliability += Math.Min(250, (p.A + p.B) / 2000 * 3);
                    }
                }
                c.Security = Math.Min(950, security);
                c.Reliability = Math.Min(950, reliability);
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
                firm.Crew = c;
            }
        }

        // ---------------------------------------------------------------- the day

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
            // tenders whose last day is today are decided
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
                        _taken.Add(t.Id);   // (told after the day's notes whether anybody can work on it)
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
                // contracts under way come first
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
                        _behind.Add(w.Id);   // (a whole day under way with nothing done: told after the day's notes if nobody worked)
                    }
                    long paid = FirmsRules.Work(w, file, give, day, _work);
                    if (paid > 0)
                    {
                        Paid(firm, w, paid * level / 1000);
                    }
                }
                // then the products, by their shares
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
                    ProductDay r = FirmsRules.Sell(_firmsFile, file, p, c, c.Support, day, cycle, HostingRate(file.Place));
                    firm.Today += r.Takings * level / 1000;
                    hosting += r.Hosting * level / 1000;
                    marketing += r.Marketing;
                    quality += FirmsRules.Quality(p);
                    rated++;
                    if (r.Outage)
                    {
                        outages++;
                        TechBridges.Fame?.Invoke(0, -1);
                        Tell2(NotificationType.Warning, "reallife_tech_note_outage", Data("product", p.Name, "lost", r.Lost.ToString()), TabProducts, TechApp.ProductRow + p.Id);
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
                            Tell2(NotificationType.Info, "reallife_tech_note_rival", Data("product", p.Name, "rival", FirmsCatalogue.RivalName(FirmsCatalogue.RivalStock(p.K, p.S))), TabProducts, TechApp.ProductRow + p.Id);
                        }
                    }
                }
                if (rated > 0)
                {
                    FirmsRules.Reputation(file, quality / rated);
                }
                // the research and development relief: the share of the qualifying pay that went to products
                if (c.Engineering > 0 && rndPoints > 0)
                {
                    file.RnD += c.Wages * Math.Min(c.Engineering, rndPoints) / Math.Max(1, c.Engineering);
                }
                if (day - file.ReliefDay >= QuarterDays && file.RnD > 0)
                {
                    long relief = FirmsRules.Relief(file.RnD);
                    FirmsGame.Relief(relief);
                    Tell2(NotificationType.Success, "reallife_tech_note_relief", Data("firm", file.Name, "amount", TechText.Cents(relief)), TabFirms, TechApp.FirmRow + file.Place);
                    file.RnD = 0;
                    file.ReliefDay = day;
                }
                else if (file.ReliefDay == 0)
                {
                    file.ReliefDay = day;
                }
                // the day's money, dated the day that ended
                int dated = day - 1;
                FirmsGame.Takings(firm.Today, firm.Reg, file.Name, dated);
                FirmsGame.Cost(hosting, FirmsGame.HostingKey, firm.Reg, file.Name, dated);
                if (HostingRate(file.Place) < 1000)
                {
                    _ownHosting += hosting;   // (paid to the player's own cloud: its takings on the infrastructure's day)
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
                Logger?.Info("Tech check (firms, day " + day + "): " + _firms.Count + " firm(s), " + _firmsFile.Products.Count + " product(s), " + _firmsFile.Contracts.Count + " contract(s), " + _work.Count
                             + " contract event(s), " + outages + " outage(s), " + breaches + " breach(es); " + ((Time.realtimeSinceStartup - started) * 1000f).ToString("0.0") + " ms: PASS");
            }
        }

        /// <summary>The steps of the way up after the firms' own (a data centre, a cloud, a hyperscaler, an AI lab), for the Firms tab.</summary>
        internal void PathMore(RealLifeOverhaul.GameUi.InfoBox box)
        {
            PathInfra(box);
        }

        partial void PathInfra(RealLifeOverhaul.GameUi.InfoBox box);

        /// <summary>What the firm's hosting costs, thousandths of the list price: the rival clouds' list price until the player runs a cloud of their own.</summary>
        internal int HostingRate(string place)
        {
            return Save != null && OwnCloud(Save.Day) ? InfraCatalogue.OwnHostingShare : 1000;
        }

        // A contract has paid: the firm takes it; a client of the player's own pays it from that business.
        private void Paid(FirmPlace firm, Contract w, long cents)
        {
            firm.Today += cents;
            if (!string.IsNullOrEmpty(w.OwnPlace))
            {
                BuildingRegistration client = TalentGame.Find(w.OwnPlace);
                if (client != null)
                {
                    FirmsGame.Cost(cents, FirmsGame.ServicesKey, client, firm.File.Name, Save.Day - 1);
                }
            }
        }

        // A breach: the regulator's fine, a case in court for a firm off the register or caught twice, the insurer told, Standing and the paper.
        private void Breach(FirmPlace firm, Product p, ProductDay r, int day)
        {
            Firm file = firm.File;
            FirmsGame.Fine(r.Fine * Prices.Level / 1000, firm.Reg, file.Name);
            TechBridges.Claim?.Invoke(firm.Reg, (int)Math.Min(int.MaxValue, r.Fine / 100));
            TechBridges.Fame?.Invoke(3, -2);
            bool court = day >= file.RegisteredUntil || file.Breaches >= 2;
            int number = court && TechBridges.Prosecute != null ? TechBridges.Prosecute(file.Name) : 0;
            Tell2(NotificationType.Error, number > 0 ? "reallife_tech_note_breach_court" : "reallife_tech_note_breach",
                Data("product", p.Name, "affected", TechText.Whole(r.Affected), "fine", TechText.Cents(r.Fine * Prices.Level / 1000)), TabFirms, TechApp.FirmRow + file.Place);
            Story("reallife_tech_news_breach", "firm", file.Name, "count", TechText.Whole(r.Affected));
        }

        // What a contract's day did, in a notification.
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
                case FirmsRules.NoteOffered:
                    Tell2(NotificationType.Info, "reallife_tech_note_offer", Data("client", w.Client, "work", TechText.Work((WorkKind)w.Kind), "value", TechText.Cents(w.Value * Prices.Level / 1000)), TabContracts, row);
                    break;
                case FirmsRules.NoteTender:
                    Tell2(NotificationType.Info, "reallife_tech_note_tender", Data("value", TechText.Cents(w.Value * Prices.Level / 1000), "day", w.End.ToString()), TabContracts, row);
                    break;
                case FirmsRules.NoteWon:
                    Tell2(NotificationType.Success, "reallife_tech_note_tender_won", Data("value", TechText.Cents(w.Value)), TabContracts, row);
                    break;
                case FirmsRules.NoteLostTender:
                    Tell2(NotificationType.Info, "reallife_tech_note_tender_lost", Data("client", w.Client), TabContracts, row);
                    break;
                case FirmsRules.NoteDone:
                    Tell2(NotificationType.Success, "reallife_tech_note_done", Data("client", w.Client, "amount", TechText.Cents(note.Amount)), TabContracts, row);
                    break;
                case FirmsRules.NoteFailed:
                    Tell2(NotificationType.Warning, "reallife_tech_note_failed", Data("client", w.Client), TabContracts, row);
                    break;
            }
        }

        // ---------------------------------------------------------------- what the player does (the app)

        internal Product NewProduct(string place, ProductKind kind, Segment segment, int scope)
        {
            FirmPlace firm = FirmOf(place);
            int live = 0;
            for (int i = 0; i < _firmsFile.Products.Count; i++)
            {
                live += _firmsFile.Products[i].Stage < 2 ? 1 : 0;
            }
            if (firm == null || !FirmsRules.Makes(firm.File.K, kind) || live >= FirmsCatalogue.ProductsMost || Save == null)
            {
                return null;
            }
            Product p = FirmsRules.NewProduct(_firmsFile, place, kind, segment, scope, null);
            p.Launched = Save.Day;
            _firmsFile.Products.Add(p);
            ChangedTech();
            return p;
        }

        internal void SetProduct(int id, int tier, int marketing, int share)
        {
            Product p = _firmsOn ? _firmsFile.ProductOf(id) : null;
            if (p == null)
            {
                return;
            }
            if (tier >= 0)
            {
                p.Tier = TalentCatalogue.Clamp(tier, 0, 2);
            }
            if (marketing >= 0)
            {
                p.Marketing = TalentCatalogue.Clamp(marketing, 0, FirmsCatalogue.MarketingDay.Length - 1);
            }
            if (share >= 0)
            {
                p.Share = TalentCatalogue.Clamp(share, 0, 100);
            }
            ChangedTech();
        }

        internal bool Launch(int id)
        {
            Product p = _firmsOn ? _firmsFile.ProductOf(id) : null;
            FirmPlace firm = p != null ? FirmOf(p.Place) : null;
            if (p == null || firm == null || Save == null || !FirmsRules.CanLaunch(p))
            {
                return false;
            }
            FirmsRules.Launch(p, Save.Day);
            TechBridges.Fame?.Invoke(0, 1);
            Story("reallife_tech_news_launch", "product", p.Name, "firm", firm.File.Name);
            Tell2(NotificationType.Success, "reallife_tech_note_launched", Data("product", p.Name), TabProducts, TechApp.ProductRow + p.Id);
            ChangedTech();
            return true;
        }

        internal void Withdraw(int id)
        {
            Product p = _firmsOn ? _firmsFile.ProductOf(id) : null;
            if (p != null && p.Stage < 2)
            {
                p.Stage = 2;
                ChangedTech();
            }
        }

        internal void SetFocus(string place, int focus)
        {
            FirmPlace firm = FirmOf(place);
            if (firm != null)
            {
                firm.File.Focus = TalentCatalogue.Clamp(focus, 0, (int)Focus.Count - 1);
                ChangedTech();
            }
        }

        /// <summary>A year on the data protection register, paid now. False if the cash does not cover it.</summary>
        internal bool Register(string place)
        {
            FirmPlace firm = FirmOf(place);
            if (firm == null || Save == null)
            {
                return false;
            }
            long fee = (long)Prices.At(FirmsCatalogue.RegisterFee / 100) * 100;
            if (!FirmsGame.Pay(fee, FirmsGame.RegisterKey, firm.Reg, firm.File.Name))
            {
                Notifications.ShowInsufficientMoney();
                return false;
            }
            firm.File.RegisteredUntil = Math.Max(firm.File.RegisteredUntil, Save.Day) + GameCalendar.DaysPerYear;
            ChangedTech();
            return true;
        }

        /// <summary>An offer taken by the firm it was made to: under way from today, a project's first share paid in.</summary>
        internal bool TakeContract(int id)
        {
            Contract w = _firmsOn ? _firmsFile.ContractOf(id) : null;
            FirmPlace firm = w != null ? FirmOf(w.Place) : null;
            if (w == null || firm == null || w.Stage != (int)WorkStage.Offered || Save == null)
            {
                return false;
            }
            long upfront = FirmsRules.Take(w, firm.File.Place, Save.Day) * Prices.Level / 1000;
            if (upfront > 0)
            {
                FirmsGame.Takings(upfront, firm.Reg, firm.File.Name, null);
                if (!string.IsNullOrEmpty(w.OwnPlace))
                {
                    BuildingRegistration client = TalentGame.Find(w.OwnPlace);
                    if (client != null)
                    {
                        FirmsGame.Cost(upfront, FirmsGame.ServicesKey, client, firm.File.Name, null);
                    }
                }
            }
            TellIfNobody(w);
            ChangedTech();
            return true;
        }

        // ---------------------------------------------------------------- whether anybody works

        /// <summary>What decides whether a firm's people can work on a kind of contract (-1: any of its work), read now.</summary>
        internal FirmFacts FactsOf(FirmPlace firm, int kind)
        {
            return firm == null || Save == null ? default(FirmFacts) : FirmsGame.Facts(firm.Reg, firm.File.K, kind, _people, firm.File.Place, Save.Day);
        }

        // A contract just taken on (or a tender just won) that nobody at the firm can work on: told once, with why and what to do.
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
            Tell2(NotificationType.Warning, "reallife_tech_note_nobody", Data("client", w.Client, "why", TechText.IdleLine(why, facts, false)), TabContracts, TechApp.ContractRow + w.Id);
        }

        // A contract under way that nobody who does its work worked on yesterday: told once (again only after somebody has
        // worked on it since), with why and what to do.
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
                return;   // (somebody worked: the points went to other work of the firm's first)
            }
            w.Told |= Contract.ToldBehind;
            Tell2(NotificationType.Warning, "reallife_tech_note_behind", Data("client", w.Client, "why", TechText.IdleLine(why, facts, true)), TabContracts, TechApp.ContractRow + w.Id);
        }

        /// <summary>The Utilities app on its internet page, the game's schedule of a firm, or the Company app's rota for it.</summary>
        internal bool OpenInternet() => TechBridges.Try(TechBridges.OpenInternet);

        internal bool OpenSchedule(FirmPlace firm) => firm != null && FirmsGame.OpenSchedule(firm.Reg);

        internal bool OpenRota(FirmPlace firm) => firm != null && TechBridges.RotaFor(firm.File.Place);

        internal void DeclineContract(int id)
        {
            Contract w = _firmsOn ? _firmsFile.ContractOf(id) : null;
            if (w != null && (w.Stage == (int)WorkStage.Offered || w.Stage == (int)WorkStage.Tender))
            {
                w.Stage = (int)WorkStage.Declined;
                w.End = Save != null ? Save.Day : w.End;
                ChangedTech();
            }
        }

        /// <summary>A bid on the council's tender by one of the player's firms, at a share of the fair price.</summary>
        internal bool BidTender(int id, string place, int thousandths)
        {
            Contract t = _firmsOn ? _firmsFile.ContractOf(id) : null;
            FirmPlace firm = FirmOf(place);
            if (t == null || firm == null || t.Stage != (int)WorkStage.Tender || !FirmsRules.Takes(firm.File.K, t.K))
            {
                return false;
            }
            t.Bid = FirmsRules.BidAt(t, thousandths);
            t.Place = place;
            t.Stage = (int)WorkStage.Bid;
            ChangedTech();
            return true;
        }

        /// <summary>A contract under way given up: no more is paid, and the firm's reputation suffers.</summary>
        internal void DropContract(int id)
        {
            Contract w = _firmsOn ? _firmsFile.ContractOf(id) : null;
            FirmPlace firm = w != null ? FirmOf(w.Place) : null;
            if (w != null && w.Stage == (int)WorkStage.Active)
            {
                w.Stage = (int)WorkStage.Failed;
                w.End = Save != null ? Save.Day : w.End;
                if (firm != null)
                {
                    firm.File.Reputation = Math.Max(0, firm.File.Reputation - 80);
                }
                ChangedTech();
            }
        }

        // ---------------------------------------------------------------- the app

        private static readonly string[] TechGlyphs = { "Icon-Laptop", "Icon-Building" };

        private void InstallTech()
        {
            var view = new TechApp(this);
            GameSprites.Want(TechGlyphs);
            bool tryAgain;
            _techApp = PhoneApp.Install(new PhoneAppDef
            {
                Id = TechAppId,
                TitleKey = "reallife_tech_app",
                Icon = like => AppIconPainter.Paint(like, Glyph(TechGlyphs), new Color32(120, 120, 230, 255), new Color32(60, 50, 160, 255), 0.42f, "AppIcon-TechSector"),
                Build = view.Build,
                Opened = view.Opened,
                Closed = view.Closed
            }, out tryAgain);
            if (_techApp == null)
            {
                _techGaveUp = !tryAgain;
                if (_techGaveUp)
                {
                    Logger?.Warn("Tech: the game's phone is not built as this version expects, so the Tech app is left off. Firms still run each day and the notifications still say what needs an answer.");
                }
                return;
            }
            _techView = view;
            TechBadge();
        }

        public void OpenTech(int tab, string pick)
        {
            if (_techApp != null && _techView != null)
            {
                _techView.Want(tab, pick);
                _techApp.Open();
            }
        }

        internal bool TechOpened()
        {
            if (_broken || Save == null || !_firmsOn)
            {
                return false;
            }
            try
            {
                ReadPeople(Save.Day);
                ReadFirms(Save.Day);
                Crews();
                TechBadge();
                return true;
            }
            catch (Exception e)
            {
                Fault("opening the Tech app", e);
                return false;
            }
        }

        /// <summary>Offers and tenders waiting for an answer, and games ready to launch.</summary>
        internal int TechWaiting()
        {
            int n = 0;
            if (!_firmsOn)
            {
                return 0;
            }
            for (int i = 0; i < _firmsFile.Contracts.Count; i++)
            {
                n += _firmsFile.Contracts[i].Stage == (int)WorkStage.Offered || _firmsFile.Contracts[i].Stage == (int)WorkStage.Tender ? 1 : 0;
            }
            for (int i = 0; i < _firmsFile.Products.Count; i++)
            {
                n += FirmsRules.CanLaunch(_firmsFile.Products[i]) ? 1 : 0;
            }
            return n;
        }

        private void TechBadge()
        {
            if (_techApp != null)
            {
                _techApp.SetBadge(_techApp.Showing ? 0 : TechWaiting());
            }
        }

        private void ChangedTech()
        {
            _techView?.Changed();
            TechBadge();
        }

        /// <summary>A notification of the game's that opens the Tech app on a tab with a row picked.</summary>
        internal void Tell2(NotificationType type, string key, Dictionary<string, string> data, int tab, string pick)
        {
            Notifications.Show(type, key, data, 10f, null, () => OpenTech(tab, pick), true, false);
        }
    }
}
