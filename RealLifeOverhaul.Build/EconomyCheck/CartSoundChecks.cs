using System;
using System.Collections.Generic;
using RealLifeOverhaul;

// A player kept missing drinks cart orders: nothing told them a customer had reached the counter, and the order had about
// ten seconds. The cart is now heard with the game's own sounds (Scripts/Jobs/Kinds/VendorJob.Sounds.cs, CartSoundRules.cs):
// the customer's voice as their order starts, once more for an untouched order near its end, and the till when a drink is
// paid for; "Drinks cart sounds" (job_cart_sounds, on by default) switches them off. First the rules, then the gig calling
// them at the right places, then the option and the texts.
internal static partial class Program
{
    private static void CartSound()
    {
        CartSoundRulesHeld();
        CartSoundSources();
        CartSoundWords();
    }

    private static void CartSoundRulesHeld()
    {
        Check(CartSoundRules.Audible(true, 1f, false, false), "cart sound: with the option on and the game running, the cart is heard");
        Check(!CartSoundRules.Audible(false, 1f, false, false), "cart sound: with the option off the cart is silent, as before");
        Check(!CartSoundRules.Audible(true, 0f, false, false), "cart sound: nothing while the game is paused");
        Check(!CartSoundRules.Audible(true, 1f, true, false), "cart sound: nothing while the clock works the shift");
        Check(!CartSoundRules.Audible(true, 1f, false, true), "cart sound: nothing once the shift is ending");
        Check(CartSoundRules.Audible(true, 3f, false, false), "cart sound: the game's faster speeds are still heard (only the cart's own clock run is silent)");

        // VendorJob is not linked here: its figures are read from its source and held to these.
        const float p = 11f, wrongCost = 3f;
        string vendor = ModSource(@"Jobs\Kinds\VendorJob.cs");
        Check(vendor.Contains("public const float Patience = 11f;") && vendor.Contains("public const float WrongCost = 3f;"),
            "cart sound: the gig's patience and a wrong drink's cost are the " + p + " s and " + wrongCost + " s these checks hold the reminder to");
        float at = p * (1f - CartSoundRules.RemindShare);
        Check(!CartSoundRules.Remind(true, false, at - 0.05f, 0f, p, p), "cart sound: no reminder before a third of the patience is left");
        Check(CartSoundRules.Remind(true, false, at, 0f, p, p), "cart sound: the reminder at a third of the patience left (" + at.ToString("F1") + " s into " + p.ToString("F0") + ")");
        Check(!CartSoundRules.Remind(false, false, at + 1f, 0f, p, p), "cart sound: one reminder an order, never a second");
        Check(!CartSoundRules.Remind(true, true, at + 1f, 0f, p, p), "cart sound: no reminder once a drink was tried: the player heard the order");
        Check(!CartSoundRules.Remind(true, false, p, 0f, p, p), "cart sound: no reminder once the customer has walked off");
        Check(!CartSoundRules.Remind(true, false, at, 0f, p, at), "cart sound: no reminder past an order's end brought forward");
        Check(CartSoundRules.RemindShare > 0.2f && CartSoundRules.RemindShare < 0.5f && p * CartSoundRules.RemindShare >= wrongCost,
            "cart sound: the reminder leaves time to serve (" + (p * CartSoundRules.RemindShare).ToString("F1") + " s, at least a wrong drink's " + wrongCost + " s)");

        bool seeded = CartSoundRules.FemaleBySeed(7) && !CartSoundRules.FemaleBySeed(8) && CartSoundRules.FemaleBySeed(7) == CartSoundRules.FemaleBySeed(7);
        Check(seeded, "cart sound: a voice that cannot be read from the look is picked by the seeded lease, the same every time");
        Check(CartSoundRules.AllPlayed(0, 0) && CartSoundRules.AllPlayed(5, 5) && !CartSoundRules.AllPlayed(5, 4), "cart sound: the shift's check passes only when every cue wanted was played");

        var watch = System.Diagnostics.Stopwatch.StartNew();
        int due = 0;
        for (int i = 0; i < 200000; i++)
        {
            due += CartSoundRules.Remind(true, (i & 7) == 0, (i % 110) * 0.1f, 0f, p, p) ? 1 : 0;
        }
        watch.Stop();
        Check(due > 0 && watch.Elapsed.TotalMilliseconds < 50.0, "cart sound: 200,000 looks at an order's reminder take " + watch.Elapsed.TotalMilliseconds.ToString("F1") + " ms (under 50)");
    }

    private static void CartSoundSources()
    {
        string customers = ModSource(@"Jobs\Kinds\VendorJob.Customers.cs");
        string serving = ModSource(@"Jobs\Kinds\VendorJob.Serving.cs");
        string sounds = ModSource(@"Jobs\Kinds\VendorJob.Sounds.cs");
        string job = ModSource(@"Jobs\Kinds\VendorJob.cs");
        string attend = MemberText(customers, "private void Attend(int i, float now)");
        int start = attend.IndexOf("_orderEnds = now + Patience;", StringComparison.Ordinal);
        Check(start > 0 && attend.IndexOf("OrderHeard(person);", StringComparison.Ordinal) > start && Times(attend, "OrderHeard(") == 1 && Times(customers, "= Guest.Ordering;") == 1,
            "cart sound: the order cue is played at the one place a customer becomes Ordering, after the order's clock starts");
        Check(attend.Contains("else if (now < _orderEnds)") && attend.Contains("OrderWaiting(person, now);"),
            "cart sound: an order still waiting is looked at for its one reminder, never one already walked off");
        string choose = MemberText(serving, "public override void Choose(int choice, float now)");
        int tried = choose.IndexOf("_tried = true;", StringComparison.Ordinal);
        int wrong = choose.IndexOf("if (choice != _wants || sendBack)", StringComparison.Ordinal);
        int till = choose.IndexOf("TillHeard();", StringComparison.Ordinal);
        Check(tried > 0 && tried < wrong && till > wrong && Times(choose, "TillHeard();") == 1,
            "cart sound: any drink tried stops the reminder, and only a drink paid for rings the till");
        Check(MemberText(job, "public override void End(JobEnd why)").Contains("SoundCheck();"), "cart sound: every shift's end writes the sound check");
        Check(sounds.Contains("SoundType.NpcFemaleProducerInteraction : SoundType.NpcMaleProducerInteraction, person.Position)")
              && sounds.Contains("SoundType.PurchaseSuccess, _cartAt + Vector3.up * CounterHeight, 1f, isPlayerCreatedSound: true)")
              && sounds.Contains("look.data.gender == Gender.Female") && sounds.Contains("CartSoundRules.FemaleBySeed(person.Lease)"),
            "cart sound: the game's own customer voice for their gender at the customer, and its till at the cart, as its counters play them");
        Check(sounds.Contains("if (sounds != _soundsSeen)") && sounds.Contains("audioClip == null") && sounds.Contains("if (_soundSaid)"),
            "cart sound: the game's presets are looked at once for each sound manager, an empty one is never played, and a missing one is said once a shift");
        Check(!sounds.Contains("Random.") && !sounds.Contains("FindObject") && !sounds.Contains("void Update"), "cart sound: nothing random, no search and nothing per frame");
        string settings = ModSource(@"Core\Settings.cs");
        string mod = ModSource(@"Core\RealLifeOverhaulMod.cs");
        string tree = ModSource(@"Meta\OptionsTree.cs");
        string list = ModSource(@"Meta\FeatureList.cs");
        Check(settings.Contains("JobCartSoundsId = \"job_cart_sounds\"") && settings.Contains("public static bool JobCartSounds = true;")
              && settings.Contains("JobCartSounds = Bool(modId, JobCartSoundsId, JobCartSounds);")
              && mod.Contains("AddToggle(Settings.JobCartSoundsId, \"reallife_jobs_cart_sounds\", Settings.JobCartSounds,")
              && tree.Contains("\"job_cart_sounds\")") && list.Contains("Settings.JobCartSoundsId)")
              && sounds.Contains("CartSoundRules.Audible(Settings.JobCartSounds,"),
            "cart sound: the option is saved, on by default, on the jobs' section of the page, owned by the jobs' Guide page, and read before every sound");
    }

    private static void CartSoundWords()
    {
        Dictionary<string, string>[] langs = LanguageWords(out string[] codes);
        if (!LanguagesRead("cart sound", langs, codes))
        {
            return;
        }
        string lacking = string.Empty;
        foreach (string key in new[] { "reallife_jobs_cart_sounds", "reallife_opt_tip_job_cart_sounds" })
        {
            string l = LanguagesLacking(langs, codes, key);
            lacking += l.Length > 0 ? " " + key + " (" + l + ")" : string.Empty;
        }
        Check(lacking.Length == 0, "cart sound: every text is in every language:" + lacking);
        Check(langs[0].TryGetValue("reallife_jobs_cart_sounds", out string label) && label == "Drinks cart sounds"
              && langs[0].TryGetValue("reallife_opt_tip_job_cart_sounds", out string tip) && tip.Contains("reach the counter") && tip.Contains("the till") && tip.Contains("Off, the cart is silent"),
            "cart sound: the option is named as the roadmap has it and its sentence says what is heard and what off does");
        Check(langs[0].TryGetValue("reallife_guide_jobs", out string guide) && guide.Contains("You hear each customer as they reach the counter") && guide.Contains("unless Drinks cart sounds is off"),
            "cart sound: the jobs' Guide page says a customer at the counter is heard, and the option");
        string[] three = { "you now hear a customer reach the counter", "agora você ouve o cliente chegar ao balcão", "你会听到顾客走到柜台的声音" };
        bool news = true;
        for (int l = 0; l < 3 && l < langs.Length; l++)
        {
            news &= WhatsNewHas(langs[l], three[l]);
        }
        Check(news, "cart sound: What's new has the line in en, pt and zh-cn");
    }

    // How often a piece of code stands in another.
    private static int Times(string code, string piece)
    {
        int n = 0;
        for (int at = code.IndexOf(piece, StringComparison.Ordinal); at >= 0; at = code.IndexOf(piece, at + piece.Length, StringComparison.Ordinal))
        {
            n++;
        }
        return n;
    }
}
