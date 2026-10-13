using System.Collections.Generic;
using RealLifeOverhaul.GameUi;
using UI;
using UnityEngine;

namespace RealLifeOverhaul.Tech
{
    /// <summary>The Tech app's Infra tab: the player's data centres, the warehouses that could be one, and the cloud.</summary>
    public sealed partial class TechApp
    {
        private static readonly int[] CloudPrices = { 700, 850, 1000, 1150, 1300 };
        private int _fitTier = 1;

        private TableSpec WriteInfra(InfoBox box, int day)
        {
            InfraFile f = _director.Infra;
            if (f == null)
            {
                box.Title(TechText.T("reallife_roles_off_title"));
                box.Line(TechText.T("reallife_tech_off_body"));
                _picked = null;
                return TechColumns.Off;
            }
            TableRow cloud = Row(CloudRow);
            cloud.Pinned = true;
            cloud.Set(0, f.Cloud.Open ? f.Cloud.Name : TechText.T("reallife_infra_row_cloud_none"));
            cloud.Set(8, f.Cloud.Open ? TechText.T(f.Cloud.Hyper ? "reallife_infra_state_hyper" : "reallife_infra_state_cloud") : TechText.T("reallife_infra_state_none"));
            DataCentre picked = null;
            string id = Picked(_picked, CentreRow), fit = Picked(_picked, FitRow);
            BuildingRegistration fitting = null;
            for (int i = 0; i < f.Centres.Count; i++)
            {
                DataCentre d = f.Centres[i];
                TableRow row = Row(CentreRow + d.Place);
                row.Set(0, d.Name);
                row.Set(1, HiringGame.District(d.District));
                row.Set(2, TechText.InfraTier(d.Tier), d.Tier);
                row.Set(3, d.Racks.ToString(), d.Racks);
                row.Set(4, d.GpuRacks.ToString(), d.GpuRacks);
                row.Set(5, d.Colo.ToString(), d.Colo);
                int need = InfraRules.TechniciansNeeded(d);
                row.Set(6, d.Technicians + " / " + need, d.Technicians, d.Technicians < need ? TableRow.Red : TableRow.Plain);
                Cents(row, 7, d.Takings30);
                row.Set(8, TechText.T(d.Down(day) ? "reallife_infra_state_down" : !d.Power || !d.Net ? "reallife_infra_state_supply" : "reallife_infra_state_up"),
                    d.Down(day) || !d.Power || !d.Net ? TableRow.Red : TableRow.Plain);
                picked = d.Place == id ? d : picked;
            }
            IReadOnlyList<BuildingRegistration> warehouses = _director.Warehouses;
            for (int i = 0; i < warehouses.Count; i++)
            {
                string place = TalentGame.Place(warehouses[i]);
                if (f.CentreAt(place) != null)
                {
                    continue;
                }
                TableRow row = Row(FitRow + place);
                row.NoTotal = true;
                row.Set(0, TalentGame.NameOf(warehouses[i]));
                row.Set(1, HiringGame.District(InfraGame.District(warehouses[i])));
                row.Set(8, TechText.T("reallife_infra_state_warehouse"));
                fitting = place == fit ? warehouses[i] : fitting;
            }
            if (_picked == CloudRow)
            {
                WriteCloud(box, f, day);
            }
            else if (picked != null)
            {
                WriteCentre(box, picked, day);
            }
            else if (fitting != null)
            {
                WriteFit(box, fitting);
            }
            else
            {
                _picked = null;
                box.Title(TechText.T("reallife_infra_title"));
                if (f.Cloud.Open)
                {
                    box.Line(TechText.Fill("reallife_infra_cloud_line", "sold", f.Cloud.Sold.ToString(), "own", f.Cloud.Own.ToString(), "gpu", f.Cloud.GpuSold.ToString(), "rival", TechText.Mille(f.Cloud.RivalPrice),
                        "regions", InfraRules.Regions(f.Centres).ToString()));
                }
                box.Line(TechText.Fill("reallife_infra_about", "mbps", InfraCatalogue.NetNeeded.ToString(), "per", InfraCatalogue.RacksPerTechnician.ToString()));
                Meta.Guide.Point(box, SectorGuide);
            }
            return TechColumns.Infra;
        }

        private void WriteFit(InfoBox box, BuildingRegistration reg)
        {
            string place = TalentGame.Place(reg);
            int sqm = InfraGame.Sqm(reg);
            box.Title(TechText.T("reallife_infra_fit_title", "name", TalentGame.NameOf(reg)));
            if (box.HasSelector)
            {
                box.Selector(InfraCatalogue.Tiers, _fitTier, i => TechText.Fill("reallife_infra_fit_tier", "tier", TechText.InfraTier(i), "outages", TechText.Mille(InfraCatalogue.OutageChance[i] * 10)), i => Set(() => _fitTier = i));
            }
            box.Line(TechText.Fill("reallife_infra_fit_cost", "sqm", sqm.ToString(), "racks", (sqm / InfraCatalogue.SqmPerRack).ToString(), "cost", TechText.Cents(_director.Level(InfraRules.FitCost(sqm, _fitTier)))));
            Transform buttons = box.ButtonRow();
            int tier = _fitTier;
            InfoBox.Button(buttons, InfoBox.Positive, "reallife_infra_button_fit", () => Do(() =>
            {
                if (_director.FitOut(place, tier))
                {
                    _picked = CentreRow + place;
                }
            }));
            InfoBox.Button(buttons, InfoBox.Additional, "common_close", ClosePick);
            Meta.Guide.Point(box, SectorGuide);
        }

        private void WriteCentre(InfoBox box, DataCentre d, int day)
        {
            string place = d.Place;
            box.Title(TechText.T("reallife_infra_centre_title", "name", d.Name, "tier", TechText.InfraTier(d.Tier)));
            long power = InfraRules.Power(d), water = InfraRules.Water(d);
            box.Line(TechText.Fill("reallife_infra_centre_power", "kwh", (power / 1000).ToString("N0", System.Globalization.CultureInfo.InvariantCulture),
                "water", (water / 1000f).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture), "supplied", TechText.T(d.Power ? "reallife_infra_yes" : "reallife_infra_no"),
                "net", TechText.T(d.Net ? "reallife_infra_yes" : "reallife_infra_no")));
            box.Line(TechText.Fill("reallife_infra_centre_staff", "have", d.Technicians.ToString(), "need", InfraRules.TechniciansNeeded(d).ToString(), "outages", d.Outages.ToString()));
            box.Line(TechText.Fill("reallife_infra_centre_racks", "racks", d.Racks.ToString(), "gpu", d.GpuRacks.ToString(), "room", InfraRules.Room(d).ToString(), "let", d.Colo.ToString(),
                "book", TechText.Cents(_director.Level(d.Book))));
            if (d.Tier < InfraCatalogue.Tiers - 1)
            {
                box.Line(TechText.T("reallife_infra_centre_upgrade", "tier", TechText.InfraTier(d.Tier + 1), "cost", TechText.Cents(_director.Level(InfraRules.UpgradeCost(d.Sqm, d.Tier, d.Tier + 1)))));
            }
            Transform buttons = box.ButtonRow();
            InfoBox.Button(buttons, InfoBox.Positive, "reallife_infra_button_racks", () => Do(() => _director.BuyRacks(place, 5, false)));
            InfoBox.Button(buttons, InfoBox.Additional, "reallife_infra_button_gpu", () => Do(() => _director.BuyRacks(place, 1, true)));
            Transform more = box.ButtonRow();
            if (d.Tier < InfraCatalogue.Tiers - 1)
            {
                int next = d.Tier + 1;
                InfoBox.Button(more, InfoBox.Additional, "reallife_infra_button_upgrade", () => Do(() => _director.Upgrade(place, next)));
            }
            InfoBox.Button(more, InfoBox.Additional, "reallife_infra_button_hire", () =>
            {
                _director.OpenRoles(TechDirector.TabJobs, null);
            });
            InfoBox.Button(more, InfoBox.Additional, "common_close", ClosePick);
            box.Line(TechText.Fill("reallife_infra_centre_prices", "rack", TechText.Cents(_director.Level(InfraCatalogue.RackCost)), "gpu", TechText.Cents(_director.Level(InfraCatalogue.GpuRackCost))));
            Meta.Guide.Point(box, SectorGuide);
        }

        private void WriteCloud(InfoBox box, InfraFile f, int day)
        {
            Cloud c = f.Cloud;
            if (!c.Open)
            {
                int needs = _director.CloudNeeds();
                box.Title(TechText.T("reallife_infra_cloud_title_new"));
                box.Line(needs == 0 ? TechText.T("reallife_infra_cloud_ready") : _director.Needs(needs, true));
                Transform buttons = box.ButtonRow();
                UIs uis = InstanceBehavior<UIs>.Instance;
                if (needs == 0 && uis != null && uis.textInputUi != null)
                {
                    InfoBox.Button(buttons, InfoBox.Positive, "reallife_infra_button_launch", () =>
                        uis.textInputUi.Show("reallife_infra_cloud_name_placeholder", "common_confirm", text => UiClone.Safe(() => Do(() => _director.LaunchCloud(text))), null, string.Empty, false, 30));
                }
                InfoBox.Button(buttons, InfoBox.Additional, "common_close", ClosePick);
                box.Line(TechText.Fill("reallife_infra_cloud_about", "racks", InfraCatalogue.CloudRacksLeast.ToString(), "engineers", InfraCatalogue.CloudEngineersLeast.ToString(),
                    "cash", TechText.Cents(_director.Level(InfraCatalogue.CloudCashLeast)), "licence", TechText.Cents(_director.Level(InfraCatalogue.LicenceFee))));
                Meta.Guide.Point(box, SectorGuide);
                return;
            }
            box.Title(TechText.T("reallife_infra_cloud_title", "name", c.Name));
            box.Line(TechText.Fill("reallife_infra_cloud_state", "reputation", TechText.Mille(c.Reputation), "day", c.LicenceUntil.ToString(),
                "hyper", c.Hyper ? TechText.T("reallife_infra_state_hyper") : _director.Needs(_director.HyperNeeds(), false)));
            box.Line(TechText.Fill("reallife_infra_cloud_line", "sold", c.Sold.ToString(), "own", c.Own.ToString(), "gpu", c.GpuSold.ToString(), "rival", TechText.Mille(c.RivalPrice),
                "regions", InfraRules.Regions(f.Centres).ToString()));
            int at = 2;
            for (int i = 0; i < CloudPrices.Length; i++)
            {
                at = CloudPrices[i] <= c.Price ? i : at;
            }
            if (box.HasSelector)
            {
                box.Selector(CloudPrices.Length, at, i => TechText.Fill("reallife_infra_cloud_price", "share", TechText.Mille(CloudPrices[i]), "unit", TechText.Rate((int)_director.Level(InfraCatalogue.UnitDay * (long)CloudPrices[i] / 1000))),
                    i => Do(() => _director.SetCloudPrice(CloudPrices[i])));
            }
            Transform row = box.ButtonRow();
            if (day >= c.LicenceUntil - 14)
            {
                InfoBox.Button(row, InfoBox.Positive, "reallife_infra_button_licence", () => Do(() => _director.RenewLicence()));
            }
            InfoBox.Button(row, InfoBox.Additional, "common_close", ClosePick);
            Meta.Guide.Point(box, SectorGuide);
        }
    }
}
