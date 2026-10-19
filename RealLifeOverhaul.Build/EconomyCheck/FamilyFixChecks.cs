using System;
using System.IO;
using System.Text.RegularExpressions;
using IoPath = System.IO.Path;
using RealLifeOverhaul.Family;

// Faults found in Scripts/Family: a passage refused (another under way, the director off) after its money or errand was
// taken left nothing to show for it; the player's own missed scan or class was said to be rebooked and was not; and the
// counselling threshold was a bare number beside a dead constant that said another. All of it is the game's side, so it is
// checked in its source.
internal static partial class Program
{
    private static void FamilyFixes()
    {
        FamilyPassages();
        FamilyRebooked();
        FamilyCounselLine();
        FamilyPartnerTexts();
        FamilyPartnerJob();
    }

    // Social's partner had a trade of Family's own, rolled from their seed, whatever People said they did: a barista in the
    // Family talk who was a cab driver in People. Their job is People's now, with the hours and pay of the nearest trade.
    private static void FamilyPartnerJob()
    {
        bool all = true;
        string social = MemberText(ModSource(@"Social\SocialCatalogue.cs"), "public static readonly string[] Jobs =");
        var jobs = System.Text.RegularExpressions.Regex.Matches(social, "\"(reallife_social_job_[a-z]+)\"");
        all &= jobs.Count > 10;
        foreach (System.Text.RegularExpressions.Match m in jobs)
        {
            string key = m.Groups[1].Value;
            all &= RealLifeOverhaul.Family.PartnerJobRules.Retired(key) || (RealLifeOverhaul.Family.PartnerJobRules.TradeOf(key, -1) >= 0 && PartnerJobRules.IncomeOf(key, -1) > 0);
        }
        Check(all && PartnerJobRules.IncomeOf(PartnerJobRules.RetiredKey, 200) == 0 && PartnerJobRules.TradeOf("reallife_social_job_nurse", -1) == 0
              && PartnerJobRules.TradeOf("reallife_social_job_cook", -1) == 2 && PartnerJobRules.TradeOf("no such job", 7) == 7,
            "family fix: every job in People has Family's hours and a day's pay, a nurse keeps a nurse's shifts, a cook a chef's evenings, and the retired bring in nothing");
        string partner = ModSource(@"Family\Partner.cs"), home = ModSource(@"Family\HomeLife.cs"), links = ModSource(@"Wiring\FamilyLinks.cs");
        Check(partner.Contains("s.PJobKey = p.JobKey;") && partner.Contains("public static int Income => Retired ? 0") && home.Contains("works = works && !Partner.Retired;")
              && links.Contains("JobKey = SocialCatalogue.Jobs["),
            "family fix: Social's partner's job, hours and pay come from People, and a retired partner stays at home");
    }

    // With the social life keeping the partner Family has no stand-in contact: their texts were notices on screen. They come
    // from the partner's own contact in People now, and the stand-in is still the one used without the social life.
    private static void FamilyPartnerTexts()
    {
        string phone = ModSource(@"Family\FamilyPhone.cs"), links = ModSource(@"Wiring\FamilyLinks.cs");
        Check(phone.Contains("PartnerContact => _partnerContact ?? (Partner.Kept(out _) ? FamilyBridges.Social.PartnerContact : null)")
              && phone.Contains("_partnerContact = Register(PartnerId,") && links.Contains("SocialPhone.ContactOf(p)"),
            "family fix: the partner's texts come from their own contact in People while the social life keeps them, else from Family's stand-in");
    }

    private static void FamilyPassages()
    {
        string pass = MemberText(ModSource(@"Family\FamilyDirector.Sequences.cs"), "internal void Pass(string infoKey, int hours, Action during, Action after)");
        int run = pass.IndexOf("if (!Run(Passage(infoKey, hours, during, after)))", StringComparison.Ordinal);
        Check(run > 0 && pass.IndexOf("during?.Invoke();", run, StringComparison.Ordinal) > run && pass.IndexOf("after?.Invoke();", run, StringComparison.Ordinal) > run,
            "family fix: a refused passage does what it would have done at once, through FamilyDirector.Pass");
        string bare = null;
        int passes = 0;
        foreach (string path in Directory.GetFiles(IoPath.GetFullPath(IoPath.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\Assets\Mods\RealLifeOverhaul\Scripts\Family")), "*.cs"))
        {
            string code = File.ReadAllText(path);
            passes += Regex.Matches(code, @"\bdirector\.Pass\(").Count;
            // A passage run by hand must say what happens when it is refused: only the staged wedding does, with its scene's own state.
            foreach (Match m in Regex.Matches(code, @"(?<!if \(!)\b(director|_d)\.Run\((director|_d)\.Passage\("))
            {
                bare = (bare == null ? "" : bare + ", ") + IoPath.GetFileName(path);
            }
        }
        Check(bare == null, "family fix: no passage is run with its refusal ignored" + (bare != null ? " (" + bare + ")" : string.Empty));
        Check(passes == 14, "family fix: the fourteen passages after a payment, an errand or a booking go through Pass (" + passes + ")");
    }

    private static void FamilyRebooked()
    {
        string missed = MemberText(ModSource(@"Family\Children.Arrival.cs"), "public static void AppointmentMissed(Errand errand)");
        int own = missed.IndexOf("if (s.PCarrier == 1)", StringComparison.Ordinal);
        int added = missed.IndexOf("Errands.Add(errand.Kind, TimeHelper.CurrentDay + 1, errand.FromHour, errand.ToHour).Number = errand.Number;", StringComparison.Ordinal);
        int told = missed.IndexOf("\"reallife_family_appointment_missed_me\"", StringComparison.Ordinal);
        Check(own > 0 && added > own && told > added, "family fix: the player's own missed scan or class is booked again for tomorrow before the notice says it was");
        Check(FixLineEverywhere("the clinic now books it again for the next day", "a clínica agora remarca", "改约到第二天", "bucht die Klinik ihn jetzt",
                                "la clinique le reprogramme", "la clínica ahora la vuelve a reservar", "клиника теперь переносит",
                                "plant de kliniek dit nu opnieuw in voor de volgende dag", "klinik artık mesajında belirttiği gibi bunu ertesi gün için tekrar planlıyor"),
            "family fix: What's new has the rebooking's line, with its fix mark, in some row of every language");
        Check(FixLineEverywhere("is no longer lost when another scene is under way", "não se perde mais", "不会再白白丢失", "geht nicht mehr verloren",
                                "n'est plus perdue", "ya no se pierde", "больше не пропадает",
                                "gaat niet meer verloren als er een andere scène bezig is", "başka bir sahne devam ederken artık kaybolmuyor"),
            "family fix: What's new has the passages' line, with its fix mark, in some row of every language");
    }

    private static void FamilyCounselLine()
    {
        string balance = ModSource(@"Family\FamilyBalance.cs");
        string hard = ModSource(@"Family\HardTimes.cs");
        string can = MemberText(hard, "public static bool CanCounsel");
        Check(balance.Contains("public const float HarmonyCounsel = 60f;") && can.Contains("FamilyState.S.Harmony < FamilyBalance.HarmonyCounsel") && !Regex.IsMatch(can, @"\d+f\b"),
            "family fix: counselling is offered below one named threshold, 60, with no bare number in CanCounsel");
        // The texts promise it so: "Things are not easy" is under "happy" (FamilyText, 60), and the in-laws suggest a counsellor under 45.
        Match word = Regex.Match(ModSource(@"Family\FamilyText.cs"), @"harmony >= (\d+)f \? ""reallife_family_harmony_good""");
        Match inLaws = Regex.Match(ModSource(@"Family\Relatives.cs"), @"s\.Harmony < (\d+)f \? ""reallife_family_inlaws_advice_tense""");
        Check(word.Success && word.Groups[1].Value == "60" && inLaws.Success && int.Parse(inLaws.Groups[1].Value) <= 60,
            "family fix: counselling is on offer wherever the texts say things are tense and the in-laws suggest a counsellor");
    }
}
