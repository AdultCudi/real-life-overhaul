using System;
using System.Collections.Generic;
using RealLifeOverhaul.Economy;
using RealLifeOverhaul.Meta;

// Giving food that would spoil to charity (Economy/Logistics: FoodGiftRules, LogisticsDirector.Give, the Giving director's GiveFood): what an honest shop gains, that a shop
// that orders far more than it sells gains nothing more and ends poorer, that splitting stock over places or carrying it in on its last day earns no more, the weekly
// ceilings, that what is given is exactly what the turn would have thrown out and never both, that cold storage still decides when a piece is due, the option and the save.
internal static partial class Program
{
    private const int GiveCost = 475;      // cents a piece costs (a burger's wholesale price)
    private const int GivePrice = 1250;    // cents a piece sells for
    private const int GiveTaxPer100 = 30;  // per cent of a deduction that comes back as less tax: any rate under 100 leaves the gift a loss

    // What one month of a shop (or of several, sharing the player's ceilings) comes to.
    private sealed class GiveMonth
    {
        public long Money;                 // cents, after sales, orders and the tax saved
        public long Earned;                // cents of cost that earned
        public long Saved;                 // cents of tax those deductions saved
        public int Standing, Staff, Mood;  // points, days and days
        public int PlaceWeekMost, PlayerWeekMost, StaffWeekMost, MoodWeekMost;
        public int Given;                  // pieces given away in all
    }

    // Days 1 to 30: each place sells <sold> a day and orders <sold + left>, so left pieces are due tomorrow every evening and are given. Places share the player's rings.
    private static GiveMonth GiveRun(int places, int sold, int left, bool carriedIn = false)
    {
        var m = new GiveMonth();
        var sales = new WeekRing[places];
        var standing = new WeekRing[places];
        var staff = new WeekRing[places];
        var playerStanding = new WeekRing();
        var playerMood = new WeekRing();
        for (int p = 0; p < places; p++)
        {
            sales[p] = new WeekRing();
            standing[p] = new WeekRing();
            staff[p] = new WeekRing();
        }
        for (int day = 1; day <= 30; day++)
        {
            for (int p = 0; p < places; p++)
            {
                sales[p].Add(day, sold);
                m.Money += (long)sold * GivePrice - (long)(sold + left) * GiveCost;
                int earning = carriedIn ? 0 : FoodGiftRules.Earning(left, sales[p].Sum(day));   // stock carried in on its last day has no sales of its own to be measured against
                long cents = FoodGiftRules.ValueOf(earning, left, (long)left * GiveCost);
                m.Given += left;
                if (cents >= 100)
                {
                    FoodGiftRules.Grant(day, standing[p], staff[p], playerStanding, playerMood, out int points, out bool lift, out bool mood);
                    m.Standing += points;
                    m.Staff += lift ? 1 : 0;
                    m.Mood += mood ? 1 : 0;
                    m.Earned += cents;
                    m.Saved += cents * GiveTaxPer100 / 100;
                    m.Money += cents * GiveTaxPer100 / 100;
                }
            }
            for (int p = 0; p < places; p++)
            {
                m.PlaceWeekMost = Math.Max(m.PlaceWeekMost, standing[p].Sum(day));
                m.StaffWeekMost = Math.Max(m.StaffWeekMost, staff[p].Sum(day));
            }
            m.PlayerWeekMost = Math.Max(m.PlayerWeekMost, playerStanding.Sum(day));
            m.MoodWeekMost = Math.Max(m.MoodWeekMost, playerMood.Sum(day));
        }
        return m;
    }

    private static void FoodGifts()
    {
        // An honest shop sells 100 a day and wastes what its ordinary ordering leaves, about the share the rule lets earn (3 per cent of the week's 700 sold is 21 a day).
        int cap = FoodGiftRules.Earning(1000, 700);
        GiveMonth honest = GiveRun(1, 100, cap);
        long leftovers = (long)cap * GiveCost * 30;
        Check(cap == 21 && honest.Standing > 0 && honest.Mood > 0 && honest.Staff > 0 && honest.Earned > 0 && honest.Earned <= leftovers
              && honest.Money == 30L * 100 * GivePrice - 30L * 100 * GiveCost - leftovers + honest.Saved && honest.Money < 30L * 100 * (GivePrice - GiveCost),
            "food gifts: an honest shop gains a little standing, a good mood and a lift for its staff, and loses its real leftovers at what they cost, less only the tax a deduction saves");

        GiveMonth farmer = GiveRun(1, 100, 900);   // orders ten times what it sells, every day, on purpose
        Check(farmer.Money < honest.Money && farmer.Standing <= honest.Standing && farmer.Mood <= honest.Mood && farmer.Staff <= honest.Staff && farmer.Earned <= honest.Earned,
            "food gifts: a shop that orders ten times its sales every day for a month ends with less money and no more standing, mood, staff lift or deduction than the honest one");
        GiveMonth tenfold = GiveRun(1, 100, 100);   // and one that orders double
        Check(tenfold.Money < honest.Money && tenfold.Earned <= honest.Earned && farmer.Given == 27000 && honest.Given == 30 * cap,
            "food gifts: what is over the share is still given away but earns nothing more");

        GiveMonth many = GiveRun(10, 10, 2);   // the same stock split over ten places: each sells a tenth and has a tenth to give
        GiveMonth one = GiveRun(1, 100, 21);
        Check(many.Earned <= one.Earned && many.Standing <= 2 * 5 && one.Standing <= 2 * 5 && many.PlayerWeekMost <= FoodGiftRules.PlayerStandingWeek && many.Mood <= 3 * 5 && one.Mood <= 3 * 5,
            "food gifts: splitting the same stock over many places earns no more deduction than one place, and their standing and mood together hold to the weekly ceilings a single place can reach");
        GiveMonth moved = GiveRun(1, 100, 1000, carriedIn: true);
        GiveMonth movedWithSales = GiveRun(1, 0, 1000);
        Check(moved.Earned == 0 && moved.Standing == 0 && moved.Staff == 0 && moved.Mood == 0 && movedWithSales.Earned == 0 && movedWithSales.Standing == 0 && movedWithSales.Money < 0,
            "food gifts: stock carried into a place on its last day, or a place that sells nothing, is measured against that place's own sales and earns nothing");

        bool ceilings = honest.PlaceWeekMost <= FoodGiftRules.PlaceStandingWeek && honest.StaffWeekMost <= FoodGiftRules.StaffWeek && honest.MoodWeekMost <= FoodGiftRules.MoodWeek
                        && many.PlayerWeekMost <= FoodGiftRules.PlayerStandingWeek && many.PlaceWeekMost <= FoodGiftRules.PlaceStandingWeek && farmer.PlayerWeekMost <= FoodGiftRules.PlayerStandingWeek
                        && many.StaffWeekMost <= FoodGiftRules.StaffWeek && many.MoodWeekMost <= FoodGiftRules.MoodWeek;
        GiveMonth big = GiveRun(30, 100, 100);   // thirty places never pass the player's ceilings either
        ceilings &= big.PlayerWeekMost == FoodGiftRules.PlayerStandingWeek && big.MoodWeekMost == FoodGiftRules.MoodWeek && big.Standing <= 2 * 5 && big.Mood <= 3 * 5;
        Check(ceilings, "food gifts: the weekly ceilings hold: " + FoodGiftRules.PlaceStandingWeek + " points of standing a place and " + FoodGiftRules.PlayerStandingWeek
                        + " the player, " + FoodGiftRules.StaffWeek + " staff lifts a place and " + FoodGiftRules.MoodWeek + " good moods, however many places or how much is given");

        // A gift is always a loss: whatever the tax rate under 100 per cent, the saving is less than what the food cost.
        bool loss = true;
        for (int rate = 0; rate < 100; rate += 7)
        {
            loss &= honest.Earned * rate / 100 < honest.Earned && honest.Earned > 0;
        }
        Check(loss && FoodGiftRules.ValueOf(7, 10, 1001) == 700 && FoodGiftRules.ValueOf(0, 10, 500) == 0 && FoodGiftRules.ValueOf(5, 0, 500) == 0
              && FoodGiftRules.ValueOf(3, 3, 1001) + FoodGiftRules.ValueOf(7, 10, 1001) <= 1001 + 700,
            "food gifts: a gift is valued at what the stock cost, in whole cents rounded down, and a deduction saves only a share of it, so it is always a loss in money");
        Check(FoodGiftRules.Earning(50, 0) == 0 && FoodGiftRules.Earning(0, 700) == 0 && FoodGiftRules.Earning(5, 700) == 5 && FoodGiftRules.Earning(1000, 700) == 21 && FoodGiftRules.Earning(1000, 33) == 0
              && FoodGiftRules.Earning(int.MaxValue, int.MaxValue) <= int.MaxValue / 20,
            "food gifts: the pieces that earn are 3 per cent of the last seven days' sales of the product at the place, never more than were given, none with no sales");

        // What is given is exactly what the next turn would have thrown out, and never both.
        int life = SpoilRules.Life(SpoilClass.Prepared, false), cold = SpoilRules.Life(SpoilClass.Prepared, true);
        var stock = new List<Cohort> { new Cohort(5, 30), new Cohort(7, 10), new Cohort(9, 8) };
        int turnTomorrow = SpoilRules.Expired(stock, life, cold, 0, 9);
        int due = FoodGiftRules.DueTomorrow(stock, life, cold, 0, 8);
        SpoilRules.Take(stock, due, null);
        Check(due == turnTomorrow && due == 30 && SpoilRules.Expired(stock, life, cold, 0, 9) == 0 && SpoilRules.Total(stock) == 18 && FoodGiftRules.DueTomorrow(stock, life, cold, 0, 8) == 0,
            "food gifts: what the evening gives is exactly what the next turn of the day would have thrown out, taken from the stock once, so the turn finds none of it");
        var chilled = new List<Cohort> { new Cohort(5, 30), new Cohort(9, 10) };   // the newest ten are kept cold: they keep 8 days, not 4
        bool coldStays = FoodGiftRules.DueTomorrow(chilled, life, cold, 10, 12) == 30 && FoodGiftRules.DueTomorrow(chilled, life, cold, 10, 16) == 40
                         && FoodGiftRules.DueTomorrow(chilled, life, cold, 0, 12) == 40;
        Check(coldStays, "food gifts: cold storage still decides when a piece is due: a covered piece is not given the evening before an ordinary one would be");

        // The option, the save and the words.
        string settings = ModSource(@"Core\Settings.cs"), mod = ModSource(@"Core\RealLifeOverhaulMod.cs"), give = ModSource(@"Economy\Logistics\LogisticsDirector.Give.cs");
        string spoil = ModSource(@"Economy\Logistics\LogisticsDirector.Spoil.cs"), giving = ModSource(@"Giving\GivingDirector.Gifts.cs");
        TreeRow row = OptionsTree.Row("living_spoilage_give");
        Check(settings.Contains("LivingSpoilageGiveId = \"living_spoilage_give\"") && settings.Contains("public static bool LivingSpoilageGive = false;")
              && settings.Contains("LivingSpoilageGive = Bool(modId, LivingSpoilageGiveId, LivingSpoilageGive)")
              && mod.Contains(".AddToggle(Settings.LivingSpoilageGiveId, \"reallife_spoil_give\", Settings.LivingSpoilageGive,")
              && row != null && !row.IsButton && OptionsTree.SectionOf("living_spoilage_give").Id == "trips" && Array.IndexOf(row.Needs, "living_spoilage") >= 0
              && row.TipKey == "reallife_opt_tip_living_spoilage_give",
            "food gifts: the option is off to begin with, saved and read back, in the section of food spoilage and needing it, with its sentence");
        string evening = MemberText(give, "private void GiveEvening(BuildingRegistration reg, SpoilPlace p, int today)");
        Check(give.Contains("public static bool GiveWanted => SpoilWanted && Settings.LivingSpoilageGive;") && evening.Contains("if (!GiveWanted || p.GaveDay == today")
              && spoil.Contains("if (GiveWanted && onSite < had)") && spoil.Replace("\r\n", "\n").Contains("if (GiveWanted)\n"),
            "food gifts: with the option off nothing is given, no sale is counted and nothing is saved, so the game goes on exactly as without it");
        Check(evening.Contains("FoodGiftRules.DueTomorrow(kv.Value, life, coldLife, _spoilChilled[at], today)") && evening.Contains("SpoilRules.Take(kv.Value, gone, null)")
              && evening.Contains("LogisticsGame.Discard(reg, kv.Key, due, out int paid)") && evening.Contains("p.GaveDay = today;") && evening.Contains("FoodGiftRules.Earning(gone, sold)")
              && evening.Contains("FoodGiftRules.ValueOf(FoodGiftRules.Earning(gone, sold), gone, paid)") && !evening.Contains("SellingPrice") && !evening.Contains("1250")
              && evening.Contains("LogisticsBridges.FoodGiftReady"),
            "food gifts: the evening takes exactly the pieces due at the next turn out of the stock and the batches once a day a place, at what they cost, and only when giving can take them");
        Check(giving.Contains("public bool GiveFood(long given, long earned)") && giving.Contains("_state.TaxGifts += earned;") && giving.Contains("_state.Credit(Charity.FoodBank, given, -1);")
              && giving.Contains("public bool FoodGiftReady => Running && CauseWhy == 0;") && !MemberText(giving, "public bool GiveFood(long given, long earned)").Contains("GivingGame.Pay"),
            "food gifts: the gift is recorded as a giving at what the stock cost (the year's gifts and so the tax deduction), puts it in the food bank, moves no cash and is never taken in a Going Public session");

        // The state: a week of sales, a place's and the player's weeks survive a save, and a state that gave nothing writes the line it always wrote.
        var s = new SpoilState();
        SpoilPlace pl = s.Place("Main%20St~3");
        SpoilRules.Add(pl.Of("ba:itemname_burger"), 6, 12);
        pl.SoldRing("ba:itemname_burger").Add(5, 40);
        pl.SoldRing("ba:itemname_burger").Add(6, 60);
        pl.GaveStanding.Add(6, 1);
        pl.GaveStaff.Add(6, 1);
        s.GaveStanding.Add(6, 1);
        s.GaveMood.Add(6, 1);
        string line = SpoilStore.Write(s);
        SpoilState back = SpoilStore.Read(line);
        string plain = SpoilStore.Write(new SpoilState());
        var quiet = new SpoilState();
        quiet.Place("a~1").SoldRing("ba:itemname_burger");
        Check(back != null && SpoilStore.Write(back) == line && back.Places["Main%20St~3"].Sold["ba:itemname_burger"].Sum(6) == 100 && back.Places["Main%20St~3"].GaveStanding.Sum(6) == 1
              && back.Places["Main%20St~3"].GaveStaff.On(6) == 1 && back.GaveStanding.Sum(6) == 1 && back.GaveMood.On(6) == 1 && plain == "v=1^N|-1" && SpoilStore.Write(quiet) == "v=1^P|a~1|-1|" + string.Join("|", new[] { "-1,-1,-1,-1,-1,-1,-1", "0,0,0,0,0,0,0", "0,0,0,0,0,0,0", "0,0,0,0,0,0,0" }) + "|^N|-1"
              && SpoilStore.Read("v=1^S|a|b|1,2,3|0,0,0^N|-1") == null,
            "food gifts' save: a place's sales, its standing and lift, and the player's week read back to the same line; a state that gave nothing writes the line it always wrote, so a save from before loads with nothing changed");

        Dictionary<string, string> en = LocaleFile("en");
        string tip = en.TryGetValue("reallife_opt_tip_living_spoilage_give", out string t) ? t : "", note = en.TryGetValue("reallife_spoil_note_gave", out string n) ? n : "";
        string page = en.TryGetValue("reallife_guide_trips", out string g) ? g : "";
        Check(tip.Length > 0 && tip.Length <= 190 && tip.Split(' ').Length <= 32 && note.Length <= 240 && note.Contains("{given}") && note.Contains("{worth}") && note.Contains("{count}")
              && en.TryGetValue("reallife_spoil_give", out string label) && label.Length > 0 && label.Length <= 40 && en.ContainsKey("reallife_giving_row_food")
              && page.Contains("<b>Give spoiling food to charity</b>") && page.Contains("{give_share}") && page.Contains("{give_player_week}") && page.Contains("always a loss in money")
              && WhatsNewHas(en, "Food spoilage can now give food that would be thrown out overnight to charity")
              && !WhatsNewHas(en, "Fixed: Food spoilage can now give"),
            "food gifts' texts: the option's label and sentence, the overnight notice, the Guide's bullet and What's new (a feature, no fix mark) are in English");
        string pages = ModSource(@"Ui\Ventures\GivePages.cs"), app = ModSource(@"Giving\GivingApp.cs"), screens = ModSource(@"Ui\UiScreens.Ventures.cs");
        Check(pages.Contains("reallife_giving_row_food") && app.Contains("reallife_giving_row_food") && screens.Contains("\"reallife_giving_row_food\""),
            "food gifts: the giving's line is on the Giving page of the old interface and of the kit, and in the kit's keys");
        string docs = System.IO.File.ReadAllText(System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\docs\features\delivery-trips.md")));
        string docsGiving = System.IO.File.ReadAllText(System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\docs\features\giving.md")));
        Check(docs.Contains("### Giving spoiling food to charity") && docs.Contains("always a loss in money") && docsGiving.Contains("Food in kind"),
            "food gifts: the feature pages state the rules, the share, the ceilings and where the gift is recorded");
    }
}
