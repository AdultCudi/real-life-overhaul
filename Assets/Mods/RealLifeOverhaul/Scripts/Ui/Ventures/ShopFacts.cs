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

    /// <summary>An order, open or finished.</summary>
    public sealed class ShopOrderFact
    {
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
        public string AdLine;
        public bool CanAdvertise, CanPay;
    }

    /// <summary>Everything the Web shop pages read, plain.</summary>
    public sealed class ShopFacts
    {
        public bool Ready = true;
        public string OffTitle, OffBody;
        public ShopSiteFact[] Sites = new ShopSiteFact[0];
        public string[] OpenBusinesses = new string[0];
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
    }
}
