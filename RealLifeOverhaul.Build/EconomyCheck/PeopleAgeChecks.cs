using System;
using System.Collections.Generic;
using RealLifeOverhaul;

// The People contact's page shows each person's age (a player's question). The stored age never changed, so a person
// stayed the age they were rolled with however many birthdays the page counted down to. Their age is now worked out when
// shown: the age on the game's first day and a year more at each birthday after it (SocialRules.AgeOn), as the game ages
// its rivals from the save's first day. The page says it in every language, and the Guide names People as a contact.
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

    private static void PeopleAge()
    {
        PeopleAgeGrows();
        PeopleAgeShown();
    }

    // Over three years of 28-day years, 4-day years and 1-day years, for every birthday: the age is the first day's on day 1,
    // and it goes up by one on exactly the days the page says are their birthday (SocialTalk.BirthdayIn is 0), and on no other.
    private static void PeopleAgeGrows()
    {
        bool steps = true, start = true;
        string fault = string.Empty;
        foreach (int perYear in new[] { 28, 4, 1 })
        {
            for (int birthday = 1; birthday <= perYear; birthday++)
            {
                start &= SocialRules.AgeOn(30, birthday, 1, perYear) == 30;
                for (int day = 2; day <= perYear * 3 + 1; day++)
                {
                    int today = (day - 1) % perYear + 1;
                    bool isBirthday = ((birthday - today) % perYear + perYear) % perYear == 0;
                    int grew = SocialRules.AgeOn(30, birthday, day, perYear) - SocialRules.AgeOn(30, birthday, day - 1, perYear);
                    if (grew != (isBirthday ? 1 : 0) && fault.Length == 0)
                    {
                        fault = "a year of " + perYear + ", birthday " + birthday + ", day " + day + ": grew " + grew;
                    }
                    steps &= grew == (isBirthday ? 1 : 0);
                }
            }
        }
        Check(start, "people's age: on the game's first day a person is the age they were rolled with, whatever their birthday");
        Check(steps, "people's age: it goes up by one on each birthday the page counts down to, and on no other day (" + fault + ")");
        Check(SocialRules.AgeOn(40, 10, 28 * 5 + 9, 28) == 45 && SocialRules.AgeOn(40, 10, 28 * 5 + 10, 28) == 46 && SocialRules.AgeOn(58, 28, 28 * 10, 28) == 68,
            "people's age: in the sixth year of 28 days, a person is five years older the day before their birthday and six on it; ten birthdays make ten years");
        Check(SocialRules.AgeOn(30, 0, 28, 28) == SocialRules.AgeOn(30, 28, 28, 28) && SocialRules.AgeOn(30, 31, 3, 28) == SocialRules.AgeOn(30, 3, 3, 28)
              && SocialRules.AgeOn(30, 5, 0, 28) == 30 && SocialRules.AgeOn(30, 5, -9, 28) == 30 && SocialRules.AgeOn(30, 5, 9, 0) >= 30,
            "people's age: a birthday outside the year is read as the birthday countdown reads it, and no day or year length makes them younger");

        // The page puts it in; the stored age is left as the first day's, so the look and the save stay as they were.
        string page = MemberText(ModSource(@"Social\PeopleDialog.cs"), "private DialogEntry Page(int at)");
        string talk = MemberText(ModSource(@"Social\SocialTalk.cs"), "public static int AgeOf(SocialPerson person)");
        Check(page.Contains("data[\"age\"] = SocialTalk.AgeOf(p).ToString();") && page.Contains("\"reallife_social_people_page\".Localize(data)")
              && talk.Contains("SocialRules.AgeOn(person.Age, person.Birthday, SocialLife.Day, SocialPlaces.DaysPerYear)"),
            "people's age: the People page shows today's age, worked out from the first day's age, the birthday and today");
        Check(!ModSource(@"Social\SocialState.cs").Contains("AgeOn") && !ModSource(@"Social\SocialState.cs").Contains("AgeOf"),
            "people's age: nothing about it is saved; the save's record is as it was");
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
