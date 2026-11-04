using System.Collections.Generic;
using RealLifeOverhaul.Ui;

/// <summary>
/// The words the work samples put in their data: the same keys the live sources read (days, hours, ranks, courses, months), so a render in Portuguese or
/// Chinese carries the language through the dates, the hours and the names of the catalogue, and no English stands in a sentence. The English of each key
/// is kept in <see cref="WkEn"/> (UiSamples.WorkEn.cs, made from en.json by not-in-repo/scratch/work/genwken.py).
/// </summary>
internal static partial class UiSamples
{
    /// <summary>The sample's today: the status bar of the previews says Day 412, so every date of the work samples is read against it.</summary>
    public const int Today = 412;

    private static readonly string[] WkWeekdays = { "Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun" };

    /// <summary>A text by its key, its English from the language file.</summary>
    private static string Sx(UtilitiesWords w, string key) => w.T(key, WkEn.TryGetValue(key, out string en) ? en : key);

    /// <summary>A text by its key with its parts filled in.</summary>
    private static string Sf(UtilitiesWords w, string key, params string[] pairs) => w.F(key, WkEn.TryGetValue(key, out string en) ? en : key, pairs);

    /// <summary>"Day 399".</summary>
    private static string SDay(UtilitiesWords w, int day) => Sf(w, "reallife_econ_day", "day", day.ToString());

    /// <summary>"Mon".</summary>
    private static string SWd(UtilitiesWords w, int i) => w.T("reallife_study_day_" + i, WkWeekdays[i]);

    // The short day names of a card's narrow line (the live source names them through the same keys).
    private static string SWdCard(UtilitiesWords w, int i) => w.T("reallife_work_day_card_" + i, WkWeekdays[i]);

    /// <summary>"09:00 to 17:00" in the player's words.</summary>
    private static string SHours(UtilitiesWords w, int from, int to) => Sf(w, "reallife_career_row_hours", "from", (from % 24).ToString("00") + ":00", "to", (to % 24).ToString("00") + ":00");

    /// <summary>"Mon to Fri, 09:00 to 17:00" (first and last day of the week, 0 for Monday).</summary>
    private static string SWeek(UtilitiesWords w, int first, int last, int from, int to)
    {
        string days = first == last ? SWd(w, first) : Sf(w, "reallife_career_row_hours", "from", SWd(w, first), "to", SWd(w, last));
        return Sf(w, "reallife_work_hours", "days", days, "from", (from % 24).ToString("00") + ":00", "to", (to % 24).ToString("00") + ":00");
    }

    /// <summary>"$21.50 an hour".</summary>
    private static string SPerHour(UtilitiesWords w, string rate) => Sf(w, "reallife_career_row_rate", "rate", rate);

    /// <summary>"11 months".</summary>
    private static string SMonths(UtilitiesWords w, int n) => n.ToString() + " " + Sx(w, "reallife_work_months");

    /// <summary>A rank's title: reallife_pro_rank_{profession}_{n}.</summary>
    private static string SRank(UtilitiesWords w, string prof, int rank) => Sx(w, "reallife_pro_rank_" + prof + "_" + rank);

    /// <summary>A profession's name: reallife_pro_name_{profession}.</summary>
    private static string SPro(UtilitiesWords w, string prof) => Sx(w, "reallife_pro_name_" + prof);

    /// <summary>A course's name: reallife_study_course_{key}.</summary>
    private static string SCourse(UtilitiesWords w, string key) => Sx(w, "reallife_study_course_" + key);

    /// <summary>A skill's name: reallife_study_skill_{key}.</summary>
    private static string SSkill(UtilitiesWords w, string key) => key.StartsWith("t:") ? Sx(w, "reallife_talent_skill_" + key.Substring(2)) : Sx(w, "reallife_study_skill_" + key);

    /// <summary>"you have 14".</summary>
    private static string SHave(UtilitiesWords w, int n) => Sf(w, "reallife_work_you_have", "n", n.ToString());

    /// <summary>A rating of a seller or a person in halves of five stars, from tenths (4.1 is 41).</summary>
    private static int SHalves(int tenths) => (tenths * 2 + 5) / 10;

    private static readonly Dictionary<string, string> WkEnLocal = new Dictionary<string, string>();
}
