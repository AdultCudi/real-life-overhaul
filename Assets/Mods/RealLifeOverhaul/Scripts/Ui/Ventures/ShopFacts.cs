namespace RealLifeOverhaul.Ui
{
    /// <summary>A district the shop may serve.</summary>
    public struct ShopAreaFact
    {
        public string Name;
        public bool Served;
    }

    /// <summary>A product line of a shop: its price, the going price, the rival price and the week.</summary>
    public sealed class ShopLineFact
    {
        public string Item, Name;
        public long OwnCents, AskCents, GoingCents, RivalCents;
        public int Stock, SoldWeek, MissedWeek;
        public bool On;
    }

    /// <summary>A review as the product page shows it: who, where, how many stars, when, and what the owner answered.</summary>
    public sealed class ShopReviewFact
    {
        public int Index, Stars, Day;
        public string Name, District, Reply;
    }

    /// <summary>One seller of a product on the marketplace: the player or a rival, its price, rating and badge.</summary>
    public sealed class ShopSellerFact
    {
        public string Name;
        public long Cents;
        public int Halves, Reviews, Rival;
        public bool Express, Mine;
    }

    /// <summary>A product of the player as customers see it.</summary>
    public sealed class ShopProductFact
    {
        public string Item, Name, Title, Desc, Arrives;
        public long Cents, WasCents;
        public int PercentOff, Stock, Halves, Count;
        public bool OnlyLeft, Express, On;
        public int[] Breakdown = new int[5];
        public ShopReviewFact[] Reviews = new ShopReviewFact[0];
        public ShopSellerFact[] Sellers = new ShopSellerFact[0];
    }

    /// <summary>A line of a rival catalogue.</summary>
    public struct ShopRivalLine
    {
        public string Name;
        public long Cents;
        public int Halves, Count;
    }

    /// <summary>A rival web shop: its name, rating and the catalogue it lists beside yours.</summary>
    public sealed class ShopRivalFact
    {
        public int Index, Halves, Reviews;
        public string Name;
        public bool Express;
        public ShopRivalLine[] Lines = new ShopRivalLine[0];
    }

    /// <summary>A line of a basket.</summary>
    public struct ShopBasketLine
    {
        public string Name;
        public int Qty;
        public long Cents;
    }

    /// <summary>The basket of the latest order, as the customer saw it: lines, the delivery fee under the free-over amount, and when it arrives.</summary>
    public sealed class ShopBasketFact
    {
        public string Customer, Arrives;
        public ShopBasketLine[] Lines = new ShopBasketLine[0];
        public long Subtotal, Fee, FreeOver, ToFree, Total;
    }

    /// <summary>An order, open or finished.</summary>
    public sealed class ShopOrderFact
    {
        public int Id, StageCode;
        public bool CanRefund, CanSend;
        public string Customer, Items, District, Stage;
        public long Total;
        public int Day;
        public bool Late, Returned, Open;
    }

    /// <summary>A site of the player's web shop with its figures, settings and the lists under it.</summary>
    public sealed class ShopSiteFact
    {
        public string Key, Name, Business, Quality, HostingLine;
        public int Orders, Queue, Overdue, Halves, Reviews;
        public long Takings, Profit, Week;
        public bool Offline, Express;
        public int FeeCents, FreeOverCents, Packing;
        public bool Night, Match, ShowStock;
        public ShopAreaFact[] Areas = new ShopAreaFact[0];
        public long[] Sales30 = new long[0];
        public string[] SalesDays = new string[0];
        public ShopLineFact[] Lines = new ShopLineFact[0];
        public ShopOrderFact[] Open = new ShopOrderFact[0], Done = new ShopOrderFact[0];
        public string AdLine, StoreName;
        public ShopProductFact[] Products = new ShopProductFact[0];
        public ShopRivalFact[] Rivals = new ShopRivalFact[0];
        public ShopBasketFact Basket;
        public bool CanAdvertise, CanPay;
        public int Way, Promise, QualityIndex, ReadyVans;
        public bool CanBetter;
        public string BetterName;
        public long BetterSetUpCents, BetterHostingCents, HostingCents;
    }

    /// <summary>What a site of one quality would cost and bring: the set-up, a month of hosting, the orders a day and, over a building's connection, the most it could take.</summary>
    public struct ShopQuoteFact
    {
        public string Name;
        public long SetUpCents, HostingCents;
        public int PerDay, Most;
    }

    /// <summary>A shop or warehouse of the player with no site yet: where one could be opened.</summary>
    public sealed class ShopCandidateFact
    {
        public string Key, Name, Type;
        public int Mbps;
        public int[] NetMost = new int[0];
    }

    /// <summary>Everything the Web shop pages read, plain.</summary>
    public sealed class ShopFacts
    {
        public bool Ready = true;
        public string OffTitle, OffBody;
        public ShopSiteFact[] Sites = new ShopSiteFact[0];
        public string[] OpenBusinesses = new string[0];
        public ShopCandidateFact[] Candidates = new ShopCandidateFact[0];
        public ShopQuoteFact[] Quotes = new ShopQuoteFact[0];
        public bool InHouse;
        public int MonthDays, SitesMost;
        public string AgencyShare;
    }

    /// <summary>What a button on the Web shop pages does in the game.</summary>
    public interface IShopActions
    {
        void Fee(string place, int cents);
        void FreeOver(string place, int cents);
        void Packing(string place, int packing);
        void Toggle(string place, int which);
        void Area(string place, int district);
        void Price(string place, string item, int cents);
        void Offer(string place, string item, bool on);
        void Pay(string place);
        void Advertise(string place);
        void Rename(string place);
        void Close(string place);
        void Title(string place, string item);
        void Describe(string place, string item);
        void Reply(string place, int note);
        void Refund(string place, int order);
        void Courier(string place, int order);
        void OpenSite(string place, int quality, int way, int promise);
        void Way(string place, int way);
        void Promise(string place, int promise);
        void Better(string place);
    }
}
