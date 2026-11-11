using System;

namespace RealLifeOverhaul.Ui
{
    /// <summary>The window mechanisms a phone app's action can open, each one taken by exactly one runner of the kit.</summary>
    public enum AuditWindow : byte
    {
        AppFace,    // the app's own window: its kit face stands over the app's panel
        Scene,      // a scene's list window (SceneDesk, SceneList): a hearing, a show, a door, an interview, a pitch
        Picker,     // a list window (DeskList, TravelList): rows to choose from
        ModBox,     // MetaUi.Message and Confirm, the mod's questions and messages
        Question,   // GameConfirm: the game's yes or no box
        TextInput,  // TextBox.Ask: the game's typed answer box
        Prompt,     // WorldPanel, DoorPanel, JobPanel: the card over a door, a desk or a kiosk
        Dialog,     // the game's dialogue panel: a talk, a call, a clerk
        Notice,     // Notifications.Show: a toast
        Session     // a desk session on the activity panel
    }

    /// <summary>One window an action can open: where it is opened from, what it is, and which runner draws it.</summary>
    public struct AuditRow
    {
        public string App;        // the phone app or place
        public string Action;     // the tab or action that opens it
        public string Window;     // what the player sees
        public AuditWindow Kind;
        public string Runner;     // the registered screen that draws it
        public string Area;       // the folder (under Scripts) whose files open it
        public string Decision;   // set where the owner left it the game's, with the date
    }

    /// <summary>
    /// The audit by action: for every phone app and place, each window an action can open, its mechanism and the runner of the kit that draws it,
    /// traced from the code (the folders named hold the callers), not from the mechanism's own say-so. EconomyCheck (--old-windows, --sweep-ui)
    /// reads the source of every folder for each mechanism's callers and fails on one this table does not name for its folder, on a runner that
    /// is not registered or has no switch, and on a scene window the scene kit does not take on the very first opening. Game-free. Cost: an array.
    /// </summary>
    public static class SweepAudit
    {
        public const string Scenes = "scenes", Picker = "world_picker", ModBox = "mod_box", Question = "question_box", Text = "text_box", Prompt = "world_prompt",
            Dialog = "dialog", Notice = "notice_cards", Session = "session_panel";

        static AuditRow Row(string app, string action, string window, AuditWindow kind, string runner, string area)
            => new AuditRow { App = app, Action = action, Window = window, Kind = kind, Runner = runner, Area = area };

        static AuditRow Face(string app, string action, string runner, string area) => Row(app, action, "the app's window", AuditWindow.AppFace, runner, area);

        public static readonly AuditRow[] Rows =
        {
            // ---------------------------------------------------------------------------------------------------- Tech
            Face("Tech", "Firms, Products, Contracts, Infra, Sector and Ventures tabs", "tech", "Tech/"),
            Row("Tech", "Ventures tab, Meet the founders", "the founders' pitch meeting", AuditWindow.Scene, Scenes, "Tech/Venture/"),
            Row("Tech", "Ventures tab, a firm of the player's: Pitch to a venture firm", "the player's pitch meeting", AuditWindow.Scene, Scenes, "Tech/Venture/"),
            Row("Tech", "Ventures, Infra, Studio tabs: invest, sign, cancel, release", "a yes or no box", AuditWindow.Question, Question, "Tech/"),
            Row("Tech", "Infra tab, name a cloud", "the typed name", AuditWindow.TextInput, Text, "Tech/Infra/"),
            Row("Tech", "Studio tab", "the studio page (an app window, as the Tech app's firm pages stand)", AuditWindow.AppFace, "studio", "Tech/Studio/"),
            Row("Tech", "Studio, own desk and sector desk: a work session", "the desk session's card", AuditWindow.Session, Session, "Tech/Studio/"),
            Row("Open Roles", "Roles, Team and Crew tabs", "the app's window", AuditWindow.AppFace, "open_roles", "Tech/People/"),
            Row("Open Roles", "Applicants tab, Interview", "the interview meeting", AuditWindow.Scene, Scenes, "Tech/Hiring/"),
            Row("Open Roles", "Hire, Let go, Release", "a yes or no box", AuditWindow.Question, Question, "Tech/People/"),

            // ---------------------------------------------------------------------------------------------------- Ventures and brands
            Face("Ventures", "Investors, offers and holdings", "ventures", "Economy/Ventures/"),
            Row("Ventures", "Investors tab, Meet an investor", "the investor's pitch meeting", AuditWindow.Scene, Scenes, "Economy/Ventures/"),
            Row("Ventures", "Accept, refuse, counter", "a yes or no box", AuditWindow.Question, Question, "Economy/Ventures/"),
            Face("Brands", "Brands, Deals and Shops tabs", "brands", "Economy/Brands/"),
            Row("Brands", "End a brand, a deal or a shop", "a yes or no box", AuditWindow.Question, Question, "Economy/Brands/"),
            Row("Brands", "Brands, Deals and Shops: end, sell, rename", "the mod's question", AuditWindow.ModBox, ModBox, "Economy/Brands/"),
            Row("Brands", "Rename a brand", "the typed name", AuditWindow.TextInput, Text, "Economy/Brands/"),
            Row("Web shop", "Shops tab, rename a shop", "the typed name", AuditWindow.TextInput, Text, "Economy/Online/"),
            Face("Web shop", "Shops, orders and the day", "webshop", "Economy/Online/"),
            Face("Giving", "Charities, loans and the year's giving", "giving", "Giving/"),
            Face("Health", "How you are, the plans, the bills and the city", "health", "Health/"),
            Face("Suppliers", "Suppliers, contracts and the market", "suppliers", "Suppliers/"),
            Row("Suppliers", "End a contract early", "a yes or no box", AuditWindow.Question, Question, "Suppliers/"),
            Row("Web shop", "Close or sell a shop", "a yes or no box", AuditWindow.Question, Question, "Economy/Online/"),

            // ---------------------------------------------------------------------------------------------------- Company, Estate, Services
            Face("Company", "Overview, Staff, Rota, Pay, Insurance, Marketing, Tax", "company", "Economy/Company/"),
            Row("Company", "Rota: fill a shift, raise", "a yes or no box", AuditWindow.Question, Question, "Economy/Company/"),
            Face("Property", "Market, Mortgages, Tenancies, Works", "estate", "Economy/Estate/"),
            Row("Property", "Visit a property", "the card over its door", AuditWindow.Prompt, Prompt, "Economy/Estate/"),
            Row("Property", "Buy, rent or sell back a parking space", "a yes or no box", AuditWindow.Question, Question, "Economy/Estate/"),
            Face("Car Showroom", "Stock, Lots, Market, Growth, Workshop", "dealer", "Economy/Dealer/"),
            Row("Car Showroom", "Lots, Stock, Market, Growth, Workshop actions", "the mod's question", AuditWindow.ModBox, ModBox, "Economy/Dealer/"),
            Row("Car Showroom", "Sell, scrap, hire out", "a yes or no box", AuditWindow.Question, Question, "Economy/Dealer/"),
            Face("Services", "Cleaning, Sale, Rivals tabs", "services", "Economy/Services/"),
            Row("Services", "Start, end or sell a service", "a yes or no box", AuditWindow.Question, Question, "Economy/Services/"),

            // ---------------------------------------------------------------------------------------------------- Construction
            Face("Construction", "Land, Market, Projects, Crews, Hire, Refit, Redevelop, Park, Hedge, Show flat", "construction", "Construction/"),
            Row("Construction", "Every section's confirm: raise, release, sell, start, finish", "the mod's question", AuditWindow.ModBox, ModBox, "Construction/"),
            Row("Construction", "Build mode, leave and the planning notices", "a yes or no box", AuditWindow.Question, Question, "Construction/"),
            Row("Construction", "Builder's desk, car park desk, planning call", "the dialogue", AuditWindow.Dialog, Dialog, "Construction/"),
            Row("Construction", "Builder's desk and site: its prompt", "the card over the desk", AuditWindow.Prompt, Prompt, "Construction/"),
            Row("Construction", "Build mode itself", "the build mode bar, legend and window", AuditWindow.AppFace, "build_mode", "Construction/"),

            // ---------------------------------------------------------------------------------------------------- Banks and Finance
            Face("Bank apps", "Accounts, Loans, Apply, Invest", "banks", "Economy/Banking/"),
            Row("Bank apps", "Apply, Loans: take, repay, close", "a yes or no box", AuditWindow.Question, Question, "Economy/Banking/"),
            Row("Bank apps", "Rename an account", "the typed name", AuditWindow.TextInput, Text, "Economy/Banking/"),
            Row("Bank apps", "Debt collector's call", "the dialogue", AuditWindow.Dialog, Dialog, "Economy/Banking/"),
            Face("Finance", "Accounts, Borrow, Products, Rivals", "finance", "Economy/Finance/"),
            Row("Finance", "Borrow, Products, Accounts: confirm", "a yes or no box", AuditWindow.Question, Question, "Economy/Finance/"),
            Row("Finance", "Borrow an amount, rename an account", "the typed amount or name", AuditWindow.TextInput, Text, "Economy/Finance/"),
            Face("Your bank", "Branches, Tiers, Dividends", "own_bank", "Economy/OwnBank/"),
            Row("Your bank", "Found, rename, licence, capital", "a yes or no box", AuditWindow.Question, Question, "Economy/OwnBank/"),
            Row("Your bank", "Name the bank, a sum", "the typed name or sum", AuditWindow.TextInput, Text, "Economy/OwnBank/"),
            Face("Stocks", "Market, Bonds, Bots, Derivatives, Invest, Own", "stocks", "Markets/"),
            Row("Stocks", "Bonds, Bots, Derivatives, Invest, Own: confirm", "a yes or no box", AuditWindow.Question, Question, "Markets/"),
            Row("Stocks", "Name a list, a limit, an amount", "the typed answer", AuditWindow.TextInput, Text, "Markets/"),
            Face("Economy", "Outlook pages", "economy", "Outlook/"),
            Face("Utilities", "Home, Network, Internet", "utilities", "Utilities/"),
            Row("Utilities", "Network: change plan", "a yes or no box", AuditWindow.Question, Question, "Utilities/"),
            Row("Utilities", "The utilities desk", "the desk's list", AuditWindow.Picker, Picker, "Utilities/"),
            Row("Utilities", "The utilities desk", "the card over the desk", AuditWindow.Prompt, Prompt, "Utilities/"),

            // ---------------------------------------------------------------------------------------------------- Work and careers
            Face("Career", "Work, Life, Own tabs", "career", "Careers/"),
            Row("Career", "Own tab, quit, promotion", "the mod's question", AuditWindow.ModBox, ModBox, "Careers/"),
            Row("Career, Campus", "Quit, resign, drop a course", "a yes or no box", AuditWindow.Question, Question, "Careers/"),
            Row("Career", "The recruitment agency desk", "the dialogue", AuditWindow.Dialog, Dialog, "Careers/"),
            Row("Career", "A workplace door and a desk", "the card over the door", AuditWindow.Prompt, Prompt, "Careers/"),
            Face("Campus", "Courses and study", "campus", "Careers/"),
            Face("Marketplace", "Listings, Freelance, Offers, Yours", "marketplace", "Hustles/"),
            Row("Marketplace", "Freelance, Listings, Offers: accept, decline, cancel", "the mod's question", AuditWindow.ModBox, ModBox, "Hustles/"),
            Row("Marketplace", "Take a gig", "a yes or no box", AuditWindow.Question, Question, "Hustles/"),
            Row("Marketplace", "A place's door", "the card over the door", AuditWindow.Prompt, Prompt, "Hustles/"),
            Row("Jobs", "A job board, a kiosk, a vendor", "the card over the board", AuditWindow.Prompt, Prompt, "Jobs/"),
            Row("Jobs", "Dispatch board, design stages, the tender bill", "the list window", AuditWindow.Picker, Picker, "Jobs/"),
            Row("Jobs", "Career and job phone calls", "the dialogue", AuditWindow.Dialog, Dialog, "Jobs/"),
            Row("Licences", "The driving school and its theory test", "the list window", AuditWindow.Picker, Picker, "Licences/"),
            Row("Licences", "The school's desk and the practical test", "the card over the desk", AuditWindow.Prompt, Prompt, "Licences/"),
            Row("Licences", "The driving school's talk", "the dialogue", AuditWindow.Dialog, Dialog, "Licences/"),
            Row("Schools", "A school's page", "the page inside the Persona screen", AuditWindow.AppFace, "persona", "Schools/"),

            // ---------------------------------------------------------------------------------------------------- Social, Flare and family
            Face("Flare", "Feed, Messages, Profile, Camera", "flare", "Social/"),
            Row("Flare", "Compose a post, a message", "the typed text", AuditWindow.TextInput, Text, "Social/"),
            Row("Flare", "Delete, block, unfollow, take a photo", "a yes or no box", AuditWindow.Question, Question, "Social/"),
            Row("Flare", "A person in the street", "the card over the person", AuditWindow.Prompt, Prompt, "Social/"),
            Row("Flare", "Phoning a person", "the dialogue", AuditWindow.Dialog, Dialog, "Social/"),
            Row("Family", "Partner's allowance", "the typed amount", AuditWindow.TextInput, Text, "Family/"),
            Row("Family", "The family's prompts and chores", "a yes or no box", AuditWindow.Question, Question, "Family/"),
            Row("Family", "A family member's door and the home", "the card over the door", AuditWindow.Prompt, Prompt, "Family/"),
            Row("Family", "Phoning family, talking to them", "the dialogue", AuditWindow.Dialog, Dialog, "Family/"),
            Row("Home", "The fridge, the doctor, the roommates", "the card over the object", AuditWindow.Prompt, Prompt, "Home/"),
            Row("Home", "Cooking", "the list window", AuditWindow.Picker, Picker, "Home/"),
            Row("Home", "The doctor, the household and roommates", "the dialogue", AuditWindow.Dialog, Dialog, "Home/"),
            Row("Street", "Talking to a stranger", "the typed words", AuditWindow.TextInput, Text, "Street/"),
            Row("Street", "A person on the street", "the card over the person", AuditWindow.Prompt, Prompt, "Street/"),
            Row("Street", "A stranger's talk", "the dialogue", AuditWindow.Dialog, Dialog, "Street/"),
            Row("Texts", "The AI texts' model and address", "the typed text", AuditWindow.TextInput, Text, "Texts/"),

            // ---------------------------------------------------------------------------------------------------- Planner, Calendar, Photos, Meta
            Face("Parties", "Host a party, Staff events, Diary", "parties", "Parties/"),
            Row("Parties", "At the place of a party: begin it, or go in to a friend's", "a yes or no box", AuditWindow.Question, Question, "Parties/"),
            Face("Planner", "Week, Notes, Plans", "planner", "Planner/"),
            Row("Planner", "Notes: add, delete", "a yes or no box", AuditWindow.Question, Question, "Planner/"),
            Row("Planner", "Notes: write one", "the typed note", AuditWindow.TextInput, Text, "Planner/"),
            Face("Calendar", "Month and day", "calendar", "Calendar/"),
            Face("Camera", "Viewfinder and shutter", "camera", "Photos/"),
            Face("Gallery", "Photos and albums", "photo_gallery", "Photos/"),
            Row("Gallery", "Delete a photo, camera limits", "a yes or no box", AuditWindow.Question, Question, "Photos/"),
            Face("City Ledger", "Pages and kiosks", "ledger", "Ledger/"),
            Face("Newsstand", "Titles, editions and reading", "newsstand", "Ledger/"),
            Row("City Ledger", "A kiosk", "the card over the kiosk", AuditWindow.Prompt, Prompt, "Ledger/"),
            Face("Guide", "The mod's guide", "guide", "Meta/"),
            Face("Mod settings", "Options and presets", "mod_settings", "Meta/"),
            Face("Staff and roles", "Roles", "staff_roles", "Meta/"),
            Row("Mod settings", "Reset, remove, save tools", "the mod's question", AuditWindow.ModBox, ModBox, "Meta/"),
            Row("Mod settings", "Feature list, city changes, options notice", "a yes or no box", AuditWindow.Question, Question, "Meta/"),
            Row("Mod settings", "Bug report", "the typed report", AuditWindow.TextInput, Text, "Meta/"),
            Row("Guide", "Guide talks", "the dialogue", AuditWindow.Dialog, Dialog, "Meta/"),
            Row("Permadeath", "The ending and the warnings", "the mod's question", AuditWindow.ModBox, ModBox, "Permadeath/"),
            Row("Permadeath", "The warnings", "a yes or no box", AuditWindow.Question, Question, "Permadeath/"),
            Row("Phone", "A contact's call", "the dialogue", AuditWindow.Dialog, Dialog, "Phone/"),
            Row("Rivals", "A rival's page", "a yes or no box", AuditWindow.Question, Question, "Rivals/"),
            Row("Interiors", "The Interior tab in BizMan", "a yes or no box", AuditWindow.Question, Question, "Interiors/"),

            // ---------------------------------------------------------------------------------------------------- the world's own scenes and desks
            Row("Fame", "A show's invitation: the host's questions", "the show's scene", AuditWindow.Scene, Scenes, "Fame/"),
            Row("Fame", "The press and the reporter", "the card over the reporter", AuditWindow.Prompt, Prompt, "Fame/"),
            Row("Fame", "The reporter's talk", "the dialogue", AuditWindow.Dialog, Dialog, "Fame/"),
            Row("Justice", "A hearing, bail and the Legal file", "the court's scene", AuditWindow.Scene, Scenes, "Justice/"),
            Row("Justice", "Community service, the courthouse", "the card over the door", AuditWindow.Prompt, Prompt, "Justice/"),
            Row("Justice", "Legal counsel", "the dialogue", AuditWindow.Dialog, Dialog, "Justice/"),
            Row("Civic", "Running for office: the door, the vote", "the door scene", AuditWindow.Scene, Scenes, "Civic/"),
            Row("Civic", "The council and the town hall", "the card over the door", AuditWindow.Prompt, Prompt, "Civic/"),
            Row("Civic", "The town hall's phone", "the dialogue", AuditWindow.Dialog, Dialog, "Civic/"),
            Row("Law", "The station desk and custody", "the list window", AuditWindow.Picker, Picker, "Law/"),
            Row("Law", "The station and the record", "the card over the desk", AuditWindow.Prompt, Prompt, "Law/"),
            Row("Law", "The record's call", "the dialogue", AuditWindow.Dialog, Dialog, "Law/"),
            Row("Garage", "The garage bay", "the list window", AuditWindow.Picker, Picker, "Garage/"),
            Row("Garage", "The bay, the two wheelers", "the card over the bay", AuditWindow.Prompt, Prompt, "Garage/"),
            Row("Transport", "Bus stops", "the list window", AuditWindow.Picker, Picker, "Transport/"),
            Row("Transport", "A stop, a car, the subway", "the card over the stop", AuditWindow.Prompt, Prompt, "Transport/"),
            Row("Transport", "Taxi, car and subway talks", "the dialogue", AuditWindow.Dialog, Dialog, "Transport/"),
            Row("Transport", "Leave a bus route", "a yes or no box", AuditWindow.Question, Question, "Transport/"),
            Row("Travel", "The travel desk and holidays", "the list window", AuditWindow.Picker, Picker, "Travel/"),
            Row("Travel", "The travel desk's door", "the card over the desk", AuditWindow.Prompt, Prompt, "Travel/"),
            Row("Travel", "Hotel, holidays, the agent", "the dialogue", AuditWindow.Dialog, Dialog, "Travel/"),
            Row("City", "Doors, kiosks and landmarks", "the card over the door", AuditWindow.Prompt, Prompt, "BuildTrades/"),
            Row("City", "Doors, kiosks and landmarks", "the card over the door", AuditWindow.Prompt, Prompt, "CityLife/"),
            Row("City", "Doors, kiosks and landmarks", "the card over the door", AuditWindow.Prompt, Prompt, "Landmarks/"),
            Row("City", "Doors, kiosks and landmarks", "the card over the door", AuditWindow.Prompt, Prompt, "Leisure/"),
            Row("City", "Doors, kiosks and landmarks", "the card over the door", AuditWindow.Prompt, Prompt, "Floors/"),
            Row("City", "The business desk's talk", "the dialogue", AuditWindow.Dialog, Dialog, "Business/"),
            Row("City", "Events and crimes: a question", "a yes or no box", AuditWindow.Question, Question, "Crime/"),
            Row("City", "Life events: a question", "a yes or no box", AuditWindow.Question, Question, "Life/"),
            Row("City", "The map's and the build's notices", "a yes or no box", AuditWindow.Question, Question, "Core/"),
            Row("City", "The interface's and the game's notices", "the game's toast", AuditWindow.Notice, Notice, string.Empty),
        };

        /// <summary>The registered screen that draws a mechanism's windows, or null for an app face (its row names its own).</summary>
        public static string RunnerOf(AuditWindow kind)
        {
            switch (kind)
            {
                case AuditWindow.Scene: return Scenes;
                case AuditWindow.Picker: return Picker;
                case AuditWindow.ModBox: return ModBox;
                case AuditWindow.Question: return Question;
                case AuditWindow.TextInput: return Text;
                case AuditWindow.Prompt: return Prompt;
                case AuditWindow.Dialog: return Dialog;
                case AuditWindow.Notice: return Notice;
                case AuditWindow.Session: return Session;
                default: return null;
            }
        }

        /// <summary>True where a row's folder holds a file at this path (an empty folder holds every file).</summary>
        public static bool Holds(string area, string path) => area.Length == 0 || path.StartsWith(area, StringComparison.Ordinal);
    }
}
