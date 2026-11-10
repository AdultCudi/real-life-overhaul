namespace RealLifeOverhaul.Parties
{
    /// <summary>The locale keys parties and company events name things by: a place, a tier, a kind of event, an answer and a refusal. Game-free, so the pages and the checks share them.</summary>
    public static class PartyKeys
    {
        public static string Place(PartyPlace place)
        {
            return place == PartyPlace.Bar ? "reallife_party_place_bar" : place == PartyPlace.Restaurant ? "reallife_party_place_restaurant" : "reallife_party_place_home";
        }

        public static string Tier(PartyTier tier)
        {
            return tier == PartyTier.Lavish ? "reallife_party_tier_lavish" : tier == PartyTier.Good ? "reallife_party_tier_good" : "reallife_party_tier_simple";
        }

        public static string Kind(StaffEventKind kind)
        {
            return kind == StaffEventKind.TeamDay ? "reallife_party_kind_teamday" : kind == StaffEventKind.EveningOut ? "reallife_party_kind_evening" : "reallife_party_kind_lunch";
        }

        public static string Answer(PartyAnswer answer)
        {
            switch (answer)
            {
                case PartyAnswer.Yes: return "reallife_party_answer_yes";
                case PartyAnswer.Quarrel: return "reallife_party_answer_quarrel";
                case PartyAnswer.Asleep: return "reallife_party_answer_asleep";
                case PartyAnswer.Working: return "reallife_party_answer_working";
                case PartyAnswer.Far: return "reallife_party_answer_far";
                case PartyAnswer.RoughDay: return "reallife_party_answer_rough";
                case PartyAnswer.Busy: return "reallife_party_answer_busy";
                default: return "reallife_party_answer_notclose";
            }
        }

        /// <summary>The sentence for a refusal: every reason has its own, so nothing is ever refused without a word.</summary>
        public static string Refusal(PartyRefusal why)
        {
            switch (why)
            {
                case PartyRefusal.Off: return "reallife_party_no_off";
                case PartyRefusal.Guest: return "reallife_party_no_guest";
                case PartyRefusal.NoSave: return "reallife_party_no_save";
                case PartyRefusal.NoHome: return "reallife_party_no_home";
                case PartyRefusal.NoVenue: return "reallife_party_no_venue";
                case PartyRefusal.VenueShut: return "reallife_party_no_shut";
                case PartyRefusal.TooSoon: return "reallife_party_no_soon";
                case PartyRefusal.TooFar: return "reallife_party_no_far";
                case PartyRefusal.BadHour: return "reallife_party_no_hour";
                case PartyRefusal.DayTaken: return "reallife_party_no_day";
                case PartyRefusal.TooMany: return "reallife_party_no_many";
                case PartyRefusal.NoGuests: return "reallife_party_no_guests";
                case PartyRefusal.NobodyComes: return "reallife_party_no_nobody";
                case PartyRefusal.TooManyGuests: return "reallife_party_no_room";
                case PartyRefusal.NoMoney: return "reallife_party_no_money";
                case PartyRefusal.NoBusiness: return "reallife_party_no_business";
                case PartyRefusal.NoStaff: return "reallife_party_no_staff";
                case PartyRefusal.TooOften: return "reallife_party_no_often";
                case PartyRefusal.Reserve: return "reallife_party_no_reserve";
                case PartyRefusal.NotPlanned: return "reallife_party_no_planned";
                case PartyRefusal.NotThere: return "reallife_party_no_there";
                case PartyRefusal.TooEarly: return "reallife_party_no_early";
                case PartyRefusal.TooLate: return "reallife_party_no_late";
                case PartyRefusal.NoManager: return "reallife_party_no_manager";
                case PartyRefusal.Begun: return "reallife_party_no_begun";
                default: return "reallife_party_no_planned";
            }
        }

        public static string State(PartyState state)
        {
            switch (state)
            {
                case PartyState.Planned: return "reallife_party_state_planned";
                case PartyState.Running: return "reallife_party_state_running";
                case PartyState.Held: return "reallife_party_state_held";
                case PartyState.Missed: return "reallife_party_state_missed";
                case PartyState.Empty: return "reallife_party_state_empty";
                default: return "reallife_party_state_cancelled";
            }
        }

        public static string Event(StaffEventState state)
        {
            switch (state)
            {
                case StaffEventState.Planned: return "reallife_party_state_planned";
                case StaffEventState.Running: return "reallife_party_state_running";
                case StaffEventState.Held: return "reallife_party_state_held";
                case StaffEventState.Empty: return "reallife_party_state_empty";
                default: return "reallife_party_state_cancelled";
            }
        }

        public static string Invite(InviteState state, bool there)
        {
            switch (state)
            {
                case InviteState.Asked: return "reallife_party_state_asked";
                case InviteState.Accepted: return there ? "reallife_party_state_running" : "reallife_party_state_accepted";
                case InviteState.Declined: return "reallife_party_state_declined";
                case InviteState.Went: return "reallife_party_state_went";
                case InviteState.Missed: return "reallife_party_state_missed";
                case InviteState.Lapsed: return "reallife_party_state_lapsed";
                case InviteState.Left: return "reallife_party_state_left";
                default: return "reallife_party_state_cancelled";
            }
        }
    }
}
