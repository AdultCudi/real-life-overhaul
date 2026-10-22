using System;
using System.Collections.Generic;
using BigAmbitions.Neighborhoods;
using RealLifeOverhaul.Districts;
using RealLifeOverhaul.Economy;
using RealLifeOverhaul.Fame;
using RealLifeOverhaul.Utilities;
using UnityEngine;

namespace RealLifeOverhaul
{
    public static partial class Wiring
    {
        /// <summary>
        /// The district view on the city map (DistrictBridges) with what the features keep by district: the economy's
        /// income (with it off, the level the game's class shares start from) and footfall, Standing, power, water and the
        /// internet, the council, and the crimes reported there. Each is asked once a day at most, when the map opens with
        /// a layer ticked.
        /// </summary>
        public static class DistrictLinks
        {
            private static readonly Action<NewsKind, Vector3> Heard = OnReported;
            private static readonly Action<BuildingRegistration, int, int> BrokenInto = OnBreakIn;

            internal static void Connect()
            {
                DistrictBridges.Income = Income;
                DistrictBridges.Footfall = district => CityEconomy.Running
                    ? CityModel.DistrictFootfall(new CityDistrict { Name = district, Income = Mathf.RoundToInt(CityEconomy.DistrictIncome(district) * 10f) })
                    : (int?)null;
                DistrictBridges.Bears = district => CityEconomy.Running ? CityEconomy.Bears(district) : (int?)null;
                DistrictBridges.Standing = Standing;
                DistrictBridges.Supply = Supply;
                DistrictBridges.Internet = Internet;
                DistrictBridges.Council = Council;
                News.ReportedAt -= Heard;
                News.ReportedAt += Heard;
                BreakIns.Happened -= BrokenInto;
                BreakIns.Happened += BrokenInto;
            }

            internal static void Disconnect()
            {
                DistrictBridges.Reset();
                News.ReportedAt -= Heard;
                BreakIns.Happened -= BrokenInto;
            }

            private static bool Income(string district, out int level, out bool live)
            {
                live = CityEconomy.Running;
                if (live)
                {
                    level = Mathf.RoundToInt(CityEconomy.DistrictIncome(district));
                    return true;
                }
                NeighborhoodData data;
                try
                {
                    data = NeighborhoodHelper.GetData(district);
                }
                catch (Exception)
                {
                    data = null;
                }
                level = data != null ? (CityModel.StartIncome(data.workingClassPercentage, data.middleClassPercentage, data.upperClassClassPercentage) + 5) / 10 : 0;
                return data != null;
            }

            private static bool Standing(string district, out int opinion, out int known)
            {
                FameFile file = FameDirector.Instance != null ? FameDirector.Instance.File : null;
                FameDistrict here = file != null ? file.District(district) : null;
                opinion = here != null ? here.Opinion : FameRules.Start;
                known = here != null ? here.Known : file != null ? FameRules.DistrictKnown(file.Recognition, 0, 0) : 0;
                return file != null;
            }

            private static bool Supply(string district, out int accounts, out int supplied, out int cut)
            {
                accounts = supplied = cut = 0;
                UtilitiesDirector utilities = UtilitiesDirector.Instance;
                UtilitiesState state = utilities != null && Settings.Utilities ? utilities.State : null;
                if (state == null)
                {
                    return false;
                }
                IReadOnlyList<PlaceInfo> places = utilities.Places;
                for (int p = 0; places != null && p < places.Count; p++)
                {
                    BuildingRegistration reg = places[p].Reg;
                    if (reg == null || reg.BuildingCached == null || reg.Neighborhood != district)
                    {
                        continue;
                    }
                    for (int s = (int)Service.Power; s <= (int)Service.Water; s++)
                    {
                        accounts++;
                        if (UtilitiesDirector.Supplied(reg, (Service)s))
                        {
                            supplied++;
                        }
                        Account account = state.Find(places[p].Place, (Service)s);
                        if (account != null && account.State == SupplyState.Cut)
                        {
                            cut++;
                        }
                    }
                }
                return true;
            }

            private static bool Internet(string district, int day, out int providers, out int down, out int fibre, out int readyDay, out int share)
            {
                providers = down = fibre = readyDay = share = 0;
                UtilitiesDirector utilities = UtilitiesDirector.Instance;
                UtilitiesState state = utilities != null && Settings.Utilities ? utilities.State : null;
                for (int p = 0; p <= InternetCatalogue.OwnIndex; p++)
                {
                    if (!InternetRules.Reaches(p, district))
                    {
                        continue;
                    }
                    providers++;
                    int from, hours;
                    if (state != null && InternetRules.Outage(state.Seed, day, p, false, out from, out hours))
                    {
                        down++;
                    }
                }
                Network network = utilities != null && utilities.Company != null ? utilities.Company.Network : null;
                int index = BroadbandRules.District(district);
                if (network != null && index >= 0)
                {
                    if (network.Open(index, day))
                    {
                        fibre = 2;
                        share = network.Share[index, Network.Own];
                    }
                    else if (network.Digging(index))
                    {
                        fibre = 1;
                        readyDay = network.ReadyDay(index);
                    }
                }
                return true;
            }

            private static bool Council(string district, out int sites, out int electorate, out string winner, out int percent)
            {
                sites = electorate = percent = 0;
                winner = null;
                CivicDirector civic = CivicDirector.Instance;
                if (civic == null || !civic.Running)
                {
                    return false;
                }
                if (civic.Works != null)
                {
                    List<SiteState> all = civic.Works.State.Sites;
                    for (int i = 0; i < all.Count; i++)
                    {
                        SiteState site = all[i];
                        if (site == null || site.Finished)
                        {
                            continue;
                        }
                        CityBuildingController at = CivicCity.Find(site.Street, site.Number);
                        if (at != null && at.building != null && at.building.Neighbourhood == district)
                        {
                            sites++;
                        }
                    }
                }
                CivicCity city = civic.City;
                int index = -1;
                for (int i = 0; city != null && i < city.Districts.Count; i++)
                {
                    if (city.Districts[i].Id == district)
                    {
                        index = i;
                        electorate = city.Districts[i].Electorate;
                    }
                }
                CivicGovernment government = civic.Government;
                GovState gov = government != null ? government.State : null;
                if (index >= 0 && gov != null && gov.Phase == ElectionPhase.Counted && gov.CandidateCount > 0)
                {
                    int best = -1, bestVotes = 0, total = 0;
                    for (int c = 0; c < gov.CandidateCount; c++)
                    {
                        int votes = government.DistrictVotes(index, c);
                        total += votes;
                        if (votes > bestVotes)
                        {
                            best = c;
                            bestVotes = votes;
                        }
                    }
                    if (best >= 0 && total > 0)
                    {
                        winner = CivicText.Name(gov.Candidates[best]);
                        percent = Mathf.RoundToInt(bestVotes * 100f / total);
                    }
                }
                return true;
            }

            // Crimes against somebody in the street are noted in their district.
            private static void OnReported(NewsKind kind, Vector3 where)
            {
                if (kind == NewsKind.Crime || kind == NewsKind.CarBreakIn || kind == NewsKind.HitAndRun)
                {
                    DistrictView.Instance?.Report(where);
                }
            }

            private static void OnBreakIn(BuildingRegistration business, int stock, int repair)
            {
                if (business != null && business.BuildingCached != null)
                {
                    DistrictView.Instance?.ReportIn(business.Neighborhood);
                }
            }
        }
    }
}
