using System;
using System.Collections.Generic;
using RealLifeOverhaul.Ui;

// The Web shop's samples: a bakery site and a warehouse site with orders, a catalogue and thirty days of sales. Names are placeholders and never ship.
internal static partial class UiSamples
{
    public static readonly string[] ShopStates = { "hub", "hub_offline", "orders", "orders_empty", "catalogue", "catalogue_pick", "empty", "off" };

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
            case "catalogue": view.Tab = ShopPages.TabCatalogue; break;
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
            new ShopOrderFact { Customer = "Adele Ashdown", Items = "2 x Sourdough loaf, 1 x Croissant", District = "Midtown", Total = 1450, Stage = "Packing", Open = true },
            new ShopOrderFact { Customer = "Hugo Lindqvist", Items = "6 x Croissant", District = "Garment District", Total = 2160, Stage = "On its way", Open = true },
            new ShopOrderFact { Customer = "Thea Okafor", Items = "1 x Sourdough loaf", District = "Hell's Kitchen", Total = 650, Stage = "Waiting for a van", Open = true }
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
            Sales30 = sales, SalesDays = days, Lines = lines, Open = state == "orders_empty" ? new ShopOrderFact[0] : open, Done = state == "orders_empty" ? new ShopOrderFact[0] : done,
            CanPay = state == "hub_offline", CanAdvertise = state != "hub_offline", HostingLine = state == "hub_offline" ? "Hosting is unpaid: no orders come in until it is paid." : null
        };
        return new ShopFacts { Sites = new[] { site } };
    }
}
