using System.Collections.Generic;

namespace RealLifeOverhaul.Economy.Dealership
{
    /// <summary>Where a car of the stock came from.</summary>
    public enum CarSource : byte { Wholesale = 0, Used = 1, Auction = 2, TradeIn = 3 }

    /// <summary>What a listing of the market is.</summary>
    public enum ListingKind : byte { Wholesale = 0, Used = 1, Auction = 2 }

    /// <summary>Where an offer of a customer's stands.</summary>
    public enum OfferStage : byte { Open = 0, Driving = 1 }

    /// <summary>
    /// The car dealership in the save: the player's dealerships with their lots and stock, the day's market, the
    /// customers' offers, and the finance plans and warranties sold. Game-free: the game's objects are read and
    /// written by DealerGame, and what is here is only what a save must keep that the game does not.
    /// </summary>
    public sealed class DealerState
    {
        public int Seed;
        public int FirstDay;
        public int LastHour = -1;           // the last hour worked through (day x 24 + hour)
        public int LastDay = -1;            // the last day closed
        public int NextId = 1;              // the next number for a car, a listing, an offer, a plan or a warranty
        public int MarketDay = -1;          // the day the market's listings are of
        public int NoBidsUntil = -1;        // a lot won and not paid for: no bids until this day
        public readonly List<DealerYard> Yards = new List<DealerYard>(DealerCatalogue.YardsMost);
        public readonly List<DealerListing> Market = new List<DealerListing>(16);
        public readonly List<DealerPlan> Plans = new List<DealerPlan>(16);
        public readonly List<DealerCover> Covers = new List<DealerCover>(16);
        public readonly List<DealerSale> Sales = new List<DealerSale>(DealerCatalogue.SalesKept);

        public DealerYard Yard(string place)
        {
            for (int i = 0; i < Yards.Count; i++)
            {
                if (Yards[i].Place == place)
                {
                    return Yards[i];
                }
            }
            return null;
        }

        public DealerListing Listing(int id)
        {
            for (int i = 0; i < Market.Count; i++)
            {
                if (Market[i].Id == id)
                {
                    return Market[i];
                }
            }
            return null;
        }

        /// <summary>The car with this number, on whichever lot; null if it is none of the stock.</summary>
        public StockCar Car(int id, out DealerYard yard)
        {
            for (int y = 0; y < Yards.Count; y++)
            {
                StockCar car = Yards[y].Car(id);
                if (car != null)
                {
                    yard = Yards[y];
                    return car;
                }
            }
            yard = null;
            return null;
        }

        /// <summary>The car of the game's vehicle with this id, on whichever lot; null if it is none of the stock.</summary>
        public StockCar CarOf(string vehicleId, out DealerYard yard)
        {
            for (int y = 0; vehicleId != null && y < Yards.Count; y++)
            {
                List<StockCar> cars = Yards[y].Cars;
                for (int c = 0; c < cars.Count; c++)
                {
                    if (cars[c].VehicleId == vehicleId)
                    {
                        yard = Yards[y];
                        return cars[c];
                    }
                }
            }
            yard = null;
            return null;
        }

        public int Next()
        {
            int id = NextId;
            NextId = NextId >= int.MaxValue - 1 ? 1 : NextId + 1;
            return id;
        }
    }

    /// <summary>One dealership of the player's: its place, its lot, its stock and offers, and what it has done.</summary>
    public sealed class DealerYard
    {
        public string Place;                // the business's address, as the company writes a place
        public string Name;                 // as the game names it, for the books and the app
        public int StaffFloor = DealerCatalogue.StaffFloor;   // staff close a deal on their own at this share of the asking price
        public bool Finance = true;         // the dealership offers its own finance
        public bool Warranty = true;        // ...and warranties
        public int VisitorCarry;            // thousandths of a visitor carried from hour to hour
        public readonly List<LotSpot> Spots = new List<LotSpot>(DealerCatalogue.SpotsMost);
        public readonly List<StockCar> Cars = new List<StockCar>(DealerCatalogue.CarsMost);
        public readonly List<DealerOffer> Deals = new List<DealerOffer>(DealerCatalogue.DealsMost);
        public DealerBook Today = new DealerBook();
        public readonly DealerBook[] Week = NewWeek();

        public bool Laid => Spots.Count > 0;

        public StockCar Car(int id)
        {
            for (int i = 0; i < Cars.Count; i++)
            {
                if (Cars[i].Id == id)
                {
                    return Cars[i];
                }
            }
            return null;
        }

        public DealerOffer Deal(int id)
        {
            for (int i = 0; i < Deals.Count; i++)
            {
                if (Deals[i].Id == id)
                {
                    return Deals[i];
                }
            }
            return null;
        }

        /// <summary>The first place of the lot no car stands on; -1 if every place is taken (or the lot is not laid out).</summary>
        public int FreeSpot()
        {
            for (int s = 0; s < Spots.Count; s++)
            {
                bool taken = false;
                for (int c = 0; c < Cars.Count && !taken; c++)
                {
                    taken = Cars[c].Spot == s;
                }
                if (!taken)
                {
                    return s;
                }
            }
            return -1;
        }

        public int FreeSpots()
        {
            int free = 0;
            for (int s = 0; s < Spots.Count; s++)
            {
                bool taken = false;
                for (int c = 0; c < Cars.Count && !taken; c++)
                {
                    taken = Cars[c].Spot == s;
                }
                free += taken ? 0 : 1;
            }
            return free;
        }

        private static DealerBook[] NewWeek()
        {
            var week = new DealerBook[DealerCatalogue.WeekBooks];
            for (int i = 0; i < week.Length; i++)
            {
                week[i] = new DealerBook { Day = -1 };
            }
            return week;
        }
    }

    /// <summary>A place on a lot: where a car stands, in the game's world, in centimetres, and which way it faces (degrees).</summary>
    public struct LotSpot
    {
        public int X, Y, Z, Yaw;
    }

    /// <summary>A car of the stock: the game's vehicle by its id, and what the game does not keep of it.</summary>
    public sealed class StockCar
    {
        public int Id;
        public string VehicleId;            // the game's VehicleInstance.id
        public string Type;                 // the game's vehicle type
        public string Colour;               // the game's colour name
        public int Km;
        public int Condition;               // thousandths
        public int Faults;                  // a bit for each fault it has (DealerCatalogue.Faults)
        public int Known;                   // the faults the dealership knows of
        public int Cost;                    // what it cost the dealership, all in
        public int Asking;                  // the price on its windscreen
        public int Bought;                  // the day it came onto the lot
        public CarSource Source;
        public int Spot = -1;               // its place on the lot
        public int OutUntil = -1;           // out on a test drive until this hour
    }

    /// <summary>A car of the day's market: a new one from the wholesaler, one a private owner sells, or a lot of the trade auction.</summary>
    public sealed class DealerListing
    {
        public int Id;
        public ListingKind Kind;
        public string Type;
        public string Colour;
        public int Km;
        public int Condition;               // the truth
        public int Shown;                   // the condition the seller or the catalogue gives
        public int Faults;
        public bool Inspected;
        public int Asking;                  // the price, the owner's asking price, or the current bid of a lot
        public int Floor;                   // the least an owner takes (with an inspection's lever in it once inspected)
        public int Haggles;                 // the player's offers so far
        public int Countered;               // the owner's last counter; 0 for none
        public int Ends = -1;               // a lot: the hour it is knocked down (day x 24 + hour)
        public int Leader = -1;             // a lot: -1 nobody yet, 0 the player, 1 and more another dealer of the room
        public int Ceiling;                 // a lot: the most the room will bid
        public int Bids;                    // a lot: bids made so far
        public bool Gone;                   // sold to someone else, withdrawn or bought: kept for the day so the app can say so
    }

    /// <summary>A customer's offer on a car of a dealership's.</summary>
    public sealed class DealerOffer
    {
        public int Id;
        public int Car;
        public string Customer;
        public int Profile;                 // DealerCatalogue.ProfileShare's kinds
        public int Offer;                   // what they offer now
        public int Ceiling;                 // the most they would pay (never shown)
        public int Counters;                // the player's counters so far
        public int Made;                    // the hour the offer came (day x 24 + hour)
        public int Expires;                 // the hour it lapses
        public OfferStage Stage;
        public bool Drove;                  // has had a test drive
        public bool WantsDrive;             // asks for one first
        public bool WantsFinance;
        public int Score;                   // their credit rating, if they want finance
        public int Deposit;                 // per cent they put down
        public int Term;                    // days they would pay over
        public int Tier = -1;               // the warranty they would take; -1 none
        public string TradeType;            // their own car offered in part exchange; null for none
        public string TradeColour;
        public int TradeKm, TradeCondition, TradeAllowance;
    }

    /// <summary>A car sold on the dealership's own finance: what is still owed, and how it is being paid.</summary>
    public sealed class DealerPlan
    {
        public int Id;
        public string Place;
        public string Customer;
        public string Type;
        public int Worth;                   // the car's worth when sold: what a recovery is measured by
        public int Owed;                    // still to be paid, interest in
        public int Weekly;
        public int Due;                     // the day the next instalment is due
        public int Missed;                  // weeks missed running
        public int Paid;                    // paid so far
        public int Score;
    }

    /// <summary>A warranty sold with a car.</summary>
    public sealed class DealerCover
    {
        public int Id;
        public string Place;
        public string Customer;
        public string Type;
        public int Tier;
        public int Worth;
        public int Condition;
        public int Until;                   // the last day it covers
        public int Claims;
        public int Paid;                    // paid out in claims
        public int RestUntil = -1;
    }

    /// <summary>A car sold, for the app's list.</summary>
    public sealed class DealerSale
    {
        public int Day;
        public string Place;
        public string Customer;
        public string Type;
        public int Price;
        public int Cost;
        public bool Financed;
        public int Tier = -1;
        public bool Trade;                  // sold to the trade, not to a customer
    }

    /// <summary>A dealership's figures of a day.</summary>
    public sealed class DealerBook
    {
        public int Day;
        public int Visitors, Served, Lost, Offers, Sold;
        public int Takings;                 // cars sold, finance paid in, premiums
        public int Bought;                  // cars bought, inspections, fees, preparation
        public int Claims;                  // warranty claims paid out
        public int Margin;                  // sale prices less what the cars sold had cost

        public void Clear(int day)
        {
            Day = day;
            Visitors = Served = Lost = Offers = Sold = Takings = Bought = Claims = Margin = 0;
        }

        public void CopyFrom(DealerBook b)
        {
            Day = b.Day;
            Visitors = b.Visitors;
            Served = b.Served;
            Lost = b.Lost;
            Offers = b.Offers;
            Sold = b.Sold;
            Takings = b.Takings;
            Bought = b.Bought;
            Claims = b.Claims;
            Margin = b.Margin;
        }
    }
}
