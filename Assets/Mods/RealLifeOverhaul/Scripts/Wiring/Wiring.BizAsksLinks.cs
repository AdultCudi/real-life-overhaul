using System;
using RealLifeOverhaul.Economy;
using RealLifeOverhaul.Economy.Dealership;
using RealLifeOverhaul.ParkSpaces;
using RealLifeOverhaul.Rivals;

namespace RealLifeOverhaul
{
    public static partial class Wiring
    {
        /// <summary>
        /// The smaller business asks with the rest of the mod:
        ///
        ///   Parking spaces (ParkSpaces/SpaceBridges)
        ///     RivalCount, RivalWorth,
        ///     RivalFloor, RivalMoney,
        ///     RivalName            the rival tycoons buy and sell a space from their own worth, by the player's rules
        ///     SpaceLine            the tycoons' line on the rich list and their panel
        ///
        ///   The showroom's suppliers (Economy/Dealer/Supply, CarSupplyBridges)
        ///     Manages              a manager of the company with the Stock orders task, able to decide today, fills a showroom's standing order
        ///
        /// Asked on a day's pass, a morning or a press, never per frame; a fault is said once (<see cref="Fault"/>) and the bridge's default answered.
        /// </summary>
        public static class BizAsksLinks
        {
            internal static void Connect()
            {
                SpaceBridges.RivalCount = () => RivalCatalogue.TycoonCount;
                SpaceBridges.RivalWorth = RivalWorth;
                SpaceBridges.RivalFloor = RivalFloor;
                SpaceBridges.RivalMoney = RivalMoney;
                SpaceBridges.RivalName = RivalName;
                RivalBridges.SpaceLine = tycoon => SpaceDirector.Instance != null ? SpaceDirector.Instance.RivalLine(tycoon) : string.Empty;
                CarSupplyBridges.Manages = Manages;
            }

            internal static void Disconnect()
            {
                SpaceBridges.Reset();
                RivalBridges.SpaceLine = null;
                CarSupplyBridges.Reset();
            }

            private static Tycoon Of(int index)
            {
                RivalFile f = RivalDirector.Instance != null ? RivalDirector.Instance.File : null;
                return f != null && index >= 0 && index < f.Tycoons.Length ? f.Tycoons[index] : null;
            }

            // A tycoon's worth while they are forward and the rivals are not resting (a Going Public session); -1 otherwise.
            private static long RivalWorth(int index)
            {
                try
                {
                    Tycoon t = GoingPublic.InSession ? null : Of(index);
                    return t != null && t.Active ? t.Worth : -1L;
                }
                catch (Exception e)
                {
                    Fault("BizAsksLinks.RivalWorth", e);
                    return -1L;
                }
            }

            // The worth a tycoon is raised to each day (RivalRules.Grow): a space bought from under it would be made up the next day, so none is.
            private static long RivalFloor(int index)
            {
                try
                {
                    Tycoon t = Of(index);
                    return t != null ? Rivals.RivalRules.At(t.Def.StartWorth, Prices.Level) : 0L;
                }
                catch (Exception e)
                {
                    Fault("BizAsksLinks.RivalFloor", e);
                    return long.MaxValue / 4;
                }
            }

            private static void RivalMoney(int index, long amount)
            {
                try
                {
                    Tycoon t = Of(index);
                    if (t != null && t.Active)
                    {
                        t.Worth = Math.Max(0L, t.Worth + amount);
                    }
                }
                catch (Exception e)
                {
                    Fault("BizAsksLinks.RivalMoney", e);
                }
            }

            private static string RivalName(int index)
            {
                try
                {
                    return index >= 0 && index < RivalCatalogue.TycoonCount ? RivalCatalogue.Tycoons[index].Name : string.Empty;
                }
                catch (Exception e)
                {
                    Fault("BizAsksLinks.RivalName", e);
                    return string.Empty;
                }
            }

            private static bool Manages(string place)
            {
                try
                {
                    CompanyDirector company = CompanyDirector.Instance;
                    return company != null && company.Decides(place, ManagerTask.Stock);   // the same door as every other decision: off sick with nobody standing in, it waits
                }
                catch (Exception e)
                {
                    Fault("BizAsksLinks.Manages", e);
                    return false;
                }
            }
        }
    }
}
