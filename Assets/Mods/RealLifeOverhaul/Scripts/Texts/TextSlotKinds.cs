namespace RealLifeOverhaul.Texts
{
    /// <summary>
    /// What a card's {slot} holds (data v6), as the contract names it: a name the game shows as it is in every language, a
    /// figure, or English words the model says in the card's language.
    /// </summary>
    public enum TextSlotKind
    {
        Text,      // English words from the game ("the coffee machine at work broke")
        Choice,    // one of a fixed list of English words ("raining", "rush hour", "yes")
        Person,    // somebody's name, as the game has it ("Dana", "Dana Reyes")
        Place,     // a place, business, street, district or company, as the game shows it in the player's language
        Name,      // another proper name the game shows (a degree, an account)
        Job,       // a job in English words ("nurse", "shop assistant")
        Number,    // a figure or a sum, as the card writes it ("12", "$400", "3%")
        Time,      // a time of day, or a day and a time ("18:30", "Friday 19:30")
        Day        // a day of the week in English ("Monday")
    }

    /// <summary>
    /// The kind of every slot a card of data v6 names, and what that means for the reply in another language: a must fact
    /// that is a name, a figure or a time's clock is found in a reply of any language as the card writes it; one of English
    /// words (a holiday, a weekday, a kind of place) only in an English reply, since the model says it in its own language.
    /// Every slot any intent names has a kind here (EconomyCheck, TextsSixChecks.cs). Game-free. COST: a switch.
    /// </summary>
    public static class TextSlotKinds
    {
        /// <summary>The kind of a slot by its name; Text for one this table does not know.</summary>
        public static TextSlotKind Of(string slot) => Of(slot, out _);

        /// <summary>True for a slot this table names (every slot of every intent must be: TextsSixChecks.cs).</summary>
        public static bool Listed(string slot)
        {
            Of(slot, out bool listed);
            return listed;
        }

        private static TextSlotKind Of(string slot, out bool listed)
        {
            listed = true;
            switch (slot)
            {
                case "first":
                case "player":
                case "other":
                case "other2":
                case "name":
                case "child":
                case "tycoon":
                case "candidate":
                    return TextSlotKind.Person;
                case "place":
                case "place_two":
                case "street":
                case "district":
                case "shop":
                case "address":
                case "company":
                case "other_company":
                case "target":
                case "works":
                case "home":
                    return TextSlotKind.Place;
                case "self":
                case "degree":
                case "handle":
                    return TextSlotKind.Name;
                case "amount":
                case "waited":
                case "days":
                case "late":
                case "n":
                case "marks":
                case "wage":
                case "least":
                case "most":
                case "minutes":
                case "change":
                case "points":
                case "stars":
                case "paid":
                case "asked":
                case "going":
                case "hours":
                case "age":
                    return TextSlotKind.Number;
                case "time":
                case "when":
                    return TextSlotKind.Time;
                case "day":
                    return TextSlotKind.Day;
                case "job":
                case "title":
                    return TextSlotKind.Job;
                case "reaction":
                case "outing":
                case "what":
                case "way":
                case "walk":
                case "weather":
                case "moment":
                case "known":
                case "event":
                case "service":
                case "prices":
                case "cleanliness":
                case "interior":
                case "about":
                case "crowd":
                case "left":
                case "happening":
                case "reason":
                case "mood":
                case "role":
                case "traits":
                case "topic":
                    return TextSlotKind.Choice;
                case "holiday":    // (the model names it in its own language: "Thanksgiving", "Ação de Graças")
                case "issue":
                case "problem":
                    return TextSlotKind.Text;
                default:
                    listed = false;
                    return TextSlotKind.Text;
            }
        }

        /// <summary>The kind's name as the contract writes it.</summary>
        public static string Name(TextSlotKind kind)
        {
            switch (kind)
            {
                case TextSlotKind.Choice: return "choice";
                case TextSlotKind.Person: return "person";
                case TextSlotKind.Place: return "place";
                case TextSlotKind.Name: return "name";
                case TextSlotKind.Job: return "job";
                case TextSlotKind.Number: return "number";
                case TextSlotKind.Time: return "time";
                case TextSlotKind.Day: return "day";
                default: return "text";
            }
        }

        /// <summary>The kind of a must line's template: its first slot's, or a figure for a template of digits alone ("3").</summary>
        public static TextSlotKind OfMust(string template)
        {
            if (string.IsNullOrEmpty(template))
            {
                return TextSlotKind.Text;
            }
            int open = template.IndexOf('{');
            int close = open >= 0 ? template.IndexOf('}', open + 1) : -1;
            if (close > open)
            {
                return Of(template.Substring(open + 1, close - open - 1));
            }
            for (int i = 0; i < template.Length; i++)
            {
                if (!char.IsDigit(template[i]))
                {
                    return TextSlotKind.Text;
                }
            }
            return TextSlotKind.Number;
        }

        /// <summary>
        /// The words a reply in a language must hold for a must fact of this kind: the fact itself for a name, a figure or
        /// any fact in English; a time's clock alone ("19:30" of "Friday 19:30") in another language; null when the model
        /// says the fact in its own words (English words in another language: nothing to look for), a place of common
        /// words among them ("the park": its first letter small, where a name has a capital).
        /// </summary>
        public static string Needed(string fact, TextSlotKind kind, string lang)
        {
            if (string.IsNullOrEmpty(fact) || lang == null || lang == TextLanguages.English)
            {
                return fact;
            }
            switch (kind)
            {
                case TextSlotKind.Place:
                case TextSlotKind.Name:
                    return char.IsLower(fact[0]) ? null : fact;
                case TextSlotKind.Person:
                case TextSlotKind.Number:
                    return fact;
                case TextSlotKind.Time:
                    return Clock(fact);
                default:
                    return null;
            }
        }

        // The clock of a time ("19:30" of "Friday 19:30"); null when there is none.
        private static string Clock(string time)
        {
            int colon = time.IndexOf(':');
            if (colon <= 0)
            {
                return null;
            }
            int start = colon;
            while (start > 0 && char.IsDigit(time[start - 1]))
            {
                start--;
            }
            int end = colon + 1;
            while (end < time.Length && char.IsDigit(time[end]))
            {
                end++;
            }
            return start < colon && end > colon + 1 ? time.Substring(start, end - start) : null;
        }
    }
}
