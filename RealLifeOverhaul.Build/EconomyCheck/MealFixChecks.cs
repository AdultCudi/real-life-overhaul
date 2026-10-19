using System;

internal static partial class Program
{
    private static void MealFixes()
    {
        string catalogue = ModSource(@"Social\SocialCatalogue.cs");
        int meal = catalogue.IndexOf("Kind = PlanKind.Meal,", StringComparison.Ordinal);
        int serving = catalogue.IndexOf("MealItem = \"ba:itemname_burger\",", StringComparison.Ordinal);
        Check(catalogue.Contains("public string MealItem;") && meal > 0 && serving > meal
              && serving < catalogue.IndexOf("Invite =", meal, StringComparison.Ordinal)
              && Occurs(catalogue, "MealItem =") == 1,
            "meal fix: only the meal outing serves food, using the game's burger item");

        string after = ModSource(@"Social\OutingScene.After.cs");
        string settle = MemberText(after, "private void Settle(bool wentWell)");
        string scene = ModSource(@"Social\OutingScene.cs");
        Check(settle.Contains("if (_settled)") && settle.Contains("_settled = true;")
              && settle.IndexOf("_settled = true;", StringComparison.Ordinal)
                 < settle.IndexOf("Feed();", StringComparison.Ordinal)
              && Occurs(after, "Feed();") == 1
              && !scene.Contains("Feed(") && !settle.Contains(".Ate();")
              && MemberText(ModSource(@"Social\OutingScene.Meeting.cs"), "private void Arrived()").Contains("ClearPlan();"),
            "meal fix: the serving is settled once, never at start or abort, and the met plan cannot replay on load");

        string food = ModSource(@"Social\OutingScene.Food.cs");
        string feed = MemberText(food, "private void Feed()");
        Check(feed.Contains("save == null || food == null || food.saturation <= 0 || Mathf.RoundToInt(save.Hunger) >= 100")
              && feed.IndexOf("SocialLinks.Wellbeing?.Ate();", StringComparison.Ordinal) < feed.IndexOf("Mathf.RoundToInt(save.Hunger) >= 100", StringComparison.Ordinal)
              && feed.Contains("ItemsGetter.GetByName(_def.MealItem, true)")
              && feed.Contains("Mathf.Min(before + food.saturation, 100f)")
              && feed.Contains("EnergyHelper.SetCurrentHunger(wanted);")
              && !feed.Contains(".ChangeHunger("),
            "meal fix: a settled meal is recorded, and fills by live game saturation, capped at 100 with the game's full refusal");
        Check(Occurs(feed, "SocialLinks.Wellbeing?.Ate();") == 1
              && !food.Contains("HomeState") && !food.Contains("GameEvent.Invoke"),
            "meal fix: one bridge call records the meal, without a second food-eaten event");
        Check(feed.Contains("if (Meta.Dev.Verbose)") && Occurs(feed, "SocialDirector.Logger?.Info(") == 1
              && feed.Contains("Social check (meal): ") && feed.Contains("food.saturation")
              && feed.Contains("save.Hunger == wanted ? \"PASS.\" : \"FAIL.\""),
            "meal fix: one developer diagnostic compares the hunger after eating to the capped serving");
        Check(FixLineEverywhere("a meal out with a friend now fills you", "uma refeição fora com um amigo agora sacia sua fome",
                                "和朋友外出吃饭现在会增加饱腹度", "Beim Essen mit Freunden im Restaurant wirst du jetzt satt",
                                "un repas au restaurant avec un ami vous rassasie maintenant", "comer fuera con un amigo ahora te sacia",
                                "еда в ресторане с другом теперь утоляет голод", "uit eten met een vriend vult nu je maag",
                                "bir arkadaşla dışarıda yemek yemek artık açlığınızı giderir"),
            "meal fix: What's new marks the meal's fullness in every language");
    }
}
