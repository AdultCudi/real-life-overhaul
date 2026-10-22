using System;
using System.Collections.Generic;
using RealLifeOverhaul;

// The People contact's page shows each person's age (a player's question). The stored age never changed, so a person
// stayed the age they were rolled with however many birthdays the page counted down to. Their age is now worked out when
// shown: the stored age is true on the person's AgeDay (the day the cast was made, or an older save's first load with this
// version), and each birthday after that day adds a year (SocialRules.AgeOn). AgeDay is saved at the end of each person's
// record, after the look, so an older version reads the record as before. The page says it in every language, and the
// Guide names People as a contact.
internal static partial class Program
{
    // The page's first line in each language of ModLanguages.All: the name, the age as each language says a person's, the stage.
    private static readonly string[] PeopleAgeLines =
    {
        "{name}, {age}: {stage}", "{name}, {age} anos: {stage}", "{name}，{age}岁：{stage}", "{name}, {age} Jahre: {stage}", "{name}, {age} ans : {stage}",
        "{name}, {age} años: {stage}", "{name}, возраст {age}: {stage}", "{name}, {age} jaar: {stage}", "{name}, {age} yaşında: {stage}"
    };

    // The Guide's family page, in each language, names the People contact as a contact, by its name in that language.
    private static readonly string[] PeopleContactInGuide =
    {
        "in the <b>People</b> contact", "do contato <b>Pessoas</b>", "<b>人脉</b>联系人", "im Kontakt <b>Leute</b>", "du contact <b>Personnes</b>",
        "del contacto <b>Gente</b>", "в контакте <b>Люди</b>", "in het contact <b>Mensen</b>", "<b>Kişiler</b> kişisindeki"
    };

    private const int AgeYear = 28;   // days in a year for the save checks below; any length works

    private static void PeopleAge()
    {
        PeopleAgeGrows();
        PeopleAgeSaved();
        PeopleAgeShown();
    }

    // Whether game day is the birthday, with the day of the year as SocialTalk.BirthdayIn has it.
    private static bool IsBirthday(int birthday, int day, int perYear)
    {
        int today = (Math.Max(1, day) - 1) % perYear + 1;
        return ((birthday - today) % perYear + perYear) % perYear == 0;
    }

    // Over years of 28, 4 and 1 days, for every birthday and for an age true on day 1 and on later days: the stored age on and
    // before that day, and a year more on exactly the birthdays after it the page counts down to (SocialTalk.BirthdayIn is 0).
    private static void PeopleAgeGrows()
    {
        bool steps = true, start = true;
        string fault = string.Empty;
        foreach (int perYear in new[] { 28, 4, 1 })
        {
            foreach (int ageDay in new[] { 1, perYear * 2 + 1, perYear * 3 })
            {
                for (int birthday = 1; birthday <= perYear; birthday++)
                {
                    for (int day = 1; day <= ageDay; day++)
                    {
                        start &= SocialRules.AgeOn(30, ageDay, birthday, day, perYear) == 30;
                    }
                    for (int day = ageDay + 1; day <= ageDay + perYear * 3; day++)
                    {
                        bool isBirthday = IsBirthday(birthday, day, perYear);
                        int grew = SocialRules.AgeOn(30, ageDay, birthday, day, perYear) - SocialRules.AgeOn(30, ageDay, birthday, day - 1, perYear);
                        if (grew != (isBirthday ? 1 : 0) && fault.Length == 0)
                        {
                            fault = "a year of " + perYear + ", age true on day " + ageDay + ", birthday " + birthday + ", day " + day + ": grew " + grew;
                        }
                        steps &= grew == (isBirthday ? 1 : 0);
                    }
                }
            }
        }
        Check(start, "people's age: on the day their age is true on, and before it, a person is their stored age, whatever their birthday");
        Check(steps, "people's age: it goes up by one on each birthday after that day the page counts down to, and on no other day (" + fault + ")");
        Check(SocialRules.AgeOn(40, 1, 10, AgeYear * 5 + 9, AgeYear) == 45 && SocialRules.AgeOn(40, 1, 10, AgeYear * 5 + 10, AgeYear) == 46
              && SocialRules.AgeOn(25, 1000, 10, 1000, AgeYear) == 25 && SocialRules.AgeOn(25, 1000, 10, 1000 + AgeYear * 2, AgeYear) == 27,
            "people's age: in the sixth year one rolled on day 1 is five years older the day before their birthday and six on it; one rolled on day 1,000 is their rolled age that day, not 35 years more");
        Check(SocialRules.AgeOn(30, 1, 0, 28, 28) == SocialRules.AgeOn(30, 1, 28, 28, 28) && SocialRules.AgeOn(30, 1, 31, 3, 28) == SocialRules.AgeOn(30, 1, 3, 3, 28)
              && SocialRules.AgeOn(30, 1, 5, 0, 28) == 30 && SocialRules.AgeOn(30, 1, 5, -9, 28) == 30 && SocialRules.AgeOn(30, 1, 5, 9, 0) >= 30
              && SocialRules.AgeOn(30, 400, 5, 9, 28) == 30 && SocialRules.AgeOn(30, -5, 5, 9, 28) == 31,
            "people's age: a birthday outside the year is read as the birthday countdown reads it, and no day, anchor or year length makes them younger");

        string page = MemberText(ModSource(@"Social\PeopleDialog.cs"), "private DialogEntry Page(int at)");
        string talk = MemberText(ModSource(@"Social\SocialTalk.cs"), "public static int AgeOf(SocialPerson person)");
        Check(page.Contains("data[\"age\"] = SocialTalk.AgeOf(p).ToString();") && page.Contains("\"reallife_social_people_page\".Localize(data)")
              && talk.Contains("SocialRules.AgeOn(person.Age, person.AgeDay, person.Birthday, SocialLife.Day, SocialPlaces.DaysPerYear)"),
            "people's age: the People page shows today's age, worked out from the stored age, the day it is true on, the birthday and today");
    }

    // A cast made late, the anchor through a save and a load, a record from before the anchor, and an older version's reading.
    private static void PeopleAgeSaved()
    {
        // A save that took the social life up on day 500: everybody is their rolled age that day.
        var data = new Dictionary<string, string>();
        SocialState.Load(data, 500, AgeYear, 9, 5150);
        bool rolled = true;
        var ages = new int[SocialRules.BaseCast];
        var birthdays = new int[SocialRules.BaseCast];
        for (int i = 0; i < SocialRules.BaseCast; i++)
        {
            SocialPerson p = SocialState.Get(i);
            rolled &= p != null && p.AgeDay == 500 && SocialRules.AgeOn(p.Age, p.AgeDay, p.Birthday, 500, AgeYear) == p.Age;
            ages[i] = p?.Age ?? 0;
            birthdays[i] = p?.Birthday ?? 0;
        }
        Check(rolled, "people's age: a cast made on day 500 shows the ages it was rolled with on day 500");

        // Saved and loaded on day 900: the anchor is kept, and the age is the rolled one plus the birthdays from day 501 to 900.
        SocialState.Save(data);
        string saved = data["reallife:social_people"];
        SocialState.Load(data, 900, AgeYear, 9, 1);
        bool kept = true;
        for (int i = 0; i < SocialRules.BaseCast; i++)
        {
            SocialPerson p = SocialState.Get(i);
            int birthdaysSince = 0;
            for (int day = 501; day <= 900; day++)
            {
                birthdaysSince += IsBirthday(birthdays[i], day, AgeYear) ? 1 : 0;
            }
            kept &= p != null && p.AgeDay == 500 && p.Age == ages[i] && SocialRules.AgeOn(p.Age, p.AgeDay, p.Birthday, 900, AgeYear) == ages[i] + birthdaysSince;
        }
        Check(kept, "people's age: the day their age is true on survives a save and a load, and the age grows only by the birthdays since");

        // The record: field 41 is still the look and the anchor comes after it, so a version that reads 41 or 42 fields reads all
        // it did (a call back's fields come after the anchor: CallBackChecks.cs).
        string[] records = saved.Split('\n');
        bool shape = records.Length == SocialRules.BaseCast;
        foreach (string record in records)
        {
            string[] f = record.Split('|');
            shape &= f.Length >= 43 && f[42] == "500" && int.TryParse(f[0], out int index) && index >= 0 && index < SocialRules.BaseCast
                     && f[41] == (SocialState.Get(index)?.Look ?? string.Empty);
        }
        Check(shape, "people's age: the day is saved after the look in each person's record (field 42), so an older version still reads the look and the rest");

        // A save from before the anchor (records of 42 fields), first loaded with it on day 300: the stored age is their age that
        // day, and one more at the next birthday after it.
        var trimmed = new string[records.Length];
        for (int i = 0; i < records.Length; i++)
        {
            trimmed[i] = string.Join("|", records[i].Split('|'), 0, 42);
        }
        data["reallife:social_people"] = string.Join("\n", trimmed);
        SocialState.Load(data, 300, AgeYear, 9, 2);
        bool older = true;
        for (int i = 0; i < SocialRules.BaseCast; i++)
        {
            SocialPerson p = SocialState.Get(i);
            int next = 301;
            while (!IsBirthday(birthdays[i], next, AgeYear))
            {
                next++;
            }
            older &= p != null && p.AgeDay == 300 && p.Age == ages[i] && SocialRules.AgeOn(p.Age, p.AgeDay, p.Birthday, 300, AgeYear) == ages[i]
                     && SocialRules.AgeOn(p.Age, p.AgeDay, p.Birthday, next - 1, AgeYear) == ages[i] && SocialRules.AgeOn(p.Age, p.AgeDay, p.Birthday, next, AgeYear) == ages[i] + 1;
        }
        Check(older, "people's age: a save from before, first loaded on day 300, shows the stored age that day and one more at the next birthday after it");
        var resaved = new Dictionary<string, string>();
        SocialState.Save(resaved);
        string[] first = resaved.TryGetValue("reallife:social_people", out string again) ? again.Split('\n')[0].Split('|') : new string[0];
        Check(first.Length >= 43 && first[42] == "300", "people's age: and that first load's day is saved, so the age goes on from there");
    }

    // Every language's own file has the page with the age on its first line, in that language's words, and the Guide's
    // family page names People as a contact.
    private static void PeopleAgeShown()
    {
        string folder = LocalesFolder();
        string[] codes = ModLanguages.All;
        var wrong = new List<string>();
        var guide = new List<string>();
        for (int l = 0; l < codes.Length; l++)
        {
            Dictionary<string, string> words = LoadWords(System.IO.Path.Combine(folder, codes[l] + ".json"));
            string page = words != null && words.TryGetValue("reallife_social_people_page", out string p) ? p : string.Empty;
            int line = page.IndexOf('\n');
            if (line < 0 || page.Substring(0, line) != PeopleAgeLines[l] || page.IndexOf("{age}", line, StringComparison.Ordinal) >= 0)
            {
                wrong.Add(codes[l]);
            }
            string family = words != null && words.TryGetValue("reallife_guide_family", out string f) ? f : string.Empty;
            if (!family.Contains(PeopleContactInGuide[l]))
            {
                guide.Add(codes[l]);
            }
        }
        Check(wrong.Count == 0, "people's age: the People page's first line shows the age in each of the nine languages' own words (not so in: " + string.Join(", ", wrong) + ")");
        Check(guide.Count == 0, "people's age: the Guide's family page names People as a contact in all nine languages (not so in: " + string.Join(", ", guide) + ")");
        Check(LineEverywhere(new[]
              {
                  "The People contact shows each person's age", "O contato Pessoas mostra a idade de cada pessoa", "人脉联系人现在会显示每个人的年龄",
                  "Der Kontakt Leute zeigt das Alter jeder Person", "Le contact Personnes affiche l'âge de chaque personne",
                  "El contacto Gente muestra la edad de cada persona", "Контакт «Люди» показывает возраст каждого человека",
                  "Het contact Mensen toont ieders leeftijd", "Kişiler kişisi artık her kişinin yaşını gösteriyor"
              }),
            "people's age: What's new has the age line, as a feature's line, in some row of every language");
    }
}
