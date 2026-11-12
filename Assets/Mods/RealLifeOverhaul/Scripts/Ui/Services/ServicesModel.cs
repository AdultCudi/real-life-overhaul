using System;

namespace RealLifeOverhaul.Ui
{
    /// <summary>What the Services app's page is: nothing yet (the feature is off), a table with a card beside it, or the sale of a business.</summary>
    public enum ServicesShape : byte
    {
        Off = 0,
        Table,      // a tab's rows with the card of the one picked (or of the service) beside them
        Sell        // a business to sell: its worth, the offers on it, what accepting one leaves and the price to list it at
    }

    /// <summary>
    /// The sale of one business as the mockup draws it: the business to choose, its worth as the hero figure and how it is valued, what would raise it,
    /// the offers (or a rival's listings) as rows graded against the worth, what accepting the picked one leaves, and the asking price as a step with
    /// the offers each price draws. Plain words and figures; what a press does is the source's (a run).
    /// </summary>
    public sealed class ServicesSell
    {
        public string PickerTitle;                              // "Business to sell"
        public string[] Businesses = new string[0];             // each business's name in the picker, with its district
        public string[] BusinessKeys = new string[0];           // by business: the place a choice puts into the view
        public int Chosen = -1;
        public bool PickerOpen;
        public string WorthLabel, Worth, WorthSub;              // "Worth today", "$1,284,000", the line under it
        public UiRole WorthSubInk = UiRole.None;
        public UiChartModel Spark;                              // the worth by day as a line, or null where the days are not kept yet
        public UiFactCardModel Valued, Raise, After;            // how it is valued, what would raise it, and what the buyers still owe or ask after earlier sales (null for none)
        public UiActionModel[] Views = new UiActionModel[0];    // the table's two views: the offers on this business and the rivals' listings
        public int View;
        public string OffersTitle, OffersNote;                  // "Offers", "4 buyers, the bar is the worth"
        public UiDataTableModel Offers;
        public string[] OfferKeys = new string[0];              // by row: what a press picks ("bid:7", "rsale:3")
        public UiFactCardModel Accept;                          // the picked offer: what it leaves and what the player gives up, with Accept, Counter and Refuse
        public UiFactCardModel Price;                           // the asking price as a step, with what it means
        public UiChartModel PriceBars;                          // the offers each asking price draws, the chosen price marked
        public UiFactCardModel PriceFoot;                       // the sentences under the bars and the buttons: list, withdraw, quick sale
    }

    /// <summary>
    /// What the Services app shows, after the kit's app window: the four tabs across the top and a page of the shape given. The actions are runs (an index
    /// of <see cref="Runs"/>), so a state can be drawn and walked offline and a press can be told from another number.
    /// </summary>
    public sealed class ServicesModel
    {
        public string Title;
        public UiIcon Icon = UiIcon.Handshake;
        public UiNavEntry[] Nav = new UiNavEntry[0];
        public int Tab;
        public ServicesShape Shape;
        public string OffTitle, OffBody, OffAction;         // the page of a part that is off: OffAction leaves it, and focus starts there
        public UiIcon OffIcon = UiIcon.Handshake;
        public UiPrompt[] Prompts = new UiPrompt[0];

        public UiActionModel[] Chips = new UiActionModel[0];   // the table's views as chips over it ("All 12", "Active 8"): Arg is the chip's index
        public int ChipAt;
        public UiDataTableModel Table;
        public string[] RowKeys = new string[0];            // by row of the table: what a press picks
        public UiFactCardModel Side;                        // beside the table: the service, or the picked row
        public UiChartModel Chart;                          // under the table: a series over time, or null
        public UiFigureModel[] Strip = new UiFigureModel[0];   // a band of figures over the table
        public string Back;                                 // the way back's words while a row is picked

        public ServicesSell Sell;
        public Func<bool>[] Runs = new Func<bool>[0];

        // Written back by the layout, so a wheel and a focus reach the table they belong to.
        public short TableClip = -1, TableList = -1;
    }

    /// <summary>Where the screen stands: which tab, what is picked on it and the choices the player made there.</summary>
    public sealed class ServicesView
    {
        public int Tab;
        public string Pick;             // a row's id, its kind before the colon: "cli:", "add:", "emp:", "offer:", "con:", "bid:", "rsale:"
        public string Business;         // the place of the business chosen on the Sell tab, or null for the first
        public int Step = -1;           // the asking price's step shown on the Sell tab; -1 for the listing's own or the default
        public bool PickerOpen;
        public int Filter;              // the chip chosen over a table
        public int SellView;            // 0 the offers on the business, 1 the rivals' listings
        public float Offset;
        public int Reveal = -1;
        public int Selected = -1;
    }
}
