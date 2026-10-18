using System;
using System.Collections.Generic;
using RealLifeOverhaul.GameUi;
using UI.Notification;

namespace RealLifeOverhaul.Tech
{
    /// <summary>
    /// The infrastructure part: data centres, the cloud and AI compute, one pass a game day. At the end game's scale it also hires
    /// crews and charges what the scale adds, never past the player's cash.
    /// </summary>
    public sealed partial class TechDirector
    {
        public const int TabInfra = 5;

        private readonly List<BuildingRegistration> _warehouses = new List<BuildingRegistration>(8);
        private InfraFile _infra;
        private long _ownHosting;      // what the player's own firms paid their own cloud today, cents
        private readonly long[] _due = new long[InfraCatalogue.CentresMost * 3];   // each centre's cooling, upkeep and crews at scale today, cents
        private long _opsDue;          // the cloud's operations people's wages at scale today, cents

        /// <summary>The infrastructure part's file; null while the tech sector is off.</summary>
        public InfraFile Infra => _firmsOn ? _infra : null;

        public IReadOnlyList<BuildingRegistration> Warehouses => _warehouses;

        private void StartInfra()
        {
            InfraFile file = InfraStore.Load(Save.modData, out _);
            _infra = file ?? new InfraFile { Seed = new System.Random().Next(1, int.MaxValue), FirstDay = Save.Day };
            ReadInfra();
        }

        private void WriteInfra(Dictionary<string, string> data)
        {
            if (_infra != null && data != null)
            {
                data[InfraStore.Key] = InfraStore.Write(_infra);
            }
        }

        // A data centre whose warehouse is no longer the player's is gone with its racks.
        private void ReadInfra()
        {
            InfraGame.Warehouses(_warehouses);
            for (int i = _infra.Centres.Count - 1; i >= 0; i--)
            {
                DataCentre d = _infra.Centres[i];
                BuildingRegistration reg = TalentGame.Find(d.Place);
                if (reg == null)
                {
                    _infra.Centres.RemoveAt(i);
                    continue;
                }
                d.Name = TalentGame.NameOf(reg);
                d.Technicians = 0;
                for (int p = 0; p < _people.Count; p++)
                {
                    Profile f = _people[p].File;
                    d.Technicians += f.Place == d.Place && f.Def.Family == Family.Infrastructure && f.NoticeEnd == 0 ? 1 : 0;
                }
                d.Power = TechBridges.Supplied == null || TechBridges.Supplied(reg, 0);
                d.Net = TechBridges.Mbps == null || TechBridges.Mbps(reg) >= InfraCatalogue.NetNeeded;
            }
        }

        internal DataCentre CentreOf(string place) => _infra != null ? _infra.CentreAt(place) : null;

        /// <summary>The end game's share today, 0 with its option off; an older save comes to it over <see cref="EndScaleRules.PhaseDays"/>.</summary>
        internal int ScaleNow()
        {
            if (_infra == null || Save == null)
            {
                return 0;
            }
            int today = Save.Day, before = _infra.ScaleFrom;
            _infra.ScaleFrom = EndScaleRules.From(Settings.EndScale, before, today, _infra.Centres.Count > 0 || _infra.Cloud.Open);
            if (EndScaleRules.Begins(before, _infra.ScaleFrom, today))
            {
                TellTech(NotificationType.Info, "reallife_endscale_note_phase", Data("what", TechText.T("reallife_endscale_what_infra"), "days", EndScaleRules.PhaseDays.ToString()),
                    TabInfra, null);
                Logger?.Info("Tech: the end game's scale begins to come in for the data centres and the cloud on day " + today + ", over " + EndScaleRules.PhaseDays + " days.");
            }
            return EndScaleRules.Share(Settings.EndScale, _infra.ScaleFrom, today);
        }

        /// <summary>Test: the data centres and the cloud start coming to scale today, or are at it at once. False with none.</summary>
        internal bool TestScale(bool full)
        {
            if (_infra == null || Save == null || (_infra.Centres.Count == 0 && !_infra.Cloud.Open))
            {
                return false;
            }
            _infra.ScaleFrom = full ? Save.Day - EndScaleRules.PhaseDays : EndScaleRules.Unset;
            ScaleNow();
            ChangedTech();
            return true;
        }

        /// <summary>Charges what the scale adds as far as the cash goes; the rest is owed, and crews leave in the share unpaid.</summary>
        private void Settle(int dated)
        {
            long room = VentureGame.Cash, due = _infra.Owed + _opsDue, paid = 0;
            int count = Math.Min(_infra.Centres.Count, InfraCatalogue.CentresMost);
            for (int i = 0; i < count * 3; i++)
            {
                due += _due[i];
            }
            if (due <= 0)
            {
                return;
            }
            DataCentre home = _infra.Centres.Count > 0 ? _infra.Centres[0] : null;
            BuildingRegistration at = home != null ? TalentGame.Find(home.Place) : null;
            long owed = EndScaleRules.Take(ref room, _infra.Owed);
            FirmsGame.Cost(owed, InfraGame.UpkeepKey, at, home != null ? home.Name : string.Empty, dated);
            paid += owed;
            for (int i = 0; i < count; i++)
            {
                DataCentre d = _infra.Centres[i];
                BuildingRegistration reg = TalentGame.Find(d.Place);
                for (int k = 0; k < 3; k++)
                {
                    long part = EndScaleRules.Take(ref room, _due[i * 3 + k]);
                    FirmsGame.Cost(part, k == 0 ? InfraGame.PowerKey : k == 1 ? InfraGame.UpkeepKey : InfraGame.CrewsKey, reg, d.Name, dated);
                    paid += part;
                }
            }
            long ops = EndScaleRules.Take(ref room, _opsDue);
            FirmsGame.Cost(ops, InfraGame.CrewsKey, at, _infra.Cloud.Name, dated);
            paid += ops;
            bool was = _infra.Owed > 0;
            _infra.Owed = due - paid;
            int unpaid = EndScaleRules.Unpaid(due, paid);
            if (unpaid > 0)
            {
                for (int i = 0; i < count; i++)
                {
                    InfraRules.Leave(_infra.Centres[i], unpaid);
                }
                _infra.Cloud.Ops = EndScaleRules.Leave(_infra.Cloud.Ops, unpaid);
                if (!was)
                {
                    TellTech(NotificationType.Warning, "reallife_endscale_note_owed", Data("what", TechText.T("reallife_endscale_what_infra"), "owed", TechText.Cents(_infra.Owed)),
                        TabInfra, null);
                }
            }
            Array.Clear(_due, 0, _due.Length);
        }

        /// <summary>The share of the scale what is bought is priced at: all of it while its option is on.</summary>
        internal static int BuyScale => EndScaleRules.Buying(Settings.EndScale);

        /// <summary>The cloud is selling to the player's own firms today: open, licensed and with a data centre up.</summary>
        private bool OwnCloud(int day)
        {
            if (_infra == null || !_infra.Cloud.Open || day > _infra.Cloud.LicenceUntil)
            {
                return false;
            }
            for (int i = 0; i < _infra.Centres.Count; i++)
            {
                if (!_infra.Centres[i].Down(day) && InfraRules.Units(_infra.Centres[i]) > 0)
                {
                    return true;
                }
            }
            return false;
        }

        private void InfraDay(int day)
        {
            if (_infra.LastDay >= day)
            {
                return;
            }
            _infra.LastDay = day;
            ReadInfra();
            int cycle = HiringGame.Cycle, level = Prices.Level, outages = 0, share = ScaleNow();
            int wanted = InfraRules.ColoWanted(cycle, share), let = 0;
            int capacity = 0, gpu = 0;
            for (int i = 0; i < _infra.Centres.Count; i++)
            {
                let += _infra.Centres[i].Colo;
            }
            for (int i = 0; i < _infra.Centres.Count; i++)
            {
                DataCentre d = _infra.Centres[i];
                BuildingRegistration reg = TalentGame.Find(d.Place);
                var r = new CentreDay();
                InfraRules.Hire(d, share);
                int cover = InfraRules.Cover(d, share);
                if (InfraRules.Outage(_infra.Seed, d, day, cover))
                {
                    d.DownUntil = day + 1;
                    d.Outages++;
                    outages++;
                    r.Outage = true;
                    TellTech(NotificationType.Warning, d.Power && d.Net ? "reallife_infra_note_outage" : "reallife_infra_note_outage_supply", Data("name", d.Name),
                        TabInfra, TechApp.CentreRow + d.Place);
                }
                // racks are let towards what the city wants, a rack a day; some leave
                int free = Math.Max(0, d.Racks - d.Colo);
                if (!r.Outage && let < wanted && free > d.Racks / 2)
                {
                    d.Colo++;
                    let++;
                }
                else if (d.Colo > 0 && (let > wanted || TechRandom.Mille(_infra.Seed, i, day, 9) < InfraCatalogue.ColoChurn / FirmsCatalogue.MonthDays + (r.Outage ? 300 : 0)))
                {
                    d.Colo--;
                    let--;
                }
                r.Takings = r.Outage ? 0 : InfraRules.ColoDay(d, share) * level / 1000;
                r.Power = Utility(0, InfraRules.Power(d));
                r.Water = Utility(1, InfraRules.Water(d));
                r.WriteDown = InfraRules.WriteDown(d);
                d.Book -= r.WriteDown;
                if (!r.Outage)
                {
                    int working = EndScaleRules.Working(cover);
                    capacity += (int)((long)InfraRules.Units(d, share) * working / 1000);
                    gpu += (int)((long)InfraRules.GpuUnits(d, share) * working / 1000);
                }
                int dated = day - 1;
                FirmsGame.Takings(r.Takings, reg, d.Name, dated);
                FirmsGame.Cost(r.Power, InfraGame.PowerKey, reg, d.Name, dated);
                FirmsGame.Cost(r.Water, InfraGame.WaterKey, reg, d.Name, dated);
                if (i < InfraCatalogue.CentresMost)   // charged by Settle once the day's takings are in
                {
                    _due[i * 3] = Math.Max(0, Utility(0, InfraRules.Power(d, share)) - r.Power);
                    _due[i * 3 + 1] = InfraRules.Upkeep(d, share) * level / 1000;
                    _due[i * 3 + 2] = InfraRules.Crews(d) * level / 1000;
                }
                InfraGame.WriteDown(r.WriteDown * level / 1000);
                InfraRules.Roll(d, day, r.Takings);
            }
            _opsDue = 0;
            Cloud(day, cycle, capacity, gpu, outages, share);
            Settle(day - 1);
            TechBridges.Books?.Invoke(day - 1);
            _ownHosting = 0;
        }

        // A utility's day at the tariff of the city's utilities (power 0, water 1), cents at today's prices.
        private static long Utility(int service, long thousandths)
        {
            if (TechBridges.UtilityCents != null)
            {
                return TechBridges.UtilityCents(service, thousandths);
            }
            return thousandths * (service == 0 ? 15 : 200) / 1000 * Prices.Level / 1000;
        }

        // The cloud's day: the AI labs' training and running compute first, the player's own firms, then the city.
        private void Cloud(int day, int cycle, int capacity, int gpu, int outages, int share)
        {
            Cloud c = _infra.Cloud;
            int level = Prices.Level;
            bool open = c.Open && day <= c.LicenceUntil;
            // short of operations people at the scale, the cloud sells less of what it has
            int ops = open ? InfraRules.OpsNeed((long)capacity + gpu, share) : 0;
            c.Ops = EndScaleRules.Hire(c.Ops, ops, InfraCatalogue.CrewPace);
            int working = EndScaleRules.Working(EndScaleRules.Cover(c.Ops, ops));
            capacity = (int)((long)capacity * working / 1000);
            gpu = (int)((long)gpu * working / 1000);
            // the AI labs train and run their models on the cloud's accelerators while it has them, else rented ones
            long rented = 0;
            for (int i = 0; _firmsOn && i < _firmsFile.Products.Count; i++)
            {
                Product p = _firmsFile.Products[i];
                if (p.K != ProductKind.Model || p.Stage >= 2)
                {
                    continue;
                }
                int want = InfraRules.TrainLevels[TalentCatalogue.Clamp(p.Train, 0, InfraRules.TrainLevels.Length - 1)] + (p.Live ? InfraRules.Inference(p) : 0);
                int own = open ? Math.Min(gpu, want) : 0;
                gpu -= own;
                int bought = want - own;
                p.Compute += InfraRules.TrainLevels[TalentCatalogue.Clamp(p.Train, 0, InfraRules.TrainLevels.Length - 1)];
                long cost = (long)bought * InfraCatalogue.GpuUnitDay * InfraCatalogue.RentedGpuShare / 1000 * level / 1000;
                if (cost > 0)
                {
                    FirmPlace lab = FirmOf(p.Place);
                    FirmsGame.Cost(cost, InfraGame.ComputeKey, lab != null ? lab.Reg : null, lab != null ? lab.File.Name : p.Name, day - 1);
                    rented += cost;
                }
            }
            if (!open)
            {
                c.Sold = c.GpuSold = c.Own = 0;
                if (c.Open && day == c.LicenceUntil + 1)
                {
                    TellTech(NotificationType.Warning, "reallife_infra_note_licence", Data("name", c.Name), TabInfra, TechApp.CloudRow);
                }
                return;
            }
            int ownUnits = (int)Math.Min(capacity, _ownHosting * 1000 / Math.Max(1, (long)InfraCatalogue.UnitDay * InfraCatalogue.OwnHostingShare / 1000 * level));
            int regions = share > 0 ? InfraRules.Regions(_infra.Centres) : 0;
            int city = InfraRules.CityDemand(c, cycle, Math.Max(0, capacity - ownUnits), false, share, regions);
            int cityGpu = InfraRules.CityDemand(c, cycle, Math.Max(0, gpu), true, share, regions);
            long takings = InfraRules.Takings(c, city, cityGpu) * level / 1000 + _ownHosting;
            c.Sold = city + ownUnits;
            c.GpuSold = cityGpu;
            c.Own = ownUnits;
            DataCentre home = _infra.Centres.Count > 0 ? _infra.Centres[0] : null;
            BuildingRegistration at = home != null ? TalentGame.Find(home.Place) : null;
            FirmsGame.Takings(takings, at, c.Name, day - 1);
            _opsDue = (long)c.Ops * InfraCatalogue.OpsWage * level / 1000;
            InfraRules.Reputation(c, outages);
            if (outages > 0 && c.Sold > 0)
            {
                Story("reallife_tech_news_cloud_down", "name", c.Name);
                TechBridges.Fame?.Invoke(0, -1);
            }
            if (InfraRules.PriceWar(c, city, day, InfraRules.WarMarket(c, share, regions)))
            {
                TechBridges.Stock?.Invoke(107, -30);
                Story("reallife_tech_news_price_war", "name", c.Name);
                TellTech(NotificationType.Info, "reallife_infra_note_price_war", Data("name", c.Name, "price", TechText.Mille(c.RivalPrice)), TabInfra, TechApp.CloudRow);
            }
            if (!c.Hyper && HyperNeeds() == 0)
            {
                c.Hyper = true;
                Story("reallife_tech_news_hyperscaler", "name", c.Name);
                TechBridges.Stock?.Invoke(107, -60);
                TechBridges.Stock?.Invoke(106, -30);
                TechBridges.Stock?.Invoke(112, -30);
                TellTech(NotificationType.Success, "reallife_infra_note_hyper", Data("name", c.Name), TabInfra, TechApp.CloudRow);
            }
        }

        /// <summary>The site reliability and DevOps people across the player's tech firms and data centres.</summary>
        internal int Engineers()
        {
            int n = 0;
            for (int i = 0; i < _people.Count; i++)
            {
                int r = _people[i].File.Role;
                n += r == TalentCatalogue.Sre || r == TalentCatalogue.DevOps ? 1 : 0;
            }
            return n;
        }

        /// <summary>A chief technology officer and two staff-level or principal infrastructure people among the player's professionals.</summary>
        internal bool Chiefs()
        {
            bool cto = false;
            int staff = 0;
            for (int i = 0; i < _people.Count; i++)
            {
                Profile p = _people[i].File;
                cto |= p.Role == TalentCatalogue.Cto;
                staff += p.Def.Family == Family.Infrastructure && p.Level >= TalentCatalogue.Staff ? 1 : 0;
            }
            return cto && staff >= 2;
        }

        internal int CloudNeeds() => InfraRules.CloudNeeds(_infra.Centres, Engineers(), VentureGame.Cash * 1000 / Math.Max(1, Prices.Level), NetAll(), BuyScale);

        internal int HyperNeeds() => InfraRules.HyperNeeds(_infra.Centres, Chiefs(), VentureGame.Cash * 1000 / Math.Max(1, Prices.Level), BuyScale);

        private bool NetAll()
        {
            if (_infra.Centres.Count == 0)
            {
                return false;
            }
            for (int i = 0; i < _infra.Centres.Count; i++)
            {
                if (!_infra.Centres[i].Net)
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>A warehouse fitted out as a data centre at a tier, paid now. False if the cash does not cover it.</summary>
        internal bool FitOut(string place, int tier)
        {
            BuildingRegistration reg = TalentGame.Find(place);
            if (reg == null || _infra == null || _infra.CentreAt(place) != null || _infra.Centres.Count >= InfraCatalogue.CentresMost || Save == null)
            {
                return false;
            }
            int sqm = InfraGame.Sqm(reg);
            long cost = InfraRules.FitCost(sqm, tier, BuyScale) * Prices.Level / 1000;
            if (!InfraGame.Pay(cost, InfraGame.FitKey, reg, TalentGame.NameOf(reg), false))
            {
                Notifications.ShowInsufficientMoney();
                return false;
            }
            _infra.Centres.Add(new DataCentre
            {
                Place = place, District = InfraGame.District(reg), Tier = TalentCatalogue.Clamp(tier, 0, InfraCatalogue.Tiers - 1), Sqm = sqm, Fitted = Save.Day
            });
            ReadInfra();
            ChangedTech();
            return true;
        }

        internal bool Upgrade(string place, int tier)
        {
            DataCentre d = CentreOf(place);
            BuildingRegistration reg = TalentGame.Find(place);
            if (d == null || reg == null || tier <= d.Tier || tier >= InfraCatalogue.Tiers)
            {
                return false;
            }
            long cost = InfraRules.UpgradeCost(d.Sqm, d.Tier, tier, BuyScale) * Prices.Level / 1000;
            if (!InfraGame.Pay(cost, InfraGame.FitKey, reg, d.Name, false))
            {
                Notifications.ShowInsufficientMoney();
                return false;
            }
            d.Tier = tier;
            ChangedTech();
            return true;
        }

        /// <summary>Racks bought for a data centre, paid now: capital, written down over five years. At the scale they are of the new kind.</summary>
        internal bool BuyRacks(string place, int racks, bool gpu)
        {
            DataCentre d = CentreOf(place);
            BuildingRegistration reg = TalentGame.Find(place);
            if (d == null || reg == null || racks <= 0 || InfraRules.Room(d) < (gpu ? racks * 2 : racks))
            {
                return false;
            }
            int share = BuyScale;
            long cost = racks * InfraRules.RackPrice(gpu, share) * Prices.Level / 1000;
            if (!InfraGame.Pay(cost, InfraGame.RacksKey, reg, d.Name, false))
            {
                Notifications.ShowInsufficientMoney();
                return false;
            }
            InfraRules.Buy(d, racks, gpu, share);
            ChangedTech();
            return true;
        }

        /// <summary>Racks of the old kind replaced at the new price, servers first. False with none left, no scale or too little cash.</summary>
        internal bool ReplaceRacks(string place)
        {
            DataCentre d = CentreOf(place);
            BuildingRegistration reg = TalentGame.Find(place);
            int share = BuyScale;
            if (d == null || reg == null || share <= 0 || d.Legacy + d.LegacyGpu == 0)
            {
                return false;
            }
            bool gpu = d.Legacy == 0;
            int racks = gpu ? 1 : Math.Min(d.Legacy, InfraRules.RacksStep(share));
            long cost = racks * InfraRules.RackPrice(gpu, share) * Prices.Level / 1000;
            if (!InfraGame.Pay(cost, InfraGame.RacksKey, reg, d.Name, false))
            {
                Notifications.ShowInsufficientMoney();
                return false;
            }
            InfraRules.Replace(d, racks, gpu, share);
            ChangedTech();
            return true;
        }

        /// <summary>The cloud launched: its licence paid for a year, its name the player's. False with something still missing.</summary>
        internal bool LaunchCloud(string name)
        {
            if (_infra == null || Save == null || _infra.Cloud.Open || CloudNeeds() != 0)
            {
                return false;
            }
            DataCentre home = _infra.Centres[0];
            if (!InfraGame.Pay(Level(InfraRules.Licence(BuyScale)), InfraGame.LicenceKey, TalentGame.Find(home.Place), name, true))
            {
                Notifications.ShowInsufficientMoney();
                return false;
            }
            Cloud c = _infra.Cloud;
            c.Open = true;
            c.Name = string.IsNullOrEmpty(name) ? TechText.T("reallife_infra_cloud_default") : name;
            c.Launched = Save.Day;
            c.LicenceUntil = Save.Day + GameCalendar.DaysPerYear;
            Story("reallife_tech_news_cloud", "name", c.Name);
            TechBridges.Stock?.Invoke(107, -20);
            ChangedTech();
            return true;
        }

        internal bool RenewLicence()
        {
            Cloud c = _infra != null ? _infra.Cloud : null;
            if (c == null || !c.Open || _infra.Centres.Count == 0)
            {
                return false;
            }
            if (!InfraGame.Pay(Level(InfraRules.Licence(BuyScale)), InfraGame.LicenceKey, TalentGame.Find(_infra.Centres[0].Place), c.Name, true))
            {
                Notifications.ShowInsufficientMoney();
                return false;
            }
            c.LicenceUntil = Math.Max(c.LicenceUntil, Save.Day) + GameCalendar.DaysPerYear;
            ChangedTech();
            return true;
        }

        internal void SetCloudPrice(int thousandths)
        {
            if (_infra != null && _infra.Cloud.Open)
            {
                _infra.Cloud.Price = TalentCatalogue.Clamp(thousandths, 500, 2000);
                ChangedTech();
            }
        }

        internal void SetTraining(int product, int level)
        {
            Product p = _firmsOn ? _firmsFile.ProductOf(product) : null;
            if (p != null && p.K == ProductKind.Model)
            {
                p.Train = TalentCatalogue.Clamp(level, 0, InfraRules.TrainLevels.Length - 1);
                ChangedTech();
            }
        }

        /// <summary>The steps of the way up after the firms' own (a data centre, a cloud, a hyperscaler, an AI lab), for the Firms tab.</summary>
        internal void PathMore(InfoBox box)
        {
            if (_infra == null)
            {
                return;
            }
            int cloud = CloudNeeds(), hyper = HyperNeeds();
            TechApp.Step(box, _infra.Centres.Count > 0, "reallife_tech_path_centre", null);
            TechApp.Step(box, _infra.Cloud.Open, "reallife_tech_path_cloud", _infra.Cloud.Open ? null : Needs(cloud, true));
            TechApp.Step(box, _infra.Cloud.Hyper, "reallife_tech_path_hyper", _infra.Cloud.Hyper ? null : Needs(hyper, false));
            bool lab = false;
            for (int i = 0; _firmsOn && i < _firmsFile.Products.Count; i++)
            {
                lab |= _firmsFile.Products[i].K == ProductKind.Model && _firmsFile.Products[i].Live;
            }
            TechApp.Step(box, lab, "reallife_tech_path_ai", null);
        }

        private static readonly string[] CloudNeedKeys =
        {
            "reallife_infra_need_racks", "reallife_infra_need_tier", "reallife_infra_need_engineers", "reallife_infra_need_cash", "reallife_infra_need_net"
        };

        private static readonly string[] HyperNeedKeys =
        {
            "reallife_infra_need_regions", "reallife_infra_need_hyper_racks", "reallife_infra_need_chiefs", "reallife_infra_need_hyper_cash"
        };

        /// <summary>What a step still needs, in words; <paramref name="bits"/> from <see cref="CloudNeeds"/> or <see cref="HyperNeeds"/>.</summary>
        internal string Needs(int bits, bool cloud)
        {
            var words = new System.Text.StringBuilder(96);
            int share = BuyScale;
            string[] keys = cloud ? CloudNeedKeys : HyperNeedKeys;
            for (int i = 0; i < keys.Length; i++)
            {
                if ((bits & (1 << i)) != 0)
                {
                    words.Append(words.Length > 0 ? ", " : string.Empty).Append(TechText.Fill(keys[i],
                        "racks", (cloud ? InfraRules.CloudRacksLeast(share) : InfraRules.HyperRacks(share)).ToString(),
                        "engineers", InfraCatalogue.CloudEngineersLeast.ToString(),
                        "cash", TechText.Cents(Level(cloud ? InfraRules.CloudCashLeast(share) : InfraRules.HyperCash(share))),
                        "regions", InfraCatalogue.HyperRegions.ToString(), "mbps", InfraCatalogue.NetNeeded.ToString()));
                }
            }
            return TechText.T("reallife_tech_path_needs", "what", words.ToString());
        }
    }
}
