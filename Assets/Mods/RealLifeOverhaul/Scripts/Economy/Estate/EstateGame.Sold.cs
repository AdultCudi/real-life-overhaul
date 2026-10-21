using System;
using System.Collections.Generic;
using Buildings;
using Entities;
using Helpers;
using Streets;

namespace RealLifeOverhaul.Economy
{
    /// <summary>
    /// Buildings the player has sold, as the game has them (<see cref="EstateRules.SoldBack"/> says why): a sale is
    /// remembered as the building leaves the player's list, and the same ownership put back afterwards is handed back to its
    /// buyer. Nothing of it runs in a Going Public session, where who owns what is the host's record.
    /// </summary>
    internal static partial class EstateGame
    {
        public const string BoughtKey = "ba:transaction_buildingbought";   // the game's own purchase (BizManPresentation), with the address

        /// <summary>
        /// A building that has just left the player's list: if the game's books have its sale within the days they keep, the
        /// sale is remembered. The day of that sale, or 0 if there is none (the building went some other way: nothing is kept).
        /// </summary>
        private static int SoldOn(EstateState s, EstateHolding h)
        {
            string name = !string.IsNullOrEmpty(h.Name) ? h.Name : NameOf(h.Place);   // (a building on the books from the save, not read since)
            if (GoingPublic.InSession || string.IsNullOrEmpty(name))
            {
                return 0;
            }
            int today = Save != null ? Save.Day : 0;
            Transaction sale = Books.Newest(SoldKey, "address", name, today - EstateCatalogue.SoldLookBack);
            if (sale == null || sale.timestamp.Day < h.BoughtDay)
            {
                return 0;
            }
            Address address = AddressOf(h.Place);
            BuildingRegistration reg = address != null ? BuildingHelper.GetBuildingRegistration(address) : null;
            EstateRules.Remember(s.Sold, h.Place, sale.timestamp.Day, h.BoughtDay, reg != null ? reg.buildingOwnerRivalId : null);
            return sale.timestamp.Day;
        }

        /// <summary>
        /// A sale the agency completed: remembered as the game's own are. Its money came in today.
        /// </summary>
        public static void SoldByAgency(EstateState s, EstateHolding h, int day)
        {
            if (GoingPublic.InSession)
            {
                return;
            }
            Address address = AddressOf(h.Place);
            BuildingRegistration reg = address != null ? BuildingHelper.GetBuildingRegistration(address) : null;
            EstateRules.Remember(s.Sold, h.Place, day, h.BoughtDay, reg != null ? reg.buildingOwnerRivalId : null);
        }

        /// <summary>
        /// The sales in the game's books, within the days they keep, of buildings the player holds again from before them: a
        /// save that came back that way before these sales were remembered. Once a start; how many were found.
        /// </summary>
        public static int SalesInBooks(EstateState s, int today)
        {
            GameInstance save = Save;
            List<RealEstate> owned = save != null ? save.realEstate : null;
            if (GoingPublic.InSession || save == null || save.Transactions == null || owned == null || owned.Count == 0)
            {
                return 0;
            }
            int found = 0;
            int since = today - EstateCatalogue.SoldLookBack;
            foreach (Transaction t in save.Transactions)
            {
                string name;
                if (t == null || t.transactionType != SoldKey || t.timestamp.Day < since || t.transactionData == null || !t.transactionData.TryGetValue("address", out name))
                {
                    continue;
                }
                for (int i = 0; i < owned.Count; i++)
                {
                    RealEstate e = owned[i];
                    if (e == null || e.address == null || e.purchaseDay >= t.timestamp.Day || e.address.ToFormattedString() != name || Hamptons(e.address))
                    {
                        continue;
                    }
                    string place = Place(e.address.streetName, e.address.streetNumber);
                    SoldBuilding known = s.SoldOf(place);
                    if (known == null || known.SoldDay < t.timestamp.Day)
                    {
                        EstateRules.Remember(s.Sold, place, t.timestamp.Day, e.purchaseDay, null);
                        found++;
                    }
                }
            }
            return found;
        }

        // A house in the Hamptons is a home, left to the game (as ReadHoldings leaves it).
        private static bool Hamptons(Address address)
        {
            try
            {
                Building b = BuildingHelper.GetBuilding(address);
                return b != null && b.IsHamptonsHouse();
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Every building remembered as sold that the player holds again: the ownership that was sold (no purchase of it since)
        /// is handed back to its buyer as the sale left it; one bought again is the player's, and forgotten. The names of those
        /// handed back for the first time go into <paramref name="told"/>. How many were handed back.
        /// </summary>
        public static int HandBackSold(EstateState s, List<string> told)
        {
            GameInstance save = Save;
            if (GoingPublic.InSession || save == null || save.realEstate == null || s.Sold.Count == 0)
            {
                return 0;
            }
            int back = 0;
            for (int i = s.Sold.Count - 1; i >= 0; i--)
            {
                SoldBuilding r = s.Sold[i];
                RealEstate e = Estate(r.Place);
                if (e == null || e.address == null)
                {
                    continue;   // still sold: nothing to do (one compare a building remembered)
                }
                string name = e.address.ToFormattedString();
                Transaction bought = Books.Newest(BoughtKey, "address", name, r.SoldDay);
                int again = bought != null ? bought.timestamp.Day : -1;
                if (!EstateRules.SoldBack(r.SoldDay, e.purchaseDay, again))
                {
                    s.Sold.RemoveAt(i);
                    if (Meta.Dev.Verbose)
                    {
                        EstateDirector.Logger?.Info("Estate check (a sold building held again): " + name + " was sold on day " + r.SoldDay
                                                    + " and is the player's again, bought on day " + e.purchaseDay
                                                    + (again >= 0 ? " (a purchase on day " + again + " in the game's books)" : string.Empty)
                                                    + ": a purchase of the player's, kept: PASS");
                    }
                    continue;
                }
                BuildingRegistration reg = BuildingHelper.GetBuildingRegistration(e.address);
                string rival = reg != null && !string.IsNullOrEmpty(reg.buildingOwnerRivalId) ? reg.buildingOwnerRivalId : r.Rival;
                bool listed = Listed(save, e.address);
                Release(save, e, rival);
                back++;
                EstateDirector.Logger?.Info("Estate: " + name + " was sold on day " + r.SoldDay + " and was among the player's buildings again with no purchase since"
                                            + " (its purchase day " + e.purchaseDay + ", before the sale" + (listed ? ", and on the market again" : string.Empty)
                                            + "; another mod that keeps its own buildings puts them back as a save loads): handed back to its buyer as the sale left it."
                                            + " No money moved.");
                if (!r.Told)
                {
                    r.Told = true;
                    told?.Add(name);
                }
            }
            return back;
        }

        private static bool Listed(GameInstance save, Address address)
        {
            for (int i = 0; save.buildingsForSale != null && i < save.buildingsForSale.Count; i++)
            {
                if (save.buildingsForSale[i] != null && save.buildingsForSale[i].address == address)
                {
                    return true;
                }
            }
            return false;
        }
    }
}
