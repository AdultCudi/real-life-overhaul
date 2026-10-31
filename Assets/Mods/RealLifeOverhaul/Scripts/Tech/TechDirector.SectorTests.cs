using System;
using System.Collections.Generic;
using System.Globalization;
using UI.Notification;

namespace RealLifeOverhaul.Tech
{
    /// <summary>
    /// The other tech firms' entries in Options, Tests, each on the player's own firms and for real: a kind's desk sessions held
    /// at once with a manager's sound choices (the event a session answers set up first where the test can: an outage, a client's
    /// offer, an incident, a power price spike, a data centre down, a model's new generation), the markets as they stand, and a
    /// year of the markets lived at once. Each says in a notice why it cannot run.
    /// </summary>
    public sealed partial class TechDirector
    {
        private static readonly CultureInfo TestInv = CultureInfo.InvariantCulture;

        // The sector running, or null with a notice why.
        private bool SectorTestReady()
        {
            if (_sectorOn && Save != null && _firmsFile != null)
            {
                return true;
            }
            Notifications.Show(NotificationType.Warning, SectorWanted ? "reallife_tech_mkt_test_not_ready" : "reallife_tech_mkt_test_off", null, 8f);
            return false;
        }

        // The player's first firm of a kind (the web agency's test takes a design studio too), or null.
        private FirmPlace FirstFirm(int kind)
        {
            for (int i = 0; i < _firms.Count; i++)
            {
                int k = _firms[i].File.Kind;
                if (k == kind || kind == (int)FirmKind.WebAgency && k == (int)FirmKind.Design || kind < 0 && k != (int)FirmKind.Games)
                {
                    return _firms[i];
                }
            }
            return null;
        }

        /// <summary>A kind's desk sessions held at once at the player's first firm of it (-1: the data centres' at any tech office), each with a manager's sound choices.</summary>
        public void TestSectorDesks(int kind)
        {
            try
            {
                if (!SectorTestReady())
                {
                    return;
                }
                if (kind < 0 && !HasCentres())
                {
                    Notifications.Show(NotificationType.Warning, "reallife_tech_mkt_test_no_centre", null, 8f);
                    return;
                }
                FirmPlace firm = FirstFirm(kind);
                if (firm == null)
                {
                    Notifications.Show(NotificationType.Warning, "reallife_tech_mkt_test_no_firm", Data("kind", TechText.FirmName((FirmKind)Math.Max(0, kind))), 8f);
                    return;
                }
                int held = 0, tried = 0;
                string last = null;
                string[] lastPairs = null;
                for (int k = 0; k < SectorRules.Desks; k++)
                {
                    if (SectorRules.DeskFirm[k] != kind && !(kind < 0 && SectorRules.DeskFirm[k] < 0))
                    {
                        continue;
                    }
                    tried++;
                    SetUpDesk(k, firm);
                    var def = new int[3];
                    for (int step = 0; step < def.Length; step++)
                    {
                        def[step] = SectorPick(k, firm.File.Place, step);
                    }
                    bool done = SectorSession(k, firm.File.Place, firm.File.Name, 1000, def, out string key, out string[] pairs);
                    held += done ? 1 : 0;
                    last = key;
                    lastPairs = pairs;
                    Logger?.Info("Tech check (desk, " + SectorRules.DeskIds[k] + " at " + firm.File.Name + "): " + (done ? "held with the sound choices: PASS" : "not held: " + key));
                    if (!done)
                    {
                        Notifications.Show(NotificationType.Warning, key, Data(pairs), 8f);
                    }
                }
                if (held > 0)
                {
                    Notifications.Show(NotificationType.Success, "reallife_tech_mkt_test_desks_done", Data("held", held.ToString(TestInv), "of", tried.ToString(TestInv), "firm", firm.File.Name), 10f);
                }
            }
            catch (Exception e)
            {
                Fault("the tech markets' test", e);
            }
        }

        // The event a session answers set up when the test can: an outage, an offer, an incident, a spike, a centre down, a model's next generation.
        private void SetUpDesk(int kind, FirmPlace firm)
        {
            int day = Save.Day;
            string place = firm.File.Place;
            SectorFirm sf = _sector.FirmFor(place);
            switch (kind)
            {
                case SectorRules.DeskOutage:
                    Product down = LiveProduct(place, 0, false);
                    if (down != null)
                    {
                        long lost = down.Users * FirmsCatalogue.OutageChurn / 1000;
                        down.Users -= lost;
                        sf.LastOutage = day;
                        sf.LostToOutage = lost;
                    }
                    break;
                case SectorRules.DeskLaunch:
                    Product model = TrainingModel(place);
                    SectorProduct sp = model != null ? _sector.ProductFor(model.Id) : null;
                    if (sp != null && model.Live && !SectorRules.HasLaunch(model, sp))
                    {
                        sp.Generation = Math.Max(0, model.Features - 2);   // as if a new generation had just been trained
                    }
                    break;
                case SectorRules.DeskTraining:
                    SectorProduct trained = TrainingModel(place) != null ? _sector.ProductFor(TrainingModel(place).Id) : null;
                    if (trained != null)
                    {
                        trained.TrainedDay = Math.Min(trained.TrainedDay, day);   // its last checks done
                    }
                    break;
                case SectorRules.DeskBigCustomer:
                    sf.LastBigAsk = Math.Min(sf.LastBigAsk, day - SectorCatalogue.BigCustomerEvery);
                    break;
                case SectorRules.DeskPitch:
                case SectorRules.DeskClientPitch:
                    if (PitchFor(firm, kind == SectorRules.DeskPitch) == null)
                    {
                        OfferForTest(firm, day);
                    }
                    break;
                case SectorRules.DeskIncident:
                    if (ActiveWhere(place, OpenIncident) == null && _firmsFile.Contracts.Count < SectorCatalogue.ContractsMost)
                    {
                        var w = new Contract
                        {
                            Id = _firmsFile.Next(), Place = place, Kind = (int)WorkKind.Incident, Client = FirmsCatalogue.Clients[0], Stage = (int)WorkStage.Offered, End = day + 1,
                            Points = SectorCatalogue.IncidentPoints, Value = FirmsRules.ValueOf(WorkKind.Incident, SectorCatalogue.IncidentPoints, false)
                        };
                        _firmsFile.Contracts.Add(w);
                        TakeContract(w.Id);
                    }
                    break;
                case SectorRules.DeskPower:
                    if (!SectorRules.SpikeWaits(_sector, day))
                    {
                        _sector.SpikeFrom = day;
                        _sector.SpikeUntil = day + SectorCatalogue.SpikeDaysLeast;
                        _sector.SpikeFactor = (SectorCatalogue.SpikeLeast + SectorCatalogue.SpikeMost) / 2;
                        _sector.Hedged = false;
                        SharedCity.Changed(SectorShare.Part);
                    }
                    break;
                case SectorRules.DeskFailover:
                    DataCentre d = _infra.Centres[0];
                    if (!SectorRules.DownLately(d, day))
                    {
                        d.DownUntil = day + 1;
                        d.Outages++;
                    }
                    break;
            }
        }

        // An offer of the firm's own kind of work from one of the city's clients, as the market would make one.
        private void OfferForTest(FirmPlace firm, int day)
        {
            int kinds = FirmsCatalogue.TakesWork[TalentCatalogue.Clamp(firm.File.Kind, 0, (int)FirmKind.Count - 1)];
            if (kinds == 0 || _firmsFile.Contracts.Count >= SectorCatalogue.ContractsMost)
            {
                return;
            }
            WorkKind kind = SectorRules.KindOf(kinds, TechRandom.Mille(_firmsFile.Seed, day, 72, 99));
            int points = kind == WorkKind.Managed ? 0 : SectorRules.PointsFor(kind, SectorRules.ClientMid, 500);
            _firmsFile.Contracts.Add(new Contract
            {
                Id = _firmsFile.Next(), Place = firm.File.Place, Kind = (int)kind,
                Client = FirmsCatalogue.Clients[TechRandom.Range(_firmsFile.Seed, day, 75, 99, 0, FirmsCatalogue.Clients.Length - 1)],
                Stage = (int)WorkStage.Offered, End = day + FirmsCatalogue.OfferDays, Points = points,
                Value = kind == WorkKind.Managed ? FirmsCatalogue.ManagedMonth : FirmsRules.ValueOf(kind, points, false)
            });
        }

        /// <summary>Each market as it stands: its buyers, the player's share and the leader's, in the log, with the best of them in a notice.</summary>
        public void TestSectorMarkets()
        {
            try
            {
                if (!SectorTestReady())
                {
                    return;
                }
                int best = -1;
                long bestShare = -1;
                for (int m = 0; m < _sector.Markets.Length; m++)
                {
                    SectorMarketDay md = _sector.Markets[m];
                    long mine = md.Buyers > 0 ? md.Mine * 1000 / md.Buyers : 0;
                    RivalProduct lead = md.Leader >= 0 && md.Leader < _sector.Rivals.Count ? _sector.Rivals[md.Leader] : null;
                    Logger?.Info("Tech check (market, " + (SectorMarket)m + "): " + md.Buyers.ToString(TestInv) + " buyers, the player's share " + mine.ToString(TestInv) + " thousandths, "
                                 + md.Rivals + " rival(s)" + (lead != null ? ", " + lead.Name + " leads with " + lead.Customers.ToString(TestInv) : string.Empty) + "; the city "
                                 + (_sector.City.Known ? "from the population" : "documented") + ": PASS");
                    if (mine > bestShare)
                    {
                        bestShare = mine;
                        best = m;
                    }
                }
                Notifications.Show(NotificationType.Success, "reallife_tech_mkt_test_markets_done", Data("rivals", _sector.Rivals.Count.ToString(TestInv),
                    "market", best >= 0 ? TechText.T(SectorViews.MarketKey(best)) : string.Empty, "share", TechText.Mille((int)Math.Max(0L, bestShare)),
                    "people", TechText.Whole(_sector.City.People)), 10f);
            }
            catch (Exception e)
            {
                Fault("the tech markets' test", e);
            }
        }

        /// <summary>A year of the markets lived at once: the rivals' year (the host's or a lone player's), the player's products selling through it, the takings booked today.</summary>
        public void TestSectorYear()
        {
            try
            {
                if (!SectorTestReady())
                {
                    return;
                }
                int day = Save.Day, cycle = HiringGame.Cycle, level = Prices.Level;
                bool following = SharedCity.Follows(SectorShare.Part);
                var takings = new Dictionary<string, long>(8);
                long sold = 0;
                for (int k = 1; k <= GameCalendar.DaysPerYear; k++)
                {
                    SectorPulls(day + k, cycle);
                    if (!following)
                    {
                        SectorRules.RivalDay(_sector, day + k, null);
                    }
                    for (int i = 0; i < _firmsFile.Products.Count; i++)
                    {
                        Product p = _firmsFile.Products[i];
                        FirmPlace firm = p.Live && SectorRules.MarketOf(p) >= 0 ? FirmOf(p.Place) : null;
                        if (firm == null)
                        {
                            continue;
                        }
                        ProductDay r = SectorRules.SellDay(_firmsFile, firm.File, p, firm.Crew, firm.Crew.Support, day + k, _sector.Markets[SectorRules.MarketOf(p)],
                            _appeals[i], _aware[i], HostingRate());
                        takings.TryGetValue(p.Place, out long t);
                        takings[p.Place] = t + r.Takings - r.Hosting - r.Marketing;
                        sold += r.Won;
                    }
                }
                foreach (KeyValuePair<string, long> t in takings)
                {
                    FirmPlace firm = FirmOf(t.Key);
                    if (firm != null && t.Value > 0)
                    {
                        FirmsGame.Takings(t.Value * level / 1000, firm.Reg, firm.File.Name, null);
                    }
                }
                if (!following)
                {
                    SharedCity.Changed(SectorShare.Part);
                }
                ChangedTech();
                Logger?.Info("Tech check (markets, a year): " + takings.Count + " firm(s) sold " + sold.ToString(TestInv) + " new customers through " + GameCalendar.DaysPerYear + " days: PASS");
                Notifications.Show(NotificationType.Success, "reallife_tech_mkt_test_year_done", Data("firms", takings.Count.ToString(TestInv), "won", TechText.Whole(sold)), 10f);
            }
            catch (Exception e)
            {
                Fault("the tech markets' test", e);
            }
        }
    }
}
