using System;
using System.Collections.Generic;
using RealLifeOverhaul;
using RealLifeOverhaul.Economy.Dealership;
using RealLifeOverhaul.Ui;

// The Car Showroom's one sample world: a real DealerState (two showrooms, their cars, offers, plans, warranties, sales, books and the market) that every dealer page reads
// through the same functions the live pages use (DealerFigures, DealerReport, DealerRules), so a head, a count and a chart can only agree with the rows. Day 412 is
// "today" for every tab. Names and prices are placeholders and never ship.
internal static partial class UiSamples
{
    public const int Today = 412, TodayHour = 14, PriceLevel = 1000;

    internal sealed class SampleCar
    {
        public string Name;
        public CarKind Kind;
        public int Sold;   // what one sells for, near enough, when the car goes
        public SampleCar(string name, CarKind kind, int sold)
        {
            Name = name;
            Kind = kind;
            Sold = sold;
        }
    }

    // Every model the showrooms name. The list price is worked out below so that the real worth and asking functions give the price a car stands at.
    private static readonly SampleCar[] CarModels =
    {
        new SampleCar("Meridian GT coupe", CarKind.Sports, 80000), new SampleCar("Atlas pickup", CarKind.Utility, 36000), new SampleCar("Halden sedan", CarKind.Saloon, 26000),
        new SampleCar("Sora hatchback", CarKind.Economy, 19000), new SampleCar("Volta SUV", CarKind.Utility, 41000), new SampleCar("Pinecrest saloon", CarKind.Saloon, 16000),
        new SampleCar("Courier van", CarKind.Van, 33000), new SampleCar("Marlow sedan", CarKind.Saloon, 13500), new SampleCar("Boxer box truck", CarKind.Truck, 49000),
        new SampleCar("Brisa roadster", CarKind.Sports, 21500), new SampleCar("Dune camper van", CarKind.Van, 17500), new SampleCar("Ranger pickup", CarKind.Utility, 24000),
        new SampleCar("Corbel hatchback", CarKind.Economy, 15500), new SampleCar("Anvil SUV", CarKind.Utility, 38000), new SampleCar("Briar wagon", CarKind.Saloon, 23000),
        new SampleCar("Cobalt coupe", CarKind.Sports, 47000), new SampleCar("Drift hatchback", CarKind.Economy, 17000), new SampleCar("Ember sedan", CarKind.Saloon, 29500),
        new SampleCar("Finch van", CarKind.Van, 28000), new SampleCar("Garnet pickup", CarKind.Utility, 31000), new SampleCar("Hale saloon", CarKind.Saloon, 20500),
        new SampleCar("Ibis roadster", CarKind.Sports, 26500), new SampleCar("Jasper SUV", CarKind.Utility, 52500), new SampleCar("Linden coupe", CarKind.Sports, 23000),
        new SampleCar("Kestrel SUV", CarKind.Utility, 36100), new SampleCar("Orion hatchback", CarKind.Economy, 18900), new SampleCar("Tamsin sedan", CarKind.Saloon, 15300),
        new SampleCar("Alder sedan", CarKind.Saloon, 20500), new SampleCar("Quill hatchback", CarKind.Economy, 8800), new SampleCar("Fenn SUV", CarKind.Utility, 33500),
        new SampleCar("Wren roadster", CarKind.Sports, 23500), new SampleCar("Linden pickup", CarKind.Utility, 27500), new SampleCar("Pell hatchback", CarKind.Economy, 6200),
        new SampleCar("Stoat van", CarKind.Van, 25000), new SampleCar("Dale hatchback", CarKind.Economy, 14500), new SampleCar("Orion saloon", CarKind.Saloon, 17500)
    };

    private static SampleCar ModelOf(string name)
    {
        foreach (SampleCar m in CarModels)
        {
            if (m.Name == name)
            {
                return m;
            }
        }
        return CarModels[0];
    }

    // What each car on a lot is worth by the real function: the game's list price is the one that makes the car in front of us stand at the price it was given.
    private static readonly Dictionary<StockCar, int> CarWorths = new Dictionary<StockCar, int>();

    private static StockCar NewCar(DealerState s, string name, int ask, int km, int cond, int boughtAgo, CarSource source, int colour = 0)
    {
        float list = ask / (DealerCatalogue.MarkUp / 1000f) / (DealerRules.WorthShare(km, cond) / 1000f) * 1000f / PriceLevel;
        int worth = DealerRules.Worth(list, PriceLevel, km, cond), asking = DealerRules.Asking(worth);
        var made = new StockCar
        {
            Id = s.Next(), VehicleId = "v" + s.NextId, Type = name, Colour = Colours[colour % Colours.Length], Km = km, Condition = cond, Cost = DealerCatalogue.Named((int)(worth * 0.88f)), Asking = asking,
            Bought = Today - boughtAgo, Source = source, Known = 0, ViewsToday = 1 + (name.Length % 5), ViewsLast = 2 + (name.Length % 7)
        };
        CarWorths[made] = worth;
        return made;
    }

    private static readonly string[] Colours = { "Red", "Black", "White", "Blue", "Grey", "Silver", "Yellow", "Green", "Cream" };

    // The people of the city who buy cars and sign plans; the property samples take their tenants from the same pool.
    private static readonly string[] BuyerNames =
    {
        "Marcus Bell", "Amara Kimani", "Priya Nair", "Theo Brandt", "Lena Hoffmann", "Imani Cole", "Owen Park", "Yuki Tanaka", "Sofia Marchetti", "Daniel Ortiz", "Chen Wei", "Noor Haddad",
        "Ravi Shah", "Zoe Adler", "Ana Souza", "Ben Carter", "Ines Duarte", "Tom Okafor", "Mia Lang", "Luis Pena"
    };

    private static readonly Dictionary<string, DealerState> DealerWorlds = new Dictionary<string, DealerState>();

    /// <summary>The sample world of the Car Showroom for a variant: "std" (two showrooms, a busy week), "empty" (two showrooms and no cars) or "full" (every place taken).</summary>
    private static DealerState DealerWorld(string variant)
    {
        if (DealerWorlds.TryGetValue(variant, out DealerState have))
        {
            return have;
        }
        var s = new DealerState { Seed = 7, FirstDay = 330, LastDay = Today - 1, LastHour = Today * 24 + TodayHour, MarketDay = Today };
        DealerYard pioneer = Yard(s, "10 Pioneer Way", "Pioneer Motors", 12, 340), eastside = Yard(s, "27 Eastside Road", "Eastside Auto", 8, 90);
        int[] tally = { 214, 3, 2, 1, 4, 3, 3, 1, 2, 12, 1, 7 };   // the tally each mission counts (sold, good margins, part exchanges, finance, extras, workshop, events, delivered, quick, the lot's most)
        for (int i = 0; i < DealerCatalogue.Tallies; i++)
        {
            pioneer.Tally[i] = i < tally.Length ? tally[i] : 0;
        }
        pioneer.Tally[DealerCatalogue.TallySold] = 214;
        pioneer.Tally[DealerCatalogue.TallyLot] = 12;
        eastside.Tally[DealerCatalogue.TallySold] = 9;
        eastside.Tally[DealerCatalogue.TallyLot] = 4;
        // A mission is done when its tally has reached its target: the page and the count read the same bits.
        foreach (DealerYard done in s.Yards)
        {
            for (int m = 0; m < DealerCatalogue.Missions; m++)
            {
                done.Done |= DealerRules.Progress(done, m) >= DealerCatalogue.MissionTarget[m] ? 1 << m : 0;
            }
        }
        if (variant == "std" || variant == "new")
        {
            // Pioneer's nine cars and Eastside's four.
            object[][] first =
            {
                new object[] { "Meridian GT coupe", 84500, 12400, 920, 6, CarSource.Used }, new object[] { "Atlas pickup", 38900, 6000, 980, 21, CarSource.Wholesale }, new object[] { "Halden sedan", 27400, 41000, 810, 9, CarSource.Used },
                new object[] { "Sora hatchback", 19800, 3000, 1000, 3, CarSource.Wholesale }, new object[] { "Volta SUV", 44200, 15000, 940, 33, CarSource.Wholesale }, new object[] { "Pinecrest saloon", 16900, 52700, 690, 12, CarSource.Auction },
                new object[] { "Courier van", 34900, 28000, 860, 17, CarSource.Auction }, new object[] { "Marlow sedan", 14300, 88200, 610, 41, CarSource.TradeIn }, new object[] { "Boxer box truck", 52000, 9000, 970, 5, CarSource.Wholesale }
            };
            object[][] second =
            {
                new object[] { "Brisa roadster", 22700, 41300, 800, 14, CarSource.Used }, new object[] { "Dune camper van", 18900, 76500, 660, 27, CarSource.Used }, new object[] { "Ranger pickup", 25600, 5000, 1000, 8, CarSource.Wholesale },
                new object[] { "Corbel hatchback", 15900, 22000, 880, 10, CarSource.Wholesale }
            };
            foreach (object[] c in first)
            {
                StockCar car = NewCar(s, (string)c[0], (int)c[1], (int)c[2], (int)c[3], (int)c[4], (CarSource)c[5], pioneer.Cars.Count);
                car.Spot = pioneer.Cars.Count;
                pioneer.Cars.Add(car);
            }
            foreach (object[] c in second)
            {
                StockCar car = NewCar(s, (string)c[0], (int)c[1], (int)c[2], (int)c[3], (int)c[4], (CarSource)c[5], eastside.Cars.Count + 3);
                car.Spot = eastside.Cars.Count;
                eastside.Cars.Add(car);
            }
            pioneer.Cars[7].ShopUntil = Today * 24 + TodayHour + 20;   // the Marlow sedan is in the workshop
            pioneer.Cars[4].ShopUntil = Today * 24 + TodayHour + 31;
            pioneer.Cars[0].Faults = 0;
        }
        else if (variant == "full")
        {
            string[] names = { "Meridian GT coupe", "Atlas pickup", "Halden sedan", "Sora hatchback", "Volta SUV", "Pinecrest saloon", "Courier van", "Marlow sedan", "Boxer box truck", "Brisa roadster", "Dune camper van", "Ranger pickup",
                "Anvil SUV", "Briar wagon", "Cobalt coupe", "Drift hatchback", "Ember sedan", "Finch van", "Garnet pickup", "Hale saloon" };
            int[] asks = { 84500, 38900, 27400, 19800, 44200, 16900, 34900, 14300, 52000, 22700, 18900, 25600, 41500, 25200, 51000, 18500, 32000, 30500, 33500, 22400 };
            int[] ago = { 6, 21, 9, 3, 33, 12, 17, 41, 5, 14, 27, 8, 11, 19, 4, 15, 23, 7, 30, 2 };
            for (int i = 0; i < names.Length; i++)
            {
                DealerYard to = i < 12 ? pioneer : eastside;
                StockCar car = NewCar(s, names[i], asks[i], 4000 + i * 5300, 700 + (i * 37) % 300, ago[i], i % 3 == 0 ? CarSource.Wholesale : i % 3 == 1 ? CarSource.Used : CarSource.Auction, i);
                car.Spot = to.Cars.Count;
                to.Cars.Add(car);
            }
        }
        if (variant == "std" || variant == "new")
        {
            Rivals(s);
            Deals(s, pioneer, eastside);
            if (variant == "std")
            {
                Plans(s, pioneer, eastside);   // a showroom that has just opened has sold nothing on finance yet
            }
            Market(s);
        }
        else
        {
            Rivals(s);
        }
        SalesAndBooks(s, pioneer, eastside, variant == "std" || variant == "full", variant == "new");
        DealerWorlds[variant] = s;
        return s;
    }

    private static DealerYard Yard(DealerState s, string place, string name, int places, int rep)
    {
        var y = new DealerYard { Place = place, Name = name, Rep = rep };
        for (int i = 0; i < places; i++)
        {
            y.Spots.Add(new LotSpot { X = 100 * i, Y = 0, Z = 0, Yaw = 0 });
        }
        s.Yards.Add(y);
        return y;
    }

    // Each rival asks about what the player asks for the same model: dearer for some, cheaper for others, so the pages say "over yours" and "under yours" from the same figures.
    private static void Rivals(DealerState s)
    {
        object[][] asks =
        {
            new object[] { 0, "Meridian GT coupe", 1.028f }, new object[] { 1, "Atlas pickup", 0.972f }, new object[] { 2, "Sora hatchback", 1.030f }, new object[] { 0, "Courier van", 1.029f },
            new object[] { 1, "Dune camper van", 0.963f }, new object[] { 2, "Ranger pickup", 1.031f }, new object[] { 3, "Volta SUV", 1.04f }, new object[] { 4, "Halden sedan", 0.99f }
        };
        foreach (object[] a in asks)
        {
            int ours = 0;
            for (int y = 0; y < s.Yards.Count; y++)
            {
                for (int c = 0; c < s.Yards[y].Cars.Count; c++)
                {
                    ours = s.Yards[y].Cars[c].Type == (string)a[1] ? s.Yards[y].Cars[c].Asking : ours;
                }
            }
            if (ours > 0)
            {
                s.Rivals.Add(new RivalCar { Rival = (int)a[0], Type = (string)a[1], Ask = DealerCatalogue.Named((int)(ours * (float)a[2])), Day = Today - 3 });
            }
        }
    }

    private static void Deals(DealerState s, DealerYard pioneer, DealerYard eastside)
    {
        // The offers waiting: who, for which of the cars on the lots, and what they offer (the ceiling is never shown).
        object[][] offers =
        {
            new object[] { "Marcus Bell", "Meridian GT coupe", 82000, 0, 4, 2 }, new object[] { "Amara Kimani", "Atlas pickup", 36900, 1, 1, 1 }, new object[] { "Priya Nair", "Halden sedan", 26000, 1, 6, 0 },
            new object[] { "Theo Brandt", "Sora hatchback", 18200, 0, 9, 1 }, new object[] { "Imani Cole", "Courier van", 33600, 2, 3, 0 }, new object[] { "Owen Park", "Volta SUV", 42000, 1, 5, 3 }
        };
        int id = 1;
        foreach (object[] o in offers)
        {
            StockCar car = null;
            for (int c = 0; c < pioneer.Cars.Count; c++)
            {
                car = pioneer.Cars[c].Type == (string)o[1] ? pioneer.Cars[c] : car;
            }
            pioneer.Deals.Add(new DealerOffer
            {
                Id = id++, Car = car.Id, Customer = (string)o[0], Profile = (int)o[3], Offer = (int)o[2], Ceiling = car.Asking, Made = Today * 24 + TodayHour - 5, Expires = Today * 24 + TodayHour + (int)o[4],
                Stage = OfferStage.Open, WantsDrive = (int)o[5] == 2, WantsFinance = (int)o[5] >= 1, Score = 720, Deposit = 20, Term = 56, Looks = (int)o[5]
            });
        }
        // One offer for a car at Eastside.
        StockCar eastCar = eastside.Cars[1];
        eastside.Deals.Add(new DealerOffer
        {
            Id = id++, Car = eastCar.Id, Customer = "Yuki Tanaka", Profile = 1, Offer = eastCar.Asking - 1100, Ceiling = eastCar.Asking, Made = Today * 24 + TodayHour - 2, Expires = Today * 24 + TodayHour + 8,
            Stage = OfferStage.Open, Looks = 1
        });
    }

    private static void Plans(DealerState s, DealerYard pioneer, DealerYard eastside)
    {
        // name, car, owed, weekly, next due, weeks missed, rating score, showroom
        object[][] plans =
        {
            new object[] { "Daniel Ortiz", "Alder sedan", 18200, 900, 418, 0, 720, pioneer }, new object[] { "Lena Hoffmann", "Quill hatchback", 7300, 420, 411, 2, 580, pioneer },
            new object[] { "Sofia Marchetti", "Wren roadster", 11350, 560, 415, 0, 790, pioneer }, new object[] { "Marcus Bell", "Linden pickup", 13500, 670, 417, 0, 650, pioneer },
            new object[] { "Zoe Adler", "Pell hatchback", 4200, 310, 410, 1, 610, pioneer }, new object[] { "Chen Wei", "Stoat van", 9950, 480, 419, 0, 700, eastside },
            new object[] { "Ben Carter", "Ember sedan", 8900, 430, 416, 0, 740, pioneer }, new object[] { "Ravi Shah", "Finch van", 12600, 610, 418, 0, 680, pioneer },
            new object[] { "Mia Lang", "Hale saloon", 5400, 280, 412, 0, 770, eastside }, new object[] { "Luis Pena", "Drift hatchback", 6800, 330, 420, 0, 690, pioneer },
            new object[] { "Noor Haddad", "Jasper SUV", 21400, 1010, 417, 0, 810, pioneer }
        };
        foreach (object[] p in plans)
        {
            s.Plans.Add(new DealerPlan
            {
                Id = s.Next(), Place = ((DealerYard)p[7]).Place, Customer = (string)p[0], Type = (string)p[1], Worth = (int)p[2] * 2, Owed = (int)p[2], Weekly = (int)p[3], Due = (int)p[4], Missed = (int)p[5],
                Paid = 4000, Score = (int)p[6]
            });
        }
        object[][] covers =
        {
            new object[] { "Theo Brandt", "Fenn SUV", 1, 490, 1, 1240, pioneer }, new object[] { "Imani Cole", "Orion saloon", 0, 468, 1, 860, pioneer }, new object[] { "Ana Souza", "Dale hatchback", 2, 520, 0, 0, eastside },
            new object[] { "Tom Okafor", "Garnet pickup", 1, 505, 0, 0, pioneer }
        };
        foreach (object[] c in covers)
        {
            s.Covers.Add(new DealerCover
            {
                Id = s.Next(), Place = ((DealerYard)c[6]).Place, Customer = (string)c[0], Type = (string)c[1], Tier = (int)c[2], Worth = 20000, Condition = 800, Until = (int)c[3], Claims = (int)c[4], Paid = (int)c[5]
            });
        }
    }

    private static void Market(DealerState s)
    {
        // kind, car, colour, km, condition shown, asking, hours left (a lot), leader (a lot: 0 you, 1 and more the rivals)
        object[][] list =
        {
            new object[] { ListingKind.Wholesale, "Volta SUV", "Red", 4, 1000, 38200, 0, -1 }, new object[] { ListingKind.Used, "Marlow sedan", "Black", 88200, 610, 11900, 0, -1 },
            new object[] { ListingKind.Auction, "Courier van", "White", 31800, 860, 29400, 2, 1 }, new object[] { ListingKind.Auction, "Linden coupe", "Blue", 14600, 910, 21800, 5, 0 },
            new object[] { ListingKind.Wholesale, "Atlas pickup", "Grey", 6, 1000, 36400, 0, -1 }, new object[] { ListingKind.Used, "Halden sedan", "Silver", 64900, 720, 18600, 0, -1 },
            new object[] { ListingKind.Used, "Brisa roadster", "Yellow", 41300, 800, 20900, 0, -1 }, new object[] { ListingKind.Auction, "Pinecrest saloon", "Green", 52700, 690, 12300, 7, 1 },
            new object[] { ListingKind.Auction, "Boxer box truck", "White", 97100, 580, 27800, 9, 2 }, new object[] { ListingKind.Wholesale, "Sora hatchback", "Red", 3, 1000, 21200, 0, -1 },
            new object[] { ListingKind.Used, "Marlow sedan", "Blue", 112400, 490, 7900, 0, -1 }, new object[] { ListingKind.Used, "Dune camper van", "Cream", 76500, 660, 16400, 0, -1 },
            new object[] { ListingKind.Wholesale, "Ranger pickup", "Green", 5, 1000, 25100, 0, -1 }
        };
        foreach (object[] r in list)
        {
            var l = new DealerListing
            {
                Id = s.Next(), Kind = (ListingKind)r[0], Type = (string)r[1], Colour = (string)r[2], Km = (int)r[3], Condition = (int)r[4], Shown = (int)r[4], Asking = (int)r[5], Inspected = false
            };
            if (l.Kind == ListingKind.Auction)
            {
                l.Ends = Today * 24 + TodayHour + (int)r[6];
                l.Leader = (int)r[7];
                l.Ceiling = l.Asking + 3000;
            }
            s.Market.Add(l);
        }
        // The Courier van's bidding, newest first: the room's dealers, and the player's earlier bid.
        DealerListing van = s.Market[2];
        van.LogBid(3, Today * 24 + TodayHour - 5, 27500);
        van.LogBid(0, Today * 24 + TodayHour - 3, 28200);
        van.LogBid(2, Today * 24 + TodayHour - 1, 29000);
        van.LogBid(1, Today * 24 + TodayHour, 29400);
        van.Bids = van.BidLogCount;
        DealerListing coupe = s.Market[3];
        coupe.LogBid(2, Today * 24 + TodayHour - 2, 21300);
        coupe.LogBid(0, Today * 24 + TodayHour - 1, 21800);
        coupe.Bids = coupe.BidLogCount;
    }

    // The sales of the last days, newest first, and the books of every day: the books of the days with sales are those sales added up, the older days' are the showroom's usual.
    private static void SalesAndBooks(DealerState s, DealerYard pioneer, DealerYard eastside, bool trading, bool fresh)
    {
        string[] sold = { "Kestrel SUV", "Orion hatchback", "Tamsin sedan", "Alder sedan", "Quill hatchback", "Fenn SUV", "Wren roadster", "Linden pickup", "Pell hatchback", "Stoat van", "Dale hatchback", "Orion saloon" };
        int[] perDay = { 2, 2, 3, 3, 2, 4, 3, 2 };    // Pioneer's sales on day 412, 411 ... 405
        int[] east = { 0, 0, 1, 0, 1, 0, 1, 0 };
        int next = 0;
        if (trading)
        {
            for (int back = 0; back < perDay.Length; back++)
            {
                int day = Today - back;
                for (int yardAt = 0; yardAt < 2; yardAt++)
                {
                    DealerYard yard = yardAt == 0 ? pioneer : eastside;
                    int n = yardAt == 0 ? perDay[back] : east[back];
                    for (int k = 0; k < n; k++)
                    {
                        string type = sold[next % sold.Length];
                        int price = back == 0 && yardAt == 0 && k == 0 ? 36100 : back == 0 && yardAt == 0 && k == 1 ? 18900 : ModelOf(type).Sold + (back * 300 + k * 700) % 1400;
                        int cost = back == 0 && yardAt == 0 && k == 0 ? 29000 : back == 0 && yardAt == 0 && k == 1 ? 17000 : (int)(price * (0.89f - 0.01f * ((next + back) % 4)));
                        s.Sales.Add(new DealerSale
                        {
                            Day = day, Place = yard.Place, Customer = BuyerNames[(next + 3) % BuyerNames.Length], Type = type, Price = price, Cost = cost, Financed = next % 3 == 0, Trade = next % 7 == 6
                        });
                        next++;
                    }
                }
            }
        }
        // The books: every kept day of each showroom.
        for (int yardAt = 0; yardAt < s.Yards.Count; yardAt++)
        {
            DealerYard yard = s.Yards[yardAt];
            for (int back = 0; back <= (fresh ? 0 : DealerCatalogue.ReportDays); back++)   // a showroom that has just opened has today's book and no more
            {
                int day = Today - back;
                DealerBook b = back == 0 ? yard.Today : yard.Past[back - 1];
                b.Clear(day);
                bool recent = trading && back < perDay.Length;
                int h = Mix(day, yardAt);
                int visitors = yardAt == 0 ? 22 + h % 17 : 6 + h % 8;
                if (recent)
                {
                    foreach (DealerSale sale in s.Sales)
                    {
                        if (sale.Day == day && sale.Place == yard.Place)
                        {
                            b.Sold++;
                            b.Revenue += sale.Price;
                            b.Margin += sale.Price - sale.Cost;
                        }
                    }
                }
                else if (trading)
                {
                    int n = yardAt == 0 ? 1 + h % 4 : h % 3 == 0 ? 1 : 0;
                    b.Sold = n;
                    b.Revenue = n * (yardAt == 0 ? 27000 + h % 9000 : 14000 + h % 5000);
                    b.Margin = b.Revenue * (9 + h % 4) / 100;
                }
                b.Visitors = trading || fresh ? visitors : 0;
                b.Served = b.Visitors * 3 / 4;
                b.Offers = trading || fresh ? Math.Max(b.Sold, b.Visitors / 6) : 0;
                b.Takings = b.Revenue + b.Revenue / 20;
                b.Bought = b.Revenue * 85 / 100;
                b.Extras = b.Sold * 450;
                b.Spent = h % 6 == 0 ? 1600 : 0;
                b.Claims = h % 9 == 0 ? 640 : 0;
                b.Cars = yard.Cars.Count;
                b.Age = back == 0 ? 0 : yard.Cars.Count * (11 + h % 6);
                b.Rep = yard.Rep;
            }
            // Today's read-offs from the lot as it stands: the cars' days together, the plans that are behind.
            int age = 0;
            for (int c = 0; c < yard.Cars.Count; c++)
            {
                age += Math.Max(0, Today - yard.Cars[c].Bought);
            }
            yard.Today.Age = age;
            int arrears = 0, late = 0;
            foreach (DealerPlan p in s.Plans)
            {
                if (p.Place == yard.Place && p.Missed > 0)
                {
                    arrears += p.Owed;
                    late++;
                }
            }
            yard.Today.Arrears = arrears;
            yard.Today.Late = late;
            yard.Today.Offers = Math.Max(yard.Today.Offers, yard.Deals.Count);
            if ((trading || fresh) && yardAt == 0)
            {
                yard.Today.Visitors = 31;
                yard.Today.Served = 24;
                yard.Today.Offers = 6;
            }
        }
    }

    private static int Mix(int day, int salt)
    {
        uint x = (uint)(day * 7919 + salt * 104729 + 17);
        x ^= x << 13;
        x ^= x >> 17;
        x ^= x << 5;
        return (int)(x & 0x7fffffff);
    }

    // The showrooms' own words in the sample: a car's name is its type, a grade letter follows the rating.
    private static string GradeLetter(int score) => score >= 800 ? "A" : score >= 700 ? "B" : score >= 600 ? "C" : score >= 500 ? "D" : "E";

    private static int GradeIndex(int score) => score >= 800 ? 4 : score >= 700 ? 3 : score >= 600 ? 2 : score >= 500 ? 1 : 0;
}

// Small words the pages share.
internal static partial class UiSamples
{
    private static string DayWord(UiWords t, int day) => Fx(W(t, "reallife_ui_estate_day", "Day {day}"), "day", day.ToString());

    // The kind of a car by its name, from the sample models; a name the table does not hold is read by its body as the game's table would give it.
    private static CarKind SampleKind(string name)
    {
        foreach (SampleCar m in CarModels)
        {
            if (m.Name == name)
            {
                return m.Kind;
            }
        }
        string n = name.ToLowerInvariant();
        return n.Contains("truck") ? CarKind.Truck : n.Contains("van") ? CarKind.Van : n.Contains("coupe") || n.Contains("roadster") ? CarKind.Sports
            : n.Contains("hatchback") ? CarKind.Economy : n.Contains("suv") || n.Contains("pickup") ? CarKind.Utility : CarKind.Saloon;
    }
}
