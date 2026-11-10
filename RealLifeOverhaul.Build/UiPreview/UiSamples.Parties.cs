using System;
using System.Collections.Generic;
using RealLifeOverhaul.Parties;
using RealLifeOverhaul.Ui;

// The Parties app's samples: the facts each page reads, invented but shaped as the game's are (a long guest list, a business with a long name, an empty diary, a full one,
// a guest of a session). Every word goes through the text source, so the checks measure the longest translation. Fixtures, never shipped.
internal static partial class UiSamples
{
    public static readonly string[] PartiesStates = { "host", "host_refused", "host_venue", "host_nobody", "staff", "staff_refused", "staff_none", "diary", "diary_invite", "diary_empty", "guest",
        "off" };

    /// <summary>A recording stand-in for the director: each press says which action it was, so the checks can see a button reaches the one it was drawn for.</summary>
    public sealed class PartyStub : IPartyActions
    {
        public string Last;
        public int Count;

        private void Did(string what)
        {
            Last = what;
            Count++;
        }

        public void NextPlace() => Did("place");
        public void OtherVenue() => Did("venue");
        public void NextTier() => Did("tier");
        public void Day(int by) => Did(by < 0 ? "day_less" : "day_more");
        public void Hour(int by) => Did(by < 0 ? "hour_less" : "hour_more");
        public void Toggle(int person) => Did("toggle:" + person);
        public void Host() => Did("host");
        public void NextBusiness() => Did("business");
        public void NextKind() => Did("kind");
        public void EventDay(int by) => Did(by < 0 ? "event_day_less" : "event_day_more");
        public void EventHour(int by) => Did(by < 0 ? "event_hour_less" : "event_hour_more");
        public void Book() => Did("book");
        public void NextSchedule() => Did("schedule");
        public void NextPlanKind() => Did("plan_kind");
        public void OpenManagers() => Did("managers");
        public void Cancel(int kind, int id) => Did("cancel:" + kind + ":" + id);
        public void Reply(int id, bool yes) => Did((yes ? "accept:" : "decline:") + id);
        public void Locate(int kind, int id) => Did("locate:" + kind + ":" + id);
    }

    private static readonly string[] PartyNames =
    {
        "Margaret Okonkwo-Hartley", "Alex Rivera", "Dana Whitfield", "Tomasz Zielinski", "Priya Raman", "Jun Takahashi", "Bernadette O'Sullivan", "Carl Lindqvist",
        "Imani Clarke", "Hugo Marchetti", "Sofia Petrenko", "Wen Li", "Oluwaseun Adeyemi", "Greta Hallberg"
    };

    /// <summary>The model of a state: the same page the game writes, from invented facts.</summary>
    public static VentModel Parties(UiWords t, string state, VentView view = null, IPartyActions act = null)
    {
        VentWords w = VentWordsOf(t);
        view = view ?? new VentView();
        if (view.C == 0)
        {
            view.C = 1;   // the state's own tab, once: a tab pressed afterwards stays pressed
            view.Tab = state.StartsWith("staff", StringComparison.Ordinal) ? PartyPages.TabStaff : state.StartsWith("diary", StringComparison.Ordinal) ? PartyPages.TabDiary : view.Tab;
        }
        var m = new VentModel();
        PartyPages.Fill(m, view, PartiesFacts(t, state), act ?? new PartyStub(), w);
        return m;
    }

    private static PartyFacts PartiesFacts(UiWords t, string state)
    {
        if (state == "off")
        {
            return new PartyFacts { Ready = false };
        }
        var f = new PartyFacts
        {
            Place = state == "host_venue" ? PartyPlace.Restaurant : PartyPlace.Home, Tier = PartyTier.Good, PlaceFound = true,
            PlaceName = state == "host_venue" ? "The Gilded Oyster and Chop House on Seventh" : t("reallife_party_place_home_name", "Your home"), DayText = "tomorrow at 19:00",
            HourText = "19:00", HeadText = state == "host_venue" ? "$36" : "$24", CostText = "$168 for 6 guests and you", Capacity = 12, Hours = 3, CanDayLess = true,
            CanDayMore = true, CanHourLess = true, CanHourMore = true, Kind = StaffEventKind.EveningOut, EventDayText = "day 214 at 18:00", EventHourText = "18:00",
            EventHeadText = "$56", EventCostText = "$1,008", EventHours = 3, CanEventDayLess = true, CanEventDayMore = true, CanEventHourLess = true, CanEventHourMore = true
        };
        if (state == "guest")
        {
            f.Stand = t("reallife_party_no_guest", "You are a guest in another player's city, and its people are theirs. Nothing can be booked or answered here.");
        }
        if (state == "host_refused")
        {
            f.Why = t("reallife_party_no_reserve", "Your wallet would fall under what it keeps for your firm's investors.");
        }
        var people = new List<PartyPersonFact>();
        var answers = new[]
        {
            PartyAnswer.Yes, PartyAnswer.Yes, PartyAnswer.Working, PartyAnswer.Yes, PartyAnswer.Quarrel, PartyAnswer.Yes, PartyAnswer.Far, PartyAnswer.Yes, PartyAnswer.Busy,
            PartyAnswer.Yes, PartyAnswer.NotClose, PartyAnswer.RoughDay, PartyAnswer.Asleep, PartyAnswer.Yes
        };
        for (int i = 0; state != "host_nobody" && i < PartyNames.Length; i++)
        {
            people.Add(new PartyPersonFact
            {
                Index = i, Name = PartyNames[i], Stage = i < 2 ? "Close friend" : i < 7 ? "Friend" : "Acquaintance", Picked = i < 11, Answer = answers[i],
                Ring = i % 4 == 0 ? UiRole.Ok : i % 4 == 1 ? UiRole.Notice : UiRole.Warn
            });
        }
        f.People = people.ToArray();
        f.Picked = state == "host_nobody" ? 0 : 11;
        f.Coming = state == "host_nobody" ? 0 : 6;
        if (state == "host_nobody")
        {
            f.Why = t("reallife_party_no_guests", "Ask somebody first.");
        }
        if (state != "staff_none")
        {
            f.Businesses = new[]
            {
                new PartyBizFact
                {
                    Place = "Seventh%20Avenue~1204", Name = "Hartley and Daughters Fine Groceries and Delicatessen", Staff = 18, Low = 3, Underpaid = 5, Every = 60,
                    PlanKind = StaffEventKind.Lunch, HasManager = true, Manager = "Bernadette O'Sullivan", Last = "Staff lunch, day 171"
                },
                new PartyBizFact { Place = "Elm%20Street~10", Name = "Speedy Bites", Staff = 6, Every = 0, PlanKind = StaffEventKind.Lunch, HasManager = false }
            };
            f.Business = state == "staff_refused" ? 1 : 0;
        }
        if (state == "staff_refused")
        {
            f.EventWhy = t("reallife_party_no_often", "The staff there have an event within a week of that day.");
        }
        if (state != "diary_empty")
        {
            var coming = new List<PartyRowFact>
            {
                new PartyRowFact
                {
                    Id = 4, Kind = PartyPages.RowParty, Title = "Your home", Sub = "tomorrow at 19:00, 6 guests, Planned", Chip = "Planned", ChipTone = UiRole.Notice, Amount = "$168",
                    Icon = UiIcon.Home, CanCancel = true, CanLocate = true, Back = "$134"
                },
                new PartyRowFact
                {
                    Id = 7, Kind = PartyPages.RowEvent, Title = "Evening out at Hartley and Daughters Fine Groceries and Delicatessen", Sub = "day 214 at 18:00, 18 staff, Planned",
                    Chip = "Planned", ChipTone = UiRole.Ok, Amount = "$1,008", Icon = UiIcon.Briefcase, CanCancel = true, Back = "$806"
                }
            };
            if (state == "diary_invite")
            {
                coming.Insert(0, new PartyRowFact
                {
                    Id = 9, Kind = PartyPages.RowInvite, Title = "Margaret Okonkwo-Hartley's party", Face = "Margaret Okonkwo-Hartley", Icon = UiIcon.Person,
                    Sub = "The Gilded Oyster and Chop House on Seventh, day 213 at 19:00, Waiting for your answer", Chip = "Waiting for your answer", ChipTone = UiRole.Notice,
                    CanAnswer = true, CanLocate = true
                });
            }
            f.Coming0 = coming.ToArray();
            f.Past = new[]
            {
                new PartyRowFact
                {
                    Id = 2, Kind = PartyPages.RowParty, Title = "Club Meridian", Sub = "day 204 at 21:00, 9 guests, Held", Chip = "Held", ChipTone = UiRole.Ok, Amount = "$840",
                    Icon = UiIcon.Store
                },
                new PartyRowFact
                {
                    Id = 1, Kind = PartyPages.RowParty, Title = "Your home", Sub = "day 190 at 18:00, 4 guests, Missed", Chip = "Missed", ChipTone = UiRole.Danger, Amount = "$120",
                    Icon = UiIcon.Home
                }
            };
            f.City = new[]
            {
                new PartyRowFact { Id = 0, Kind = PartyPages.RowRival, Title = "Calloway Meridian Holdings", Sub = "Team day for 5 staff, day 209", Amount = "$565", Icon = UiIcon.Flag }
            };
        }
        return f;
    }
}
