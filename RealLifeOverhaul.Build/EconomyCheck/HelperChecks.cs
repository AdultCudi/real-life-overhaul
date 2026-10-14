using System;
using System.Collections.Generic;
using RealLifeOverhaul.Careers;

// Ten small helpers of 14 Oct 2026, each built on a rule that was already there: the recruitment office's last day
// (Careers/Work/ProRules.AgencyEnds), and the others added below as they come. For each: that it comes once, when it
// may and may not, the save's field and an old save without it, and its texts in the three languages (at most 240
// characters in English for a notification, and no dash or exclamation mark).
internal static partial class Program
{
    private static Dictionary<string, string>[] _helperWords;

    private static void Helpers()
    {
        if (!HelperTexts())
        {
            return;
        }
        HelperAgency();
    }

    // ---------------------------------------------------------------- 1. the recruitment office's last day

    private static void HelperAgency()
    {
        int until = 30, h = ProRules.AgencyNoteHour;
        Check(!ProRules.AgencyEnds(until, 0, until - 1, 12) && !ProRules.AgencyEnds(until, 0, until + 1, 12), "helpers, office: no reminder the day before the last day, or the day after it");
        Check(!ProRules.AgencyEnds(until, 0, until, h - 1) && ProRules.AgencyEnds(until, 0, until, h) && ProRules.AgencyEnds(until, 0, until, 23),
            "helpers, office: on the last day from " + h + ":00 (or the first hour seen after it), not in the night before");
        Check(!ProRules.AgencyEnds(until, until, until, 12), "helpers, office: once told for this registration it is not told again");
        Check(ProRules.AgencyEnds(until + ProRules.AgencyDays, until, until + ProRules.AgencyDays, h), "helpers, office: a new registration's last day is told again");
        Check(!ProRules.AgencyEnds(0, 0, 0, 12) && !ProRules.AgencyEnds(0, 0, 1, 12), "helpers, office: never registered, nothing");

        // one pass over a day's hours, as the hour event comes, with a reload in the middle: one reminder
        var f = new StudyFile { Seed = 5 };
        f.Work.AgencyUntil = until;
        int sent = 0;
        for (int hour = 0; hour < 24; hour++)
        {
            if (hour == 15)
            {
                f = CareersStore.Read(CareersStore.Write(f), out _);   // a save and a load in the afternoon
            }
            if (ProRules.AgencyEnds(f.Work.AgencyUntil, f.Work.AgencyTold, until, hour))
            {
                f.Work.AgencyTold = f.Work.AgencyUntil;
                sent++;
            }
        }
        Check(sent == 1 && f.Work.AgencyTold == until, "helpers, office: one reminder in the whole last day, a reload after it included (" + sent + ")");
        string line = CareersStore.Write(f);
        Check(CareersStore.Read(line, out _).Work.AgencyTold == until, "helpers, office: the day it was told comes back from the save");
        string old = line.Replace("|" + until + "~", "~");
        StudyFile was = CareersStore.Read(old, out _);
        Check(old != line && was.Work.AgencyTold == 0 && was.Work.AgencyUntil == until && ProRules.AgencyEnds(was.Work.AgencyUntil, was.Work.AgencyTold, until, h),
            "helpers, office: an old save without the field loads with nothing told");

        HelperNote("reallife_work_note_agency_ends", "office");
        HelperText("reallife_work_opt_agency_note", "office");
        HelperText("reallife_opt_tip_careers_agency_note", "office");
    }

    // ---------------------------------------------------------------- the texts

    private static bool HelperTexts()
    {
        string folder = System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\Assets\Mods\RealLifeOverhaul\Locales"));
        var langs = new[] { "en", "pt", "zh-cn" };
        _helperWords = new Dictionary<string, string>[langs.Length];
        for (int l = 0; l < langs.Length; l++)
        {
            _helperWords[l] = LoadWords(System.IO.Path.Combine(folder, langs[l] + ".json"));
        }
        bool found = _helperWords[0] != null && _helperWords[1] != null && _helperWords[2] != null;
        Check(found, "helpers: en.json, pt.json and zh-cn.json are where the checks look for them (" + folder + ")");
        return found;
    }

    // A text in all three languages, each with the same names in curly brackets as the English, and plain in English.
    private static void HelperText(string key, string what, params string[] slots)
    {
        bool all = true;
        for (int l = 0; l < _helperWords.Length; l++)
        {
            all &= _helperWords[l].TryGetValue(key, out string w) && w.Trim().Length > 0 && SlotsOf(w) == SlotsOf(_helperWords[0].TryGetValue(key, out string e) ? e : string.Empty);
        }
        Check(all, "helpers, " + what + ": " + key + " is in en, pt and zh-cn with the same names to fill");
        string en = _helperWords[0].TryGetValue(key, out string t) ? t : string.Empty;
        Check(en.IndexOf('!') < 0 && en.IndexOf('—') < 0 && en.IndexOf('–') < 0 && en.IndexOf(" - ", StringComparison.Ordinal) < 0,
            "helpers, " + what + ": " + key + " has no exclamation mark and no dash");
        for (int i = 0; i < slots.Length; i++)
        {
            Check(en.Contains("{" + slots[i] + "}"), "helpers, " + what + ": " + key + " names {" + slots[i] + "}");
        }
    }

    // A notification's text: as a text, and at most 240 characters in English.
    private static void HelperNote(string key, string what, params string[] slots)
    {
        HelperText(key, what, slots);
        string en = _helperWords[0].TryGetValue(key, out string t) ? t : string.Empty;
        Check(en.Length <= 240, "helpers, " + what + ": " + key + " is " + en.Length + " characters, over 240");
    }

    // An English text filled in as the game would ({name} replaced), for the length of a notification made of two texts.
    private static string HelperFill(string key, params string[] pairs)
    {
        string w = _helperWords[0].TryGetValue(key, out string t) ? t : string.Empty;
        for (int i = 0; i + 1 < pairs.Length; i += 2)
        {
            w = w.Replace("{" + pairs[i] + "}", pairs[i + 1]);
        }
        return w;
    }

    private static string SlotsOf(string words)
    {
        var names = new List<string>();
        int at = words.IndexOf('{');
        while (at >= 0)
        {
            int end = words.IndexOf('}', at);
            if (end < 0)
            {
                break;
            }
            names.Add(words.Substring(at, end - at + 1));
            at = words.IndexOf('{', end);
        }
        names.Sort(StringComparer.Ordinal);
        return string.Join(",", names);
    }
}
