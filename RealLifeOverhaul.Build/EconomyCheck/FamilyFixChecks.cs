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
        FamilyPromptButtons();
        FamilyFaceToFace();
        FamilyOneDay();
        FamilyCarParked();
        FamilyContactKept();
        FamilyRetrainedJob();
        FamilyThreeTexts();
        FamilyBugs();
    }

    // People's night owl slept to ten while the family sent the partner to work at eight, and a call at nine went to
    // voicemail. The partner the social life keeps has People's day: its sleep and its working stretch by their traits and
    // free hours, shortened on half days and gone when they stop; People reads back from it when they are free.
    private static void FamilyOneDay()
    {
        PartnerJobRules.Work(17, 23, false, out int f0, out int t0);
        PartnerJobRules.Work(14, 22, false, out int f1, out int t1);
        PartnerJobRules.Work(11, 18, true, out int f2, out int t2);
        Check(f0 == 8 && t0 == 17 && f1 == 8 && t1 == 14 && f2 == 18 && t2 == 26 && PartnerJobRules.Within(1, 18, 26) && !PartnerJobRules.Within(3, 18, 26)
              && PartnerJobRules.Asleep(9, true) && !PartnerJobRules.Asleep(9, false) && PartnerJobRules.Asleep(1, false) && !PartnerJobRules.Asleep(1, true),
            "family fix: People's day for the partner: office hours 8 to 17, an afternoon shift 8 to 14, a night owl 18 to 2 and asleep 2 to 10, as People has them");
        string model = ModSource(@"Social\SocialModel.cs"), home = ModSource(@"Family\HomeLife.cs"), asleep = MemberText(model, "public bool Asleep(int hour)");
        bool same = asleep.Contains("hour >= 2 && hour < 10") && asleep.Contains("hour >= 23 || hour < 8");
        Check(same && MemberText(model, "public bool FreeAt(int hour, bool weekend)").Contains("FreeOf?.Invoke(this, hour, weekend)")
              && home.Contains("PartnerJobRules.Asleep(hour, s.PNightOwl)") && home.Contains("to = PartnerWorkRules.Until(from, to, PartnerWork.Work);")
              && ModSource(@"Wiring\FamilyLinks.cs").Contains("p == SocialState.Partner ? FamilyApi.PartnerAsleep") == false,
            "family fix: the family's day for People's partner sleeps and works by People's hours, and People asks the family when they are free");
    }

    // The Family contact is always made while Family life is on. With the social life keeping the partner there are more ways in
    // at home, and nothing is lost: talking at home has the whole list, the fridge's Family button the list without what needs
    // the partner there, the partner's People call the phone's part (counselling, time apart, the in-laws), and a new home is a
    // text whose button is never saved.
    private static void FamilyFaceToFace()
    {
        string phone = ModSource(@"Family\FamilyPhone.cs"), choices = ModSource(@"Family\FamilyPhone.Choices.cs"), sms = ModSource(@"Family\FamilySms.cs");
        string director = ModSource(@"Family\FamilyDirector.cs"), person = ModSource(@"Social\PersonDialog.cs"), fridge = ModSource(@"Home\HomeFridgeButton.cs");
        string links = ModSource(@"Wiring\FamilyLinks.cs");
        Check(!phone.Contains("Hides") && !director.Contains("Hides") && !fridge.Contains("Hides") && !links.Contains("Hides")
              && MemberText(phone, "public static void Setup()").Contains("FamilyContact = Register(FamilyId,"),
            "family face to face: the Family contact is made whatever the social life, home life and home routines are, so nothing out of doors depends on the fridge");
        string[] call = { "HardTimes.Book)))", "HardTimes.CallApart)))", "HardTimes.ComeHome)))", "HardTimes.EndAsk)))", "Relatives.GoToMoms)))", "Relatives.InLawFavour)))" };
        Check(Array.TrueForAll(call, choices.Contains) && Regex.Matches(choices, @"list\.Add\(Call\(").Count == call.Length + 1
              && choices.Contains("list.Add(Call(new Choice(() => FamilyState.Together && s().LivesIn && BuildingManager.IsInsideBuilding")
              && person.Contains("case \"family\":") && links.Contains("p == SocialState.Partner && FamilyPhone.HasCallMatters"),
            "family face to face: the partner's People call has Family matters: a new home where the player stands, counselling, time apart (call, come home, part) and the in-laws, and nothing else");
        Check(choices.Contains("HomeLife.Present && !AtBoard;") && fridge.Contains("FamilyShown != null && FamilyShown()") && links.Contains("HomeFridgeButton.FamilyClicked = FamilyPhone.Board;"),
            "family face to face: the fridge's Family button opens the family's page and list, without what needs the partner in the room");
        int save = director.IndexOf("FamilySms.BeforeSave();", StringComparison.Ordinal), write = director.IndexOf("FamilyState.Write();", StringComparison.Ordinal);
        Check(save > 0 && write > save && director.Contains("FamilySms.Tick();") && director.Contains("FamilySms.Reset();")
              && sms.Contains("_quietUntil = Time.unscaledTime + AfterSave;") && sms.Contains("buttons.RemoveAll(b => b.key == NewHomeKey);")
              && sms.Contains("!FamilyPlaces.AtHome() && FamilyPhone.InOwnHome()"),
            "family face to face: the new-home text button is on only in another home of the player's, taken off before every save and back after it");
    }

    // The panel's button is named after the prompt (_prompt_ to _button_): the ask's prompt had no button text, and the
    // button showed its bare key. Every family prompt in the source has its button's words in the three languages.
    private static void FamilyPromptButtons()
    {
        string mod = IoPath.GetFullPath(IoPath.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\Assets\Mods\RealLifeOverhaul"));
        string folder = IoPath.Combine(mod, "Locales");
        var langs = new[] { LoadWords(IoPath.Combine(folder, "en.json")), LoadWords(IoPath.Combine(folder, "pt.json")), LoadWords(IoPath.Combine(folder, "zh-cn.json")) };
        string bare = null;
        int prompts = 0;
        foreach (string path in Directory.GetFiles(IoPath.Combine(mod, "Scripts"), "*.cs", SearchOption.AllDirectories))
        {
            foreach (Match m in Regex.Matches(File.ReadAllText(path), "\"(reallife_family_prompt_[a-z_]+)\""))
            {
                prompts++;
                string button = m.Groups[1].Value.Replace("_prompt_", "_button_");
                if (Array.Exists(langs, w => w == null || !w.TryGetValue(button, out string words) || words.Length == 0))
                {
                    bare = button;
                }
            }
        }
        Check(prompts > 10 && bare == null, "family fix: every family prompt's button has its words in English, Portuguese and Chinese" + (bare != null ? " (" + bare + ")" : string.Empty));
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

    // With the partner's expectations switched off the car handed over stayed out of the city, still theirs, and the game
    // went on billing it parking every hour (it bills a legally parked car by its saved state, object or none); it was billed
    // with the switch on too. Theirs, it has no parking state; off, it is the player's again, in the city or at a repair bay.
    private static void FamilyCarParked()
    {
        string car = ModSource(@"Family\PartnerCar.cs");
        string hourly = MemberText(car, "public static void Hourly()"), release = MemberText(car, "private static void Release(VehicleInstance v, VehicleController controller)");
        int off = hourly.IndexOf("if (!Settings.FamilyExpect)", StringComparison.Ordinal), rel = hourly.IndexOf("Release(v, controller);", StringComparison.Ordinal);
        int hide = hourly.IndexOf("Economy.EstateGame.Hide(controller);", StringComparison.Ordinal);
        Check(off > 0 && rel > off && hide > rel && hourly.Contains("Off(v);")
              && MemberText(car, "private static void Off(VehicleInstance v)").Contains("v.parkingState = ParkingState.NotAvailable;")
              && MemberText(car, "private static void Back(VehicleInstance v)").Contains("v.parkingState = ParkingState.Legal;"),
            "family car: with the switch off the car is let go before anything hides it; while it is theirs it has no parking state, and back with the player it is parked");
        string give = MemberText(car, "private static DialogEntry Give(VehicleController car)"), buy = MemberText(car, "private static DialogEntry Buy(string type, int price)");
        string back = MemberText(car, "public static DialogEntry TakeBack()");
        int buyOff = buy.IndexOf("Off(v);", StringComparison.Ordinal);
        Check(give.IndexOf("Off(car.vehicleInstance);", StringComparison.Ordinal) > 0 && buyOff > 0 && buyOff < buy.IndexOf("save.VehicleInstances.Add(v);", StringComparison.Ordinal)
              && back.IndexOf("Back(v);", StringComparison.Ordinal) > back.IndexOf("s.PCarId = null;", StringComparison.Ordinal) && back.IndexOf("s.PCarId = null;", StringComparison.Ordinal) > 0,
            "family car: a car handed over or bought for them is billed no parking from that moment, and one asked back is parked again");
        int session = release.IndexOf("GoingPublic.InSession", StringComparison.Ordinal), park = release.IndexOf("at = Park(v);", StringComparison.Ordinal);
        int let = release.IndexOf("FamilyState.S.PCarId = null;", StringComparison.Ordinal);
        Check(session > 0 && park > session && let > park && release.Contains("if (controller == null)") && release.Contains("catch (Exception e)")
              && release.Contains("\"reallife_family_car_released\"") && release.Contains("Back(v);") && !release.Contains("Harmony(") && !release.Contains("Partner.Tell("),
            "family car: let go with the switch off, a car a load made stays where it is, one with no object is made at a repair bay with a notice (never in a Going Public session), "
            + "a failure leaves it theirs, and nothing is held against the player");
        var words = LoadWords(IoPath.Combine(LocalesFolder(), "en.json"));
        string notice = words != null && words.TryGetValue("reallife_family_car_released", out string n) ? n : string.Empty;
        Check(notice.Length > 0 && notice.Length <= 240 && notice.Contains("{place}") && notice.Contains("{name}") && notice.Contains("{text}"),
            "family car: the notice says where the car is, which car and whose it was, within 240 characters");
    }

    // With the social life on, the Family contact of an earlier session was taken off the save, and every message in its
    // thread with it. It is kept now, answering and getting the family's texts; only a new one is not made. Nothing in family
    // life takes a contact off the save (the uninstall step and a retired feature's clean-up are elsewhere).
    private static void FamilyContactKept()
    {
        string phone = ModSource(@"Family\FamilyPhone.cs"), setup = MemberText(phone, "public static void Setup()");
        string saved = MemberText(phone, "private static bool Saved(string id)");
        string register = MemberText(phone, "private static Contact Register(string id,");
        Check(!setup.Contains("Hides") && setup.Contains("FamilyContact = Register(FamilyId,") && setup.Contains("else if (Saved(PartnerId))")
              && saved.Contains("contacts[i].id == id") && !saved.Contains("Remove")
              && register.Contains("contact.messagesQueue == null || contact.messagesQueue.Count == 0"),
            "family contact: the Family contact is registered on every load, an earlier session's with its thread kept (the welcome is sent into an empty thread only)");
        string family = IoPath.GetFullPath(IoPath.Combine(LocalesFolder(), "..", "Scripts", "Family"));
        var removing = new System.Collections.Generic.List<string>();
        foreach (string file in Directory.GetFiles(family, "*.cs", SearchOption.AllDirectories))
        {
            if (Regex.IsMatch(File.ReadAllText(file), @"\.Contacts\s*\.\s*(RemoveAll|Remove|RemoveAt|Clear)\s*\("))
            {
                removing.Add(IoPath.GetFileName(file));
            }
        }
        Check(removing.Count == 0, "family contact: nothing in family life takes a contact off the save, and its messages with it (" + string.Join(", ", removing) + ")");
    }

    // After the partner Social keeps retrained for the job they always wanted, Family had its own trade for them while People
    // and everything said about them kept the old job. The dream is one of People's jobs now, and a yes is told to Social.
    private static void FamilyRetrainedJob()
    {
        string[] people = new string[RealLifeOverhaul.SocialCatalogue.Jobs.Length - 1];
        Array.Copy(RealLifeOverhaul.SocialCatalogue.Jobs, people, people.Length);
        bool rule = PartnerJobRules.Count == people.Length && PartnerJobRules.Retired(RealLifeOverhaul.SocialCatalogue.Jobs[RealLifeOverhaul.SocialCatalogue.Jobs.Length - 1])
                    && PartnerJobRules.DreamKey(5, PartnerJobRules.RetiredKey) == null && PartnerJobRules.DreamKey(5, "no such job") == null;
        for (int seed = 1; seed <= 3000 && rule; seed++)
        {
            int s = seed * 7919;
            foreach (string job in people)
            {
                string dream = PartnerJobRules.DreamKey(s, job);
                rule &= dream != null && dream != job && Array.IndexOf(people, dream) >= 0 && dream == PartnerJobRules.DreamKey(s, job)
                        && PartnerJobRules.TradeOf(dream, -1) >= 0 && PartnerJobRules.IncomeOf(dream, -1) > 0;
            }
        }
        Check(rule, "family retrain: the job a partner in People always wanted is one of People's jobs, never the one they have or retired, the same every time, with hours and pay");
        string wish = ModSource(@"Family\PartnerWish.cs"), links = ModSource(@"Wiring\FamilyLinks.cs"), bridge = ModSource(@"Family\FamilyBridges.cs");
        string talk = MemberText(wish, "public static DialogEntry Talk()"), retrained = MemberText(wish, "private static void Retrained(FamilySave s, string job)");
        string told = MemberText(links, "public bool PartnerJob(string jobKey)");
        Check(talk.Contains("string people = stop ? null : PeopleDream;") && talk.Contains("FamilyText.T(people ?? Partner.KeyOf(dream))") && talk.Contains("Retrained(s, people);")
              && wish.Contains("PartnerJobRules.DreamKey(FamilyState.S.PSeed, p.JobKey)")
              && retrained.Contains("s.PJobKey = job;") && retrained.Contains("FamilyBridges.Social.PartnerJob(job)") && bridge.Contains("bool PartnerJob(string jobKey);")
              && told.Contains("Array.IndexOf(SocialCatalogue.Jobs, jobKey)") && told.Contains("p.Job = job;") && told.Contains("catch (Exception e)"),
            "family retrain: the ask names People's job, and a yes makes it the partner's job in People as well as in family life");
    }

    // The three fixes' lines in What's new, in every language's own file, in some reallife_changes_ row (the next row until a
    // release numbers it), and the notice's key in the three languages every release carries.
    private static void FamilyThreeTexts()
    {
        var lines = new System.Collections.Generic.Dictionary<string, string[]>
        {
            { "en", new[] { "no longer billed for parking while it is theirs", "a Family contact you already have stays", "People shows the new job" } },
            { "pt", new[] { "não paga mais estacionamento enquanto é dele", "um contato Família que você já tem continua", "Pessoas mostra o novo trabalho" } },
            { "zh-cn", new[] { "不再收停车费", "你已有的“家庭”联系人", "“人脉”会显示新工作" } },
            { "de", new[] { "keine Parkgebühren mehr berechnet", "ein Familie-Kontakt, den du schon hast", "zeigt Leute den neuen Beruf" } },
            { "fr", new[] { "n'est plus facturée en stationnement", "un contact Famille que vous avez déjà", "Personnes affiche le nouveau métier" } },
            { "es", new[] { "ya no paga aparcamiento mientras es suyo", "un contacto Familia que ya tienes", "Gente muestra el nuevo trabajo" } },
            { "ru", new[] { "больше не берут плату за парковку", "контакт «Семья», который у вас уже есть", "«Люди» показывают новую работу" } },
            { "nl", new[] { "wordt geen parkeergeld meer gerekend", "een Familie-contact dat je al hebt", "toont Mensen de nieuwe baan" } },
            { "tr", new[] { "artık otopark ücreti yazılmıyor", "zaten sahip olduğunuz Aile kişisi", "Kişiler yeni işi gösteriyor" } }
        };
        var lacking = new System.Collections.Generic.List<string>();
        foreach (var language in lines)
        {
            var words = LoadWords(IoPath.Combine(LocalesFolder(), language.Key + ".json"));
            foreach (string part in language.Value)
            {
                if (!AnyChangesRow(words, part))
                {
                    lacking.Add(language.Key + ": " + part);
                }
            }
            if (Array.IndexOf(new[] { "en", "pt", "zh-cn" }, language.Key) >= 0 && (words == null || !words.TryGetValue("reallife_family_car_released", out string n) || !n.Contains("{place}")))
            {
                lacking.Add(language.Key + ": reallife_family_car_released");
            }
        }
        Check(lacking.Count == 0, "family fixes: What's new has the three fix lines in all nine languages, and the car's notice is in en, pt and zh-cn (" + string.Join("; ", lacking) + ")");
    }
}
