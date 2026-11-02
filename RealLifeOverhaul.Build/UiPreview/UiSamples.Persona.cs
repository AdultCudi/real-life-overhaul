using System.Collections.Generic;
using System.Globalization;
using RealLifeOverhaul.Ui;

/// <summary>
/// Persona, Rivals and Market Insider on the kit, each state as the game would hand it over (through the same views the
/// game's screens fill): Persona for a new character and a rich one on every tab; the league with a rival at war with the
/// player chosen, that rival's businesses and buildings, and an empty league; Market Insider in Uptown with its events
/// and without, its real estate for sale only, a filter's list open; and each app in French (the longest of the mod's
/// languages: the game's words in it, the mod's new words in English until the next batch). Plain English otherwise.
/// </summary>
internal static partial class UiSamples
{
    /// <summary>Every state of Persona, in the previews' order.</summary>
    public static readonly string[] PersonaStates =
    {
        "rich_overview", "rich_needs", "rich_education", "rich_goals", "new_overview", "new_needs", "new_education", "new_goals", "rich_overview_fr", "rich_goals_fr"
    };

    /// <summary>Every state of Rivals.</summary>
    public static readonly string[] RivalsStates = { "league", "league_quiet", "businesses", "buildings", "league_empty", "league_fr", "businesses_fr" };

    /// <summary>Every state of Market Insider.</summary>
    public static readonly string[] MarketStates = { "uptown_demands", "uptown_events", "uptown_quiet", "estate_for_sale", "filter_open", "uptown_fr", "estate_fr", "events_fr" };

    private static readonly CultureInfo En = CultureInfo.GetCultureInfo("en-US");

    // A sum as the game's short money words have it: dollars with their thousands, a K or an M once it runs past five figures.
    private static string ShortMoney(float v)
    {
        double a = System.Math.Abs(v);
        if (a <= 9999.0)
        {
            return ((double)v).ToString("C0", En);
        }
        string[] suffix = { "K", "M", "B" };
        int i = -1;
        while (a >= 1000.0 && i < suffix.Length - 1)
        {
            a /= 1000.0;
            i++;
        }
        a = System.Math.Floor(a * 10.0) / 10.0;
        return (v < 0f ? "-" : string.Empty) + a.ToString(a >= 100.0 ? "C0" : "C1", En) + suffix[i];
    }

    private static string FullMoney(float v) => (v < 0f ? "-" : string.Empty) + System.Math.Truncate((double)System.Math.Abs(v)).ToString("C0", En);

    // ------------------------------------------------------------------------------------------------------- Persona

    /// <summary>Persona in one of its states.</summary>
    public static UiPersonaModel Persona(UiWords t, string state)
    {
        UiWords w = state.EndsWith("_fr") ? PersonaFrench(t) : t;
        PersonaView v = state.StartsWith("new") ? NewPersona(w) : RichPersona(w);
        v.Tab = state.Contains("needs") ? UiPersona.Needs : state.Contains("education") ? UiPersona.Education : state.Contains("goals") ? UiPersona.Goals : UiPersona.Overview;
        v.Selected = v.Tab == UiPersona.Overview ? -1 : 1;
        var m = new UiPersonaModel();
        PersonaViews.Persona(m, w, v);
        return m;
    }

    private static PersonaView RichPersona(UiWords t)
    {
        var v = new PersonaView
        {
            Name = "Maya Okafor", Age = 34, Businesses = 6, Employees = 41, Cash = 1284500f, Investments = 612300f, Assets = 2950000f, Loans = 480000f, WeeklyIncome = 186420f,
            IncomeDays = new[] { 142300f, 151800f, 149200f, 163900f, 171400f, 168100f, 186420f },
            IncomeDayNumbers = new[] { 406, 407, 408, 409, 410, 411, 412 }, Money = ShortMoney, Energy = 72, Hunger = 38, Happiness = 81,
            GoalSort = 1, GoalSortDown = true
        };
        v.Modifiers = new[]
        {
            new PersonaModifier { Name = "Lives in a luxury apartment", Amount = 15, Left = null },
            new PersonaModifier { Name = "Drove a sports car", Amount = 10, Left = Fill(t("common_days_left", "{days} days left"), "days", "3") },
            new PersonaModifier { Name = "A good night's sleep", Amount = 8, Left = Fill(t("common_hours_left", "{hours} hours left"), "hours", "14") },
            new PersonaModifier { Name = "Dinner with friends", Amount = 6, Left = Fill(t("common_hours_left", "{hours} hours left"), "hours", "9") },
            new PersonaModifier { Name = "Working too many hours", Amount = -12, Left = Fill(t("common_days_left", "{days} days left"), "days", "2") },
            new PersonaModifier { Name = "Hungry", Amount = -5, Left = null },
            new PersonaModifier { Name = "Argued with a neighbour", Amount = -4, Left = Fill(t("common_hours_left", "{hours} hours left"), "hours", "20") },
        };
        v.Courses = Courses(t, true);
        v.Goals = Goals(t, true);
        return v;
    }

    private static PersonaView NewPersona(UiWords t) => new PersonaView
    {
        Name = "Leo Hart", Age = 19, Businesses = 0, Employees = 0, Cash = 850f, Investments = 0f, Assets = 0f, Loans = 0f, WeeklyIncome = 0f,
        IncomeDays = new float[0], IncomeDayNumbers = new int[0], Money = ShortMoney, Energy = 94, Hunger = 61, Happiness = 55, Modifiers = new PersonaModifier[0], Courses = Courses(t, false),
        Goals = Goals(t, false)
    };

    private static PersonaCourse[] Courses(UiWords t, bool rich)
    {
        string done = t("persona_education_completed", "Completed"), none = t("persona_education_not_started", "Not started yet");
        string share = t("persona_education_completed_percentage", "{percentageComplete}% Completed");
        string degree = "Degrees", nursing = "Nursing", online = "Online courses", certificate = "Certificates";
        var list = new List<PersonaCourse>
        {
            Course("Business Administration", rich ? done : none, rich ? PersonaCourseState.Done : PersonaCourseState.NotStarted, null, null),
            Course("Marketing", rich ? done : Fill(share, "percentageComplete", "35"), rich ? PersonaCourseState.Done : PersonaCourseState.Under, null, null),
            Course("Human Resources", rich ? Fill(share, "percentageComplete", "60") : none, rich ? PersonaCourseState.Under : PersonaCourseState.NotStarted, null, null),
            Course("Logistics", none, PersonaCourseState.NotStarted, null, null),
            Course("Programming", rich ? done : none, rich ? PersonaCourseState.Done : PersonaCourseState.NotStarted, null, null),
            Course("Law", none, PersonaCourseState.NotStarted, null, null),
            Course("Interior Design", none, PersonaCourseState.NotStarted, null, null),
            Course("BSc Computer Science", rich ? "Exam on Day 418" : none, rich ? PersonaCourseState.Waiting : PersonaCourseState.NotStarted, degree, "reallife_persona_edu_group_degree"),
            Course("BA Economics", rich ? "Classes on weekdays" : none, rich ? PersonaCourseState.Under : PersonaCourseState.NotStarted, degree, "reallife_persona_edu_group_degree"),
            Course("Registered Nurse", none, PersonaCourseState.NotStarted, nursing, "reallife_persona_edu_group_nursing"),
            Course("Digital Marketing Online", rich ? "Withdrawn" : none, rich ? PersonaCourseState.Stopped : PersonaCourseState.NotStarted, online, "reallife_persona_edu_group_online"),
            Course("Food Hygiene Certificate", rich ? "Passed" : none, rich ? PersonaCourseState.Done : PersonaCourseState.NotStarted, certificate, "reallife_persona_edu_group_certificate"),
            Course("Forklift Licence", none, PersonaCourseState.NotStarted, certificate, "reallife_persona_edu_group_certificate"),
            Course("First Aid at Work", none, PersonaCourseState.NotStarted, certificate, "reallife_persona_edu_group_certificate"),
        };
        return list.ToArray();
    }

    private static PersonaCourse Course(string name, string status, PersonaCourseState state, string group, string key) =>
        new PersonaCourse { Name = name, Status = status, State = state, Group = group, GroupKey = key };

    // The game's goals as its entries word them: the goal of the tier under way (its target the progress's), or of the last.
    private static PersonaGoal[] Goals(UiWords t, bool rich)
    {
        var goals = new List<PersonaGoal>
        {
            Goal(t, "BankBalanceGoal", "personalgoal_bank_balance", "Reach a bank balance of {amount}", rich ? "$10,000,000" : "$100,000",
                rich ? "$1,284,500 / $10,000,000" : "$850 / $100,000", rich ? 2 : 0, rich ? 1 : -1),
            Goal(t, "BusinessWeeklyIncomeGoal", "personalgoal_business_weekly_income", "Reach a weekly income of {amount} for one business", rich ? "$100,000" : "$5,000",
                rich ? "$52,600 / $100,000" : "$0 / $5,000", rich ? 2 : 0, rich ? 2 : -1),
            Goal(t, "EmploymentGoal", "personalgoal_minemployees", "Employ at least {minimumEmployees} people", rich ? "100" : "5", rich ? "41 / 100" : "0 / 5", rich ? 1 : 0, -1, "minimumEmployees"),
            Goal(t, "RealEstateGoal", "personalgoal_realestate", "Own at least {amount} buildings", "25", rich ? "3 / 25" : "0 / 1", rich ? 2 : 0, rich ? 0 : -1),
            Goal(t, "TaxiRideGoal", "personalgoal_taxi_rides", "Complete {amount} taxi rides", "250", rich ? "250 / 250" : "2 / 10", rich ? 3 : 0, -1),
            Goal(t, "TaxesPaidGoal", "personalgoal_taxes_paid", "Pay at least {amount} in taxes", rich ? "$1,000,000" : "$10,000", rich ? "$612,000 / $1,000,000" : "$0 / $10,000",
                rich ? 1 : 0, -1),
            Goal(t, "TotalDailyCustomerGoal", "personalgoal_dailycustomers", "Reach at least {amount} customers per day for one business", rich ? "1,000" : "100",
                rich ? "812 / 1,000" : "0 / 100", rich ? 2 : 0, -1),
            Goal(t, "ItemsInStockGoal", "personalgoal_items_in_stock", "Stockpile at least {amount} of any product", rich ? "10,000" : "500",
                rich ? "7,410 / 10,000" : "0 / 500", rich ? 2 : 0, rich ? 1 : -1),
            Goal(t, "CasinoWinGoal", "personalgoal_casinowin", "Win a total of {amount} at the casino", "$1,000,000", rich ? "$1,000,000 / $1,000,000" : "$0 / $10,000", rich ? 3 : 0,
                rich ? 2 : -1),
            Goal(t, "VehicleGoal", "personalgoal_owned_vehicles", "Own a total of {amount} vehicles", rich ? "10" : "2", rich ? "4 / 10" : "0 / 2", rich ? 1 : 0, -1),
            Goal(t, "EmployeeMaxLevelGoal", "personalgoal_employee_max_skill", "Reach level {amount}% of any skill for one employee", "100", rich ? "92 / 100" : "38 / 75",
                rich ? 2 : 0, -1),
            Goal(t, "PlayerAgeGoal", "personalgoal_age", "Celebrate your {amount}th birthday", rich ? "40" : "25", rich ? "34 / 40" : "19 / 25", rich ? 2 : 0, -1),
        };
        // As the game's own sort of the current tier shows them: the most tiers first, the game's order kept among equals.
        var sorted = new List<PersonaGoal>(goals.Count);
        for (int tier = 3; tier >= 0; tier--)
        {
            sorted.AddRange(goals.FindAll(g => g.Tiers == tier));
        }
        return sorted.ToArray();
    }

    private static PersonaGoal Goal(UiWords t, string kind, string key, string english, string amount, string progress, int tiers, int steam, string slot = "amount")
    {
        int slash = progress != null ? progress.IndexOf('/') : -1;
        string target = slash >= 0 ? progress.Substring(slash + 1).Trim() : amount;   // the tier under way's target, as the game's entry names it
        return new PersonaGoal { Kind = kind, Title = Fill(t(key, english), slot, target), Progress = progress, Tiers = tiers, Steam = steam };
    }

    // ------------------------------------------------------------------------------------------------------- Rivals

    /// <summary>Rivals in one of its states.</summary>
    public static UiRivalsModel Rivals(UiWords t, string state)
    {
        UiWords w = state.EndsWith("_fr") ? PersonaFrench(t) : t;
        RivalsView v = RivalsSample(w, state);
        var m = new UiRivalsModel();
        RivalsViews.Rivals(m, w, v);
        return m;
    }

    private static RivalsView RivalsSample(UiWords t, string state)
    {
        var v = new RivalsView { Money = FullMoney, ChartDays = new[] { 406, 407, 408, 409, 410, 411, 412 }, ChartMoney = true, Contacts = true };
        if (state == "league_empty")
        {
            return v;
        }
        v.League = new[]
        {
            Rival("Huang Guo", 312400f, 281000f, 14, "Midtown", 58, false, false, false),
            Rival("Thierry Laurent Moreau", 254800f, 262100f, 11, "Murray Hill", 61, false, true, false),
            Rival("Maya Okafor", 186420f, 142300f, 6, "Hell's Kitchen", 34, true, false, false),
            Rival("Ingrid Schneider", 171900f, 160400f, 9, "Garment District", 52, false, false, false),
            Rival("Jessica Johnson", 98300f, 121700f, 7, "Hell's Kitchen", 47, false, false, false),
            Rival("Marcus Hale", 64200f, 59800f, 4, "Lower Manhattan", 44, false, false, false),
            Rival("Priya Raman", 41800f, float.NaN, 3, "Industry City", 39, false, false, false),
            Rival("Owen Pryce", 22600f, 30100f, 2, "Midtown", 51, false, false, false),
            Rival("Lena Kowalski", 19800f, 18200f, 2, "Garment District", 36, false, false, false),
            Rival("Daniel Osei", 15400f, 16900f, 2, "Hell's Kitchen", 42, false, false, false),
            Rival("Sofia Castillo", 12700f, 9900f, 1, "Murray Hill", 29, false, false, false),
            Rival("Viktor Hahn", 9100f, 10400f, 1, "Industry City", 57, false, false, false),
            Rival("Amara Diallo", 6300f, 5800f, 1, "Lower Manhattan", 33, false, false, false),
            Rival("Felix Weber", 2400f, 3100f, 1, "Midtown", 48, false, false, false),
            Rival("Carmen Ortega", -4100f, 8700f, 0, null, 63, false, false, true),
        };
        v.Chosen = state == "league_quiet" ? 3 : 1;
        v.Contacts = state != "league_quiet";
        v.Filters = new[] { t("common_weekly_income", "Weekly Income"), t("common_businesses", "Businesses") };
        v.Filter = 0;
        v.Chart = state == "league_quiet" ? new[] { 148100f, 152400f, 150900f, 158300f, 162700f, 166200f, 171900f }
            : new[] { 281300f, 276900f, 270400f, 268800f, 265100f, 263000f, 254800f };
        if (state != "league_quiet")
        {
            v.Threats = new[]
            {
                new UiRivalThreat { Icon = UiIcon.Tag, Title = Fill(t("rivals_defense_icons_price_reduction_title", "Price reduction - {days} day(s) left").Replace(" - ", ", "), "days", "3"),
                    Body = Fill(t("rivals_defense_icons_price_reduction_description",
                        "{rivalName} lowered their prices on your best selling products to steal your customers"), "rivalName", "Thierry Laurent Moreau") },
                new UiRivalThreat { Icon = UiIcon.People, Title = Fill(t("rivals_defense_icons_hire_best_employees_title", "Hire best employees - {days} day(s) left")
                    .Replace(" - ", ", "), "days", "5"),
                    Body = Fill(t("rivals_defense_icons_hire_best_employees_description", "{rivalName} is poaching your employees"), "rivalName", "Thierry Laurent Moreau") },
            };
        }
        v.Tab = state.StartsWith("businesses") ? UiRivals.Businesses : state == "buildings" ? UiRivals.Buildings : UiRivals.League;
        v.Selected = 2;
        v.Businesses = new[]
        {
            Business("Le Petit Bistro", "ba:businesstype_fastfoodrestaurant", t("ba:businesstype_fastfoodrestaurant", "Fast Food Restaurant"), 48200f, "112 Lexington Avenue"),
            Business("Moreau Coffee House", "ba:businesstype_coffeeshop", t("ba:businesstype_coffeeshop", "Coffee Shop"), 31800f, "40 East 34th Street"),
            Business("Maison Laurent", "ba:businesstype_clothingstore", t("ba:businesstype_clothingstore", "Clothing Store"), 52600f, "75 Park Avenue"),
            Business("Murray Hill Market", "ba:businesstype_supermarket", t("ba:businesstype_supermarket", "Supermarket"), 44100f, "210 Third Avenue"),
            Business("TLM Jewellers", "ba:businesstype_jewelrystore", t("ba:businesstype_jewelrystore", "Jewelry Store"), 27900f, "18 Madison Avenue"),
            Business("TLM Enterprise", "ba:businesstype_headquarters", t("ba:businesstype_headquarters", "Headquarters"), -6200f, "1 Park Avenue"),
            Business("Fleurs de Paris", "ba:businesstype_florist", t("ba:businesstype_florist", "Florist"), 12300f, "56 Lexington Avenue"),
            Business("Moreau Electronics", "ba:businesstype_electronicsstore", t("ba:businesstype_electronicsstore", "Electronics Store"), 38900f, "301 Second Avenue"),
            Business("Rive Gauche Books", "ba:businesstype_bookstore", t("ba:businesstype_bookstore", "Bookstore"), 9400f, "88 East 30th Street"),
            Business("TLM Logistics", "ba:businesstype_warehouse", t("ba:businesstype_warehouse", "Warehouse"), 0f, "12 Pier Road"),
            Business("Chez Thierry", "ba:businesstype_fastfoodrestaurant", t("ba:businesstype_fastfoodrestaurant", "Fast Food Restaurant"), 41300f, "140 Lexington Avenue"),
        };
        System.Array.Sort(v.Businesses, (a, b) => b.WeeklyIncome.CompareTo(a.WeeklyIncome));   // as the sorted head says: the most first
        v.SortColumn = 3;
        v.SortDown = true;
        v.Buildings = new[]
        {
            Building("1 Park Avenue", "Murray Hill", "ba:buildingtype_office", t("ba:buildingtype_office", "Office"), "1,240 m²", 4820000f, 0f),
            Building("75 Park Avenue", "Murray Hill", "ba:buildingtype_retail", t("ba:buildingtype_retail", "Retail"), "380 m²", 1960000f, 2250000f),
            Building("210 Third Avenue", "Murray Hill", "ba:buildingtype_retail", t("ba:buildingtype_retail", "Retail"), "620 m²", 2410000f, 0f),
            Building("12 Pier Road", "Industry City", "ba:buildingtype_warehouse", t("ba:buildingtype_warehouse", "Warehouse"), "2,100 m²", 1380000f, 0f),
            Building("9 East 36th Street", "Murray Hill", "ba:buildingtype_residential", t("ba:buildingtype_residential", "Residential"), "160 m²", 890000f, 940000f),
        };
        if (state == "buildings")
        {
            v.SortColumn = 4;
            v.SortDown = true;
            System.Array.Sort(v.Buildings, (a, b) => b.Value.CompareTo(a.Value));
        }
        return v;
    }

    private static RivalEntry Rival(string name, float income, float weekAgo, int businesses, string district, int age, bool you, bool rivalry, bool defeated) =>
        new RivalEntry { Name = name, WeeklyIncome = income, WeekAgo = weekAgo, Businesses = businesses, District = district, Age = age, You = you, Rivalry = rivalry, Defeated = defeated };

    private static RivalBusiness Business(string name, string key, string type, float income, string address) =>
        new RivalBusiness { Name = name, TypeKey = key, Type = type, WeeklyIncome = income, Address = address };

    private static RivalBuilding Building(string address, string district, string key, string type, string size, float value, float price) =>
        new RivalBuilding { Address = address, District = district, TypeKey = key, Type = type, Size = size, Value = value, Price = price };

    // ------------------------------------------------------------------------------------------------------- Market Insider

    /// <summary>Market Insider in one of its states.</summary>
    public static UiMarketModel Market(UiWords t, string state)
    {
        UiWords w = state.EndsWith("_fr") ? PersonaFrench(t) : t;
        MarketView v = MarketSample(w, state);
        var m = new UiMarketModel();
        MarketViews.Market(m, w, v);
        return m;
    }

    private static MarketView MarketSample(UiWords t, string state)
    {
        var v = new MarketView { Money = FullMoney };
        v.Districts = new[]
        {
            District("ba:neighborhood_garmentdistrict", t("ba:neighborhood_garmentdistrict", "Garment District")),
            District("ba:neighborhood_hellskitchen", t("ba:neighborhood_hellskitchen", "Hell's Kitchen")),
            District("ba:neighborhood_industrycity", t("ba:neighborhood_industrycity", "Industry City")),
            District("ba:neighborhood_lowermanhattan", t("ba:neighborhood_lowermanhattan", "Lower Manhattan")),
            District("ba:neighborhood_midtown", t("ba:neighborhood_midtown", "Midtown")),
            District("ba:neighborhood_murrayhill", t("ba:neighborhood_murrayhill", "Murray Hill")),
            District("ba:neighborhood_thehamptons", t("ba:neighborhood_thehamptons", "The Hamptons")),
            District("reallife_neighborhood_uptown", "Uptown"),   // the mod's districts as the game's tabs name them
            District("reallife_neighborhood_cedarheights", "Cedar Heights"),
        };
        v.District = 7;
        v.Working = 28;
        v.Middle = 49;
        v.Upper = 23;
        string price = t("market_insider_average_building_price", "Average <lowercase>{buildingType}</lowercase> price/{unit}").Replace("{unit}", "m²");
        v.EstateLabels = new[]
        {
            t("market_insider_total_buildings", "Total buildings"), MarketViews.Plain(price.Replace("{buildingType}", t("ba:buildingtype_retail", "Retail"))),
            MarketViews.Plain(price.Replace("{buildingType}", t("ba:buildingtype_office", "Office"))),
            MarketViews.Plain(price.Replace("{buildingType}", t("ba:buildingtype_warehouse", "Warehouse"))),
            MarketViews.Plain(price.Replace("{buildingType}", t("ba:buildingtype_residential", "Residential"))),
        };
        v.EstateValues = new[] { "214", "$6,840", "$5,920", "$2,310", "$7,450" };
        v.Table = state.StartsWith("estate") ? UiMarket.RealEstate : state.Contains("events") || state == "uptown_quiet" ? UiMarket.Events : UiMarket.Demands;
        v.Selected = 2;
        if (v.Table != UiMarket.RealEstate)
        {
            v.Demands = new[]
            {
                Demand(t("ba:itemname_cupofcoffee", "Cup Of Coffee"), 92, "$4.20", 0.8f, false, 3),
                Demand(t("ba:itemname_burger", "Burger"), 84, "$8.50", 1.1f, false, 2),
                Demand(t("ba:itemname_croissant", "Croissant"), 77, "$3.10", 0.7f, false, 1),
                Demand(t("ba:itemname_cheapflower", "Flower (Cheap)"), 71, null, 0.9f, false, 0),
                Demand(t("ba:itemname_laptop", "Laptop"), 66, "$612.00", 1.3f, false, 4),
                Demand(t("ba:itemname_hotdog", "Hotdog"), 58, "$3.40", 1.0f, false, 2),
                Demand(t("ba:itemname_classicphone", "Classic Phone"), 49, "$88.00", 1.0f, false, 1),
                Demand(t("ba:itemname_bottleofwine", "Bottle of Wine"), 44, "$19.90", 0.6f, false, 2),
                Demand(t("ba:itemname_donut", "Donut"), 37, "$2.40", 0.5f, false, 1),
                Demand(t("ba:itemname_sofaluxury", "Sofa (Luxury)"), 31, "$940.00", 1.2f, false, 0),
                Demand(t("ba:itemname_cheapgift", "Gift (Cheap)"), 22, "$25.00", 0.9f, false, 3),
                Demand(t("ba:itemname_paper", "Paper Ream"), 14, "$6.30", 0.7f, false, 2),
            };
            v.All = 38;
            v.SortColumn = 1;
            v.SortDown = true;
        }
        else
        {
            v.ForSaleOnly = state == "estate_for_sale";
            var places = new List<RivalBuilding>
            {
                Building("14 Riverside Drive", null, "ba:buildingtype_retail", t("ba:buildingtype_retail", "Retail"), "240 m²", 1640000f, 1720000f),
                Building("88 Cedar Lane", null, "ba:buildingtype_residential", t("ba:buildingtype_residential", "Residential"), "140 m²", 1040000f, 1095000f),
                Building("3 Harbour View", null, "ba:buildingtype_office", t("ba:buildingtype_office", "Office"), "860 m²", 5090000f, 5300000f),
                Building("201 North Avenue", null, "ba:buildingtype_warehouse", t("ba:buildingtype_warehouse", "Warehouse"), "1,800 m²", 4150000f, 4420000f),
                Building("57 Linden Street", null, "ba:buildingtype_retail", t("ba:buildingtype_retail", "Retail"), "190 m²", 1300000f, 1360000f),
                Building("9 Orchard Square", null, "ba:buildingtype_residential", t("ba:buildingtype_residential", "Residential"), "310 m²", 2300000f, 2480000f),
            };
            if (!v.ForSaleOnly)
            {
                places.Insert(1, Building("120 North Avenue", null, "ba:buildingtype_office", t("ba:buildingtype_office", "Office"), "1,120 m²", 6620000f, 0f));
                places.Insert(4, Building("40 Linden Street", null, "ba:buildingtype_retail", t("ba:buildingtype_retail", "Retail"), "210 m²", 1440000f, 0f));
            }
            places.Sort((a, b) => a.Price.CompareTo(b.Price));
            v.Places = places.ToArray();
            v.All = 214;
            v.SortColumn = 4;
        }
        v.Filtered = false;
        if (state == "filter_open")
        {
            v.OpenList = 0;
            v.Options = new[] { "All business types", "Coffee Shop", "Fast Food Restaurant", "Supermarket", "Clothing Store", "Florist", "Electronics Store", "Bookstore", "Gift Shop" };
            v.OptionChosen = 0;
        }
        if (state != "uptown_quiet")
        {
            v.Events = new[]
            {
                Event("Hype", t("marketeventtype_hype", "Hype"), 412, Hype(t, t("ba:itemname_cupofcoffee", "Cup Of Coffee"))),
                Event("BusinessOpened", t("marketeventtype_businessopened", "Business opened"), 410,
                    MarketViews.Plain(t("marketeventtype_businessopened_description", "{rivalName} opened <b>{businessName}</b> at {address}.").Replace("{rivalName}", "Huang Guo")
                        .Replace("{businessName}", "Golden Dragon Supermarket").Replace("{address}", "33 North Avenue"))),
                Event("ProductShortage", t("marketeventtype_productshortage", "Product shortage"), 407,
                    MarketViews.Plain(t("marketeventtype_productshortage_description", "{source} is currently experiencing a shortage of {product}. Expected duration is {durationInDays} days.")
                        .Replace("{source}", "Riverside Wholesale").Replace("{product}", t("ba:itemname_croissant", "Croissant")).Replace("{durationInDays}", "4"))),
                Event("BusinessClosed", t("marketeventtype_businessclosed", "Business closed"), 401,
                    MarketViews.Plain(t("marketeventtype_businessclosed_description", "<b>{businessName}</b> at {address} owned by {rivalName} was shut down.")
                        .Replace("{businessName}", "Linden Florist").Replace("{address}", "61 Linden Street").Replace("{rivalName}", "Owen Pryce"))),
                Event("Hype", t("marketeventtype_hype", "Hype"), 396, Hype(t, t("ba:itemname_laptop", "Laptop"))),
            };
        }
        return v;
    }

    private static MarketDistrict District(string key, string name) => new MarketDistrict { Key = key, Name = name };

    private static string Hype(UiWords t, string item) => MarketViews.Plain(t("marketeventtype_hype_description",
        "Citizens in {neighbourhood} are recently showing strong demand for <b>{itemName}</b>.").Replace("{neighbourhood}", "Uptown").Replace("{itemName}", item));

    private static MarketDemand Demand(string product, int demand, string lowest, float import, bool service, int providers) =>
        new MarketDemand { Product = product, Demand = demand, Lowest = lowest, Import = import, Service = service, Providers = providers };

    private static MarketEvent Event(string kind, string title, int day, string body) => new MarketEvent { Kind = kind, Title = title, Day = day, Body = body };

    private static string Fill(string text, string name, string value) => (text ?? string.Empty).Replace("{" + name + "}", value);

    // The game's words in French over the words the checks and previews were given, as a French player reads these apps:
    // the game's own keys in French, the mod's new keys in English until the next translation batch.
    private static UiWords PersonaFrench(UiWords t)
    {
        var fr = new Dictionary<string, string>
        {
            { "common_days_left", "Plus que {days} jours" }, { "common_hours_left", "Plus que {hours} heures" }, { "persona_education_completed", "Terminé" },
            { "persona_education_not_started", "Pas encore commencé" }, { "persona_education_completed_percentage", "{percentageComplete}% terminé" },
            { "personalgoal_bank_balance", "Atteindre un solde bancaire de {amount}" },
            { "personalgoal_business_weekly_income", "Atteindre un revenu hebdomadaire de {amount} pour une entreprise" },
            { "personalgoal_minemployees", "Employer au moins {minimumEmployees} personnes" },
            { "personalgoal_realestate", "Posséder au moins {amount} bâtiments" }, { "personalgoal_taxi_rides", "Effectuer {amount} trajets en taxi" },
            { "personalgoal_taxes_paid", "Payer au moins {amount} d'impôts" },
            { "personalgoal_dailycustomers", "Atteindre au moins {amount} clients par jour pour une entreprise" },
            { "personalgoal_items_in_stock", "Stocker au moins {amount} unités de n'importe quel produit" },
            { "personalgoal_casinowin", "Gagner un total de {amount} au casino" }, { "personalgoal_owned_vehicles", "Posséder un total de {amount} véhicules" },
            { "personalgoal_employee_max_skill", "Atteindre le niveau {amount}% dans n'importe quelle compétence pour un employé" },
            { "personalgoal_age", "Fêtez vos {amount} ans" }, { "common_weekly_income", "Revenu hebdomadaire" }, { "common_businesses", "Entreprises" },
            { "rivals_defense_icons_price_reduction_title", "Réduction de prix - {days} jour(s) restant(s)" },
            { "rivals_defense_icons_price_reduction_description", "{rivalName} a baissé ses prix sur vos produits les plus vendus pour vous voler vos clients" },
            { "rivals_defense_icons_hire_best_employees_title", "Débauchage des meilleurs employés - {days} jour(s) restant(s)" },
            { "rivals_defense_icons_hire_best_employees_description", "{rivalName} débauche vos employés" },
            { "ba:businesstype_fastfoodrestaurant", "Restaurant fast-food" }, { "ba:businesstype_coffeeshop", "Café" },
            { "ba:businesstype_clothingstore", "Magasin de vêtements" }, { "ba:businesstype_supermarket", "Supermarché" },
            { "ba:businesstype_jewelrystore", "Bijouterie" }, { "ba:businesstype_headquarters", "Siège social" }, { "ba:businesstype_florist", "Fleuriste" },
            { "ba:businesstype_electronicsstore", "Magasin d'électronique" }, { "ba:businesstype_bookstore", "Librairie" },
            { "ba:businesstype_warehouse", "Entrepôt" }, { "ba:buildingtype_office", "Bureaux" }, { "ba:buildingtype_retail", "Commerce" },
            { "ba:buildingtype_warehouse", "Entrepôt" }, { "ba:buildingtype_residential", "Résidentiel" },
            { "ba:neighborhood_garmentdistrict", "Garment District" }, { "ba:neighborhood_hellskitchen", "Hell's Kitchen" },
            { "ba:neighborhood_industrycity", "Industry City" }, { "ba:neighborhood_lowermanhattan", "Lower Manhattan" },
            { "ba:neighborhood_midtown", "Midtown" }, { "ba:neighborhood_murrayhill", "Murray Hill" }, { "ba:neighborhood_thehamptons", "Les Hamptons" },
            { "market_insider_average_building_price", "Prix moyen d'un(e) <lowercase>{buildingType}</lowercase>/{unit}" },
            { "market_insider_total_buildings", "Nombre total de bâtiments" }, { "ba:itemname_cupofcoffee", "Tasse de café" },
            { "ba:itemname_burger", "Burger" }, { "ba:itemname_croissant", "Croissant" }, { "ba:itemname_cheapflower", "Fleur (Bon marché)" },
            { "ba:itemname_laptop", "Ordinateur portable" }, { "ba:itemname_hotdog", "Hot-dog" }, { "ba:itemname_classicphone", "Téléphone classique" },
            { "ba:itemname_bottleofwine", "Bouteille de vin" }, { "ba:itemname_donut", "Donut" }, { "ba:itemname_sofaluxury", "Canapé (Luxe)" },
            { "ba:itemname_cheapgift", "Cadeau (bon marché)" }, { "ba:itemname_paper", "Rame de papier" }, { "marketeventtype_hype", "Engouement" },
            { "marketeventtype_businessopened", "Ouverture d’entreprise" },
            { "marketeventtype_businessopened_description", "{rivalName} a ouvert <b>{businessName}</b> à {address}." },
            { "marketeventtype_productshortage", "Pénurie de produit" },
            { "marketeventtype_productshortage_description", "{source} connaît actuellement une pénurie de {product}. La durée prévue est de {durationInDays} jours." },
            { "marketeventtype_businessclosed", "Fermeture d’entreprise" },
            { "marketeventtype_businessclosed_description", "<b>{businessName}</b> à {address}, appartenant à {rivalName}, a été fermé." },
            { "marketeventtype_hype_description", "Les habitants de {neighbourhood} montrent récemment une forte demande pour <b>{itemName}</b>." },
            { "smartphone_persona", "Persona" }, { "persona_education", "Éducation" }, { "common_years_amount", "{years} ans" },
            { "persona_characterinfo_networth", "Valeur nette totale : <b>{amount}</b>" }, { "persona_characterinfo_businesses", "Nombre total d'entreprises" },
            { "persona_characterinfo_employees", "Nombre total d’employés" }, { "persona_personal_wealth", "Richesse personnelle" },
            { "persona_cash", "Trésorerie" }, { "common_investments", "Investissements" }, { "persona_assets", "Actifs" }, { "econoview_loans", "Prêts" },
            { "common_energy", "Énergie" }, { "common_hunger", "Faim" }, { "common_happiness", "Bonheur" }, { "common_day_number", "Jour {number}" },
            { "bizman_status", "Statut" }, { "common_businessschool", "École de commerce de Manhattan" },
            { "persona_personalgoal_current_goals", "Objectifs actuels" }, { "persona_personalgoal_steam_achievements", "Steam" },
            { "smartphone_rivals", "Rivaux" }, { "rivals_leaderboard", "Classement" }, { "rivals_real_estate_properties", "Biens immobiliers" },
            { "rivals_overview", "Aperçu des rivaux" }, { "rivals_defense_icons_active_rivalry_title", "Rivalité active" },
            { "open_in_contacts", "Ouvrir dans Contacts" }, { "common_name", "Nom" }, { "common_business_name", "Nom de l'entreprise" },
            { "common_business_type", "Type d’entreprise" }, { "common_address", "Adresse" }, { "common_show_on_map", "Afficher sur la carte" },
            { "common_setdestination", "Définir la destination" }, { "common_neighborhood", "Quartier" }, { "common_building_type", "Type de bâtiment" },
            { "bizman_building_total_size", "Taille totale" }, { "real_estate_estimated_value", "Valeur estimée" }, { "common_price", "Prix" },
            { "rivals_business_development", "Développement de l'entreprise" }, { "market_insider_citizen_data", "Données des citoyens" },
            { "socialclass_working", "Classe ouvrière" }, { "socialclass_middle", "Classe moyenne" }, { "socialclass_upper", "Classe supérieure" },
            { "market_insider_real_estate_data", "Données immobilières" }, { "phone_view_on_map_button", "Voir sur la carte" },
            { "common_market_demands", "Demande du marché" }, { "common_real_estate", "Immobilier" }, { "common_events", "Événements" },
            { "market_insider_show_only_for_sale", "Afficher uniquement à la vente" }, { "common_search", "Rechercher" }, { "common_product", "Produit" },
            { "marketinsider_demand", "Demande" }, { "marketinsider_lowest_market_price", "Prix le plus bas du marché" },
            { "marketinsider_import_price_index", "Indice des prix à l’importation" }, { "marketinsider_providers", "Fournisseurs" }
        };
        return (key, english) => fr.TryGetValue(key, out string words) ? words : t(key, english);
    }
}
