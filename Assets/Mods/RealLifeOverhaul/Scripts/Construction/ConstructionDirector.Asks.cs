using System;
using System.Collections.Generic;
using UnityEngine;

namespace RealLifeOverhaul.Construction
{
    /// <summary>
    /// The director's part that asks the rest of the mod (ConstructionBridges): each question through a guard that logs a
    /// fault there once and answers with the bridge's default, so another feature's trouble never stops the construction game.
    /// </summary>
    public sealed partial class ConstructionDirector
    {
        private static readonly HashSet<string> Faulted = new HashSet<string>(StringComparer.Ordinal);

        private static void Faulty(string bridge, Exception e)
        {
            if (Faulted.Add(bridge))
            {
                Logger?.Error(e);
                Logger?.Warn("Construction: the bridge '" + bridge + "' failed; it is answered as if unset from now on in this session.");
            }
        }

        private static bool Down(string bridge) => Faulted.Contains(bridge);

        internal static bool DistrictLives()
        {
            try
            {
                return !Down("DistrictLives") && ConstructionBridges.DistrictLives != null && ConstructionBridges.DistrictLives();
            }
            catch (Exception e)
            {
                Faulty("DistrictLives", e);
                return false;
            }
        }

        internal static void ReadLots(List<LotView> into)
        {
            into.Clear();
            try
            {
                if (!Down("Lots") && ConstructionBridges.Lots != null)
                {
                    ConstructionBridges.Lots(into);
                }
            }
            catch (Exception e)
            {
                into.Clear();
                Faulty("Lots", e);
            }
        }

        internal static int LotsStamp()
        {
            try
            {
                return !Down("LotsStamp") && ConstructionBridges.LotsStamp != null ? ConstructionBridges.LotsStamp() : 0;
            }
            catch (Exception e)
            {
                Faulty("LotsStamp", e);
                return 0;
            }
        }

        internal static bool LotTaken(string lot)
        {
            try
            {
                return LandmarkHeld(lot) || (!Down("LotTaken") && ConstructionBridges.LotTaken != null && ConstructionBridges.LotTaken(lot));
            }
            catch (Exception e)
            {
                Faulty("LotTaken", e);
                return false;
            }
        }

        internal static bool LotBuilt(string lot)
        {
            try
            {
                if (LandmarkBuilt(lot))
                {
                    return true;   // a landmark's site: the stadium stands as the mod's own, the tower and the mall as the district's
                }
                return !Down("LotBuilt") && ConstructionBridges.LotBuilt != null && ConstructionBridges.LotBuilt(lot);
            }
            catch (Exception e)
            {
                Faulty("LotBuilt", e);
                return false;
            }
        }

        internal static bool SizeTaken(string lot, string size)
        {
            try
            {
                return Down("SizeTaken") || ConstructionBridges.SizeTaken == null || ConstructionBridges.SizeTaken(lot, size);
            }
            catch (Exception e)
            {
                Faulty("SizeTaken", e);
                return true;
            }
        }

        internal static double LandPrice(string lot)
        {
            try
            {
                return !Down("LandPrice") && ConstructionBridges.LandPrice != null ? ConstructionBridges.LandPrice(lot) : double.NaN;
            }
            catch (Exception e)
            {
                Faulty("LandPrice", e);
                return double.NaN;
            }
        }

        internal static bool FinishLot(string lot, string design, string size, int version, string type, int storeys, bool player, double price, string look, out string why)
        {
            why = "the district takes no buildings in this version";
            try
            {
                return !Down("Finish") && ConstructionBridges.Finish != null
                       && ConstructionBridges.Finish(lot, design, size, version, type, storeys, player, price, string.IsNullOrEmpty(look) ? null : look, out why);
            }
            catch (Exception e)
            {
                Faulty("Finish", e);
                why = "the district's call failed";
                return false;
            }
        }

        internal static bool FinishOverLot(string lot, IList<string> over, string design, string size, int version, string type, int storeys, bool player, double price,
                                           string look, out string why)
        {
            why = "the district takes no buildings over several spaces in this version";
            try
            {
                return !Down("FinishOver") && ConstructionBridges.FinishOver != null
                       && ConstructionBridges.FinishOver(lot, over, design, size, version, type, storeys, player, price, string.IsNullOrEmpty(look) ? null : look, out why);
            }
            catch (Exception e)
            {
                Faulty("FinishOver", e);
                why = "the district's call failed";
                return false;
            }
        }

        internal static int SpacesOf(string size, int version)
        {
            try
            {
                return !Down("SpacesOf") && ConstructionBridges.SpacesOf != null ? ConstructionBridges.SpacesOf(size, version) : 0;
            }
            catch (Exception e)
            {
                Faulty("SpacesOf", e);
                return 0;
            }
        }

        /// <summary>The designs that fit ground for a type; false while the city's designs are being looked through, or with no copy engine.</summary>
        internal static bool ReadDesigns(float width, float depth, string type, int most, List<DesignChoice> into)
        {
            into.Clear();
            try
            {
                return !Down("Designs") && ConstructionBridges.Designs != null && ConstructionBridges.Designs(width, depth, type, most, into);
            }
            catch (Exception e)
            {
                into.Clear();
                Faulty("Designs", e);
                return false;
            }
        }

        internal static int StartPlotSite(Vector3 middle, int yaw, int width, int depth, int storeys, int contractor, int hours, out string why)
        {
            why = "the city's construction programme is not running";
            try
            {
                return !Down("StartPlot") && ConstructionBridges.StartPlot != null ? ConstructionBridges.StartPlot(middle, yaw, width, depth, storeys, contractor, hours, out why) : 0;
            }
            catch (Exception e)
            {
                Faulty("StartPlot", e);
                return 0;
            }
        }

        internal static int StartLotSite(Vector3 middle, int yaw, int width, int depth, int contractor, int hours, out string why)
        {
            why = "the city's construction programme is not running";
            try
            {
                return !Down("StartLot") && ConstructionBridges.StartLot != null ? ConstructionBridges.StartLot(middle, yaw, width, depth, 0, contractor, hours, out why) : 0;
            }
            catch (Exception e)
            {
                Faulty("StartLot", e);
                return 0;
            }
        }

        internal static int StartWorksSite(string street, int number, int kind, bool mine, int contractor, int hours, out string why)
        {
            why = "the city's construction programme is not running";
            try
            {
                return !Down("StartWorks") && ConstructionBridges.StartWorks != null ? ConstructionBridges.StartWorks(street, number, kind, mine, contractor, hours, out why) : 0;
            }
            catch (Exception e)
            {
                Faulty("StartWorks", e);
                return 0;
            }
        }

        internal static SiteFacts SiteOf(int id)
        {
            try
            {
                return !Down("Site") && ConstructionBridges.Site != null && id > 0 ? ConstructionBridges.Site(id) : default(SiteFacts);
            }
            catch (Exception e)
            {
                Faulty("Site", e);
                return default(SiteFacts);
            }
        }

        internal static void SetContractor(int site, int rival)
        {
            try
            {
                if (!Down("SetContractor"))
                {
                    ConstructionBridges.SetContractor?.Invoke(site, rival);
                }
            }
            catch (Exception e)
            {
                Faulty("SetContractor", e);
            }
        }

        internal static void SetSiteDone(int site, float done)
        {
            try
            {
                if (!Down("SiteDone"))
                {
                    ConstructionBridges.SiteDone?.Invoke(site, done);
                }
            }
            catch (Exception e)
            {
                Faulty("SiteDone", e);
            }
        }

        internal static bool FinishSite(int site)
        {
            try
            {
                return !Down("FinishSite") && ConstructionBridges.FinishSite != null && ConstructionBridges.FinishSite(site);
            }
            catch (Exception e)
            {
                Faulty("FinishSite", e);
                return false;
            }
        }

        internal static bool DropSite(int site)
        {
            try
            {
                return !Down("DropSite") && ConstructionBridges.DropSite != null && ConstructionBridges.DropSite(site);
            }
            catch (Exception e)
            {
                Faulty("DropSite", e);
                return false;
            }
        }

        internal static bool AdvanceSiteNow(int site)
        {
            try
            {
                return !Down("AdvanceSite") && ConstructionBridges.AdvanceSite != null && ConstructionBridges.AdvanceSite(site);
            }
            catch (Exception e)
            {
                Faulty("AdvanceSite", e);
                return false;
            }
        }

        internal static bool GroundClear(Vector3 middle, int yaw, int width, int depth, out float ground)
        {
            ground = middle.y;
            try
            {
                return !Down("Ground") && ConstructionBridges.Ground != null && ConstructionBridges.Ground(middle, yaw, width, depth, out ground);
            }
            catch (Exception e)
            {
                Faulty("Ground", e);
                return false;
            }
        }

        internal static int SiteCount()
        {
            try
            {
                return !Down("SiteCount") && ConstructionBridges.SiteCount != null ? ConstructionBridges.SiteCount() : BuildCatalogue.SitesMost;
            }
            catch (Exception e)
            {
                Faulty("SiteCount", e);
                return BuildCatalogue.SitesMost;
            }
        }

        internal static int Permits()
        {
            try
            {
                return !Down("Permits") && ConstructionBridges.Permits != null ? BuildCatalogue.Clamp(ConstructionBridges.Permits(), -1, 1) : 0;
            }
            catch (Exception e)
            {
                Faulty("Permits", e);
                return 0;
            }
        }

        internal static double BusinessRate()
        {
            try
            {
                return !Down("BusinessRate") && ConstructionBridges.BusinessRate != null ? ConstructionBridges.BusinessRate() : double.NaN;
            }
            catch (Exception e)
            {
                Faulty("BusinessRate", e);
                return double.NaN;
            }
        }

        internal static string BankName()
        {
            try
            {
                return !Down("BankName") && ConstructionBridges.BankName != null ? ConstructionBridges.BankName() : null;
            }
            catch (Exception e)
            {
                Faulty("BankName", e);
                return null;
            }
        }

        internal static void Credit(int kind, long amount)
        {
            try
            {
                if (!Down("Credit"))
                {
                    ConstructionBridges.Credit?.Invoke(kind, (int)Math.Min(int.MaxValue, Math.Max(0L, amount)));
                }
            }
            catch (Exception e)
            {
                Faulty("Credit", e);
            }
        }

        /// <summary>Each material's price on the exchange against its own, in thousandths (1000 where the exchange has none).</summary>
        internal static void Exchange(int[] into)
        {
            for (int m = 0; m < into.Length && m < BuildCatalogue.ExchangeItem.Length; m++)
            {
                double factor = double.NaN;
                try
                {
                    factor = !Down("MaterialFactor") && ConstructionBridges.MaterialFactor != null ? ConstructionBridges.MaterialFactor(BuildCatalogue.ExchangeItem[m]) : double.NaN;
                }
                catch (Exception e)
                {
                    Faulty("MaterialFactor", e);
                }
                into[m] = double.IsNaN(factor) || factor <= 0.0 ? 1000 : BuildCatalogue.Clamp((int)Math.Round(factor * 1000.0), 250, 4000);
            }
        }

        internal static bool Record()
        {
            try
            {
                return !Down("Record") && ConstructionBridges.Record != null && ConstructionBridges.Record();
            }
            catch (Exception e)
            {
                Faulty("Record", e);
                return false;
            }
        }

        internal static bool Lawyer()
        {
            try
            {
                return !Down("Lawyer") && ConstructionBridges.Lawyer != null && ConstructionBridges.Lawyer();
            }
            catch (Exception e)
            {
                Faulty("Lawyer", e);
                return false;
            }
        }

        internal static int Claim(long amount, string claimant, string building)
        {
            try
            {
                return !Down("Claim") && ConstructionBridges.Claim != null ? ConstructionBridges.Claim(amount, claimant, building) : 0;
            }
            catch (Exception e)
            {
                Faulty("Claim", e);
                return 0;
            }
        }

        internal static int ClaimDue(int number)
        {
            try
            {
                return number > 0 && !Down("ClaimDue") && ConstructionBridges.ClaimDue != null ? ConstructionBridges.ClaimDue(number) : -1;
            }
            catch (Exception e)
            {
                Faulty("ClaimDue", e);
                return -1;
            }
        }

        internal static int Bribery(int amount)
        {
            try
            {
                return !Down("Bribery") && ConstructionBridges.Bribery != null ? ConstructionBridges.Bribery(amount) : 0;
            }
            catch (Exception e)
            {
                Faulty("Bribery", e);
                return 0;
            }
        }

        internal static void FlareOpened(string place, string name)
        {
            try
            {
                if (!Down("Opened"))
                {
                    ConstructionBridges.Opened?.Invoke(place, name);
                }
            }
            catch (Exception e)
            {
                Faulty("Opened", e);
            }
        }

        internal static void Standing(int points, bool scandal)
        {
            try
            {
                if (!Down("Standing"))
                {
                    ConstructionBridges.Standing?.Invoke(points, scandal);
                }
            }
            catch (Exception e)
            {
                Faulty("Standing", e);
            }
        }

        internal static bool Storm()
        {
            try
            {
                return !Down("Storm") && ConstructionBridges.Storm != null && ConstructionBridges.Storm();
            }
            catch (Exception e)
            {
                Faulty("Storm", e);
                return false;
            }
        }

        internal static bool Qualified(string role)
        {
            try
            {
                return !Down("Qualified") && ConstructionBridges.Qualified != null && ConstructionBridges.Qualified(role);
            }
            catch (Exception e)
            {
                Faulty("Qualified", e);
                return false;
            }
        }

        internal static bool Commission(string projectId, int storeys, int units, long budget)
        {
            try
            {
                if (Down("Commission") || ConstructionBridges.Commission == null)
                {
                    return false;
                }
                ConstructionBridges.Commission(projectId, storeys, units, budget);
                return true;
            }
            catch (Exception e)
            {
                Faulty("Commission", e);
                return false;
            }
        }

        internal static bool PlayerDesign(string projectId, out int total, out int costPermille, out int storeys, out int units)
        {
            total = 0;
            costPermille = 1000;
            storeys = units = 0;
            try
            {
                return !Down("DesignOf") && ConstructionBridges.DesignOf != null && ConstructionBridges.DesignOf(projectId, out total, out costPermille, out storeys, out units);
            }
            catch (Exception e)
            {
                Faulty("DesignOf", e);
                return false;
            }
        }
    }
}
