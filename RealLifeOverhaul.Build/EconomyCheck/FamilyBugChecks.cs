using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using IoPath = System.IO.Path;
using RealLifeOverhaul;
using RealLifeOverhaul.Networking;

// Faults the Guide audit found in family and social life (26 Oct 2026): the Family contact was left out on a new save when the
// partner lived in People and home life was on, so a walk with the pushchair, a proposal outdoors and a wedding in the open
// air (all asked for out of doors) had no way in; People's "Ask them to move in" marked the partner moved in without family
// life moving them; a wedding moved the couple in with no room check; some family and social money ignored the price level;
// and a position in the networking life stayed with a slot of the cast after the player lost touch with who held it.
internal static partial class Program
{
    private static void FamilyBugs()
    {
        FamilyBugContact();
        FamilyBugMoveIn();
        FamilyBugPrices();
        FamilyBugNetwork();
        FamilyBugTexts();
    }

    // Always made, so that everything asked for out of doors can be asked by phone, whatever else is on.
    private static void FamilyBugContact()
    {
        string phone = ModSource(@"Family\FamilyPhone.cs"), setup = MemberText(phone, "public static void Setup()");
        string choices = ModSource(@"Family\FamilyPhone.Choices.cs"), courtship = ModSource(@"Family\Courtship.cs"), planner = ModSource(@"Family\WeddingPlanner.cs");
        Check(!setup.Contains("Hides") && !phone.Contains("Hides") && setup.Contains("FamilyContact = Register(FamilyId,")
              && !MemberText(phone, "public static bool BoardShown").Contains("Hides") && !MemberText(phone, "public static bool HasCallMatters").Contains("Hides"),
            "family bugs: the Family contact is made on every load while Family life is on, and neither the fridge's button nor the People call depends on a rule that left it out");
        // What is asked outdoors: the walk, the proposal away from home and the wedding in the open. The conversation that asks
        // for them refuses indoors, so only a phone call (anywhere) reaches them.
        Check(choices.Contains("StageWhenFree(true, () => new WalkScene(false));") && choices.Contains("TestRefusal.Indoors ? \"reallife_family_no_walk_indoors\"")
              && courtship.Contains("refusal = \"reallife_family_no_propose_indoors\";") && planner.Contains("!BuildingManager.IsInsideBuilding && !PlayerHelper.IsUsingVehicle"),
            "family bugs: the walk, the proposal and the open-air wedding still need the player outdoors, which the Family contact's call is the way to");
        Check(MemberText(phone, "private static Contact Register(string id,").Contains("contact.messagesQueue == null || contact.messagesQueue.Count == 0"),
            "family bugs: a Family contact the save already has keeps its thread (the welcome goes into an empty one only)");
    }

    // Moving in goes through family life's own move in (room, the home, the rent share), and a wedding asks for the room too.
    private static void FamilyBugMoveIn()
    {
        string person = ModSource(@"Social\PersonDialog.cs"), move = MemberText(person, "private DialogEntry MoveIn()");
        string links = ModSource(@"Wiring\FamilyLinks.cs"), home = ModSource(@"Family\HomeLife.cs"), phone = ModSource(@"Family\FamilyPhone.cs");
        Check(move.Contains("SocialPhone.FamilyKeepsMoveIn(_p)") && move.Contains("SocialPhone.FamilyMoveIn()") && move.Contains("return Close(why);")
              && links.Contains("SocialPhone.FamilyMoveIn = MoveIn;") && links.Contains("SocialPhone.FamilyKeepsMoveIn = null;")
              && phone.Contains("return HomeLife.MoveIn(false);") && phone.Contains("Partner.Kept(out _)"),
            "family bugs: People's 'Ask them to move in' moves the partner in through family life (HomeLife.MoveIn), and says why if it cannot");
        string moveIn = MemberText(home, "public static string MoveIn(bool test)");
        Check(moveIn.Contains("!FamilyPlaces.HasRoom(home, 0)") && moveIn.Contains("Partner.Tell(FamilyEvent.MovedIn, 5f, 5f);"),
            "family bugs: family life's move in checks the room, and tells People, which marks the partner as moved in and writes the memory");
        string wedding = MemberText(ModSource(@"Family\WeddingDay.cs"), "public static void Complete(bool staged, bool free = false)");
        int room = wedding.IndexOf("FamilyPlaces.HasRoom(home, 0)", StringComparison.Ordinal), lives = wedding.IndexOf("s.LivesIn = true;", StringComparison.Ordinal);
        Check(room > 0 && lives > room && wedding.Contains("reallife_family_married_no_room"),
            "family bugs: a wedding moves the couple in only if the home has room, and the story says so when it has not");
    }

    // Money that follows the price level like the rest: amounts written at the start's prices go through Prices.At.
    private static void FamilyBugPrices()
    {
        string balance = ModSource(@"Family\FamilyBalance.cs"), catalogue = ModSource(@"Social\SocialCatalogue.cs");
        Check(balance.Contains("public static int CateringPerGuest => Prices.At(85);") && balance.Contains("public static int PocketMoney(int level) => Prices.At(PocketBase[")
              && !balance.Contains("public const int CateringPerGuest"),
            "family bugs: a wedding's catering and a child's pocket money follow the price level");
        var literal = new List<string>();
        foreach (string file in new[] { @"Family\Children.Arrival.cs", @"Family\Children.Growing.cs", @"Family\Children.cs" })
        {
            foreach (Match m in Regex.Matches(ModSource(file), @"MomGift\(\s*\d"))
            {
                literal.Add(file + ": " + m.Value);
            }
        }
        Check(literal.Count == 0, "family bugs: Mom's gifts are written through Prices.At (" + string.Join(", ", literal) + ")");
        Check(catalogue.Contains("public int Cost => Prices.At(BaseCost);") && !Regex.IsMatch(catalogue, @"(?m)^\s+Cost = \d"),
            "family bugs: what an outing costs a head follows the price level, in every outing");
        string dialog = ModSource(@"Social\PersonDialog.cs"), after = ModSource(@"Social\OutingScene.After.cs");
        Check(MemberText(dialog, "private int BorrowAmount()").Contains("Prices.At(amount)") && after.Contains("gift = Prices.At(_kind == PlanKind.Lift"),
            "family bugs: what a friend lends and the thanks for a lift or a move follow the price level");
        // The rounding is the game's own for prices: 6 at the start is 6, at double it is 12.
        Check(PriceRules.At(24, PriceRules.Start) == 24 && PriceRules.At(24, 2 * PriceRules.Start) == 48 && PriceRules.At(0, 3 * PriceRules.Start) == 0 && PriceRules.At(10, 2 * PriceRules.Start) == 20,
            "family bugs: the price level scales an outing's cost and leaves a free one free");
    }

    // The position a person held goes when the player loses touch with them; whoever takes the slot is rolled afresh.
    private static void FamilyBugNetwork()
    {
        var file = new NetworkFile();
        file.Contacts.Add(new NetContact { Person = 4, Role = NetRole.Lawyer, Given = NetContact.Bit(NetFavour.Terms), Org = "Hale and Cross" });
        file.Contacts.Add(new NetContact { Person = 9, Role = NetRole.Buyer });
        Check(file.Of(4) != null && file.Forget(4) && file.Of(4) == null && file.Of(9) != null && file.Contacts.Count == 1 && !file.Forget(4) && !file.Forget(77),
            "family bugs: losing touch lets go of the person's position and favours, and of nobody else's");
        Check(NetworkRules.SeedOf(1234, 0) == 1234 && NetworkRules.SeedOf(1234, 98765) != NetworkRules.SeedOf(1234, 98766) && NetworkRules.SeedOf(1234, 98765) == NetworkRules.SeedOf(1234, 98765),
            "family bugs: a person is rolled for a position with their own seed too, the same on every machine, so a replacement does not draw their predecessor's");
        // Rolling is deterministic from the seed: across a hundred people the salted roll differs from the unsalted one somewhere.
        int differ = 0;
        bool again = true;
        for (int i = 0; i < 100; i++)
        {
            NetRole a = NetworkRules.RoleOf(777, i, 5, NetVia.Met), b = NetworkRules.RoleOf(NetworkRules.SeedOf(777, 1000 + i * 31), i, 5, NetVia.Met);
            differ += a != b ? 1 : 0;
            again &= NetworkRules.RoleOf(NetworkRules.SeedOf(777, 1000 + i * 31), i, 5, NetVia.Met) == b;
        }
        Check(differ > 0 && again, "family bugs: the person's own seed changes who holds a position, and the same person draws the same again");
        string director = ModSource(@"Networking\NetworkDirector.cs"), lose = ModSource(@"Social\SocialDirector.Lose.cs"), links = ModSource(@"Wiring\Wiring.NetworkLinks.cs");
        Check(lose.Contains("SocialLinks.NetworkForget?.Invoke(index);") && links.Contains("SocialLinks.NetworkForget = NetworkDirector.Forget;") && links.Contains("SocialLinks.NetworkForget = null;")
              && MemberText(director, "public static void Forget(int person)").Contains("f.Forget(person)") && MemberText(director, "public static void Forget(int person)").Contains("Write();"),
            "family bugs: losing touch tells the networking life before somebody new takes the slot, and the save's line is written");
    }

    // The new texts are in en, pt and zh-cn, short enough; the Guide page is untranslated (read in English) in the other six;
    // every language's next row has a fix line for each of these.
    private static void FamilyBugTexts()
    {
        string folder = LocalesFolder();
        string[] keys = { "reallife_family_married_no_room", "reallife_social_movein_room", "reallife_social_movein_home" };
        var bad = new List<string>();
        foreach (string code in new[] { "en", "pt", "zh-cn" })
        {
            Dictionary<string, string> words = LoadWords(IoPath.Combine(folder, code + ".json"));
            foreach (string key in keys)
            {
                if (words == null || !words.TryGetValue(key, out string text) || text.Length == 0 || text.Length > 240 || text.Contains("!"))
                {
                    bad.Add(code + ":" + key);
                }
            }
            if (words != null && words.TryGetValue("reallife_guide_family", out string guide) && !guide.Contains("{key_interact}"))
            {
                bad.Add(code + ":guide");
            }
        }
        Check(bad.Count == 0, "family bugs: the new texts are in en, pt and zh-cn, under 240 characters, with no exclamation mark (" + string.Join(", ", bad) + ")");
        Dictionary<string, string> en = LoadWords(IoPath.Combine(folder, "en.json"));
        string page = en != null && en.TryGetValue("reallife_guide_family", out string p) ? p : string.Empty;
        Check(page.Contains("which is made on every save while Family life is on") && !page.Contains("a new save has no <b>Family</b> contact") && !page.Contains("With any of those three options off"),
            "family bugs: the Guide's family page says the Family contact is always made");
        var untranslated = new List<string>();
        var marks = new Dictionary<string, string>
        {
            { "en", "Fixed: " }, { "pt", "Corrigido: " }, { "zh-cn", "已修复：" }, { "de", "Behoben: " }, { "fr", "Corrigé : " }, { "es", "Corregido: " },
            { "ru", "Исправлено: " }, { "nl", "Opgelost: " }, { "tr", "Düzeltildi: " }
        };
        var lacking = new List<string>();
        foreach (KeyValuePair<string, string> mark in marks)
        {
            Dictionary<string, string> words = LoadWords(IoPath.Combine(folder, mark.Key + ".json"));
            if (words == null)
            {
                lacking.Add(mark.Key);
                continue;
            }
            if (Array.IndexOf(new[] { "en", "pt", "zh-cn" }, mark.Key) < 0 && words.ContainsKey("reallife_guide_family"))
            {
                untranslated.Add(mark.Key);
            }
            string next = words.TryGetValue("reallife_changes_next", out string n) ? n : string.Empty;
            if (Regex.Matches(next, Regex.Escape(mark.Value)).Count < 5)
            {
                lacking.Add(mark.Key);
            }
        }
        Check(untranslated.Count == 0, "family bugs: the changed Guide page is out of de, fr, es, ru, nl and tr until it is translated again (" + string.Join(", ", untranslated) + ")");
        Check(lacking.Count == 0, "family bugs: What's new's next row has its five fixes in all nine languages (" + string.Join(", ", lacking) + ")");
    }
}
