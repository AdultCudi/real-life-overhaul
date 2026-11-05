using System;
using System.Collections.Generic;
using RealLifeOverhaul.Ui;

// The Web shop's samples: a bakery site and a warehouse site with orders, a catalogue and thirty days of sales. Names are placeholders and never ship.
internal static partial class UiSamples
{
    public static readonly string[] ShopStates = { "hub", "hub_offline", "orders", "orders_pick", "orders_empty", "catalogue", "catalogue_pick", "store", "store_search", "store_product", "store_rival", "store_basket", "empty", "off" };

    /// <summary>A recording stand-in for the director: each press says which action it was.</summary>
    public sealed class ShopStub : IShopActions
    {
        public string Last;
        public int Count;

        private void Did(string what)
        {
            Last = what;
            Count++;
        }

        public void Fee(string place, int cents) => Did("fee");
        public void FreeOver(string place, int cents) => Did("free_over");
        public void Packing(string place, int packing) => Did("packing");
        public void Toggle(string place, int which) => Did("toggle");
        public void Area(string place, int district) => Did("area");
        public void Price(string place, string item, int cents) => Did("price");
        public void Offer(string place, string item, bool on) => Did("offer");
        public void Pay(string place) => Did("pay");
        public void Advertise(string place) => Did("advertise");
        public void Rename(string place) => Did("rename");
        public void Close(string place) => Did("close");
        public void Title(string place, string item) => Did("title");
        public void Describe(string place, string item) => Did("describe");
        public void Reply(string place, int note) => Did("reply");
        public void Refund(string place, int order) => Did("refund");
        public void Courier(string place, int order) => Did("courier");
    }

    public static VentModel Shop(UiWords t, string state, VentView view = null, IShopActions act = null)
    {
        VentWords w = VentWordsOf(t);
        ShopFacts f = ShopFactsOf(state);
        view = view ?? new VentView();
        switch (state)
        {
            case "orders":
            case "orders_empty": view.Tab = ShopPages.TabOrders; break;
            case "orders_pick": view.Tab = ShopPages.TabOrders; view.Pick = ShopPages.OrderKey + 2; break;
            case "catalogue": view.Tab = ShopPages.TabCatalogue; break;
            case "store": view.Tab = ShopPages.TabStore; break;
            case "store_search": view.Tab = ShopPages.TabStore; view.Query = "loaf"; break;
            case "store_product": view.Tab = ShopPages.TabStore; view.Pick = ShopPages.ProductKey + "Sourdough loaf"; break;
            case "store_rival": view.Tab = ShopPages.TabStore; view.Pick = ShopPages.RivalKey + 0; break;
            case "store_basket": view.Tab = ShopPages.TabStore; view.Pick = ShopPages.BasketKey; break;
            case "catalogue_pick": view.Tab = ShopPages.TabCatalogue; view.Pick = ShopPages.LineKey + "Sourdough loaf"; break;
        }
        var m = new VentModel();
        ShopPages.Fill(m, view, f, act ?? new ShopStub(), w);
        return m;
    }

    private static ShopFacts ShopFactsOf(string state)
    {
        if (state == "off")
        {
            return new ShopFacts { Ready = false };
        }
        if (state == "empty")
        {
            return new ShopFacts();
        }
        var sales = new long[30];
        var days = new string[30];
        for (int i = 0; i < 30; i++)
        {
            sales[i] = 52000 + i * 1900 + (i % 5) * 3100;
            days[i] = (372 + i).ToString();
        }
        ShopOrderFact[] open =
        {
            new ShopOrderFact { Id = 1, Customer = "Adele Ashdown", Items = "2 x Sourdough loaf, 1 x Croissant", District = "Midtown", Total = 1450, Stage = "Packing", Open = true, CanRefund = true },
            new ShopOrderFact { Id = 2, Customer = "Hugo Lindqvist", Items = "6 x Croissant", District = "Garment District", Total = 2160, Stage = "Waiting for a van", Open = true, CanRefund = true, CanSend = true, StageCode = 1 },
            new ShopOrderFact { Id = 3, Customer = "Thea Okafor", Items = "1 x Sourdough loaf", District = "Hell's Kitchen", Total = 650, Stage = "On its way", Open = true, CanRefund = true, StageCode = 2 }
        };
        ShopOrderFact[] done =
        {
            new ShopOrderFact { Customer = "Marcus Fenwick", Items = "3 products", District = "Midtown", Total = 1890, Day = 400 },
            new ShopOrderFact { Customer = "Nadia Garrow", Items = "2 products", District = "Murray Hill", Total = 1240, Day = 400, Late = true },
            new ShopOrderFact { Customer = "Victor Marlowe", Items = "1 products", District = "Midtown", Total = 650, Day = 399, Returned = true }
        };
        var lines = new[]
        {
            new ShopLineFact { Item = "Sourdough loaf", Name = "Sourdough loaf", AskCents = 650, GoingCents = 620, RivalCents = 580, Stock = 42, SoldWeek = 118, MissedWeek = 3, On = true },
            new ShopLineFact { Item = "Croissant", Name = "Croissant", AskCents = 360, GoingCents = 350, RivalCents = 330, Stock = 0, SoldWeek = 204, MissedWeek = 21, On = true },
            new ShopLineFact { Item = "Rye bread", Name = "Rye bread with caraway seeds and a very long name", AskCents = 540, GoingCents = 520, RivalCents = 500, Stock = 12, SoldWeek = 9, On = false }
        };
        var site = new ShopSiteFact
        {
            Key = "88 Broadway", Name = "Hudson Street Bakery Online", Business = "Hudson Street Bakery", Quality = "Standard", Orders = 31, Queue = 3, Overdue = 0, Halves = 9, Reviews = 312, Takings = 74200,
            Profit = 21800, Week = 389000, Offline = state == "hub_offline", Express = true, FeeCents = 450, FreeOverCents = 5000, Packing = 1, Night = false, Match = true, ShowStock = true,
            Areas = new[]
            {
                new ShopAreaFact { Name = "Midtown", Served = true }, new ShopAreaFact { Name = "Garment District", Served = true }, new ShopAreaFact { Name = "Hell's Kitchen", Served = true },
                new ShopAreaFact { Name = "Murray Hill", Served = false }
            },
            Products = Products(lines), Rivals = Rivals(), Basket = new ShopBasketFact
            {
                Customer = "Adele Ashdown", Arrives = "Tomorrow by 10:00", Lines = new[] { new ShopBasketLine { Name = "Sourdough loaf", Qty = 2, Cents = 650 }, new ShopBasketLine { Name = "Croissant", Qty = 1, Cents = 360 } },
                Subtotal = 1660, Fee = 450, FreeOver = 5000, ToFree = 3340, Total = 2110
            },
            Sales30 = sales, SalesDays = days, Lines = lines, Open = state == "orders_empty" ? new ShopOrderFact[0] : open, Done = state == "orders_empty" ? new ShopOrderFact[0] : done,
            CanPay = state == "hub_offline", CanAdvertise = state != "hub_offline", HostingLine = state == "hub_offline" ? "Hosting is unpaid: no orders come in until it is paid." : null
        };
        return new ShopFacts { Sites = new[] { site } };
    }

    private static ShopProductFact[] Products(ShopLineFact[] lines)
    {
        var list = new List<ShopProductFact>(lines.Length);
        for (int i = 0; i < lines.Length; i++)
        {
            ShopLineFact l = lines[i];
            var p = new ShopProductFact
            {
                Item = l.Item, Name = l.Name, Cents = l.AskCents, WasCents = i == 0 ? 720 : 0, PercentOff = i == 0 ? 9 : 0, Stock = l.Stock == 42 ? 4 : l.Stock, OnlyLeft = l.Stock == 42, Express = true, On = l.On,
                Arrives = "Tomorrow by 10:00", Halves = 9, Count = 118, Desc = i == 0 ? "A slow risen loaf with a thick crust, baked each morning in Midtown." : null,
                Breakdown = new[] { 2, 3, 9, 31, 73 }, Title = i == 0 ? "Hudson Street sourdough loaf, 800 g" : null,
                Reviews = new[]
                {
                    new ShopReviewFact { Index = 0, Name = "Adele Ashdown", Stars = 5, Day = 400, District = "Midtown", Reply = "Thank you, see you tomorrow." },
                    new ShopReviewFact { Index = 1, Name = "Hugo Lindqvist", Stars = 3, Day = 398, District = "Garment District", Reply = null }
                },
                Sellers = new[]
                {
                    new ShopSellerFact { Name = "Corner Daily Online", Cents = l.AskCents - 70, Halves = 8, Reviews = 410, Express = true, Rival = 0 },
                    new ShopSellerFact { Name = "Hudson Street Bakery Online", Cents = l.AskCents, Halves = 9, Reviews = 118, Express = true, Mine = true, Rival = -1 },
                    new ShopSellerFact { Name = "Gold Crust Direct", Cents = l.AskCents + 40, Halves = 9, Reviews = 902, Express = false, Rival = 1 }
                }
            };
            list.Add(p);
        }
        return list.ToArray();
    }

    private static ShopRivalFact[] Rivals() => new[]
    {
        new ShopRivalFact { Index = 0, Name = "Corner Daily Online", Halves = 8, Reviews = 1420, Express = true, Lines = new[] { new ShopRivalLine { Name = "Sourdough loaf", Cents = 580, Halves = 8, Count = 410 }, new ShopRivalLine { Name = "Croissant", Cents = 330, Halves = 9, Count = 1010 } } },
        new ShopRivalFact { Index = 1, Name = "Gold Crust Direct", Halves = 9, Reviews = 902, Express = false, Lines = new[] { new ShopRivalLine { Name = "Sourdough loaf", Cents = 690, Halves = 9, Count = 902 } } }
    };
}
