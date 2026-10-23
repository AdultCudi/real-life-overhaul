using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using RealLifeOverhaul;
using RealLifeOverhaul.Law;

// Crime's money and words (Scripts/Crime/CrimeRules.cs, Law/ReleaseRules.cs, Justice/JusticeLaw.cs): a hold-up in the
// player's own shop empties its till, a share of the shop's day booked at the shop as theft, never a share of all the
// player's money; the reward for a citizen's arrest and a passer-by's tip at today's prices; a release from a cell in a
// Going Public session (no time passed) names no hours held; and a charge the player cannot refuse is always taken, so
// a notice never says a fee was paid that was not (no Crime.Pay whose answer is dropped).
internal static partial class Program
{
    private static void CrimeFixes()
    {
        CrimeTill();
        CrimeThanks();
        CrimeReleaseWords();
        CrimeChargesTaken();
    }

    private static void CrimeTill()
    {
        const int start = PriceRules.Start;
        Check(CrimeRules.TillTake(1000f, 50000f, start) == 250, "crime till: a quarter of a day of 1,000 is taken (" + CrimeRules.TillTake(1000f, 50000f, start) + ", wanted 250)");
        Check(CrimeRules.TillTake(0f, 50000f, start) == CrimeRules.HoldUpLeast, "crime till: a shop with no takings yet still has its float in the till (" + CrimeRules.TillTake(0f, 50000f, start) + ")");
        Check(CrimeRules.TillTake(-50f, 50000f, start) == CrimeRules.HoldUpLeast, "crime till: a day's figure below nothing is read as nothing");
        Check(CrimeRules.TillTake(100000f, 50000f, start) == CrimeRules.HoldUpMost, "crime till: one grab carries no more than the cap (" + CrimeRules.TillTake(100000f, 50000f, start) + ")");
        Check(CrimeRules.TillTake(1000f, 120f, start) == 120 && CrimeRules.TillTake(1000f, 0f, start) == 0 && CrimeRules.TillTake(1000f, -300f, start) == 0,
            "crime till: never more than the money there is, and nothing from none (the game's one purse is never put in the red by a robbery)");
        Check(CrimeRules.TillTake(1000f, 1e9f, start) == CrimeRules.TillTake(1000f, 2e9f, start),
            "crime till: what is taken is the shop's day, whatever the player holds (it used to be a share of all the player's money)");
        Check(CrimeRules.TillTake(0f, 50000f, 2000) == 80 && CrimeRules.TillTake(100000f, 50000f, 2000) == 1000,
            "crime till: the floor and the cap move with the city's prices (" + CrimeRules.TillTake(0f, 50000f, 2000) + ", " + CrimeRules.TillTake(100000f, 50000f, 2000) + ")");
        Check(CrimeRules.TillTake(1234f, 50000f, start) % 5 == 0, "crime till: rounded to five as the game names prices");

        string crime = TextsSource("Crime/Crime.cs"), scene = TextsSource("Crime/Scenes/ShopIncidentScene.cs");
        string robTill = Between(crime, "public static int RobTill(", "/// <summary>");
        Check(Regex.IsMatch(robTill, @"CrimeRules\.TillTake\(BusinessGame\.DailyRevenue\(shop\), Cash, Prices\.Level\)")
              && Regex.IsMatch(robTill, @"ChangeMoneySafe\(-amount, new TransactionInfo\(""reallife_transaction_holdup_till""\), null, shop\.Address, force: true\)")
              && robTill.Contains("BusinessGame.BookTheft(shop, amount)"),
            "crime till: the till's money is booked at the shop's address under its own label and in the shop's day as theft");
        Check(scene.Contains("Crime.RobTill(ShopFloor.OwnRegistration)") && !scene.Contains("Crime.Rob("), "crime till: the hold-up in the player's own shop takes from the till, not a share of the cash in hand");
        Dictionary<string, string> en = LoadWords(System.IO.Path.Combine(LocalesFolder(), "en.json"));
        Check(en != null && en.TryGetValue("reallife_transaction_holdup_till", out string label) && label.Contains("Till")
              && en["reallife_crime_holdup_till_1"].Contains("till") && en["reallife_guide_crime"].Contains("empties the till") && !en["reallife_guide_crime"].Contains("Hold-up in your own shop: 4% of your cash"),
            "crime till: the transaction, the notice and the Guide say the till was robbed, not the player's pocket");
        Check(AnyChangesRow(en, "hold-up in your own shop"), "crime till: What's new says so");
    }

    private static void CrimeThanks()
    {
        const int start = PriceRules.Start;
        bool same = true;
        for (int k = 0; k <= 100; k++)
        {
            float roll = k / 100f;
            int old = (int)Math.Round((20f + 60f * roll) / 5f) * 5;   // what the reward was, at every price level
            same &= CrimeRules.Thanks(roll, CrimeRules.RewardLeast, CrimeRules.RewardMost, start) == old;
        }
        Check(same, "crime reward: at the start's prices the reward for a citizen's arrest is what it was, 20 to 80 in fives");
        Check(CrimeRules.Thanks(0f, CrimeRules.RewardLeast, CrimeRules.RewardMost, 2000) == 40 && CrimeRules.Thanks(1f, CrimeRules.RewardLeast, CrimeRules.RewardMost, 2000) == 160,
            "crime reward: at twice the prices it is twice the money (" + CrimeRules.Thanks(0f, CrimeRules.RewardLeast, CrimeRules.RewardMost, 2000) + " to "
            + CrimeRules.Thanks(1f, CrimeRules.RewardLeast, CrimeRules.RewardMost, 2000) + ", wanted 40 to 160)");
        Check(CrimeRules.Thanks(-1f, 20, 80, start) == 20 && CrimeRules.Thanks(9f, 20, 80, start) == 80, "crime reward: a roll outside 0 to 1 stays within the bounds");
        string arrest = TextsSource("Crime/CrimeScene.Arrest.cs"), law = TextsSource("Law/LawDirector.cs");
        Check(arrest.Contains("CrimeRules.Thanks(Random.value, CrimeRules.RewardLeast, CrimeRules.RewardMost, Prices.Level)") && !arrest.Contains("Random.Range(20f, 80f)"),
            "crime reward: the citizen's arrest pays at today's prices");
        Check(law.Contains("int tip = Prices.At(Random.Range(1, 5) * 5);"), "crime reward: a passer-by's tip to a well-liked player is at today's prices too");
    }

    private static void CrimeReleaseWords()
    {
        Check(CrimeRules.CustodyReleasedKey(false) == "reallife_crime_custody_released" && CrimeRules.CustodyReleasedKey(true) == "reallife_crime_custody_released_shared",
            "release words: custody with no precinct names its hours alone, and none in a Going Public session");
        Check(JusticeLaw.CustodyReleasedKey(false) == "reallife_justice_custody_released" && JusticeLaw.CustodyReleasedKey(true) == "reallife_justice_custody_released_shared",
            "release words: a court's custody names its hours alone, and none in a Going Public session");
        var keys = new List<string>(16);
        bool right = true;
        foreach (bool court in new[] { false, true })
        {
            foreach (bool bailed in new[] { false, true })
            {
                foreach (bool desk in new[] { false, true })
                {
                    foreach (bool shared in new[] { false, true })
                    {
                        string key = ReleaseRules.Key(court, bailed, desk, shared);
                        keys.Add(key);
                        right &= key.Contains("_court_") == court && key.EndsWith("_bail", StringComparison.Ordinal) == bailed && key.Contains("_desk") == desk
                                 && key.EndsWith("_shared", StringComparison.Ordinal) == (shared && !bailed);
                    }
                }
            }
        }
        Check(right, "release words: a precinct's line is the court's or the station's, at the desk or the door, with bail or without, and served at once in a session only without bail");
        keys.Add(CrimeRules.CustodyReleasedKey(true));
        keys.Add(CrimeRules.CustodyReleasedKey(false));
        keys.Add(JusticeLaw.CustodyReleasedKey(true));
        keys.Add(JusticeLaw.CustodyReleasedKey(false));
        foreach (string language in new[] { "en", "pt", "zh-cn" })
        {
            Dictionary<string, string> words = LoadWords(System.IO.Path.Combine(LocalesFolder(), language + ".json"));
            var wrong = new List<string>();
            foreach (string key in keys)
            {
                if (words == null || !words.TryGetValue(key, out string text) || key.EndsWith("_shared", StringComparison.Ordinal) && text.Contains("{hours}")
                    || language == "en" && text.Length > 240)
                {
                    wrong.Add(key);
                }
            }
            Check(wrong.Count == 0, "release words (" + language + "): every release line is there, those of a Going Public session name no {hours}, within 240 characters"
                                    + (wrong.Count > 0 ? " (" + string.Join(", ", wrong) + ")" : string.Empty));
        }
        string custody = TextsSource("Crime/PoliceAttention.Custody.cs"), cell = TextsSource("Law/Custody.Cell.cs"), prison = TextsSource("Justice/PrisonStay.cs");
        Check(custody.Contains("CrimeRules.CustodyReleasedKey(shared)") && cell.Contains("ReleaseRules.Key(court, _bailed, false, ServedAtOnce)")
              && cell.Contains("ReleaseRules.Key(court, _bailed, true, ServedAtOnce)") && prison.Contains("JusticeLaw.CustodyReleasedKey(GoingPublic.ClockShared)"),
            "release words: every release from a cell picks its line by whether the session served it at once");
        Check(AnyChangesRow(LoadWords(System.IO.Path.Combine(LocalesFolder(), "en.json")), "no longer names hours held"), "release words: What's new says so");
    }

    // A Crime.Pay whose answer is dropped charges nothing to a player who cannot pay, and whatever follows says it was paid.
    private static void CrimeChargesTaken()
    {
        string scripts = System.IO.Path.GetFullPath(System.IO.Path.Combine(LocalesFolder(), "..", "Scripts"));
        var dropped = new List<string>();
        int calls = 0;
        foreach (string path in System.IO.Directory.GetFiles(scripts, "*.cs", System.IO.SearchOption.AllDirectories))
        {
            string[] lines = System.IO.File.ReadAllLines(path);
            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i].Contains("Crime.Pay("))
                {
                    calls++;
                    if (Regex.IsMatch(lines[i], @"^\s*Crime\.Pay\("))
                    {
                        dropped.Add(System.IO.Path.GetFileName(path) + ":" + (i + 1));
                    }
                }
            }
        }
        Check(calls >= 8 && dropped.Count == 0, "charges taken: every Crime.Pay's answer is read (" + calls + " calls" + (dropped.Count > 0 ? "; dropped at " + string.Join(", ", dropped) : string.Empty) + ")");
        string making = TextsSource("Garage/TwoWheelers.Making.cs");
        Check(making.Contains("Crime.Charge(BikeCatalogue.LateFee, \"reallife_transaction_bike\")"), "charges taken: a hired bicycle's late fee is charged in full, as the notice says, as a tow is");
        string crime = TextsSource("Crime/Crime.cs");
        Check(Regex.IsMatch(Between(crime, "public static void Charge(", "/// <summary>"), @"ChangeMoneySafe\(-amount, new TransactionInfo\(transactionKey\), null, null, force: true\)"),
            "charges taken: a charge that cannot be refused is taken even into the red, as the game allows");
        Check(AnyChangesRow(LoadWords(System.IO.Path.Combine(LocalesFolder(), "en.json")), "late fee for a hired bicycle"), "charges taken: What's new says so");
    }
}
