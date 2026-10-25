using System.Collections.Generic;

namespace RealLifeOverhaul.Hustles
{
    /// <summary>An address of the city as the game writes it: a street and a number. Game-free.</summary>
    public struct HustlePlace
    {
        public string Street;
        public int Number;

        public HustlePlace(string street, int number)
        {
            Street = street;
            Number = number;
        }

        public bool Is(string street, int number) => Number == number && string.Equals(Street, street, System.StringComparison.Ordinal);
    }

    /// <summary>A thing a person of the city has put up for sale today.</summary>
    public sealed class Listing
    {
        public int Id;
        public string Thing;               // a ThingDef's id, or the game's vehicle type for a car
        public bool Car;
        public string Colour;              // a car's, as the game names its colours
        public int Km;                     // a car's
        public int Price;                  // a car's list price new at the start's prices; goods read theirs from the catalogue
        public int Condition;              // the truth, percent
        public int Shown;                  // what the seller says, percent
        public int Asking;
        public int Floor;                  // the least they take
        public int Haggles;
        public int Countered;              // their last counter; 0 for none
        public string Seller;
        public string Street;
        public int Number;
        public bool Gone;                  // sold to someone else today: kept so the app can say so
    }

    /// <summary>Where a thing of the player's is.</summary>
    public enum OwnedState
    {
        Waiting = 0,       // paid for, at the seller's door to be collected
        Delivering = 1,    // on its way home, there on the morning of <see cref="Owned.Due"/>
        Kept = 2,          // the player's
        Listed = 3         // the player's, up for sale
    }

    /// <summary>What the player means to do with a thing of theirs, at a place.</summary>
    public enum OwnedPlan
    {
        None = 0,
        FixAtHome = 1,     // a repair by hand, at home or at a building of theirs
        CleanAtHome = 2,
        Shop = 3           // taken to the repair shop at <see cref="Owned.Street"/>
    }

    /// <summary>A thing the player bought on the Marketplace, until it is sold.</summary>
    public sealed class Owned
    {
        public int Id;
        public string Thing;
        public bool Car;
        public string Colour;
        public int Km;
        public int Price;                  // a car's list price new at the start's prices
        public int Condition;              // percent; a car's is read from the game's vehicle when it is about
        public int Paid;
        public int Spent;                  // on delivery, parts, repairs and cleaning
        public OwnedState State;
        public OwnedPlan Plan;
        public string Street;              // the seller's while waiting, the repair shop's for that plan
        public int Number;
        public string Seller;
        public int Due;                    // the day a delivery comes
        public string VehicleId;           // a car once collected
        public int Asking;                 // while listed
        public int ListedDay;
        public bool Cleaned;               // a clean is once only
        public int Bought;                 // the day
        public bool Test;                  // made by a test button: selling it moves no money
    }

    /// <summary>A buyer's offer for a thing the player has listed.</summary>
    public sealed class BuyerOffer
    {
        public int Id;
        public int Item;
        public string Buyer;
        public int Offer;
        public int Ceiling;                // never shown
        public int Counters;
        public int Expires;                // an hour stamp: day x 24 + hour
    }

    /// <summary>A thing sold, for the app's record.</summary>
    public sealed class Sale
    {
        public string Thing;
        public bool Car;
        public string Buyer;
        public int Price;
        public int Cost;                   // paid and spent
        public int Day;
    }

    /// <summary>How far a freelance request has come.</summary>
    public enum GigStage
    {
        Offered = 0,       // asked for today; nobody has said yes
        Booked = 1,        // the player said they would come
        Helper = 2,        // a helper was sent
        Loaded = 3,        // a move: loaded at the first address, to be unloaded at the second
        Done = 4,
        Missed = 5,
        Cancelled = 6
    }

    /// <summary>A client's request for a visit, and what came of it.</summary>
    public sealed class Gig
    {
        public int Id;
        public GigKind Kind;
        public string Client;
        public string Street;
        public int Number;
        public string Street2;             // a move's second address
        public int Number2;
        public int Pay;
        public int From;                   // an hour stamp: wanted from
        public int Until;                  // ...and the client waits until
        public GigStage Stage;
        public int HelperFee;
        public int HelperDone;             // an hour stamp: when the helper is finished
        public int HelperRating;
        public int Rating;                 // stars given, once done
        public int Day;
        public bool Late;                  // the player began after the first hour
        public bool Test;
    }

    /// <summary>The Marketplace and freelance work of one save. Game-free; written by <see cref="HustleStore"/>.</summary>
    public sealed class HustleState
    {
        public int Seed = 1;
        public int Day = -1;               // the day the listings and requests are of
        public int LastHour = -1;          // the last hour stamp worked through
        public int NextId = 1;
        public int RepairXp;               // 0 to 100: repairs by hand done
        public int Earned;                 // freelance, all told
        public int Profit;                 // flips, all told
        public int JobsDone;

        public readonly List<Listing> Listings = new List<Listing>(HustleCatalogue.GoodsADay + HustleCatalogue.CarsADay);
        public readonly List<Owned> Items = new List<Owned>(8);
        public readonly List<BuyerOffer> Offers = new List<BuyerOffer>(4);
        public readonly List<Sale> Sales = new List<Sale>(HustleCatalogue.SalesKept);
        public readonly List<Gig> Gigs = new List<Gig>(6);
        public readonly List<int> Ratings = new List<int>(HustleCatalogue.RatingsKept);

        public int Next()
        {
            return NextId++;
        }

        public Listing ListingOf(int id)
        {
            for (int i = 0; i < Listings.Count; i++)
            {
                if (Listings[i].Id == id)
                {
                    return Listings[i];
                }
            }
            return null;
        }

        public Owned Item(int id)
        {
            for (int i = 0; i < Items.Count; i++)
            {
                if (Items[i].Id == id)
                {
                    return Items[i];
                }
            }
            return null;
        }

        public BuyerOffer OfferOf(int id)
        {
            for (int i = 0; i < Offers.Count; i++)
            {
                if (Offers[i].Id == id)
                {
                    return Offers[i];
                }
            }
            return null;
        }

        public BuyerOffer OfferFor(int item)
        {
            for (int i = 0; i < Offers.Count; i++)
            {
                if (Offers[i].Item == item)
                {
                    return Offers[i];
                }
            }
            return null;
        }

        public Gig GigOf(int id)
        {
            for (int i = 0; i < Gigs.Count; i++)
            {
                if (Gigs[i].Id == id)
                {
                    return Gigs[i];
                }
            }
            return null;
        }

        /// <summary>The average of the ratings kept, in thousandths of a star; 0 with none yet.</summary>
        public int Rating
        {
            get
            {
                if (Ratings.Count == 0)
                {
                    return 0;
                }
                int sum = 0;
                for (int i = 0; i < Ratings.Count; i++)
                {
                    sum += Ratings[i];
                }
                return sum * 1000 / Ratings.Count;
            }
        }

        public void Rate(int stars)
        {
            Ratings.Add(HustleCatalogue.Clamp(stars, 1, 5));
            while (Ratings.Count > HustleCatalogue.RatingsKept)
            {
                Ratings.RemoveAt(0);
            }
        }

        public void Sold(Sale sale)
        {
            Sales.Insert(0, sale);
            while (Sales.Count > HustleCatalogue.SalesKept)
            {
                Sales.RemoveAt(Sales.Count - 1);
            }
        }

        /// <summary>The offers waiting for the player's answer.</summary>
        public int Waiting => Offers.Count;
    }
}
