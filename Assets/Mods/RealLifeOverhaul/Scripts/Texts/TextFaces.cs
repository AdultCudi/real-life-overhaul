using System.Collections.Generic;

namespace RealLifeOverhaul.Texts
{
    /// <summary>Who is speaking: the shape of the card's first lines.</summary>
    public enum TextFace
    {
        Friend,          // a friend, the partner, a roommate: "Name Surname, 34, nurse, lives in Midtown", their role, traits, mood, style, the player
        Citizen,         // one of the city's people on Flare: "Dana (@dana_nyc), 34, lives in Midtown", a city resident on Flare
        CitizenDm,       // ...answering the player's message: a person on Flare you messaged
        CitizenDmMood,   // ...the same with their mood (data v3: the mood decides the tone, and a yes, maybe or no to an invitation)
        Mom,             // "Mom, 60, retired", your mother, her mood, style, the player
        InLaws,          // "Dana's parents", your in-laws, style, the player
        Boss,            // "Ms Halloran, your manager at Hudson Logistics", your boss; you work there as a clerk
        Lender,          // the lender, a back-street moneylender
        Official,        // a city account's own post: "City Police", the city's official Flare account
        OfficialAnswer,  // ...answering a report
        Tycoon,          // a rival tycoon: "Space Karen, owner of Karen Aerospace & Motors", a parody, their own style
        Business,        // a business's own account: "Sam's Deli", a business's Flare account
        Ledger,          // the City Ledger, the city's newspaper
        Employee         // data v6: one of the player's staff: "Dana Reyes, shop assistant at Sam's Deli", your employee, their style, the player
    }

    /// <summary>
    /// The speaker of a card, read from the game's state by the feature that asks, in the English words the models were
    /// trained on (TextWords). Game-free; kept and cleared by its owner.
    /// </summary>
    public sealed class TextWho
    {
        public string Name;       // full name, an account's or a business's name
        public string First;      // first name (the news lines name the speaker by it)
        public int Age;
        public string Job;        // a friend's job; a tycoon's or a boss's company
        public string District;   // where they live
        public string Handle;     // a citizen's Flare handle, with its @
        public string Role;       // a friend's stage and how they met ("a friend, met at work"), the partner's ("dating you")
        public string Title;      // the player's job title at the boss's company
        public string Mood;
        public string Style;      // the style words, a comma and a space between each
        public string Player;     // the player's name
        public string When;       // "Monday 14:05"
        public string Works;      // data v6: where one of the player's staff works (the business's name)
        public readonly List<string> Traits = new List<string>(2);

        public void Clear()
        {
            Name = First = Job = District = Handle = Role = Title = Mood = Style = Player = When = Works = null;
            Age = 0;
            Traits.Clear();
        }
    }

    /// <summary>
    /// The first lines of a card by its face, as the training generator wrote them (world.sheet_of and the sheets of
    /// defs_people, defs_work and defs_flare). Game-free. COST: a few short strings a request.
    /// </summary>
    public static class TextFaces
    {
        public static void Fill(TextFace face, TextWho w, TextSheet s)
        {
            switch (face)
            {
                case TextFace.Friend:
                    s.Who = w.Name + ", " + w.Age + ", " + w.Job + ", lives in " + w.District;
                    s.Role = w.Role;
                    s.Traits.AddRange(w.Traits);
                    s.Mood = w.Mood;
                    s.Style = w.Style;
                    s.Player = w.Player;
                    break;
                case TextFace.Citizen:
                case TextFace.CitizenDm:
                case TextFace.CitizenDmMood:
                    s.Who = w.First + " (" + w.Handle + "), " + w.Age + ", lives in " + w.District;
                    s.Role = face == TextFace.Citizen ? "a city resident on Flare" : "a person on Flare you messaged";
                    s.Mood = face == TextFace.CitizenDmMood ? w.Mood : null;
                    s.Style = w.Style;
                    break;
                case TextFace.Mom:
                    s.Who = "Mom, " + w.Age + ", retired";
                    s.Role = "your mother";
                    s.Mood = w.Mood;
                    s.Style = w.Style;
                    s.Player = w.Player;
                    break;
                case TextFace.InLaws:
                    s.Who = w.Name + "'s parents";
                    s.Role = "your in-laws";
                    s.Style = w.Style;
                    s.Player = w.Player;
                    break;
                case TextFace.Boss:
                    s.Who = w.Name + ", your manager at " + w.Job;
                    s.Role = "your boss; you work there as " + w.Title;
                    s.Style = w.Style;
                    s.Player = w.Player;
                    break;
                case TextFace.Lender:
                    s.Who = "the lender";
                    s.Role = "a back-street moneylender, not a bank";
                    s.Style = "blunt, few words";
                    s.Player = w.Player;
                    break;
                case TextFace.Official:
                case TextFace.OfficialAnswer:
                    s.Who = w.Name;
                    s.Role = face == TextFace.Official ? "the city's official Flare account" : "the city's official Flare account, answering a report";
                    s.Style = "formal, plain, calm";
                    break;
                case TextFace.Tycoon:
                    s.Who = w.Name + ", owner of " + w.Job;
                    s.Role = "a rival tycoon, a parody";
                    s.Style = w.Style;
                    break;
                case TextFace.Business:
                    s.Who = w.Name;
                    s.Role = "a business's Flare account";
                    s.Style = w.Style;
                    break;
                case TextFace.Ledger:
                    s.Who = "the City Ledger";
                    s.Role = "the city's newspaper";
                    s.Style = "plain, factual, a newspaper brief";
                    break;
                case TextFace.Employee:
                    s.Who = w.Name + ", " + w.Job + " at " + w.Works;
                    s.Role = "your employee";
                    s.Style = w.Style;
                    s.Player = w.Player;
                    break;
            }
            s.When = w.When;
        }
    }
}
