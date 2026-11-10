using System.Collections.Generic;

namespace RealLifeOverhaul.Parties
{
    /// <summary>One person asked to a party: who (their place in the social life's cast), what they answered, and whether they came.</summary>
    public struct PartyGuest
    {
        public int Person;
        public PartyAnswer Answer;
        public bool Came;
    }

    /// <summary>A party the player hosts: where, when, what it cost, who was asked, and how it went once it is over.</summary>
    public sealed class Party
    {
        public int Id;
        public PartyPlace Place;
        public PartyTier Tier;
        public PartyState State;
        public int Day, Hour;                  // the game day and the whole hour it starts at
        public string Street = string.Empty;   // the place's address (the game's own street name), a venue's or the home's
        public int Number;
        public string Name = string.Empty;     // what the place is called, as it was booked
        public bool Own;                       // a venue of the player's own: the goods at cost, nothing booked as a sale
        public int Paid;                       // whole money booked for it and not given back
        public int Began = -1;                 // the game minute the host began it at; -1 while they have not
        public int Quality;                    // 0 to 100, once held
        public bool Late;                      // the host began it late
        public bool Left;                      // the host was gone when it ended
        public bool Reminded;                  // the hour-before reminder went out
        public int Sqm;                        // the place's floor, square metres, as it was booked (0: not known)
        public readonly List<PartyGuest> Guests = new List<PartyGuest>(12);

        public int Start => PartyRules.Stamp(Day, Hour);
        public int End => Start + PartyCatalogue.Minutes[PartyCatalogue.Clamp((int)Tier, 0, PartyCatalogue.Minutes.Length - 1)];
        public bool Over => State != PartyState.Planned && State != PartyState.Running;

        /// <summary>Those who said yes.</summary>
        public int Coming
        {
            get
            {
                int n = 0;
                for (int i = 0; i < Guests.Count; i++)
                {
                    n += Guests[i].Answer == PartyAnswer.Yes ? 1 : 0;
                }
                return n;
            }
        }

        /// <summary>Those who came.</summary>
        public int Came
        {
            get
            {
                int n = 0;
                for (int i = 0; i < Guests.Count; i++)
                {
                    n += Guests[i].Came ? 1 : 0;
                }
                return n;
            }
        }
    }

    /// <summary>A company event at one of the player's businesses: its kind, its day and hour, what it cost and how many came.</summary>
    public sealed class StaffEvent
    {
        public int Id;
        public string Place = string.Empty;    // the business, as business pressure writes a place
        public string Name = string.Empty;     // its name, as it was booked
        public StaffEventKind Kind;
        public StaffEventState State;
        public int Day, Hour;
        public int Paid;                       // whole money booked at the business and not given back
        public int Heads;                      // staff it was booked for
        public int Came;                       // staff who came, once held
        public int Shut;                       // opening hours the business was closed for it, once held
        public bool ByManager;                 // booked by the business's manager on its schedule

        public int Start => PartyRules.Stamp(Day, Hour);
        public int End => Start + PartyCatalogue.EventMinutes[PartyCatalogue.Clamp((int)Kind, 0, PartyCatalogue.EventMinutes.Length - 1)];
    }

    /// <summary>One gain of a lift: the day it was given on and how much, per mille of the cap.</summary>
    public struct LiftGain
    {
        public int Day, Amount;
    }

    /// <summary>
    /// What parties or events gave somebody, gain by gain, per mille of their cap: every gain counts in full against the cap
    /// for a whole window of days, so no window ever holds more than the cap, and only the room left can be given again.
    /// A person of the cast is keyed by their place in it, an employee (the player's or a rival's) by their id.
    /// </summary>
    public sealed class Lift
    {
        public string Id = string.Empty;       // an employee's id; empty for a person of the cast
        public int Person = -1;                // a person's place in the cast; -1 for an employee
        public int Stand;                      // points of the company's standing an employee holds from it now: taken back as it fades
        public readonly List<LiftGain> Gains = new List<LiftGain>(4);
    }

    /// <summary>A business's manager told to hold an event every so many days.</summary>
    public sealed class StaffPlan
    {
        public string Place = string.Empty;
        public int Every;                      // days between events; 0 off
        public StaffEventKind Kind;
        public int Since;                      // the day the schedule was set: the first event is due so many days after it
        public int Tried;                      // the last day the manager tried to book and could not (one notice a refusal a week)
    }

    /// <summary>An invitation to a party of somebody the player knows.</summary>
    public sealed class Invite
    {
        public int Id;
        public int Person;                     // the host's place in the cast
        public InviteState State;
        public int Asked;                      // the day it came
        public int Day, Hour;
        public PartyPlace Place;
        public string Street = string.Empty;
        public int Number;
        public string Name = string.Empty;
        public int Began = -1;                 // the game minute the player joined it at; -1 while they have not
        public bool Reminded;                  // the hour-before reminder went out

        public int Start => PartyRules.Stamp(Day, Hour);
        public int End => Start + PartyCatalogue.InviteMinutes;
        public bool Open => State == InviteState.Asked || State == InviteState.Accepted;
    }

    /// <summary>A staff event a rival tycoon's outlet held: for the paper, the page and the outlet's next one.</summary>
    public sealed class RivalEvent
    {
        public int Day;
        public int Tycoon;
        public string Outlet = string.Empty;   // RivalFirms.Key
        public string District = string.Empty; // the game's own key of the outlet's district
        public StaffEventKind Kind;
        public int Heads;
        public int Cost;
    }

    /// <summary>
    /// Parties and company events of a save: the player's parties, their staff events, the managers' schedules, invitations,
    /// what is left of every lift, and the rivals' events. Game-free; <see cref="PartyStore"/> writes it as one line.
    /// </summary>
    public sealed class PartyFile
    {
        public int Seed;
        public int NextId = 1;
        public int Settled = -1;               // the last day the day's round ran for; -1 for a new file
        public int LastInvite = -1000;         // the day the last invitation came
        public int OffSince = -1;              // the day the option was found off, while it is; -1 while on
        public readonly List<Party> Parties = new List<Party>(PartyCatalogue.PlannedMost + PartyCatalogue.HostedMost);
        public readonly List<StaffEvent> Events = new List<StaffEvent>(PartyCatalogue.EventsKept + 4);
        public readonly List<StaffPlan> Plans = new List<StaffPlan>(8);
        public readonly List<Invite> Invites = new List<Invite>(4);
        public readonly List<Lift> Lifts = new List<Lift>(32);
        public readonly List<RivalEvent> Rivals = new List<RivalEvent>(PartyCatalogue.RivalLogMost);

        public Party Find(int id)
        {
            for (int i = 0; i < Parties.Count; i++)
            {
                if (Parties[i].Id == id)
                {
                    return Parties[i];
                }
            }
            return null;
        }

        public StaffEvent Event(int id)
        {
            for (int i = 0; i < Events.Count; i++)
            {
                if (Events[i].Id == id)
                {
                    return Events[i];
                }
            }
            return null;
        }

        public Invite Invitation(int id)
        {
            for (int i = 0; i < Invites.Count; i++)
            {
                if (Invites[i].Id == id)
                {
                    return Invites[i];
                }
            }
            return null;
        }

        /// <summary>A person's lift, made when <paramref name="make"/> and there is none.</summary>
        public Lift Of(int person, bool make)
        {
            for (int i = 0; i < Lifts.Count; i++)
            {
                if (Lifts[i].Person == person && Lifts[i].Id.Length == 0)
                {
                    return Lifts[i];
                }
            }
            if (!make)
            {
                return null;
            }
            var lift = new Lift { Person = person };
            Lifts.Add(lift);
            return lift;
        }

        /// <summary>An employee's lift, made when <paramref name="make"/> and there is none.</summary>
        public Lift Of(string id, bool make)
        {
            if (string.IsNullOrEmpty(id))
            {
                return null;
            }
            for (int i = 0; i < Lifts.Count; i++)
            {
                if (Lifts[i].Person < 0 && Lifts[i].Id == id)
                {
                    return Lifts[i];
                }
            }
            if (!make)
            {
                return null;
            }
            var lift = new Lift { Id = id };
            Lifts.Add(lift);
            return lift;
        }

        /// <summary>A business's schedule, made when <paramref name="make"/> and there is none.</summary>
        public StaffPlan Plan(string place, bool make)
        {
            for (int i = 0; i < Plans.Count; i++)
            {
                if (Plans[i].Place == place)
                {
                    return Plans[i];
                }
            }
            if (!make || string.IsNullOrEmpty(place))
            {
                return null;
            }
            var plan = new StaffPlan { Place = place };
            Plans.Add(plan);
            return plan;
        }

        /// <summary>Parties still to come or under way.</summary>
        public int Planned
        {
            get
            {
                int n = 0;
                for (int i = 0; i < Parties.Count; i++)
                {
                    n += Parties[i].Over ? 0 : 1;
                }
                return n;
            }
        }
    }
}
