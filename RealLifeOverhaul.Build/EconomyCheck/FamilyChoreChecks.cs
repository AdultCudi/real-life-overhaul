using System;
using System.Collections.Generic;
using RealLifeOverhaul;
using RealLifeOverhaul.Family;

// The household chores were a click on a question for a warmer home. Now each is asked only when it is really so and counts
// only once it has really changed (ChoreRules), face to face at home or by text. The rules are checked here as they are; the
// asking and the settling are the game's side, so they are checked in its source.
internal static partial class Program
{
    private static void FamilyChores()
    {
        Check(ChoreRules.Wanted(ChoreRules.FoodLow - 1, 90f, 90f, 0f, false) == ChoreKind.Groceries,
            "family chores: a nearly empty fridge asks for shopping");
        Check(ChoreRules.Wanted(-1, -1f, -1f, -1f, true) == ChoreKind.None && ChoreRules.Wanted(ChoreRules.FoodDone, 90f, 90f, 0.1f, true) == ChoreKind.None,
            "family chores: nothing is asked of a home that needs nothing, or of states that cannot be read");
        Check(ChoreRules.Wanted(2, 10f, 10f, 0.9f, true) == ChoreKind.Shower && ChoreRules.Wanted(2, 10f, 10f, 0.9f, false) == ChoreKind.Groceries,
            "family chores: a shower is asked first, and only face to face");
        Check(ChoreRules.Wanted(20, ChoreRules.TidyLow - 1f, 90f, 0f, false) == ChoreKind.Clean && ChoreRules.Wanted(20, 90f, 90f, ChoreRules.CarDirty + 0.1f, false) == ChoreKind.CarWash,
            "family chores: a messy home asks for a clean, a filthy car for a wash");
        Check(!ChoreRules.Done(ChoreKind.Groceries, -1, 0f, 0f, 0f) && !ChoreRules.Done(ChoreKind.Groceries, ChoreRules.FoodLow, 0f, 0f, 0f)
              && ChoreRules.Done(ChoreKind.Groceries, ChoreRules.FoodDone, 0f, 0f, 0f),
            "family chores: shopping counts once the fridge really holds enough, never from a fridge not read");
        Check(ChoreRules.Done(ChoreKind.Clean, 0, ChoreRules.TidyDone, 0f, 0f) && ChoreRules.Done(ChoreKind.Shower, 0, 0f, ChoreRules.HygieneDone, 0f)
              && ChoreRules.Done(ChoreKind.CarWash, 0, 0f, 0f, ChoreRules.CarDone) && !ChoreRules.Done(ChoreKind.CarWash, 0, 0f, 0f, -1f),
            "family chores: a clean, a shower and a car wash count by the home's tidiness, the player's hygiene and the car's own dirt");
        Check(ChoreRules.Due(ChoreKind.Shower, 10) == 10 && ChoreRules.Due(ChoreKind.Groceries, 10) == 12 && !ChoreRules.Textable(ChoreKind.Shower) && ChoreRules.Textable(ChoreKind.CarWash),
            "family chores: a shower is for today and never texted; the rest by the day after next");
        Check(ChoreRules.HarmonyDone > 0f && ChoreRules.HarmonyMissed < ChoreRules.HarmonyRefused && ChoreRules.HarmonyRefused < 0f,
            "family chores: a chore done warms the home; a promise broken cools it more than a plain no");

        string chores = ModSource(@"Family\Chores.cs");
        string at = ModSource(@"Family\HomeLife.AtHome.cs");
        Check(!ModSource(@"Family\HomeLife.cs").Contains("public static void Chore(int day, int hour)") && MemberText(ModSource(@"Family\FamilyCalendar.cs"), "public static void Hourly(int day, int hour)").Contains("Chores.Hourly(day, hour);"),
            "family chores: the old question with a button for harmony is gone; the chores run hourly");
        Check(chores.Contains("ChoreRules.Done(kind, food, tidy, hygiene, car)") && chores.Contains("if (agreed)") && chores.Contains("FamilyState.Harmony(ChoreRules.HarmonyDone);"),
            "family chores: harmony comes only from a chore agreed and really done");
        Check(at.Contains("(Chores.Pending || PartnerVoice.AnyAsk || (HasWord != null && HasWord()))") && at.Contains("Chores.Opening() ?? FamilyPhone.PartnerMenu(true)"),
            "family chores: at home the partner comes over and opens the talk with the chore");
        string links = ModSource(@"Wiring\FamilyLinks.cs");
        Check(links.Contains("HomePlace.ReadPantry(Pantry);") && links.Contains("most = Math.Max(most, car.dirtiness);") && links.Contains("? HomeState.Hygiene : -1f;"),
            "family chores: the fridge, the car's dirt and the player's hygiene are read from the game and Home, not assumed");
        string[] pieces = { "it counts once it is really done", "conta quando está feito de verdade", "只有真正做完才算", "es zählt erst, wenn es wirklich erledigt ist",
                            "cela compte une fois vraiment fait", "cuenta cuando está hecho de verdad", "засчитывается, только когда действительно сделано",
                            "het telt pas als het echt gedaan is", "ancak gerçekten yapıldığında sayılır" };
        Dictionary<string, string>[] langs = LanguageWords(out string[] codes);
        bool every = codes.Length == ModLanguages.All.Length;
        for (int l = 0; every && l < codes.Length; l++)
        {
            int at2 = Array.IndexOf(ModLanguages.All, codes[l]);
            every &= at2 >= 0 && WhatsNewHas(langs[l], pieces[at2]);
        }
        Check(every, "family chores: What's new has the chores' line in every language");
    }
}
