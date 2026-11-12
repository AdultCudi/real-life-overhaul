using RealLifeOverhaul.Parties;

namespace RealLifeOverhaul.Ui
{
    /// <summary>Somebody the player knows, as the guest list shows them: picked or not, and what they would answer.</summary>
    public struct PartyPersonFact
    {
        public int Index;
        public string Name, Stage;
        public bool Picked;
        public PartyAnswer Answer;
        public UiRole Ring;
    }

    /// <summary>One line of the diary: a party, an invitation, a company event or a rival's event, with what can be done about it.</summary>
    public struct PartyRowFact
    {
        public int Id;
        public int Kind;              // PartyPages.RowParty, RowInvite, RowEvent or RowRival
        public string Title, Sub, Chip, Amount;
        public UiRole ChipTone;
        public UiIcon Icon;
        public string Face;           // a host's name, for their portrait; null for a place
        public bool CanCancel, CanAnswer, CanLocate;
        public string Back;           // what calling it off gives back now, as money words
    }

    /// <summary>One of the player's businesses, as the staff events page shows it.</summary>
    public struct PartyBizFact
    {
        public string Place, Name, Last, Manager;
        public int Staff, Low, Underpaid;
        public int Every;             // the manager's schedule in days; 0 off
        public StaffEventKind PlanKind;
        public bool HasManager;
    }

    /// <summary>
    /// What the Parties app's pages read: the form of a party being planned with what each person would answer, the
    /// player's businesses and the event being booked, and the diary. Plain data: the live source reads it from the
    /// director's file and the game, the samples invent it, and the page is the same.
    /// </summary>
    public sealed class PartyFacts
    {
        public bool Ready = true;
        public string OffTitle, OffBody;
        public string Stand;          // why nothing can be booked now (a guest of a session), or null

        // ---- the party being planned
        public PartyPlace Place;
        public PartyTier Tier;
        public string PlaceName, DayText, HourText, HeadText, CostText, Why;
        public bool PlaceFound, CanDayLess, CanDayMore, CanHourLess, CanHourMore;
        public int Capacity, Picked, Coming, Hours;
        public PartyPersonFact[] People = new PartyPersonFact[0];
        public string PeopleOff;      // why there is nobody to ask (the social life is off), or null

        // ---- the event being booked
        public PartyBizFact[] Businesses = new PartyBizFact[0];
        public int Business;          // the one chosen
        public StaffEventKind Kind;
        public string EventDayText, EventHourText, EventHeadText, EventCostText, EventWhy;
        public bool CanEventDayLess, CanEventDayMore, CanEventHourLess, CanEventHourMore;
        public int EventHours;

        // ---- the diary
        public PartyRowFact[] Coming0 = new PartyRowFact[0];   // what is still to come or under way
        public PartyRowFact[] Past = new PartyRowFact[0];
        public PartyRowFact[] City = new PartyRowFact[0];      // the rival tycoons' staff events
    }

    /// <summary>What the pages' buttons do, carried to the director by the live source; the samples record them.</summary>
    public interface IPartyActions
    {
        void NextPlace();
        void OtherVenue();
        void NextTier();
        void Day(int by);
        void Hour(int by);
        void Toggle(int person);
        void Host();
        void NextBusiness();
        void NextKind();
        void EventDay(int by);
        void EventHour(int by);
        void Book();
        void NextSchedule();
        void NextPlanKind();
        void OpenManagers();
        void Cancel(int kind, int id);
        void Reply(int id, bool yes);
        void Locate(int kind, int id);
    }
}
