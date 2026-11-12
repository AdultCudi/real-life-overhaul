using System;
using System.Collections.Generic;
using System.Globalization;
using Extensions;
using RealLifeOverhaul.Economy;
using RealLifeOverhaul.Parties;
using RealLifeOverhaul.Rivals;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// Reads the Parties app's pages from the director's file and the game, and carries every button to the director, which
    /// runs the rules and tells a refusal in a notice. What is being planned (the place, the day, the guest list, the
    /// business and the kind) is kept here while the city stands and never saved. A page is written when the director's
    /// revision moves or the player pressed something; nothing a frame.
    /// </summary>
    public sealed class PartiesLive : IVentSource, IPartyActions
    {
        public static readonly PartiesLive Instance = new PartiesLive();
        public const string ScreenId = "parties";
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        private static int _wantTab = -1;
        private readonly VentWords _words = new VentWords((key, english) => PartyText.T(key), x => x.ToCurrencyFormat());
        private readonly List<int> _picked = new List<int>(16);
        private readonly List<string> _places = new List<string>(16);
        private readonly List<StaffHand> _hands = new List<StaffHand>(32);
        private int _local;
        private PartyPlace _place;
        private PartyTier _tier = PartyTier.Good;
        private int _dayOff = 1, _hour = 19;
        private string _venueStreet;
        private int _venueNumber;
        private int _business, _eventDayOff = 1, _eventHour = PartyCatalogue.EventHourFrom[0];
        private StaffEventKind _kind;
        private GameInstance _save;

        private PartiesLive()
        {
        }

        public int Generation => (PartyDirector.Instance != null ? PartyDirector.Instance.Revision : 0) + _local;

        /// <summary>A link names a tab: taken when the screen next opens or is next written.</summary>
        public static void WantFrom(string want)
        {
            if (int.TryParse(want, NumberStyles.Integer, Inv, out int tab))
            {
                _wantTab = tab;
                Instance._local++;
            }
        }

        public void Opened(VentView view)
        {
            TakeWant(view);
        }

        private static void TakeWant(VentView view)
        {
            if (_wantTab >= 0)
            {
                view.Tab = Math.Max(0, Math.Min(PartyPages.TabDiary, _wantTab));
                view.Pick = null;
                _wantTab = -1;
            }
        }

        public bool Back(VentView view) => false;

        public void Fill(VentModel m, VentView v)
        {
            TakeWant(v);
            PartyDirector d = PartyDirector.Instance;
            GameInstance save = SaveGameManager.Current;
            if (!ReferenceEquals(save, _save))
            {
                _save = save;   // another save: what was being planned went with the last one
                _picked.Clear();
                _venueStreet = null;
                _business = 0;
            }
            PartyFacts f = d != null && PartyDirector.On && d.File != null && save != null ? Facts(d) : new PartyFacts { Ready = false };
            PartyPages.Fill(m, v, f, this, _words);
        }

        // ------------------------------------------------------------------------------------------------- the facts

        private PartyFacts Facts(PartyDirector d)
        {
            PartyFile file = d.File;
            IPartyWorld w = d.World;
            var f = new PartyFacts { Place = _place, Tier = _tier, Kind = _kind };
            f.Stand = w.Guest ? PartyText.T(PartyKeys.Refusal(PartyRefusal.Guest)) : null;
            HostFacts(f, file, w);
            StaffFacts(f, file, w);
            DiaryFacts(f, file, w);
            return f;
        }

        private PartyAsk Ask(IPartyWorld w)
        {
            return new PartyAsk { Place = _place, Tier = _tier, Day = w.Day + _dayOff, Hour = _hour, Street = _place == PartyPlace.Home ? null : _venueStreet, Number = _venueNumber,
                Guests = _picked };
        }

        private void HostFacts(PartyFacts f, PartyFile file, IPartyWorld w)
        {
            int minutes = PartyCatalogue.Minutes[(int)_tier], day = w.Day + _dayOff;
            if (_place != PartyPlace.Home && string.IsNullOrEmpty(_venueStreet))
            {
                PlaceFacts found = w.FindVenue(_place, PartyRules.Stamp(day, _hour), minutes);   // looked for once, then kept: the page names one place until another is asked for
                if (found.Found)
                {
                    _venueStreet = found.Street;
                    _venueNumber = found.Number;
                }
            }
            Party draft = PartyDesk.Draft(file, w, Ask(w), out PlaceFacts place, out int capacity, out int cost);
            PartyRefusal why = PartyDesk.Refuse(file, w, draft, place, capacity, cost);
            f.PlaceFound = place.Found;
            f.PlaceName = place.Found ? place.Name + (place.Own ? " " + PartyText.T("reallife_party_f_own") : string.Empty) : null;
            f.DayText = w.When(day, _hour);
            f.HourText = TimeHelper.GetFormattedTime(_hour, 0f, false);
            f.Hours = minutes / 60;
            f.Capacity = capacity;
            f.HeadText = w.Cash(PartyRules.HeadCost(_tier, _place, place.Own, w.Level));
            f.CostText = PartyText.Fill("reallife_party_f_cost_for", "cost", w.Cash(cost), "count", PartyText.Whole(draft.Coming));
            f.Why = why != PartyRefusal.None && why != PartyRefusal.Guest ? PartyText.T(PartyKeys.Refusal(why)) : null;
            f.CanDayLess = _dayOff > 0;
            f.CanDayMore = _dayOff < PartyCatalogue.AheadDays;
            f.CanHourLess = _hour > PartyCatalogue.FirstHour;
            f.CanHourMore = _hour < PartyCatalogue.LastHour;
            f.Picked = draft.Guests.Count;
            f.Coming = draft.Coming;
            if (!Settings.Social)
            {
                f.PeopleOff = PartyText.T("reallife_party_g_social_off");
            }
            var people = new List<PartyPersonFact>(32);
            for (int i = 0; i < w.People; i++)
            {
                SocialPerson p = w.Person(i);
                if (p == null || !p.Met || p.Drifted)
                {
                    continue;
                }
                var fact = new PartyPersonFact { Index = p.Index, Name = p.Name, Stage = SocialLife.StageName(p), Ring = StaffViews.Mood(p.Closeness) };
                for (int g = 0; g < draft.Guests.Count; g++)
                {
                    if (draft.Guests[g].Person == p.Index)
                    {
                        fact.Picked = true;
                        fact.Answer = draft.Guests[g].Answer;
                    }
                }
                people.Add(fact);
            }
            f.People = people.ToArray();
        }

        private void StaffFacts(PartyFacts f, PartyFile file, IPartyWorld w)
        {
            PartyGame.Businesses(_places);
            var list = new List<PartyBizFact>(_places.Count);
            for (int i = 0; i < _places.Count; i++)
            {
                int heads = w.Staff(_places[i], _hands);
                if (heads <= 0)
                {
                    continue;
                }
                var b = new PartyBizFact { Place = _places[i], Name = w.Business(_places[i]), Staff = heads };
                for (int h = 0; h < _hands.Count; h++)
                {
                    b.Underpaid += _hands[h].Underpaid ? 1 : 0;
                    b.Low += _hands[h].File != null && StaffRules.StandsLow(_hands[h].File) ? 1 : 0;
                }
                StaffPlan plan = file.Plan(b.Place, false);
                b.Every = plan != null ? plan.Every : 0;
                b.PlanKind = plan != null ? plan.Kind : StaffEventKind.Lunch;
                b.HasManager = w.Manager(b.Place);
                b.Manager = b.HasManager && Sessions.SessionBridges.ManagerAt(b.Place, out string name, out int _) ? name : null;
                for (int e = 0; e < file.Events.Count; e++)
                {
                    StaffEvent ev = file.Events[e];
                    if (ev.Place == b.Place && ev.State == StaffEventState.Held)
                    {
                        b.Last = PartyText.Fill("reallife_party_e_last_was", "kind", PartyText.T(PartyKeys.Kind(ev.Kind)), "day", PartyText.Whole(ev.Day));
                    }
                }
                list.Add(b);
            }
            f.Businesses = list.ToArray();
            _business = list.Count == 0 ? 0 : Math.Max(0, Math.Min(list.Count - 1, _business));
            f.Business = _business;
            int k = (int)_kind;
            _eventHour = Math.Max(PartyCatalogue.EventHourFrom[k], Math.Min(PartyCatalogue.EventHourTo[k], _eventHour));
            int day = w.Day + _eventDayOff;
            f.EventDayText = w.When(day, _eventHour);
            f.EventHourText = TimeHelper.GetFormattedTime(_eventHour, 0f, false);
            f.EventHours = Math.Max(1, PartyCatalogue.EventMinutes[k] / 60);
            f.CanEventDayLess = _eventDayOff > 0;
            f.CanEventDayMore = _eventDayOff < PartyCatalogue.AheadDays;
            f.CanEventHourLess = _eventHour > PartyCatalogue.EventHourFrom[k];
            f.CanEventHourMore = _eventHour < PartyCatalogue.EventHourTo[k];
            if (list.Count == 0)
            {
                return;
            }
            PartyBizFact chosen = list[_business];
            int cost = StaffDesk.Cost(_kind, w.Level, chosen.Staff);
            f.EventHeadText = w.Cash(StaffDesk.Cost(_kind, w.Level, 1));
            f.EventCostText = w.Cash(cost);
            PartyRefusal why = StaffDesk.Refuse(file, w, EventAsk(w, chosen.Place), chosen.Staff, cost);
            f.EventWhy = why != PartyRefusal.None && why != PartyRefusal.Guest ? PartyText.T(PartyKeys.Refusal(why)) : null;
        }

        private StaffAsk EventAsk(IPartyWorld w, string place)
        {
            return new StaffAsk { Place = place, Kind = _kind, Day = w.Day + _eventDayOff, Hour = _eventHour };
        }

        private static void DiaryFacts(PartyFacts f, PartyFile file, IPartyWorld w)
        {
            var coming = new List<PartyRowFact>(8);
            var past = new List<PartyRowFact>(12);
            int now = w.Now;
            for (int i = 0; i < file.Parties.Count; i++)
            {
                Party p = file.Parties[i];
                var row = new PartyRowFact
                {
                    Id = p.Id, Kind = PartyPages.RowParty, Title = p.Name, Icon = p.Place == PartyPlace.Home ? UiIcon.Home : UiIcon.Store, Amount = w.Cash(p.Paid),
                    Sub = PartyText.Fill("reallife_party_row_party", "when", w.When(p.Day, p.Hour), "count", PartyText.Whole(p.Over ? p.Came : p.Coming),
                        "state", PartyText.T(PartyKeys.State(p.State))),
                    Chip = PartyText.T(PartyKeys.State(p.State)), ChipTone = Tone(p.State), CanCancel = p.State == PartyState.Planned, CanLocate = !p.Over,
                    Back = w.Cash(PartyRules.Refund(p.Paid, p.Start, now))
                };
                (p.Over ? past : coming).Add(row);
            }
            for (int i = 0; i < file.Invites.Count; i++)
            {
                Invite v = file.Invites[i];
                SocialPerson host = w.Person(v.Person);
                string name = host != null ? host.Name : string.Empty;
                var row = new PartyRowFact
                {
                    Id = v.Id, Kind = PartyPages.RowInvite, Title = PartyText.Fill("reallife_party_row_invite_title", "name", name), Face = name, Icon = UiIcon.Person,
                    Sub = PartyText.Fill("reallife_party_row_invite", "place", v.Name, "when", w.When(v.Day, v.Hour), "state", PartyText.T(PartyKeys.Invite(v.State, v.Began >= 0))),
                    Chip = PartyText.T(PartyKeys.Invite(v.State, v.Began >= 0)), ChipTone = v.State == InviteState.Asked ? UiRole.Notice : v.Open || v.State == InviteState.Went ? UiRole.Ok
                        : UiRole.Muted,
                    CanAnswer = v.State == InviteState.Asked && now <= v.Start, CanLocate = v.Open
                };
                (v.Open ? coming : past).Add(row);
            }
            for (int i = 0; i < file.Events.Count; i++)
            {
                StaffEvent e = file.Events[i];
                bool over = e.State != StaffEventState.Planned && e.State != StaffEventState.Running;
                var row = new PartyRowFact
                {
                    Id = e.Id, Kind = PartyPages.RowEvent, Icon = UiIcon.Briefcase, Amount = w.Cash(e.Paid),
                    Title = PartyText.Fill("reallife_party_row_event_title", "kind", PartyText.T(PartyKeys.Kind(e.Kind)), "business", e.Name),
                    Sub = PartyText.Fill("reallife_party_row_event", "when", w.When(e.Day, e.Hour), "count", PartyText.Whole(over ? e.Came : e.Heads),
                        "state", PartyText.T(PartyKeys.Event(e.State))),
                    Chip = PartyText.T(PartyKeys.Event(e.State)), ChipTone = e.State == StaffEventState.Held || !over ? UiRole.Ok : UiRole.Muted,
                    CanCancel = e.State == StaffEventState.Planned, Back = w.Cash(PartyRules.Refund(e.Paid, e.Start, now))
                };
                (over ? past : coming).Add(row);
            }
            past.Reverse();
            var city = new List<PartyRowFact>(file.Rivals.Count);
            for (int i = file.Rivals.Count - 1; i >= 0 && city.Count < 6; i--)
            {
                RivalEvent r = file.Rivals[i];
                TycoonDef who = RivalCatalogue.Tycoons[RivalCatalogue.Clamp(r.Tycoon, 0, RivalCatalogue.TycoonCount - 1)];
                city.Add(new PartyRowFact
                {
                    Id = i, Kind = PartyPages.RowRival, Icon = UiIcon.Flag, Title = who.Holding, Amount = w.Cash(r.Cost),
                    Sub = PartyText.Fill("reallife_party_row_rival", "kind", PartyText.T(PartyKeys.Kind(r.Kind)), "count", PartyText.Whole(r.Heads), "day", PartyText.Whole(r.Day))
                });
            }
            f.Coming0 = coming.ToArray();
            f.Past = past.ToArray();
            f.City = city.ToArray();
        }

        private static UiRole Tone(PartyState state)
        {
            return state == PartyState.Held || state == PartyState.Running ? UiRole.Ok : state == PartyState.Planned ? UiRole.Notice
                : state == PartyState.Missed ? UiRole.Danger : UiRole.Muted;
        }

        // ------------------------------------------------------------------------------------------------- the buttons

        private void Done()
        {
            _local++;
        }

        public void NextPlace()
        {
            _place = (PartyPlace)(((int)_place + 1) % 3);
            _venueStreet = null;
            Done();
        }

        public void OtherVenue()
        {
            _venueStreet = null;
            Done();
        }

        public void NextTier()
        {
            _tier = (PartyTier)(((int)_tier + 1) % 3);
            Done();
        }

        public void Day(int by)
        {
            _dayOff = Math.Max(0, Math.Min(PartyCatalogue.AheadDays, _dayOff + by));
            Done();
        }

        public void Hour(int by)
        {
            _hour = Math.Max(PartyCatalogue.FirstHour, Math.Min(PartyCatalogue.LastHour, _hour + by));
            Done();
        }

        public void Toggle(int person)
        {
            if (!_picked.Remove(person))
            {
                _picked.Add(person);
            }
            Done();
        }

        public void Host()
        {
            PartyDirector d = PartyDirector.Instance;
            if (d != null && d.Host(Ask(d.World)) == PartyRefusal.None)
            {
                _picked.Clear();
            }
            Done();
        }

        public void NextBusiness()
        {
            _business++;
            if (_business >= Math.Max(1, CountBusinesses()))
            {
                _business = 0;
            }
            Done();
        }

        // The businesses with staff, as the page lists them.
        private int CountBusinesses()
        {
            PartyDirector d = PartyDirector.Instance;
            if (d == null)
            {
                return 0;
            }
            PartyGame.Businesses(_places);
            int n = 0;
            for (int i = 0; i < _places.Count; i++)
            {
                n += d.World.Staff(_places[i], _hands) > 0 ? 1 : 0;
            }
            return n;
        }

        // The chosen business's place, as the page lists them; null with none.
        private string ChosenPlace()
        {
            PartyDirector d = PartyDirector.Instance;
            if (d == null)
            {
                return null;
            }
            PartyGame.Businesses(_places);
            int n = 0;
            for (int i = 0; i < _places.Count; i++)
            {
                if (d.World.Staff(_places[i], _hands) > 0 && n++ == _business)
                {
                    return _places[i];
                }
            }
            return null;
        }

        public void NextKind()
        {
            _kind = (StaffEventKind)(((int)_kind + 1) % 3);
            _eventHour = PartyCatalogue.EventHourFrom[(int)_kind];
            Done();
        }

        public void EventDay(int by)
        {
            _eventDayOff = Math.Max(0, Math.Min(PartyCatalogue.AheadDays, _eventDayOff + by));
            Done();
        }

        public void EventHour(int by)
        {
            int k = (int)_kind;
            _eventHour = Math.Max(PartyCatalogue.EventHourFrom[k], Math.Min(PartyCatalogue.EventHourTo[k], _eventHour + by));
            Done();
        }

        public void Book()
        {
            PartyDirector d = PartyDirector.Instance;
            string place = ChosenPlace();
            if (d != null && place != null)
            {
                d.Book(EventAsk(d.World, place));
            }
            Done();
        }

        public void NextSchedule()
        {
            PartyDirector d = PartyDirector.Instance;
            string place = ChosenPlace();
            if (d != null && place != null && d.File != null)
            {
                StaffPlan plan = d.File.Plan(place, false);
                int at = Array.IndexOf(PartyCatalogue.ScheduleDays, plan != null ? plan.Every : 0);
                d.Schedule(place, PartyCatalogue.ScheduleDays[(Math.Max(0, at) + 1) % PartyCatalogue.ScheduleDays.Length], plan != null ? plan.Kind : _kind);
            }
            Done();
        }

        public void NextPlanKind()
        {
            PartyDirector d = PartyDirector.Instance;
            string place = ChosenPlace();
            StaffPlan plan = d != null && place != null && d.File != null ? d.File.Plan(place, false) : null;
            if (plan != null)
            {
                d.Schedule(place, plan.Every, (StaffEventKind)(((int)plan.Kind + 1) % 3));
            }
            Done();
        }

        public void OpenManagers()
        {
            Func<bool> open = Sessions.SessionBridges.OpenManagers;
            if (open == null || !open())
            {
                UI.Notification.Notifications.Show(UI.Notification.NotificationType.Warning, "reallife_party_no_managers_app", null, 6f, null, null, true, false);
            }
        }

        public void Cancel(int kind, int id)
        {
            PartyDirector d = PartyDirector.Instance;
            if (d != null)
            {
                if (kind == PartyPages.RowEvent)
                {
                    d.CancelEvent(id);
                }
                else
                {
                    d.CancelParty(id);
                }
            }
            Done();
        }

        public void Reply(int id, bool yes)
        {
            PartyDirector.Instance?.Reply(id, yes);
            Done();
        }

        public void Locate(int kind, int id)
        {
            PartyDirector d = PartyDirector.Instance;
            PartyFile file = d != null ? d.File : null;
            string street = null;
            int number = 0;
            if (file != null && kind == PartyPages.RowParty && file.Find(id) != null)
            {
                street = file.Find(id).Street;
                number = file.Find(id).Number;
            }
            else if (file != null && kind == PartyPages.RowInvite && file.Invitation(id) != null)
            {
                street = file.Invitation(id).Street;
                number = file.Invitation(id).Number;
            }
            if (string.IsNullOrEmpty(street) || !EstateGame.Locate(BusinessState.PlaceKey(street, number)))
            {
                UI.Notification.Notifications.Show(UI.Notification.NotificationType.Warning, "reallife_ven_note_no_place", null, 6f, null, null, true, false);
            }
            Done();
        }
    }
}
