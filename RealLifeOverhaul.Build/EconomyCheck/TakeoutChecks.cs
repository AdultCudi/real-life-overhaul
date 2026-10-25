using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using RealLifeOverhaul;
using RealLifeOverhaul.Meals;

// Takeout and plates (Scripts/Meals, Home/HomeTakeout): where the food goes at a seat and what Eat eats, the home portions
// packed to take (the carry's limit, the average, nothing lost going back to the leftovers, the save line read by builds
// with and without them); then, over the sources, that the food fills once and only through the game's own eating (or home
// cooking's), that a seat of the game's rest stays the game's, that nothing runs per frame, and the texts in every language.
internal static partial class Program
{
    private static void Takeout()
    {
        TakeoutRuleChecks();
        TakeoutCarryChecks();
        TakeoutSourceChecks();
        TakeoutTextChecks();
    }

    private static void TakeoutRuleChecks()
    {
        // Where the food goes: the place's tray, a plate on the table, a plate in the lap, the hand, in that order.
        Check(MealsRules.Serve(true, false, true, true) == Plate.Tray && MealsRules.Serve(true, false, false, false) == Plate.Tray,
            "takeout: at a table with its own tray, the place's tray");
        Check(MealsRules.Serve(true, true, true, true) == Plate.Table, "takeout: a dish of the player's own comes on a plate, never the place's tray");
        Check(MealsRules.Serve(false, false, true, true) == Plate.Table && MealsRules.Serve(false, true, false, true) == Plate.Lap,
            "takeout: a plate on the table top where there is one, else in the lap");
        Check(MealsRules.Serve(false, false, false, false) == Plate.Hand && MealsRules.Serve(true, true, false, false) == Plate.Hand,
            "takeout: with no table and no lap, the hand");
        Check(MealsRules.LapUp > 0.45f && MealsRules.LapUp < 0.75f && MealsRules.LapAhead > 0.1f && MealsRules.LapAhead < MealsRules.PlateAhead,
            "takeout: the lap is above a seat of ordinary height, below a dining table's top, and nearer than a table's plate");

        // What can be eaten from the bag: what the game's own inventory would let the player eat, and a meal.
        Check(MealsRules.Eatable(true, false, true, 30f), "takeout: a bought, opened meal of the game's can be eaten at a seat");
        Check(!MealsRules.Eatable(false, false, true, 30f), "takeout: food not paid for yet (still in the shop's basket) is not");
        Check(!MealsRules.Eatable(true, true, true, 30f) && !MealsRules.Eatable(true, false, false, 30f), "takeout: a sealed box or something not to eat is not");
        Check(!MealsRules.Eatable(true, false, true, 5f) && MealsRules.Eatable(true, false, true, MealsRules.SeatedAtLeast), "takeout: a sip of a drink is not a meal to sit down to; a meal is");

        // What Eat eats: the bag in hand first, then a packed portion.
        Check(MealsRules.Choose(true, 2) == TakeoutFood.Bag && MealsRules.Choose(true, 0) == TakeoutFood.Bag, "takeout: the meal in the bag in hand is eaten first");
        Check(MealsRules.Choose(false, 1) == TakeoutFood.Packed, "takeout: with nothing in the bag, a packed home portion");
        Check(MealsRules.Choose(false, 0) == TakeoutFood.None && MealsRules.Choose(false, -3) == TakeoutFood.None, "takeout: nothing carried, nothing to eat");

        // When Eat is in the rest panel.
        Check(MealsRules.Offered(true, true, false, TakeoutFood.Bag) && MealsRules.Offered(true, true, false, TakeoutFood.Packed), "takeout: seated at rest with food, Eat is offered");
        Check(!MealsRules.Offered(false, true, false, TakeoutFood.Bag), "takeout: with the option off, never (the game as before)");
        Check(!MealsRules.Offered(true, false, false, TakeoutFood.Bag), "takeout: not unless the game's own rest has the player on a seat");
        Check(!MealsRules.Offered(true, true, true, TakeoutFood.Bag), "takeout: not while a meal is on: one at a time");
        Check(!MealsRules.Offered(true, true, false, TakeoutFood.None), "takeout: not with nothing to eat");
    }

    private static void TakeoutCarryChecks()
    {
        var empty = new Dictionary<string, string>();
        HomeState.Load(empty);
        Check(HomeState.Takeout == 0 && HomeState.TakeoutFill == 0, "takeout carry: a new save carries nothing");

        Check(HomeState.Pack(1, 8, 50) == 1 && HomeState.Takeout == 1 && HomeState.TakeoutDish == 8 && HomeState.TakeoutFill == 50, "takeout carry: one portion packed");
        Check(HomeState.Pack(3, 9, 30) == HomeBalance.TakeoutMost - 1 && HomeState.Takeout == HomeBalance.TakeoutMost, "takeout carry: no more than the bag holds: " + HomeState.Takeout);
        Check(HomeState.TakeoutFill == 40 && HomeState.TakeoutDish == 9, "takeout carry: one lot, the average fill, the last dish: " + HomeState.TakeoutFill);
        Check(HomeState.Pack(1, 8, 50) == 0 && HomeState.Takeout == HomeBalance.TakeoutMost, "takeout carry: with the bag full nothing more is packed (it goes with the leftovers)");

        // The save line, read back by this build and by a build that came before takeout.
        var data = new Dictionary<string, string>();
        HomeState.Save(data);
        string line = data.TryGetValue(HomeState.Key, out string saved) ? saved : string.Empty;
        HomeState.Load(data);
        Check(line.StartsWith("h1;", StringComparison.Ordinal) && HomeState.Takeout == HomeBalance.TakeoutMost && HomeState.TakeoutDish == 9 && HomeState.TakeoutFill == 40,
            "takeout carry: the packed portions come back from the save under the same version");
        var old = new Dictionary<string, string> { { HomeState.Key, Regex.Replace(line, @";tk[odf]=[^;]*", string.Empty) } };
        HomeState.Load(old);
        Check(!old[HomeState.Key].Contains("tko") && HomeState.Takeout == 0 && HomeState.Loaded, "takeout carry: a save from before takeout loads, carrying nothing");
        var tooMany = new Dictionary<string, string> { { HomeState.Key, line.Replace(";tko=" + HomeBalance.TakeoutMost, ";tko=9") } };
        HomeState.Load(tooMany);
        Check(HomeState.Takeout == HomeBalance.TakeoutMost, "takeout carry: a line carrying more than the bag holds is read as a full bag: " + HomeState.Takeout);

        // Eaten one at a time; then nothing.
        HomeState.Load(data);
        int first = HomeState.TakeOne();
        int second = HomeState.TakeOne();
        int third = HomeState.TakeOne();
        Check(first == 40 && second == 40 && third == 0 && HomeState.Takeout == 0 && HomeState.TakeoutDish == 0 && HomeState.TakeoutFill == 0,
            "takeout carry: each portion eaten fills by its fill, the last empties the bag, an empty bag gives nothing");

        // The option off: back with the leftovers as far as the fridge has room, the rest still packed. Nothing lost.
        HomeState.Load(empty);
        HomeState.PutBy(HomeBalance.LeftoversMost - 1, 3, 30);
        HomeState.Pack(2, 8, 50);
        int before = HomeState.Leftovers + HomeState.Takeout;
        int back = HomeState.Unpack();
        Check(back == 1 && HomeState.Leftovers == HomeBalance.LeftoversMost && HomeState.Takeout == 1 && HomeState.Leftovers + HomeState.Takeout == before,
            "takeout carry: switched off, the packed portions go back with the leftovers as far as there is room, none lost");
        HomeState.Leftovers = 0;
        Check(HomeState.Unpack() == 1 && HomeState.Takeout == 0 && HomeState.TakeoutDish == 0, "takeout carry: with room, all of them");
        Check(HomeState.Unpack() == 0, "takeout carry: nothing packed, nothing put back");
        HomeState.Reset();
    }

    private static string TakeoutSource(string relative)
    {
        return MealCode(ModSource(relative));
    }

    private static void TakeoutSourceChecks()
    {
        string game = TakeoutSource(@"Meals\TakeoutGame.cs");
        string eat = MemberText(game, "public static bool EatFromBag(CargoInstance food)");
        Check(game.Length > 0 && Occurs(game, "TryToConsume()") == 1 && Occurs(game, "ReduceFromCargo(") == 1
              && InOrder(eat, "!bag.cargoInstances.Contains(food)", "if (!food.ItemCached.TryToConsume())", "return false;", "bag.ReduceFromCargo(food, 1);"),
            "takeout sources: a meal from the bag is eaten by the game's own consume and one less in the bag, once, as the game's inventory does");
        Check(!Regex.IsMatch(game, @"ChangeHunger|SetCurrentHunger|ChangeMoney|GenerateEnergy|GameEvent\.Invoke|RemoveFromCargo"),
            "takeout sources: nothing of the food's effect is done beside the game's own consume");
        Check(MemberText(game, "public static bool Resting(PlayerController player, out Transform place)").Contains("panel.GetCurrentActivity is RestActivity rest")
              && game.Contains("player.Character.isSittingOn"),
            "takeout sources: Eat is offered only on a seat the game's own rest has the player on");

        string home = TakeoutSource(@"Home\HomeTakeout.cs");
        string one = MemberText(home, "public static bool EatOne()");
        Check(Occurs(home, "ChangeHunger(") == 1 && InOrder(one, "Mathf.RoundToInt(save.Hunger) >= 100", "return false;", "HomeState.TakeOne()", "host.OwnMeal = Meal.Home;", "game.ChangeHunger(fill);"),
            "takeout sources: a packed portion is refused when too full before it is taken, then fills once as a home-cooked meal");

        string director = TakeoutSource(@"Meals\MealsDirector.Takeout.cs");
        string main = TakeoutSource(@"Meals\MealsDirector.cs");
        Check(InOrder(MemberText(director, "public void EatHere()"), "_hereArmed = seat;", "TakeoutGame.EatFromBag(food)", "MealsBridges.EatCarried()", "if (_hereArmed != null)", "_hereArmed = null;"),
            "takeout sources: the seat is armed only for the food event of the press, and disarmed when none came");
        string onEvent = MemberText(main, "private void OnGameEvent(string gameEvent)");
        Check(InOrder(onEvent, "if (_hereArmed != null)", "_hereArmed = null;", "Begin(seat, false, rise, _hereArmedWhat, true, _hereArmedOwn);", "return;", "if (!Settings.MealsSeated")
              && Occurs(director, "Begin(") == 1,
            "takeout sources: the press's food event begins the meal at the rested seat, and only that one");
        Check(InOrder(MemberText(main, "private void Begin(MealSeat seat, bool cooked, float fill, string what, bool here = false, bool own = false)"), "if (!here)", "MealsGame.Reserve(seat, true);")
              && MemberText(main, "public void End(bool eaten, string why, bool stopWalk)").Contains("if (seat != null && !here)"),
            "takeout sources: a seat of the game's rest is neither kept nor given back by the meal: the game keeps it while the player rests");
        string step = MemberText(director, "private void HereStep(PlayerController player)");
        Check(InOrder(step, "TakeoutGame.Resting(player, out Transform place)", "End(", "MealsGame.BiteOver(player)", "Serve();", "End(true") && !Regex.IsMatch(step, @"\bnew\b|""\s*\+|\+\s*""|\$""|ToString\("),
            "takeout sources: a meal at the rested seat ends with the rest, waits for the game's bite, is served by the one path, and allocates nothing a frame");
        Check(Occurs(main, "Serve();") == 1 && Occurs(director, "Serve();") == 1 && main.Contains("private void Serve()") && !director.Contains("private void Serve()"),
            "takeout sources: one serving for a meal walked to and a meal at the rested seat");
        Check(director.Contains("body.onSittingChanged += _onSitting;") && director.Contains("_sitter.onSittingChanged -= _onSitting;") && main.Contains("_onSitting = Guard.Wrap<bool>(OnSitting);")
              && MemberText(main, "private void OnDestroy()").Contains("Unbind();"),
            "takeout sources: the player's own sitting is heard through one guarded handler, made once and taken off with the city");

        string button = TakeoutSource(@"Meals\TakeoutButton.cs");
        Check(button.Contains("UiClone.Asleep(template.gameObject") && button.Contains("private void OnEnable()") && !Regex.IsMatch(button, @"void (Update|LateUpdate|FixedUpdate)\("),
            "takeout sources: Eat is a copy of the panel's own button, untagged so the game's rebuild keeps it, decided on events and never per frame");
        Check(ModSource(@"Core\GameContract.cs").Contains("Field(TakeoutPlates, Game, \"PlayerActivity.PlayerActivityUI\", \"buttonTemplate\", \"Button\")"),
            "takeout sources: the panel's button template is a line of the game contract");

        string cook = TakeoutSource(@"Home\CookActivity.cs");
        string end = MemberText(cook, "protected override void End(PlayerController player, bool completed, int minutes)");
        Check(InOrder(end, "bool pack = _plan.Pack && Settings.MealsTakeout;", "HomeState.Pack(1,", "bool eats = !full && !pack;", "if (eats)", "ChangeHunger(fill);", "if (!eats)", "float poison")
              && Occurs(end, "ChangeHunger(") == 1 && end.Contains("_plan.Portions - (eats ? 1 : 0) - packed"),
            "takeout sources: a dish packed to take is not eaten at the cooker, and every portion is eaten, packed or put by, never two of them");
        string list = TakeoutSource(@"Home\HomeCookList.cs");
        Check(list.Contains("plan.Pack = _pack && Settings.MealsTakeout;") && InOrder(list, "if (!Settings.MealsTakeout && HomeState.Takeout > 0)", "HomeState.Unpack()")
              && list.Contains("if (Settings.MealsTakeout)"),
            "takeout sources: the serve row is there only with the option on; off, packed portions go back with the leftovers");

        string links = ModSource(@"Wiring\Wiring.MealsLinks.cs");
        Check(links.Contains("MealsBridges.EatCarried = HomeTakeout.EatOne;") && links.Contains("MealsBridges.EatCarried = null;") && links.Contains("MealsBridges.Carried = null;"),
            "takeout sources: meals reaches home cooking's packed portions only through its bridge, filled in and taken off by Wiring");
    }

    private static void TakeoutTextChecks()
    {
        string[] notices = { MealsText.TestArmed, MealsText.TestPass, MealsText.TestFail, MealsText.TestLapsed, MealsText.Packed, MealsText.PackedBurnt, MealsText.CarryFull, MealsText.BurntLost };
        var heading = new Dictionary<string, string> { { "en", "# Takeout and plates" }, { "pt", "# Comida para viagem e pratos" }, { "zh-cn", "# 外带与餐盘" } };
        foreach (KeyValuePair<string, string> said in heading)
        {
            Dictionary<string, string> words = LoadWords(System.IO.Path.Combine(DesignLocales, said.Key + ".json"));
            Check(words != null && words.TryGetValue(MealsText.Guide, out string page) && page.Contains(said.Value) && page.Contains("{meals_takeout_most}"),
                "takeout texts: the Meals Guide page has its takeout section in " + said.Key);
            foreach (string key in notices)
            {
                Check(words != null && words.TryGetValue(key, out string value) && value.Length <= 240, "takeout texts: the notice " + key + " is at most 240 characters in " + said.Key);
            }
            Check(words != null && words.TryGetValue(MealsText.ButtonEat, out string eat) && eat.Contains("{food}") && words.ContainsKey(MealsText.TakeoutTip),
                "takeout texts: the button names the food, and the option has its line, in " + said.Key);
        }
        Dictionary<string, string> en = LoadWords(System.IO.Path.Combine(DesignLocales, "en.json"));
        Check(en != null && en.TryGetValue(MealsText.Guide, out string enPage) && !enPage.Contains("you eat from your hands") && enPage.Contains("the plate goes in your lap"),
            "takeout texts: the Guide says a plate goes in the lap with no table, as it now does");
        var lines = new Dictionary<string, string>
        {
            { "en", "Takeout and plates: food you carry in a bag" }, { "pt", "Comida para viagem e pratos:" }, { "zh-cn", "外带与餐盘：" },
            { "de", "Essen zum Mitnehmen und Teller:" }, { "fr", "Plats à emporter et assiettes :" }, { "es", "Comida para llevar y platos:" },
            { "ru", "Еда с собой и тарелки:" }, { "nl", "Eten om mee te nemen en borden:" }, { "tr", "Paket yemek ve tabaklar:" },
        };
        foreach (KeyValuePair<string, string> line in lines)
        {
            Dictionary<string, string> words = LoadWords(System.IO.Path.Combine(DesignLocales, line.Key + ".json"));
            Check(AnyChangesRow(words, line.Value), "takeout texts: What's new says so in " + line.Key);
        }
    }
}
