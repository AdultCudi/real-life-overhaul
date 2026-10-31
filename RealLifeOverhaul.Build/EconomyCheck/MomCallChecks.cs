using System;
using System.Collections.Generic;
using RealLifeOverhaul.Family;

// The family dinner call (the owner, 1 Nov 2026: Mom's Thanksgiving invitation gave no way to attend, and "a call from Alex"
// went away when it was opened). Two causes, each held here:
// - the social life's ring (Social/SocialDirector.Plan): its notice did nothing when clicked, its panel hid behind any open
//   screen while the ring ran on, and a call placed to the caller from the phone was not the answer, so opening the phone or
//   the notice let the call lapse unseen. Now the notice answers, the ring waits behind a screen, and calling them answers;
// - the dinner at Mom's was offered only inside the Family contact's menu, while her texts sent the player to ring and her own
//   call had no way over. Now one table holds the days, places and hours (Family/DinnerRules), Mom's texts say how to come,
//   her call (either dialog) and the Family call open on going over, and Options > Tests sets today as a dinner day.
internal static partial class Program
{
    private static void MomCall()
    {
        MomCallCalendar();
        MomCallTable();
        MomCallRing();
        MomCallDinnerWays();
        MomCallTexts();
    }

    // The days: the table gives what the old per-feature formula gave, the invitation the day before each dinner, four a year.
    private static void MomCallCalendar()
    {
        float[] at = { 0.22f, 0.47f, 0.72f, 0.97f };
        var wrong = new List<string>();
        foreach (int year in new[] { 60, 61, 90, 120, 365 })
        {
            for (int i = 0; i < DinnerRules.Count; i++)
            {
                int old = Math.Clamp((int)Math.Round(year * at[i]), 1, year - 1);   // Relatives' and FamilyPage's own formula before the table
                if (DinnerRules.DayOfYear(i, year) != old)
                {
                    wrong.Add(year + "/" + i + ": " + DinnerRules.DayOfYear(i, year) + " not " + old);
                }
            }
        }
        Check(wrong.Count == 0 && DinnerRules.DayOfYear(0, 60) == 13 && DinnerRules.DayOfYear(1, 60) == 28 && DinnerRules.DayOfYear(2, 60) == 43 && DinnerRules.DayOfYear(3, 60) == 58,
            "family dinners: each dinner falls on the day of the year the family's old formula gave (60, 61, 90, 120 and 365 days; a 60-day year 13, 28, 43, 58) (" + string.Join(", ", wrong) + ")");

        bool invites = true, four = true, unique = true, next = true;
        foreach (int year in new[] { 60, 90, 365 })
        {
            var stamps = new HashSet<int>();
            int last = -1;
            for (int day = 1; day <= year * 3; day++)
            {
                int which = DinnerRules.DinnerOn(day, year, out int stamp);
                if (which >= 0)
                {
                    int told = DinnerRules.InviteOn(day - 1, year, out int toldStamp);
                    invites &= day == 1 || (told == which && toldStamp == stamp);
                    unique &= stamps.Add(stamp) && stamp > last;
                    last = stamp;
                }
                else
                {
                    invites &= DinnerRules.InviteOn(day - 1, year, out _) < 0 || day == 1;
                }
                int coming = DinnerRules.Next(day, year, out int inDays);
                bool between = coming >= 0 && DinnerRules.DinnerOn(day + inDays, year, out _) == coming;
                for (int k = 0; k < inDays && between; k++)
                {
                    between = DinnerRules.DinnerOn(day + k, year, out _) < 0;
                }
                next &= between;
            }
            four &= stamps.Count == DinnerRules.Count * 3;
        }
        Check(invites && four && unique, "family dinners: Mom's invitation falls the day before each dinner and on no other day; four dinners a year, each with its own rising stamp");
        Check(next, "family dinners: the Family page's next dinner is the first one from today on, today's included, and none falls before it");
        Check(DinnerRules.DinnerOn(0, 60, out int none) < 0 && none == -1 && DinnerRules.InviteOn(5, 2, out _) < 0 && DinnerRules.Next(-3, 60, out _) < 0
              && DinnerRules.DinnerOn(29 + 60, 60, out int again) == 1 && again == DinnerRules.Count + 1,
            "family dinners: a day before the first, or a year too short to read, has no dinner; the second year's summer dinner is stamped after the first year's four");
    }

    // Where and when: at home with a household, else at Mom's; her table from midday (a test's from now), the home's hours.
    private static void MomCallTable()
    {
        Check(DinnerRules.PlaceFor(true, 0) == DinnerPlace.Home && DinnerRules.PlaceFor(false, 2) == DinnerPlace.Home && DinnerRules.PlaceFor(false, 0) == DinnerPlace.Moms,
            "family dinners: a partner living in or a child at home has the dinner at home; anybody else is expected at Mom's");
        Check(DinnerRules.Table(0, 40, 13, DinnerRules.MomsFrom) == MomsTable.None && DinnerRules.Table(40, 41, 13, DinnerRules.MomsFrom) == MomsTable.None
              && DinnerRules.Table(40, 40, DinnerRules.MomsFrom - 1, DinnerRules.MomsFrom) == MomsTable.Later && DinnerRules.Table(40, 40, DinnerRules.MomsFrom, DinnerRules.MomsFrom) == MomsTable.Set
              && DinnerRules.Table(40, 40, 23, DinnerRules.MomsFrom) == MomsTable.Set && DinnerRules.Table(40, 40, 8, 0) == MomsTable.Set && DinnerRules.MomsFrom == 12,
            "family dinners: Mom's table is set from midday (12) on her dinner day to its end, later before it, never on another day; a test sets it from the moment");
        DinnerRules.HomeHours(10, false, out int f1, out int t1);
        DinnerRules.HomeHours(10, true, out int f2, out int t2);
        DinnerRules.HomeHours(18, true, out int f3, out int t3);
        DinnerRules.HomeHours(23, true, out int f4, out int t4);
        DinnerRules.HomeHours(-1, true, out int f5, out int t5);
        Check(f1 == DinnerRules.HomeFrom && t1 == DinnerRules.HomeTo && f2 == 10 && t2 == DinnerRules.HomeTo && f3 == DinnerRules.HomeFrom && t3 == DinnerRules.HomeTo
              && f4 == 23 && t4 == 24 && f5 == 0 && DinnerRules.HomeFrom == 15 && DinnerRules.HomeTo == 23 && DinnerRules.HomeLength == 3 && DinnerRules.MomsLength == 4,
            "family dinners: a dinner at home is due 15:00 to 23:00, and a test's from the hour it was pressed (to the day's end late at night), so it can be tried at once");
        DinnerRules.HomeHours(10, true, out int f6, out int t6);
        Check(f6 == f2 && t6 == t2 && DinnerRules.DinnerOn(43, 60, out int s1) == DinnerRules.DinnerOn(43, 60, out int s2) && s1 == s2,
            "family dinners: the same day and hour give the same answer every time (deterministic, nothing random or machine-local)");
    }

    // The ring: the notice answers, the ring waits behind a screen, calling them from the phone answers.
    private static void MomCallRing()
    {
        string plan = ModSource(@"Social\SocialDirector.Plan.cs"), ui = ModSource(@"Social\SocialDirector.Ui.cs"), life = ModSource(@"Social\SocialLife.cs");
        string person = ModSource(@"Social\PersonDialog.cs");
        Check(plan.Contains("SocialLife.Toast(NotificationType.Info, \"reallife_social_ringing\", data, RingSeconds, _onRingClicked ?? (_onRingClicked = Guard.Wrap(RingClicked)))")
              && life.Contains("Notifications.Show(type, key, data, seconds, null, onClick, true, false);"),
            "ring: its notice lasts the ring and answers when clicked, through a guarded action made once, never kept in the save");
        Check(plan.Contains("private void RingClicked()") && plan.Contains("DialogController.current != null || HudConfirm.isOpen") && plan.Contains("AnswerRing(\"from its notice\")")
              && ui.Contains("AnswerRing(\"from its panel\")"),
            "ring: a click on the notice answers as the panel's button does, but never over a call or conversation already open, nor a question box");
        Check(plan.Contains("_ringHeld < RingHoldMost && UiOpen()") && plan.Contains("_ringUntil += step;") && plan.Contains("RingHoldMost = 120f"),
            "ring: while a screen hides its panel (the phone open, a menu) the ring waits for it, two minutes at most, so opening the phone never lets it lapse unseen");
        Check(person.Contains("bool incoming = SocialPhone.Incoming == index || SocialDirector.TakeRing(index);") && plan.Contains("internal static bool TakeRing(int index)")
              && plan.Contains("d._ringPerson.Index != index"),
            "ring: a call placed from the phone to the one ringing is their call answered and stops the ring; a call to anybody else leaves it ringing");
        Check(plan.Contains("\"Social check (ring): \""), "ring: with Developer diagnostics on, each ring's end writes a 'Social check (ring)' line");
    }

    // Every way to the dinner: Mom's call (either dialog), the Family call, its menu, the test; one table for the day's round and the page.
    private static void MomCallDinnerWays()
    {
        string relatives = ModSource(@"Family\Relatives.cs"), page = ModSource(@"Family\FamilyPage.cs"), phone = ModSource(@"Family\FamilyPhone.cs");
        string choices = ModSource(@"Family\FamilyPhone.Choices.cs"), contacts = ModSource(@"Phone\PhoneContacts.cs"), person = ModSource(@"Social\PersonDialog.cs");
        string links = ModSource(@"Wiring\FamilyLinks.cs"), mod = ModSource(@"Core\RealLifeOverhaulMod.cs"), tests = ModSource(@"Family\FamilyDirector.Tests.cs");
        string bridges = ModSource(@"Family\FamilyBridges.cs");
        Check(relatives.Contains("DinnerRules.InviteOn(day, year, out int stamp)") && relatives.Contains("DinnerRules.DinnerOn(day, year, out stamp)") && !relatives.Contains("HolidayAt")
              && page.Contains("DinnerRules.Next(TimeHelper.CurrentDay, GameCalendar.DaysPerYear, out int inDays)") && !page.Contains("0.72f"),
            "family dinners: the day's round and the Family page read the one table (DinnerRules), with no copy of the year's dates of their own");
        Check(relatives.Contains("HomeInvites") && relatives.Contains("\"reallife_family_holiday_at_home\"") && relatives.Contains("fill.Hours(from, to)")
              && relatives.Contains("Place == DinnerPlace.Home"),
            "family dinners: Mom's invitation and her text on the day say where it is, with the home's hours for a dinner at home");
        int mom = choices.IndexOf("Relatives.CanGoToMoms", StringComparison.Ordinal), chat = choices.IndexOf("_chatAt = list.Count", StringComparison.Ordinal);
        Check(phone.Contains("Relatives.CanGoToMoms") && phone.Contains("\"reallife_family_do_moms_yes\", Relatives.GoToMoms") && mom >= 0 && chat > mom
              && choices.IndexOf("Relatives.CanGoToMoms", mom + 1, StringComparison.Ordinal) < 0,
            "family dinners: the Family contact's call opens on going over to Mom's while her table is set, and it is the first of its Things to do (once)");
        Check(contacts.Contains("string dinner = PhoneContacts.MomsDinnerNow();") && contacts.Contains("PhoneContacts.DinnerAsk(dinner, Greeting, PhoneContacts.HangUp)")
              && person.Contains("string dinner = _p.IsFamily ? PhoneContacts.MomsDinnerNow() : null;") && person.Contains("PhoneContacts.DinnerAsk(dinner, open, PhoneContacts.HangUp)")
              && person.Contains("_actions.Add(\"dinner\")") && person.Contains("case \"dinner\":") && person.Contains("dinner == null && _p.Asleep(SocialLife.Hour)"),
            "family dinners: Mom's own call, whichever dialog her contact opens, asks first whether the player is coming over (Go to Mom's), and Something else offers it too");
        Check(links.Contains("PhoneContacts.MomsDinner = () => FamilyApi.MomsDinner;") && links.Contains("PhoneContacts.GoToMoms = FamilyApi.GoToMoms;")
              && links.Contains("PhoneContacts.MomsDinner = null;") && links.Contains("PhoneContacts.GoToMoms = null;")
              && bridges.Contains("public static string MomsDinner => Active ? Relatives.MomsDinnerName : null;") && bridges.Contains("FamilyPageDialog.Guard("),
            "family dinners: Mom's call reaches family life only through the bridge wired in Wiring/FamilyLinks, taken off again, and going over is guarded");
        Check(mod.Contains("AddButton(\"reallife_family_test_dinner\", () => Family.FamilyDirector.Instance?.TestDinner())") && tests.Contains("Relatives.TestToday(out string name)")
              && tests.Contains("\"reallife_family_no_relatives_off\"") && relatives.Contains("_tableNowDay = fromNow ? day : 0;"),
            "family dinners: Options > Tests has Family dinner today, which sets today as a dinner day ready now, or says relatives are switched off");
        string features = ModSource(@"Meta\FeatureList.Life.cs");
        bool facts = true;
        foreach (string fact in new[] { "family_holiday_from", "family_holiday_to", "family_holiday_cost", "family_holiday_hours", "family_moms_from", "family_moms_hours" })
        {
            facts &= features.Contains(".Fact(\"" + fact + "\"") && LocaleFile("en")["reallife_guide_family"].Contains("{" + fact + "}");
        }
        Check(facts, "family dinners: every figure the Guide's family page gives for the dinners is filled from the table (guidefacts)");
    }

    // The words: Mom's lines say how to come, the test's and the bridge's texts, the Guide in every language, What's new in eleven.
    private static void MomCallTexts()
    {
        string[] three = { "en", "pt", "zh-cn" };
        string[] midday = { "midday", "meio-dia", "中午" };
        string[] keys =
        {
            "reallife_family_holiday_invite_1", "reallife_family_holiday_invite_2", "reallife_family_holiday_invite_3", "reallife_family_holiday_at_moms",
            "reallife_family_holiday_home_invite_1", "reallife_family_holiday_home_invite_2", "reallife_family_holiday_at_home", "reallife_family_holiday_at_moms_now",
            "reallife_mom_dinner_ask", "reallife_mom_dinner_later", "reallife_family_test_dinner", "reallife_family_test_dinner_moms", "reallife_family_test_dinner_home",
            "reallife_family_no_relatives_off"
        };
        for (int l = 0; l < three.Length; l++)
        {
            Dictionary<string, string> w = LocaleFile(three[l]);
            bool all = true, fits = true, plain = true;
            string bad = string.Empty;
            foreach (string key in keys)
            {
                string v = w.TryGetValue(key, out string t) ? t : string.Empty;
                string filled = v.Replace("{text}", "Thanksgiving").Replace("{from}", "03:00 PM").Replace("{to}", "11:00 PM");
                all &= v.Length > 0;
                fits &= filled.Length <= 240;
                bool tidy = !v.Contains("!") && !v.Contains("—") && !v.Contains("–") && !v.Contains(" - ") && !v.Contains("  ");
                plain &= tidy;
                bad += v.Length == 0 || filled.Length > 240 || !tidy ? key + " " : string.Empty;
            }
            Check(all && fits && plain, "family dinners: Mom's lines and the test's texts are in " + three[l] + ", at most 240 characters filled, with no exclamation marks, dashes or double spaces (" + bad + ")");
            bool moms = true;
            foreach (string key in new[] { "reallife_family_holiday_invite_1", "reallife_family_holiday_invite_2", "reallife_family_holiday_invite_3", "reallife_family_holiday_at_moms" })
            {
                moms &= w[key].Contains(midday[l]) && w[key].Contains("{text}");
            }
            bool home = true;
            foreach (string key in new[] { "reallife_family_holiday_home_invite_1", "reallife_family_holiday_home_invite_2", "reallife_family_holiday_at_home" })
            {
                home &= w[key].Contains("{from}") && w[key].Contains("{to}") && w[key].Contains("{text}");
            }
            Check(moms && home && w["reallife_family_holiday_at_moms"].Contains(w["reallife-family"]) && w["reallife_family_test_dinner_moms"].Contains(w["reallife_family_do_moms_yes"])
                  && w["reallife_mom_dinner_ask"].Contains("{text}") && w["reallife_family_no_relatives_off"].Contains(w["reallife_family_relatives"]),
                "family dinners: in " + three[l] + " Mom's invitations to hers say to ring her from midday, the day's text names Family too, a dinner at home gives its hours, and the test names Go to Mom's");
        }

        string[] codes = { "en", "pt", "zh-cn", "de", "fr", "es", "ru", "nl", "tr", "pl", "ko" };
        bool guide = true;
        string lacking = string.Empty;
        foreach (string code in codes)
        {
            Dictionary<string, string> w = LocaleFile(code);
            if (!w.TryGetValue("reallife_guide_family", out string g))
            {
                continue;   // a language without the page reads it in English
            }
            bool ok = g.Contains("<b>" + w["reallife-mom"] + "</b>") && g.Contains("<b>" + w["reallife-family"] + "</b>") && g.Contains("<b>" + w["reallife_family_do_moms_yes"] + "</b>")
                      && g.Contains("{family_moms_from}") && g.Contains("{family_holiday_from}") && g.Contains("{family_holiday_to}");
            guide &= ok;
            lacking += ok ? string.Empty : code + " ";
        }
        Check(guide, "family dinners: the Guide's family page says how to attend each dinner (Mom or Family from midday and Go to Mom's, or the hours at home), in every language that has it (" + lacking + ")");

        string[] lines =
        {
            "Fixed: a call from a friend went away when you opened the phone or clicked its notice.",
            "Corrigido: uma ligação de um amigo sumia quando você abria o celular ou clicava no aviso.",
            "已修复：打开手机或点击通知时，朋友的来电会消失。",
            "Behoben: Ein Anruf von Freunden verschwand, wenn du das Handy geöffnet oder auf die Mitteilung geklickt hast.",
            "Corrigé : un appel d'un ami disparaissait quand vous ouvriez le téléphone ou cliquiez sur sa notification.",
            "Corregido: una llamada de un amigo desaparecía al abrir el teléfono o pulsar su aviso.",
            "Исправлено: звонок от друга пропадал, когда вы открывали телефон или нажимали на уведомление.",
            "Opgelost: een telefoontje van een vriend verdween als je de telefoon opende of op de melding klikte.",
            "Düzeltildi: telefonu açtığınızda ya da bildirimine tıkladığınızda bir arkadaştan gelen arama kayboluyordu.",
            "Naprawiono: połączenie od znajomego znikało po otwarciu telefonu albo kliknięciu powiadomienia.",
            "수정: 휴대폰을 열거나 알림을 클릭하면 친구의 전화가 사라지던 문제를 고쳤습니다."
        };
        string[] dinner =
        {
            "Fixed: Mom's family dinners now say how to come.", "Corrigido: os jantares de família da Mãe agora dizem como ir.", "已修复：妈妈的家庭晚餐现在会说明怎么参加。",
            "Behoben: Mamas Familienessen sagen jetzt, wie du hinkommst.", "Corrigé : les dîners de famille de Maman disent maintenant comment venir.",
            "Corregido: las cenas familiares de mamá ahora dicen cómo ir.", "Исправлено: семейные ужины мамы теперь объясняют, как прийти.",
            "Opgelost: Mama's familiediners zeggen nu hoe je erheen gaat.", "Düzeltildi: Anne'nin aile yemekleri artık nasıl gideceğinizi söylüyor.",
            "Naprawiono: rodzinne obiady Mamy mówią teraz, jak przyjść.", "수정: 엄마의 가족 저녁이 이제 가는 방법을 알려 줍니다."
        };
        bool every = true, clean = true;
        string missing = string.Empty;
        for (int l = 0; l < codes.Length; l++)
        {
            Dictionary<string, string> w = LocaleFile(codes[l]);
            bool has = WhatsNewHas(w, lines[l]) && WhatsNewHas(w, dinner[l]);
            every &= has;
            missing += has ? string.Empty : codes[l] + " ";
            foreach (KeyValuePair<string, string> row in w)
            {
                if (row.Key.StartsWith("reallife_changes_", StringComparison.Ordinal) && !row.Key.EndsWith("_title", StringComparison.Ordinal) && row.Value.Contains(lines[l]))
                {
                    clean &= Occurs(row.Value, lines[l]) == 1 && Occurs(row.Value, dinner[l]) == 1 && !row.Value.Contains("!") && !row.Value.Contains("  ")
                             && !row.Value.Contains("—") && !row.Value.Contains("–");
                }
            }
        }
        Check(every, "family dinners: What's new tells of both fixes, each with its fix mark, in all eleven languages, in any row (" + missing + ")");
        Check(clean, "family dinners: each fix line is said once in its row, with no exclamation marks, dashes or double spaces");
    }
}
