using System;
using System.Collections.Generic;
using System.Globalization;
using UI.Notification;

namespace RealLifeOverhaul.Tech
{
    /// <summary>
    /// The other tech firms under Living population (plans/tech-overhaul DRAFT2 section 3), under the master option and their own
    /// switch: every product of the player's firms other than a game sells into one shared market of its kind, sized from the
    /// city's people and firms (SectorBridges.City, the population) and the world they stand for, against the listed companies'
    /// and the startups' products, which build, price and sell by the same rules (SectorRules). The city's IT work is shared
    /// among the player's firms and the rival IT firms, the data centres let racks and the cloud sells compute from the city's
    /// firms and labs. The rivals run on the host and travel to Going Public guests as a part of the shared city. Off, the
    /// firms run by their own rules as before: the old per product markets, the old offers and tenders, the old cloud. The
    /// cost: nothing per frame; one pass over the markets and the rival products inside the firms' day.
    /// </summary>
    public sealed partial class TechDirector
    {
        private SectorFile _sector;
        private bool _sectorOn;
        private readonly List<RivalNote> _rivalNotes = new List<RivalNote>(8);
        private int[] _appeals = new int[64], _aware = new int[64];   // each product's appeal and awareness today, by its place in the firms' list

        /// <summary>The sector's file; null while it is off.</summary>
        public SectorFile Sector => _sectorOn ? _sector : null;

        /// <summary>True while the options want the sector: the overhaul's master switch and the firms' own.</summary>
        public static bool SectorWanted => Settings.LivingPopulation && Settings.LivingFirms;

        /// <summary>True while the sector runs the firms' markets.</summary>
        internal bool SectorOn => _sectorOn;

        private static readonly CultureInfo SectorInv = CultureInfo.InvariantCulture;

        // ---------------------------------------------------------------- start, stop and the save

        private void StartSector()
        {
            GameInstance save = Save;
            SectorFile file = SectorStore.Load(save.modData, out int version);
            bool fresh = file == null;
            if (fresh)
            {
                file = new SectorFile { Seed = new System.Random().Next(1, int.MaxValue), FirstDay = save.Day };
            }
            _sector = file;
            _sectorOn = true;
            SharedCity.Register(new SharedCityPart
            {
                Id = SectorShare.Part, Name = "tech markets",
                Ready = () => _sectorOn && _sector != null && Save != null,
                Write = () => _sectorOn && _sector != null ? SectorShare.Write(_sector) : null,
                Apply = SectorShared
            });
            TechBridges.SectorSessionsNow(true, HasCentres());
            Logger?.Info("Tech: the tech markets " + (fresh ? "opened for this save" : "loaded (version " + version + ")") + " on day " + save.Day + ": " + _sector.Rivals.Count
                         + " rival product(s)" + (_sector.Converted ? string.Empty : "; this save's products are taken in on the first day, each from where it stands") + ".");
        }

        private void StopSector(bool keep)
        {
            if (!_sectorOn)
            {
                return;
            }
            if (keep && Save != null && _sector != null)
            {
                WriteSector(Save.modData);
            }
            _sectorOn = false;
            TechBridges.SectorSessionsNow(false, false);
        }

        private void WriteSector(Dictionary<string, string> data)
        {
            if (_sector != null && data != null)
            {
                data[SectorStore.Key] = SectorStore.Write(_sector);
            }
        }

        // A guest: the host's city, companies, rivals and power price in place of this player's own (their firms stay their own).
        private bool SectorShared(string text, bool whole) => _sectorOn && SectorShare.Apply(_sector, text);

        /// <summary>True while the player has a data centre: its desk sessions are offered then.</summary>
        internal bool HasCentres() => _infra != null && _infra.Centres.Count > 0;

        /// <summary>For the save tools: the sector's line is forgotten; the timer starts it again from the firms as they stand.</summary>
        public void ForgetSector()
        {
            try
            {
                StopSector(false);
                _sector = null;
                Save?.modData?.Remove(SectorStore.Key);
            }
            catch (Exception e)
            {
                Fault("the save tools", e);
            }
        }

        // ---------------------------------------------------------------- the markets' day

        /// <summary>
        /// Once a day before the firms sell: the city read (the host's or a lone player's; a guest has the host's), an older
        /// save's products taken in on the first day, the startups' products followed, the power price's spike, then the
        /// markets begun with every product of the player's counted and its pull worked out (kept for its sale below), and the
        /// rivals' day (the host's or a lone player's; a guest follows the host's) with its news told.
        /// </summary>
        private void SectorDayBegin(int day, int cycle)
        {
            bool following = SharedCity.Follows(SectorShare.Part);
            if (!following)
            {
                SectorBridges.Read(_sector);
            }
            if (!_sector.Converted)
            {
                ConvertSector(day, cycle, following);
            }
            int fromRival = _sector.NextRival;
            if (!following)
            {
                // a company a newer version adds gets its products here, settled at their share below
                SectorRules.Companies(_sector, day, SectorRules.StartFeatures(_firmsFile));
                VentureFile v = Ventures;
                SectorRules.Startups(_sector, v != null ? v.Startups : null, day, _rivalNotes);
                for (int i = 0; i < _rivalNotes.Count; i++)
                {
                    RivalProduct r = _rivalNotes[i].Rival >= 0 && _rivalNotes[i].Rival < _sector.Rivals.Count ? _sector.Rivals[_rivalNotes[i].Rival] : null;
                    if (r != null && _rivalNotes[i].Kind == SectorRules.NoteNew)
                    {
                        Story("reallife_tech_mkt_news_startup", "name", r.Name, "market", TechText.T(SectorViews.MarketKey(r.Market)));
                    }
                }
                if (SectorRules.SpikeDay(_sector, day) && HasCentres())
                {
                    TellTech(NotificationType.Warning, "reallife_tech_mkt_note_spike", Data("tariff", TechText.Mille(_sector.SpikeFactor), "day", TechText.Day(_sector.SpikeUntil)), TabInfra, null);
                }
            }
            SectorPulls(day, cycle);
            if (!following)
            {
                SectorRules.Settle(_sector, fromRival);
                SectorRules.RivalDay(_sector, day, _rivalNotes);
                RivalNews();
                SharedCity.Changed(SectorShare.Part);
            }
            SectorRules.RivalCompute(_sector);
            _sector.Prune(KeepsPlace, KeepsProduct);
            TechBridges.SectorSessionsNow(true, HasCentres());
        }

        // A place the sector keeps a record for: one of the player's firms, or a data centre (where its sessions may be held).
        private bool KeepsPlace(string place) => _firmsFile.FirmAt(place) != null || (_infra != null && _infra.CentreAt(place) != null);

        private bool KeepsProduct(int id) => _firmsFile.ProductOf(id) != null;

        // The markets begun, every product of the player's counted, then each one's appeal and awareness worked out and its pull joined.
        private void SectorPulls(int day, int cycle)
        {
            SectorRules.BeginDay(_sector, day, cycle);
            List<Product> products = _firmsFile.Products;
            if (_appeals.Length < products.Count)
            {
                Array.Resize(ref _appeals, products.Count * 2);
                Array.Resize(ref _aware, products.Count * 2);
            }
            for (int i = 0; i < products.Count; i++)
            {
                SectorRules.Count(_sector, products[i]);
            }
            SectorRules.Attract(_sector);
            for (int i = 0; i < products.Count; i++)
            {
                Product p = products[i];
                int m = SectorRules.MarketOf(p);
                FirmPlace firm = m >= 0 && p.Stage < 2 ? FirmOf(p.Place) : null;
                _appeals[i] = _aware[i] = 0;
                if (firm == null)
                {
                    continue;
                }
                SectorMarketDay md = _sector.Markets[m];
                SectorProduct sp = _sector.ProductAt(p.Id);
                p.RivalFeatures = md.RivalFeatures;   // the products' table and the old rules (should the sector be switched off) read the best rival as the product's
                _appeals[i] = SectorRules.Appeal(p, firm.File, firm.Crew, md);
                _aware[i] = SectorRules.Awareness(FirmsCatalogue.MarketingDay[TalentCatalogue.Clamp(p.Marketing, 0, FirmsCatalogue.MarketingDay.Length - 1)], md.Buyers, SectorRules.Have(p),
                    md.Who, SectorRules.Boost(sp, day));
                if (p.Live)
                {
                    SectorRules.Join(md, SectorRules.Attraction(_appeals[i], _aware[i]));
                }
            }
        }

        /// <summary>
        /// An older save taken in on the sector's first day: the companies' products made as strong as the rivals the products
        /// faced, then each market held up so every product of the player's sits at its share with the customers it has
        /// (SectorRules.Anchor), fading to the city's own market over a year: nothing jumps. A guest has the host's companies
        /// and markets, so it takes its products in as they stand.
        /// </summary>
        private void ConvertSector(int day, int cycle, bool following)
        {
            if (!following)
            {
                SectorRules.Companies(_sector, day, SectorRules.StartFeatures(_firmsFile));
                var products = new List<Product>(8);
                var appeals = new List<int>(8);
                var marketing = new List<long>(8);
                SectorRules.BeginDay(_sector, day, cycle);
                for (int i = 0; i < _firmsFile.Products.Count; i++)
                {
                    SectorRules.Count(_sector, _firmsFile.Products[i]);
                }
                for (int i = 0; i < _firmsFile.Products.Count; i++)
                {
                    Product p = _firmsFile.Products[i];
                    int m = SectorRules.MarketOf(p);
                    FirmPlace firm = m >= 0 && p.Live ? FirmOf(p.Place) : null;
                    if (firm != null)
                    {
                        products.Add(p);
                        appeals.Add(SectorRules.Appeal(p, firm.File, firm.Crew, _sector.Markets[m]));
                        marketing.Add(FirmsCatalogue.MarketingDay[TalentCatalogue.Clamp(p.Marketing, 0, FirmsCatalogue.MarketingDay.Length - 1)]);
                    }
                }
                SectorRules.Anchor(_sector, day, cycle, products, appeals, marketing);
                int held = 0;
                for (int m = 0; m < _sector.Anchor.Length; m++)
                {
                    held += _sector.Anchor[m] > 1000 ? 1 : 0;
                }
                Logger?.Info("Tech: the tech markets took this save's " + products.Count + " product(s) on sale in, " + held + " market(s) held up for their customers, fading over "
                             + SectorRules.AnchorDays + " days: nothing jumps.");
            }
            _sector.Converted = true;
        }

        // The rivals' news of the day: a product of theirs that took a lead from the player's best, a price moved, a service down.
        private void RivalNews()
        {
            for (int i = 0; i < _rivalNotes.Count; i++)
            {
                RivalNote n = _rivalNotes[i];
                RivalProduct r = n.Rival >= 0 && n.Rival < _sector.Rivals.Count ? _sector.Rivals[n.Rival] : null;
                if (r == null)
                {
                    continue;
                }
                int stock = r.Startups ? 0 : CompetitorRules.CompanyStock[TalentCatalogue.Clamp(r.Company, 0, CompetitorRules.CompanyStock.Length - 1)];
                string market = TechText.T(SectorViews.MarketKey(r.Market));
                switch (n.Kind)
                {
                    case SectorRules.NoteShipped:
                        if (stock > 0)
                        {
                            TechBridges.Stock?.Invoke(stock, SectorCatalogue.RivalStockShip);
                        }
                        TellTech(NotificationType.Info, "reallife_tech_mkt_note_overtaken", Data("rival", r.Name, "market", market, "features", r.Features.ToString(SectorInv)), TabProducts, null);
                        Story("reallife_tech_mkt_news_ship", "name", r.Name, "market", market);
                        SectorBridges.Say(SectorNews.RivalShipped, null, r.Name, r.Features);
                        break;
                    case SectorRules.NoteCut:
                        if (stock > 0)
                        {
                            TechBridges.Stock?.Invoke(stock, SectorCatalogue.RivalStockCut);
                        }
                        if (_sector.Markets[r.Market].Mine > 0)
                        {
                            TellTech(NotificationType.Info, "reallife_tech_mkt_note_cut", Data("rival", r.Name, "market", market), TabProducts, null);
                        }
                        Story("reallife_tech_mkt_news_cut", "name", r.Name, "market", market);
                        SectorBridges.Say(SectorNews.RivalCut, null, r.Name, FirmsCatalogue.PriceTiers[TalentCatalogue.Clamp(r.Tier, 0, 2)]);
                        break;
                    case SectorRules.NoteOutage:
                        if (r.Customers >= 500)
                        {
                            Story("reallife_tech_mkt_news_outage", "name", r.Name, "market", market);
                        }
                        break;
                }
            }
            if (Verbose && _rivalNotes.Count > 0)
            {
                Logger?.Info("Tech check (markets, rivals): " + _sector.Rivals.Count + " rival product(s), " + _rivalNotes.Count + " event(s) today: PASS");
            }
        }

        // ---------------------------------------------------------------- a product of the player's in its market

        /// <summary>
        /// A product's day of building and selling under the sector: built by the firm's points with a sprint's focus and pace, the
        /// product people's curve and no cap on features (a model held while its safety checks run), then sold in its shared market
        /// with the pull worked out this morning. An outage is remembered for its response at the desk.
        /// </summary>
        private ProductDay SectorProductDay(FirmPlace firm, Product p, int index, int points, int testing, int day, int hostingRate, out bool built)
        {
            Firm file = firm.File;
            Crew c = firm.Crew;
            SectorFirm sf = _sector.FirmFor(file.Place);
            SectorProduct sp = _sector.ProductAt(p.Id);
            bool held = p.K == ProductKind.Model && SectorRules.Held(sp, day);
            Crew building = c;
            building.Faults = c.Faults * SectorRules.SprintFaults(sf, day) / 1000;
            int give = held ? 0 : SectorRules.Diverted(sf, points, day) * SectorRules.SprintPoints(sf, day) / 1000;
            int before = p.Features;
            FirmsRules.Build(file, p, building, give, testing, SectorCatalogue.FeaturesMost, SectorRules.ProductBoost(c), SectorRules.SprintFocus(sf, file.Focus, day));
            built = p.Features > before;
            if (!p.Live)
            {
                return default(ProductDay);
            }
            int m = SectorRules.MarketOf(p);
            long users = p.Users;
            int appeal = index < _appeals.Length ? _appeals[index] : 0, aware = index < _aware.Length ? _aware[index] : 0;
            ProductDay r = SectorRules.SellDay(_firmsFile, file, p, c, c.Support, day, _sector.Markets[m], appeal, aware, hostingRate);
            if (r.Outage)
            {
                sf.LastOutage = day;
                sf.LostToOutage += users * FirmsCatalogue.OutageChurn / 1000;
                SectorBridges.Say(SectorNews.Outage, file.Place, p.Name, r.Lost);
            }
            if (r.Breach)
            {
                SectorBridges.Say(SectorNews.Breach, file.Place, p.Name, r.Affected);
            }
            return r;
        }

        /// <summary>A rival's pull on a professional's place (thousandths for TalentRules.Approached): harder from a company that just took a lead or won a tender against the firm's kind.</summary>
        internal int SectorPull(string place, int day)
        {
            FirmPlace firm = _sectorOn ? FirmOf(place) : null;
            if (firm == null)
            {
                return 1000;
            }
            SectorRules.Poacher(_sector, firm.File.Kind, day, out int pull);
            return pull;
        }

        /// <summary>The rival company that approaches a firm's people, or null: the one that lately took a lead or won a tender against its kind.</summary>
        internal string SectorPoacher(string place, int day)
        {
            FirmPlace firm = _sectorOn ? FirmOf(place) : null;
            int c = firm != null ? SectorRules.Poacher(_sector, firm.File.Kind, day, out _) : -1;
            return c >= 0 && c < HiringCatalogue.RivalFirms.Length ? HiringCatalogue.RivalFirms[c] : null;
        }

        /// <summary>A tender lost to a rival under the sector: the paper names the winner, Flare hears of it.</summary>
        private void TenderToRival(Contract t, int winner)
        {
            if (winner < 0 || winner >= HiringCatalogue.RivalFirms.Length)
            {
                return;
            }
            Story("reallife_tech_mkt_news_tender", "name", HiringCatalogue.RivalFirms[winner], "value", TechText.Cents(t.Value * Prices.Level / 1000));
            SectorBridges.Say(SectorNews.TenderWon, t.Place, HiringCatalogue.RivalFirms[winner], t.Value);
        }
    }
}
