using System;
using System.Collections.Generic;
using RealLifeOverhaul.NewBuildings;

namespace RealLifeOverhaul.Uptown
{
    /// <summary>
    /// The district's leased office lots (BuildingsRules.Leased) and the businesses other features run from them
    /// (DistrictBuildings.Lessee: the construction game's eight rival builders). Which lot each lease has is fixed data, known
    /// before the city loads; at the game's late "game loaded" call each lease's business is in its building (LifeGame.Lease:
    /// moved in where the building is new at this load or stands empty to let and nobody's, else there from an earlier load,
    /// the district save keeping its registration), and held off the market. The same on every machine with the same save.
    /// Nothing here runs a frame.
    /// </summary>
    public sealed partial class LifeDirector
    {
        private static readonly string[] LeasedIds = new string[BuildingsRules.LeasedOffices];   // each lease's lot, worked out once a run
        private static readonly List<int> LeasedPick = new List<int>(BuildingsRules.LeasedOffices);
        private static readonly MadeBuilding[] LeasedUp = new MadeBuilding[BuildingsRules.LeasedOffices];   // the building each lease's business stands in at this load
        private static bool _leasesRead;
        private static bool _lesseeFaulted;

        /// <summary>The lot leased to the business of that index, or null: fixed data.</summary>
        public static string LeasedLot(int lease)
        {
            if (!_leasesRead)
            {
                if (Plan.Count == 0)
                {
                    LifeGame.Lots(Plan);
                }
                BuildingsRules.Leased(Plan, LeasedPick);
                for (int i = 0; i < LeasedIds.Length; i++)
                {
                    LeasedIds[i] = i < LeasedPick.Count ? Plan[LeasedPick[i]].Id : null;
                }
                _leasesRead = true;
            }
            return lease >= 0 && lease < LeasedIds.Length ? LeasedIds[lease] : null;
        }

        // The lease a lot is given to, or -1.
        private static int LeaseOf(string lotId)
        {
            for (int i = 0; !string.IsNullOrEmpty(lotId) && i < LeasedIds.Length; i++)
            {
                if (string.Equals(LeasedLot(i), lotId, StringComparison.Ordinal))
                {
                    return i;
                }
            }
            return -1;
        }

        /// <summary>True when the leased business of that index stands at this load; its address as the game writes it.</summary>
        public static bool LeaseStands(int lease, out string address)
        {
            MadeBuilding made = Lives && lease >= 0 && lease < LeasedUp.Length ? LeasedUp[lease] : null;
            address = made != null ? made.Address : null;
            return made != null;
        }

        /// <summary>The game's own destination set at the leased business's door; false when it does not stand.</summary>
        public static bool LeaseDestination(int lease)
        {
            if (!LeaseStands(lease, out _))
            {
                return false;
            }
            LifeGame.Destination(LeasedUp[lease]);
            return true;
        }

        private static void ForgetLeases()
        {
            for (int i = 0; i < LeasedUp.Length; i++)
            {
                LeasedUp[i] = null;
            }
            LifeGame.Unclaim();
        }

        // At the game's late "game loaded" call: each lease's business in its building, held off the market; how many stand,
        // and how many of them took the lease's name again.
        private static int Leases(GameInstance save, out int renamed)
        {
            int up = 0;
            renamed = 0;
            for (int i = 0; i < LeasedUp.Length; i++)
            {
                LeasedUp[i] = null;
                DistrictLease lease = Lessee(i);
                string lot = LeasedLot(i);
                MadeBuilding made = StandingOnLot(lot);
                if (lease == null)
                {
                    continue;   // nobody leases it: an office to let like any other
                }
                if (made == null)
                {
                    Logger?.Info("Uptown: " + lease.Name + " has no office at this load: its lot " + (lot ?? "(none)") + " has no building standing (Player.log says why).");
                    continue;
                }
                string why = LifeGame.Lease(made, lease, i, save, out bool movedIn, out bool named);
                if (why != null)
                {
                    Logger?.Info("Uptown: " + lease.Name + " has no office at this load: the leased office at " + made.Address + " cannot be theirs (" + why + ").");
                    continue;
                }
                LeasedUp[i] = made;
                up++;
                renamed += named ? 1 : 0;
                if (movedIn)
                {
                    Logger?.Info("Uptown: " + lease.Name + " moved into its office at " + made.Address + " (lot " + lot + "), a business of the game's of the type " + lease.BusinessType + ".");
                }
            }
            _stamp++;
            return up;
        }

        // The lessee of a lease as the feature leasing it says; none after a fault (said once).
        private static DistrictLease Lessee(int lease)
        {
            Func<int, DistrictLease> lessee = DistrictBuildings.Lessee;
            if (lessee == null || _lesseeFaulted)
            {
                return null;
            }
            try
            {
                DistrictLease l = lessee(lease);
                return l != null && !string.IsNullOrEmpty(l.BusinessType) && !string.IsNullOrEmpty(l.Name) ? l : null;
            }
            catch (Exception e)
            {
                _lesseeFaulted = true;
                Logger?.Error(e);
                Logger?.Warn("Uptown: the leased offices' lessees could not be read; the offices stand as offices to let for the rest of the session.");
                return null;
            }
        }

        private static MadeBuilding StandingOnLot(string lotId)
        {
            for (int i = 0; lotId != null && i < Standing.Count; i++)
            {
                if (string.Equals(Standing[i].Spec.Id, lotId, StringComparison.Ordinal))
                {
                    return Standing[i];
                }
            }
            return null;
        }
    }
}
