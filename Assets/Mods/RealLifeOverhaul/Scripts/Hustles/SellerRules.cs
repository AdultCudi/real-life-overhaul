using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Hustles
{
    /// <summary>What a seller is known for: the saved number of <see cref="Seller.Tag"/>.</summary>
    public enum SellerTag
    {
        None = 0,
        Discount = 1,   // sells at a discount: asks under the going price
        Premium = 2,    // asks over it, for things in better shape than they say
        Trade = 3       // a business clearing used stock: fair prices, many listings
    }

    /// <summary>A person or firm that sells on the Marketplace, the same one day after day. Game-free.</summary>
    public sealed class Seller
    {
        public int Id;
        public string Name = string.Empty;   // a name the city's customers have (DealerRules.CustomerName), or a rival's own
        public int Rating;                    // buyers' rating in tenths, 20 to 50
        public int Tag;                       // SellerTag
        public string Rival = string.Empty;   // the game's rival whose business this is; empty for a person of the city
        public int Sold;                      // things sold through the Marketplace, all told
        public int PersonId;                  // the seller as a person of the city's people pool (0: a catalogue name or a rival's business)
    }

    /// <summary>A rival that owns a business that sells used vehicles or used equipment, as the game's rivals are read.</summary>
    public struct RivalDealer
    {
        public string Name;
        public bool Vehicles;   // owns a dealership or a vehicle workshop
        public bool Machines;   // owns a business that runs on equipment (a restaurant, a bakery, a cafe)
    }

    /// <summary>The sellers of one save, in their own line (<see cref="SellerStore"/>). Game-free.</summary>
    public sealed class SellerFile
    {
        public readonly List<Seller> Sellers = new List<Seller>(SellerRules.PoolSize + SellerRules.RivalsMost);
        public int NextId = 1;

        public Seller Of(int id)
        {
            for (int i = 0; i < Sellers.Count; i++)
            {
                if (Sellers[i].Id == id)
                {
                    return Sellers[i];
                }
            }
            return null;
        }

        public Seller OfRival(string rival)
        {
            for (int i = 0; i < Sellers.Count; i++)
            {
                if (Sellers[i].Rival == rival)
                {
                    return Sellers[i];
                }
            }
            return null;
        }
    }

    /// <summary>
    /// The people and firms behind the Marketplace's listings: a pool of named sellers that stay from day to day, each with a rating and
    /// a tag that really moves their asking price, and the rivals that sell used vehicles and machines where they own such a business.
    /// A rival sells by the same rule as a person: it is a seller of the pool with its own name and tag, a little over or under the going
    /// price. Names come from the same function the listings used before (the city's customers). Game-free (EconomyCheck runs it).
    /// </summary>
    public static class SellerRules
    {
        public const int PoolSize = 24;
        public const int RivalsMost = 6;
        public const int DiscountBp = 1100, PremiumBp = 600;   // basis points under or over the going price a tagged seller asks
        public const int RivalVehicleBp = -300;                // a rival dealer asks a little under; a rival's machines a little over
        public const int RivalMachineBp = 400;
        public const int DiscountShare = 160, PremiumShare = 120, TradeShare = 100;   // per mille of the pool

        /// <summary>The pool of sellers, made up to <see cref="PoolSize"/> persons by the given name function, each with its rating and tag.</summary>
        public static void Ensure(SellerFile f, int seed, Func<int, string> name) => Ensure(f, seed, k => new RealLifeOverhaul.Careers.PoolPerson { Name = name(k) });

        /// <summary>The pool of sellers made up to its size, each a person of the city's people pool (or a catalogue name) by their place in it.</summary>
        public static void Ensure(SellerFile f, int seed, Func<int, RealLifeOverhaul.Careers.PoolPerson> who)
        {
            int persons = 0;
            for (int i = 0; i < f.Sellers.Count; i++)
            {
                persons += string.IsNullOrEmpty(f.Sellers[i].Rival) ? 1 : 0;
            }
            for (int k = persons; k < PoolSize; k++)
            {
                int roll = Mille(seed, k, 1);
                int tag = roll < DiscountShare ? (int)SellerTag.Discount
                    : roll < DiscountShare + PremiumShare ? (int)SellerTag.Premium
                    : roll < DiscountShare + PremiumShare + TradeShare ? (int)SellerTag.Trade : (int)SellerTag.None;
                RealLifeOverhaul.Careers.PoolPerson person = who(k);
                f.Sellers.Add(new Seller { Id = f.NextId++, Name = person.Name, PersonId = person.Id, Rating = Rating(seed, k, tag), Tag = tag });
            }
        }

        private static int Rating(int seed, int k, int tag)
        {
            int baseRating = 30 + Mille(seed, k, 2) * 18 / 1000;   // 3.0 to 4.8
            return Math.Max(20, Math.Min(50, baseRating + (tag == (int)SellerTag.Discount ? -3 : tag == (int)SellerTag.Trade ? 2 : 0)));
        }

        /// <summary>A rival that owns a vehicle dealership or a workshop sells through a seller of its own; made when first seen.</summary>
        public static Seller ForRival(SellerFile f, string rival, int seed)
        {
            Seller s = f.OfRival(rival);
            if (s != null || string.IsNullOrEmpty(rival))
            {
                return s;
            }
            int rivals = 0;
            for (int i = 0; i < f.Sellers.Count; i++)
            {
                rivals += string.IsNullOrEmpty(f.Sellers[i].Rival) ? 0 : 1;
            }
            if (rivals >= RivalsMost)
            {
                return null;
            }
            s = new Seller { Id = f.NextId++, Name = rival, Rival = rival, Tag = (int)SellerTag.Trade, Rating = 30 + Mille(seed,
                rival.Length * 31 + rival[0], 3) * 15 / 1000 };
            f.Sellers.Add(s);
            return s;
        }

        /// <summary>The seller of a listing, by the pick of a roll from 0 up to the pool: persons only,
        /// a rival is chosen by <see cref="RivalListing"/>.</summary>
        public static Seller Pick(SellerFile f, int roll)
        {
            int persons = 0;
            for (int i = 0; i < f.Sellers.Count; i++)
            {
                persons += string.IsNullOrEmpty(f.Sellers[i].Rival) ? 1 : 0;
            }
            if (persons == 0)
            {
                return null;
            }
            int at = Math.Abs(roll) % persons;
            for (int i = 0; i < f.Sellers.Count; i++)
            {
                if (string.IsNullOrEmpty(f.Sellers[i].Rival) && at-- == 0)
                {
                    return f.Sellers[i];
                }
            }
            return null;
        }

        /// <summary>The asking price, whole money, after the seller's tag: a discount seller asks under, a premium seller over.</summary>
        public static int Asking(Seller s, int asking, bool vehicle, bool machine)
        {
            int bp = 0;
            if (s != null)
            {
                bp = s.Tag == (int)SellerTag.Discount ? -DiscountBp : s.Tag == (int)SellerTag.Premium ? PremiumBp : 0;
                if (!string.IsNullOrEmpty(s.Rival))
                {
                    bp = vehicle ? RivalVehicleBp : machine ? RivalMachineBp : 0;
                }
            }
            return Math.Max(1, (int)((long)asking * (10000 + bp) / 10000));
        }

        /// <summary>True when a rival lists a thing today: its own seeded chance, at most one vehicle or machine a day.</summary>
        public static bool RivalListing(int seed, int day, string rival) => Mille(seed ^ (rival ?? string.Empty).Length * 977, day, 4) < 450;

        /// <summary>A listing bought: its seller has sold one more thing (their record on the Marketplace).</summary>
        public static void NoteSale(SellerFile f, Listing l)
        {
            Seller s = f != null && l != null && l.SellerId > 0 ? f.Of(l.SellerId) : null;
            if (s != null)
            {
                s.Sold++;
            }
        }

        /// <summary>The listings of a seller among a day's, for "more from this seller".</summary>
        public static int MoreFrom(IList<Listing> listings, int sellerId, int exceptId, List<Listing> into)
        {
            into.Clear();
            for (int i = 0; i < listings.Count; i++)
            {
                if (listings[i].SellerId == sellerId && listings[i].Id != exceptId && !listings[i].Gone)
                {
                    into.Add(listings[i]);
                }
            }
            return into.Count;
        }

        /// <summary>The category a listing is in for the tabs: vehicles, machines, furniture or stock; electronics and clothes are with furniture,
        /// as household goods.</summary>
        public static int CategoryOf(Listing l, ThingDef def)
        {
            if (l.Car)
            {
                return CategoryVehicles;
            }
            if (def == null)
            {
                return CategoryFurniture;
            }
            return def.Kind == ThingKind.Machine ? CategoryMachines : def.Kind == ThingKind.Stock ? CategoryStock : CategoryFurniture;
        }

        public const int CategoryVehicles = 0, CategoryMachines = 1, CategoryFurniture = 2, CategoryStock = 3, Categories = 4;

        /// <summary>The share a listing is under or over its worth, whole per cent (negative: under).</summary>
        public static int VersusWorth(int asking, int worth) => worth <= 0 ? 0 : (int)Math.Round((asking - worth) * 100.0 / worth);

        private static int Mille(int seed, int a, int b)
        {
            unchecked
            {
                uint x = (uint)seed * 2654435761u ^ (uint)a * 40503u ^ (uint)b * 2246822519u;
                x ^= x >> 15;
                x *= 2246822519u;
                x ^= x >> 13;
                return (int)(x % 1000u);
            }
        }
    }
}
