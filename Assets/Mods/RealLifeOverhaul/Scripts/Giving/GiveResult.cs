namespace RealLifeOverhaul.Giving
{
    /// <summary>How a gift, a loan or an answer ended. Each has a line of its own for the notice or the call.</summary>
    public enum GiveResult
    {
        Done = 0,        // the money moved and landed
        DoneOnly = 1,    // the money moved, and only the relationship changed: the person is not a resident the population holds
        Off = 2,         // giving is switched off or not yet read
        Broke = 3,       // the spare cash does not cover it
        NoCause = 4,     // the city's people are not there to help (Living population off, or the city not known)
        Session = 5,     // in a Going Public session the city's people are the host's
        NoPerson = 6,    // not a person the population holds, so a loan has nobody to be paid back by
        Limit = 7,       // too many loans open, or this person has one, or the amount is over the most
        Nothing = 8      // no amount
    }

    /// <summary>The words of a result.</summary>
    public static class GivingResults
    {
        private static readonly string[] Keys =
        {
            "reallife_giving_result_done", "reallife_giving_result_done_only", "reallife_giving_result_off", "reallife_giving_result_broke",
            "reallife_giving_result_no_cause", "reallife_giving_result_session", "reallife_giving_result_no_person", "reallife_giving_result_limit",
            "reallife_giving_result_nothing"
        };

        public static string KeyOf(GiveResult r) => Keys[GivingCatalogue.Clamp((int)r, 0, Keys.Length - 1)];

        public static bool Moved(GiveResult r) => r == GiveResult.Done || r == GiveResult.DoneOnly;
    }
}
